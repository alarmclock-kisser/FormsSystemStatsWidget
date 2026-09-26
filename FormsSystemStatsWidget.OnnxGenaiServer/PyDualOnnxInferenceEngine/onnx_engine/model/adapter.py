from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping

import numpy as np
import onnxruntime as ort

from ..errors import ModelValidationError
from .inspector import OnnxGraphInspector
from .io_spec import ModelIoSpec


@dataclass(slots=True)
class ModelStepResult:
    logits: np.ndarray
    state: dict[str, ort.OrtValue]
    sequence_length: int


class CausalOnnxAdapter:
    """
    Generic causal decoder adapter.

    The adapter knows how to construct the common:
      input_ids / attention_mask / position_ids / cache_position
      + past state
      -> logits / present state

    Model-specific quirks belong in model_io.json rather than in the engine.
    """

    def __init__(
        self,
        session: ort.InferenceSession,
        device_id: int,
        io_spec: ModelIoSpec | None = None,
    ) -> None:
        self._session = session
        self._device_id = device_id
        self._io_spec = io_spec or OnnxGraphInspector().infer(session)

        self._input_info = {
            item.name: item
            for item in session.get_inputs()
        }
        self._output_info = {
            item.name: item
            for item in session.get_outputs()
        }

        self._validate_spec()

    @property
    def io_spec(self) -> ModelIoSpec:
        return self._io_spec

    def prefill(
        self,
        input_ids: np.ndarray,
        *,
        state: Mapping[str, ort.OrtValue] | None = None,
    ) -> ModelStepResult:
        self._validate_ids(input_ids)

        sequence_length = int(input_ids.shape[1])
        feeds = self._build_inputs(
            input_ids,
            sequence_length=sequence_length,
            state=state,
            is_decode=False,
        )

        outputs = self._run_cuda(feeds)
        logits = self._extract_logits(outputs)
        next_state = self._extract_state(outputs)

        return ModelStepResult(
            logits=logits,
            state=next_state,
            sequence_length=sequence_length,
        )

    def decode(
        self,
        token_id: int,
        *,
        position: int,
        state: Mapping[str, ort.OrtValue],
    ) -> ModelStepResult:
        input_ids = np.asarray([[token_id]], dtype=np.int64)

        feeds = self._build_inputs(
            input_ids,
            sequence_length=position + 1,
            state=state,
            is_decode=True,
            position=position,
        )

        outputs = self._run_cuda(feeds)
        logits = self._extract_logits(outputs)
        next_state = self._extract_state(outputs)

        return ModelStepResult(
            logits=logits,
            state=next_state,
            sequence_length=position + 1,
        )

    def state_to_cpu(self, state: Mapping[str, ort.OrtValue]) -> dict[str, np.ndarray]:
        result: dict[str, np.ndarray] = {}

        for name, value in state.items():
            result[name] = value.numpy()

        return result

    def state_from_cpu(
        self,
        state: Mapping[str, np.ndarray],
    ) -> dict[str, ort.OrtValue]:
        return {
            name: self._to_cuda_value(value)
            for name, value in state.items()
        }

    def _build_inputs(
        self,
        input_ids: np.ndarray,
        *,
        sequence_length: int,
        state: Mapping[str, ort.OrtValue] | None,
        is_decode: bool,
        position: int | None = None,
    ) -> dict[str, ort.OrtValue | np.ndarray]:
        feeds: dict[str, ort.OrtValue | np.ndarray] = {}

        feeds[self._io_spec.input_ids] = self._to_cuda_value(input_ids)

        if self._io_spec.attention_mask:
            mask = np.ones((1, sequence_length), dtype=np.int64)
            feeds[self._io_spec.attention_mask] = self._to_cuda_value(mask)

        if self._io_spec.position_ids:
            if is_decode:
                position_ids = np.asarray([[position]], dtype=np.int64)
            else:
                position_ids = np.arange(sequence_length, dtype=np.int64)[None, :]
            feeds[self._io_spec.position_ids] = self._to_cuda_value(position_ids)

        if self._io_spec.cache_position:
            if is_decode:
                cache_position = np.asarray([position], dtype=np.int64)
            else:
                cache_position = np.arange(sequence_length, dtype=np.int64)
            feeds[self._io_spec.cache_position] = self._to_cuda_value(cache_position)

        for binding in self._io_spec.past_inputs:
            if state is not None and binding.state_name in state:
                feeds[binding.input_name] = state[binding.state_name]
            else:
                feeds[binding.input_name] = self._empty_past(binding.input_name)

        return feeds

    def _empty_past(self, input_name: str) -> ort.OrtValue:
        info = self._input_info.get(input_name)
        if info is None:
            raise ModelValidationError(f"Unknown past input: {input_name}")

        shape = list(info.shape)

        if len(shape) <= self._io_spec.past_sequence_axis:
            raise ModelValidationError(
                f"Cannot infer empty past shape for {input_name}: {info.shape}"
            )

        axis = self._io_spec.past_sequence_axis
        shape[axis] = 0

        for index, value in enumerate(shape):
            if not isinstance(value, int) or value < 0:
                shape[index] = 1

        dtype = self._numpy_dtype(info.type)
        return self._to_cuda_value(np.zeros(tuple(shape), dtype=dtype))

    def _run_cuda(
        self,
        inputs: Mapping[str, ort.OrtValue | np.ndarray],
    ) -> dict[str, ort.OrtValue]:
        binding = self._session.io_binding()

        for name, value in inputs.items():
            if isinstance(value, ort.OrtValue):
                binding.bind_ortvalue_input(name, value)
            else:
                binding.bind_ortvalue_input(name, self._to_cuda_value(value))

        output_names = [
            self._io_spec.logits_output,
            *(binding.output_name for binding in ()),
        ]

        for binding_spec in self._io_spec.past_inputs:
            output_names.append(binding_spec.output_name)

        seen: set[str] = set()
        for output_name in output_names:
            if output_name in seen:
                continue
            seen.add(output_name)
            if output_name not in self._output_info:
                raise ModelValidationError(
                    f"Configured output '{output_name}' does not exist."
                )
            binding.bind_output(
                output_name,
                "cuda",
                self._device_id,
            )

        self._session.run_with_iobinding(binding)
        results = binding.get_outputs()

        return {
            name: value
            for name, value in zip(seen, results, strict=False)
        }

    def _extract_logits(self, outputs: Mapping[str, ort.OrtValue]) -> np.ndarray:
        logits_value = outputs.get(self._io_spec.logits_output)
        if logits_value is None:
            raise ModelValidationError(
                f"Logits output '{self._io_spec.logits_output}' was not returned."
            )

        return logits_value.numpy()

    def _extract_state(
        self,
        outputs: Mapping[str, ort.OrtValue],
    ) -> dict[str, ort.OrtValue]:
        result: dict[str, ort.OrtValue] = {}

        for binding in self._io_spec.past_inputs:
            output = outputs.get(binding.output_name)
            if output is None:
                raise ModelValidationError(
                    f"State output '{binding.output_name}' was not returned."
                )
            result[binding.state_name] = output

        return result

    def _to_cuda_value(self, value: np.ndarray) -> ort.OrtValue:
        array = np.ascontiguousarray(value)
        return ort.OrtValue.ortvalue_from_numpy(
            array,
            "cuda",
            self._device_id,
        )

    @staticmethod
    def _numpy_dtype(ort_type: str) -> np.dtype:
        mapping = {
            "tensor(float)": np.dtype(np.float32),
            "tensor(float16)": np.dtype(np.float16),
            "tensor(double)": np.dtype(np.float64),
            "tensor(int64)": np.dtype(np.int64),
            "tensor(int32)": np.dtype(np.int32),
            "tensor(int16)": np.dtype(np.int16),
            "tensor(int8)": np.dtype(np.int8),
            "tensor(uint8)": np.dtype(np.uint8),
            "tensor(bool)": np.dtype(np.bool_),
        }

        try:
            return mapping[ort_type]
        except KeyError as exc:
            raise ModelValidationError(
                f"Unsupported ONNX tensor type: {ort_type}"
            ) from exc

    def _validate_ids(self, input_ids: np.ndarray) -> None:
        if input_ids.ndim != 2:
            raise ValueError(
                f"input_ids must have shape [batch, sequence], got {input_ids.shape}"
            )

        if input_ids.shape[0] != 1:
            raise ValueError("This core currently manages one sequence per context.")

    def _validate_spec(self) -> None:
        required = {
            self._io_spec.input_ids,
            self._io_spec.logits_output,
        }
        missing_inputs = [
            name for name in (self._io_spec.input_ids, self._io_spec.attention_mask,
                              self._io_spec.position_ids, self._io_spec.cache_position)
            if name and name not in self._input_info
        ]
        missing_outputs = [
            binding.output_name
            for binding in self._io_spec.past_inputs
            if binding.output_name not in self._output_info
        ]

        if missing_inputs:
            raise ModelValidationError(
                f"Configured inputs do not exist: {missing_inputs}"
            )

        if missing_outputs:
            raise ModelValidationError(
                f"Configured state outputs do not exist: {missing_outputs}"
            )

        if not required.issubset(self._input_info | self._output_info):
            raise ModelValidationError("Invalid model I/O specification.")
