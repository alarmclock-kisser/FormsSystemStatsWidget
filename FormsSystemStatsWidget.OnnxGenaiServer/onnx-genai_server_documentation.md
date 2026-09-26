# FormsSystemStatsWidget ONNX GenAI Server - Comprehensive Documentation

## Overview

The **FormsSystemStatsWidget ONNX GenAI Server** is an OpenAI-compatible inference server built on **ONNX Runtime (DirectML)** and **Microsoft.ML.OnnxRuntime.GenerativeAI**. It serves as a C# counterpart to `llama-server` (GGUF) for ONNX-converted models, enabling efficient LLM inference directly from .NET applications.

This documentation provides a detailed overview of the current implementation, model handling, API endpoints, and deployment instructions - perfect for sharing in online chats or documentation.

---

## Model Layout

Models are stored in a root directory, with each subdirectory (one level deep, not recursive) representing a separate model. Each model directory must contain at minimum:

- `model.onnx` - The ONNX model file
- `model.onnx.data` - Associated data file
- `config.json` - Model configuration
- `tokenizer.json` - Tokenizer file
- Any additional JSON files

### Example Directory Structure

```
D:\Models\ONNX\                  <- ModelRootDirectory
├── gemma-4-eb4-it-ONNX\        <- Modell-Rootdir (1 Ebene, nicht rekursiv)
│   ├── model.onnx
│   ├── model.onnx.data
│   ├── config.json
│   ├── tokenizer.json
│   └── ...
└── Qwen3.8-27B-onnx-int4\      <- Modell-Rootdir
    ├── model.onnx
    ├── model.onnx.data
    ├── config.json
    ├── tokenizer.json
    └── ...
```

**Key Point**: Only subdirectories directly under `ModelRootDirectory` that contain `*.onnx` + at least one `*.json` file are registered as models.

---

## API Routes (OpenAI-kompatibel)

The server exposes a full OpenAI-compatible API:

| Methode | Pfad                 | Beschreibung                          |
|---------|----------------------|---------------------------------------|
| GET     | `/v1/models`         | Listet alle verfügbaren Modelle       |
| POST    | `/v1/chat/completions` | Chat-Completion (streaming + non-streaming) |
| POST    | `/v1/completions`    | Legacy Completion (streaming + non-streaming) |
| POST    | `/v1/embeddings`     | Embeddings (falls Modell unterstützt) |
| GET     | `/health`            | Health-Check + Engine-Status          |

### Model Discovery

The engine automatically discovers models by enumerating subdirectories in `ModelRootDirectory`. Each valid model directory is registered with its folder name as the model ID.

---

## Configuration

### appsettings.json

Configuration is read from `appsettings.json` under the `OnnxGenaiServer` section, or via environment variables (`ONNXGENAI_SERVER__*`).

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "OnnxGenaiServer": {
    "ListenUrls": [ "http://localhost:8080" ],
    "ModelRootDirectory": "D:\\Models\\ONNX",
    "DefaultModel": "",
    "ExecutionProvider": "Dml",
    "ContextLength": 4096,
    "MaxConcurrentGenerations": 1,
    "Temperature": 0.8,
    "TopP": 0.9,
    "TopK": 40,
    "MaxTokens": 1024,
    "RepeatPenalty": 1.1,
    "SystemPrompt": null,
    "ModelId": "fssw-onnx-genai"
  }
}
```

### Key Configuration Options

| Einstellung | Standardwert | Beschreibung |
|-------------|--------------|-------------|
| `ListenUrls` | `http://localhost:8080` | Kestrel-Listen-URLs |
| `ModelRootDirectory` | `D:\Models\ONNX` | Root-Ordner für alle ONNX-Modelle |
| `DefaultModel` | `""` | Name des Modells, das beim Start geladen wird (leer = erstes gefundenes) |
| `ExecutionProvider` | `Dml` | ONNX Runtime Execution Provider ("Dml" oder "Cuda") |
| `ContextLength` | `4096` | Kontextlänge (n_ctx) für KV-Cache-Allokation |
| `MaxConcurrentGenerations` | `1` | Max. Anzahl paralleler Generationen (Sessions) |
| `Temperature` | `0.8` | Standard-Temperatur für Generation |
| `TopP` | `0.9` | Top-p Sampling |
| `TopK` | `40` | Top-k Sampling |
| `MaxTokens` | `1024` | Maximale Tokens pro Generation |
| `RepeatPenalty` | `1.1` | Wiederholungsstrafe |
| `ModelId` | `fssw-onnx-genai` | Standard-Modell-ID für API-Antworten |

