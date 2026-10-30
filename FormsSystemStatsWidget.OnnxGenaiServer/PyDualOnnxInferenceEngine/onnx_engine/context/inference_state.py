from __future__ import annotations

from collections.abc import Mapping
from dataclasses import dataclass, field
from typing import Any

import onnxruntime as ort


@dataclass(slots=True)
class InferenceState:
    """
    Runtime inference state container.

    Owns all model-execution state (KV, conv, recurrent) as CUDA-resident
    OrtValue dictionaries. This is the single source of truth for runtime
    state — ContextState delegates to this class.

    Supports:
      - single ONNX model (one state dict)
      - Stage0 + Stage1 partition (two state dicts, filled by the adapter)
      - KV / conv / recurrent state (any state kind the adapter produces)

    Only the state kinds actually present in the loaded model are populated.
    No concrete tensor names, shapes, or device mappings are assumed here.
    """

    # Single-model state: {tensor_name: OrtValue}
    # Populated by CausalOnnxAdapter._extract_state()
    state: dict[str, ort.OrtValue] = field(default_factory=dict)

    # Partition state (Stage0 + Stage1) — populated by a future
    # multi-stage adapter. Kept as separate dicts so the adapter can
    # manage each stage's state independently.
    stage0_state: dict[str, ort.OrtValue] = field(default_factory=dict)
    stage1_state: dict[str, ort.OrtValue] = field(default_factory=dict)

    # Metadata: which state kind is active
    is_partitioned: bool = False

    @property
    def is_empty(self) -> bool:
        if self.is_partitioned:
            return not self.stage0_state and not self.stage1_state
        return not self.state

    def reset(self) -> None:
        """Clear all state. The model session remains loaded."""
        self.state.clear()
        self.stage0_state.clear()
        self.stage1_state.clear()
        self.is_partitioned = False

    def to_cpu(self) -> dict[str, Any]:
        """
        Convert all state to CPU NumPy arrays for snapshot persistence.
        This is a device → host transfer; only used for explicit save.
        """
        result: dict[str, Any] = {}
        for name, value in self.state.items():
            result[name] = value.numpy()
        for name, value in self.stage0_state.items():
            result[f"stage0.{name}"] = value.numpy()
        for name, value in self.stage1_state.items():
            result[f"stage1.{name}"] = value.numpy()
        return result

    def to_cpu_snapshot(self) -> dict[str, dict[str, Any]]:
        """Copy each state namespace separately for an explicit snapshot."""
        return {
            "state": {name: value.numpy() for name, value in self.state.items()},
            "stage0_state": {
                name: value.numpy() for name, value in self.stage0_state.items()
            },
            "stage1_state": {
                name: value.numpy() for name, value in self.stage1_state.items()
            },
        }

    def restore_from_cpu_snapshot(
        self,
        states: Mapping[str, Mapping[str, Any]],
        device_ids: Mapping[str, int],
        *,
        is_partitioned: bool,
    ) -> None:
        """Restore named state dictionaries onto their assigned CUDA devices."""
        import numpy as np

        sections = {"state", "stage0_state", "stage1_state"}
        if set(states) != sections:
            raise ValueError("Snapshot must contain all three state sections.")

        if is_partitioned:
            if states["state"]:
                raise ValueError("Partitioned snapshot contains single-model state.")
            required_devices = {"stage0_state", "stage1_state"}
        else:
            if states["stage0_state"] or states["stage1_state"]:
                raise ValueError("Single-model snapshot contains partitioned state.")
            required_devices = {"state"}

        if set(device_ids) != required_devices:
            raise ValueError("Snapshot device mapping does not match its state layout.")
        if any(
            isinstance(device_id, bool)
            or not isinstance(device_id, int)
            or device_id < 0
            for device_id in device_ids.values()
        ):
            raise ValueError("Snapshot device IDs must be non-negative integers.")

        restored: dict[str, dict[str, ort.OrtValue]] = {
            section: {} for section in sections
        }
        for section in sections:
            device_id = device_ids.get(section)
            if device_id is None:
                continue
            for name, value in states[section].items():
                if not isinstance(name, str) or not name:
                    raise ValueError("Snapshot state names must be non-empty strings.")
                array = np.asarray(value)
                if array.dtype.hasobject:
                    raise ValueError(f"Snapshot state '{name}' has an invalid object dtype.")
                restored[section][name] = ort.OrtValue.ortvalue_from_numpy(
                    np.ascontiguousarray(array), "cuda", device_id
                )

        self.state = restored["state"]
        self.stage0_state = restored["stage0_state"]
        self.stage1_state = restored["stage1_state"]
        self.is_partitioned = is_partitioned

    def from_cpu(
        self,
        arrays: dict[str, Any],
        device_id: int,
    ) -> None:
        """
        Restore state from CPU NumPy arrays (snapshot load).
        This is a host → device transfer.
        """
        import numpy as np

        self.state.clear()
        self.stage0_state.clear()
        self.stage1_state.clear()

        for name, array in arrays.items():
            arr = np.ascontiguousarray(array)
            value = ort.OrtValue.ortvalue_from_numpy(arr, "cuda", device_id)
            if name.startswith("stage0."):
                self.stage0_state[name[7:]] = value
            elif name.startswith("stage1."):
                self.stage1_state[name[7:]] = value
            else:
                self.state[name] = value

        self.is_partitioned = bool(self.stage0_state or self.stage1_state)
