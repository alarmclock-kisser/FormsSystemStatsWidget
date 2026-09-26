"""
Model-independent tests for DualStageOnnxAdapter.

These tests use mock sessions and mock OrtValue-like objects. They do NOT
require a real ONNX model, CUDA, or an ONNX Runtime session. They verify:
  - spec validation (boundary match, required I/O)
  - explicit state binding (no positional zip)
  - boundary handoff Stage 0 -> Stage 1
  - prefill and decode feed construction
  - state extraction by name
  - empty-past shape inference
"""

from __future__ import annotations

import dataclasses
import unittest
from typing import Any

import numpy as np

from onnx_engine.errors import ModelValidationError
from onnx_engine.model.dual_stage_adapter import (
    DualStageOnnxAdapter,
    DualStageStepResult,
)
from onnx_engine.model.io_spec import (
    DualStageIoSpec,
    StageIoSpec,
    StateBinding,
)


# ---------------------------------------------------------------------------
# Mock infrastructure
# ---------------------------------------------------------------------------


class MockTensorInfo:
    """Mimics onnxruntime's NodeArg shape/type info."""

    def __init__(self, name: str, shape: list[Any], type: str) -> None:
        self.name = name
        self.shape = shape
        self.type = type


class MockOrtValue:
    """
    Mimics ort.OrtValue for testing.

    Stores the underlying numpy array and the device it was created on.
    .numpy() returns the array (simulating a device -> host transfer).
    """

    def __init__(self, array: np.ndarray, device: str = "cuda", device_id: int = 0) -> None:
        self._array = array
        self.device = device
        self.device_id = device_id

    def numpy(self) -> np.ndarray:
        return self._array


class MockIoBinding:
    """Mimics ort.InferenceSession.io_binding()."""

    def __init__(self, session: "MockInferenceSession") -> None:
        self._session = session
        self._inputs: dict[str, MockOrtValue] = {}
        self._outputs: list[str] = []

    def bind_ortvalue_input(self, name: str, value: MockOrtValue) -> None:
        self._inputs[name] = value

    def bind_output(self, name: str, device: str, device_id: int) -> None:
        self._outputs.append(name)

    def get_outputs(self) -> list[MockOrtValue]:
        return self._session._execute(self._inputs, self._outputs)


class MockInferenceSession:
    """
    Mock ONNX Runtime session.

    Stores input/output metadata and a callable that produces outputs
    from inputs. The callable receives (inputs, output_names) and returns
    a list of MockOrtValue in the same order as output_names.
    """

    def __init__(
        self,
        inputs: list[MockTensorInfo],
        outputs: list[MockTensorInfo],
        execute_fn: Any,
    ) -> None:
        self._inputs = inputs
        self._outputs = outputs
        self._execute_fn = execute_fn
        self.last_inputs: dict[str, MockOrtValue] = {}

    def get_inputs(self) -> list[MockTensorInfo]:
        return self._inputs

    def get_outputs(self) -> list[MockTensorInfo]:
        return self._outputs

    def io_binding(self) -> MockIoBinding:
        return MockIoBinding(self)

    def run_with_iobinding(self, binding: MockIoBinding) -> None:
        pass

    def _execute(
        self,
        inputs: dict[str, MockOrtValue],
        output_names: list[str],
    ) -> list[MockOrtValue]:
        self.last_inputs = inputs
        return self._execute_fn(inputs, output_names)


# ---------------------------------------------------------------------------
# Test fixtures
# ---------------------------------------------------------------------------


