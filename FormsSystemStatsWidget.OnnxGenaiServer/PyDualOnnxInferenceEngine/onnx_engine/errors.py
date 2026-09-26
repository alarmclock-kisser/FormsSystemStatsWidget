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