### Environment Variables

Configuration can also be overridden via environment variables using the prefix `ONNXGENAI_SERVER__`:

- `ONNXGENAI_SERVER__ListenUrls`
- `ONNXGENAI_SERVER__ModelRootDirectory`
- `ONNXGENAI_SERVER__DefaultModel`
- `ONNXGENAI_SERVER__ExecutionProvider`
- `ONNXGENAI_SERVER__ContextLength`
- etc.

---

## Engine Architecture

### OnnxGenaiEngine

The core engine class (`OnnxGenaiEngine`) handles:

1. **Model Discovery**: Scans `ModelRootDirectory` for valid model folders
2. **Model Loading**: Initializes the selected ONNX model
3. **Generation**: Handles both streaming and non-streaming text generation
4. **Health Monitoring**: Provides status endpoints

The engine communicates with a Python inference server via HTTP, sending generation requests and receiving streamed responses.

### Generation Flow

1. User sends chat/completion request to API
2. Engine builds prompt from request messages
3. Parameters are constructed from request settings
4. Request is forwarded to Python server at `_pythonServerBaseUrl`
5. Python server performs inference using ONNX Runtime
6. Results are streamed back to the client

---

## Python Inference Server

The system includes a Python-based inference server (`run_onnx_genai_server.py`) that can either:

- **Primary**: Forward requests to the .NET host, which then communicates with ONNX Runtime
- **Fallback**: Run ONNX inference directly in Python (for testing/debugging)

### Running the Python Server

```bash
python run_onnx_genai_server.py --model PATH --context-length N --max-tokens N
       --temperature F --top-p F --top-k N --repeat-penalty F --execution-provider EP
```

### Python Server Arguments

| Argument | Standardwert | Beschreibung |
|----------|--------------|-------------|
| `--model` | Required | Pfad zur .onnx Modelldatei |
| `--context-length` | `4096` | Context length (n_ctx) |
| `--max-tokens` | `1024` | Max tokens to generate |
| `--temperature` | `0.8` | Temperature für Sampling |
| `--top-p` | `0.9` | Top-p Sampling |
| `--top-k` | `40` | Top-k Sampling |
| `--repeat-penalty` | `1.1` | Repeat penalty |
| `--execution-provider` | `Dml` | EP: Dml, Cuda, CPU |
| `--port` | `8080` | Server port |
| `--host` | `localhost` | Server host |

---

## Model Splitting & Dual GPU Setup

### Current Implementation

The current implementation supports running a single ONNX model with a specified execution provider (DML for DirectML/CUDA). For running **two engines in parallel on two GPUs**, the following approach is recommended:

#### Option 1: Multiple Server Instances

Run two separate ONNX GenAI Server instances, each configured with different models or model partitions:

```bash
# Instance 1 - Model A on GPU 0
dotnet run --project FormsSystemStatsWidget.OnnxGenaiServer \
  --OnnxGenaiServer:ModelRootDirectory=D:\Models\ONNX\Qwen3.8-27B-onnx-int4 \
  --OnnxGenaiServer:ExecutionProvider=Dml \
  --OnnxGenaiServer:ListenUrls=http://localhost:8081

# Instance 2 - Model B on GPU 1  
dotnet run --project FormsSystemStatsWidget.OnnxGenaiServer \
  --OnnxGenaiServer:ModelRootDirectory=D:\Models\ONNX\OtherModel-onnx-int4 \
  --OnnxGenaiServer:ExecutionProvider=Dml \
  --OnnxGenaiServer:ListenUrls=http://localhost:8082
```

#### Option 2: Model Partitioning

Split a large model across two GPUs using ONNX Runtime's multi-GPU support. This requires:

