from __future__ import annotations

import asyncio
import time
from collections.abc import AsyncIterator, Iterator
from dataclasses import replace
from pathlib import Path
from typing import Any

from .context import ContextSnapshotStore, ContextState
from .errors import ContextExceededError, EngineStateError, ModelValidationError
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


def check_context_limit(prompt_tokens: int, max_new_tokens: int, context_length: int) -> None:
    """Fail fast when prompt + requested tokens exceed the configured context.

    Pure function (no model needed) so it is unit-testable. Raises
    ContextExceededError with the counts; ``context_length <= 0`` disables
    the cap (unlimited, legacy behaviour).
    """
    ctx = int(context_length or 0)
    if ctx <= 0:
        return
    if int(prompt_tokens) + int(max_new_tokens) > ctx:
        raise ContextExceededError(int(prompt_tokens), int(max_new_tokens), ctx)


class _GenerationTimingTracker:
    """Records PP/TG/TTFT timings for one generation.

    TTFT covers prompt tokenization is excluded (measured from generate start)
    and includes prefill + first decode step, i.e. prompt_tps ~= prefill
    throughput (PP), gen_tps = steady decode throughput (TG).
    """

    def __init__(self, prompt_tokens: int) -> None:
        self._prompt_tokens = int(prompt_tokens)
        self._start = time.perf_counter()
        self._first_at: float | None = None
        self._generated = 0
        self._finished = False

    def observe(self, chunk: GenerationChunk) -> None:
        now = time.perf_counter()
        if self._first_at is None:
            self._first_at = now
        self._generated = int(chunk.generated_tokens)
        if chunk.finished:
            self._finished = True

    def snapshot(self, *, finish_reason: str) -> dict[str, Any]:
        end = time.perf_counter()
        first_at = self._first_at if self._first_at is not None else end
        ttft_ms = max(0.0, (first_at - self._start) * 1000.0)
        decode_ms = max(0.0, (end - first_at) * 1000.0)
        total_ms = max(0.0, (end - self._start) * 1000.0)
        prompt_tps = (self._prompt_tokens / (ttft_ms / 1000.0)) if ttft_ms > 0 else 0.0
        gen_tps = (self._generated / (decode_ms / 1000.0)) if decode_ms > 0 and self._generated > 0 else 0.0
        return {
            "prompt_tokens": self._prompt_tokens,
            "completion_tokens": self._generated,
            "context_tokens": self._prompt_tokens + self._generated,
            "ttft_ms": round(ttft_ms, 2),
            "decode_ms": round(decode_ms, 2),
            "total_ms": round(total_ms, 2),
            "prompt_tps": round(prompt_tps, 2),
            "gen_tps": round(gen_tps, 2),
            "finish_reason": finish_reason,
        }


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
        model_layout: str = "auto",
        context_length: int = 0,
        providers: list[str] | None = None,
    ) -> None:
        self._model_path = Path(model_path)
        self._cuda = cuda or CudaRuntimeConfig()
        self._session_config = session
        self._allow_cpu_fallback = allow_cpu_fallback
        self._preload_cuda_dll_dependencies = preload_cuda_dll_dependencies
        self._model_layout = model_layout
        self._context_length = max(0, int(context_length or 0))
        self._providers = [str(p).lower() for p in providers] if providers else None
        self._session_manager = self._new_session_manager(self._cuda)
        self._stage1_session_manager: OrtSessionManager | None = None
        self._trust_remote_code = trust_remote_code

        self._package = None
        self._tokenizer: TokenizerService | None = None
        self._adapter: CausalOnnxAdapter | DualStageOnnxAdapter | None = None
        self._generation: GenerationEngine | None = None
        self._snapshot_store = ContextSnapshotStore()
        self._last_generation: dict[str, Any] | None = None
        self._generation_totals = {
            "generations": 0,
            "prompt_tokens": 0,
            "completion_tokens": 0,
        }

    @property
    def context_length(self) -> int:
        """Configured context cap (0 = unlimited)."""
        return self._context_length

    def _new_session_manager(self, cuda: CudaRuntimeConfig) -> OrtSessionManager:
        return OrtSessionManager(
            cuda,
            self._session_config,
            allow_cpu_fallback=self._allow_cpu_fallback,
            preload_dll_dependencies=self._preload_cuda_dll_dependencies,
            providers=self._providers,
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

        package = ModelPackageLoader().load(self._model_path, layout=self._model_layout)
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

    def generate_checked(
        self,
        request: GenerationRequest,
        *,
        existing: ContextState | None = None,
    ) -> tuple[GenerationContext, Iterator[GenerationChunk], _GenerationTimingTracker]:
        """Like :meth:`generate`, but enforces the context cap and tracks timings.

        Raises ContextExceededError before any GPU work when
        prompt_tokens + max_new_tokens exceeds the configured context_length.
        """
        context = self.prepare(request, existing=existing)
        check_context_limit(
            context.prompt_token_count,
            request.stopping.max_new_tokens,
            self._context_length,
        )
        self._generation_totals["generations"] += 1
        self._generation_totals["prompt_tokens"] += context.prompt_token_count
        tracker = _GenerationTimingTracker(context.prompt_token_count)
        return context, self._tracked_chunks(request, context, tracker), tracker

    def _tracked_chunks(
        self,
        request: GenerationRequest,
        context: GenerationContext,
        tracker: _GenerationTimingTracker,
    ) -> Iterator[GenerationChunk]:
        assert self._generation is not None
        try:
            for chunk in self._generation.generate(context, request):
                tracker.observe(chunk)
                yield chunk
        finally:
            completion = tracker._generated
            self._generation_totals["completion_tokens"] += completion
            stats = tracker.snapshot(
                finish_reason="stop" if tracker._finished else "cancelled"
            )
            stats["timestamp_utc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
            self._last_generation = stats

    def get_generation_stats(self) -> dict[str, Any]:
        """Last-generation timings + totals + effective config (serves GET /stats)."""
        try:
            providers = list(self.active_providers())
        except Exception:
            providers = []
        return {
            "last": self._last_generation,
            "totals": dict(self._generation_totals),
            "config": {
                "model_path": str(self._model_path),
                "model_layout": self._model_layout,
                "context_length": self._context_length,
                "kv_cache": {
                    "k_type": "f16",
                    "v_type": "f16",
                    "quantized": False,
                    "reason": "graph past inputs are fp16-only; cache-side requantization cannot lower peak VRAM",
                },
                "providers": providers,
            },
        }

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
            model_id=str(self.package.root),
        )

    def load_context(self, path: str | Path) -> ContextState:
        self.load()
        return self._snapshot_store.load(
            path,
            self.adapter,
            model_id=str(self.package.root),
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
