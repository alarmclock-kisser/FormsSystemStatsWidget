from unittest.mock import Mock

import numpy as np

from onnx_engine.context.state import ContextState
from onnx_engine.generation.engine import GenerationEngine
from onnx_engine.generation.sampling import SamplingConfig
from onnx_engine.generation.types import GenerationContext, GenerationRequest


def test_generation_stops_at_tokenizer_eos_without_emitting_it() -> None:
    tokenizer = Mock()
    tokenizer.eos_token_id = 7
    tokenizer.decode_token.return_value = "<|im_end|>"
    adapter = Mock()
    engine = GenerationEngine(tokenizer, adapter)
    engine.prefill = Mock(return_value=np.array([0, 0, 0, 0, 0, 0, 0, 1]))

    chunks = list(
        engine.generate(
            GenerationContext(state=ContextState()),
            GenerationRequest(
                messages=(),
                sampling=SamplingConfig(temperature=0),
            ),
        )
    )

    assert len(chunks) == 1
    assert chunks[0].token_id == 7
    assert chunks[0].finished
    assert chunks[0].text == ""
    adapter.decode.assert_not_called()