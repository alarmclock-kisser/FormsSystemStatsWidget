from __future__ import annotations

import re

import onnx

from .io_spec import DualStageIoSpec, StageIoSpec, StateBinding
from ..errors import ModelValidationError


class OnnxGraphInspector:
    """
    Statically inspects ONNX graphs (via onnx.load) to build the I/O contract.

    No InferenceSession, no CUDA, no inference.

    Supports dual-stage models where:
      - Stage 0 has input_ids but no logits
      - Stage 1 has boundary inputs + logits but no input_ids
      - State is split into KV, Conv, and Recurrent categories
    """

    _KV_INPUT_RE = re.compile(r"^past_key_values\.(\d+)\.(key|value)$")
    _KV_OUTPUT_RE = re.compile(r"^present\.(\d+)\.(key|value)$")
    _CONV_INPUT_RE = re.compile(r"^past\.(\d+)\.conv$")
    _CONV_OUTPUT_RE = re.compile(r"^present\.(\d+)\.conv$")
    _RECURRENT_INPUT_RE = re.compile(r"^past\.(\d+)\.recurrent$")
    _RECURRENT_OUTPUT_RE = re.compile(r"^present\.(\d+)\.recurrent$")

    def infer_single(self, path: str) -> StageIoSpec:
        """Statically inspect a single ONNX file."""
        return self._infer_stage(onnx.load(path, load_external_data=False))

    def infer_dual_stage(
        self,
        stage0_path: str,
        stage1_path: str,
    ) -> DualStageIoSpec:
        """Statically inspect both stage ONNX files and build the dual-stage I/O spec."""
        stage0_model = onnx.load(stage0_path, load_external_data=False)
        stage1_model = onnx.load(stage1_path, load_external_data=False)

        stage0_output_names = {o.name for o in stage0_model.graph.output}
        stage1_input_names = {i.name for i in stage1_model.graph.input}

        # Boundary: Stage 0 outputs that are also Stage 1 inputs
        boundary = sorted(stage0_output_names & stage1_input_names)

        stage0 = self._infer_stage(
            stage0_model,
            boundary_outputs=tuple(boundary),
        )
        stage1 = self._infer_stage(
            stage1_model,
            boundary_inputs=tuple(boundary),
        )

        return DualStageIoSpec(stage0=stage0, stage1=stage1)

    def _infer_stage(
        self,
        model: onnx.ModelProto,
        boundary_outputs: tuple[str, ...] = (),
        boundary_inputs: tuple[str, ...] = (),
    ) -> StageIoSpec:
        input_names = [i.name for i in model.graph.input]
        output_names = [o.name for o in model.graph.output]

        # Primary inputs
        input_ids = self._choose(input_names, ("input_ids", "inputIds"))
        attention_mask = self._choose(input_names, ("attention_mask", "attentionMask"))
        position_ids = self._choose(input_names, ("position_ids", "positionIds"))
        cache_position = self._choose(input_names, ("cache_position", "cachePosition"))

        # Primary outputs
        logits = self._choose(output_names, ("logits", "lm_logits", "last_logits"))
        if logits is None:
            logits = self._find_by_keywords(output_names, ("logit",))
        hidden_states = self._choose(output_names, ("hidden_states", "hiddenStates"))

        # State bindings — 3 categories, regex-based (no positional zip)
        kv_bindings = self._infer_kv_bindings(input_names, output_names)
        conv_bindings = self._infer_conv_bindings(input_names, output_names)
        recurrent_bindings = self._infer_recurrent_bindings(input_names, output_names)

        recognized_inputs = {
            binding.input_name
            for binding in (*kv_bindings, *conv_bindings, *recurrent_bindings)
        }
        recognized_outputs = {
            binding.output_name
            for binding in (*kv_bindings, *conv_bindings, *recurrent_bindings)
        }
        state_inputs = {
            name for name in input_names
            if name.startswith("past_key_values.") or name.startswith("past.")
        }
        state_outputs = {
            name for name in output_names
            if name.startswith("present.")
        }
        unmatched_inputs = state_inputs - recognized_inputs
        unmatched_outputs = state_outputs - recognized_outputs
        if unmatched_inputs or unmatched_outputs:
            raise ModelValidationError(
                f"Unpaired state tensors: inputs={sorted(unmatched_inputs)}, "
                f"outputs={sorted(unmatched_outputs)}"
            )

        return StageIoSpec(
            input_ids=input_ids,
            attention_mask=attention_mask,
            position_ids=position_ids,
            cache_position=cache_position,
            logits_output=logits,
            hidden_states_output=hidden_states,
            boundary_outputs=boundary_outputs,
            boundary_inputs=boundary_inputs,
            kv_bindings=kv_bindings,
            conv_bindings=conv_bindings,
            recurrent_bindings=recurrent_bindings,
        )

    def _infer_kv_bindings(
        self,
        input_names: list[str],
        output_names: list[str],
    ) -> tuple[StateBinding, ...]:
        outputs_by_key = {
            (match.group(1), match.group(2)): name
            for name in output_names
            if (match := self._KV_OUTPUT_RE.match(name))
        }
        bindings: list[StateBinding] = []
        for name in input_names:
            m = self._KV_INPUT_RE.match(name)
            if m:
                output_name = outputs_by_key.get((m.group(1), m.group(2)))
                if output_name is not None:
                    bindings.append(StateBinding(
                        input_name=name,
                        output_name=output_name,
                        state_name=name,
                    ))
        return tuple(bindings)

    def _infer_conv_bindings(
        self,
        input_names: list[str],
        output_names: list[str],
    ) -> tuple[StateBinding, ...]:
        outputs_by_layer = {
            match.group(1): name
            for name in output_names
            if (match := self._CONV_OUTPUT_RE.match(name))
        }
        bindings: list[StateBinding] = []
        for name in input_names:
            m = self._CONV_INPUT_RE.match(name)
            if m:
                output_name = outputs_by_layer.get(m.group(1))
                if output_name is not None:
                    bindings.append(StateBinding(
                        input_name=name,
                        output_name=output_name,
                        state_name=name,
                    ))
        return tuple(bindings)

    def _infer_recurrent_bindings(
        self,
        input_names: list[str],
        output_names: list[str],
    ) -> tuple[StateBinding, ...]:
        outputs_by_layer = {
            match.group(1): name
            for name in output_names
            if (match := self._RECURRENT_OUTPUT_RE.match(name))
        }
        bindings: list[StateBinding] = []
        for name in input_names:
            m = self._RECURRENT_INPUT_RE.match(name)
            if m:
                output_name = outputs_by_layer.get(m.group(1))
                if output_name is not None:
                    bindings.append(StateBinding(
                        input_name=name,
                        output_name=output_name,
                        state_name=name,
                    ))
        return tuple(bindings)

    @staticmethod
    def _choose(names: list[str], candidates: tuple[str, ...]) -> str | None:
        lowered = {name.lower(): name for name in names}
        for candidate in candidates:
            if candidate.lower() in lowered:
                return lowered[candidate.lower()]
        return None

    @staticmethod
    def _find_by_keywords(names: list[str], keywords: tuple[str, ...]) -> str | None:
        for name in names:
            lowered = name.lower()
            if any(kw in lowered for kw in keywords):
                return name
        return None
