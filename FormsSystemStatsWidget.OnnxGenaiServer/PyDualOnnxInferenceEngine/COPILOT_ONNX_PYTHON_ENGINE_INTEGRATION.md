# ONNX Python Inference Engine — C# Integration & Wiring Specification

**Status:** Architecture / implementation contract  
**Target:** .NET 10 host application + external Python ONNX Runtime CUDA engine  
**Purpose:** Give Copilot an exact integration boundary so it does not invent a second inference stack inside the C# application.

---

# 0. NON-NEGOTIABLE SCOPE

## The Python side owns the complete model-inference runtime

Python must own:

- model-package loading
- local JSON configuration loading
- tokenizer loading
- chat-template loading/execution
- special-token handling
- message/conversation normalization
- prompt construction
- text -> token IDs
- token IDs -> text
- ONNX Runtime session creation
- CUDAExecutionProvider configuration
- ONNX graph inspection / explicit model I/O contract
- CUDA I/O Binding
- model prefill
- incremental decode
- model state / KV / recurrent state handling
- generation loop
- sampling
- EOS and stop conditions
- generation limits
- streaming generated tokens
- context lifecycle
- context state ownership
- context snapshot save/restore
- model load/unload
- runtime metrics
- model/runtime validation
- model-specific ONNX adapter logic

## The C# application owns:

- application UI
- application business logic
- HTTP server
- OpenAI-compatible API surface
- authentication / authorization
- HTTP request validation at the application boundary
- request routing
- application logging
- external tool registry if the application owns it
- Python process hosting / supervision
- IPC transport to the Python engine
- translating Python engine events into existing C# abstractions
- exposing the final application API

## Explicitly forbidden

Do NOT move these back into C#:

- tokenizer implementation
- chat-template rendering
- generation loop
- sampling math
- KV cache implementation
- ONNX Runtime calls
- ONNX graph I/O naming logic
- Qwen/model-specific prompt rules
- model-specific state rules

Do NOT create a Python web server or OpenAI API server.

Do NOT use llama.cpp / llama-server in the ONNX engine.

Do NOT execute GGUF through ONNX Runtime.

Do NOT add Python files below the C# application's `/src` tree unless explicitly requested.

Recommended repository layout:

    /src/                         # existing .NET application
    /python/
        /onnx_inference_engine/   # Python runtime
    /models/
        /<model-package>/

If the repository already has a designated Python/engine directory, use that instead.

---

# 1. ARCHITECTURE

High-level flow:

    .NET 10 application
        |
        | local IPC
        v
    Python Inference Engine
        |
        +-- ModelPackageLoader
        |
        +-- TokenizerService
        |
        +-- ChatTemplate
        |
        +-- ContextStore
        |
        +-- GenerationEngine
        |
        +-- Sampler
        |
        +-- StopController
        |
        +-- CausalOnnxAdapter
        |
        +-- ONNX Runtime
        |
        +-- CUDAExecutionProvider
        |
        v
      NVIDIA GPU

The Python runtime is a long-lived process.

The C# process must NOT restart Python for every request.

The model and ONNX Runtime session must be loaded once and reused until explicit unload/shutdown.

---

# 2. IMPORTANT: ONNX IS NOT A UNIVERSAL LLM INTERFACE

An arbitrary `model.onnx` does not imply one universal input/output contract.

Possible inputs can differ:

    input_ids
    attention_mask
    position_ids
    cache_position
    past_key_values.*
    state.*
    recurrent_state.*
    ...

Possible outputs can differ:

    logits
    last_logits
    present.*
    state.*
    recurrent_state.*
    ...

Therefore:

1. inspect the actual ONNX graph;
2. prefer an explicit `model_io.json`;
3. only use automatic I/O inference as a conservative fallback;
4. never silently guess incompatible KV/state mappings.

The existing Python skeleton contains:

    model/package.py
    model/io_spec.py
    model/inspector.py
    model/adapter.py

Use those responsibilities rather than putting graph-specific logic into `engine.py`.

---

# 3. MODEL PACKAGE

A model package is a directory, not just a `.onnx` file.

Expected/optional files:

    model.onnx

    config.json
    genai_config.json
    generation_config.json

    tokenizer.json
    tokenizer_config.json
    special_tokens_map.json

    chat_template.jinja
    additional_chat_templates/
        tool_use.jinja
        ...

    preprocessor_config.json
    processor_config.json

    model_io.json

Potential future files:

    added_tokens.json
    tokenizer.model
    merges.txt
    vocab.json
    processor-specific files
    external ONNX data files

The loader must:

- resolve one package root;
- validate that the model exists;
- load supported JSON dictionaries;
- load standalone Jinja chat templates;
- load optional `additional_chat_templates`;
- expose raw normalized configuration;
- never perform inference during package parsing.

Hugging Face currently recommends standalone `chat_template.jinja` and `additional_chat_templates/*.jinja`. Standalone templates override an embedded template when both exist.

Reference:
https://huggingface.co/docs/transformers/chat_templating_writing

---

# 4. TOKENIZER OWNERSHIP

Tokenizer stays entirely in Python.

Use the model-local tokenizer:

    AutoTokenizer.from_pretrained(
        package_root,
        local_files_only=True,
        use_fast=True
    )

`trust_remote_code` must be an explicit runtime option and default to `false`.

Never download model files during normal inference.

The tokenizer service owns:

- encode text
- decode token sequence
- decode one token
- BOS/EOS IDs
- special tokens
- chat template application
- tools argument
- documents argument
- additional template kwargs

Do NOT duplicate tokenizer IDs or special-token constants in C#.

---

# 5. CHAT TEMPLATE

The C# side sends structured messages.

Example:

    {
      "role": "system",
      "content": "You are a coding assistant."
    }

    {
      "role": "user",
      "content": "Explain this method."
    }

Python performs:

    messages
        -> chat_template
        -> rendered prompt
        -> tokenizer
        -> input_ids

Default generation behavior:

    add_generation_prompt = true

The caller must be able to override:

    add_generation_prompt

The caller must also be able to pass:

    tools
    documents
    arbitrary template kwargs

Do not hard-code Qwen-specific Jinja text into C#.

Transformers supports arbitrary keyword arguments passed into chat templates; common conventions include `tools` and `documents`.

Reference:
https://huggingface.co/docs/transformers/main/chat_template_tools_and_documents

Important special-token rule:

If the pipeline does:

    apply_chat_template(..., tokenize=False)

then subsequent tokenization must avoid duplicating special tokens:

    add_special_tokens=False

If using:

    apply_chat_template(..., tokenize=True)

then do not add another special-token layer afterward.

Reference:
https://huggingface.co/docs/transformers/main/chat_templating

---

# 6. CONVERSATION / CONTEXT OWNERSHIP

Python owns conversation state.

C# should not maintain a second authoritative context representation.

The Python runtime needs two conceptual layers:

## Conversation

Human-readable logical history:

    messages[]

## ContextState

Runtime state:

    token_ids
    model/KV/recurrent state
    generated token IDs
    current position
    model-specific state

The C# side should receive an opaque context ID, for example:

    context_id = "ctx-7f2c..."

The C# side must never need to understand the structure of the model state.

Recommended operations:

    CreateContext
    GetContextMetadata
    AppendMessage
    Generate
    CancelGeneration
    SaveContext
    RestoreContext
    ResetContext
    DestroyContext

---

# 7. CONTEXT REUSE

A new request must reuse an existing context whenever the prefix is compatible.

Conceptually:

    previous context
        |
        +-- common prefix ---------------------+
        |                                      |
        |                                 retained state
        |
        +-- new suffix
              |
              +-- tokenize
              +-- prefill suffix / required state reconstruction

The engine must not automatically rebuild the entire conversation from token zero when the existing runtime state can safely be reused.

However, the engine must never assume that token-ID prefix compatibility alone is sufficient for a recurrent/stateful export.

The adapter owns the rules needed to determine whether runtime state is valid.

---

# 8. CONTEXT SNAPSHOTS

Snapshots are optional persistence, not per-token persistence.

Required API concept:

    save_context(context_id, path)
    load_context(path) -> context_id

A snapshot can contain:

    conversation metadata
    token IDs
    generated token IDs
    model-state tensors
    adapter/version metadata
    model identity
    tokenizer/config identity

Device state may be resident on CUDA during normal operation.

When explicitly saving a context:

    CUDA state
        -> CPU NumPy representation
        -> serialized file

Do not serialize CUDA state every generation step.

A snapshot must contain enough metadata to reject incompatible restores:

- model package identity
- ONNX graph identity/version if available
- tokenizer identity
- adapter/model I/O version
- state schema version

Do not silently restore a state into a different incompatible model.

---

# 9. GENERATION PIPELINE

The generation loop is Python-owned:

    request
      |
      v
    Conversation
      |
      v
    Chat Template
      |
      v
    Tokenizer
      |
      v
    input_ids
      |
      v
    PREFILL
      |
      +-- initial logits
      +-- runtime state
      |
      v
    SAMPLE
      |
      v
    next token
      |
      v
    DECODE
      |
      +-- next logits
      +-- updated runtime state
      |
      v
    SAMPLE
      |
      v
    ...
      |
      +-- EOS/stop/limit
      v
    finished

The C# side receives already-decoded token text/metadata.

C# must not make one IPC request per generated token if a streaming channel can carry token events from one generation request.

---

# 10. PREFILL VS DECODE

These must remain separate internal phases.

## Prefill

Input:

    [batch, sequence]

Typical purpose:

- process the prompt
- initialize/update KV/recurrent state
- produce logits for the next generated token

## Decode

Input:

    [batch=1, sequence=1]

plus existing runtime state.

Typical purpose:

- process one next token
- update state
- produce next-token logits

Do not pretend that a generic ONNX export necessarily uses this exact shape; inspect the graph.

---

# 11. LOGITS CONTRACT

Generation expects one vocabulary distribution for the next token.

The adapter must normalize model output to:

    [vocab]

internally.

Accepted source forms can include:

    [vocab]
    [1, vocab]
    [batch, sequence, vocab]

If a full sequence logits tensor is returned:

    logits[0, -1, :]

is the conceptual next-token distribution for a single sequence.

For performance-sensitive long-context models, prefer an ONNX graph/export where the graph already emits only the last-token logits instead of materializing the complete sequence logits tensor.

Do not silently add expensive host-side slicing if an appropriate graph/export exists.

---

# 12. SAMPLING

Sampling is Python-owned.

Expose these controls:

    temperature
    top_k
    top_p
    min_p
    repetition_penalty
    frequency_penalty
    presence_penalty
    seed