1. **Model Splitting**: Divide the Qwen model into two parts (layers 0-N and N-end)
2. **Separate ONNX Files**: Create `model_gpu0.onnx` and `model_gpu1.onnx`
3. **Coordinated Loading**: Both engines must coordinate token generation

#### Option 3: HuggingFace Transformers + ONNX

For the Qwen 3.8 27B model split across two GPUs:

1. **Original Model**: Qwen3-8B or Qwen3-27B from HuggingFace
2. **ONNX Conversion**: Convert to ONNX with int4 quantization
3. **Model Splitting**: Use `optimum` or custom scripts to split:
   ```python
   from optimum.onnxruntime import ORTModelForCausalLM
   # Split model layers across GPUs
   ```

### Qwen Model Splitting Workflow

Based on your success splitting Qwen with ONNX and running 2 engines parallel on 2 GPUs:

1. **Start with HuggingFace model**: `Qwen/Qwen3-27B`
2. **Quantize to INT4**: Use `quanto` or `optimum` for ONNX int4 quantization
3. **Split the model**: 
   - First half → GPU 0 (layers 0-27 for 27B model)
   - Second half → GPU 1 (layers 28-54 for 27B model)
4. **Create separate ONNX files**: `qwen-27b-gpu0.onnx`, `qwen-27b-gpu1.onnx`
5. **Configure two server instances**: Each pointing to its respective model partition
6. **Coordinate inference**: Send prompt to both engines, combine results

### Configuration for Dual GPU

```json
{
  "OnnxGenaiServer": {
    "ExecutionProvider": "Dml",
    "ModelRootDirectory": "D:\\Models\\ONNX",
    "DefaultModel": ""  // Will use first discovered, or specify via env
  }
}
```

Then start two instances:
```bash
# Terminal 1
dotnet run --project FormsSystemStatsWidget.OnnxGenaiServer -- 
  --OnnxGenaiServer:ModelRootDirectory=D:\Models\ONNX\Qwen3.8-27B-onnx-int4-gpu0 \
  --OnnxGenaiServer:ExecutionProvider=Dml \
  --OnnxGenaiServer:ListenUrls=http://localhost:8080

# Terminal 2
dotnet run --project FormsSystemStatsWidget.OnnxGenaiServer -- 
  --OnnxGenaiServer:ModelRootDirectory=D:\Models\ONNX\Qwen3.8-27B-onnx-int4-gpu1 \
  --OnnxGenaiServer:ExecutionProvider=Dml \
  --OnnxGenaiServer:ListenUrls=http://localhost:8081
```

Each instance will discover and load its respective model partition.

---

## Startup & Deployment

### Running the Server

```bash
dotnet run --project FormsSystemStatsWidget.OnnxGenaiServer
```

Or with custom configuration:
```bash
dotnet run --project FormsSystemStatsWidget.OnnxGenaiServer \
  -- OnnxGenaiServer:ModelRootDirectory=D:\Models\ONNX \
  -- OnnxGenaiServer:DefaultModel=Qwen3.8-27B-onnx-int4 \
  -- OnnxGenaiServer:ExecutionProvider=Dml
```

### Python Environment Setup

Before running, ensure Python environment is set up:

```bash
python Scripts/setup_onnx_env.py
```

This checks Python version and installs required packages:
- onnxruntime >= 1.20.0
- onnx >= 1.16.0
- safetensors
- transformers >= 4.40.0
- huggingface-hub >= 0.24.0
- numpy >= 1.26.0
- tokenizers >= 0.19.0
- accelerate >= 0.30.0

### Python Server Runner

```bash
python run_onnx_genai_server.py --model PATH --context-length 4096 --max-tokens 1024
       --temperature 0.8 --top-p 0.9 --top-k 40 --repeat-penalty 1.1 --execution-provider Dml
```

---

## Health & Monitoring

### Health Endpoint

```
GET /health
```

Returns:
```json
{
  "status": "ok",
  "engineReady": true,
  "models": ["Qwen3.8-27B-onnx-int4", "gemma-4-eb4-it-ONNX"],
  "uptime": "00:05:32"
}
```

### Models Endpoint

```
GET /v1/models
```

Lists all discovered models with metadata.

### Engine Status

