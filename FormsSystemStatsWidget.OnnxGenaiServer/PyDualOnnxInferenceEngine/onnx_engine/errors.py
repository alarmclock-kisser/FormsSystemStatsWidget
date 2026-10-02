from __future__ import annotations


class OnnxEngineError(RuntimeError):
    """Base error for the inference engine."""


class EngineStateError(OnnxEngineError):
    """Raised for invalid engine/context lifecycle operations."""


class CudaExecutionProviderError(OnnxEngineError):
    """Raised when CUDAExecutionProvider cannot be used."""


class ModelValidationError(OnnxEngineError):
    """Raised when model package or graph I/O cannot be validated."""


class GenerationError(OnnxEngineError):
    """Raised when generation cannot proceed."""


class ContextExceededError(GenerationError):
    """Raised when prompt + requested tokens exceed the configured context length.

    Mapped to HTTP 400 (fail fast) instead of OOMing mid-generation.
    Carries the counts so callers can report/trim precisely.
    """

    def __init__(self, prompt_tokens: int, max_new_tokens: int, context_length: int) -> None:
        self.prompt_tokens = int(prompt_tokens)
        self.max_new_tokens = int(max_new_tokens)
        self.context_length = int(context_length)
        super().__init__(
            f"Context length exceeded: prompt ({self.prompt_tokens}) + "
            f"max_tokens ({self.max_new_tokens}) > context_length ({self.context_length})."
        )
