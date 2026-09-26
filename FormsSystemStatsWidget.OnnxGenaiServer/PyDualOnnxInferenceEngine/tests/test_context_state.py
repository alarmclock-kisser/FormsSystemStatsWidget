"""
Model-independent tests for ContextState, InferenceState, and reset_context.

These tests do NOT require a real ONNX model or CUDA.
They verify the state container, reset semantics, and lifecycle invariants.
"""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import numpy as np

from onnx_engine.context import ContextState, InferenceState
from onnx_engine.context.snapshot import ContextSnapshotStore
from onnx_engine.context.conversation import Conversation
from onnx_engine.errors import ModelValidationError
from onnx_engine.model.adapter import CausalOnnxAdapter
from onnx_engine.model.dual_stage_adapter import DualStageOnnxAdapter
from onnx_engine.model.io_spec import (
    DualStageIoSpec,
    ModelIoSpec,
    StageIoSpec,
    StateBinding,
)


class FakeOrtValue:
    def __init__(self, array: np.ndarray, device_id: int) -> None:
        self.array = np.asarray(array).copy()
        self.device_id = device_id

    def numpy(self) -> np.ndarray:
        return self.array.copy()

    @classmethod
    def ortvalue_from_numpy(
        cls,
        array: np.ndarray,
        device_type: str,
        device_id: int,
    ) -> "FakeOrtValue":
        if device_type != "cuda":
            raise ValueError("Expected CUDA restore.")
        return cls(array, device_id)


def _state_binding(name: str) -> StateBinding:
    return StateBinding(
        input_name=f"input.{name}",
        output_name=f"output.{name}",
        state_name=name,
    )


def _dual_snapshot_adapter(
    stage0_device_id: int = 0,
    stage1_device_id: int = 1,
) -> DualStageOnnxAdapter:
    adapter = object.__new__(DualStageOnnxAdapter)
    adapter._stage0_device_id = stage0_device_id
    adapter._stage1_device_id = stage1_device_id
    stage0_bindings = (
        _state_binding("kv.0.key"),
        _state_binding("conv.0"),
        _state_binding("recurrent.0"),
    )
    stage1_bindings = (
        _state_binding("kv.1.key"),
        _state_binding("conv.1"),
        _state_binding("recurrent.1"),
    )
    adapter._io_spec = DualStageIoSpec(
        stage0=StageIoSpec(
            boundary_outputs=("boundary.hidden",),
            kv_bindings=(stage0_bindings[0],),
            conv_bindings=(stage0_bindings[1],),
            recurrent_bindings=(stage0_bindings[2],),
        ),
        stage1=StageIoSpec(
            boundary_inputs=("boundary.hidden",),
            kv_bindings=(stage1_bindings[0],),
            conv_bindings=(stage1_bindings[1],),
            recurrent_bindings=(stage1_bindings[2],),
        ),
    )
    node_args = lambda bindings: {
        binding.input_name: SimpleNamespace(
            shape=[1, 2, 3],
            type="tensor(float16)",
        )
        for binding in bindings
    }
    adapter._stage0_input_info = node_args(stage0_bindings)
    adapter._stage1_input_info = node_args(stage1_bindings)
    return adapter


def _single_snapshot_adapter(device_id: int = 0) -> CausalOnnxAdapter:
    adapter = object.__new__(CausalOnnxAdapter)
    binding = _state_binding("stage0.legacy-looking-name")
    adapter._device_id = device_id
    adapter._io_spec = ModelIoSpec(
        input_ids="input_ids",
        logits_output="logits",
        past_inputs=(binding,),
    )
    adapter._input_info = {
        binding.input_name: SimpleNamespace(
            shape=[1, 2, 3],
            type="tensor(float16)",
        )
    }
    return adapter


