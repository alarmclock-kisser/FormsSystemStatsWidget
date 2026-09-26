from __future__ import annotations

import asyncio
from collections.abc import AsyncIterator, Iterator
from pathlib import Path
from typing import Any

from .context import ContextSnapshotStore, ContextState
from .errors import EngineStateError
from .generation import (
    GenerationChunk,
    GenerationContext,
    GenerationEngine,
    GenerationRequest,
)
from .model import (
    CausalOnnxAdapter,
    ModelIoSpec,
    ModelPackageLoader,
)
from .runtime import (
    CudaRuntimeConfig,
    OrtSessionManager,
    SessionRuntimeConfig,
)
from .tokenization import TokenizerService


class InferenceEngine:
    """
    Public high-level Python inference core.

    Owns:
      package -> tokenizer -> ONNX Runtime -> adapter -> generation.
    """

    def __init__(
        self,
        model_path: str | Path,
        *,
        cuda: CudaRuntimeConfig | None = None,
        session: SessionRuntimeConfig | None = None,
        trust_remote_code: bool = False,
        allow_cpu_fallback: bool = False,
        preload_cuda_dll_dependencies: bool = True,
    ) -> None:
        self._model_path = Path(model_path)
        self._cuda = cuda or CudaRuntimeConfig()
        self._session_manager = OrtSessionManager(
            self._cuda,
            session,
            allow_cpu_fallback=allow_cpu_fallback,
            preload_cuda_dll_dependencies=preload_cuda_dll_dependencies,
        )
        self._trust_remote_code = trust_remote_code

        self._package = None
        self._tokenizer: TokenizerService | None = None
        self._adapter: CausalOnnxAdapter | None = None
        self._generation: GenerationEngine | None = None
        self._snapshot_store = ContextSnapshotStore()

    @property
    def package(self):
        if self._package is None:
            raise EngineStateError("Engine is not loaded.")
        return self._package

    @property
    def adapter(self) -> CausalOnnxAdapter:
        if self._adapter is None:
            raise EngineStateError("Engine is not loaded.")
        return self._adapter

    @property
    def tokenizer(self) -> TokenizerService:
        if self._tokenizer is None:
            raise EngineStateError("Engine is not loaded.")
        return self._tokenizer

    @property
    def generation(self) -> GenerationEngine:
        if self._generation is None:
            raise EngineStateError("Engine is not loaded.")
        return self._generation

    def load(self) -> None:
        if self._package is not None:
            return

        package = ModelPackageLoader().load(self._model_path)
        session = self._session_manager.load(str(package.model_path))

        io_spec = (
            ModelIoSpec.from_dict(package.model_io)
            if package.model_io
            else None
        )

        tokenizer = TokenizerService(
            package,
            trust_remote_code=self._trust_remote_code,
        )

        adapter = CausalOnnxAdapter(
            session,
            self._cuda.device_id,
            io_spec=io_spec,
        )

        self._package = package
        self._tokenizer = tokenizer
        self._adapter = adapter
        self._generation = GenerationEngine(tokenizer, adapter)

    def unload(self) -> None:
        self._generation = None
        self._adapter = None
        self._tokenizer = None
        self._package = None
        self._session_manager.close()

    close = unload

    def __enter__(self) -> "InferenceEngine":
        self.load()
        return self

    def __exit__(self, exc_type: Any, exc_value: Any, traceback: Any) -> None:
        self.unload()

    def create_context(self) -> ContextState:
        return ContextState()

    def prepare(
        self,
        request: GenerationRequest,
        *,
        existing: ContextState | None = None,
    ) -> GenerationContext:
        self.load()
        return self.generation.prepare_context(
            request,
            existing=existing,
        )

    def generate(
        self,
        request: GenerationRequest,
        *,
        existing: ContextState | None = None,
    ) -> tuple[GenerationContext, Iterator[GenerationChunk]]:
        context = self.prepare(request, existing=existing)
        return context, self.generation.generate(context, request)

    async def generate_async(
        self,
        request: GenerationRequest,
        *,
        existing: ContextState | None = None,
    ) -> AsyncIterator[GenerationChunk]:
        context = self.prepare(request, existing=existing)

        async for chunk in self.generation.generate_async(context, request):
            yield chunk

    def save_context(
        self,
        path: str | Path,
        context: ContextState,
    ) -> None:
        self._snapshot_store.save(
            path,
            context,
            self.adapter,
        )

    def load_context(self, path: str | Path) -> ContextState:
        self.load()
        return self._snapshot_store.load(
            path,
            self.adapter,
        )

    def available_providers(self) -> tuple[str, ...]:
        import onnxruntime as ort
        return tuple(ort.get_available_providers())

    def active_providers(self) -> tuple[str, ...]:
        return tuple(self._session_manager.session.get_providers())