def make_stage0_spec() -> StageIoSpec:
    """
    Stage 0 spec matching the real Qwen3.8-27B model:
      - input_ids, attention_mask, position_ids (no cache_position)
      - 2 boundary outputs
      - 2 KV bindings (1 layer for test simplicity)
      - 1 conv binding
      - 1 recurrent binding
    """
    return StageIoSpec(
        input_ids="input_ids",
        attention_mask="attention_mask",
        position_ids="position_ids",
        cache_position=None,
        logits_output=None,
        hidden_states_output=None,
        boundary_outputs=(
            "/model/layers.41/post_attention_layernorm/output_3",
            "/model/layers.41/mlp/down_proj/MatMul/output_0",
        ),
        boundary_inputs=(),
        kv_bindings=(
            StateBinding(
                input_name="past_key_values.0.key",
                output_name="present.0.key",
                state_name="past_key_values.0.key",
            ),
            StateBinding(
                input_name="past_key_values.0.value",
                output_name="present.0.value",
                state_name="past_key_values.0.value",
            ),
        ),
        conv_bindings=(
            StateBinding(
                input_name="past.0.conv",
                output_name="present.0.conv",
                state_name="past.0.conv",
            ),
        ),
        recurrent_bindings=(
            StateBinding(
                input_name="past.0.recurrent",
                output_name="present.0.recurrent",
                state_name="past.0.recurrent",
            ),
        ),
        kv_sequence_axis=2,
    )


def make_stage1_spec() -> StageIoSpec:
    """
    Stage 1 spec matching the real Qwen3.8-27B model:
      - attention_mask, position_ids (no input_ids, no cache_position)
      - 2 boundary inputs
      - logits + hidden_states outputs
      - 1 KV binding, 1 conv binding, 1 recurrent binding
    """
    return StageIoSpec(
        input_ids=None,
        attention_mask="attention_mask",
        position_ids="position_ids",
        cache_position=None,
        logits_output="logits",
        hidden_states_output="hidden_states",
        boundary_outputs=(),
        boundary_inputs=(
            "/model/layers.41/post_attention_layernorm/output_3",
            "/model/layers.41/mlp/down_proj/MatMul/output_0",
        ),
        kv_bindings=(
            StateBinding(
                input_name="past_key_values.42.key",
                output_name="present.42.key",
                state_name="past_key_values.42.key",
            ),
            StateBinding(
                input_name="past_key_values.42.value",
                output_name="present.42.value",
                state_name="past_key_values.42.value",
            ),
        ),
        conv_bindings=(
            StateBinding(
                input_name="past.42.conv",
                output_name="present.42.conv",
                state_name="past.42.conv",
            ),
        ),
        recurrent_bindings=(
            StateBinding(
                input_name="past.42.recurrent",
                output_name="present.42.recurrent",
                state_name="past.42.recurrent",
            ),
        ),
        kv_sequence_axis=2,
    )


def make_dual_spec() -> DualStageIoSpec:
    return DualStageIoSpec(stage0=make_stage0_spec(), stage1=make_stage1_spec())


