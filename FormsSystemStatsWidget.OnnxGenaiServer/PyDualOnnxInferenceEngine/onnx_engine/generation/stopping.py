from __future__ import annotations

from dataclasses import dataclass, field
from typing import Sequence


@dataclass(slots=True, frozen=True)
class StopConfig:
    max_new_tokens: int = 256
    eos_token_ids: frozenset[int] = frozenset()
    stop_token_ids: frozenset[int] = frozenset()
    stop_strings: tuple[str, ...] = ()


@dataclass(slots=True)
class StopController:
    config: StopConfig
    generated_text: str = ""

    def push(self, token_id: int, token_text: str) -> bool:
        if token_id in self.config.eos_token_ids:
            return True

        if token_id in self.config.stop_token_ids:
            return True

        self.generated_text += token_text

        return any(
            marker and marker in self.generated_text
            for marker in self.config.stop_strings
        )

    def reached_limit(self, generated_count: int) -> bool:
        return generated_count >= self.config.max_new_tokens
