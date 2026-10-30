# ONNX Inference Engine Core

A pure Python inference core for local causal-language-model packages using:

- Hugging Face tokenizer + chat templates
- local JSON model/tokenizer/generation configuration loading
- ONNX Runtime `CUDAExecutionProvider`
- optional CUDA I/O binding
- explicit model load/unload lifecycle
- conversation/context management
- state/KV snapshots
- prefill + incremental decode
- sampling
- stop conditions
- synchronous and asyncio-compatible token streaming

## Deliberately NOT included

This package does **not** contain:

- GGUF loading/inference
- llama.cpp
- llama-server
- HTTP/OpenAI API
- web server
- authentication
- application orchestration
- .NET integration
- model-specific Qwen code

Those belong outside this core.

## Runtime requirements

Install an ONNX Runtime GPU build whose CUDA/cuDNN versions match the local NVIDIA runtime.
Current ONNX Runtime documentation provides `onnxruntime.preload_dlls()` for loading CUDA/cuDNN/MSVC DLLs before session creation. See:
https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html

## Model package

The loader expects a local directory containing at minimum:

    model.onnx

and optionally:

    tokenizer.json
    tokenizer_config.json
    special_tokens_map.json
    chat_template.jinja
    generation_config.json
    config.json
    genai_config.json
    preprocessor_config.json
    processor_config.json
    model_io.json

The tokenizer is loaded through `transformers.AutoTokenizer.from_pretrained(..., local_files_only=True)`.
Modern standalone `chat_template.jinja` files are supported by Transformers and take precedence over embedded templates.

## ONNX graph contract

A generic ONNX graph cannot be assumed to have one universal I/O layout.

For a real decoder model, the engine expects either:

1. a common causal-LM naming convention that can be inferred, or
2. an explicit `model_io.json`.

The explicit spec is recommended for production.

See `examples/model_io.json`.

### Example model_io.json

    {
      "input_ids": "input_ids",
      "attention_mask": "attention_mask",
      "position_ids": "position_ids",
      "cache_position": "cache_position",
      "logits_output": "logits",
      "past_sequence_axis": 2,
      "past_inputs": [
        {
          "input": "past_key_values.0.key",
          "output": "present.0.key",
          "state_name": "layer0.key"
        }
      ]
    }

`past_inputs` is deliberately explicit because ONNX exporters use different naming and ordering schemes.

The adapter also attempts conservative name-based inference when `model_io.json` is absent.

## Large-prefill warning

If the ONNX graph outputs a full `[batch, sequence, vocab]` logits tensor, a generic runtime may need to materialize/copy more logits than a production decoder actually needs.
For large-context inference, prefer an ONNX export whose logits output is already reduced to the last position, or add a small post-processing graph that exposes last-token logits.

The engine does not silently invent such a graph transformation.

## CUDA execution

The low-level runtime supports:

- normal `session.run()`
- CUDA OrtValues
- I/O Binding
- CUDA-resident state between decode iterations
- explicit `device_id`
- optional CUDA graph provider flag
- configurable session graph optimization

ONNX Runtime documents I/O Binding specifically for keeping inputs/outputs on device and avoiding unnecessary device/host copies.

## Context snapshots

In-memory context state may remain as CUDA OrtValues.

Saving a snapshot intentionally copies device state to CPU NumPy arrays and serializes it. This is an explicit persistence operation, not something done automatically on every token.

## Sampling

Implemented CPU-side:

- temperature
- top-k
- top-p
- min-p
- repetition penalty
- frequency penalty
- presence penalty
- deterministic seed
- EOS/stop token IDs
- stop strings

Sampling is intentionally separate from the ONNX backend.

## Async

ONNX Runtime's Python execution call is synchronous. The async wrapper delegates blocking inference work to a worker thread so the asyncio event loop remains responsive.