class TestInferenceState(unittest.TestCase):
    def test_initial_state_is_empty(self) -> None:
        state = InferenceState()
        self.assertTrue(state.is_empty)
        self.assertFalse(state.is_partitioned)
        self.assertEqual(len(state.state), 0)
        self.assertEqual(len(state.stage0_state), 0)
        self.assertEqual(len(state.stage1_state), 0)

    def test_reset_clears_all_state(self) -> None:
        state = InferenceState()
        # Simulate populated state (no real OrtValue needed for container test)
        state.state["kv.0.key"] = object()
        state.state["kv.0.value"] = object()
        state.stage0_state["s0_kv"] = object()
        state.stage1_state["s1_kv"] = object()
        state.is_partitioned = True

        self.assertFalse(state.is_empty)

        state.reset()

        self.assertTrue(state.is_empty)
        self.assertFalse(state.is_partitioned)
        self.assertEqual(len(state.state), 0)
        self.assertEqual(len(state.stage0_state), 0)
        self.assertEqual(len(state.stage1_state), 0)

    def test_single_model_state(self) -> None:
        state = InferenceState()
        state.state["past_key_values.0.key"] = object()
        state.state["past_key_values.0.value"] = object()
        self.assertFalse(state.is_empty)
        self.assertFalse(state.is_partitioned)

    def test_partition_state(self) -> None:
        state = InferenceState()
        state.stage0_state["s0_kv"] = object()
        state.stage1_state["s1_kv"] = object()
        state.is_partitioned = True
        self.assertFalse(state.is_empty)
        self.assertTrue(state.is_partitioned)


