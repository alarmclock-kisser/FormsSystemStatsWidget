
# Requirement Specification

# .NET 10 Native ONNX Multi-GPU Inference Engine & OpenAI-Compatible Server

**Project:** Native .NET 10 ONNX Multi-GPU LLM Engine
**Primary Model:** Qwen3.8-27B INT4 ONNX
**Target Platform:** Windows x64
**Primary Runtime:** .NET 10
**GPU Backend:** ONNX Runtime CUDA Execution Provider
**API:** OpenAI-compatible HTTP API
**Reference Backend:** Existing validated Python ONNX Runtime engine
**Primary Use Case:** Local Qwen inference for applications such as VS Code / Copilot-compatible clients

**⚠️ Python Inference Engine (HART — NIE ignorieren):**
- **Code:** `PyDualOnnxInferenceEngine/` (im Projekt-Root neben dieser Datei)
- **Integrations-Doku:** `PyDualOnnxInferenceEngine/COPILOT_ONNX_PYTHON_ENGINE_INTEGRATION.md`
- **Architektur:** C# = Infrastruktur (HTTP, OpenAI-API, Config, Validation, Diagnostics, Python-Process-Supervision, IPC). Python = komplette Inference (Tokenizer, Chat-Template, Prefill, Decode, Sampling, KV-State, ONNX Runtime CUDA).
- **NIE** Inference-Logik in C# implementieren. **NIE** ONNX-NuGets (`Microsoft.ML.OnnxRuntime*`) zum C#-Projekt hinzufügen.

---

# 1. Project Objective

Build a production-capable **native .NET 10 class library + optional ASP.NET Core server** for local large-language-model inference using ONNX Runtime.

The system must be capable of loading and serving large ONNX language models, especially:

```text
Qwen3.8-27B
INT4
ONNX
```

across multiple NVIDIA GPUs.

The primary tested configuration is:

```text
GPU 0: 16 GB VRAM
GPU 1: 12 GB VRAM

Stage 0:
  embedding
  transformer layers 0..41

Stage 1:
  transformer layers 42..63
  final normalization
  lm_head
```

The engine must maintain persistent inference state on the GPUs and perform autoregressive generation without reloading the model or reconstructing the complete context for every token.

The public interface must be usable from native .NET applications and must additionally expose an **OpenAI-compatible HTTP API**.

The goal is to make the resulting server usable as a local backend for:

* VS Code
* Copilot-compatible clients
* OpenAI-compatible applications
* custom .NET applications
* Python applications
* command-line clients
* local development tools
* future multimodal clients

---

# 2. Critical Architectural Principle

The project consists of two logically separate layers.

## Layer A — Native Engine

Responsible for:

* model discovery
* model validation
* ONNX validation
* execution-provider management
* CUDA device management
* model partitioning
* session creation
* GPU state management
* prefill
* decode
* sampling
* tokenization
* detokenization
* streaming
* benchmarking
* telemetry
* diagnostics
* concurrency
* cancellation
* reset
* resource management

## Layer B — API Server

Responsible for:

* HTTP
* OpenAI-compatible DTOs
* authentication if enabled
* request validation
* model selection
* chat templates
* streaming SSE
* non-streaming responses
* request/session management
* health endpoints
* metrics endpoints
* error handling

The engine must **not depend on HTTP**.

The HTTP server must depend on the engine.

---

# 3. Existing Validated Reference Implementation

An existing Python implementation has already proven the fundamental architecture.

Do NOT discard this implementation.

The following functionality is already validated and must be treated as the behavioral reference:

### Model

```text
Qwen3.8-27B ONNX INT4
```

### Partition

```text
Stage 0:
  embedding + layers 0..41

Stage 1:
  layers 42..63 + final norm + lm_head
```

### Devices

```text
Stage 0 -> CUDA:0
Stage 1 -> CUDA:1
```

### Boundary tensors

```text
/model/layers.41/post_attention_layernorm/output_3
/model/layers.41/mlp/down_proj/MatMul/output_0
```

Shape:

```text
[batch_size, sequence_length, 5120]
```

Datatype:

```text
float16
```

### KV cache

```text
KV head count = 4
KV cache dimension = 256
```

### Persistent state

The model uses:

* KV cache
* convolution state
* recurrent state

These states must remain GPU-resident between decode calls.

---

# 4. Proven Validation

The existing implementation has already successfully demonstrated:

## 4.1 CPU partition execution

Stage 0 and Stage 1 execute independently and correctly.

## 4.2 CUDA Stage 0

Stage 0 executes successfully on CUDA:0.

## 4.3 CUDA Stage 1

Stage 1 executes successfully on CUDA:1.

## 4.4 End-to-end GPU pipeline

Real Stage 0 output is consumed by Stage 1 and produces valid logits.

## 4.5 Device-resident boundary

Stage 0 CUDA output is passed as a CUDA OrtValue/device pointer to Stage 1 without an explicit CPU roundtrip.

Important:

This proves device-resident handoff accepted by ONNX Runtime.

It does **not** yet prove physical PCIe/NVLink peer-to-peer transfer.

Do not claim hardware P2P without explicit instrumentation.

## 4.6 Persistent decode

Multiple sequential tokens were generated while:

* models remained loaded
* KV cache remained resident
* convolution state remained resident
* recurrent state remained resident
* no persistent-state CPU roundtrip occurred

## 4.7 Real prefill

A 128-token prefill has been validated.

Example baseline:

```text
Prefill:
128 tokens

Stage 0:
~0.357 s

Stage 1:
~0.214 s

Pipeline:
~0.571 s

Effective:
~224 tok/s
```

## 4.8 Steady-state decode

Validated baseline:

```text
Stage 0:
~40 ms/token

Stage 1:
~21 ms/token

Total:
~62 ms/token

Throughput:
~16 tok/s
```

The engine must preserve this behavior while being migrated to C#.

---

# 5. Target Solution

Create a Visual Studio / dotnet solution similar to:

