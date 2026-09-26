from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Sequence

from ..context.state import ContextState
from .sampling import SamplingConfig
from .stopping import StopConfig


@dataclass(slots=True, frozen=True)
class GenerationRequest:
    messages: tuple[dict[str, Any], ...]
    sampling: SamplingConfig = field(default_factory=SamplingConfig)
    stopping: StopConfig = field(default_factory=StopConfig)
    add_generation_prompt: bool = True
    tools: tuple[dict[str, Any], ...] = ()
    documents: tuple[dict[str, Any], ...] = ()
    template_kwargs: dict[str, Any] = field(default_factory=dict)


@dataclass(slots=True, frozen=True)
class GenerationChunk:
    token_id: int
    text: str
    finished: bool
    generated_tokens: int
    context_length: int


@dataclass(slots=True)
class GenerationContext:
    state: ContextState
    prompt_token_count: int = 0
    generated_count: int = 0
    prompt_text: str = ""
