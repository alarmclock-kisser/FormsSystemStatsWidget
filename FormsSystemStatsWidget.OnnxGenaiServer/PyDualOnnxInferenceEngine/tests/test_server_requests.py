import pytest

from onnx_engine.generation.sampling import SamplingConfig
from onnx_engine.server import _parse_generation_request


def test_generation_request_preserves_sampling_controls() -> None:
    request = _parse_generation_request(
        {
            "messages": [{"role": "user", "content": "hello"}],
            "temperature": 0.4,
            "top_k": 12,
            "top_p": 0.8,
            "typical_p": 0.9,
            "min_p": 0.05,
            "repeat_penalty": 1.1,
            "presence_penalty": 0.3,
            "frequency_penalty": 0.2,
            "seed": 42,
            "max_tokens": 23,
        }
    )

    assert request.sampling == SamplingConfig(
        temperature=0.4,
        top_k=12,
        top_p=0.8,
        typical_p=0.9,
        min_p=0.05,
        repetition_penalty=1.1,
        presence_penalty=0.3,
        frequency_penalty=0.2,
        seed=42,
    )
    assert request.stopping.max_new_tokens == 23


def test_min_p_must_be_in_unit_interval() -> None:
    with pytest.raises(ValueError, match="min_p"):
        SamplingConfig(min_p=1.1)