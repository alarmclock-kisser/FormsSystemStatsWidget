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
    stage0_path: Path | None = None
    stage1_path: Path | None = None

    @property
    def is_partitioned(self) -> bool:
        return self.stage0_path is not None and self.stage1_path is not None

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

    def load(self, path: str | Path, *, layout: str = "auto") -> ModelPackage:
        normalized_layout = layout.strip().casefold()
        if normalized_layout not in {"auto", "main", "partitioned"}:
            raise ModelValidationError(
                f"Unsupported model layout {layout!r}; expected auto, main, or partitioned."
            )

        root = Path(path).resolve()

        if root.is_file() and root.suffix.lower() == ".onnx":
            root = root.parent

        if not root.is_dir():
            raise FileNotFoundError(f"Model package directory not found: {root}")

        model_path = self._find_model(root, allow_nested=normalized_layout != "main")
        stage0_path, stage1_path = self._find_partitioned_models(root, normalized_layout)
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
            stage0_path=stage0_path,
            stage1_path=stage1_path,
        )

    @staticmethod
    def _find_partitioned_models(root: Path, layout: str) -> tuple[Path | None, Path | None]:
        if layout == "main":
            return None, None

        partition_root = root / "partitioned"
        stage0_path = partition_root / "model.stage0.onnx"
        stage1_path = partition_root / "model.stage1.onnx"
        has_stage0 = stage0_path.is_file()
        has_stage1 = stage1_path.is_file()

        if not has_stage0 and not has_stage1 and layout == "auto":
            return None, None
        if not has_stage0 or not has_stage1:
            raise ModelValidationError(
                f"Partitioned model must contain both {stage0_path.name} and {stage1_path.name}."
            )
        return stage0_path, stage1_path

    @staticmethod
    def _find_model(root: Path, *, allow_nested: bool = True) -> Path:
        preferred = root / "model.onnx"
        if preferred.is_file():
            return preferred

        candidates = sorted(root.glob("*.onnx"))
        if not candidates and allow_nested:
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