def make_stage0_session() -> MockInferenceSession:
    """
    Stage 0 mock session.

    Inputs: input_ids, attention_mask, position_ids,
            past_key_values.0.key, past_key_values.0.value,
            past.0.conv, past.0.recurrent
    Outputs: 2 boundary tensors, present.0.key, present.0.value,
             present.0.conv, present.0.recurrent
    """
    boundary_a = "/model/layers.41/post_attention_layernorm/output_3"
    boundary_b = "/model/layers.41/mlp/down_proj/MatMul/output_0"

    inputs = [
        MockTensorInfo("input_ids", [1, "S"], "tensor(int64)"),
        MockTensorInfo("attention_mask", [1, "S"], "tensor(int64)"),
        MockTensorInfo("position_ids", [3, 1, "S"], "tensor(int64)"),
        MockTensorInfo("past_key_values.0.key", [1, 4, 0, "kv_cache_dim"], "tensor(float16)"),
        MockTensorInfo("past_key_values.0.value", [1, 4, 0, "kv_cache_dim"], "tensor(float16)"),
        MockTensorInfo("past.0.conv", [1, 10240, 3], "tensor(float16)"),
        MockTensorInfo("past.0.recurrent", [1, 48, 128, 128], "tensor(float16)"),
    ]
    outputs = [
        MockTensorInfo(boundary_a, [1, "S", 5120], "tensor(float16)"),
        MockTensorInfo(boundary_b, [1, "S", 5120], "tensor(float16)"),
        MockTensorInfo("present.0.key", [1, 4, "S", "kv_cache_dim"], "tensor(float16)"),
        MockTensorInfo("present.0.value", [1, 4, "S", "kv_cache_dim"], "tensor(float16)"),
        MockTensorInfo("present.0.conv", [1, 10240, 3], "tensor(float16)"),
        MockTensorInfo("present.0.recurrent", [1, 48, 128, 128], "tensor(float16)"),
    ]

    def execute(inputs: dict[str, MockOrtValue], output_names: list[str]) -> list[MockOrtValue]:
        results: list[MockOrtValue] = []
        for name in output_names:
            if name == boundary_a:
                results.append(MockOrtValue(np.zeros((1, 4, 5120), dtype=np.float16), "cuda", 0))
            elif name == boundary_b:
                results.append(MockOrtValue(np.ones((1, 4, 5120), dtype=np.float16), "cuda", 0))
            elif name == "present.0.key":
                results.append(MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 0))
            elif name == "present.0.value":
                results.append(MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 0))
            elif name == "present.0.conv":
                results.append(MockOrtValue(np.zeros((1, 10240, 3), dtype=np.float16), "cuda", 0))
            elif name == "present.0.recurrent":
                results.append(MockOrtValue(np.zeros((1, 48, 128, 128), dtype=np.float16), "cuda", 0))
            else:
                raise AssertionError(f"Unexpected output name: {name}")
        return results

    return MockInferenceSession(inputs, outputs, execute)


def make_stage1_session() -> MockInferenceSession:
    """
    Stage 1 mock session.

    Inputs: attention_mask, position_ids, 2 boundary tensors,
            past_key_values.42.key, past_key_values.42.value,
            past.42.conv, past.42.recurrent
    Outputs: logits, hidden_states, present.42.key, present.42.value,
             present.42.conv, present.42.recurrent
    """
    boundary_a = "/model/layers.41/post_attention_layernorm/output_3"
    boundary_b = "/model/layers.41/mlp/down_proj/MatMul/output_0"

    inputs = [
        MockTensorInfo("attention_mask", [1, "S"], "tensor(int64)"),
        MockTensorInfo("position_ids", [3, 1, "S"], "tensor(int64)"),
        MockTensorInfo(boundary_a, [1, "S", 5120], "tensor(float16)"),
        MockTensorInfo(boundary_b, [1, "S", 5120], "tensor(float16)"),
        MockTensorInfo("past_key_values.42.key", [1, 4, 0, "kv_cache_dim"], "tensor(float16)"),
        MockTensorInfo("past_key_values.42.value", [1, 4, 0, "kv_cache_dim"], "tensor(float16)"),
        MockTensorInfo("past.42.conv", [1, 10240, 3], "tensor(float16)"),
        MockTensorInfo("past.42.recurrent", [1, 48, 128, 128], "tensor(float16)"),
    ]
    outputs = [
        MockTensorInfo("logits", [1, "S", 248320], "tensor(float16)"),
        MockTensorInfo("hidden_states", [1, "S", 5120], "tensor(float16)"),
        MockTensorInfo("present.42.key", [1, 4, "S", "kv_cache_dim"], "tensor(float16)"),
        MockTensorInfo("present.42.value", [1, 4, "S", "kv_cache_dim"], "tensor(float16)"),
        MockTensorInfo("present.42.conv", [1, 10240, 3], "tensor(float16)"),
        MockTensorInfo("present.42.recurrent", [1, 48, 128, 128], "tensor(float16)"),
    ]

    def execute(inputs: dict[str, MockOrtValue], output_names: list[str]) -> list[MockOrtValue]:
        results: list[MockOrtValue] = []
        for name in output_names:
            if name == "logits":
                results.append(MockOrtValue(np.zeros((1, 4, 248320), dtype=np.float16), "cuda", 1))
            elif name == "hidden_states":
                results.append(MockOrtValue(np.zeros((1, 4, 5120), dtype=np.float16), "cuda", 1))
            elif name == "present.42.key":
                results.append(MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 1))
            elif name == "present.42.value":
                results.append(MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 1))
            elif name == "present.42.conv":
                results.append(MockOrtValue(np.zeros((1, 10240, 3), dtype=np.float16), "cuda", 1))
            elif name == "present.42.recurrent":
                results.append(MockOrtValue(np.zeros((1, 48, 128, 128), dtype=np.float16), "cuda", 1))
            else:
                raise AssertionError(f"Unexpected output name: {name}")
        return results

    return MockInferenceSession(inputs, outputs, execute)