Recommended normalized DTO:

    SamplingOptions
        Temperature
        TopK
        TopP
        MinP
        RepetitionPenalty
        FrequencyPenalty
        PresencePenalty
        Seed

Semantics:

## temperature

    <= 0
        greedy/argmax mode

    > 0
        scale logits by temperature

## top_k

Keep only the K highest-scoring candidates.

    <= 0
        disabled

## top_p

Nucleus sampling.

    1.0
        disabled

## min_p

Keep candidates whose probability satisfies:

    p(token) >= min_p * p(max)

    <= 0
        disabled

## repetition_penalty

Apply only to tokens present in history.

Do not combine model-specific repetition logic from a random external implementation without verifying semantics.

## frequency_penalty

Penalize according to token occurrence count.

## presence_penalty

Apply a fixed penalty once a token has occurred.

## seed

A deterministic local random source when non-null.

Do not use global application RNG state.

---

# 13. STOP CONDITIONS

Expose:

    max_new_tokens
    eos_token_ids
    stop_token_ids
    stop_strings

Recommended DTO:

    StopOptions
        MaxNewTokens
        EosTokenIds
        StopTokenIds
        StopStrings

Stop checks happen after sampling and before the next decode call.

Do not depend solely on stop strings if a model has a proper EOS token.

Stop strings require careful handling of decoded text boundaries; do not assume that one token equals one semantic character/string boundary.

---

# 14. STREAMING CONTRACT

Each generated event should contain at minimum:

    request_id
    context_id
    token_id
    text
    generated_tokens
    context_length
    finished

Optional:

    finish_reason
    prompt_tokens
    prompt_elapsed_seconds
    decode_elapsed_seconds
    tokens_per_second
    seed
    model_id

Recommended event types:

    ready
    generation_started
    token
    generation_finished
    generation_cancelled
    error
    context_saved
    context_restored

The C# side translates these events into its existing streaming/OpenAI surface.

---

# 15. CANCELLATION

C# must expose `CancellationToken`.

Flow:

    C# CancellationToken
        |
        v
    IPC cancel message
        |
        v
    Python generation loop

Python must check cancellation:

- before prefill
- between decode steps
- before sampling
- before yielding the next token

A single ONNX Runtime call is synchronous through the normal Python API.

Therefore:

- cancellation between ORT calls can be immediate;
- cancellation during one long ORT execution may only take effect after that call returns unless a more advanced ORT cancellation mechanism is explicitly implemented;
- never claim hard realtime interruption if the underlying runtime does not provide it.

---

# 16. ONNX RUNTIME SESSION LIFECYCLE

Exactly one long-lived `InferenceSession` per loaded model/runtime configuration unless a deliberate multi-session architecture is required.

Lifecycle:

    unload
      |
      v
    ModelPackageLoader
      |
      v
    CUDA DLL preload
      |
      v
    SessionOptions
      |
      v
    InferenceSession
      |
      v
    CUDA provider validation
      |
      v
    Model I/O validation
      |
      v
    Tokenizer
      |
      v
    Adapter
      |
      v
    GenerationEngine
      |
      v
    READY

Unloading must release references in dependency order:

    GenerationEngine
    Adapter
    Tokenizer
    Session
    ModelPackage

Then allow Python GC to release remaining objects.

Do not reload the model per request.

---

# 17. EXECUTION PROVIDERS

Provider order matters.

Preferred:

    CUDAExecutionProvider

Optional fallback:

    CPUExecutionProvider

For this engine, default:

    allow_cpu_fallback = false

Reason:

The application is intended to know when the CUDA backend is unavailable instead of silently moving part of the graph to CPU.

ONNX Runtime documents provider order as decreasing precedence. For example:

    ["CUDAExecutionProvider", "CPUExecutionProvider"]

means CUDA is preferred and unsupported nodes can be assigned to CPU.

Reference:
https://onnxruntime.ai/docs/api/python/api_summary

Important:

Even with CUDA as the first provider, individual unsupported graph nodes can still end up on another execution provider when fallback is enabled.

The engine therefore needs an explicit validation/reporting path for active providers and, where possible, graph placement/profile diagnostics.

---

# 18. CUDA EXECUTION PROVIDER KNOBS

Expose these as `CudaRuntimeOptions`.

## device_id

    int
    default = 0

Selects CUDA device.

Do not infer multi-GPU sharding from this.

ONNX Runtime CUDA EP does not magically tensor-parallelize an arbitrary ONNX model.

If multi-GPU ONNX inference is required later, that is a separate architecture involving explicit graph/model partitioning or multiple sessions.

## gpu_mem_limit

    integer bytes
    default = 0 / provider default

Do not set an artificial limit unless required.

## arena_extend_strategy

Common values:

    kNextPowerOfTwo
    kSameAsRequested

`kNextPowerOfTwo` grows the arena geometrically.

`kSameAsRequested` requests sizes closer to the actual allocation request.

Benchmark rather than guessing.

Reference:
https://onnxruntime.ai/docs/api/c/struct_ort_c_u_d_a_provider_options.html

## cudnn_conv_algo_search

Common values exposed by ORT include search strategies such as:

    EXHAUSTIVE

Only matters for graphs containing relevant cuDNN convolution operations.

Do not optimize this knob for a transformer graph if there are no relevant convolution nodes.

## do_copy_in_default_stream

Controls whether copies use the compute/default CUDA stream or separate copy streams.

