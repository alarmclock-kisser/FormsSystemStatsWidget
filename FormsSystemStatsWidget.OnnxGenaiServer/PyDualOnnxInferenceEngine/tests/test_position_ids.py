from __future__ import annotations

import unittest

import numpy as np

from onnx_engine.errors import GenerationError
from onnx_engine.model.position_ids import build_text_position_ids


class TextPositionIdsTests(unittest.TestCase):
    def test_qwen35_text_positions_repeat_sequential_text_positions(self) -> None:
        result = build_text_position_ids(
            "qwen3_5_text",
            (3, 1, "sequence_length"),
            start_position=7,
            sequence_length=3,
        )

        expected = np.asarray(
            [
                [[7, 8, 9]],
                [[7, 8, 9]],
                [[7, 8, 9]],
            ],
            dtype=np.int64,
        )
        np.testing.assert_array_equal(result, expected)

    def test_unknown_model_type_is_not_guessed(self) -> None:
        with self.assertRaises(GenerationError):
            build_text_position_ids(
                "unknown",
                (3, 1, "sequence_length"),
                start_position=0,
                sequence_length=1,
            )

    def test_graph_shape_must_match_requested_sequence(self) -> None:
        with self.assertRaises(GenerationError):
            build_text_position_ids(
                "qwen3_5_text",
                (3, 1, 4),
                start_position=0,
                sequence_length=3,
            )

    def test_graph_must_declare_three_mrope_axes(self) -> None:
        with self.assertRaises(GenerationError):
            build_text_position_ids(
                "qwen3_5_text",
                (2, 1, "sequence_length"),
                start_position=0,
                sequence_length=1,
            )


if __name__ == "__main__":
    unittest.main()