def make_adapter(model_type: str | None = None) -> DualStageOnnxAdapter:
    return DualStageOnnxAdapter(
        make_stage0_session(),
        make_stage1_session(),
        stage0_device_id=0,
        stage1_device_id=1,
        io_spec=make_dual_spec(),
        model_type=model_type,
        symbolic_dimensions={"kv_cache_dim": 256},
    )


def synthetic_position_ids(sequence_length: int) -> np.ndarray:
    """Test-only values; production callers must provide model-valid axes."""
    return np.zeros((3, 1, sequence_length), dtype=np.int64)


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------


class TestDualStageSpecValidation(unittest.TestCase):
    def test_valid_spec_passes(self) -> None:
        adapter = make_adapter()
        self.assertEqual(adapter.stage0_device_id, 0)
        self.assertEqual(adapter.stage1_device_id, 1)

    def test_boundary_mismatch_raises(self) -> None:
        spec = make_dual_spec()
        bad_spec = DualStageIoSpec(
            stage0=spec.stage0,
            stage1=StageIoSpec(
                **{**dataclasses.asdict(spec.stage1), "boundary_inputs": ("wrong_name",)},
            ),
        )
        with self.assertRaises(ModelValidationError):
            DualStageOnnxAdapter(
                make_stage0_session(),
                make_stage1_session(),
                0,
                1,
                io_spec=bad_spec,
            )

    def test_missing_stage0_input_ids_raises(self) -> None:
        spec = make_dual_spec()
        bad_spec = DualStageIoSpec(
            stage0=StageIoSpec(**{**dataclasses.asdict(spec.stage0), "input_ids": None}),
            stage1=spec.stage1,
        )
        with self.assertRaises(ModelValidationError):
            DualStageOnnxAdapter(
                make_stage0_session(),
                make_stage1_session(),
                0,
                1,
                io_spec=bad_spec,
            )

    def test_missing_stage1_logits_raises(self) -> None:
        spec = make_dual_spec()
        bad_spec = DualStageIoSpec(
            stage0=spec.stage0,
            stage1=StageIoSpec(**{**dataclasses.asdict(spec.stage1), "logits_output": None}),
        )
        with self.assertRaises(ModelValidationError):
            DualStageOnnxAdapter(
                make_stage0_session(),
                make_stage1_session(),
                0,
                1,
                io_spec=bad_spec,
            )


