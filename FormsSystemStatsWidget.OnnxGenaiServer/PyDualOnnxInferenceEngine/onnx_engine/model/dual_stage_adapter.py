from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping

import numpy as np
import onnxruntime as ort

from ..errors import ModelValidationError
from .io_spec import DualStageIoSpec, StageIoSpec, StateBinding
from .position_ids import build_text_position_ids


@dataclass(slots=True)
class DualStageStepResult:
    """
    Result of one dual-stage step (prefill or decode).

    logits:        Stage 1 logits (CPU NumPy array)
    hidden_states: Stage 1 hidden states (CPU NumPy array)
    stage0_state:  Stage 0 state (KV/conv/recurrent) as CUDA OrtValue dict
    stage1_state:  Stage 1 state (KV/conv/recurrent) as CUDA OrtValue dict
    sequence_length: total sequence length after this step
    """
    logits: np.ndarray
    hidden_states: np.ndarray
    stage0_state: dict[str, ort.OrtValue]
    stage1_state: dict[str, ort.OrtValue]
    sequence_length: int


class DualStageOnnxAdapter:
    """
    Dual-stage causal decoder adapter.

    Owns two ONNX Runtime sessions:
      Stage 0 (GPU 0): embedding + layers 0..41 -> boundary tensors
      Stage 1 (GPU 1): layers 42..63 + norm + lm_head -> logits

    Each stage has its own device, its own state dict, and its own I/O
    contract (StageIoSpec). State is bound explicitly by name — never
    positionally. Boundary tensors are temporary step data (not persistent
    state) and are passed from Stage 0 outputs to Stage 1 inputs.

    The adapter is model-agnostic: all tensor names, shapes, and device
    assignments come from the DualStageIoSpec (built by OnnxGraphInspector
    or loaded from model_io.json).
    """

    def __init__(
        self,
        stage0_session: ort.InferenceSession,
        stage1_session: ort.InferenceSession,
        stage0_device_id: int,
        stage1_device_id: int,
        io_spec: DualStageIoSpec,
        model_type: str | None = None,
        symbolic_dimensions: Mapping[str, int] | None = None,
    ) -> None:
        self._stage0_session = stage0_session
        self._stage1_session = stage1_session
        self._stage0_device_id = stage0_device_id
        self._stage1_device_id = stage1_device_id
        self._io_spec = io_spec
        self._model_type = model_type
        self._symbolic_dimensions = dict(symbolic_dimensions or {})
        if any(value < 1 for value in self._symbolic_dimensions.values()):
            raise ValueError("Symbolic state dimensions must be positive integers.")

        self._stage0_input_info = {
            item.name: item for item in stage0_session.get_inputs()
        }
        self._stage0_output_info = {
            item.name: item for item in stage0_session.get_outputs()
        }
        self._stage1_input_info = {
            item.name: item for item in stage1_session.get_inputs()
        }
        self._stage1_output_info = {
            item.name: item for item in stage1_session.get_outputs()
        }

        self._validate_spec()

    @property
    def io_spec(self) -> DualStageIoSpec:
        return self._io_spec

    @property
    def stage0_device_id(self) -> int:
        return self._stage0_device_id

    @property
    def stage1_device_id(self) -> int:
        return self._stage1_device_id

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def prefill(
        self,
        input_ids: np.ndarray,
        *,
        stage0_state: Mapping[str, ort.OrtValue] | None = None,
        stage1_state: Mapping[str, ort.OrtValue] | None = None,
        position_ids: np.ndarray | None = None,
    ) -> DualStageStepResult:
        self._validate_ids(input_ids)
        sequence_length = int(input_ids.shape[1])
        position_ids = self._resolve_position_ids(
            position_ids,
            start_position=0,
            sequence_length=sequence_length,
            batch_size=int(input_ids.shape[0]),
        )

        stage0_feeds = self._build_stage0_inputs(
            input_ids,
            sequence_length=sequence_length,
            state=stage0_state,
            is_decode=False,
            position_ids=position_ids,
        )
        stage0_outputs = self._run_stage0(stage0_feeds)

        boundary = self._extract_boundary(stage0_outputs)
        next_stage0_state = self._extract_stage_state(
            stage0_outputs,
            self._io_spec.stage0,
            self._stage0_output_info,
        )

        stage1_feeds = self._build_stage1_inputs(
            sequence_length=sequence_length,
            boundary=boundary,
            state=stage1_state,
            is_decode=False,
            position_ids=position_ids,
        )
        stage1_outputs = self._run_stage1(stage1_feeds)

        logits = self._extract_output(
            stage1_outputs,
            self._io_spec.stage1.logits_output,
            "logits",
        )
        hidden_states = self._extract_output(
            stage1_outputs,
            self._io_spec.stage1.hidden_states_output,
            "hidden_states",
        )
        next_stage1_state = self._extract_stage_state(
            stage1_outputs,
            self._io_spec.stage1,
            self._stage1_output_info,
        )

        return DualStageStepResult(
            logits=logits,
            hidden_states=hidden_states,
            stage0_state=next_stage0_state,
            stage1_state=next_stage1_state,
            sequence_length=sequence_length,
        )

    def decode(
        self,
        token_id: int,
        *,
        position: int,
        stage0_state: Mapping[str, ort.OrtValue],
        stage1_state: Mapping[str, ort.OrtValue],
        position_ids: np.ndarray | None = None,
    ) -> DualStageStepResult:
        input_ids = np.asarray([[token_id]], dtype=np.int64)
        sequence_length = position + 1
        position_ids = self._resolve_position_ids(
            position_ids,
            start_position=position,
            sequence_length=1,
            batch_size=1,
        )

        stage0_feeds = self._build_stage0_inputs(
            input_ids,
            sequence_length=sequence_length,
            state=stage0_state,
            is_decode=True,
            position=position,
            position_ids=position_ids,
        )
        stage0_outputs = self._run_stage0(stage0_feeds)

        boundary = self._extract_boundary(stage0_outputs)
        next_stage0_state = self._extract_stage_state(
            stage0_outputs,
            self._io_spec.stage0,
            self._stage0_output_info,
        )

        stage1_feeds = self._build_stage1_inputs(
            sequence_length=sequence_length,
            boundary=boundary,
            state=stage1_state,
            is_decode=True,
            position=position,
            position_ids=position_ids,
        )
        stage1_outputs = self._run_stage1(stage1_feeds)

        logits = self._extract_output(
            stage1_outputs,
            self._io_spec.stage1.logits_output,
            "logits",
        )
        hidden_states = self._extract_output(
            stage1_outputs,
            self._io_spec.stage1.hidden_states_output,
            "hidden_states",
        )
        next_stage1_state = self._extract_stage_state(
            stage1_outputs,
            self._io_spec.stage1,
            self._stage1_output_info,
        )

        return DualStageStepResult(
            logits=logits,
            hidden_states=hidden_states,
            stage0_state=next_stage0_state,
            stage1_state=next_stage1_state,
            sequence_length=sequence_length,
        )

    # ------------------------------------------------------------------
    # State transfer (CPU <-> CUDA)
    # ------------------------------------------------------------------

    def stage0_state_to_cpu(
        self,
        state: Mapping[str, ort.OrtValue],
    ) -> dict[str, np.ndarray]:
        return {name: value.numpy() for name, value in state.items()}

    def stage1_state_to_cpu(
        self,
        state: Mapping[str, ort.OrtValue],
    ) -> dict[str, np.ndarray]:
        return {name: value.numpy() for name, value in state.items()}

    def stage0_state_from_cpu(
        self,
        state: Mapping[str, np.ndarray],
    ) -> dict[str, ort.OrtValue]:
        return {
            name: self._to_cuda_value(value, self._stage0_device_id)
            for name, value in state.items()
        }

    def stage1_state_from_cpu(
        self,
        state: Mapping[str, np.ndarray],
    ) -> dict[str, ort.OrtValue]:
        return {
            name: self._to_cuda_value(value, self._stage1_device_id)
            for name, value in state.items()
        }

    # ------------------------------------------------------------------
    # Internal: Stage 0
    # ------------------------------------------------------------------

    def _build_stage0_inputs(
        self,
        input_ids: np.ndarray,
        *,
        sequence_length: int,
        state: Mapping[str, ort.OrtValue] | None,
        is_decode: bool,
        position: int | None = None,
        position_ids: np.ndarray | None = None,
    ) -> dict[str, ort.OrtValue]:
        spec = self._io_spec.stage0
        feeds: dict[str, ort.OrtValue] = {}

        if spec.input_ids:
            feeds[spec.input_ids] = self._to_cuda_value(
                input_ids, self._stage0_device_id
            )

        if spec.attention_mask:
            mask = np.ones((1, sequence_length), dtype=np.int64)
            feeds[spec.attention_mask] = self._to_cuda_value(
                mask, self._stage0_device_id
            )

        if spec.position_ids:
            position_ids = self._validate_position_ids(
                position_ids,
                spec.position_ids,
                self._stage0_input_info,
            )
            feeds[spec.position_ids] = self._to_cuda_value(
                position_ids, self._stage0_device_id
            )

        if spec.cache_position:
            if is_decode and position is not None:
                cache_position = np.asarray([position], dtype=np.int64)
            else:
                cache_position = np.arange(sequence_length, dtype=np.int64)
            feeds[spec.cache_position] = self._to_cuda_value(
                cache_position, self._stage0_device_id
            )

        self._bind_state_inputs(
            feeds,
            spec,
            state,
            self._stage0_input_info,
            self._stage0_output_info,
            self._stage0_device_id,
        )

        return feeds

    def _run_stage0(
        self,
        feeds: Mapping[str, ort.OrtValue],
    ) -> dict[str, ort.OrtValue]:
        spec = self._io_spec.stage0
        output_names = self._stage0_output_names(spec)
        return self._run_session(
            self._stage0_session,
            feeds,
            output_names,
            self._stage0_output_info,
            self._stage0_device_id,
        )

    @staticmethod
    def _stage0_output_names(spec: StageIoSpec) -> list[str]:
        names: list[str] = list(spec.boundary_outputs)
        for binding in spec.all_state_bindings:
            names.append(binding.output_name)
        return _dedupe(names)

    # ------------------------------------------------------------------
    # Internal: Stage 1
    # ------------------------------------------------------------------

    def _build_stage1_inputs(
        self,
        *,
        sequence_length: int,
        boundary: Mapping[str, ort.OrtValue],
        state: Mapping[str, ort.OrtValue] | None,
        is_decode: bool,
        position: int | None = None,
        position_ids: np.ndarray | None = None,
    ) -> dict[str, ort.OrtValue]:
        spec = self._io_spec.stage1
        feeds: dict[str, ort.OrtValue] = {}

        if spec.attention_mask:
            mask = np.ones((1, sequence_length), dtype=np.int64)
            feeds[spec.attention_mask] = self._to_cuda_value(
                mask, self._stage1_device_id
            )

        if spec.position_ids:
            position_ids = self._validate_position_ids(
                position_ids,
                spec.position_ids,
                self._stage1_input_info,
            )
            feeds[spec.position_ids] = self._to_cuda_value(
                position_ids, self._stage1_device_id
            )

        if spec.cache_position:
            if is_decode and position is not None:
                cache_position = np.asarray([position], dtype=np.int64)
            else:
                cache_position = np.arange(sequence_length, dtype=np.int64)
            feeds[spec.cache_position] = self._to_cuda_value(
                cache_position, self._stage1_device_id
            )

        for name in spec.boundary_inputs:
            if name not in boundary:
                raise ModelValidationError(
                    f"Boundary tensor '{name}' was not produced by Stage 0."
                )
            feeds[name] = boundary[name]

        self._bind_state_inputs(
            feeds,
            spec,
            state,
            self._stage1_input_info,
            self._stage1_output_info,
            self._stage1_device_id,
        )

        return feeds

    def _run_stage1(
        self,
        feeds: Mapping[str, ort.OrtValue],
    ) -> dict[str, ort.OrtValue]:
        spec = self._io_spec.stage1
        output_names: list[str] = []
        if spec.logits_output:
            output_names.append(spec.logits_output)
        if spec.hidden_states_output:
            output_names.append(spec.hidden_states_output)
        for binding in spec.all_state_bindings:
            output_names.append(binding.output_name)
        return self._run_session(
            self._stage1_session,
            feeds,
            _dedupe(output_names),
            self._stage1_output_info,
            self._stage1_device_id,
        )

    # ------------------------------------------------------------------
    # Internal: shared helpers
    # ------------------------------------------------------------------

    def _bind_state_inputs(
        self,
        feeds: dict[str, ort.OrtValue],
        spec: StageIoSpec,
        state: Mapping[str, ort.OrtValue] | None,
        input_info: dict[str, Any],
        output_info: dict[str, Any],
        device_id: int,
    ) -> None:
        categories = (
            (spec.kv_bindings, spec.kv_sequence_axis),
            (spec.conv_bindings, None),
            (spec.recurrent_bindings, None),
        )
        for bindings, sequence_axis in categories:
            for binding in bindings:
                if state is not None and binding.state_name in state:
                    feeds[binding.input_name] = state[binding.state_name]
                else:
                    feeds[binding.input_name] = self._empty_past(
                        binding,
                        input_info,
                        output_info,
                        device_id,
                        sequence_axis,
                    )

    def _empty_past(
        self,
        binding: StateBinding,
        input_info: dict[str, Any],
        output_info: dict[str, Any],
        device_id: int,
        sequence_axis: int | None,
    ) -> ort.OrtValue:
        info = input_info.get(binding.input_name)
        if info is None:
            raise ModelValidationError(f"Unknown state input: {binding.input_name}")
        output = output_info.get(binding.output_name)
        if output is None:
            raise ModelValidationError(f"Unknown state output: {binding.output_name}")

        shape = list(info.shape)
        if sequence_axis is not None and len(shape) > sequence_axis:
            shape[sequence_axis] = 0

        output_shape = output.shape
        for index, value in enumerate(shape):
            if not isinstance(value, int) or value < 0:
                if index == sequence_axis:
                    continue
                if isinstance(value, str) and value in self._symbolic_dimensions:
                    shape[index] = self._symbolic_dimensions[value]
                    continue
                output_dimension = (
                    output_shape[index]
                    if len(output_shape) == len(shape)
                    else None
                )
                if isinstance(output_dimension, int) and output_dimension >= 0:
                    shape[index] = output_dimension
                elif index == 0:
                    shape[index] = 1
                else:
                    raise ModelValidationError(
                        f"Cannot infer fixed dimension {index} for state input "
                        f"'{binding.input_name}' from its graph input/output shapes."
                    )

        dtype = self._numpy_dtype(info.type)
        return self._to_cuda_value(np.zeros(tuple(shape), dtype=dtype), device_id)

    def _run_session(
        self,
        session: ort.InferenceSession,
        feeds: Mapping[str, ort.OrtValue],
        output_names: list[str],
        output_info: dict[str, Any],
        device_id: int,
    ) -> dict[str, ort.OrtValue]:
        binding = session.io_binding()

        for name, value in feeds.items():
            binding.bind_ortvalue_input(name, value)

        for name in output_names:
            if name not in output_info:
                raise ModelValidationError(
                    f"Configured output '{name}' does not exist."
                )
            binding.bind_output(name, "cuda", device_id)

        session.run_with_iobinding(binding)
        results = binding.get_outputs()

        return dict(zip(output_names, results, strict=False))

    @staticmethod
    def _validate_position_ids(
        position_ids: np.ndarray | None,
        input_name: str,
        input_info: dict[str, Any],
    ) -> np.ndarray:
        if position_ids is None:
            raise ModelValidationError(
                f"Position IDs for '{input_name}' must be provided explicitly; "
                "their model-specific axis semantics are not inferred."
            )

        value = np.asarray(position_ids)
        info = input_info.get(input_name)
        if info is None:
            raise ModelValidationError(f"Unknown position_ids input: {input_name}")

        expected_shape = info.shape
        if value.ndim != len(expected_shape):
            raise ModelValidationError(
                f"Position IDs for '{input_name}' have rank {value.ndim}; "
                f"expected rank {len(expected_shape)} from the ONNX graph."
            )
        for axis, expected in enumerate(expected_shape):
            if isinstance(expected, int) and expected >= 0 and value.shape[axis] != expected:
                raise ModelValidationError(
                    f"Position IDs for '{input_name}' have shape {value.shape}; "
                    f"axis {axis} must be {expected} according to the ONNX graph."
                )

        expected_dtype = DualStageOnnxAdapter._numpy_dtype(info.type)
        if value.dtype != expected_dtype:
            raise ModelValidationError(
                f"Position IDs for '{input_name}' must use {expected_dtype}, "
                f"got {value.dtype}."
            )
        return np.ascontiguousarray(value)

    def _resolve_position_ids(
        self,
        position_ids: np.ndarray | None,
        *,
        start_position: int,
        sequence_length: int,
        batch_size: int,
    ) -> np.ndarray | None:
        if position_ids is not None or self._model_type is None:
            return position_ids

        for spec, input_info in (
            (self._io_spec.stage0, self._stage0_input_info),
            (self._io_spec.stage1, self._stage1_input_info),
        ):
            if spec.position_ids:
                info = input_info.get(spec.position_ids)
                if info is None:
                    raise ModelValidationError(
                        f"Unknown position_ids input: {spec.position_ids}"
                    )
                return build_text_position_ids(
                    self._model_type,
                    info.shape,
                    start_position=start_position,
                    sequence_length=sequence_length,
                    batch_size=batch_size,
                )
        return None

    def _extract_boundary(
        self,
        stage0_outputs: Mapping[str, ort.OrtValue],
    ) -> dict[str, ort.OrtValue]:
        result: dict[str, ort.OrtValue] = {}
        for name in self._io_spec.stage0.boundary_outputs:
            value = stage0_outputs.get(name)
            if value is None:
                raise ModelValidationError(
                    f"Boundary output '{name}' was not returned by Stage 0."
                )
            result[name] = value
        return result

    def _extract_stage_state(
        self,
        outputs: Mapping[str, ort.OrtValue],
        spec: StageIoSpec,
        output_info: dict[str, Any],
    ) -> dict[str, ort.OrtValue]:
        result: dict[str, ort.OrtValue] = {}
        for binding in spec.all_state_bindings:
            value = outputs.get(binding.output_name)
            if value is None:
                raise ModelValidationError(
                    f"State output '{binding.output_name}' was not returned."
                )
            result[binding.state_name] = value
        return result

    def _extract_output(
        self,
        outputs: Mapping[str, ort.OrtValue],
        name: str | None,
        label: str,
    ) -> np.ndarray:
        if name is None:
            raise ModelValidationError(f"Stage 1 has no {label} output configured.")
        value = outputs.get(name)
        if value is None:
            raise ModelValidationError(f"{label} output '{name}' was not returned.")
        return value.numpy()

    def _to_cuda_value(self, value: np.ndarray, device_id: int) -> ort.OrtValue:
        array = np.ascontiguousarray(value)
        return ort.OrtValue.ortvalue_from_numpy(array, "cuda", device_id)

    @staticmethod
    def _numpy_dtype(ort_type: str) -> np.dtype:
        mapping = {
            "tensor(float)": np.dtype(np.float32),
            "tensor(float16)": np.dtype(np.float16),
            "tensor(double)": np.dtype(np.float64),
            "tensor(int64)": np.dtype(np.int64),
            "tensor(int32)": np.dtype(np.int32),
            "tensor(int16)": np.dtype(np.int16),
            "tensor(int8)": np.dtype(np.int8),
            "tensor(uint8)": np.dtype(np.uint8),
            "tensor(bool)": np.dtype(np.bool_),
        }
        try:
            return mapping[ort_type]
        except KeyError as exc:
            raise ModelValidationError(
                f"Unsupported ONNX tensor type: {ort_type}"
            ) from exc

    def _validate_ids(self, input_ids: np.ndarray) -> None:
        if input_ids.ndim != 2:
            raise ValueError(
                f"input_ids must have shape [batch, sequence], got {input_ids.shape}"
            )
        if input_ids.shape[0] != 1:
            raise ValueError("This core currently manages one sequence per context.")

    def _validate_spec(self) -> None:
        s0 = self._io_spec.stage0
        s1 = self._io_spec.stage1

        if not s0.has_input_ids:
            raise ModelValidationError("Stage 0 must have input_ids.")
        if not s0.boundary_outputs:
            raise ModelValidationError("Stage 0 must have boundary outputs.")
        if not s1.boundary_inputs:
            raise ModelValidationError("Stage 1 must have boundary inputs.")
        if not s1.has_logits:
            raise ModelValidationError("Stage 1 must have logits output.")

        if set(s0.boundary_outputs) != set(s1.boundary_inputs):
            raise ModelValidationError(
                f"Boundary mismatch: Stage 0 outputs {sorted(s0.boundary_outputs)} "
                f"vs Stage 1 inputs {sorted(s1.boundary_inputs)}"
            )

        missing_s0_inputs = [
            name
            for name in (
                *([s0.input_ids] if s0.input_ids else []),
                *([s0.attention_mask] if s0.attention_mask else []),
                *([s0.position_ids] if s0.position_ids else []),
                *([s0.cache_position] if s0.cache_position else []),
                *(b.input_name for b in s0.all_state_bindings),
            )
            if name and name not in self._stage0_input_info
        ]
        missing_s0_outputs = [
            name
            for name in (*s0.boundary_outputs, *(b.output_name for b in s0.all_state_bindings))
            if name not in self._stage0_output_info
        ]
        missing_s1_inputs = [
            name
            for name in (
                *([s1.attention_mask] if s1.attention_mask else []),
                *([s1.position_ids] if s1.position_ids else []),
                *([s1.cache_position] if s1.cache_position else []),
                *s1.boundary_inputs,
                *(b.input_name for b in s1.all_state_bindings),
            )
            if name and name not in self._stage1_input_info
        ]
        missing_s1_outputs = [
            name
            for name in (
                *([s1.logits_output] if s1.logits_output else []),
                *([s1.hidden_states_output] if s1.hidden_states_output else []),
                *(b.output_name for b in s1.all_state_bindings),
            )
            if name and name not in self._stage1_output_info
        ]

        if missing_s0_inputs:
            raise ModelValidationError(f"Stage 0 inputs do not exist: {missing_s0_inputs}")
        if missing_s0_outputs:
            raise ModelValidationError(f"Stage 0 outputs do not exist: {missing_s0_outputs}")
        if missing_s1_inputs:
            raise ModelValidationError(f"Stage 1 inputs do not exist: {missing_s1_inputs}")
        if missing_s1_outputs:
            raise ModelValidationError(f"Stage 1 outputs do not exist: {missing_s1_outputs}")


def _dedupe(names: list[str]) -> list[str]:
    seen: set[str] = set()
    result: list[str] = []
    for name in names:
        if name not in seen:
            seen.add(name)
            result.append(name)
    return result
