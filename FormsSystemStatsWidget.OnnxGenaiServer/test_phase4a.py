"""Phase 4a verification — static I/O contract test (no runtime, no CUDA)."""
import sys
import types

# Mock the package structure to avoid triggering adapter.py import chain
pkg = types.ModuleType("onnx_engine")
pkg.__path__ = [
    r"c:\Users\op\source\repos\alarmclock-kisser\FormsSystemStatsWidget\FormsSystemStatsWidget.OnnxGenaiServer\PyDualOnnxInferenceEngine\onnx_engine"
]
sys.modules["onnx_engine"] = pkg

sub = types.ModuleType("onnx_engine.model")
sub.__path__ = [
    r"c:\Users\op\source\repos\alarmclock-kisser\FormsSystemStatsWidget\FormsSystemStatsWidget.OnnxGenaiServer\PyDualOnnxInferenceEngine\onnx_engine\model"
]
sys.modules["onnx_engine.model"] = sub

from onnx_engine.model.io_spec import StageIoSpec, DualStageIoSpec, StateBinding
from onnx_engine.model.inspector import OnnxGraphInspector

print("=== Phase 4a Verification ===")
print("io_spec imports: OK")
print("inspector imports: OK")

insp = OnnxGraphInspector()
spec = insp.infer_dual_stage(
    r"D:\Models\ONNX\Qwen3.8-27B-onnx-int4\partitioned\model.stage0.onnx",
    r"D:\Models\ONNX\Qwen3.8-27B-onnx-int4\partitioned\model.stage1.onnx",
)

s0 = spec.stage0
s1 = spec.stage1

print()
print("Stage 0:")
print(f"  input_ids:        {s0.input_ids}")
print(f"  attention_mask:   {s0.attention_mask}")
print(f"  position_ids:     {s0.position_ids}")
print(f"  logits_output:    {s0.logits_output}")
print(f"  hidden_states:    {s0.hidden_states_output}")
print(f"  boundary_outputs: {len(s0.boundary_outputs)}")
print(f"  kv_bindings:      {len(s0.kv_bindings)}")
print(f"  conv_bindings:    {len(s0.conv_bindings)}")
print(f"  recurrent:        {len(s0.recurrent_bindings)}")
print(f"  is_stage0:        {s0.is_stage0}")

print()
print("Stage 1:")
print(f"  input_ids:        {s1.input_ids}")
print(f"  attention_mask:   {s1.attention_mask}")
print(f"  position_ids:     {s1.position_ids}")
print(f"  logits_output:    {s1.logits_output}")
print(f"  hidden_states:    {s1.hidden_states_output}")
print(f"  boundary_inputs:  {len(s1.boundary_inputs)}")
print(f"  kv_bindings:      {len(s1.kv_bindings)}")
print(f"  conv_bindings:    {len(s1.conv_bindings)}")
print(f"  recurrent:        {len(s1.recurrent_bindings)}")
print(f"  is_stage1:        {s1.is_stage1}")

# Round-trip test
d = spec.to_dict()
spec2 = DualStageIoSpec.from_dict(d)
print()
print("Round-trip (to_dict → from_dict):")
print(f"  stage0 kv:        {len(spec2.stage0.kv_bindings)}")
print(f"  stage0 conv:      {len(spec2.stage0.conv_bindings)}")
print(f"  stage0 rec:       {len(spec2.stage0.recurrent_bindings)}")
print(f"  stage1 kv:        {len(spec2.stage1.kv_bindings)}")
print(f"  stage1 conv:      {len(spec2.stage1.conv_bindings)}")
print(f"  stage1 rec:       {len(spec2.stage1.recurrent_bindings)}")
print(f"  stage1 logits:    {spec2.stage1.logits_output}")
print(f"  stage1 hidden:    {spec2.stage1.hidden_states_output}")

# Ground Truth assertions
assert s0.input_ids == "input_ids", f"Stage0 input_ids: {s0.input_ids}"
assert s0.logits_output is None, f"Stage0 logits: {s0.logits_output}"
assert s0.hidden_states_output is None, f"Stage0 hidden: {s0.hidden_states_output}"
assert len(s0.boundary_outputs) == 2, f"Stage0 boundary: {len(s0.boundary_outputs)}"
assert len(s0.kv_bindings) == 20, f"Stage0 kv: {len(s0.kv_bindings)}"
assert len(s0.conv_bindings) == 32, f"Stage0 conv: {len(s0.conv_bindings)}"
assert len(s0.recurrent_bindings) == 32, f"Stage0 rec: {len(s0.recurrent_bindings)}"
assert s0.is_stage0, "Stage0 is_stage0"

assert s1.input_ids is None, f"Stage1 input_ids: {s1.input_ids}"
assert s1.logits_output == "logits", f"Stage1 logits: {s1.logits_output}"
assert s1.hidden_states_output == "hidden_states", f"Stage1 hidden: {s1.hidden_states_output}"
assert len(s1.boundary_inputs) == 2, f"Stage1 boundary: {len(s1.boundary_inputs)}"
assert len(s1.kv_bindings) == 12, f"Stage1 kv: {len(s1.kv_bindings)}"
assert len(s1.conv_bindings) == 16, f"Stage1 conv: {len(s1.conv_bindings)}"
assert len(s1.recurrent_bindings) == 16, f"Stage1 rec: {len(s1.recurrent_bindings)}"
assert s1.is_stage1, "Stage1 is_stage1"

# Round-trip assertions
assert len(spec2.stage0.kv_bindings) == 20
assert len(spec2.stage0.conv_bindings) == 32
assert len(spec2.stage0.recurrent_bindings) == 32
assert len(spec2.stage1.kv_bindings) == 12
assert len(spec2.stage1.conv_bindings) == 16
assert len(spec2.stage1.recurrent_bindings) == 16
assert spec2.stage1.logits_output == "logits"
assert spec2.stage1.hidden_states_output == "hidden_states"

print()
print("=== ALL ASSERTIONS PASSED ===")