The engine tracks:
- `IsReady`: Whether the engine has successfully initialized
- `LoadedModelId`: The currently loaded model
- `LastError`: Any initialization errors
- `StartTimeUtc`: When the engine was started

---

## Troubleshooting

### Common Issues

1. **Model not found**: Ensure `ModelRootDirectory` exists and contains valid model subdirectories with `*.onnx` + `*.json` files

2. **Engine not ready**: Check `LastError` via `/health` endpoint. Common causes:
   - Missing ONNX files in model directory
   - Invalid execution provider configuration
   - Insufficient GPU memory

3. **Python server not reachable**: Verify `_pythonServerBaseUrl` matches the actual Python server URL

4. **Performance issues**: 
   - Increase `ContextLength` if KV cache is too small
   - Adjust `MaxConcurrentGenerations` based on GPU memory
   - Use appropriate `ExecutionProvider` (Dml for DirectML, Cuda for NVIDIA)

5. **Dual GPU setup not working**: 
   - Verify both instances are running on different ports
   - Ensure each model partition has its own tokenizer.json
   - Check that ONNX files are compatible with the split

### Debug Logging

Enable detailed logging by checking the console output or configuring `appsettings.json` log levels.

---

## API Usage Examples

### Chat Completion (Streaming)

```bash
curl -N -X POST "http://localhost:8080/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen3.8-27B-onnx-int4",
    "messages": [{"role": "user", "content": "Hello, how are you?"}],
    "temperature": 0.7,
    "top_p": 0.9,
    "stream": true
  }'
```

### Chat Completion (Non-streaming)

```bash
curl -X POST "http://localhost:8080/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen3.8-27B-onnx-int4",
    "messages": [{"role": "user", "content": "Hello, how are you?"}],
    "temperature": 0.7
  }'
```

### Check Available Models

```bash
curl "http://localhost:8080/v1/models"
```

### Health Check

```bash
curl "http://localhost:8080/health"
```

---

## Development & Extension

### Adding New Features

The project is structured with clear separation of concerns:

- **Engine** (`Engine/OnnxGenaiEngine.cs`): Core engine logic, model discovery, generation
- **API** (`OpenAi/OpenAiApiHandler.cs`): OpenAI-compatible API endpoints
- **DTOs** (`OpenAi/OpenAiDtos.cs`): Request/response data transfer objects
- **Configuration** (`OnnxGenaiServerOptions.cs`: Server options configuration

### Customizing Generation Parameters

The `GenerationParameters` class controls inference behavior:

```csharp
public sealed class GenerationParameters
{
    public float Temperature { get; init; } = 0.8f;
    public float TopP { get; init; } = 0.9f;
    public int TopK { get; init; } = 40;
    public int MaxNewTokens { get; init; } = 1024;
    public float RepeatPenalty { get; init; } = 1.1f;
}
```

### Adding New API Endpoints

New endpoints can be added in `Program.cs` using the minimal API pattern:

```csharp
app.MapPost("/v1/your-endpoint", async (HttpContext ctx, OnnxGenaiEngine engine, CancellationToken ct) => {
    // Custom implementation
});
```

---

## License & Links

- **Package URL**: https://github.com/alarmclock-kisser/FormsSystemStatsWidget
- **Repository**: https://github.com/alarmclock-kisser/FormsSystemStatsWidget
- **Description**: OpenAI-kompatibler ONNX GenAI Server (DirectML) als Pendant zu llama-server (GGUF)
- **Tags**: onnx; genai; directml; openai; inference; server;

---

## Quick Reference Cheat Sheet

| Action | Command |
|--------|---------|
| Start server | `dotnet run --project FormsSystemStatsWidget.OnnxGenaiServer` |
| Check models | `curl http://localhost:8080/v1/models` |
| Health check | `curl http://localhost:8080/health` |
| Chat completion (streaming) | `curl -N -X POST "http://localhost:8080/v1/chat/completions" -d '{"messages":[{"role":"user","content":"test"}]}'` |
| Python env setup | `python Scripts/setup_onnx_env.py` |
| Python server runner | `python run_onnx_genai_server.py --model PATH --execution-provider Dml` |

---

*Documentation generated on 2026-09-25 for FormsSystemStatsWidget ONNX GenAI Server*