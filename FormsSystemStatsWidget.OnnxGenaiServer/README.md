# FSSW ONNX GenAI Server

OpenAI-kompatibler Inference-Server auf Basis von **ONNX Runtime (DirectML)** und
**Microsoft.ML.OnnxRuntime.GenerativeAI**. Pendant zu `llama-server` (GGUF) für
ONNX-konvertierte Modelle (z. B. CUDA-int4-Quantisierung via safetensors → ONNX).

## Modell-Layout

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

Nur Subdirs, die **direkt** `.onnx` + mindestens ein `.json` enthalten, werden
als Modell gelistet.

## API-Routen (OpenAI-kompatibel)

| Methode | Pfad                 | Beschreibung                          |
|---------|----------------------|---------------------------------------|
| GET     | `/v1/models`         | Listet alle verfügbaren Modelle       |
| POST    | `/v1/chat/completions` | Chat-Completion (streaming + non)   |
| POST    | `/v1/completions`    | Legacy Completion (streaming + non)   |
| POST    | `/v1/embeddings`     | Embeddings (falls Modell unterstützt) |
| GET     | `/health`            | Health-Check + Engine-Status          |

## Start

```bash
dotnet run --project FormsSystemStatsWidget.OnnxGenaiServer
```

Konfiguration in `appsettings.json` (Section `OnnxGenaiServer`) oder via
Umgebungsvariablen (`ONNXGENAI_SERVER__MODELROOTDIRECTORY` etc.).

## Python-Setup (für ONNX-Konvertierung)

```bash
python Scripts/setup_onnx_env.py
```

Checkt Python-Version, installiert fehlende Pakete (onnxruntime, onnx,
safetensors, transformers, huggingface-hub, numpy).
