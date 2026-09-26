"""
Thin local HTTP/SSE transport server for the ONNX Python inference engine.

This module provides the IPC endpoints expected by the C# PythonIpcClient:
  POST /load      — load model package
  POST /unload    — unload model
  POST /generate  — generate (SSE streaming, OpenAI-compatible chunks)
  GET  /health    — health check
  POST /shutdown  — graceful shutdown

The OpenAI API mapping and application layer remain in C#.
This server is a local-only transport, not an OpenAI API server.
"""

from __future__ import annotations

import argparse
import json
import logging
import sys
import threading
import time
import uuid
from typing import Any

from flask import Flask, Response, jsonify, request

from .engine import InferenceEngine
from .errors import OnnxEngineError
from .generation import GenerationRequest, SamplingConfig, StopConfig

logger = logging.getLogger("onnx_engine.server")

app = Flask(__name__)

_engine: InferenceEngine | None = None
_engine_lock = threading.Lock()
_shutdown_event = threading.Event()


def _get_engine() -> InferenceEngine:
    if _engine is None:
        raise OnnxEngineError("ENGINE_NOT_READY")
    return _engine


def _error_response(code: str, message: str, status: int = 500) -> Response:
    return jsonify({"error": {"code": code, "message": message}}), status


def _parse_generation_request(data: dict[str, Any]) -> GenerationRequest:
    """
    Parse the C# IPC payload into a GenerationRequest.

    C# sends: {prompt, temperature, top_p, top_k, max_tokens, repeat_penalty, stream}
    Python expects: GenerationRequest(messages, sampling, stopping, ...)
    """
    prompt = data.get("prompt", "")
    messages = [{"role": "user", "content": prompt}]

    sampling = SamplingConfig(
        temperature=float(data.get("temperature", 0.6)),
        top_k=int(data.get("top_k", 20)),
        top_p=float(data.get("top_p", 0.9)),
        repetition_penalty=float(data.get("repeat_penalty", 1.0)),
    )

    stopping = StopConfig(
        max_new_tokens=int(data.get("max_tokens", 256)),
    )

    return GenerationRequest(
        messages=tuple(messages),
        sampling=sampling,
        stopping=stopping,
        add_generation_prompt=True,
    )


def _sse_chunk(
    request_id: str,
    model_id: str,
    text: str,
    finish_reason: str | None,
    prompt_tokens: int,
    completion_tokens: int,
) -> str:
    """Build an OpenAI-compatible SSE chunk."""
    chunk: dict[str, Any] = {
        "id": request_id,
        "object": "chat.completion.chunk",
        "created": int(time.time()),
        "model": model_id,
        "choices": [
            {
                "index": 0,
                "delta": {"content": text} if text else {},
                "finish_reason": finish_reason,
            }
        ],
    }
    if finish_reason is not None:
        chunk["usage"] = {
            "prompt_tokens": prompt_tokens,
            "completion_tokens": completion_tokens,
            "total_tokens": prompt_tokens + completion_tokens,
        }
    return f"data: {json.dumps(chunk, ensure_ascii=False)}\n\n"


@app.route("/health", methods=["GET"])
def health() -> Response:
    if _engine is None:
        return jsonify({"status": "unloaded", "ready": False}), 200
    try:
        providers = _engine.active_providers()
        return jsonify({
            "status": "loaded",
            "ready": True,
            "providers": list(providers),
        }), 200
    except OnnxEngineError:
        return jsonify({"status": "unloaded", "ready": False}), 200