```text
QwenOnnxNative.sln

src/
    QwenOnnxNative/
        QwenOnnxNative.csproj

    QwenOnnxNative.Server/
        QwenOnnxNative.Server.csproj

    QwenOnnxNative.Cli/
        QwenOnnxNative.Cli.csproj

tests/
    QwenOnnxNative.Tests/
    QwenOnnxNative.IntegrationTests/
    QwenOnnxNative.BenchmarkTests/
```

The core class library must have no dependency on ASP.NET.

---

# 6. Target Framework

Mandatory:

```xml
<TargetFramework>net10.0</TargetFramework>
```

Platform:

```text
win-x64
```

Use nullable reference types:

```xml
<Nullable>enable</Nullable>
```

Use implicit usings where appropriate.

Prefer modern C# features available in .NET 10.

---

# 7. Core Public API

The core engine must expose a clean API similar to:

```csharp
public interface IInferenceEngine : IAsyncDisposable
{
    EngineInfo GetInfo();

    Task LoadAsync(
        CancellationToken cancellationToken = default);

    Task<PrefillResult> PrefillAsync(
        IReadOnlyList<int> inputTokens,
        GenerationOptions options,
        CancellationToken cancellationToken = default);

    Task<DecodeResult> DecodeAsync(
        int token,
        GenerationOptions options,
        CancellationToken cancellationToken = default);

    Task ResetAsync(
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<TokenResult> GenerateAsync(
        IReadOnlyList<int> inputTokens,
        GenerationOptions options,
        CancellationToken cancellationToken = default);
}
```

The exact API may differ if technically justified, but the architecture must remain equivalent.

---

# 8. Engine Lifecycle

The engine must support:

```text
Construct
    ↓
Validate configuration
    ↓
Discover environment
    ↓
Validate CUDA
    ↓
Validate model
    ↓
Load Stage 0
    ↓
Load Stage 1
    ↓
Allocate initial state
    ↓
Ready
    ↓
Prefill
    ↓
Decode
    ↓
Decode
    ↓
...
    ↓
Reset
    ↓
Prefill again
    ↓
Decode
    ↓
Dispose
```

Model loading must happen exactly once per engine instance unless an explicit reload is requested.

---

# 9. Lazy Loading

Support configurable lazy loading.

Example:

```json
{
  "Engine": {
    "LazyLoad": true
  }
}
```

If lazy loading is enabled:

```csharp
new Engine(...)
```

must not necessarily load the 27B model immediately.

The first actual inference request triggers loading.

Also provide:

```csharp
await engine.LoadAsync();
```

for explicit eager loading.

---

# 10. No Magic Values

Absolutely no hidden hardcoded model assumptions in the general engine.

Do NOT hardcode:

```text
5120
248320
256
64 layers
42 split
CUDA:0
CUDA:1
128 context
```

These values may exist in a **model-specific configuration profile**.

The engine itself must discover model properties where possible.

For example:

```json
{
  "Model": {
    "ModelId": "Qwen3.8-27B-onnx-int4",
    "ModelDirectory": "...",
    "PartitionMode": "PrePartitioned",
    "Stage0Model": "model.stage0.onnx",
    "Stage1Model": "model.stage1.onnx"
  }
}
```

Model metadata must determine:

* hidden size
* vocabulary size
* layer count
* dtype
* KV dimensions
* supported sequence length
* architecture
* tokenizer
* chat template

---

# 11. Configuration System

Use strongly typed .NET options.

Example:

```csharp
public sealed class EngineOptions
{
    public ModelOptions Model { get; init; } = new();
    public RuntimeOptions Runtime { get; init; } = new();
    public CudaOptions Cuda { get; init; } = new();
    public GenerationDefaults Generation { get; init; } = new();
    public BenchmarkOptions Benchmark { get; init; } = new();
    public DiagnosticsOptions Diagnostics { get; init; } = new();
}
```

All settings must be configurable through:

1. `appsettings.json`
2. environment variables
3. command-line arguments
4. programmatic configuration
5. optionally per-request API parameters

---

# 12. CUDA Configuration

Must support explicit GPU selection.

Example:

```json
{
  "Cuda": {
    "Enabled": true,
    "Stage0Device": 0,
    "Stage1Device": 1,
    "UseCudaExecutionProvider": true,
    "EnableMemoryArena": true,
    "EnableCudnn": true
  }
}
```

Do not assume two GPUs.

Support:

```text
1 GPU
2 GPU
N GPU
```

where technically possible.

The partition graph should define which stage goes to which device.

---

# 13. Execution Providers

The architecture must support configurable execution providers.

At minimum:

```text
CUDA
CPU
```

Optional providers:

```text
TensorRT
DirectML
CPU
```

The system must never silently fall back from CUDA to CPU without reporting it.

Provider configuration must be visible in diagnostics.

Example:

```json
{
  "Runtime": {
    "ExecutionProviders": [
      "CUDAExecutionProvider",
      "CPUExecutionProvider"
    ],
    "AllowCpuFallback": true
  }
}
```

---

# 14. Provider Diagnostics

At startup expose:

```text
Available providers
Requested providers
Active providers
CUDA runtime version
CUDA device count
CUDA device names
CUDA memory
CUDA compute capability
ONNX Runtime version
Python environment if applicable
```

The existing warning:

```text
No registered plugin EP device found for CUDAExecutionProvider
```

must not automatically be treated as fatal if CUDAExecutionProvider itself loads and inference succeeds.

However, warnings must be logged clearly.

The engine must distinguish:

```text
INFO
WARNING
ERROR
FATAL
```

---

# 15. Python Environment Management

Python is not the primary inference server.

Python exists as a controlled compatibility/tooling layer where required.

The system must optionally inspect a configured Python environment.

Support:

```text
system Python
venv
embedded Python
configured Python executable
```

Configuration:

```json
{
  "Python": {
    "Enabled": true,
    "Executable": "",
    "EnvironmentDirectory": "",
    "AutoCreateVenv": true,
    "AutoInstallDependencies": false,
    "AutoUpgradeDependencies": false,
    "AllowNetworkAccess": false
  }
}
```

Never silently install packages unless explicitly enabled.

---

# 16. Python Environment Validation