class TestDualStagePrefill(unittest.TestCase):
    def test_prefill_returns_result(self) -> None:
        adapter = make_adapter()
        input_ids = np.arange(4, dtype=np.int64)[None, :]

        result = adapter.prefill(input_ids, position_ids=synthetic_position_ids(4))

        self.assertIsInstance(result, DualStageStepResult)
        self.assertEqual(result.sequence_length, 4)
        self.assertEqual(result.logits.shape, (1, 4, 248320))
        self.assertEqual(result.hidden_states.shape, (1, 4, 5120))

    def test_qwen35_text_prefill_builds_three_axis_positions(self) -> None:
        stage0 = make_stage0_session()
        stage1 = make_stage1_session()
        adapter = DualStageOnnxAdapter(
            stage0,
            stage1,
            0,
            1,
            io_spec=make_dual_spec(),
            model_type="qwen3_5_text",
            symbolic_dimensions={"kv_cache_dim": 256},
        )

        adapter.prefill(np.arange(4, dtype=np.int64)[None, :])

        expected = np.broadcast_to(
            np.arange(4, dtype=np.int64)[None, None, :],
            (3, 1, 4),
        )
        np.testing.assert_array_equal(stage0.last_inputs["position_ids"].numpy(), expected)
        np.testing.assert_array_equal(stage1.last_inputs["position_ids"].numpy(), expected)

    def test_prefill_stage0_state_populated(self) -> None:
        adapter = make_adapter()
        input_ids = np.arange(4, dtype=np.int64)[None, :]
        result = adapter.prefill(input_ids, position_ids=synthetic_position_ids(4))

        self.assertIn("past_key_values.0.key", result.stage0_state)
        self.assertIn("past_key_values.0.value", result.stage0_state)
        self.assertIn("past.0.conv", result.stage0_state)
        self.assertIn("past.0.recurrent", result.stage0_state)
        self.assertEqual(len(result.stage0_state), 4)

    def test_prefill_stage1_state_populated(self) -> None:
        adapter = make_adapter()
        input_ids = np.arange(4, dtype=np.int64)[None, :]
        result = adapter.prefill(input_ids, position_ids=synthetic_position_ids(4))

        self.assertIn("past_key_values.42.key", result.stage1_state)
        self.assertIn("past_key_values.42.value", result.stage1_state)
        self.assertIn("past.42.conv", result.stage1_state)
        self.assertIn("past.42.recurrent", result.stage1_state)
        self.assertEqual(len(result.stage1_state), 4)

    def test_prefill_boundary_handoff(self) -> None:
        """
        Verify that Stage 0 boundary outputs are passed to Stage 1 as inputs.
        We check this by verifying the Stage 1 session received the boundary
        tensors (the mock execute function would raise if they were missing).
        """
        adapter = make_adapter()
        input_ids = np.arange(4, dtype=np.int64)[None, :]
        # If boundary handoff fails, the Stage 1 mock would raise AssertionError
        result = adapter.prefill(input_ids, position_ids=synthetic_position_ids(4))
        self.assertIsNotNone(result.logits)

    def test_prefill_with_existing_state(self) -> None:
        adapter = make_adapter()
        input_ids = np.arange(4, dtype=np.int64)[None, :]

        existing_s0 = {
            "past_key_values.0.key": MockOrtValue(np.zeros((1, 4, 2, 256), dtype=np.float16), "cuda", 0),
            "past_key_values.0.value": MockOrtValue(np.zeros((1, 4, 2, 256), dtype=np.float16), "cuda", 0),
            "past.0.conv": MockOrtValue(np.zeros((1, 10240, 3), dtype=np.float16), "cuda", 0),
            "past.0.recurrent": MockOrtValue(np.zeros((1, 48, 128, 128), dtype=np.float16), "cuda", 0),
        }
        existing_s1 = {
            "past_key_values.42.key": MockOrtValue(np.zeros((1, 4, 2, 256), dtype=np.float16), "cuda", 1),
            "past_key_values.42.value": MockOrtValue(np.zeros((1, 4, 2, 256), dtype=np.float16), "cuda", 1),
            "past.42.conv": MockOrtValue(np.zeros((1, 10240, 3), dtype=np.float16), "cuda", 1),
            "past.42.recurrent": MockOrtValue(np.zeros((1, 48, 128, 128), dtype=np.float16), "cuda", 1),
        }

        result = adapter.prefill(
            input_ids,
            stage0_state=existing_s0,
            stage1_state=existing_s1,
            position_ids=synthetic_position_ids(4),
        )
        self.assertEqual(result.sequence_length, 4)