class TestContextSnapshotStore(unittest.TestCase):
    def test_dual_snapshot_round_trip_preserves_names_devices_and_state_kinds(self) -> None:
        adapter = _dual_snapshot_adapter()
        context = ContextState(
            token_ids=[10, 11],
            messages=[{"role": "user", "content": "hello"}],
            generated_token_ids=[11],
        )
        context.inference_state.is_partitioned = True
        for index, binding in enumerate(adapter.io_spec.stage0.all_state_bindings):
            context.inference_state.stage0_state[binding.state_name] = FakeOrtValue(
                np.full((1, 2, 3), index + 1, dtype=np.float16), 0
            )
        for index, binding in enumerate(adapter.io_spec.stage1.all_state_bindings):
            context.inference_state.stage1_state[binding.state_name] = FakeOrtValue(
                np.full((1, 2, 3), index + 4, dtype=np.float16), 1
            )

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "dual-context.npz"
            store = ContextSnapshotStore()
            store.save(path, context, adapter, model_id="qwen-test")

            with np.load(path, allow_pickle=False) as archive:
                metadata = json.loads(str(archive["__metadata__"].item()))
                state_arrays = [
                    name for name in archive.files if name.startswith("__state__")
                ]
            self.assertEqual(len(state_arrays), 6)
            self.assertFalse(any("boundary.hidden" in name for name in state_arrays))
            self.assertEqual(
                metadata["contract"]["devices"],
                {"stage0_state": 0, "stage1_state": 1},
            )

            with patch(
                "onnx_engine.context.inference_state.ort",
                SimpleNamespace(OrtValue=FakeOrtValue),
            ):
                restored = store.load(path, adapter, model_id="qwen-test")

        self.assertEqual(restored.token_ids, [10, 11])
        self.assertEqual(restored.messages, context.messages)
        self.assertEqual(restored.generated_token_ids, [11])
        self.assertTrue(restored.inference_state.is_partitioned)
        self.assertEqual(
            set(restored.inference_state.stage0_state),
            set(context.inference_state.stage0_state),
        )
        self.assertEqual(
            set(restored.inference_state.stage1_state),
            set(context.inference_state.stage1_state),
        )
        for name, value in restored.inference_state.stage0_state.items():
            self.assertEqual(value.device_id, 0)
            np.testing.assert_array_equal(
                value.array,
                context.inference_state.stage0_state[name].array,
            )
        for name, value in restored.inference_state.stage1_state.items():
            self.assertEqual(value.device_id, 1)
            np.testing.assert_array_equal(
                value.array,
                context.inference_state.stage1_state[name].array,
            )

    def test_empty_dual_context_can_be_snapshotted_before_prefill(self) -> None:
        adapter = _dual_snapshot_adapter()
        context = ContextState()

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "empty-context.npz"
            store = ContextSnapshotStore()
            store.save(path, context, adapter, model_id="qwen-test")
            with patch(
                "onnx_engine.context.inference_state.ort",
                SimpleNamespace(OrtValue=FakeOrtValue),
            ):
                restored = store.load(path, adapter, model_id="qwen-test")

        self.assertTrue(restored.inference_state.is_partitioned)
        self.assertTrue(restored.inference_state.is_empty)

    def test_single_snapshot_preserves_state_and_device(self) -> None:
        adapter = _single_snapshot_adapter(device_id=2)
        context = ContextState()
        context.inference_state.state["stage0.legacy-looking-name"] = FakeOrtValue(
            np.ones((1, 2, 3), dtype=np.float16), 2
        )

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "single-context.npz"
            store = ContextSnapshotStore()
            store.save(path, context, adapter, model_id="single-test")
            with patch(
                "onnx_engine.context.inference_state.ort",
                SimpleNamespace(OrtValue=FakeOrtValue),
            ):
                restored = store.load(path, adapter, model_id="single-test")

        self.assertFalse(restored.inference_state.is_partitioned)
        self.assertIn("stage0.legacy-looking-name", restored.inference_state.state)
        self.assertEqual(
            restored.inference_state.state["stage0.legacy-looking-name"].device_id,
            2,
        )

    def test_incompatible_or_partial_snapshot_is_rejected(self) -> None:
        adapter = _dual_snapshot_adapter()
        context = ContextState()
        context.inference_state.is_partitioned = True
        for binding in adapter.io_spec.stage0.all_state_bindings:
            context.inference_state.stage0_state[binding.state_name] = FakeOrtValue(
                np.ones((1, 2, 3), dtype=np.float16), 0
            )
        for binding in adapter.io_spec.stage1.all_state_bindings:
            context.inference_state.stage1_state[binding.state_name] = FakeOrtValue(
                np.ones((1, 2, 3), dtype=np.float16), 1
            )

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "dual-context.npz"
            store = ContextSnapshotStore()
            store.save(path, context, adapter, model_id="qwen-test")
            with self.assertRaises(ModelValidationError):
                store.load(path, adapter, model_id="different-model")

            partial = ContextState()
            partial.inference_state.is_partitioned = True
            partial.inference_state.stage0_state = dict(
                context.inference_state.stage0_state
            )
            partial.inference_state.stage0_state.pop(
                next(iter(partial.inference_state.stage0_state))
            )
            partial.inference_state.stage1_state = dict(
                context.inference_state.stage1_state
            )
            with self.assertRaises(ModelValidationError):
                store.save(path, partial, adapter, model_id="qwen-test")

            with np.load(path, allow_pickle=False) as archive:
                payload = {name: archive[name] for name in archive.files}
            state_array = next(
                name for name in payload if name.startswith("__state__stage0_state__")
            )
            payload[state_array] = np.ones((1, 2, 2), dtype=np.float16)
            malformed_path = Path(directory) / "malformed-context.npz"
            np.savez_compressed(malformed_path, **payload)
            with self.assertRaises(ModelValidationError):
                store.load(malformed_path, adapter, model_id="qwen-test")


