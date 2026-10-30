from __future__ import annotations

from collections.abc import Sequence

import numpy as np

from ..errors import GenerationError


def build_text_position_ids(
    model_type: str,
    graph_shape: Sequence[int | str | None],
    *,
    start_position: int,
    sequence_length: int,
    batch_size: int = 1,
) -> np.ndarray:
    """Build text-only Qwen3.5/3.8 MRoPE positions from its graph contract."""
    if model_type != "qwen3_5_text":
        raise GenerationError(
            f"Text position semantics are not verified for model type '{model_type}'."
        )
    if len(graph_shape) != 3:
        raise GenerationError(
            f"Qwen3.5/3.8 text position_ids must have rank 3, got {len(graph_shape)}."
        )
    if start_position < 0 or sequence_length < 1 or batch_size < 1:
        raise ValueError(
            "start_position must be nonnegative; sequence length and batch size must be positive."
        )

    axis_count = graph_shape[0]
    if axis_count != 3:
        raise GenerationError(
            f"Qwen3.5/3.8 text position_ids must have 3 MRoPE axes, got {axis_count}."
        )
    for axis, actual in ((1, batch_size), (2, sequence_length)):
        expected = graph_shape[axis]
        if isinstance(expected, int) and expected >= 0 and expected != actual:
            raise GenerationError(
                f"Position shape {tuple(graph_shape)} conflicts with requested "
                f"batch/sequence dimensions ({batch_size}, {sequence_length})."
            )

    positions = np.arange(
        start_position,
        start_position + sequence_length,
        dtype=np.int64,
    )
    return np.broadcast_to(
        positions[None, None, :],
        (axis_count, batch_size, sequence_length),
    ).copy()