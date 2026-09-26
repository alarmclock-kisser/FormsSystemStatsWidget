from __future__ import annotations

from dataclasses import dataclass
from typing import Sequence

import numpy as np


@dataclass(slots=True, frozen=True)
class SamplingConfig:
    temperature: float = 0.6
    top_k: int = 20
    top_p: float = 0.9
    typical_p: float = 1.0
    min_p: float = 0.0
    repetition_penalty: float = 1.0
    frequency_penalty: float = 0.0
    presence_penalty: float = 0.0
    seed: int | None = None

    def __post_init__(self) -> None:
        if not 0.0 < self.typical_p <= 1.0:
            raise ValueError("typical_p must be greater than 0 and at most 1.")


class Sampler:
    def __init__(self, config: SamplingConfig) -> None:
        self._config = config
        self._rng = np.random.default_rng(config.seed)

    def sample(self, logits: np.ndarray, history: Sequence[int]) -> int:
        scores = np.asarray(logits, dtype=np.float64).copy()
        if scores.ndim == 1:
            vector = scores
        else:
            vector = scores.reshape(-1)

        self._apply_repetition(vector, history)
        self._apply_frequency_presence(vector, history)

        temperature = float(self._config.temperature)
        if temperature <= 0:
            return int(np.argmax(vector))

        vector /= temperature

        candidates = np.arange(vector.shape[0], dtype=np.int64)

        if self._config.top_k > 0 and self._config.top_k < candidates.size:
            top_indices = np.argpartition(
                vector,
                -self._config.top_k,
            )[-self._config.top_k:]
            candidates = top_indices

        candidate_logits = vector[candidates]

        if self._config.min_p > 0:
            candidate_logits, candidates = self._apply_min_p(
                candidate_logits,
                candidates,
                self._config.min_p,
            )

        probabilities = self._softmax(candidate_logits)

        if self._config.typical_p < 1.0:
            probabilities, candidates = self._apply_typical_p(
                probabilities,
                candidates,
                self._config.typical_p,
            )

        if self._config.top_p < 1.0:
            order = np.argsort(probabilities)[::-1]
            sorted_probs = probabilities[order]
            cumulative = np.cumsum(sorted_probs)

            cutoff = cumulative > self._config.top_p
            if cutoff.any():
                first = int(np.argmax(cutoff))
                keep = order[: max(first + 1, 1)]
                candidates = candidates[keep]
                probabilities = probabilities[keep]
                probabilities /= probabilities.sum()

        return int(
            self._rng.choice(
                candidates,
                p=probabilities,
            )
        )

    def _apply_repetition(self, logits: np.ndarray, history: Sequence[int]) -> None:
        penalty = float(self._config.repetition_penalty)
        if penalty <= 1.0:
            return

        for token_id in set(history):
            if token_id < 0 or token_id >= logits.size:
                continue

            if logits[token_id] < 0:
                logits[token_id] *= penalty
            else:
                logits[token_id] /= penalty

    def _apply_frequency_presence(
        self,
        logits: np.ndarray,
        history: Sequence[int],
    ) -> None:
        if not history:
            return

        counts = np.bincount(
            np.asarray(history, dtype=np.int64),
            minlength=logits.size,
        )

        frequency = float(self._config.frequency_penalty)
        presence = float(self._config.presence_penalty)

        if frequency:
            logits -= frequency * counts

        if presence:
            logits -= presence * (counts > 0)

    @staticmethod
    def _apply_min_p(
        logits: np.ndarray,
        candidates: np.ndarray,
        min_p: float,
    ) -> tuple[np.ndarray, np.ndarray]:
        probabilities = Sampler._softmax(logits)
        max_probability = float(probabilities.max())
        threshold = max_probability * min_p
        mask = probabilities >= threshold

        if not mask.any():
            mask[np.argmax(probabilities)] = True

        return logits[mask], candidates[mask]

    @staticmethod
    def _apply_typical_p(
        probabilities: np.ndarray,
        candidates: np.ndarray,
        typical_p: float,
    ) -> tuple[np.ndarray, np.ndarray]:
        safe_probabilities = np.maximum(probabilities, np.finfo(np.float64).tiny)
        entropy = -np.sum(probabilities * np.log(safe_probabilities))
        surprise = -np.log(safe_probabilities)
        order = np.argsort(np.abs(surprise - entropy), kind="stable")
        cumulative = np.cumsum(probabilities[order])
        cutoff = int(np.searchsorted(cumulative, typical_p, side="left"))
        selected = order[: cutoff + 1]
        selected_probabilities = probabilities[selected]
        selected_probabilities = selected_probabilities / selected_probabilities.sum()
        return selected_probabilities, candidates[selected]

    @staticmethod
    def _softmax(values: np.ndarray) -> np.ndarray:
        shifted = values - np.max(values)
        exp = np.exp(shifted)
        total = exp.sum()

        if not np.isfinite(total) or total <= 0:
            return np.full(
                values.shape,
                1.0 / values.size,
                dtype=np.float64,
            )

        return exp / total
