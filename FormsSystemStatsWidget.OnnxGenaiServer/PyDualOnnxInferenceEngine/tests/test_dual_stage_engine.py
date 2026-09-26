from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import numpy as np

from onnx_engine.context.state import ContextState
from onnx_engine.engine import InferenceEngine
from onnx_engine.errors import ModelValidationError
from onnx_engine.generation import (
    GenerationContext,
    GenerationEngine,
    GenerationRequest,
    SamplingConfig,
    StopConfig,
)
from onnx_engine.model.dual_stage_adapter import (
    DualStageOnnxAdapter,
    DualStageStepResult,
)
from onnx_engine.model.package import ModelPackageLoader
from onnx_engine.runtime.cuda import CudaRuntimeConfig


def _write_partition(root: Path, *, include_stage1: bool = True) -> tuple[Path, Path]:
    root.mkdir(parents=True, exist_ok=True)
    (root / "model.onnx").touch()
    partition_root = root / "partitioned"
    partition_root.mkdir()
    stage0_path = partition_root / "model.stage0.onnx"
    stage0_path.touch()
    stage1_path = partition_root / "model.stage1.onnx"
    if include_stage1:
        stage1_path.touch()
    (root / "genai_config.json").write_text(
        json.dumps(
            {
                "model": {
                    "type": "qwen3_5_text",
                    "decoder": {"head_size": 256},
                }
            }
        ),
        encoding="utf-8",
    )
    return stage0_path, stage1_path


class ModelPackagePartitionTests(unittest.TestCase):
    def test_loader_finds_complete_partition_pair(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage0_path, stage1_path = _write_partition(root)

            package = ModelPackageLoader().load(root)

        self.assertTrue(package.is_partitioned)
        self.assertEqual(package.stage0_path, stage0_path)
        self.assertEqual(package.stage1_path, stage1_path)
        self.assertEqual(package.model_path, root / "model.onnx")

    def test_loader_rejects_incomplete_partition_pair(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            _write_partition(Path(directory), include_stage1=False)

            with self.assertRaises(ModelValidationError):
                ModelPackageLoader().load(directory)


class InferenceEnginePartitionTests(unittest.TestCase):
    def test_load_uses_two_sessions_and_configured_devices(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stage0_path, stage1_path = _write_partition(root)
            managers = []

            class FakeSessionManager:
                def __init__(self, cuda, session, **kwargs) -> None:
                    self.cuda = cuda
                    self.paths: list[str] = []
                    self.closed = False
                    self.session = object()
                    managers.append(self)

                def load(self, path: str):
                    self.paths.append(path)
                    return self.session

                def close(self) -> None:
                    self.closed = True

            with (
                patch("onnx_engine.engine.OrtSessionManager", FakeSessionManager),
                patch("onnx_engine.engine.OnnxGraphInspector") as inspector_type,
                patch("onnx_engine.engine.TokenizerService"),
                patch("onnx_engine.engine.DualStageOnnxAdapter") as adapter_type,
                patch("onnx_engine.engine.GenerationEngine"),
            ):
                io_spec = object()
                inspector_type.return_value.infer_dual_stage.return_value = io_spec
                engine = InferenceEngine(
                    root,
                    cuda=CudaRuntimeConfig(device_id=0, stage1_device_id=1),
                    preload_cuda_dll_dependencies=False,
                )
                engine.load()

                self.assertEqual(managers[0].paths, [str(stage0_path)])
                self.assertEqual(managers[1].paths, [str(stage1_path)])
                self.assertEqual(managers[0].cuda.device_id, 0)
                self.assertEqual(managers[1].cuda.device_id, 1)
                self.assertEqual(adapter_type.call_args.args[2:4], (0, 1))
                self.assertEqual(adapter_type.call_args.args[4], io_spec)
                self.assertEqual(adapter_type.call_args.kwargs["model_type"], "qwen3_5_text")
                self.assertEqual(
                    adapter_type.call_args.kwargs["symbolic_dimensions"],
                    {"kv_cache_dim": 256},
                )

                engine.unload()

            self.assertTrue(all(manager.closed for manager in managers))


class FakeTokenizer:
    def encode_text(self, text: str):
        return SimpleNamespace(input_ids=np.asarray([[10, 11]], dtype=np.int64))

    def decode_token(self, token_id: int) -> str:
        return str(token_id)


class FakeDualStageAdapter(DualStageOnnxAdapter):
    def __init__(self) -> None:
        self.prefill_state_args = None
        self.decode_state_args = None
        self.prefill_stage0_state = {"stage0": object()}
        self.prefill_stage1_state = {"stage1": object()}
        self.final_stage0_state = {"stage0": object()}
        self.final_stage1_state = {"stage1": object()}

    def prefill(
        self,
        input_ids: np.ndarray,
        *,
        stage0_state=None,
        stage1_state=None,
        position_ids=None,
    ) -> DualStageStepResult:
        self.prefill_state_args = (stage0_state, stage1_state)
        return DualStageStepResult(
            logits=np.asarray([[[0.0, 1.0], [1.0, 0.0]]]),
            hidden_states=np.zeros((1, 2, 1)),
            stage0_state=self.prefill_stage0_state,
            stage1_state=self.prefill_stage1_state,
            sequence_length=int(input_ids.shape[1]),
        )

    def decode(
        self,
        token_id: int,
        *,
        position: int,
        stage0_state,
        stage1_state,
        position_ids=None,
    ) -> DualStageStepResult:
        self.decode_state_args = (token_id, position, stage0_state, stage1_state)
        return DualStageStepResult(
            logits=np.asarray([[[0.0, 1.0]]]),
            hidden_states=np.zeros((1, 1, 1)),
            stage0_state=self.final_stage0_state,
            stage1_state=self.final_stage1_state,
            sequence_length=position + 1,
        )


class GenerationEnginePartitionTests(unittest.TestCase):
    def test_generation_routes_persistent_state_through_both_stages(self) -> None:
        adapter = FakeDualStageAdapter()
        engine = GenerationEngine(FakeTokenizer(), adapter)
        generation = GenerationContext(state=ContextState(), prompt_text="prompt")
        request = GenerationRequest(
            messages=(),
            sampling=SamplingConfig(temperature=0),
            stopping=StopConfig(max_new_tokens=2),
        )

        chunks = list(engine.generate(generation, request))

        self.assertEqual(adapter.prefill_state_args, (None, None))
        self.assertEqual(adapter.decode_state_args[:2], (0, 2))
        self.assertIs(adapter.decode_state_args[2], adapter.prefill_stage0_state)
        self.assertIs(adapter.decode_state_args[3], adapter.prefill_stage1_state)
        self.assertTrue(generation.state.inference_state.is_partitioned)
        self.assertEqual(generation.state.inference_state.stage0_state, adapter.final_stage0_state)
        self.assertEqual(generation.state.inference_state.stage1_state, adapter.final_stage1_state)
        self.assertEqual(generation.state.model_state, {})
        self.assertEqual(len(chunks), 2)
        self.assertTrue(chunks[-1].finished)


if __name__ == "__main__":
    unittest.main()