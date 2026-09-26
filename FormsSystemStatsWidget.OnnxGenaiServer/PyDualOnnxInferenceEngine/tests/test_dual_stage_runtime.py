from __future__ import annotations

import gc
import os
from pathlib import Path
from unittest.mock import patch

import numpy as np
import pytest

ort = pytest.importorskip("onnxruntime")
pytest.importorskip("transformers")

from onnx_engine.context.state import ContextState
from onnx_engine.engine import InferenceEngine
from onnx_engine.generation import (
    GenerationContext,
    GenerationRequest,
    SamplingConfig,
    StopConfig,
)
from onnx_engine.model.dual_stage_adapter import DualStageOnnxAdapter
from onnx_engine.runtime.cuda import CudaRuntimeConfig


MODEL_ROOT = os.environ.get("QWEN_ONNX_PARTITIONED_MODEL_ROOT")


def _trace_stage(name: str, run, records):
    def traced(feeds):
        print(f"{name} started", flush=True)
        result = run(feeds)
        records.append((feeds, result))
        print(f"{name} completed", flush=True)
        return result

    return traced


@pytest.mark.skipif(
    not MODEL_ROOT,
    reason="Set QWEN_ONNX_PARTITIONED_MODEL_ROOT to opt in to the real-model GPU test.",
)
def test_inference_engine_partitioned_cuda_prefill_and_decode() -> None:
    model_root = Path(MODEL_ROOT or "")
    partition_root = model_root / "partitioned"
    stage0_path = partition_root / "model.stage0.onnx"
    stage1_path = partition_root / "model.stage1.onnx"
    assert stage0_path.is_file()
    assert stage1_path.is_file()
    if "CUDAExecutionProvider" not in ort.get_available_providers():
        pytest.skip("ONNX Runtime CUDAExecutionProvider is unavailable.")

    stage0_device = int(os.environ.get("QWEN_ONNX_STAGE0_DEVICE", "0"))
    stage1_device = int(os.environ.get("QWEN_ONNX_STAGE1_DEVICE", "1"))
    engine = InferenceEngine(
        model_root,
        cuda=CudaRuntimeConfig(
            device_id=stage0_device,
            stage1_device_id=stage1_device,
        ),
    )

    try:
        engine.load()
        adapter = engine.adapter
        assert isinstance(adapter, DualStageOnnxAdapter)
        assert engine.package.is_partitioned
        assert "CUDAExecutionProvider" in engine._session_manager.session.get_providers()
        assert engine._stage1_session_manager is not None
        assert "CUDAExecutionProvider" in engine._stage1_session_manager.session.get_providers()
        assert adapter.stage0_device_id == stage0_device
        assert adapter.stage1_device_id == stage1_device
        print("InferenceEngine loaded both CUDA stages", flush=True)

        prompt_text = "The quick brown fox jumps over the lazy dog."
        encoded = engine.tokenizer.encode_text(prompt_text)
        assert encoded.token_count > 1
        generation = GenerationContext(
            state=ContextState(),
            prompt_text=prompt_text,
        )
        request = GenerationRequest(
            messages=(),
            sampling=SamplingConfig(temperature=0),
            stopping=StopConfig(max_new_tokens=2),
        )
        stage0_runs = []
        stage1_runs = []

        with (
            patch.object(
                adapter,
                "_run_stage0",
                side_effect=_trace_stage(
                    "Stage 0",
                    adapter._run_stage0,
                    stage0_runs,
                ),
            ),
            patch.object(
                adapter,
                "_run_stage1",
                side_effect=_trace_stage(
                    "Stage 1",
                    adapter._run_stage1,
                    stage1_runs,
                ),
            ),
        ):
            chunks = list(engine.generation.generate(generation, request))

        io_spec = adapter.io_spec
        assert len(stage0_runs) == 2
        assert len(stage1_runs) == 2
        assert len(chunks) == 2
        assert chunks[-1].finished
        assert generation.state.inference_state.is_partitioned
        assert not generation.state.inference_state.state
        assert all(
            value.device_name() == "cuda"
            for value in generation.state.inference_state.stage0_state.values()
        )
        assert all(
            value.device_name() == "cuda"
            for value in generation.state.inference_state.stage1_state.values()
        )

        expected_prefill_positions = np.broadcast_to(
            np.arange(encoded.token_count, dtype=np.int64)[None, None, :],
            (3, 1, encoded.token_count),
        )
        expected_decode_positions = np.full(
            (3, 1, 1),
            encoded.token_count,
            dtype=np.int64,
        )
        for feeds in (stage0_runs[0][0], stage1_runs[0][0]):
            np.testing.assert_array_equal(
                feeds[io_spec.stage0.position_ids if feeds is stage0_runs[0][0] else io_spec.stage1.position_ids].numpy(),
                expected_prefill_positions,
            )
        for feeds in (stage0_runs[1][0], stage1_runs[1][0]):
            position_name = (
                io_spec.stage0.position_ids
                if feeds is stage0_runs[1][0]
                else io_spec.stage1.position_ids
            )
            np.testing.assert_array_equal(
                feeds[position_name].numpy(),
                expected_decode_positions,
            )

        boundary_values = [
            stage1_runs[0][0][name]
            for name in io_spec.stage1.boundary_inputs
        ]
        assert len(boundary_values) == len(io_spec.stage1.boundary_inputs)
        assert all(value.device_name() == "cuda" for value in boundary_values)
        assert all(value.data_ptr() != 0 for value in boundary_values)

        for bindings, before_outputs, after_outputs in (
            (io_spec.stage0.all_state_bindings, stage0_runs[0][1], stage0_runs[1][1]),
            (io_spec.stage1.all_state_bindings, stage1_runs[0][1], stage1_runs[1][1]),
        ):
            kv_state_names = {binding.state_name for binding in io_spec.stage0.kv_bindings}
            kv_state_names.update(binding.state_name for binding in io_spec.stage1.kv_bindings)
            for binding in bindings:
                before_shape = tuple(before_outputs[binding.output_name].shape())
                after_shape = tuple(after_outputs[binding.output_name].shape())
                if binding.state_name in kv_state_names:
                    axis = io_spec.stage0.kv_sequence_axis
                    assert after_shape[axis] == before_shape[axis] + 1
                else:
                    assert after_shape == before_shape

        for outputs in (stage1_runs[0][1], stage1_runs[1][1]):
            logits = outputs[io_spec.stage1.logits_output].numpy()
            assert logits.ndim == 3
            assert np.isfinite(logits).all()
    finally:
        engine.unload()
        gc.collect()