Provide a validator:

```text
PythonEnvironmentValidator
```

It must check:

* Python executable exists
* Python version
* virtual environment
* pip availability
* package availability
* package versions
* CUDA compatibility
* ONNX Runtime availability
* ONNX availability
* NumPy
* tokenizer dependencies
* transformers
* safetensors
* huggingface-hub
* tokenizers
* accelerate
* optional model-specific dependencies

Output machine-readable diagnostics.

---

# 17. Python Dependency Manifest

Create:

```text
requirements.txt
requirements.lock.txt
```

where practical.

Do not blindly install newest packages.

Record tested versions.

Provide:

```text
environment report
```

including:

```text
Python
pip
onnxruntime
onnx
numpy
transformers
tokenizers
safetensors
huggingface-hub
accelerate
CUDA-related packages
```

---

# 18. Dependency Installation

If explicitly enabled:

```text
Validate
    ↓
Determine missing packages
    ↓
Show planned changes
    ↓
Install
    ↓
Verify imports
    ↓
Verify versions
    ↓
Run CUDA smoke test
```

Never modify an existing environment without logging the operation.

Support:

```text
--check
--repair
--install
--upgrade
--freeze
```

---

# 19. Model Discovery

Support automatic model discovery.

Example:

```text
D:\Models\ONNX\
```

with:

```text
D:\Models\ONNX\Qwen3.8-27B-onnx-int4\
D:\Models\ONNX\gemma-4-eb4-it-ONNX\
```

A model directory may contain:

```text
*.onnx
*.onnx.data
*.json
*.jinja
*.txt
tokenizer files
configuration files
metadata
```

Do not require only one exact filename globally.

---

# 20. Model Manifest

Create a normalized internal model manifest.

Example:

```csharp
public sealed class ModelManifest
{
    public string ModelId { get; init; }
    public string ModelDirectory { get; init; }

    public IReadOnlyList<ModelArtifact> Artifacts { get; init; }

    public ModelArchitectureInfo Architecture { get; init; }

    public TokenizerInfo Tokenizer { get; init; }

    public PartitionInfo? Partition { get; init; }

    public ValidationResult Validation { get; init; }
}
```

---

# 21. Model Artifact Validation

Validate all model-related files.

At minimum:

```text
model.onnx
model.onnx.data
config.json
generation_config.json
genai_config.json
tokenizer.json
tokenizer_config.json
special_tokens_map.json
chat_template.jinja
merges.txt
vocab.json
```

Only files actually belonging to the model need to be validated.

Unknown files must not automatically be considered errors.

---

# 22. ONNX Validation

Perform multiple levels of validation.

## Level 1 — Filesystem

Check:

* file exists
* file readable
* file size > 0

## Level 2 — External Data

For `.onnx.data`:

* referenced offsets
* lengths
* file size
* end offsets
* overlap detection
* missing ranges
* invalid locations

## Level 3 — ONNX protobuf

Load graph without unnecessary external data loading.

Validate:

* nodes
* initializers
* graph inputs
* graph outputs
* opsets
* domains
* external-data metadata

## Level 4 — Runtime validation

Create an ONNX Runtime session.

Some custom/runtime-specific operators may not be recognized by the generic ONNX checker.

Example:

```text
SimplifiedLayerNormalization
```

must not be replaced merely because `onnx.checker` does not know the operator.

ONNX Runtime is the authoritative execution validator.

---

# 23. Binary Integrity Validation

Provide SHA-256 validation for:

```text
model.onnx
model.onnx.data
all partition data files
all optional binary artifacts
```

Support optional manifest:

```text
checksums.sha256
```

Example:

```json
{
  "model.onnx": {
    "size": 650949,
    "sha256": "..."
  },
  "model.onnx.data": {
    "size": 11628550144,
    "sha256": "..."
  }
}
```

---

# 24. Model JSON Validation

Parse JSON files independently.

Detect:

* malformed JSON
* missing required fields
* incompatible architecture
* tokenizer mismatch
* vocabulary mismatch
* inconsistent special tokens
* context-length inconsistencies
* generation configuration conflicts

Do not assume every JSON file has the same schema.

---

# 25. Chat Template

Support:

```text
chat_template.jinja
```

where available.

Prefer model-provided templates over hardcoded templates.

Provide a template engine abstraction:

```csharp
public interface IChatTemplateRenderer
{
    string Render(
        IReadOnlyList<ChatMessage> messages,
        ChatTemplateOptions options);
}
```

---

# 26. Tokenizer

Tokenizer must be an independent service:

```csharp
ITokenizer
```

Required operations:

```csharp
IReadOnlyList<int> Encode(string text);
string Decode(IEnumerable<int> tokens);
IReadOnlyList<int> EncodeChat(...);
```

Support model-specific tokenizer implementations.

Do not assume a particular tokenizer format.

---

# 27. Generation API

Generation options must be completely configurable.

Support at minimum:

```text
temperature
top_p
top_k
min_p
typical_p
seed
max_tokens
stop
stop_token_ids
presence_penalty
frequency_penalty
repeat_penalty
```

Where the model/backend supports them.

No parameter should be hardcoded into the engine.

---

# 28. Sampling Architecture

Create:

```csharp
ISampler
```

with implementations such as:

```text
GreedySampler
TemperatureSampler
TopKSampler
TopPSampler
MinPSampler
CompositeSampler
```

Greedy must be available as a deterministic baseline.

---

# 29. Prefill

The engine must distinguish:

```text
Prefill
Decode
```

Prefill accepts multiple tokens:

```text
N >= 1
```

and must process the entire sequence in one graph execution where supported.

After prefill:

```text
sequence_length = N
```

Persistent state must represent the complete context.

---

# 30. Decode

Decode must process exactly one new token.

Input:

```text
token
```

Output:

```text
next token
```

The model must not reload.

The full context must not be resent through all transformer layers.

KV/conv/recurrent state must be reused.

---

# 31. Persistent State

State types must be represented explicitly.

Example:

```csharp
public sealed class InferenceState
{
    public IReadOnlyDictionary<string, OrtValue> Stage0State { get; }
    public IReadOnlyDictionary<string, OrtValue> Stage1State { get; }

    public int SequenceLength { get; }
}
```

State must remain device-resident.

Avoid:

```text
GPU → CPU → GPU
```

for persistent state.

---

# 32. Empty KV State

The implementation must correctly handle:

```text
[batch, heads, 0, kv_dim]
```

Empty initial KV buffers may legally have no physical data pointer.

Do not treat a null pointer as an error when the tensor has zero elements.

For non-empty state tensors:

```text
data_ptr != 0
```

must be validated.

---

# 33. Boundary Handling

The two Stage 0 outputs must remain device-resident whenever possible.

Conceptually:

```text
CUDA:0
   │
   ├── boundary tensor A
   │
   └── boundary tensor B
           │
           ▼
      ONNX Runtime
           │
           ▼
CUDA:1
```

The API must not call:

```text
CopyOutputsToCpu()
```

between stages.

The implementation must use OrtValue/device-pointer based binding where supported.

---

# 34. GPU Memory

Provide memory diagnostics.

Track:

```text
VRAM total
VRAM used
VRAM free
stage memory
state memory
temporary memory
```

where APIs permit.

Support configurable memory behavior:

```text
arena
memory pattern
preallocation
allocation strategy
```

Do not introduce undocumented custom allocators without profiling.

---

# 35. Concurrency

The engine must explicitly define whether an engine instance supports:

```text
one active generation
multiple concurrent generations
```

The initial Qwen3.8 27B configuration should default to:

```text
MaxConcurrentGenerations = 1
```

because persistent KV state belongs to a generation/session.

For concurrent requests, use independent generation sessions/states.

Never allow two HTTP requests to mutate the same KV state concurrently.

---

# 36. Session Abstraction

Introduce:

```csharp
IInferenceSession
```

with:

```text
session id
model
state
sequence length
generation statistics
creation time
last activity
```

Example:

```csharp
public interface IInferenceSession : IAsyncDisposable
{
    string Id { get; }

    int SequenceLength { get; }

    Task<PrefillResult> PrefillAsync(...);

    Task<DecodeResult> DecodeAsync(...);

    Task ResetAsync(...);
}
```

---

# 37. Reset

Reset must:

* clear sequence length
* replace/reset KV state
* replace/reset convolution state
* replace/reset recurrent state
* clear last logits
* clear boundary references
* preserve loaded ONNX sessions

Expected behavior:

```text
Before reset:
loaded = true
sequence = N

After reset:
loaded = true
sequence = 0
```

Reset must not reload the model.

---

# 38. Benchmarking

Benchmarking is a first-class feature.

Provide:

```text
IBenchmarkRunner
```

and CLI/API support.

Benchmark:

### Loading

```text
Stage 0 load
Stage 1 load
state creation
total load
```

### Prefill

Test configurable lengths:

```text
1
32
128
512
1024
2048
4096
...
```

### Decode

Configurable:

```text
warmup tokens
measured tokens
```

Collect:

```text
mean
median
p50
p90
p95
p99
min
max
standard deviation
```

---

# 39. Stage Timing

Every inference step should optionally expose:

```text
stage0
stage1
total
logits transfer
sampling
tokenization
detokenization
HTTP overhead
```

Example:

```json
{
  "stage0_ms": 40.1,
  "stage1_ms": 20.9,
  "total_ms": 62.0,
  "logits_copy_ms": 0.2
}
```

---

# 40. Real-Time Statistics

Configurable telemetry levels:

```text
Off
Error
Warning
Information
Debug
Trace
Performance
```

At performance level, optionally print:

```text
tokens/s
ms/token
context length
GPU0 time
GPU1 time
GPU memory
queue depth
request id
session id
```

Example:

```text
[GEN]
token=5328
stage0=39.8ms
stage1=20.1ms
total=60.2ms
throughput=16.61 tok/s
context=128
GPU0=...
GPU1=...
```

---

# 41. Metrics API

Expose optional:

```text
GET /metrics
GET /health
GET /ready
GET /status
```

Health must distinguish:

```text
process alive
engine loaded
engine ready
model valid
CUDA valid
GPU memory sufficient
```

---

# 42. OpenAI-Compatible API

The server must expose:

```text
GET  /v1/models

POST /v1/chat/completions

POST /v1/completions
```

Optional:

```text
POST /v1/embeddings
POST /v1/responses
```

where meaningful for the underlying model.

---

# 43. Chat Completions

Support standard OpenAI-style request structure:

```json
{
  "model": "Qwen3.8-27B-onnx-int4",
  "messages": [
    {
      "role": "system",
      "content": "You are a helpful assistant."
    },
    {
      "role": "user",
      "content": "Hello!"
    }
  ],
  "stream": true,
  "temperature": 0.7,
  "top_p": 0.9,
  "max_tokens": 512
}
```

---

# 44. Streaming

Streaming must be the default server behavior where requested.

Use:

```text
text/event-stream
```

and OpenAI-compatible SSE chunks.

Example:

```text
data: {...}

data: {...}

data: [DONE]
```

The engine must stream tokens as soon as they are available.

Do not wait for the complete response before emitting chunks.

---

# 45. Non-Streaming Fallback

If:

```json
"stream": false
```

return a normal OpenAI-compatible completion response.

Both modes must use the same underlying generation engine.

Do not implement a separate inference backend for non-streaming mode.

---

# 46. Completions API

Support:

```text
POST /v1/completions
```

for clients that do not use chat messages.

Example:

```json
{
  "model": "Qwen3.8-27B-onnx-int4",
  "prompt": "Once upon a time",
  "max_tokens": 128,
  "stream": true
}
```

---

# 47. FIM — Fill in the Middle

Support FIM if the model/tokenizer provides appropriate special tokens.

Provide an abstraction:

```csharp
public interface IFimFormatter
{
    bool IsSupported { get; }

    IReadOnlyList<int> BuildPrompt(
        string prefix,
        string suffix);
}
```

Do not assume Qwen supports arbitrary FIM semantics.

