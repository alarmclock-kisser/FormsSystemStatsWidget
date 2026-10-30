"""Validation/normalization for /load runtime options (pure functions).

Everything here is unit-testable without model weights or GPU:
invalid values raise ValueError with a clear message (fail fast at /load,
surfaced as HTTP 400/500) instead of misbehaving mid-generation.
"""

from __future__ import annotations

from typing import Any, Mapping


VALID_EXECUTION_MODES = ("ORT_SEQUENTIAL", "ORT_PARALLEL")
VALID_GRAPH_OPT_LEVELS = (
    "ORT_DISABLE_ALL",
    "ORT_ENABLE_BASIC",
    "ORT_ENABLE_EXTENDED",
    "ORT_ENABLE_ALL",
)
VALID_ARENA_STRATEGIES = ("kNextPowerOfTwo", "kSameAsRequested")
VALID_CUDNN_SEARCH = ("EXHAUSTIVE", "HEURISTIC", "DEFAULT")
VALID_PROVIDERS = ("cuda", "cpu", "dml")


def _norm_enum(value: Any, valid: tuple[str, ...], *, field: str, default: str) -> str:
    """Case-insensitive match; returns the canonical (proper-case) spelling."""
    if value is None or (isinstance(value, str) and not value.strip()):
        return default
    text = str(value).strip()
    # Also accept short forms ("all" for "ORT_ENABLE_ALL").
    short = text.lower().replace("ort_", "").replace("enable_", "")
    for candidate in valid:
        if text.lower() == candidate.lower() or short == candidate.lower().replace("ort_", "").replace("enable_", ""):
            return candidate
    raise ValueError(f"{field} must be one of {list(valid)}, got {value!r}.")


def _norm_bool(value: Any, *, field: str, default: bool) -> bool:
    if value is None or (isinstance(value, str) and not value.strip()):
        return default
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return bool(value)
    text = str(value).strip().lower()
    if text in {"1", "true", "yes", "on"}:
        return True
    if text in {"0", "false", "no", "off"}:
        return False
    raise ValueError(f"{field} must be a boolean, got {value!r}.")


def _norm_int(value: Any, *, field: str, default: int, minimum: int = 0) -> int:
    if value is None or (isinstance(value, str) and not value.strip()):
        return default
    try:
        parsed = int(value)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"{field} must be an integer, got {value!r}.") from exc
    if parsed < minimum:
        raise ValueError(f"{field} must be >= {minimum}, got {parsed}.")
    return parsed


def normalize_session_options(raw: Mapping[str, Any] | None) -> dict[str, Any]:
    """Validate session_options from /load into SessionRuntimeConfig kwargs."""
    data = dict(raw or {})
    return {
        "graph_optimization_level": _norm_enum(
            data.get("graph_optimization_level"), VALID_GRAPH_OPT_LEVELS,
            field="session_options.graph_optimization_level", default="ORT_ENABLE_ALL",
        ),
        "execution_mode": _norm_enum(
            data.get("execution_mode"), VALID_EXECUTION_MODES,
            field="session_options.execution_mode", default="ORT_SEQUENTIAL",
        ),
        "intra_op_num_threads": _norm_int(
            data.get("intra_op_num_threads"), field="session_options.intra_op_num_threads",
            default=0,
        ),
        "inter_op_num_threads": _norm_int(
            data.get("inter_op_num_threads"), field="session_options.inter_op_num_threads",
            default=0,
        ),
        "enable_mem_pattern": _norm_bool(
            data.get("enable_mem_pattern"), field="session_options.enable_mem_pattern",
            default=True,
        ),
        "enable_cpu_mem_arena": _norm_bool(
            data.get("enable_cpu_mem_arena"), field="session_options.enable_cpu_mem_arena",
            default=True,
        ),
        "enable_profiling": _norm_bool(
            data.get("enable_profiling"), field="session_options.enable_profiling",
            default=False,
        ),
        "disable_prepacking": _norm_bool(
            data.get("disable_prepacking"), field="session_options.disable_prepacking",
            default=False,
        ),
    }


def normalize_cuda_options(raw: Mapping[str, Any] | None) -> dict[str, Any]:
    """Validate cuda_options from /load into CudaRuntimeConfig kwargs."""
    data = dict(raw or {})
    return {
        "arena_extend_strategy": _norm_enum(
            data.get("arena_extend_strategy"), VALID_ARENA_STRATEGIES,
            field="cuda_options.arena_extend_strategy", default="kNextPowerOfTwo",
        ),
        "gpu_mem_limit": _norm_int(
            data.get("gpu_mem_limit"), field="cuda_options.gpu_mem_limit", default=0,
        ),
        "cudnn_conv_algo_search": _norm_enum(
            data.get("cudnn_conv_algo_search"), VALID_CUDNN_SEARCH,
            field="cuda_options.cudnn_conv_algo_search", default="EXHAUSTIVE",
        ),
        "do_copy_in_default_stream": _norm_bool(
            data.get("do_copy_in_default_stream"),
            field="cuda_options.do_copy_in_default_stream", default=True,
        ),
        "enable_cuda_graph": _norm_bool(
            data.get("enable_cuda_graph"), field="cuda_options.enable_cuda_graph",
            default=False,
        ),
        "use_tf32": _norm_bool(
            data.get("use_tf32"), field="cuda_options.use_tf32", default=True,
        ),
    }


def normalize_providers(
    provider: Any,
    allow_cpu_fallback: bool,
    *,
    available: list[str] | None = None,
) -> list[str]:
    """Map a UI provider name (+fallback flag) to an ORT provider list.

    Raises ValueError when the requested provider is not installed, instead
    of silently running on CUDA anyway (the old behaviour).
    """
    name = (str(provider).strip().lower() if provider else "cuda") or "cuda"
    if name not in VALID_PROVIDERS:
        raise ValueError(f"provider must be one of {list(VALID_PROVIDERS)}, got {provider!r}.")
    if available is None:
        try:
            import onnxruntime as ort

            available = list(ort.get_available_providers())
        except Exception:
            available = ["CPUExecutionProvider"]

    mapping = {
        "cuda": "CUDAExecutionProvider",
        "cpu": "CPUExecutionProvider",
        "dml": "DmlExecutionProvider",
    }
    wanted = mapping[name]
    if wanted not in (available or []):
        raise ValueError(
            f"Execution provider {wanted} is not available in this onnxruntime build "
            f"(available: {available})."
        )
    providers = [name]
    if name == "cuda" and allow_cpu_fallback:
        providers.append("cpu")
    return providers


def normalize_stop_sequences(value: Any) -> tuple[str, ...]:
    """Accept OpenAI-style stop (string | list) into a clean tuple."""
    if value is None:
        return ()
    if isinstance(value, str):
        items = [value]
    elif isinstance(value, (list, tuple)):
        items = list(value)
    else:
        raise ValueError(f"stop must be a string or list of strings, got {value!r}.")
    cleaned = tuple(item for item in (str(v) for v in items if str(v)) if item)
    if any(not isinstance(item, str) for item in items):
        raise ValueError(f"stop must be a string or list of strings, got {value!r}.")
    return cleaned


def normalize_max_concurrent(value: Any, *, default: int = 1) -> int:
    return _norm_int(value, field="max_concurrent_generations", default=default, minimum=1)