Default should be:

    true

Only change after a measured benchmark or when required by a specific model/runtime synchronization strategy.

## use_tf32

Expose:

    true / false

TF32 can improve throughput for supported FP32 operations on NVIDIA hardware at reduced precision semantics.

Do not force it if deterministic numerical behavior or model-specific requirements prohibit it.

## enable_cuda_graph

Expose:

    false by default

Do not enable blindly.

CUDA graph capture/replay has structural requirements around graph shapes, memory/buffer lifetime, and execution behavior.

Use only after validating that the model has stable shapes and the graph is compatible.

Reference:
https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html

## extra_provider_options

Allow a version-specific dictionary, but:

- pass through only explicitly configured options;
- do not invent unknown provider options;
- log the final effective provider options.

This protects the engine against future ONNX Runtime provider additions without hard-coding unstable knobs.

---

# 19. CUDA DLL PRELOADING

Support:

    onnxruntime.preload_dlls()

before session creation.

This is especially useful on Windows where CUDA/cuDNN/MSVC runtime DLL discovery can otherwise be the source of environment-dependent startup failures.

Expose:

    preload_cuda_dlls = true

Allow an optional explicit DLL directory when the deployment requires it.

Reference:
https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html

---

# 20. SESSION OPTIONS

Expose these as `SessionRuntimeOptions`.

## graph_optimization_level

Supported ORT levels conceptually:

    ORT_DISABLE_ALL
    ORT_ENABLE_BASIC
    ORT_ENABLE_EXTENDED
    ORT_ENABLE_ALL

Default:

    ORT_ENABLE_ALL

Production recommendation:

    ORT_ENABLE_ALL

Only lower it when diagnosing a graph optimizer/export interaction.

## execution_mode

Common values:

    ORT_SEQUENTIAL
    ORT_PARALLEL

Default:

    ORT_SEQUENTIAL

For a single decoder graph, benchmark before using ORT_PARALLEL.

`inter_op_num_threads` only matters for parallel graph execution.

## intra_op_num_threads

    0
        ORT chooses

or explicit positive integer.

For mostly-GPU inference, do not blindly match this to the entire CPU thread count.

Benchmark.

## inter_op_num_threads

    0
        ORT chooses

Only relevant to inter-op parallel execution.

## enable_mem_pattern

Default:

    true

Memory pattern can help when repeated runs have stable allocation patterns.

For dynamic-shape decoder workloads, benchmark carefully.

## enable_cpu_mem_arena

Default:

    true

Do not disable merely because inference is on CUDA.

The CPU arena is still relevant to host allocations and CPU-side runtime operations.

Expose it because memory behavior may matter for constrained hosts.

## enable_profiling

Default:

    false

When enabled, keep enough lifecycle support to retrieve/close the ORT profile cleanly.

Profiling must be opt-in because it adds overhead and generates data.

## disable_prepacking

Default:

    false

Do not disable unless diagnosing an exporter/runtime interaction.

---

# 21. CUDA I/O BINDING

The engine must support two execution paths.

## Standard path

    session.run()

Use for:

- compatibility
- debugging
- simple models
- baseline correctness testing

Typical flow:

    NumPy host input
        -> ORT
        -> CUDA EP
        -> host output

## CUDA I/O Binding path

Use:

    session.io_binding()
    bind_ortvalue_input(...)
    bind_output(..., "cuda", device_id)
    session.run_with_iobinding(...)

Use for:

- high-throughput inference
- long decode loops
- avoiding repeated host/device copies
- CUDA-resident state
- CUDA-resident outputs

Reference:
https://onnxruntime.ai/docs/api/python/api_summary

The engine should prefer device-resident state for repeated decode when the actual graph supports it.

Do not copy KV/state:

    CUDA -> CPU -> CUDA

between every decode step.

---

# 22. ORTVALUE / DEVICE MEMORY OWNERSHIP

An `OrtValue` can hold CUDA-resident tensor data.

The engine should reuse long-lived OrtValues where shape/state semantics permit.

Do not create a fresh redundant host buffer for every generated token.

For a stateful decoder:

    state[n]
        -> decode
        -> state[n+1]

should remain device resident where possible.

Only explicit persistence/debug paths should call:

    OrtValue.numpy()

because that is a device -> host transfer.

---

# 23. MEMORY MANAGEMENT

The Python engine must distinguish:

## Model memory

ONNX model weights / graph-owned runtime state.

## CUDA execution memory

Provider arenas, kernels, temporary buffers, graph allocations.

## Runtime state

KV/recurrent state / cache state.

## Host memory

- tokenizer structures
- prompt strings
- serialized snapshots
- NumPy staging arrays
- profiling data
- Python object graphs

No memory category should be silently conflated with another.

Do not add "memory cleanup" calls on every token.

Do not call `gc.collect()` in the generation hot loop.

Explicitly releasing the engine/session is preferred over forcing GC constantly.

---

# 24. MODEL-SPECIFIC ADAPTER

`CausalOnnxAdapter` is the only place where ONNX graph-specific details belong.

Responsibilities:

- map normalized `input_ids` to graph input name
- build attention mask if required
- build position IDs if required
- build cache position if required
- bind past state
- initialize empty past/state
- bind outputs
- identify logits
- identify next/present state
- return normalized `ModelStepResult`

Do not put model graph names in `GenerationEngine`.

Do not put model graph names in C#.