Detect supported special tokens from tokenizer/model configuration.

If unsupported:

```text
return capability = false
```

rather than pretending FIM is supported.

---

# 48. VS Code / Copilot Compatibility

The API must be designed for compatibility with OpenAI-style clients.

Do not build custom client requirements into the engine.

The following should work where the client supports configurable OpenAI-compatible endpoints:

```text
http://localhost:<port>/v1
```

The model endpoint:

```text
/v1/models
```

must return the configured model IDs.

---

# 49. API Authentication

Optional API-key authentication.

Configuration:

```json
{
  "Server": {
    "Authentication": {
      "Enabled": false,
      "ApiKeys": []
    }
  }
}
```

Localhost should be usable without authentication by default.

Never log API keys.

---

# 50. Server Binding

Configurable:

```text
host
port
HTTPS
HTTP
```

Example:

```json
{
  "Server": {
    "ListenUrls": [
      "http://127.0.0.1:8080"
    ]
  }
}
```

Do not hardcode port 8080.

---

# 51. Request Cancellation

Every request must support:

```csharp
CancellationToken
```

If a client disconnects during streaming:

```text
cancel generation
release request resources
preserve engine health
```

Do not leave GPU state corrupted.

---

# 52. Timeouts

Configurable:

```text
model load timeout
prefill timeout
decode timeout
request timeout
idle session timeout
```

---

# 53. Error Handling

Errors must be classified.

Examples:

```text
ConfigurationError
EnvironmentError
CudaError
ModelValidationError
TokenizerError
InferenceError
OutOfMemoryError
UnsupportedFeatureError
RequestValidationError
CancellationError
```

OpenAI-compatible API errors should be mapped to structured JSON.

---

# 54. OOM Protection

Before model loading:

* inspect GPU memory
* inspect expected model artifacts
* optionally estimate state memory
* warn about insufficient memory

During inference:

* catch CUDA/ORT OOM
* provide diagnostics
* do not silently switch to CPU unless explicitly configured
* preserve process stability where possible

---

# 55. Model Partitioning

The engine must support two modes.

## Mode A — PrePartitioned

User supplies:

```text
stage0.onnx
stage0.onnx.data
stage1.onnx
stage1.onnx.data
```

This is the initial required implementation.

## Mode B — Automatic Partitioning

Future capability.

The engine may:

1. inspect graph
2. determine layer boundaries
3. calculate initializer sizes
4. determine GPU capacities
5. split graph
6. write external data
7. validate partitions

The existing custom external-data-aware partitioner should be treated as the reference.

---

# 56. External Data Handling

Must support large ONNX external data files.

Do not rely exclusively on APIs that serialize the entire model protobuf into memory if this causes failures for very large models.

The existing model contains approximately:

```text
17 GB
```

of external initializer data.

Partitioning must be chunk-based.

Example chunk:

```text
64 MiB
```

but this value must be configurable.

---

# 57. Partition Validation

After partitioning verify:

```text
stage0 graph exists
stage0 data exists
stage1 graph exists
stage1 data exists
all external references valid
no invalid offsets
no missing initializers
graph inputs valid
graph outputs valid
boundary tensors valid
```

Then execute smoke inference.

---

# 58. Reference Model

If an original monolithic model is available, optionally compare:

```text
original model
vs
partitioned model
```

for deterministic test inputs.

Compare:

```text
logits
hidden states
state tensors
```

with configurable tolerances.

Do not require bit-identical results for different execution providers.

---

# 59. Determinism

Support deterministic test mode.

Example:

```json
{
  "Runtime": {
    "Deterministic": true
  }
}
```

Greedy generation with identical inputs should produce identical token sequences where the backend itself is deterministic.

---

# 60. Logging

Use:

```text
Microsoft.Extensions.Logging
```

throughout.

Do not use:

```text
Console.WriteLine
```

inside the core engine except CLI-specific code.

Structured logging fields should include:

```text
model
stage
device
session
request
sequence_length
token
elapsed_ms
```

---

# 61. Diagnostics Object

Provide:

```csharp
EngineDiagnostics
```

containing:

```text
engine version
runtime version
OS
CPU
RAM
GPU information
CUDA version
ORT version
model information
partition information
provider information
memory information
Python environment
tokenizer
capabilities
warnings
```

---

# 62. Capability Discovery

Expose:

```csharp
EngineCapabilities
```

Example:

```json
{
  "chat": true,
  "completion": true,
  "streaming": true,
  "fim": false,
  "vision": false,
  "embeddings": false,
  "multiGpu": true,
  "persistentKvCache": true
}
```

The API should expose capabilities where useful.

---

# 63. Image Input

Design the API so multimodal support can be added without redesigning the entire engine.

Support OpenAI-style content blocks:

```json
{
  "role": "user",
  "content": [
    {
      "type": "text",
      "text": "What is in this image?"
    },
    {
      "type": "image_url",
      "image_url": {
        "url": "..."
      }
    }
  ]
}
```

However:

**Do not fake vision support.**

Only enable it when the loaded model actually provides the required:

* vision encoder
* image processor
* tokenizer/projector
* graph inputs
* runtime support

If unsupported:

```text
capability = false
```

and return a useful API error.

---

# 64. Image Input Abstraction

Prepare:

```csharp
IImageProcessor
```

with:

```csharp
Task<ImageTensor> ProcessAsync(
    ImageInput input,
    CancellationToken cancellationToken);
```

The core text-only Qwen engine must not depend on image processing.

---

# 65. Prompt Construction

Separate:

```text
API request
↓
chat messages
↓
chat template
↓
tokenizer
↓
token IDs
↓
engine
```

Never embed prompt formatting into the CUDA engine.

---

# 66. Context Management

Configurable:

```text
maximum context
maximum generation
context overflow behavior
```

When context is exceeded, support configurable policies:

```text
Error
TruncateOldest
SlidingWindow
```

Do not silently discard user messages.

---

# 67. Stop Conditions

Support:

```text
stop strings
EOS token
custom stop token IDs
max tokens
cancellation
```

Streaming must stop cleanly.

---

