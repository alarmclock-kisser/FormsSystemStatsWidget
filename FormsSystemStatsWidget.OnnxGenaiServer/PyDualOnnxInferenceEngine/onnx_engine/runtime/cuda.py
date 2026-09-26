from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

import onnxruntime as ort


@dataclass(slots=True, frozen=True)
class CudaRuntimeConfig:
    device_id: int = 0
    arena_extend_strategy: str = "kNextPowerOfTwo"
    gpu_mem_limit: int = 0
    cudnn_conv_algo_search: str = "EXHAUSTIVE"
    do_copy_in_default_stream: bool = True
    enable_cuda_graph: bool = False
    use_tf32: bool = True
    extra_options: dict[str, Any] = field(default_factory=dict)

    def provider_options(self) -> dict[str, Any]:
        options: dict[str, Any] = {
            "device_id": self.device_id,
            "arena_extend_strategy": self.arena_extend_strategy,
            "do_copy_in_default_stream": self.do_copy_in_default_stream,
            "enable_cuda_graph": self.enable_cuda_graph,
            "use_tf32": self.use_tf32,
        }

        if self.gpu_mem_limit > 0:
            options["gpu_mem_limit"] = self.gpu_mem_limit

        if self.cudnn_conv_algo_search:
            options["cudnn_conv_algo_search"] = self.cudnn_conv_algo_search

        options.update(self.extra_options)
        return options


def preload_cuda_dlls() -> None:
    function = getattr(ort, "preload_dlls", None)
    if function is not None:
        function()


def assert_cuda_provider_available() -> None:
    available = tuple(ort.get_available_providers())
    if "CUDAExecutionProvider" not in available:
        raise RuntimeError(
            "CUDAExecutionProvider is unavailable. "
            f"Available providers: {available}"
        )
