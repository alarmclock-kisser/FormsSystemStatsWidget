from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from ..errors import ModelValidationError


@dataclass(slots=True, frozen=True)
class ModelPackage:
    root: Path
    model_path: Path
    json_data: dict[str, dict[str, Any]]
    chat_template: str | None
    extra_chat_templates: dict[str, str]
    model_io: dict[str, Any] | None
    tokenizer_path: Path | None
    generation_config: dict[str, Any]

    def get_json(self, name: str) -> dict[str, Any]:
        return self.json_data.get(name, {})

    def has(self, name: str) -> bool:
        return (self.root / name).is_file()


class ModelPackageLoader:
    """Loads a local model directory without performing inference."""

    _JSON_FILES = (
        "config.json",
        "generation_config.json",
        "genai_config.json",
        "tokenizer_config.json",
        "special_tokens_map.json",
        "preprocessor_config.json",
        "processor_config.json",
    )

    def load(self, path: str | Path) -> ModelPackage:
        root = Path(path).resolve()

        if root.is_file() and root.suffix.lower() == ".onnx":
            root = root.parent

        if not root.is_dir():
            raise FileNotFoundError(f"Model package directory not found: {root}")

        model_path = self._find_model(root)
        json_data = self._load_json_files(root)

        chat_template, extra_chat_templates = self._load_chat_templates(root)

        model_io = None
        model_io_path = root / "model_io.json"
        if model_io_path.is_file():
            model_io = self._load_json(model_io_path)

        tokenizer_path = self._find_tokenizer(root)

        return ModelPackage(
            root=root,
            model_path=model_path,
            json_data=json_data,
            chat_template=chat_template,
            extra_chat_templates=extra_chat_templates,
            model_io=model_io,
            tokenizer_path=tokenizer_path,
            generation_config=json_data.get("generation_config", {}),
        )

    @staticmethod
    def _find_model(root: Path) -> Path:
        preferred = root / "model.onnx"
        if preferred.is_file():
            return preferred

        candidates = sorted(root.glob("*.onnx"))
        if not candidates:
            candidates = sorted(root.rglob("*.onnx"))

        if not candidates:
            raise ModelValidationError(f"No ONNX model found below {root}")

        return candidates[0]

    @classmethod
    def _load_json_files(cls, root: Path) -> dict[str, dict[str, Any]]:
        result: dict[str, dict[str, Any]] = {}

        for filename in cls._JSON_FILES:
            path = root / filename
            if path.is_file():
                result[filename] = cls._load_json(path)

        return result

    @staticmethod
    def _load_json(path: Path) -> dict[str, Any]:
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            raise ModelValidationError(
                f"Invalid JSON in {path}: {exc}"
            ) from exc

        if not isinstance(data, dict):
            raise ModelValidationError(f"Expected JSON object in {path}")

        return data

    @staticmethod
    def _load_chat_templates(root: Path) -> tuple[str | None, dict[str, str]]:
        default_path = root / "chat_template.jinja"
        default = (
            default_path.read_text(encoding="utf-8")
            if default_path.is_file()
            else None
        )

        additional: dict[str, str] = {}
        additional_root = root / "additional_chat_templates"

        if additional_root.is_dir():
            for path in sorted(additional_root.glob("*.jinja")):
                additional[path.stem] = path.read_text(encoding="utf-8")

        return default, additional

    @staticmethod
    def _find_tokenizer(root: Path) -> Path | None:
        for filename in ("tokenizer.json", "tokenizer_config.json"):
            path = root / filename
            if path.is_file():
                return path
        return None
