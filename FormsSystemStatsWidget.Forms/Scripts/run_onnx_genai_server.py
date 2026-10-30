#!/usr/bin/env python3
"""
FSSW ONNX GenAI Server Runner
==============================
Startet den ONNX-Genai-Server (dotnet) mit den übergebenen Parametern.
Alternativ: direkt ONNX Runtime Inference in Python (Fallback).

Aufruf:
    python run_onnx_genai_server.py --model PATH --context-length N --max-tokens N
           --temperature F --top-p F --top-k N --repeat-penalty F --execution-provider EP
"""

import argparse
import subprocess
import sys
import os
import json
import time
import threading
import http.server
import socketserver
from typing import Optional


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="FSSW ONNX GenAI Server Runner")
    parser.add_argument("--model", type=str, required=True,
                        help="Path to the .onnx model file")
    parser.add_argument("--context-length", type=int, default=4096,
                        help="Context length (default: 4096)")
    parser.add_argument("--max-tokens", type=int, default=1024,
                        help="Max tokens to generate (default: 1024)")
    parser.add_argument("--temperature", type=float, default=0.8,
                        help="Temperature (default: 0.8)")
    parser.add_argument("--top-p", type=float, default=0.9,
                        help="Top-p (default: 0.9)")
    parser.add_argument("--top-k", type=int, default=40,
                        help="Top-k (default: 40)")
    parser.add_argument("--repeat-penalty", type=float, default=1.1,
                        help="Repeat penalty (default: 1.1)")
    parser.add_argument("--execution-provider", type=str, default="Dml",
                        choices=["Dml", "Cuda", "CPU"],
                        help="ONNX Runtime execution provider (default: Dml)")
    parser.add_argument("--port", type=int, default=8080,
                        help="Server port (default: 8080)")
    parser.add_argument("--host", type=str, default="localhost",
                        help="Server host (default: localhost)")
    return parser.parse_args()


def find_dotnet_project() -> Optional[str]:
    """Findet das OnnxGenaiServer-Projekt relativ zum Skript."""
    script_dir = os.path.dirname(os.path.abspath(__file__))
    # Skript liegt in FormsSystemStatsWidget.OnnxGenaiServer/Scripts/
    project_dir = os.path.dirname(script_dir)
    csproj = os.path.join(project_dir, "FormsSystemStatsWidget.OnnxGenaiServer.csproj")
    if os.path.exists(csproj):
        return csproj
    return None


def start_dotnet_server(args: argparse.Namespace) -> int:
    """Startet den dotnet ONNX-Genai-Server."""
    csproj = find_dotnet_project()
    if csproj is None:
        print("[ERROR] Could not find FormsSystemStatsWidget.OnnxGenaiServer.csproj")
        print(f"  Searched: {os.path.dirname(os.path.abspath(__file__))}")
        return 1

    model_root = os.path.dirname(os.path.dirname(args.model))
    model_name = os.path.basename(model_root)

    cmd = [
        "dotnet", "run", "--project", csproj,
        f"--OnnxGenaiServer:ModelRootDirectory={model_root}",
        f"--OnnxGenaiServer:DefaultModel={model_name}",
        f"--OnnxGenaiServer:ContextLength={args.context_length}",
        f"--OnnxGenaiServer:MaxTokens={args.max_tokens}",
        f"--OnnxGenaiServer:Temperature={args.temperature}",
        f"--OnnxGenaiServer:TopP={args.top_p}",
        f"--OnnxGenaiServer:TopK={args.top_k}",
        f"--OnnxGenaiServer:RepeatPenalty={args.repeat_penalty}",
        f"--OnnxGenaiServer:ExecutionProvider={args.execution_provider}",
        f"--OnnxGenaiServer:ListenUrls[0]=http://{args.host}:{args.port}"
    ]

    print(f"[INFO] Starting ONNX-Genai Server (dotnet)")
    print(f"  Model: {args.model}")
    print(f"  Model Root: {model_root}")
    print(f"  Model Name: {model_name}")
    print(f"  EP: {args.execution_provider}")
    print(f"  Port: {args.port}")
    print(f"  Command: {' '.join(cmd)}")
    print()

    try:
        result = subprocess.run(cmd)
        return result.returncode
    except FileNotFoundError:
        print("[ERROR] 'dotnet' not found in PATH. Please install .NET SDK.")
        return 1
    except KeyboardInterrupt:
        print("\n[INFO] Server stopped by user.")
        return 0


