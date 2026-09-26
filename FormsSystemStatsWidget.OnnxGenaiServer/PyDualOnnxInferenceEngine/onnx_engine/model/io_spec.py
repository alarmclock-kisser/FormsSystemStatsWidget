from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(slots=True, frozen=True)
class PastBinding:
    input_name: str
    output_name: str
    state_name: str


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
