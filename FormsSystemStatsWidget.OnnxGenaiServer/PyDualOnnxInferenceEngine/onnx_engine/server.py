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
import gc
import json
import logging
import os
import sys
import threading
import time
import uuid
from datetime import datetime, timezone
from typing import Any, Callable

from flask import Flask, Response, jsonify, request, stream_with_context
from werkzeug.serving import BaseWSGIServer, make_server

from .engine import InferenceEngine
from .errors import OnnxEngineError
from .generation import GenerationRequest, SamplingConfig, StopConfig

logger = logging.getLogger("onnx_engine.server")

app = Flask(__name__)

_engine: InferenceEngine | None = None
_engine_lock = threading.Lock()
_shutdown_event = threading.Event()
_http_server: BaseWSGIServer | None = None
_instance_id = ""


class EngineLifecycle:
    """Tracks active operations and the unloaded-only idle shutdown deadline."""

    def __init__(
        self,
        *,
        enabled: bool = True,
        idle_seconds: int = 120,
        clock: Callable[[], float] = time.monotonic,
    ) -> None:
        self._enabled = enabled
        self._idle_seconds = max(1, idle_seconds)
        self._clock = clock
        self._condition = threading.Condition()
        self._operations: dict[str, int] = {}
        self._idle_since: float | None = self._clock()
        self._last_activity_utc = datetime.now(timezone.utc).isoformat()
        self._stopping = False

    def begin_operation(self, name: str) -> bool:
        with self._condition:
            if name in {"Loading", "Unloading"}:
                while self._operations and not self._stopping:
                    self._condition.wait()
            elif any(operation in {"Loading", "Unloading"} for operation in self._operations):
                return False
            if self._stopping:
                return False
            self._operations[name] = self._operations.get(name, 0) + 1
            self._idle_since = None
            self._last_activity_utc = datetime.now(timezone.utc).isoformat()
            return True

    def end_operation(self, name: str, *, loaded: bool) -> None:
        with self._condition:
            count = self._operations.get(name, 0)
            if count <= 1:
                self._operations.pop(name, None)
            else:
                self._operations[name] = count - 1
            self._last_activity_utc = datetime.now(timezone.utc).isoformat()
            if not self._stopping and not loaded and not self._operations:
                self._idle_since = self._clock()
            elif loaded:
                self._idle_since = None
            self._condition.notify_all()

    def begin_shutdown(self) -> bool:
        with self._condition:
            if self._stopping:
                return False
            self._stopping = True
            self._idle_since = None
            self._last_activity_utc = datetime.now(timezone.utc).isoformat()
            self._condition.notify_all()
            return True

    def should_auto_shutdown(self, *, loaded: bool) -> bool:
        with self._condition:
            if (
                not self._enabled
                or self._stopping
                or loaded
                or self._operations
                or self._idle_since is None
                or self._clock() - self._idle_since < self._idle_seconds
            ):
                return False
            self._stopping = True
            self._idle_since = None
            self._last_activity_utc = datetime.now(timezone.utc).isoformat()
            self._condition.notify_all()
            return True

    def wait_for_quiescence(self) -> None:
        with self._condition:
            while self._operations:
                self._condition.wait()

    def health_snapshot(self, *, loaded: bool) -> dict[str, Any]:
        with self._condition:
            active_operations = sum(self._operations.values())
            if self._stopping:
                state = "Stopping"
            elif self._operations:
                state = next(iter(self._operations))
            else:
                state = "Loaded" if loaded else "Unloaded"
            idle_seconds = (
                max(0, int(self._clock() - self._idle_since))
                if not loaded and not self._operations and self._idle_since is not None
                else 0
            )
            return {
                "state": state,
                "loaded": loaded,
                "active_operations": active_operations,
                "idle_seconds": idle_seconds,
                "last_activity_utc": self._last_activity_utc,
            }


_lifecycle = EngineLifecycle()


def _release_engine_locked() -> None:
    global _engine
    engine = _engine
    _engine = None
    if engine is None:
        return
    try:
        engine.unload()
    finally:
        del engine
        gc.collect()


def _stop_server_after_response() -> None:
    _shutdown_event.set()
    _lifecycle.wait_for_quiescence()
    with _engine_lock:
        try:
            _release_engine_locked()
        except Exception:
            logger.exception("[ONNX-LIFECYCLE] Error releasing engine during shutdown")
    server = _http_server
    if server is not None:
        server.shutdown()


def _schedule_server_shutdown(reason: str) -> None:
    logger.info("[ONNX-LIFECYCLE] Graceful process shutdown requested: %s", reason)
    threading.Thread(target=_stop_server_after_response, daemon=True).start()


def _idle_shutdown_monitor() -> None:
    while not _shutdown_event.wait(0.5):
        if _lifecycle.should_auto_shutdown(loaded=_engine is not None):
            logger.info("[ONNX-LIFECYCLE] Engine unloaded and idle timeout reached")
            _schedule_server_shutdown("idle timeout")
            return