def start_python_inference_server(args: argparse.Namespace) -> int:
    """
    Fallback: Python-basierter Inference-Server mit ONNX Runtime.
    Bietet eine minimale OpenAI-kompatible API.
    """
    try:
        import onnxruntime as ort
    except ImportError:
        print("[ERROR] onnxruntime not installed. Run: pip install onnxruntime")
        return 1

    if args.execution_provider == "Cuda" and hasattr(ort, "preload_dlls"):
        try:
            ort.preload_dlls()
            print("[INFO] Preloaded CUDA/cuDNN runtime DLLs for ONNX Runtime.")
        except Exception as ex:
            print(f"[WARN] Could not preload CUDA/cuDNN runtime DLLs: {ex}")

    try:
        import numpy as np
    except ImportError:
        print("[ERROR] numpy not installed. Run: pip install numpy")
        return 1

    # Execution Provider konfigurieren (Multi-GPU: ein CUDA-Provider pro GPU)
    ep_map = {
        "Dml": ["DmlExecutionProvider", "CPUExecutionProvider"],
        "Cuda": ["CUDAExecutionProvider", "CPUExecutionProvider"],
        "CPU": ["CPUExecutionProvider"],
    }
    providers = ep_map.get(args.execution_provider, ["CPUExecutionProvider"])

    # Verfügbare Provider prüfen und ggf. Multi-GPU-Provider-Liste bauen
    available = ort.get_available_providers()
    print(f"[INFO] Available ONNX Runtime providers: {available}")

    if providers[0] not in available:
        print(f"[WARN] Requested provider '{providers[0]}' is NOT available in this onnxruntime build.")
        print(f"[WARN] Falling back to: {available}")
        if args.execution_provider == "Cuda":
            print("[WARN] Hint: install the GPU build:  pip install onnxruntime-gpu")
        providers = [p for p in available if p != "AzureExecutionProvider"] or ["CPUExecutionProvider"]

    if providers[0] == "CUDAExecutionProvider":
        # Multi-GPU: eine CUDAExecutionProvider-Instanz pro GPU (device_id 0..n)
        try:
            import importlib.util
            has_pynvml = importlib.util.find_spec("pynvml") is not None
        except Exception:
            has_pynvml = False
        
        if has_pynvml:
            try:
                import pynvml
                pynvml.nvmlInit()
                gpu_count = pynvml.nvmlDeviceGetCount()
                pynvml.nvmlShutdown()
            except Exception as ex:
                gpu_count = 1
                print(f"[WARN] Could not query NVIDIA GPUs through nvidia-ml-py: {ex}")
            if gpu_count > 1:
                print(
                    f"[WARN] Detected {gpu_count} NVIDIA GPUs, but ONNX Runtime does not shard one "
                    "InferenceSession across duplicate CUDA providers. Using CUDA device 0 only."
                )
            providers = [("CUDAExecutionProvider", {"device_id": "0"}), "CPUExecutionProvider"]
        else:
            # Fallback: assume single GPU if pynvml not available
            gpu_count = 1
            print("[INFO] Single GPU mode (pynvml not available for multi-GPU detection).")

    print(f"[INFO] Loading ONNX model: {args.model}")
    print(f"  Providers: {providers}")

    sess_options = ort.SessionOptions()
    sess_options.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL

    session = ort.InferenceSession(args.model, sess_options=sess_options, providers=providers)
    # onnxruntime >= 1.17: 'inputs'/'outputs' sind keine Attribute mehr, sondern Methoden
    input_names = [i.name for i in session.get_inputs()]
    output_names = [o.name for o in session.get_outputs()]
    print(f"[INFO] Model loaded. Inputs: {input_names}")
    print(f"[INFO] Model loaded. Outputs: {output_names}")

    # Tokenizer laden (aus dem Modell-Rootdir)
    model_dir = os.path.dirname(args.model)
    tokenizer_path = os.path.join(model_dir, "tokenizer.json")
    tokenizer = None
    if os.path.exists(tokenizer_path):
        try:
            from tokenizers import Tokenizer
            tokenizer = Tokenizer.from_file(tokenizer_path)
            print(f"[INFO] Tokenizer loaded: {tokenizer_path}")
        except Exception as ex:
            print(f"[WARN] Could not load tokenizer: {ex}")
    else:
        print(f"[WARN] No tokenizer.json found in {model_dir}")

    # Einfache HTTP-Server-Klasse für OpenAI-kompatible API
    class OnnxGenaiHandler(http.server.BaseHTTPRequestHandler):
        def log_message(self, format, *args):
            print(f"[HTTP] {format % args}")

        def _send_json(self, data: dict, status: int = 200):
            body = json.dumps(data).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def _send_sse(self, data: dict):
            body = f"data: {json.dumps(data)}\n\n".encode()
            self.wfile.write(body)
            self.wfile.flush()

        def do_GET(self):
            if self.path == "/health":
                self._send_json({"status": "ok", "model": os.path.basename(args.model)})
            elif self.path == "/v1/models":
                self._send_json({
                    "object": "list",
                    "data": [{
                        "id": os.path.basename(model_dir),
                        "object": "model",
                        "created": int(time.time()),
                        "owned_by": "fssw"
                    }]
                })
            else:
                self._send_json({"error": {"message": "Not found", "type": "invalid_request_error"}}, 404)

        def do_POST(self):
            content_length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(content_length).decode()

            try:
                request = json.loads(body)
            except json.JSONDecodeError:
                self._send_json({"error": {"message": "Invalid JSON", "type": "invalid_request_error"}}, 400)
                return

            if self.path == "/v1/chat/completions":
                self._handle_chat_completions(request)
            elif self.path == "/v1/completions":
                self._handle_completions(request)
            elif self.path == "/v1/embeddings":
                self._send_json({"error": {"message": "Embeddings not supported in Python fallback", "type": "server_error"}}, 501)
            else:
                self._send_json({"error": {"message": "Not found", "type": "invalid_request_error"}}, 404)

        def _handle_chat_completions(self, request: dict):
            messages = request.get("messages", [])
            prompt = "\n".join(f"[{m.get('role', 'user')}]: {m.get('content', '')}" for m in messages)

            if tokenizer is None:
                self._send_json({"error": {"message": "No tokenizer available", "type": "server_error"}}, 500)
                return

            input_ids = tokenizer.encode(prompt).ids
            prompt_tokens = len(input_ids)

            stream = request.get("stream", False)
            model_id = request.get("model", os.path.basename(model_dir))
            completion_id = f"chatcmpl-{int(time.time())}"
            created = int(time.time())

            if stream:
                self.send_response(200)
                self.send_header("Content-Type", "text/event-stream")
                self.end_headers()

                # Einfache Generation (ohne KV-Cache, nur für Testzwecke)
                # In Produktion: dotnet-Server verwenden
                self._send_sse({
                    "id": completion_id, "object": "chat.completion.chunk",
                    "created": created, "model": model_id,
                    "choices": [{"index": 0, "delta": {"role": "assistant", "content": "[Python fallback: use dotnet server for full inference]"}, "finish_reason": None}]
                })
                self._send_sse({
                    "id": completion_id, "object": "chat.completion.chunk",
                    "created": created, "model": model_id,
                    "choices": [{"index": 0, "delta": {}, "finish_reason": "stop"}]
                })
                self.wfile.write(b"data: [DONE]\n\n")
                self.wfile.flush()
            else:
                # Non-streaming: einfache Antwort
                self._send_json({
                    "id": completion_id, "object": "chat.completion",
                    "created": created, "model": model_id,
                    "choices": [{"index": 0, "message": {"role": "assistant", "content": "[Python fallback: use dotnet server for full inference]"}, "finish_reason": "stop"}],
                    "usage": {"prompt_tokens": prompt_tokens, "completion_tokens": 1, "total_tokens": prompt_tokens + 1}
                })

        def _handle_completions(self, request: dict):
            prompt = request.get("prompt", "")
            model_id = request.get("model", os.path.basename(model_dir))
            completion_id = f"cmpl-{int(time.time())}"
            created = int(time.time())

            if tokenizer is None:
                self._send_json({"error": {"message": "No tokenizer available", "type": "server_error"}}, 500)
                return

            input_ids = tokenizer.encode(prompt).ids
            prompt_tokens = len(input_ids)

            stream = request.get("stream", False)

            if stream:
                self.send_response(200)
                self.send_header("Content-Type", "text/event-stream")
                self.end_headers()
                self._send_sse({
                    "id": completion_id, "object": "text_completion",
                    "created": created, "model": model_id,
                    "choices": [{"index": 0, "text": "[Python fallback: use dotnet server for full inference]", "finish_reason": None}]
                })
                self._send_sse({
                    "id": completion_id, "object": "text_completion",
                    "created": created, "model": model_id,
                    "choices": [{"index": 0, "text": "", "finish_reason": "stop"}]
                })
                self.wfile.write(b"data: [DONE]\n\n")
                self.wfile.flush()
            else:
                self._send_json({
                    "id": completion_id, "object": "text_completion",
                    "created": created, "model": model_id,
                    "choices": [{"index": 0, "text": "[Python fallback: use dotnet server for full inference]", "finish_reason": "stop"}],
                    "usage": {"prompt_tokens": prompt_tokens, "completion_tokens": 1, "total_tokens": prompt_tokens + 1}
                })

    print(f"[INFO] Starting Python ONNX-Genai Server on http://{args.host}:{args.port}")
    print(f"  NOTE: This is a fallback. For full inference, use the dotnet server.")

    with socketserver.TCPServer(("" if args.host == "localhost" else args.host, args.port), OnnxGenaiHandler) as httpd:
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\n[INFO] Server stopped by user.")

    return 0


def main() -> int:
    args = parse_args()

    print("=" * 60)
    print("  FSSW ONNX GenAI Server Runner")
    print("=" * 60)
    print(f"  Model: {args.model}")
    print(f"  EP: {args.execution_provider}")
    print(f"  Port: {args.port}")
    print()

    # Primär: dotnet-Server starten
    csproj = find_dotnet_project()
    if csproj is not None:
        return start_dotnet_server(args)
    else:
        print("[WARN] dotnet project not found. Falling back to Python inference server.")
        print()
        return start_python_inference_server(args)


if __name__ == "__main__":
    sys.exit(main())
