from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np

from .state import ContextState
from ..model.adapter import CausalOnnxAdapter


@dataclass(slots=True, frozen=True)
class ContextSnapshot:
    token_ids: tuple[int, ...]
    messages: tuple[dict[str, Any], ...]
    generated_token_ids: tuple[int, ...]
    state_keys: tuple[str, ...]


class ContextSnapshotStore:
    """
    Explicit persistent context store.

    Snapshot creation copies CUDA state to CPU; nothing is persisted implicitly.
    """

    def save(
        self,
        path: str | Path,
        context: ContextState,
        adapter: CausalOnnxAdapter,
    ) -> None:
        target = Path(path)
        target.parent.mkdir(parents=True, exist_ok=True)

        cpu_state = context.inference_state.to_cpu()

        metadata = {
            "token_ids": context.token_ids,
            "messages": context.messages,
            "generated_token_ids": context.generated_token_ids,
            "state_keys": list(cpu_state.keys()),
            "is_partitioned": context.inference_state.is_partitioned,
        }

        np_payload: dict[str, Any] = {
            "__metadata__": np.asarray(
                json.dumps(metadata, ensure_ascii=False),
                dtype=np.str_,
            )
        }
        np_payload.update(cpu_state)

        np.savez_compressed(target, **np_payload)

    def load(
        self,
        path: str | Path,
        adapter: CausalOnnxAdapter,
    ) -> ContextState:
        source = Path(path)

        with np.load(source, allow_pickle=False) as archive:
            metadata = json.loads(str(archive["__metadata__"].item()))
            cpu_state = {
                key: np.asarray(archive[key])
                for key in metadata["state_keys"]
            }

        context = ContextState(
            token_ids=[int(value) for value in metadata["token_ids"]],
            messages=[dict(value) for value in metadata["messages"]],
            generated_token_ids=[
                int(value)
                for value in metadata["generated_token_ids"]
            ],
        )
        # Restore state via InferenceState (uses adapter's device_id)
        device_id = adapter._device_id
        context.inference_state.from_cpu(cpu_state, device_id)
        return context