# 68. Performance Optimization Roadmap

Initial implementation should prioritize correctness.

After functional parity:

### Optimization 1

Avoid copying the complete logits tensor to CPU.

Current reference behavior effectively performs:

```text
GPU logits
↓
CPU
↓
argmax
```

For:

```text
[1, sequence, 248320]
```

this can become expensive during large prefill.

Future target:

```text
GPU logits
↓
GPU argmax / sampling
↓
single token
```

or equivalent optimized path.

---

# 69. Performance Optimization 2

Minimize synchronization.

Measure:

```text
ORT execution synchronization
OrtValue transfer
GPU-to-GPU boundary
sampling
logging
```

Do not optimize based solely on wall-clock intuition.

---

# 70. Performance Optimization 3

Investigate genuine CUDA peer-to-peer transfers.

Future diagnostics may determine:

```text
P2P supported
P2P enabled
PCIe topology
NVLink topology
actual transfer path
```

Do not claim P2P without measurement.

---

# 71. Performance Optimization 4

Investigate CUDA Graphs where compatible.

Potential target:

```text
steady-state decode
```

with fixed tensor shapes.

This is optional and must only be introduced after baseline correctness is established.

---

# 72. Performance Optimization 5

Reduce Python dependency.

The final inference path should preferably be:

```text
.NET
 ↓
ONNX Runtime native
 ↓
CUDA
```

Python should not be required for ordinary serving once native functionality is complete.

---

# 73. Native ONNX Runtime Integration

Prefer official .NET ONNX Runtime bindings.

The implementation must carefully handle:

```text
InferenceSession
SessionOptions
OrtValue
IoBinding
CUDA execution provider
device pointers
```

Avoid unnecessary conversions:

```text
OrtValue → byte[] → tensor → OrtValue
```

---

# 74. Device Tensor Abstraction

Introduce a safe wrapper around device-resident tensors.

Example:

```csharp
public sealed class DeviceTensor : IDisposable
{
    public string Name { get; }
    public OrtValue Value { get; }
    public int DeviceId { get; }
    public long[] Shape { get; }
    public TensorElementType ElementType { get; }
}
```

The wrapper must own/dispose resources correctly.

---

# 75. Unsafe Code

Unsafe code may be used for:

* device pointer binding
* native interop
* performance-critical tensor operations

but must be isolated behind small, well-tested components.

Example:

```text
Interop/
    OrtInterop.cs
    CudaInterop.cs
```

Do not spread unsafe pointer manipulation through the entire codebase.

---

# 76. Resource Ownership

Every:

```text
InferenceSession
SessionOptions
OrtValue
IoBinding
native resource
CUDA resource
```

must have deterministic disposal.

Use:

```text
IDisposable
IAsyncDisposable
SafeHandle
```

where appropriate.

---

# 77. Thread Safety

Document thread safety explicitly.

Example:

```text
Engine:
    thread-safe for diagnostics

Generation session:
    single-owner

Model session:
    shared only where ONNX Runtime permits

State:
    never concurrently mutated
```

---

# 78. CLI

Provide:

```text
qwen-onnx
```

CLI.

Commands:

```text
info
validate
environment
devices
load
benchmark
partition
serve
generate
```

Examples:

```text
qwen-onnx devices
qwen-onnx validate --model ...
qwen-onnx benchmark --model ...
qwen-onnx serve
```

---

# 79. Validation Command

Example:

```text
qwen-onnx validate --model Qwen3.8-27B-onnx-int4
```

Output:

```text
[OK] model.onnx
[OK] model.onnx.data
[OK] external data references
[OK] config.json
[OK] tokenizer.json
[OK] chat template
[OK] CUDA
[OK] Stage 0
[OK] Stage 1
[OK] boundary
[OK] initial state
[OK] smoke inference
```

---

# 80. Benchmark Command

Example:

```text
qwen-onnx benchmark \
    --prefill 32,128,512,1024 \
    --warmup 10 \
    --tokens 100
```

Output must include:

```text
load
prefill
decode
stage breakdown
throughput
latency
memory
```

Support JSON output:

```text
--format json
```

for automated comparison.

---

# 81. Automated Regression Tests

Create tests for:

```text
configuration
model discovery
JSON validation
external data validation
tokenizer
chat template
CUDA discovery
partition discovery
stage0 loading
stage1 loading
prefill
decode
reset
session isolation
streaming
sampling
cancellation
OOM handling
```

---

# 82. Mandatory Integration Test

The equivalent of the validated Python Test 4 must exist in C#.

It must execute:

```text
Session 1
    prompt 1
    decode 16
    reset

Session 2
    prompt 16
    decode 16
    reset

Session 3
    prompt 128
    decode 16
    reset
```

Validate:

```text
cache growth
state persistence
reset
logits shape
logits finite
argmax
autoregressive chaining
timing
no model reload
```

---

# 83. Performance Regression Baseline

The initial C# implementation should be considered functionally successful when it reproduces approximately the validated Python baseline.

Reference:

```text
Decode:
~60–63 ms/token

Throughput:
~16 tok/s

Stage 0:
~39–41 ms

Stage 1:
~20–21 ms
```

Exact equality is not required.

A significant regression must be investigated before optimization is considered complete.

---

# 84. API Compatibility Tests

Automated tests must issue requests against:

```text
/v1/models
/v1/chat/completions
/v1/completions
```

with:

```text
stream=true
stream=false
```

and verify OpenAI-compatible response structure.

---

# 85. SSE Tests

Validate:

```text
Content-Type
data chunks
JSON structure
delta fields
finish_reason
[DONE]
```

The client must be able to consume the stream incrementally.

---

# 86. OpenAI DTO Layer

Keep API DTOs separate from engine DTOs.

For example:

```text
OpenAi/
    ChatCompletionRequest.cs
    ChatCompletionResponse.cs
    CompletionRequest.cs
    CompletionResponse.cs
    ModelResponse.cs
    ErrorResponse.cs
```

Do not leak ASP.NET or OpenAI DTO types into the inference engine.

---

# 87. API Model Selection

Requests specify:

```json
{
  "model": "Qwen3.8-27B-onnx-int4"
}
```

The server must verify that the model exists.

Support aliases:

```text
qwen
qwen3.8
default
```

through configuration.

Do not hardcode aliases.

---

# 88. Multi-Model Architecture

The server architecture should permit:

```text
Model A
Model B
Model C
```

without changing API design.

Initially only one large model may be resident due to VRAM constraints.

Support:

```text
LoadOnDemand
KeepLoaded
UnloadIdle
```

as future policies.

---

# 89. Model Registry

Create:

```csharp
IModelRegistry
```

for:

```text
discover
validate
register
load
unload
lookup
capabilities
```

---

# 90. Configuration Example

Provide a complete example:

```json
{
  "Server": {
    "ListenUrls": [
      "http://127.0.0.1:8080"
    ],
    "Authentication": {
      "Enabled": false
    }
  },

  "Model": {
    "ModelId": "Qwen3.8-27B-onnx-int4",
    "ModelDirectory": "D:\\Models\\ONNX\\Qwen3.8-27B-onnx-int4",
    "Stage0Model": "D:\\Models\\ONNX\\Qwen3.8-27B-onnx-int4\\partitioned\\model.stage0.onnx",
    "Stage1Model": "D:\\Models\\ONNX\\Qwen3.8-27B-onnx-int4\\partitioned\\model.stage1.onnx",
    "TokenizerDirectory": "",
    "ChatTemplate": ""
  },

  "Cuda": {
    "Enabled": true,
    "Stage0Device": 0,
    "Stage1Device": 1,
    "AllowCpuFallback": true
  },

  "Runtime": {
    "LazyLoad": true,
    "ExecutionProviders": [
      "CUDAExecutionProvider",
      "CPUExecutionProvider"
    ]
  },

  "Generation": {
    "Temperature": 0.7,
    "TopP": 0.9,
    "TopK": 40,
    "MaxTokens": 1024
  },

  "Python": {
    "Enabled": true,
    "Executable": "",
    "EnvironmentDirectory": "",
    "AutoCreateVenv": false,
    "AutoInstallDependencies": false,
    "AutoUpgradeDependencies": false
  },

  "Diagnostics": {
    "LogLevel": "Information",
    "PerformanceLogging": true,
    "GpuMonitoring": true
  }
}
```

This is an example only.

The actual implementation must not depend on these exact values.

---

# 91. Environment Report

Provide a command:

```text
qwen-onnx environment
```

Output:

```text
.NET
OS
CPU
RAM

CUDA
CUDA runtime
CUDA driver
GPU 0
GPU 1

ONNX Runtime
Execution providers

Python
pip
packages

Model
ONNX
external data
tokenizer
chat template
```

Support:

```text
environment.json
```

for bug reports.

---

# 92. Reproducibility

Every benchmark must record:

```text
timestamp
engine version
git commit if available
.NET version
ORT version
CUDA version
GPU model
driver
model path
model hashes
configuration
generation parameters
```

This makes performance comparisons meaningful.

---

# 93. Security

Because this is primarily a local server:

Default:

```text
127.0.0.1
```

Do not bind publicly unless explicitly configured.

Validate:

* model paths
* image URLs
* file paths
* request sizes
* maximum prompt size
* maximum generated tokens

Do not allow arbitrary local file reads through API requests.

---

# 94. Resource Limits

Configurable:

```text
maximum request body
maximum prompt tokens
maximum generation tokens
maximum concurrent requests
maximum sessions
maximum image size
maximum image pixels
```

---

# 95. Graceful Shutdown

On shutdown:

```text
stop accepting requests
cancel active generations
dispose sessions
dispose state
dispose bindings
dispose ORT sessions
release CUDA resources
shutdown ASP.NET
```

No leaked native resources.

---

# 96. Packaging

Provide:

```text
dotnet build
dotnet test
dotnet publish -c Release -r win-x64
```

and preferably:

```text
self-contained
framework-dependent
```

publish options.

---

# 97. Documentation

Generate:

```text
README.md
ARCHITECTURE.md
CONFIGURATION.md
API.md
TROUBLESHOOTING.md
BENCHMARKING.md
MODEL_FORMAT.md
DEVELOPMENT.md
```

The documentation must clearly distinguish:

```text
validated functionality
experimental functionality
future functionality
```

---

# 98. Important Technical Constraints

Do NOT:

* replace custom ONNX operators merely because the generic checker does not understand them
* copy persistent KV state through CPU
* reload the model on every token
* rebuild the entire prompt for every decode token
* hardcode Qwen-specific tensor names throughout generic engine code
* hardcode GPU IDs
* hardcode context length
* hardcode vocabulary size
* hardcode generation parameters
* silently fall back to CPU
* claim physical GPU P2P without measurement
* make Python a mandatory HTTP inference server
* duplicate inference logic between Python and C#
* hide CUDA/provider warnings
* swallow native exceptions
* use global mutable generation state

---

# 99. Reference Compatibility Layer

During development, optionally keep the existing Python engine available as:

```text
ReferenceBackend
```

This allows:

```text
Python result
vs
C# result
```

for regression testing.

The reference backend should eventually become optional and removable.

The C# engine is the intended production backend.

---

# 100. Migration Strategy

Implement in phases.

## Phase 1 — Infrastructure

Build:

```text
.NET 10 solution
configuration
logging
diagnostics
CUDA discovery
ORT discovery
```

## Phase 2 — Model Validation

Implement:

```text
model discovery
JSON validation
ONNX validation
external data validation
hash validation
```

## Phase 3 — Stage Loading

Implement:

```text
Stage0 session
Stage1 session
CUDA device assignment
provider configuration
```

## Phase 4 — State

Implement:

```text
KV state
conv state
recurrent state
device-resident state
reset
```

## Phase 5 — Boundary

Implement:

```text
GPU0 boundary
↓
GPU1 input binding
```

without CPU roundtrip.

## Phase 6 — Prefill

Implement multi-token prefill.

## Phase 7 — Decode

Implement persistent single-token decode.

## Phase 8 — Sampling