class TestContextState(unittest.TestCase):
    def test_initial_context_is_empty(self) -> None:
        ctx = ContextState()
        self.assertEqual(ctx.position, 0)
        self.assertEqual(len(ctx.token_ids), 0)
        self.assertEqual(len(ctx.messages), 0)
        self.assertEqual(len(ctx.generated_token_ids), 0)
        self.assertTrue(ctx.inference_state.is_empty)

    def test_append_tokens(self) -> None:
        ctx = ContextState()
        ctx.append_tokens([1, 2, 3])
        self.assertEqual(ctx.position, 3)
        self.assertEqual(ctx.token_ids, [1, 2, 3])

    def test_append_generated(self) -> None:
        ctx = ContextState()
        ctx.append_tokens([1, 2])
        ctx.append_generated(42)
        self.assertEqual(ctx.position, 3)
        self.assertEqual(ctx.token_ids, [1, 2, 42])
        self.assertEqual(ctx.generated_token_ids, [42])

    def test_append_message(self) -> None:
        ctx = ContextState()
        ctx.append_message({"role": "user", "content": "Hello"})
        self.assertEqual(len(ctx.messages), 1)
        self.assertEqual(ctx.messages[0]["role"], "user")

    def test_model_state_delegates_to_inference_state(self) -> None:
        ctx = ContextState()
        # Setting via deprecated alias
        ctx.model_state = {"kv.0.key": object()}
        self.assertIn("kv.0.key", ctx.inference_state.state)

        # Reading via deprecated alias
        ctx.inference_state.state["kv.0.value"] = object()
        self.assertIn("kv.0.value", ctx.model_state)

    def test_reset_clears_conversation_and_state(self) -> None:
        ctx = ContextState()
        ctx.append_tokens([1, 2, 3])
        ctx.append_message({"role": "user", "content": "Hello"})
        ctx.append_generated(42)
        ctx.inference_state.state["kv.0.key"] = object()

        self.assertEqual(ctx.position, 4)
        self.assertEqual(len(ctx.messages), 1)
        self.assertFalse(ctx.inference_state.is_empty)

        ctx.reset()

        self.assertEqual(ctx.position, 0)
        self.assertEqual(len(ctx.messages), 0)
        self.assertEqual(len(ctx.generated_token_ids), 0)
        self.assertTrue(ctx.inference_state.is_empty)

    def test_clone_metadata_only_excludes_state(self) -> None:
        ctx = ContextState()
        ctx.append_tokens([1, 2])
        ctx.append_message({"role": "user", "content": "Hi"})
        ctx.inference_state.state["kv"] = object()

        clone = ctx.clone_metadata_only()
        self.assertEqual(clone.token_ids, [1, 2])
        self.assertEqual(clone.messages, ctx.messages)
        self.assertTrue(clone.inference_state.is_empty)

    def test_cpu_state_delegates_to_inference_state(self) -> None:
        ctx = ContextState()
        # to_cpu() calls .numpy() on OrtValues — without real OrtValues
        # we verify the delegation path works (empty state returns empty dict)
        result = ctx.cpu_state()
        self.assertEqual(result, {})


class TestConversation(unittest.TestCase):
    def test_add_and_extend(self) -> None:
        conv = Conversation()
        conv.add("user", "Hello")
        conv.extend([{"role": "assistant", "content": "Hi"}])
        self.assertEqual(len(conv.messages), 2)
        self.assertEqual(conv.messages[0]["role"], "user")
        self.assertEqual(conv.messages[1]["role"], "assistant")

    def test_clear(self) -> None:
        conv = Conversation()
        conv.add("user", "Hello")
        conv.clear()
        self.assertEqual(len(conv.messages), 0)


class TestResetSemantics(unittest.TestCase):
    """
    Verify that reset clears conversation + state but does NOT
    require model reload (session/tokenizer/adapter remain loaded).
    This is a container-level test; the actual session lifecycle
    is verified by the InferenceEngine integration.
    """

    def test_reset_preserves_container_identity(self) -> None:
        ctx = ContextState()
        ctx.append_tokens([1, 2, 3])
        ctx.inference_state.state["kv"] = object()

        original_id = id(ctx)
        ctx.reset()
        # Same object, just cleared
        self.assertEqual(id(ctx), original_id)
        self.assertTrue(ctx.inference_state.is_empty)

    def test_reset_is_idempotent(self) -> None:
        ctx = ContextState()
        ctx.reset()
        ctx.reset()
        ctx.reset()
        self.assertTrue(ctx.inference_state.is_empty)
        self.assertEqual(ctx.position, 0)

    def test_reuse_after_reset(self) -> None:
        ctx = ContextState()
        ctx.append_tokens([1, 2, 3])
        ctx.append_generated(42)
        ctx.reset()

        # Context is reusable after reset
        ctx.append_tokens([10, 20])
        ctx.append_generated(30)
        self.assertEqual(ctx.position, 3)
        self.assertEqual(ctx.token_ids, [10, 20, 30])
        self.assertEqual(ctx.generated_token_ids, [30])


if __name__ == "__main__":
    unittest.main()
