from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np

from .state import ContextState
from ..model.adapter import CausalOnnxAdapter
from ..model.dual_stage_adapter import DualStageOnnxAdapter
from ..errors import ModelValidationError


_SNAPSHOT_VERSION = 2
_STATE_SECTIONS = ("state", "stage0_state", "stage1_state")
_ORT_DTYPES = {
    "tensor(bool)": np.dtype(np.bool_),
    "tensor(float)": np.dtype(np.float32),
    "tensor(float16)": np.dtype(np.float16),
    "tensor(double)": np.dtype(np.float64),
    "tensor(int8)": np.dtype(np.int8),
    "tensor(uint8)": np.dtype(np.uint8),
    "tensor(int16)": np.dtype(np.int16),
    "tensor(uint16)": np.dtype(np.uint16),
    "tensor(int32)": np.dtype(np.int32),
    "tensor(uint32)": np.dtype(np.uint32),
    "tensor(int64)": np.dtype(np.int64),
    "tensor(uint64)": np.dtype(np.uint64),
}


@dataclass(slots=True, frozen=True)
class ContextSnapshot:
    token_ids: tuple[int, ...]
    messages: tuple[dict[str, Any], ...]
    generated_token_ids: tuple[int, ...]
    state_keys: tuple[str, ...]


class ContextSnapshotStore:
    """
    Explicit persistent context store.

    Snapshot creation copies CUDA state to CPU; nothing is persisted implicitly.
    """

    def save(
        self,
        path: str | Path,
        context: ContextState,
        adapter: CausalOnnxAdapter | DualStageOnnxAdapter,
        *,
        model_id: str | None = None,
    ) -> None:
        target = Path(path)
        target.parent.mkdir(parents=True, exist_ok=True)

        contract, bindings, input_info = self._adapter_layout(adapter)
        cpu_states = context.inference_state.to_cpu_snapshot()
        self._validate_state_sections(
            cpu_states,
            context.inference_state.is_partitioned,
            contract,
        )
        self._validate_state_arrays(cpu_states, bindings, input_info)

        metadata = {
            "format_version": _SNAPSHOT_VERSION,
            "model_id": model_id,
            "contract": contract,
            "token_ids": context.token_ids,
            "messages": context.messages,
            "generated_token_ids": context.generated_token_ids,
            "state_names": {
                section: sorted(cpu_states[section])
                for section in _STATE_SECTIONS
            },
        }

        np_payload: dict[str, Any] = {
            "__metadata__": np.asarray(
                json.dumps(metadata, ensure_ascii=False),
                dtype=np.str_,
            )
        }
        for section in _STATE_SECTIONS:
            for name, value in cpu_states[section].items():
                np_payload[self._archive_key(section, name)] = np.asarray(value)

        np.savez_compressed(target, **np_payload)

    def load(
        self,
        path: str | Path,
        adapter: CausalOnnxAdapter | DualStageOnnxAdapter,
        *,
        model_id: str | None = None,
    ) -> ContextState:
        source = Path(path)

        try:
            with np.load(source, allow_pickle=False) as archive:
                metadata = self._read_metadata(archive)
                if "format_version" not in metadata:
                    return self._load_legacy(archive, metadata, adapter)

                if metadata["format_version"] != _SNAPSHOT_VERSION:
                    raise ModelValidationError(
                        f"Unsupported context snapshot version: {metadata['format_version']}."
                    )

                contract, bindings, input_info = self._adapter_layout(adapter)
                if metadata.get("contract") != contract:
                    raise ModelValidationError(
                        "Context snapshot model, state bindings, or device mapping do not match."
                    )
                if metadata.get("model_id") != model_id:
                    raise ModelValidationError(
                        "Context snapshot belongs to a different model."
                    )

                state_names = metadata.get("state_names")
                if not isinstance(state_names, dict) or set(state_names) != set(_STATE_SECTIONS):
                    raise ModelValidationError("Context snapshot has invalid state metadata.")

                cpu_states: dict[str, dict[str, np.ndarray]] = {}
                expected_archive_keys = {"__metadata__"}
                for section in _STATE_SECTIONS:
                    names = state_names[section]
                    if (
                        not isinstance(names, list)
                        or any(not isinstance(name, str) or not name for name in names)
                        or len(names) != len(set(names))
                    ):
                        raise ModelValidationError(
                            f"Context snapshot has invalid names for {section}."
                        )
                    cpu_states[section] = {}
                    for name in names:
                        archive_key = self._archive_key(section, name)
                        expected_archive_keys.add(archive_key)
                        cpu_states[section][name] = np.asarray(archive[archive_key])

                if set(archive.files) != expected_archive_keys:
                    raise ModelValidationError(
                        "Context snapshot contains missing or unexpected arrays."
                    )

                partitioned = contract["kind"] == "dual_stage"
                self._validate_state_sections(cpu_states, partitioned, contract)
                self._validate_state_arrays(cpu_states, bindings, input_info)
                context = self._context_from_metadata(metadata)
                context.inference_state.restore_from_cpu_snapshot(
                    cpu_states,
                    contract["devices"],
                    is_partitioned=partitioned,
                )
                return context
        except ModelValidationError:
            raise
        except (OSError, EOFError, ValueError, KeyError, TypeError, json.JSONDecodeError) as exc:
            raise ModelValidationError(f"Invalid context snapshot '{source}': {exc}") from exc

    @staticmethod
    def _archive_key(section: str, name: str) -> str:
        return f"__state__{section}__{name}"

    @staticmethod
    def _read_metadata(archive: Any) -> dict[str, Any]:
        if "__metadata__" not in archive.files:
            raise ModelValidationError("Context snapshot has no metadata.")
        try:
            metadata = json.loads(str(archive["__metadata__"].item()))
        except (ValueError, TypeError, json.JSONDecodeError) as exc:
            raise ModelValidationError("Context snapshot metadata is invalid JSON.") from exc
        if not isinstance(metadata, dict):
            raise ModelValidationError("Context snapshot metadata must be an object.")
        return metadata

    def _load_legacy(
        self,
        archive: Any,
        metadata: dict[str, Any],
        adapter: CausalOnnxAdapter | DualStageOnnxAdapter,
    ) -> ContextState:
        if not isinstance(adapter, CausalOnnxAdapter) or metadata.get("is_partitioned"):
            raise ModelValidationError(
                "Unversioned partitioned snapshots cannot be restored safely."
            )

        contract, bindings, input_info = self._adapter_layout(adapter)
        names = metadata.get("state_keys")
        if (
            not isinstance(names, list)
            or any(not isinstance(name, str) or not name for name in names)
            or len(names) != len(set(names))
        ):
            raise ModelValidationError("Legacy context snapshot has invalid state names.")
        if set(archive.files) != {"__metadata__", *names}:
            raise ModelValidationError("Legacy context snapshot contains unexpected arrays.")

        cpu_states = {
            "state": {name: np.asarray(archive[name]) for name in names},
            "stage0_state": {},
            "stage1_state": {},
        }
        self._validate_state_sections(cpu_states, False, contract)
        self._validate_state_arrays(cpu_states, bindings, input_info)
        context = self._context_from_metadata(metadata)
        context.inference_state.restore_from_cpu_snapshot(
            cpu_states,
            contract["devices"],
            is_partitioned=False,
        )
        return context

    @staticmethod
    def _context_from_metadata(metadata: dict[str, Any]) -> ContextState:
        token_ids = metadata.get("token_ids")
        messages = metadata.get("messages")
        generated_token_ids = metadata.get("generated_token_ids")
        if (
            not isinstance(token_ids, list)
            or any(isinstance(value, bool) or not isinstance(value, int) for value in token_ids)
            or not isinstance(messages, list)
            or any(not isinstance(message, dict) for message in messages)
            or not isinstance(generated_token_ids, list)
            or any(
                isinstance(value, bool) or not isinstance(value, int)
                for value in generated_token_ids
            )
        ):
            raise ModelValidationError("Context snapshot conversation metadata is invalid.")
        return ContextState(
            token_ids=list(token_ids),
            messages=[dict(message) for message in messages],
            generated_token_ids=list(generated_token_ids),
        )

    @staticmethod
    def _adapter_layout(
        adapter: CausalOnnxAdapter | DualStageOnnxAdapter,
    ) -> tuple[dict[str, Any], dict[str, tuple[Any, ...]], dict[str, dict[str, Any]]]:
        if isinstance(adapter, DualStageOnnxAdapter):
            specs = {
                "stage0_state": (adapter.io_spec.stage0, adapter._stage0_input_info),
                "stage1_state": (adapter.io_spec.stage1, adapter._stage1_input_info),
            }
            state_bindings: dict[str, dict[str, list[str]]] = {}
            bindings: dict[str, tuple[Any, ...]] = {}
            input_info: dict[str, dict[str, Any]] = {}
            for section, (spec, inputs) in specs.items():
                categories = {
                    "kv": spec.kv_bindings,
                    "conv": spec.conv_bindings,
                    "recurrent": spec.recurrent_bindings,
                }
                state_bindings[section] = {
                    category: sorted(binding.state_name for binding in values)
                    for category, values in categories.items()
                }
                bindings[section] = tuple(
                    binding
                    for values in categories.values()
                    for binding in values
                )
                input_info[section] = inputs

            contract = {
                "kind": "dual_stage",
                "devices": {
                    "stage0_state": adapter.stage0_device_id,
                    "stage1_state": adapter.stage1_device_id,
                },
                "state_bindings": state_bindings,
                "boundary_outputs": sorted(adapter.io_spec.stage0.boundary_outputs),
                "boundary_inputs": sorted(adapter.io_spec.stage1.boundary_inputs),
            }
            return contract, bindings, input_info

        if isinstance(adapter, CausalOnnxAdapter):
            state_names = sorted(binding.state_name for binding in adapter.io_spec.past_inputs)
            contract = {
                "kind": "single_stage",
                "devices": {"state": adapter._device_id},
                "state_bindings": {"state": {"state": state_names}},
            }
            return (
                contract,
                {"state": tuple(adapter.io_spec.past_inputs)},
                {"state": adapter._input_info},
            )

        raise ModelValidationError(
            f"Unsupported adapter for context snapshots: {type(adapter).__name__}."
        )

    @staticmethod
    def _validate_state_sections(
        states: dict[str, dict[str, Any]],
        is_partitioned: bool,
        contract: dict[str, Any],
    ) -> None:
        expected_sections = set(_STATE_SECTIONS)
        if set(states) != expected_sections:
            raise ModelValidationError("Context snapshot state sections are invalid.")

        expected_partitioned = contract["kind"] == "dual_stage"
        has_state = any(states[section] for section in _STATE_SECTIONS)
        if expected_partitioned:
            if states["state"]:
                raise ModelValidationError("Partitioned context contains single-model state.")
            if has_state and not is_partitioned:
                raise ModelValidationError("Partitioned state is not marked as partitioned.")
            sections = ("stage0_state", "stage1_state")
        else:
            if is_partitioned or states["stage0_state"] or states["stage1_state"]:
                raise ModelValidationError("Single-model context contains partitioned state.")
            sections = ("state",)

        if not has_state:
            return

        state_bindings = contract["state_bindings"]
        for section in sections:
            if expected_partitioned:
                expected_names = {
                    name
                    for names in state_bindings[section].values()
                    for name in names
                }
            else:
                expected_names = set(state_bindings[section]["state"])
            if set(states[section]) != expected_names:
                raise ModelValidationError(
                    f"State names in {section} do not match the model bindings."
                )

    @staticmethod
    def _validate_state_arrays(
        states: dict[str, dict[str, np.ndarray]],
        bindings: dict[str, tuple[Any, ...]],
        input_info: dict[str, dict[str, Any]],
    ) -> None:
        for section, section_bindings in bindings.items():
            arrays = states[section]
            for binding in section_bindings:
                if binding.state_name not in arrays:
                    continue
                info = input_info[section].get(binding.input_name)
                if info is None:
                    raise ModelValidationError(
                        f"Snapshot state input '{binding.input_name}' is missing from the model."
                    )
                array = np.asarray(arrays[binding.state_name])
                if array.dtype.hasobject:
                    raise ModelValidationError(
                        f"Snapshot state '{binding.state_name}' has an invalid object dtype."
                    )
                expected_shape = info.shape
                if len(array.shape) != len(expected_shape):
                    raise ModelValidationError(
                        f"Snapshot state '{binding.state_name}' has an incompatible rank."
                    )
                for actual, expected in zip(array.shape, expected_shape, strict=True):
                    if isinstance(expected, int) and expected > 0 and actual != expected:
                        raise ModelValidationError(
                            f"Snapshot state '{binding.state_name}' has an incompatible shape."
                        )
                expected_dtype = _ORT_DTYPES.get(info.type)
                if expected_dtype is not None and array.dtype != expected_dtype:
                    raise ModelValidationError(
                        f"Snapshot state '{binding.state_name}' has an incompatible dtype."
                    )
