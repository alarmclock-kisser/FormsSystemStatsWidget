"""
Model-independent tests for ContextState, InferenceState, and reset_context.

These tests do NOT require a real ONNX model or CUDA.
They verify the state container, reset semantics, and lifecycle invariants.
"""

from __future__ import annotations

import unittest

from onnx_engine.context import ContextState, InferenceState
from onnx_engine.context.conversation import Conversation


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
