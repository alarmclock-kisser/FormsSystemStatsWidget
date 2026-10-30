from __future__ import annotations

from dataclasses import dataclass
from typing import Any

import onnxruntime as ort

from .cuda import (
    CudaRuntimeConfig,
    assert_cuda_provider_available,
    preload_cuda_dlls,
)


@dataclass(slots=True, frozen=True)
class SessionRuntimeConfig:
    graph_optimization_level: str = "ORT_ENABLE_ALL"
    execution_mode: str = "ORT_SEQUENTIAL"
    intra_op_num_threads: int = 0
    inter_op_num_threads: int = 0
    enable_mem_pattern: bool = True
    enable_cpu_mem_arena: bool = True
    enable_profiling: bool = False
    disable_prepacking: bool = False


class OrtSessionManager:
    """Owns one ONNX Runtime CUDA session."""

    def __init__(
        self,
        cuda: CudaRuntimeConfig,
        session: SessionRuntimeConfig | None = None,
        *,
        allow_cpu_fallback: bool = False,
        preload_dll_dependencies: bool = True,
        providers: list[str] | None = None,
    ) -> None:
        self._cuda = cuda
        self._config = session or SessionRuntimeConfig()
        self._allow_cpu_fallback = allow_cpu_fallback
        self._preload_dll_dependencies = preload_dll_dependencies
        # Provider names: "cuda" | "cpu" | "dml". None = legacy behaviour
        # (CUDA, plus CPU when allow_cpu_fallback is set).
        self._providers = [p.lower() for p in providers] if providers else None

        self._session: ort.InferenceSession | None = None

    @property
    def session(self) -> ort.InferenceSession:
        if self._session is None:
            raise RuntimeError("ONNX Runtime session is not loaded.")
        return self._session

    def load(self, model_path: str) -> ort.InferenceSession:
        if self._session is not None:
            return self._session

        if self._preload_dll_dependencies:
            preload_cuda_dlls()

        assert_cuda_provider_available()

        options = ort.SessionOptions()

        optimization_levels = {
            "ORT_DISABLE_ALL": ort.GraphOptimizationLevel.ORT_DISABLE_ALL,
            "ORT_ENABLE_BASIC": ort.GraphOptimizationLevel.ORT_ENABLE_BASIC,
            "ORT_ENABLE_EXTENDED": ort.GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
            "ORT_ENABLE_ALL": ort.GraphOptimizationLevel.ORT_ENABLE_ALL,
            "disable_all": ort.GraphOptimizationLevel.ORT_DISABLE_ALL,
            "basic": ort.GraphOptimizationLevel.ORT_ENABLE_BASIC,
            "extended": ort.GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
            "all": ort.GraphOptimizationLevel.ORT_ENABLE_ALL,
        }

        execution_modes = {
            "ORT_SEQUENTIAL": ort.ExecutionMode.ORT_SEQUENTIAL,
            "ORT_PARALLEL": ort.ExecutionMode.ORT_PARALLEL,
            "sequential": ort.ExecutionMode.ORT_SEQUENTIAL,
            "parallel": ort.ExecutionMode.ORT_PARALLEL,
        }

        opt_level = optimization_levels.get(str(self._config.graph_optimization_level))
        if opt_level is None:
            raise ValueError(
                f"Unknown graph_optimization_level: {self._config.graph_optimization_level!r}."
            )
        exec_mode = execution_modes.get(str(self._config.execution_mode))
        if exec_mode is None:
            raise ValueError(
                f"Unknown execution_mode: {self._config.execution_mode!r}."
            )
        options.graph_optimization_level = opt_level
        options.execution_mode = exec_mode
        options.enable_mem_pattern = self._config.enable_mem_pattern
        options.enable_cpu_mem_arena = self._config.enable_cpu_mem_arena
        options.enable_profiling = self._config.enable_profiling

        if self._config.intra_op_num_threads > 0:
            options.intra_op_num_threads = self._config.intra_op_num_threads

        if self._config.inter_op_num_threads > 0:
            options.inter_op_num_threads = self._config.inter_op_num_threads

        if self._config.disable_prepacking:
            options.add_session_config_entry("session.disable_prepacking", "1")

        provider = (
            "CUDAExecutionProvider",
            self._cuda.provider_options(),
        )

        providers: list[Any] = self._build_providers(provider)
        if self._providers is None and self._allow_cpu_fallback:
            providers.append("CPUExecutionProvider")

        self._session = ort.InferenceSession(
            model_path,
            sess_options=options,
            providers=providers,
        )

        active = self._session.get_providers()
        if self._wants_cuda() and "CUDAExecutionProvider" not in active:
            self.close()
            raise RuntimeError(
                f"CUDAExecutionProvider was not activated. Active providers: {active}"
            )

        return self._session

    def _wants_cuda(self) -> bool:
        return self._providers is None or "cuda" in self._providers

    def _build_providers(self, cuda_provider: tuple[str, dict[str, Any]]) -> list[Any]:
        if self._providers is None:
            return [cuda_provider]
        result: list[Any] = []
        for name in self._providers:
            if name == "cuda":
                result.append(cuda_provider)
            elif name == "cpu":
                result.append("CPUExecutionProvider")
            elif name == "dml":
                result.append("DmlExecutionProvider")
            else:
                raise ValueError(f"Unknown execution provider: {name!r}.")
        if not result:
            raise ValueError("No execution providers selected.")
        return result

    def close(self) -> None:
        self._session = None