Implement:

```text
greedy
temperature
top-k
top-p
etc.
```

## Phase 9 — Tokenizer

Implement model tokenizer.

## Phase 10 — Chat Templates

Implement Jinja/model-specific template support.

## Phase 11 — OpenAI API

Implement:

```text
/v1/models
/v1/completions
/v1/chat/completions
```

## Phase 12 — Streaming

Implement SSE.

## Phase 13 — Benchmarking

Implement complete benchmark suite.

## Phase 14 — Stress Tests

Port the existing Test 4 behavior.

## Phase 15 — Optimization

Only after functional parity:

```text
GPU sampling
reduced synchronization
CUDA graphs
P2P investigation
memory optimization
```

---

# 101. Definition of Done

The project is considered functionally complete when all of the following are true.

## Engine

* [ ] .NET 10 native engine
* [ ] no HTTP dependency in core
* [ ] configurable CUDA devices
* [ ] configurable execution providers
* [ ] model discovery
* [ ] model validation
* [ ] external-data validation
* [ ] tokenizer
* [ ] chat template
* [ ] persistent state
* [ ] reset
* [ ] prefill
* [ ] decode
* [ ] sampling
* [ ] cancellation
* [ ] diagnostics
* [ ] benchmark API

## Multi-GPU

* [ ] Stage 0 CUDA:0
* [ ] Stage 1 CUDA:1
* [ ] device-resident boundary
* [ ] no persistent state CPU roundtrip
* [ ] state validation
* [ ] multi-turn generation

## API

* [ ] `/v1/models`
* [ ] `/v1/chat/completions`
* [ ] `/v1/completions`
* [ ] streaming
* [ ] non-streaming
* [ ] OpenAI-compatible errors
* [ ] cancellation
* [ ] model selection

## Diagnostics

* [ ] CUDA diagnostics
* [ ] ORT diagnostics
* [ ] model diagnostics
* [ ] Python diagnostics
* [ ] GPU memory diagnostics
* [ ] performance statistics
* [ ] structured logging

## Testing

* [ ] unit tests
* [ ] CUDA integration tests
* [ ] prefill test
* [ ] decode test
* [ ] reset test
* [ ] multi-session test
* [ ] benchmark regression test
* [ ] OpenAI API test
* [ ] SSE test

---

# 102. Initial Acceptance Test

The first production acceptance test should reproduce the behavior of the existing validated engine.

Configuration:

```text
Model:
Qwen3.8-27B INT4 ONNX

Stage 0:
CUDA:0

Stage 1:
CUDA:1
```

Test:

```text
Prefill:
128 tokens

Decode:
100 tokens
```

Expected qualitative behavior:

```text
model loads once
state remains resident
GPU0 -> GPU1 boundary remains device-resident
no model reload during decode
cache grows correctly
reset does not reload model
streaming works
```

Performance reference:

```text
Prefill @ 128:
~200+ tok/s effective

Decode:
~16 tok/s
~60–63 ms/token
```

Performance is a regression warning threshold, not a strict hardware-independent requirement.

---

# 103. Final Architectural Goal

The finished system should conceptually look like:

```text
                   ┌──────────────────────────────┐
                   │      VS Code / Client        │
                   └──────────────┬───────────────┘
                                  │
                           OpenAI API
                                  │
                                  ▼
                   ┌──────────────────────────────┐
                   │     ASP.NET Core Server      │
                   │                              │
                   │ /v1/models                   │
                   │ /v1/chat/completions         │
                   │ /v1/completions              │
                   │ SSE streaming                │
                   └──────────────┬───────────────┘
                                  │
                                  ▼
                   ┌──────────────────────────────┐
                   │     Generation Session       │
                   │                              │
                   │ tokenizer                    │
                   │ chat template                │
                   │ sampling                     │
                   │ cancellation                 │
                   └──────────────┬───────────────┘
                                  │
                                  ▼
                   ┌──────────────────────────────┐
                   │      Native .NET Engine      │
                   │                              │
                   │ Prefill / Decode             │
                   │ Persistent State             │
                   │ Benchmarking                 │
                   │ Diagnostics                  │
                   └──────────────┬───────────────┘
                                  │
                     ONNX Runtime CUDA
                                  │
                 ┌────────────────┴────────────────┐
                 │                                 │
                 ▼                                 ▼
        ┌─────────────────┐              ┌─────────────────┐
        │    CUDA:0       │              │    CUDA:1       │
        │                 │              │                 │
        │ Stage 0         │              │ Stage 1         │
        │                 │              │                 │
        │ Embedding       │              │ Layers 42..63   │
        │ Layers 0..41    │─────────────▶│ Final Norm      │
        │                 │  device      │ LM Head         │
        │ KV/Conv/RNN     │  boundary   │ KV/Conv/RNN     │
        │ persistent      │              │ persistent      │
        └─────────────────┘              └────────┬────────┘
                                                  │
                                                  ▼
                                             Logits
                                                  │
                                                  ▼
                                             Sampling
                                                  │
                                                  ▼
                                               Token
                                                  │
                                                  ▼
                                             SSE stream
```

---

# 104. Guiding Principle

The implementation must follow this order:

```text
CORRECTNESS
    ↓
OBSERVABILITY
    ↓
API COMPATIBILITY
    ↓
PERFORMANCE
    ↓
OPTIMIZATION
```

Do not sacrifice correctness for theoretical optimization.

The existing Python implementation has already established a working behavioral baseline.

The purpose of this project is therefore **not to rediscover whether the model can run**.

That has already been proven.

The purpose is to build a robust, maintainable, native **.NET 10 inference platform** around that proven architecture and expose it as a practical local OpenAI-compatible service.

The resulting application should feel to clients like a normal OpenAI-compatible local server, while internally providing:

```text
.NET 10
+
ONNX Runtime
+
CUDA
+
Multi-GPU partitioning
+
persistent GPU state
+
real prefill
+
persistent decode
+
streaming
+
benchmarking
+
diagnostics
```

with Python reduced to an optional environment/tooling/reference component rather than the primary serving layer.
