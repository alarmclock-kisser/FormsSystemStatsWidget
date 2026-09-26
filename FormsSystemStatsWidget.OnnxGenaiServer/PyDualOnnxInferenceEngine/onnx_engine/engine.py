from __future__ import annotations

import asyncio
from collections.abc import AsyncIterator, Iterator
from dataclasses import replace
from pathlib import Path
from typing import Any

from .context import ContextSnapshotStore, ContextState
from .errors import EngineStateError, ModelValidationError
from .generation import (
    GenerationChunk,
    GenerationContext,
    GenerationEngine,
    GenerationRequest,
)
from .model import (
    CausalOnnxAdapter,
    DualStageIoSpec,
    DualStageOnnxAdapter,
    ModelIoSpec,
    ModelPackageLoader,
    OnnxGraphInspector,
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
        self._session_config = session
        self._allow_cpu_fallback = allow_cpu_fallback
        self._preload_cuda_dll_dependencies = preload_cuda_dll_dependencies
        self._session_manager = self._new_session_manager(self._cuda)
        self._stage1_session_manager: OrtSessionManager | None = None
        self._trust_remote_code = trust_remote_code

        self._package = None
        self._tokenizer: TokenizerService | None = None
        self._adapter: CausalOnnxAdapter | DualStageOnnxAdapter | None = None
        self._generation: GenerationEngine | None = None
        self._snapshot_store = ContextSnapshotStore()

    def _new_session_manager(self, cuda: CudaRuntimeConfig) -> OrtSessionManager:
        return OrtSessionManager(
            cuda,
            self._session_config,
            allow_cpu_fallback=self._allow_cpu_fallback,
            preload_dll_dependencies=self._preload_cuda_dll_dependencies,
        )

    @property
    def package(self):
        if self._package is None:
            raise EngineStateError("Engine is not loaded.")
        return self._package

    @property
    def adapter(self) -> CausalOnnxAdapter | DualStageOnnxAdapter:
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
        if package.stage0_path is not None or package.stage1_path is not None:
            if package.stage0_path is None or package.stage1_path is None:
                raise ModelValidationError("Both partitioned ONNX stages are required.")

            stage1_cuda = replace(
                self._cuda,
                device_id=self._cuda.stage1_device_id,
            )
            stage1_manager = self._new_session_manager(stage1_cuda)
            self._stage1_session_manager = stage1_manager
            try:
                stage0_session = self._session_manager.load(str(package.stage0_path))
                stage1_session = stage1_manager.load(str(package.stage1_path))
                if package.model_io and "stage0" in package.model_io and "stage1" in package.model_io:
                    io_spec = DualStageIoSpec.from_dict(package.model_io)
                else:
                    io_spec = OnnxGraphInspector().infer_dual_stage(
                        str(package.stage0_path),
                        str(package.stage1_path),
                    )

                genai_model = package.get_json("genai_config.json").get("model", {})
                model_type = genai_model.get("type")
                if not isinstance(model_type, str) or not model_type:
                    raise ModelValidationError(
                        "Partitioned model genai_config.json must declare model.type."
                    )
                decoder_config = genai_model.get("decoder", {})
                head_size = decoder_config.get("head_size")
                symbolic_dimensions = (
                    {"kv_cache_dim": head_size}
                    if isinstance(head_size, int) and head_size > 0
                    else {}
                )

                tokenizer = TokenizerService(
                    package,
                    trust_remote_code=self._trust_remote_code,
                )
                adapter = DualStageOnnxAdapter(
                    stage0_session,
                    stage1_session,
                    self._cuda.device_id,
                    self._cuda.stage1_device_id,
                    io_spec,
                    model_type=model_type,
                    symbolic_dimensions=symbolic_dimensions,
                )
            except Exception:
                self._session_manager.close()
                stage1_manager.close()
                self._stage1_session_manager = None
                raise
        else:
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
        if self._stage1_session_manager is not None:
            self._stage1_session_manager.close()
            self._stage1_session_manager = None
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
        if isinstance(self.adapter, DualStageOnnxAdapter):
            raise EngineStateError(
                "Partitioned context restore is not available until stage-aware snapshot restore is implemented."
            )
        return self._snapshot_store.load(
            path,
            self.adapter,
        )

    def reset_context(self, context: ContextState) -> None:
        """
        Reset a context: clear conversation, generated tokens, and inference state.
        The model session, tokenizer, and adapter remain loaded (no model reload).
        """
        context.reset()

    def available_providers(self) -> tuple[str, ...]:
        import onnxruntime as ort
        return tuple(ort.get_available_providers())

    def active_providers(self) -> tuple[str, ...]:
        providers = list(self._session_manager.session.get_providers())
        if self._stage1_session_manager is not None:
            for provider in self._stage1_session_manager.session.get_providers():
                if provider not in providers:
                    providers.append(provider)
        return tuple(providers)