Recommended explicit file:

    model_io.json

Example:

    {
      "input_ids": "input_ids",
      "attention_mask": "attention_mask",
      "position_ids": "position_ids",
      "cache_position": "cache_position",
      "logits_output": "logits",
      "past_sequence_axis": 2,
      "logits_sequence_axis": 1,
      "past_inputs": [
        {
          "input": "past_key_values.0.key",
          "output": "present.0.key",
          "state_name": "layer0.key"
        }
      ]
    }

This is an example schema, not a universal ONNX standard.

The real ONNX graph is authoritative.

---

# 25. CONTEXT LENGTH

Expose:

    max_context_tokens

The engine must calculate:

    current_context_tokens
    prompt_tokens
    generated_tokens
    remaining_context_capacity

Before generation, validate:

    prompt_tokens + max_new_tokens <= context_limit

or apply an explicitly configured truncation/compaction policy.

Do not silently discard oldest context.

If truncation is implemented, expose the policy:

    reject
    truncate_oldest_messages
    truncate_to_token_budget
    model-specific_compaction

Default should be:

    reject

until a deliberate compaction policy is implemented.

---

# 26. REQUEST OPTIONS

Recommended request DTO:

    GenerationRequest

Fields:

    request_id
    context_id
    messages
    sampling
    stopping
    add_generation_prompt
    tools
    documents
    template_kwargs
    warmup
    return_token_ids
    save_context_after_generation

Not every option needs to be used in the first implementation, but the contract should remain extensible.

---

# 27. ENGINE OPTIONS

Recommended top-level options:

    model_path / model_package_path

    trust_remote_code
    preload_cuda_dlls
    cuda
    session
    allow_cpu_fallback

    max_context_tokens

    default_sampling
    default_stopping

    enable_io_binding
    enable_cuda_graph

    enable_profiling

    snapshot_directory

    telemetry_enabled

Avoid exposing internal Python classes directly to C#.

---

# 28. C# IPC CONTRACT

The Python core is NOT an HTTP server.

Recommended first transport:

    stdin/stdout JSON Lines

Reason:

- one long-lived process
- no extra listening port
- easy process supervision
- easy local-only security model
- easy request/response correlation
- streaming events are natural
- C# owns the external HTTP/API surface

Never send large CUDA tensors through JSON.

IPC payloads contain:

- strings
- token IDs when explicitly requested
- options
- state handles
- small metadata
- status/errors

Model execution and KV/state remain in Python memory.

---

# 29. C# -> PYTHON COMMANDS

Recommended message envelope:

    {
      "id": "request-123",
      "type": "generate",
      "payload": { ... }
    }

Commands:

    ping
    load_model
    unload_model

    create_context
    reset_context
    destroy_context
    get_context

    generate
    cancel

    save_context
    restore_context

    get_metadata
    get_runtime_status

    shutdown

Every command returns either:

    result

or:

    error

Generation additionally streams:

    token

events.

---

# 30. PYTHON -> C# EVENTS

Example:

    {
      "id": "request-123",
      "type": "token",
      "payload": {
        "context_id": "ctx-7f2c",
        "token_id": 1234,
        "text": "Hello",
        "generated_tokens": 1,
        "context_length": 2048,
        "finished": false
      }
    }

Final:

    {
      "id": "request-123",
      "type": "generation_finished",
      "payload": {
        "context_id": "ctx-7f2c",
        "finish_reason": "eos",
        "generated_tokens": 142,
        "context_length": 2190
      }
    }

Error:

    {
      "id": "request-123",
      "type": "error",
      "payload": {
        "code": "CUDA_PROVIDER_UNAVAILABLE",
        "message": "...",
        "retryable": false
      }
    }

---

# 31. C# PROCESS HOST

C# owns:

- locating Python
- selecting Python executable/virtualenv
- starting the process
- redirecting stdin/stdout/stderr
- process lifetime
- crash detection
- restart policy
- graceful shutdown
- startup timeout
- ready handshake
- stderr capture

C# must not parse Python stdout as human logs if stdout is the JSONL protocol.

Recommended:

    stdout = protocol only
    stderr = diagnostics/logging

This prevents protocol corruption.

---

# 32. PYTHON ENVIRONMENT

The Python runtime must have an isolated environment.

C# must be able to configure:

    python.exe
    engine working directory
    model package path
    environment variables

Do not install Python packages into the user's global environment as part of every model load.

Preferred deployment:

    python/.venv/

or another explicitly configured environment.

---

# 33. MODEL LOAD HANDSHAKE

Required sequence:

C#:

    start process
        |
        v
    ping
        |
        v
    load_model(package_path, options)

Python:

    validate package
        |
        v
    preload CUDA DLLs
        |
        v
    create ORT session
        |
        v
    validate CUDA EP
        |
        v
    inspect/load model I/O
        |
        v
    load tokenizer
        |
        v
    build adapter
        |
        v
    build generation engine
        |
        v
    ready

Ready response should include:

    model_path
    model package identity
    ONNX path
    active providers
    CUDA device
    input metadata
    output metadata
    tokenizer summary
    EOS/BOS IDs
    effective runtime options

---

# 34. NO SILENT CPU FALLBACK

Production default:

    allow_cpu_fallback = false

If CPU fallback is enabled for diagnostics:

- expose it explicitly;
- report active providers;
- report whether fallback is allowed;
- collect diagnostics/profile data when possible.

