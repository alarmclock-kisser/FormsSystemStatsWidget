"""Tests for context-cap enforcement and generation-timing stats.

Pure unit tests (no model weights needed): the cap check is a pure function
and the timing tracker works on synthetic GenerationChunks.
"""

import time

import pytest

from onnx_engine.engine import _GenerationTimingTracker, check_context_limit
from onnx_engine.errors import ContextExceededError, GenerationError
from onnx_engine.generation import GenerationChunk


def test_context_limit_disabled_when_zero_or_negative() -> None:
    check_context_limit(1_000_000, 1_000_000, 0)
    check_context_limit(1_000_000, 1_000_000, -5)


def test_context_limit_boundary_passes() -> None:
    # Exactly at the limit is allowed: prompt + max_new == context_length.
    check_context_limit(60_000, 5_536, 65_536)


def test_context_limit_exceeded_carries_counts() -> None:
    with pytest.raises(ContextExceededError) as exc_info:
        check_context_limit(60_000, 6_000, 65_536)
    err = exc_info.value
    assert isinstance(err, GenerationError)
    assert err.prompt_tokens == 60_000
    assert err.max_new_tokens == 6_000
    assert err.context_length == 65_536
    assert "65536" in str(err)


def _chunk(n: int, *, finished: bool = False) -> GenerationChunk:
    return GenerationChunk(
        token_id=n, text="x", finished=finished,
        generated_tokens=n, context_length=10 + n,
    )


def test_timing_tracker_reports_pp_tg_ttft_and_context() -> None:
    tracker = _GenerationTimingTracker(prompt_tokens=100)
    time.sleep(0.01)
    tracker.observe(_chunk(1))
    time.sleep(0.01)
    tracker.observe(_chunk(2, finished=True))
    stats = tracker.snapshot(finish_reason="stop")

    assert stats["prompt_tokens"] == 100
    assert stats["completion_tokens"] == 2
    assert stats["context_tokens"] == 102
    assert stats["finish_reason"] == "stop"
    assert stats["ttft_ms"] > 0
    assert stats["decode_ms"] >= 0
    assert stats["total_ms"] >= stats["ttft_ms"]
    # PP ~= prompt / TTFT, TG ~= completion / decode window.
    assert stats["prompt_tps"] == pytest.approx(100 / (stats["ttft_ms"] / 1000.0), rel=0.01)
    if stats["decode_ms"] > 0:
        assert stats["gen_tps"] == pytest.approx(2 / (stats["decode_ms"] / 1000.0), rel=0.01)


def test_timing_tracker_cancelled_without_chunks() -> None:
    tracker = _GenerationTimingTracker(prompt_tokens=10)
    stats = tracker.snapshot(finish_reason="cancelled")
    assert stats["completion_tokens"] == 0
    assert stats["context_tokens"] == 10
    assert stats["gen_tps"] == 0.0
    assert stats["finish_reason"] == "cancelled"
