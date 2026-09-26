from __future__ import annotations

import re
from typing import Any, Iterable

from .io_spec import ModelIoSpec, PastBinding
from ..errors import ModelValidationError


class OnnxGraphInspector:
    """Conservative I/O inspection and best-effort causal decoder inference."""

    _PAST_RE = re.compile(r"(past|cache|key_values|kv)", re.IGNORECASE)
    _PRESENT_RE = re.compile(r"(present|past_key_values|key_values|cache)", re.IGNORECASE)

    def infer(self, session: Any) -> ModelIoSpec:
        inputs = session.get_inputs()
        outputs = session.get_outputs()

        input_names = [item.name for item in inputs]
        output_names = [item.name for item in outputs]

        input_ids = self._choose(input_names, ("input_ids", "inputIds"))
        if input_ids is None:
            raise ModelValidationError(
                f"Could not infer input_ids from model inputs: {input_names}"
            )

        logits = self._choose(
            output_names,
            ("logits", "lm_logits", "output", "last_logits"),
        )
        if logits is None:
            logits = self._find_by_keywords(output_names, ("logit",))
        if logits is None:
            raise ModelValidationError(
                f"Could not infer logits output from model outputs: {output_names}"
            )

        attention_mask = self._choose(
            input_names,
            ("attention_mask", "attentionMask", "attention-mask"),
        )
        position_ids = self._choose(
            input_names,
            ("position_ids", "positionIds", "position-ids"),
        )
        cache_position = self._choose(
            input_names,
            ("cache_position", "cachePosition", "cache-position"),
        )

        past_inputs = [
            name for name in input_names
            if name not in {
                input_ids,
                attention_mask,
                position_ids,
                cache_position,
            }
            and self._PAST_RE.search(name)
        ]

        present_outputs = [
            name for name in output_names
            if name != logits and self._PRESENT_RE.search(name)
        ]

        bindings: list[PastBinding] = []
        for input_name, output_name in zip(past_inputs, present_outputs, strict=False):
            bindings.append(
                PastBinding(
                    input_name=input_name,
                    output_name=output_name,
                    state_name=input_name,
                )
            )

        return ModelIoSpec(
            input_ids=input_ids,
            logits_output=logits,
            attention_mask=attention_mask,
            position_ids=position_ids,
            cache_position=cache_position,
            past_inputs=tuple(bindings),
        )

    @staticmethod
    def _choose(names: Iterable[str], candidates: Iterable[str]) -> str | None:
        available = list(names)
        lowered = {name.lower(): name for name in available}

        for candidate in candidates:
            if candidate.lower() in lowered:
                return lowered[candidate.lower()]

        return None

    @staticmethod
    def _find_by_keywords(names: Iterable[str], keywords: Iterable[str]) -> str | None:
        lowered = [(name, name.lower()) for name in names]
        for name, value in lowered:
            if any(keyword in value for keyword in keywords):
                return name
        return None
