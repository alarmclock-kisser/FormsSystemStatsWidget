from __future__ import annotations

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
