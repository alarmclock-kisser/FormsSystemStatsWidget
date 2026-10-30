from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(slots=True, frozen=True)
class StateBinding:
    """Maps a state input tensor to its corresponding output and state key."""
    input_name: str
    output_name: str
    state_name: str


@dataclass(slots=True, frozen=True)
class StageIoSpec:
    """
    I/O contract for one stage of a (dual-stage) ONNX model.

    Stage 0: has input_ids, no logits, no hidden_states, has boundary_outputs.
    Stage 1: has boundary_inputs, has logits + hidden_states, no input_ids.

    State is split into three categories: KV, Conv, Recurrent.
    """
    # Optional primary inputs
    input_ids: str | None = None
    attention_mask: str | None = None
    position_ids: str | None = None
    cache_position: str | None = None

    # Optional primary outputs
    logits_output: str | None = None
    hidden_states_output: str | None = None

    # Boundary tensors (Stage 0 outputs → Stage 1 inputs)
    boundary_outputs: tuple[str, ...] = ()
    boundary_inputs: tuple[str, ...] = ()

    # State bindings — 3 categories
    kv_bindings: tuple[StateBinding, ...] = ()
    conv_bindings: tuple[StateBinding, ...] = ()
    recurrent_bindings: tuple[StateBinding, ...] = ()

    # Sequence axis for KV cache (axis where past_sequence_length lives)
    kv_sequence_axis: int = 2

    @property
    def all_state_bindings(self) -> tuple[StateBinding, ...]:
        return self.kv_bindings + self.conv_bindings + self.recurrent_bindings

    @property
    def has_input_ids(self) -> bool:
        return self.input_ids is not None

    @property
    def has_logits(self) -> bool:
        return self.logits_output is not None

    @property
    def is_stage0(self) -> bool:
        """Stage 0 has input_ids but no logits."""
        return self.has_input_ids and not self.has_logits

    @property
    def is_stage1(self) -> bool:
        """Stage 1 has logits but no input_ids."""
        return self.has_logits and not self.has_input_ids

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "StageIoSpec":
        def _parse_bindings(key: str) -> tuple[StateBinding, ...]:
            return tuple(
                StateBinding(
                    input_name=str(item["input"]),
                    output_name=str(item["output"]),
                    state_name=str(item.get("state_name", item["input"])),
                )
                for item in data.get(key, [])
            )

        return cls(
            input_ids=data.get("input_ids"),
            attention_mask=data.get("attention_mask"),
            position_ids=data.get("position_ids"),
            cache_position=data.get("cache_position"),
            logits_output=data.get("logits_output"),
            hidden_states_output=data.get("hidden_states_output"),
            boundary_outputs=tuple(data.get("boundary_outputs", [])),
            boundary_inputs=tuple(data.get("boundary_inputs", [])),
            kv_bindings=_parse_bindings("kv_bindings"),
            conv_bindings=_parse_bindings("conv_bindings"),
            recurrent_bindings=_parse_bindings("recurrent_bindings"),
            kv_sequence_axis=int(data.get("kv_sequence_axis", 2)),
        )

    def to_dict(self) -> dict[str, Any]:
        def _serialize(bindings: tuple[StateBinding, ...]) -> list[dict[str, str]]:
            return [
                {
                    "input": b.input_name,
                    "output": b.output_name,
                    "state_name": b.state_name,
                }
                for b in bindings
            ]

        result: dict[str, Any] = {}
        if self.input_ids:
            result["input_ids"] = self.input_ids
        if self.attention_mask:
            result["attention_mask"] = self.attention_mask
        if self.position_ids:
            result["position_ids"] = self.position_ids
        if self.cache_position:
            result["cache_position"] = self.cache_position
        if self.logits_output:
            result["logits_output"] = self.logits_output
        if self.hidden_states_output:
            result["hidden_states_output"] = self.hidden_states_output
        if self.boundary_outputs:
            result["boundary_outputs"] = list(self.boundary_outputs)
        if self.boundary_inputs:
            result["boundary_inputs"] = list(self.boundary_inputs)
        if self.kv_bindings:
            result["kv_bindings"] = _serialize(self.kv_bindings)
        if self.conv_bindings:
            result["conv_bindings"] = _serialize(self.conv_bindings)
        if self.recurrent_bindings:
            result["recurrent_bindings"] = _serialize(self.recurrent_bindings)
        if self.kv_sequence_axis != 2:
            result["kv_sequence_axis"] = self.kv_sequence_axis
        return result


@dataclass(slots=True, frozen=True)
class DualStageIoSpec:
    """
    Complete I/O contract for a dual-stage ONNX model.

    Stage 0: embedding + layers 0..41 → boundary tensors
    Stage 1: layers 42..63 + norm + lm_head → logits
    """
    stage0: StageIoSpec
    stage1: StageIoSpec

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "DualStageIoSpec":
        return cls(
            stage0=StageIoSpec.from_dict(data["stage0"]),
            stage1=StageIoSpec.from_dict(data["stage1"]),
        )

    def to_dict(self) -> dict[str, Any]:
        return {
            "stage0": self.stage0.to_dict(),
            "stage1": self.stage1.to_dict(),
        }


# Transitional names retained because the existing single-stage adapter still
# imports these types. They will be removed when the adapter migrates in Phase 4b.
PastBinding = StateBinding


@dataclass(slots=True, frozen=True)
class ModelIoSpec:
    input_ids: str
    logits_output: str
    attention_mask: str | None = None
    position_ids: str | None = None
    cache_position: str | None = None
    past_inputs: tuple[PastBinding, ...] = ()
    past_sequence_axis: int = 2
    logits_sequence_axis: int = 1

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "ModelIoSpec":
        past = tuple(
            PastBinding(
                input_name=str(item["input"]),
                output_name=str(item["output"]),
                state_name=str(item.get("state_name", item["input"])),
            )
            for item in data.get("past_inputs", [])
        )
        return cls(
            input_ids=str(data["input_ids"]),
            logits_output=str(data["logits_output"]),
            attention_mask=data.get("attention_mask"),
            position_ids=data.get("position_ids"),
            cache_position=data.get("cache_position"),
            past_inputs=past,
            past_sequence_axis=int(data.get("past_sequence_axis", 2)),
            logits_sequence_axis=int(data.get("logits_sequence_axis", 1)),
        )
