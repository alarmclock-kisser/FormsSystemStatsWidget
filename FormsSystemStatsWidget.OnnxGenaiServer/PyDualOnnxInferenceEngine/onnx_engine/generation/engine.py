from __future__ import annotations

import asyncio
from collections.abc import AsyncIterator, Iterator
from dataclasses import replace
from typing import Any

import numpy as np

from ..context.state import ContextState
from ..errors import GenerationError
from ..model.adapter import CausalOnnxAdapter
from ..model.dual_stage_adapter import DualStageOnnxAdapter
from ..tokenization.tokenizer import TokenizerService
from .sampling import Sampler
from .stopping import StopController
from .types import (
    GenerationChunk,
    GenerationContext,
    GenerationRequest,
)


def _next_or_done(iterator: Iterator[GenerationChunk]) -> tuple[bool, GenerationChunk | None]:
    try:
        return True, next(iterator)
    except StopIteration:
        return False, None


class GenerationEngine:
    """
    Owns the prompt -> prefill -> decode -> sample loop.

    The high-level engine is model-agnostic; model graph specifics stay inside
    CausalOnnxAdapter.
    """

    def __init__(
        self,
        tokenizer: TokenizerService,
        adapter: CausalOnnxAdapter | DualStageOnnxAdapter,
    ) -> None:
        self._tokenizer = tokenizer
        self._adapter = adapter

    def prepare_context(
        self,
        request: GenerationRequest,
        *,
        existing: ContextState | None = None,
    ) -> GenerationContext:
        encoded = self._tokenizer.encode_messages(
            request.messages,
            add_generation_prompt=request.add_generation_prompt,
            tools=request.tools or None,
            documents=request.documents or None,
            **request.template_kwargs,
        )

        return GenerationContext(
            state=existing or ContextState(
                messages=[dict(message) for message in request.messages],
            ),
            prompt_token_count=encoded.token_count,
            generated_count=0,
            prompt_text=encoded.text,
        )

    def prefill(
        self,
        generation: GenerationContext,
    ) -> np.ndarray:
        encoded = self._tokenizer.encode_text(generation.prompt_text)
        inference_state = generation.state.inference_state
        if isinstance(self._adapter, DualStageOnnxAdapter):
            result = self._adapter.prefill(
                encoded.input_ids,
                stage0_state=inference_state.stage0_state or None,
                stage1_state=inference_state.stage1_state or None,
            )
            inference_state.stage0_state = result.stage0_state
            inference_state.stage1_state = result.stage1_state
            inference_state.is_partitioned = True
        else:
            result = self._adapter.prefill(
                encoded.input_ids,
                state=inference_state.state or None,
            )
            inference_state.state = result.state

        generation.state.token_ids = [
            int(value)
            for value in encoded.input_ids[0]
        ]

        return self._select_last_logits(result.logits)

    def generate(
        self,
        generation: GenerationContext,
        request: GenerationRequest,
    ) -> Iterator[GenerationChunk]:
        logits = self.prefill(generation)

        sampler = Sampler(request.sampling)
        stop_config = request.stopping
        eos_token_id = self._tokenizer.eos_token_id
        if eos_token_id is not None:
            stop_config = replace(
                stop_config,
                eos_token_ids=stop_config.eos_token_ids | {eos_token_id},
            )
        stopper = StopController(stop_config)
        control_stop_ids = stop_config.eos_token_ids | stop_config.stop_token_ids

        for step in range(request.stopping.max_new_tokens):
            token_id = sampler.sample(
                logits,
                generation.state.token_ids,
            )
            token_text = self._tokenizer.decode_token(token_id)

            generation.state.append_generated(token_id)
            generation.generated_count = step + 1

            stop = stopper.push(token_id, token_text)
            if token_id in control_stop_ids:
                token_text = ""
            finished = stop or stopper.reached_limit(generation.generated_count)

            yield GenerationChunk(
                token_id=token_id,
                text=token_text,
                finished=finished,
                generated_tokens=generation.generated_count,
                context_length=generation.state.position,
            )

            if finished:
                break

            position = generation.state.position - 1
            inference_state = generation.state.inference_state
            if isinstance(self._adapter, DualStageOnnxAdapter):
                result = self._adapter.decode(
                    token_id,
                    position=position,
                    stage0_state=inference_state.stage0_state,
                    stage1_state=inference_state.stage1_state,
                )
                inference_state.stage0_state = result.stage0_state
                inference_state.stage1_state = result.stage1_state
                inference_state.is_partitioned = True
            else:
                result = self._adapter.decode(
                    token_id,
                    position=position,
                    state=inference_state.state,
                )
                inference_state.state = result.state
            logits = self._select_last_logits(result.logits)

    async def generate_async(
        self,
        generation: GenerationContext,
        request: GenerationRequest,
    ) -> AsyncIterator[GenerationChunk]:
        iterator = iter(self.generate(generation, request))

        while True:
            has_value, chunk = await asyncio.to_thread(
                _next_or_done,
                iterator,
            )

            if not has_value:
                break

            yield chunk

    @staticmethod
    def _select_last_logits(logits: np.ndarray) -> np.ndarray:
        array = np.asarray(logits)

        if array.ndim == 1:
            return array

        if array.ndim == 2:
            return array[-1]

        if array.ndim == 3:
            return array[0, -1]

        raise GenerationError(
            f"Unsupported logits rank {array.ndim}; expected 1D/2D/3D."
        )