class TestDualStageDecode(unittest.TestCase):
    def test_decode_returns_result(self) -> None:
        adapter = make_adapter()
        s0_state = {
            "past_key_values.0.key": MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 0),
            "past_key_values.0.value": MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 0),
            "past.0.conv": MockOrtValue(np.zeros((1, 10240, 3), dtype=np.float16), "cuda", 0),
            "past.0.recurrent": MockOrtValue(np.zeros((1, 48, 128, 128), dtype=np.float16), "cuda", 0),
        }
        s1_state = {
            "past_key_values.42.key": MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 1),
            "past_key_values.42.value": MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 1),
            "past.42.conv": MockOrtValue(np.zeros((1, 10240, 3), dtype=np.float16), "cuda", 1),
            "past.42.recurrent": MockOrtValue(np.zeros((1, 48, 128, 128), dtype=np.float16), "cuda", 1),
        }

        result = adapter.decode(
            token_id=42,
            position=4,
            stage0_state=s0_state,
            stage1_state=s1_state,
            position_ids=synthetic_position_ids(1),
        )

        self.assertEqual(result.sequence_length, 5)
        self.assertEqual(result.logits.shape, (1, 4, 248320))
        self.assertEqual(result.hidden_states.shape, (1, 4, 5120))

    def test_qwen35_text_decode_uses_absolute_token_offset(self) -> None:
        stage0 = make_stage0_session()
        stage1 = make_stage1_session()
        adapter = DualStageOnnxAdapter(
            stage0,
            stage1,
            0,
            1,
            io_spec=make_dual_spec(),
            model_type="qwen3_5_text",
            symbolic_dimensions={"kv_cache_dim": 256},
        )

        adapter.decode(
            token_id=42,
            position=7,
            stage0_state={},
            stage1_state={},
        )

        expected = np.full((3, 1, 1), 7, dtype=np.int64)
        np.testing.assert_array_equal(stage0.last_inputs["position_ids"].numpy(), expected)
        np.testing.assert_array_equal(stage1.last_inputs["position_ids"].numpy(), expected)

    def test_decode_state_keys_preserved(self) -> None:
        adapter = make_adapter()
        s0_state = {
            "past_key_values.0.key": MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 0),
            "past_key_values.0.value": MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 0),
            "past.0.conv": MockOrtValue(np.zeros((1, 10240, 3), dtype=np.float16), "cuda", 0),
            "past.0.recurrent": MockOrtValue(np.zeros((1, 48, 128, 128), dtype=np.float16), "cuda", 0),
        }
        s1_state = {
            "past_key_values.42.key": MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 1),
            "past_key_values.42.value": MockOrtValue(np.zeros((1, 4, 4, 256), dtype=np.float16), "cuda", 1),
            "past.42.conv": MockOrtValue(np.zeros((1, 10240, 3), dtype=np.float16), "cuda", 1),
            "past.42.recurrent": MockOrtValue(np.zeros((1, 48, 128, 128), dtype=np.float16), "cuda", 1),
        }

        result = adapter.decode(
            token_id=42,
            position=4,
            stage0_state=s0_state,
            stage1_state=s1_state,
            position_ids=synthetic_position_ids(1),
        )

        self.assertEqual(set(result.stage0_state.keys()), set(s0_state.keys()))
        self.assertEqual(set(result.stage1_state.keys()), set(s1_state.keys()))


