"""Tests for load-option normalization, sampler window and stop parsing.

Pure unit tests (no model weights, no GPU): these cover exactly the logic
that would otherwise need slow manual load-/generate cycles to verify.
"""

import numpy as np
import pytest

from onnx_engine.generation.sampling import Sampler, SamplingConfig
from onnx_engine.generation.stopping import StopConfig, StopController
from onnx_engine.runtime.options import (
    normalize_cuda_options,
    normalize_max_concurrent,
    normalize_providers,
    normalize_session_options,
    normalize_stop_sequences,
)


def test_session_options_defaults_match_previous_behaviour() -> None:
    opts = normalize_session_options(None)
    assert opts == {
        "graph_optimization_level": "ORT_ENABLE_ALL",
        "execution_mode": "ORT_SEQUENTIAL",
        "intra_op_num_threads": 0,
        "inter_op_num_threads": 0,
        "enable_mem_pattern": True,
        "enable_cpu_mem_arena": True,
        "enable_profiling": False,
        "disable_prepacking": False,
    }


def test_session_options_accept_short_and_ort_spellings() -> None:
    assert normalize_session_options({"execution_mode": "parallel"})["execution_mode"] == "ORT_PARALLEL"
    assert normalize_session_options({"execution_mode": "ORT_PARALLEL"})["execution_mode"] == "ORT_PARALLEL"
    assert normalize_session_options({"graph_optimization_level": "basic"})["graph_optimization_level"] == "ORT_ENABLE_BASIC"
    assert normalize_session_options({"intra_op_num_threads": 8})["intra_op_num_threads"] == 8


def test_session_options_reject_garbage() -> None:
    with pytest.raises(ValueError):
        normalize_session_options({"execution_mode": "turbo"})
    with pytest.raises(ValueError):
        normalize_session_options({"intra_op_num_threads": -1})
    with pytest.raises(ValueError):
        normalize_session_options({"graph_optimization_level": "everything"})


def test_cuda_options_defaults_and_spellings() -> None:
    opts = normalize_cuda_options(None)
    assert opts["arena_extend_strategy"] == "kNextPowerOfTwo"
    assert opts["gpu_mem_limit"] == 0
    assert opts["cudnn_conv_algo_search"] == "EXHAUSTIVE"
    assert opts["do_copy_in_default_stream"] is True
    assert opts["enable_cuda_graph"] is False
    assert opts["use_tf32"] is True
    assert normalize_cuda_options({"arena_extend_strategy": "kSameAsRequested"})["arena_extend_strategy"] == "kSameAsRequested"
    assert normalize_cuda_options({"cudnn_conv_algo_search": "heuristic"})["cudnn_conv_algo_search"] == "HEURISTIC"


def test_providers_cuda_with_and_without_fallback() -> None:
    available = ["CUDAExecutionProvider", "CPUExecutionProvider"]
    assert normalize_providers("cuda", False, available=available) == ["cuda"]
    assert normalize_providers("cuda", True, available=available) == ["cuda", "cpu"]
    assert normalize_providers("cpu", True, available=available) == ["cpu"]
    assert normalize_providers(None, False, available=available) == ["cuda"]


def test_providers_reject_missing_or_unknown() -> None:
    with pytest.raises(ValueError):
        normalize_providers("dml", False, available=["CUDAExecutionProvider", "CPUExecutionProvider"])
    with pytest.raises(ValueError):
        normalize_providers("tpu", False, available=["CPUExecutionProvider"])


def test_stop_sequences_openai_shapes() -> None:
    assert normalize_stop_sequences(None) == ()
    assert normalize_stop_sequences("###") == ("###",)
    assert normalize_stop_sequences(["a", "b"]) == ("a", "b")
    assert normalize_stop_sequences([]) == ()
    with pytest.raises(ValueError):
        normalize_stop_sequences(42)


def test_max_concurrent_minimum_one() -> None:
    assert normalize_max_concurrent(None) == 1
    assert normalize_max_concurrent(4) == 4
    with pytest.raises(ValueError):
        normalize_max_concurrent(0)


def test_repeat_last_n_window_limits_penalties() -> None:
    # Token 0 is old (5.0), token 1 is recent (9.0). Full history penalizes
    # both (2.5 vs 4.5 -> token 1 wins); a window of 1 only penalizes the
    # recent token 1 (5.0 vs 4.5 -> token 0 wins).
    logits = np.array([5.0, 9.0] + [0.0] * 8)
    history = [0] * 50 + [1]

    full = Sampler(SamplingConfig(temperature=0.0, repetition_penalty=2.0))
    assert full.sample(logits.copy(), history) == 1

    windowed = Sampler(SamplingConfig(temperature=0.0, repetition_penalty=2.0, repeat_last_n=1))
    assert windowed.sample(logits.copy(), history) == 0


def test_repeat_last_n_rejects_negative() -> None:
    with pytest.raises(ValueError):
        SamplingConfig(repeat_last_n=-1)


def test_stop_strings_end_generation() -> None:
    controller = StopController(StopConfig(max_new_tokens=100, stop_strings=("<stop>",)))
    assert not controller.push(1, "hello ")
    assert controller.push(2, "world <stop>")