The application must never believe "all CUDA" merely because CUDA is listed first.

---

# 35. LOGGING

Python should log:

- model load start/end
- provider selection
- CUDA device
- effective CUDA options
- session options
- model input/output names
- tokenizer load
- generation timing
- prefill token count
- decode token count
- tokens/sec
- context length
- save/restore lifecycle
- errors

Do not log every token by default.

C# receives structured generation metrics through the protocol.

---

# 36. METRICS

Per request:

    queue_time
    tokenize_time
    prefill_time
    decode_time
    sampling_time
    total_time

Counts:

    prompt_tokens
    generated_tokens
    context_tokens

Rates:

    prompt_tokens_per_second
    generated_tokens_per_second

Runtime:

    active_model
    active_device
    active_providers

Optional:

    CUDA memory usage
    ORT profile data
    graph execution statistics

Be careful with peak-memory numbers: a provider-reported arena allocation is not necessarily identical to Windows process working set or committed memory.

---

# 37. WARMUP

Warmup must be explicit.

Do not force warmup automatically unless configured.

Expose:

    warmup = false

When enabled:

    load session
        |
        v
    synthetic valid input
        |
        v
    one prefill/decode path
        |
        v
    discard generated result

Warmup exists to establish:

- graph initialization
- kernel selection
- memory allocation
- CUDA graph capture when deliberately enabled

It must not mutate a user's real context.

---

# 38. CUDA GRAPH RULE

CUDA graph capture is an optimization, not a correctness requirement.

Default:

    disabled

Only enable when:

- input/output shapes are sufficiently stable;
- buffer lifetimes are compatible;
- the graph itself is capture-compatible;
- repeated execution is expected.

Do not expose "CUDA graph enabled" as a promise of lower latency.

Reference:
https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html

---

# 39. THREADING RULES

The Python engine may have:

- one process
- one model session
- one generation loop per active context/request according to supported concurrency

Do not concurrently mutate one context state from multiple requests.

A single context requires serialized mutation:

    lock(context)
        prefill/decode/state update
    unlock(context)

Different independent contexts may be scheduled concurrently only after the engine explicitly supports that model/session behavior.

Do not introduce threads around every token just because Python has `asyncio`.

---

# 40. ASYNC RULE

Python ORT execution is synchronous through normal APIs.

The current async layer should:

    await asyncio.to_thread(blocking_call)

for application responsiveness.

This is a scheduling wrapper, not "GPU async inference" by itself.

The underlying CUDA EP may execute asynchronously internally, but the Python API boundary still needs correct synchronization semantics.

Never label a blocking ORT call "non-blocking" without proving it.

---

# 41. BACKPRESSURE

Streaming must tolerate a slow C# consumer.

Do not create an unbounded Python list of all output tokens.

Recommended:

    bounded asyncio.Queue

or equivalent bounded stream abstraction.

Policy:

    producer waits when consumer is too slow

rather than unboundedly accumulating token events in RAM.

---

# 42. CONTEXT STORE

Recommended Python structure:

    ContextStore
        context_id -> ContextState

Responsibilities:

- create
- get
- replace
- reset
- destroy
- list metadata
- serialize/restore

It should not own tokenizer or ORT session configuration.

A context references the engine/model identity under which it was created.

---

# 43. MODEL ADAPTER VS GENERATION ENGINE

Do not combine these.

## Adapter

Knows:

    graph input names
    graph output names
    past/present state
    tensor shapes
    device binding

## GenerationEngine

Knows:

    prefill
    decode
    sampling
    stopping
    streaming
    context bookkeeping

This keeps model-specific ONNX graph details out of generation policy.

---

# 44. MODEL PACKAGE VS TOKENIZER

Do not make the model-package loader perform tokenization.

Model package loader:

    files -> configuration objects

Tokenizer service:

    model package -> tokenizer runtime

Chat template:

    tokenizer + messages -> prompt/token IDs

This keeps disk parsing separate from runtime logic.

---

# 45. SAVE/RESTORE VERSIONING

Snapshot format must have a version header, e.g.:

    format_version = 1

and metadata:

    model_package_id
    model_io_schema_version
    tokenizer_identity
    engine_version

Restore must fail clearly when:

- model differs
- state schema differs
- tokenizer differs in a way that invalidates token IDs
- state tensors are missing
- tensor dtype/shape mismatches

Never silently "best effort" restore a partially incompatible KV/state.

---

# 46. ERRORS

Use machine-readable codes.

Recommended:

    MODEL_NOT_FOUND
    MODEL_PACKAGE_INVALID
    TOKENIZER_LOAD_FAILED
    CHAT_TEMPLATE_FAILED

    ORT_IMPORT_FAILED
    CUDA_PROVIDER_UNAVAILABLE
    CUDA_PROVIDER_NOT_ACTIVE
    ONNX_SESSION_CREATE_FAILED

    MODEL_IO_INVALID
    MODEL_INPUT_MISSING
    MODEL_OUTPUT_MISSING
    STATE_SCHEMA_INVALID

    CONTEXT_NOT_FOUND
    CONTEXT_INCOMPATIBLE
    CONTEXT_LIMIT_EXCEEDED

    GENERATION_CANCELLED
    GENERATION_FAILED
    SAMPLING_FAILED

    SNAPSHOT_SAVE_FAILED
    SNAPSHOT_RESTORE_FAILED

    ENGINE_NOT_READY
    ENGINE_ALREADY_LOADED
    ENGINE_SHUTTING_DOWN

