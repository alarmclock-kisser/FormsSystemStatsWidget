from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

import numpy as np
import onnxruntime as ort


@dataclass(slots=True)
class ContextState:
    token_ids: list[int] = field(default_factory=list)
    model_state: dict[str, ort.OrtValue] = field(default_factory=dict)
    messages: list[dict[str, Any]] = field(default_factory=list)
    generated_token_ids: list[int] = field(default_factory=list)

    @property
    def position(self) -> int:
        return len(self.token_ids)

    def append_tokens(self, token_ids: list[int]) -> None:
        self.token_ids.extend(int(value) for value in token_ids)

    def append_message(self, message: dict[str, Any]) -> None:
        self.messages.append(dict(message))

    def append_generated(self, token_id: int) -> None:
        value = int(token_id)
        self.generated_token_ids.append(value)
        self.token_ids.append(value)

    def clone_metadata_only(self) -> "ContextState":
        return ContextState(
            token_ids=list(self.token_ids),
            messages=[dict(message) for message in self.messages],
            generated_token_ids=list(self.generated_token_ids),
        )

    def cpu_state(self) -> dict[str, np.ndarray]:
        return {
            name: value.numpy()
            for name, value in self.model_state.items()
        }