def _get_engine() -> InferenceEngine:
    if _engine is None:
        raise OnnxEngineError("ENGINE_NOT_READY")
    return _engine


def _error_response(code: str, message: str, status: int = 500) -> Response:
    return jsonify({"error": {"code": code, "message": message}}), status


def _parse_generation_request(data: dict[str, Any]) -> GenerationRequest:
    """
    Parse the C# IPC payload into a GenerationRequest.

    Chat requests carry structured messages; completion requests may carry a prompt.
    """
    raw_messages = data.get("messages")
    if raw_messages is None:
        prompt = data.get("prompt", "")
        if not isinstance(prompt, str):
            raise ValueError("prompt must be a string")
        messages = [{"role": "user", "content": prompt}]
    else:
        if not isinstance(raw_messages, list) or not raw_messages:
            raise ValueError("messages must be a non-empty list")

        messages = []
        for index, message in enumerate(raw_messages):
            if not isinstance(message, dict):
                raise ValueError(f"messages[{index}] must be an object")
            role = message.get("role")
            content = message.get("content")
            if not isinstance(role, str) or not role.strip():
                raise ValueError(f"messages[{index}].role must be a non-empty string")
            if not isinstance(content, str):
                raise ValueError(f"messages[{index}].content must be a string")

            normalized = {"role": role, "content": content}
            name = message.get("name")
            if name is not None:
                if not isinstance(name, str):
                    raise ValueError(f"messages[{index}].name must be a string")
                normalized["name"] = name
            messages.append(normalized)

    sampling = SamplingConfig(
        temperature=float(data.get("temperature", 0.6)),
        top_k=int(data.get("top_k", 20)),
        top_p=float(data.get("top_p", 0.9)),
        typical_p=float(data.get("typical_p", 1.0)),
        min_p=float(data.get("min_p", 0.0)),
        repetition_penalty=float(data.get("repeat_penalty", 1.0)),
        frequency_penalty=float(data.get("frequency_penalty", 0.0)),
        presence_penalty=float(data.get("presence_penalty", 0.0)),
        seed=int(data["seed"]) if data.get("seed") is not None else None,
    )

    stopping = StopConfig(
        max_new_tokens=int(data.get("max_tokens", 256)),
    )

    return GenerationRequest(
        messages=tuple(messages),
        sampling=sampling,
        stopping=stopping,
        add_generation_prompt=True,
        template_kwargs={
            "enable_thinking": bool(data.get("enable_thinking", False)),
        } if raw_messages is not None else {},
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
    loaded = _engine is not None
    snapshot = _lifecycle.health_snapshot(loaded=loaded)
    providers: list[str] = []
    if _engine is not None:
        try:
            providers = list(_engine.active_providers())
        except OnnxEngineError:
            loaded = False
            snapshot = _lifecycle.health_snapshot(loaded=False)
    return jsonify({
        "status": "loaded" if loaded else "unloaded",
        "ready": loaded,
        "process_id": os.getpid(),
        "instance_id": _instance_id,
        **snapshot,
        "providers": providers,
    }), 200


@app.route("/load", methods=["POST"])
def load() -> Response:
    global _engine
    if not _lifecycle.begin_operation("Loading"):
        return _error_response("SHUTTING_DOWN", "Engine process is shutting down", 503)

    data = request.get_json(silent=True) or {}
    model_path = data.get("model_path", "")
    model_layout = data.get("model_layout", "auto")
    if not model_path:
        _lifecycle.end_operation("Loading", loaded=_engine is not None)
        return _error_response("MODEL_NOT_FOUND", "model_path is required", 400)
    if not isinstance(model_layout, str) or model_layout.strip().casefold() not in {"auto", "main", "partitioned"}:
        _lifecycle.end_operation("Loading", loaded=_engine is not None)
        return _error_response("INVALID_MODEL_LAYOUT", "model_layout must be auto, main, or partitioned", 400)

    try:
        with _engine_lock:
            if _engine is not None:
                _release_engine_locked()

            candidate: InferenceEngine | None = None
            try:
                candidate = InferenceEngine(model_path, model_layout=model_layout)
                candidate.load()
                _engine = candidate
                candidate = None
                logger.info("Model loaded: %s", model_path)
                return jsonify({
                    "status": "loaded",
                    "model_path": model_path,
                    "providers": list(_engine.active_providers()),
                }), 200
            except OnnxEngineError as ex:
                logger.error("Model load failed: %s", ex)
                return _error_response("MODEL_LOAD_FAILED", str(ex), 500)
            except Exception as ex:
                logger.exception("Unexpected error loading model")
                return _error_response("MODEL_LOAD_FAILED", str(ex), 500)
            finally:
                if candidate is not None:
                    try:
                        candidate.unload()
                    except Exception:
                        logger.exception("Error cleaning up incomplete model load")
                    del candidate
                    gc.collect()
    finally:
        _lifecycle.end_operation("Loading", loaded=_engine is not None)


@app.route("/unload", methods=["POST"])
def unload() -> Response:
    if not _lifecycle.begin_operation("Unloading"):
        return _error_response("SHUTTING_DOWN", "Engine process is shutting down", 503)
    try:
        with _engine_lock:
            if _engine is None:
                return jsonify({"status": "not_loaded"}), 200
            try:
                _release_engine_locked()
                logger.info("Model unloaded")
                return jsonify({"status": "unloaded"}), 200
            except Exception as ex:
                logger.exception("Error unloading model")
                return _error_response("UNLOAD_FAILED", str(ex), 500)
    finally:
        _lifecycle.end_operation("Unloading", loaded=_engine is not None)


@app.route("/generate", methods=["POST"])
def generate() -> Response:
    data = request.get_json(silent=True) or {}
    try:
        engine = _get_engine()
    except OnnxEngineError as ex:
        return _error_response("ENGINE_NOT_READY", str(ex), 503)

    try:
        gen_request = _parse_generation_request(data)
    except Exception as ex:
        return _error_response("INVALID_REQUEST", str(ex), 400)

    if not _lifecycle.begin_operation("Generating"):
        return _error_response("SHUTTING_DOWN", "Engine process is shutting down", 503)
    if _engine is not engine:
        _lifecycle.end_operation("Generating", loaded=_engine is not None)
        return _error_response("ENGINE_NOT_READY", "Model was unloaded before generation began", 503)

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
        finally:
            _lifecycle.end_operation("Generating", loaded=_engine is not None)

    return Response(
        stream_with_context(stream()),
        mimetype="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",
        },
    )