C# maps these to its own application exceptions/status model.

Do not force C# to parse exception-message strings to determine error types.

---

# 47. SECURITY

Default to:

- local-only Python process
- no listening socket
- no remote code execution
- local model files only
- no network model downloads
- `trust_remote_code = false`

If `trust_remote_code` is ever enabled, it must be an explicit deployment setting.

The Python engine must not expose an HTTP endpoint.

---

# 48. PERFORMANCE PRINCIPLES

## Do

- keep one long-lived ORT session
- keep tokenizer loaded
- keep model state on GPU
- use CUDA I/O Binding for hot paths
- reuse CUDA OrtValues/state
- minimize host/device round trips
- tokenize once per logical prompt
- stream tokens incrementally
- use bounded queues
- reuse graph memory where ORT supports it
- profile before changing knobs

## Do not

- recreate InferenceSession per token
- recreate tokenizer per request
- copy CUDA state to NumPy between every token
- JSON-serialize logits
- move logits through C# for sampling
- use `gc.collect()` in the hot loop
- spawn one Python process per request
- create a web server around the engine
- duplicate chat-template logic in C#
- guess ONNX KV names
- silently fallback to CPU

---

# 49. CURRENT ENGINE PACKAGE MAPPING

Use the existing Python package responsibilities approximately as follows:

    onnx_engine/engine.py
        High-level InferenceEngine facade

    onnx_engine/model/package.py
        Model package loading

    onnx_engine/model/io_spec.py
        Explicit graph I/O schema

    onnx_engine/model/inspector.py
        Conservative graph inspection

    onnx_engine/model/adapter.py
        Causal ONNX prefill/decode

    onnx_engine/tokenization/tokenizer.py
        tokenizer + chat template

    onnx_engine/context/conversation.py
        logical conversation

    onnx_engine/context/state.py
        runtime context state

    onnx_engine/context/snapshot.py
        explicit save/restore

    onnx_engine/generation/engine.py
        generation loop

    onnx_engine/generation/sampling.py
        sampling

    onnx_engine/generation/stopping.py
        stop conditions

    onnx_engine/runtime/cuda.py
        CUDA provider configuration

    onnx_engine/runtime/session.py
        ORT session lifecycle

The C# project may wrap this with its own adapter/client, but must not reimplement these responsibilities.

---

# 50. IMPORTANT: THE CURRENT PYTHON SKELETON IS NOT A UNIVERSAL QWEN EXPORT

The supplied Python engine is an architectural/runtime skeleton.

Before claiming support for a concrete model:

1. inspect the real ONNX graph;
2. inspect all input names/types/shapes;
3. inspect all output names/types/shapes;
4. determine whether KV/past/present/state exists;
5. determine whether logits are last-token-only or full-sequence;
6. determine whether the graph is decoder-only causal;
7. create an explicit `model_io.json`;
8. run a one-prompt correctness test;
9. run a second decode-step test;
10. compare multiple-token generation for deterministic seed.

Do not make the skeleton "support Qwen" by guessing names.

---

# 51. TEST MATRIX

## Test 1 — environment

Verify:

    import onnxruntime
    CUDAExecutionProvider in get_available_providers()

## Test 2 — provider

Verify:

    session.get_providers()

Expected:

    CUDAExecutionProvider

## Test 3 — metadata

Verify:

- all expected inputs present
- all expected outputs present
- dtype/shape are understood

## Test 4 — tokenizer parity

For known messages:

    rendered prompt
    token IDs
    decoded tokens

must remain stable.

## Test 5 — deterministic generation

Seed fixed:

    same request
    same model
    same seed

should produce the same generated sequence where the backend/model path is deterministic.

## Test 6 — context continuity

    user -> assistant
    then user follow-up

must use existing context where compatible.

## Test 7 — snapshot parity

    generate A
    save
    unload/reload
    restore
    generate B

must match an uninterrupted context path within the model/runtime's deterministic constraints.

## Test 8 — cancellation

Cancel during:

- prefill
- decode
- streaming

and verify that the context lock is released and the engine remains usable.

## Test 9 — unload

After unload:

- ORT session is gone
- tokenizer references are gone
- context store is cleared or explicitly rejected
- next load works

## Test 10 — CPU fallback

With fallback disabled, a CUDA-unavailable environment must fail early and clearly.

---

# 52. COPILOT IMPLEMENTATION ORDER

Do not start by adding model-specific generation code.

Implement in this order:

1. model package
2. runtime/CUDA session
3. graph metadata inspector
4. explicit model I/O schema
5. tokenizer
6. chat template
7. context state
8. low-level adapter
9. prefill
10. one-step decode
11. sampler
12. stop controller
13. generation loop
14. context snapshots
15. async wrapper
16. process/IPC adapter if required
17. metrics
18. tests
19. only then model-specific optimizations

After every stage, keep a small executable smoke test.

---

# 53. DO NOT CHANGE THE PUBLIC APP API TO MATCH THE PYTHON INTERNALS

The existing .NET application already has its own API/application abstractions.

Create a thin C# adapter such as:

    PythonInferenceEngineClient

or an equivalent name that matches the existing architecture.

That class converts:

    existing C# request
        ->
    Python GenerateRequest JSON

