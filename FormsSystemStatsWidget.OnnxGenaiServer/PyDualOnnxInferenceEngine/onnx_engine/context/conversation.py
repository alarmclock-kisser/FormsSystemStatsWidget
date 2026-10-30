from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Sequence


@dataclass(slots=True)
class Conversation:
    messages: list[dict[str, Any]] = field(default_factory=list)

    def add(self, role: str, content: str, **extra: Any) -> None:
        message: dict[str, Any] = {
            "role": role,
            "content": content,
        }
        message.update(extra)
        self.messages.append(message)

    def extend(self, messages: Sequence[dict[str, Any]]) -> None:
        self.messages.extend(dict(message) for message in messages)

    def as_list(self) -> list[dict[str, Any]]:
        return [dict(message) for message in self.messages]

    def clear(self) -> None:
        self.messages.clear()
