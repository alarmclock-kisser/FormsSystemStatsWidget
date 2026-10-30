from __future__ import annotations

from onnx_engine.server import EngineLifecycle


class FakeClock:
    def __init__(self) -> None:
        self.value = 0.0

    def __call__(self) -> float:
        return self.value

    def advance(self, seconds: float) -> None:
        self.value += seconds


def test_idle_timeout_begins_after_unload_and_triggers_once() -> None:
    clock = FakeClock()
    lifecycle = EngineLifecycle(enabled=True, idle_seconds=4, clock=clock)

    assert lifecycle.begin_operation("Unloading")
    clock.advance(10)
    assert not lifecycle.should_auto_shutdown(loaded=False)

    lifecycle.end_operation("Unloading", loaded=False)
    clock.advance(3)
    assert not lifecycle.should_auto_shutdown(loaded=False)
    clock.advance(1)
    assert lifecycle.should_auto_shutdown(loaded=False)
    assert not lifecycle.should_auto_shutdown(loaded=False)


def test_load_prevents_idle_shutdown_until_model_is_unloaded() -> None:
    clock = FakeClock()
    lifecycle = EngineLifecycle(enabled=True, idle_seconds=2, clock=clock)

    assert lifecycle.begin_operation("Loading")
    clock.advance(20)
    assert not lifecycle.should_auto_shutdown(loaded=False)
    lifecycle.end_operation("Loading", loaded=True)
    clock.advance(20)
    assert not lifecycle.should_auto_shutdown(loaded=True)


def test_active_generation_prevents_shutdown_and_restarts_unloaded_timer() -> None:
    clock = FakeClock()
    lifecycle = EngineLifecycle(enabled=True, idle_seconds=2, clock=clock)

    assert lifecycle.begin_operation("Generating")
    clock.advance(20)
    assert not lifecycle.should_auto_shutdown(loaded=True)
    lifecycle.end_operation("Generating", loaded=False)
    clock.advance(1)
    assert not lifecycle.should_auto_shutdown(loaded=False)
    clock.advance(1)
    assert lifecycle.should_auto_shutdown(loaded=False)


def test_idle_auto_shutdown_can_be_disabled() -> None:
    clock = FakeClock()
    lifecycle = EngineLifecycle(enabled=False, idle_seconds=1, clock=clock)
    clock.advance(1000)

    assert not lifecycle.should_auto_shutdown(loaded=False)


def test_load_is_rejected_after_shutdown_starts() -> None:
    lifecycle = EngineLifecycle(enabled=True, idle_seconds=1, clock=FakeClock())

    assert lifecycle.begin_shutdown()
    assert not lifecycle.begin_operation("Loading")