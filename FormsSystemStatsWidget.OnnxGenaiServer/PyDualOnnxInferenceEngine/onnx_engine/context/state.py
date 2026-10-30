from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

import numpy as np

from .inference_state import InferenceState


@dataclass(slots=True)
class ContextState:
    """
    Runtime context state.

    Owns conversation metadata (token_ids, messages, generated_token_ids)
    and delegates all model-execution state to InferenceState.

    Ownership: InferenceState is the single source of truth for runtime
    state (KV/conv/recurrent). ContextState.model_state is a deprecated
    alias that delegates to inference_state.state for backward compatibility.
    """

    token_ids: list[int] = field(default_factory=list)
    inference_state: InferenceState = field(default_factory=InferenceState)
    messages: list[dict[str, Any]] = field(default_factory=list)
    generated_token_ids: list[int] = field(default_factory=list)

    @property
    def model_state(self) -> dict[str, Any]:
        """Deprecated alias — delegates to inference_state.state."""
        return self.inference_state.state

    @model_state.setter
    def model_state(self, value: dict[str, Any]) -> None:
        self.inference_state.state = value

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

    def reset(self) -> None:
        """
        Reset conversation and inference state.
        The model session, tokenizer, and adapter remain loaded.
        """
        self.token_ids.clear()
        self.messages.clear()
        self.generated_token_ids.clear()
        self.inference_state.reset()

    def cpu_state(self) -> dict[str, Any]:
        return self.inference_state.to_cpu()