@app.route("/shutdown", methods=["POST"])
def shutdown() -> Response:
    if _lifecycle.begin_shutdown():
        _schedule_server_shutdown("HTTP /shutdown")
    return jsonify({"status": "shutting_down"}), 200


def _environment_bool(name: str, default: bool) -> bool:
    value = os.environ.get(name)
    if value is None:
        return default
    normalized = value.strip().lower()
    if normalized in {"1", "true", "yes", "on"}:
        return True
    if normalized in {"0", "false", "no", "off"}:
        return False
    logger.warning("Invalid %s=%r; using default %s", name, value, default)
    return default


def _environment_idle_seconds() -> int:
    value = os.environ.get("ONNX_ENGINE_IDLE_SHUTDOWN_SECONDS", "120")
    try:
        return max(1, int(value))
    except ValueError:
        logger.warning("Invalid ONNX_ENGINE_IDLE_SHUTDOWN_SECONDS=%r; using 120", value)
        return 120


def main() -> None:
    global _http_server, _instance_id, _lifecycle

    parser = argparse.ArgumentParser(description="ONNX Engine IPC Server")
    parser.add_argument("--port", type=int, default=8081, help="Port to listen on")
    parser.add_argument("--host", type=str, default="127.0.0.1", help="Host to bind to")
    parser.add_argument("--log-level", type=str, default="INFO", help="Logging level")
    parser.add_argument("--instance-id", type=str, default=None, help="C# supervisor process identity")
    args = parser.parse_args()

    logging.basicConfig(
        level=getattr(logging, args.log_level.upper(), logging.INFO),
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
        stream=sys.stderr,
    )

    environment_instance_id = os.environ.get("ONNX_ENGINE_INSTANCE_ID")
    if args.instance_id and environment_instance_id and args.instance_id != environment_instance_id:
        parser.error("--instance-id does not match ONNX_ENGINE_INSTANCE_ID")
    _instance_id = args.instance_id or environment_instance_id or str(uuid.uuid4())
    idle_enabled = _environment_bool("ONNX_ENGINE_IDLE_SHUTDOWN_ENABLED", True)
    idle_seconds = _environment_idle_seconds()
    _shutdown_event.clear()
    _lifecycle = EngineLifecycle(enabled=idle_enabled, idle_seconds=idle_seconds)

    logger.info("Starting ONNX Engine IPC Server on %s:%d", args.host, args.port)
    logger.info(
        "[ONNX-LIFECYCLE] Instance %s; idle shutdown %s (%d seconds)",
        _instance_id,
        "enabled" if idle_enabled else "disabled",
        idle_seconds,
    )
    _http_server = make_server(args.host, args.port, app, threaded=True)
    idle_monitor = threading.Thread(target=_idle_shutdown_monitor, daemon=True)
    idle_monitor.start()
    try:
        _http_server.serve_forever(poll_interval=0.5)
    except KeyboardInterrupt:
        logger.info("[ONNX-LIFECYCLE] Keyboard interrupt received")
    finally:
        _shutdown_event.set()
        _lifecycle.begin_shutdown()
        _lifecycle.wait_for_quiescence()
        with _engine_lock:
            try:
                _release_engine_locked()
            except Exception:
                logger.exception("[ONNX-LIFECYCLE] Error releasing engine at process exit")
        _http_server.server_close()
        idle_monitor.join(timeout=1)
        _http_server = None


if __name__ == "__main__":
    main()
