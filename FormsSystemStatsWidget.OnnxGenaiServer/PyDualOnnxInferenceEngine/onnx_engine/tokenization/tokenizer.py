from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Sequence

import numpy as np
from transformers import AutoTokenizer, PreTrainedTokenizerBase

from ..model.package import ModelPackage


@dataclass(slots=True, frozen=True)
class EncodedPrompt:
    input_ids: np.ndarray
    text: str
    token_count: int


class TokenizerService:
    """Loads and owns the model-local Hugging Face tokenizer."""

    def __init__(
        self,
        package: ModelPackage,
        *,
        trust_remote_code: bool = False,
    ) -> None:
        self._package = package
        self._tokenizer: PreTrainedTokenizerBase = AutoTokenizer.from_pretrained(
            str(package.root),
            local_files_only=True,
            trust_remote_code=trust_remote_code,
            use_fast=True,
        )

        if package.chat_template and not getattr(
            self._tokenizer,
            "chat_template",
            None,
        ):
            self._tokenizer.chat_template = package.chat_template

    @property
    def tokenizer(self) -> PreTrainedTokenizerBase:
        return self._tokenizer

    @property
    def eos_token_id(self) -> int | None:
        value = self._tokenizer.eos_token_id
        return int(value) if value is not None else None

    @property
    def bos_token_id(self) -> int | None:
        value = self._tokenizer.bos_token_id
        return int(value) if value is not None else None

    def apply_chat_template(
        self,
        messages: Sequence[dict[str, Any]],
        *,
        tokenize: bool = False,
        add_generation_prompt: bool = True,
        tools: Sequence[dict[str, Any]] | None = None,
        documents: Sequence[dict[str, Any]] | None = None,
        **kwargs: Any,
    ) -> str | list[int]:
        template_kwargs: dict[str, Any] = {
            "add_generation_prompt": add_generation_prompt,
            **kwargs,
        }

        if tools is not None:
            template_kwargs["tools"] = list(tools)

        if documents is not None:
            template_kwargs["documents"] = list(documents)

        return self._tokenizer.apply_chat_template(
            list(messages),
            tokenize=tokenize,
            **template_kwargs,
        )

    def encode_text(self, text: str) -> EncodedPrompt:
        encoded = self._tokenizer(
            text,
            add_special_tokens=False,
            return_tensors=None,
        )
        ids = [int(value) for value in encoded["input_ids"]]

        return EncodedPrompt(
            input_ids=np.asarray([ids], dtype=np.int64),
            text=text,
            token_count=len(ids),
        )

    def encode_messages(
        self,
        messages: Sequence[dict[str, Any]],
        *,
        add_generation_prompt: bool = True,
        tools: Sequence[dict[str, Any]] | None = None,
        documents: Sequence[dict[str, Any]] | None = None,
        **kwargs: Any,
    ) -> EncodedPrompt:
        text = self.apply_chat_template(
            messages,
            tokenize=False,
            add_generation_prompt=add_generation_prompt,
            tools=tools,
            documents=documents,
            **kwargs,
        )
        return self.encode_text(text)

    def decode(self, token_ids: Sequence[int]) -> str:
        return self._tokenizer.decode(
            list(token_ids),
            skip_special_tokens=False,
        )

    def decode_token(self, token_id: int) -> str:
        return self.decode([token_id])