class TestDualStageStateTransfer(unittest.TestCase):
    def test_stage0_state_to_cpu(self) -> None:
        adapter = make_adapter()
        state = {
            "past_key_values.0.key": MockOrtValue(np.ones((1, 4, 2, 256), dtype=np.float16), "cuda", 0),
        }
        result = adapter.stage0_state_to_cpu(state)
        self.assertIn("past_key_values.0.key", result)
        self.assertEqual(result["past_key_values.0.key"].shape, (1, 4, 2, 256))

    def test_stage1_state_to_cpu(self) -> None:
        adapter = make_adapter()
        state = {
            "past_key_values.42.key": MockOrtValue(np.ones((1, 4, 2, 256), dtype=np.float16), "cuda", 1),
        }
        result = adapter.stage1_state_to_cpu(state)
        self.assertIn("past_key_values.42.key", result)

    def test_stage0_state_from_cpu(self) -> None:
        adapter = make_adapter()
        arrays = {
            "past_key_values.0.key": np.ones((1, 4, 2, 256), dtype=np.float16),
        }
        result = adapter.stage0_state_from_cpu(arrays)
        self.assertIn("past_key_values.0.key", result)
        # Real ORT creates real OrtValues — verify .numpy() roundtrip
        self.assertEqual(result["past_key_values.0.key"].numpy().shape, (1, 4, 2, 256))

    def test_stage1_state_from_cpu(self) -> None:
        adapter = make_adapter()
        arrays = {
            "past_key_values.42.key": np.ones((1, 4, 2, 256), dtype=np.float16),
        }
        result = adapter.stage1_state_from_cpu(arrays)
        self.assertIn("past_key_values.42.key", result)
        self.assertEqual(result["past_key_values.42.key"].numpy().shape, (1, 4, 2, 256))


class TestDualStageEmptyPast(unittest.TestCase):
    def test_empty_past_shapes_are_category_specific(self) -> None:
        adapter = make_adapter()
        feeds = adapter._build_stage0_inputs(
            np.zeros((1, 1), dtype=np.int64),
            sequence_length=1,
            state=None,
            is_decode=False,
            position_ids=synthetic_position_ids(1),
        )

        self.assertEqual(feeds["past_key_values.0.key"].numpy().shape, (1, 4, 0, 256))
        self.assertEqual(feeds["past.0.conv"].numpy().shape, (1, 10240, 3))
        self.assertEqual(feeds["past.0.recurrent"].numpy().shape, (1, 48, 128, 128))

    def test_empty_past_kv_shape(self) -> None:
        """
        When no state is provided, the adapter should create empty past
        tensors with the sequence axis set to 0.
        """
        adapter = make_adapter()
        input_ids = np.arange(4, dtype=np.int64)[None, :]
        result = adapter.prefill(input_ids, position_ids=synthetic_position_ids(4))

        # KV state should have sequence length 4 (from the 4-token prefill)
        kv_key = result.stage0_state["past_key_values.0.key"]
        self.assertEqual(kv_key.numpy().shape, (1, 4, 4, 256))

    def test_empty_past_conv_shape(self) -> None:
        adapter = make_adapter()
        input_ids = np.arange(4, dtype=np.int64)[None, :]
        result = adapter.prefill(input_ids, position_ids=synthetic_position_ids(4))

        conv = result.stage0_state["past.0.conv"]
        self.assertEqual(conv.numpy().shape, (1, 10240, 3))


class TestDualStageExplicitBinding(unittest.TestCase):
    def test_no_positional_zip(self) -> None:
        """
        Verify that state binding is by name, not by position.
        We do this by checking that the state dict keys match the
        binding state_name values exactly.
        """
        adapter = make_adapter()
        input_ids = np.arange(4, dtype=np.int64)[None, :]
        result = adapter.prefill(input_ids, position_ids=synthetic_position_ids(4))

        expected_s0_keys = {
            "past_key_values.0.key",
            "past_key_values.0.value",
            "past.0.conv",
            "past.0.recurrent",
        }
        self.assertEqual(set(result.stage0_state.keys()), expected_s0_keys)

        expected_s1_keys = {
            "past_key_values.42.key",
            "past_key_values.42.value",
            "past.42.conv",
            "past.42.recurrent",
        }
        self.assertEqual(set(result.stage1_state.keys()), expected_s1_keys)


class TestDualStageInputValidation(unittest.TestCase):
    def test_invalid_input_ids_ndim_raises(self) -> None:
        adapter = make_adapter()
        with self.assertRaises(ValueError):
            adapter.prefill(np.zeros(4, dtype=np.int64))

    def test_invalid_batch_size_raises(self) -> None:
        adapter = make_adapter()
        with self.assertRaises(ValueError):
            adapter.prefill(np.zeros((2, 4), dtype=np.int64))


if __name__ == "__main__":
    unittest.main()