and:

    Python token events
        ->
    existing C# streaming abstraction

Do not expose:

    OrtValue
    NumPy
    ONNX tensor names
    Jinja
    tokenizer internals

to the rest of the C# application.

---

# 54. DO NOT PUT ENGINE CONFIG INTO APP GLOBALS

Inference configuration belongs to an engine/model instance.

Recommended:

    OnnxEngineOptions
        ModelPackagePath
        Cuda
        Session
        GenerationDefaults
        Context
        Diagnostics

C# application configuration can construct these options, but Python runtime options should remain grouped by concern.

---

# 55. "ALL KNOBS" CHECKLIST

The C# integration should be able to configure or explicitly expose:

## Model

    model package path
    ONNX model path override if needed
    model_io.json override if needed
    trust_remote_code
    local_files_only

## Tokenization

    use_fast
    chat template selection
    add_generation_prompt
    tools
    documents
    template kwargs

## Context

    max_context_tokens
    context_id
    reset/truncate policy
    snapshot path
    snapshot format version

## Sampling

    temperature
    top_k
    top_p
    min_p
    repetition_penalty
    frequency_penalty
    presence_penalty
    seed

## Stop

    max_new_tokens
    eos_token_ids
    stop_token_ids
    stop_strings

## Runtime

    device_id
    allow_cpu_fallback
    preload_cuda_dlls
    CUDA DLL directory

## CUDA EP

    gpu_mem_limit
    arena_extend_strategy
    cudnn_conv_algo_search
    do_copy_in_default_stream
    enable_cuda_graph
    use_tf32
    extra_provider_options

## ORT session

    graph_optimization_level
    execution_mode
    intra_op_num_threads
    inter_op_num_threads
    enable_mem_pattern
    enable_cpu_mem_arena
    enable_profiling
    disable_prepacking

## Execution path

    enable_io_binding
    use_cuda_resident_state
    warmup

## Diagnostics

    log level
    profiling
    timings
    provider diagnostics
    I/O metadata

Not every knob should be enabled blindly. A knob is exposed so that it can be controlled and benchmarked, not because it should be changed from its default.

---

# 56. FUTURE EXTENSIONS — DO NOT IMPLEMENT PROACTIVELY

Keep extension points for:

- multi-sequence batching
- continuous batching
- beam search
- speculative decoding
- draft-model decoding
- multimodal inputs
- multiple ONNX sessions
- model ensembles
- CUDA graph pools per shape
- paged KV/state
- prefix cache
- quantized ONNX models
- direct native CUDA kernels
- model-specific fused operators

Do not implement these without a concrete model graph and benchmark requirement.

---

# 57. MULTI-GPU WARNING

The current llama.cpp architecture's:

    -sm layer
    -sm tensor

concept does NOT map directly onto ONNX Runtime CUDAExecutionProvider.

Do not invent:

    tensor split
    layer split
    VRAM sharding

as generic ORT settings.

ONNX Runtime CUDA EP selects a CUDA device for a session. Arbitrary multi-GPU execution requires a deliberate graph/model partitioning strategy.

If a future exported ONNX model is explicitly partitioned across GPUs, introduce a separate multi-device adapter with a documented graph contract.

---

# 58. FINAL COPILOT RULESET

Copilot must follow these rules while implementing the integration:

    1. Do not create Python files under the existing C# /src tree.
    2. Do not create an HTTP server in Python.
    3. Do not create an OpenAI API implementation in Python.
    4. Do not move tokenizer/chat-template logic into C#.
    5. Do not move sampling into C#.
    6. Do not move KV/context state into C#.
    7. Do not duplicate the ONNX graph contract in C#.
    8. Do not invent model I/O names.
    9. Inspect the actual ONNX graph before adding model-specific adapter code.
    10. Do not silently enable CPU fallback.
    11. Do not serialize tensors through IPC.
    12. Do not recreate ONNX Runtime sessions per request/token.
    13. Do not add background threads without an explicit ownership/lifetime model.
    14. Do not call GC in the generation hot loop.
    15. Do not change existing C# public APIs unless required by the integration.
    16. Keep Python and C# contracts versioned.
    17. Keep context state owned by Python.
    18. Keep the inference hot path inside Python + ORT + CUDA.
    19. Keep external HTTP/OpenAI mapping in the existing C# application.
    20. Prefer explicit configuration objects over scattered literals.
    21. Prefer deterministic, testable components.
    22. Do not "fix" a missing model-specific detail by guessing.
    23. If the concrete ONNX graph contradicts an assumption in this document, stop at the adapter boundary and update the graph-specific contract rather than contaminating the generic engine.
    24. Do not optimize without measuring.
    25. Preserve a clean separation between model-independent runtime code and model/export-specific adapter code.

---

# 59. SUCCESS CRITERIA

The integration is complete when the C# application can:

    start Python engine
    |
    load model package
    |
    verify CUDA
    |
    create context
    |
    send messages + generation options
    |
    receive streamed tokens
    |
    cancel generation
    |
    continue the same context
    |
    save context
    |
    restore context
    |
    reset/destroy context
    |
    unload model
    |
    shutdown Python

while the C# application remains completely unaware of:

    Jinja syntax
    tokenizer internals
    ONNX tensor names
    KV tensor layouts
    sampling implementation
    CUDA OrtValues
    ONNX Runtime session objects
    model-specific state semantics

That separation is the core architectural requirement.
