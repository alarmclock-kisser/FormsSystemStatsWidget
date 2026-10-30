from pathlib import Path

from onnx_engine import (
    CudaRuntimeConfig,
    GenerationRequest,
    InferenceEngine,
    SamplingConfig,
    StopConfig,
)


MODEL_DIR = Path(r"D:\Models\MyOnnxModel")


def main() -> None:
    engine = InferenceEngine(
        MODEL_DIR,
        cuda=CudaRuntimeConfig(
            device_id=0,
            enable_cuda_graph=False,
            use_tf32=True,
        ),
    )

    with engine:
        print("Available providers:", engine.available_providers())

        request = GenerationRequest(
            messages=(
                {"role": "system", "content": "You are a concise assistant."},
                {"role": "user", "content": "Hello from ONNX Runtime."},
            ),
            sampling=SamplingConfig(
                temperature=0.6,
                top_k=20,
                top_p=0.9,
                min_p=0.15,
                presence_penalty=0.0,
            ),
            stopping=StopConfig(
                max_new_tokens=128,
                stop_strings=(),
            ),
        )

        context, stream = engine.generate(request)

        text_parts: list[str] = []

        for chunk in stream:
            print(chunk.text, end="", flush=True)
            text_parts.append(chunk.text)

        print()

        engine.save_context(
            MODEL_DIR / "runtime_state.npz",
            context.state,
        )


if __name__ == "__main__":
    main()
