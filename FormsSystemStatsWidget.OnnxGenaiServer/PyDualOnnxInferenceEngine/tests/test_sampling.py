from __future__ import annotations

import unittest

import numpy as np

from onnx_engine.generation.sampling import Sampler, SamplingConfig


class SamplingTests(unittest.TestCase):
    def test_greedy_is_deterministic(self) -> None:
        sampler = Sampler(SamplingConfig(temperature=0.0))
        logits = np.asarray([0.1, 2.0, 1.0])

        self.assertEqual(sampler.sample(logits, []), 1)
        self.assertEqual(sampler.sample(logits, []), 1)

    def test_typical_p_filters_by_surprise_distance(self) -> None:
        probabilities = np.asarray([0.4, 0.3, 0.3], dtype=np.float64)
        candidates = np.asarray([10, 11, 12], dtype=np.int64)

        filtered_probabilities, filtered_candidates = Sampler._apply_typical_p(
            probabilities,
            candidates,
            0.5,
        )

        self.assertEqual(set(filtered_candidates.tolist()), {11, 12})
        self.assertAlmostEqual(float(filtered_probabilities.sum()), 1.0)

    def test_typical_p_must_be_in_open_closed_unit_interval(self) -> None:
        with self.assertRaises(ValueError):
            SamplingConfig(typical_p=0.0)
        with self.assertRaises(ValueError):
            SamplingConfig(typical_p=1.01)


if __name__ == "__main__":
    unittest.main()