@app.route("/load", methods=["POST"])
def load() -> Response:
    global _engine
    data = request.get_json(silent=True) or {}
    model_path = data.get("model_path", "")
    if not model_path:
        return _error_response("MODEL_NOT_FOUND", "model_path is required", 400)

    with _engine_lock:
        if _engine is not None:
            try:
                _engine.unload()
            except Exception:
                pass
            _engine = None

        try:
            _engine = InferenceEngine(model_path)
            _engine.load()
            logger.info("Model loaded: %s", model_path)
            return jsonify({
                "status": "loaded",
                "model_path": model_path,
                "providers": list(_engine.active_providers()),
            }), 200
        except OnnxEngineError as ex:
            logger.error("Model load failed: %s", ex)
            _engine = None
            return _error_response("MODEL_LOAD_FAILED", str(ex), 500)
        except Exception as ex:
            logger.exception("Unexpected error loading model")
            _engine = None
            return _error_response("MODEL_LOAD_FAILED", str(ex), 500)


@app.route("/unload", methods=["POST"])
def unload() -> Response:
    global _engine
    with _engine_lock:
        if _engine is None:
            return jsonify({"status": "not_loaded"}), 200
        try:
            _engine.unload()
            logger.info("Model unloaded")
            return jsonify({"status": "unloaded"}), 200
        except Exception as ex:
            logger.exception("Error unloading model")
            return _error_response("UNLOAD_FAILED", str(ex), 500)
        finally:
            _engine = None


@app.route("/generate", methods=["POST"])
def generate() -> Response:
    engine = _get_engine()
    data = request.get_json(silent=True) or {}

    try:
        gen_request = _parse_generation_request(data)
    except Exception as ex:
        return _error_response("INVALID_REQUEST", str(ex), 400)

    request_id = f"chatcmpl-{uuid.uuid4().hex[:24]}"
    model_id = "onnx-engine"

    def stream() -> Any:
        prompt_tokens = 0
        completion_tokens = 0
        try:
            context, chunks = engine.generate(gen_request)
            prompt_tokens = context.prompt_token_count

            for chunk in chunks:
                if _shutdown_event.is_set():
                    break
                completion_tokens = chunk.generated_tokens
                yield _sse_chunk(
                    request_id, model_id,
                    chunk.text, None,
                    prompt_tokens, completion_tokens,
                )

            finish_reason = "stop"
            if _shutdown_event.is_set():
                finish_reason = "cancelled"
            elif chunk.finished:
                finish_reason = "stop"
            yield _sse_chunk(
                request_id, model_id, "",
                finish_reason,
                prompt_tokens, completion_tokens,
            )
            yield "data: [DONE]\n\n"
        except OnnxEngineError as ex:
            logger.error("Generation error: %s", ex)
            yield _sse_chunk(request_id, model_id, "", "error", 0, 0)
            yield "data: [DONE]\n\n"
        except Exception as ex:
            logger.exception("Unexpected generation error")
            yield _sse_chunk(request_id, model_id, "", "error", 0, 0)
            yield "data: [DONE]\n\n"

    return Response(
        stream(),
        mimetype="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",
        },
    )


@app.route("/shutdown", methods=["POST"])
def shutdown() -> Response:
    logger.info("Shutdown requested")
    _shutdown_event.set()
    global _engine
    with _engine_lock:
        if _engine is not None:
            try:
                _engine.unload()
            except Exception:
                pass
            _engine = None
    # Schedule process exit after response is sent
    threading.Thread(target=_exit_process, daemon=True).start()
    return jsonify({"status": "shutting_down"}), 200


def _exit_process() -> None:
    time.sleep(0.5)
    sys.exit(0)


def main() -> None:
    parser = argparse.ArgumentParser(description="ONNX Engine IPC Server")
    parser.add_argument("--port", type=int, default=8081, help="Port to listen on")
    parser.add_argument("--host", type=str, default="127.0.0.1", help="Host to bind to")
    parser.add_argument("--log-level", type=str, default="INFO", help="Logging level")
    args = parser.parse_args()

    logging.basicConfig(
        level=getattr(logging, args.log_level.upper(), logging.INFO),
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
        stream=sys.stderr,
    )

    logger.info("Starting ONNX Engine IPC Server on %s:%d", args.host, args.port)
    app.run(host=args.host, port=args.port, threaded=True, use_reloader=False)


if __name__ == "__main__":
    main()
