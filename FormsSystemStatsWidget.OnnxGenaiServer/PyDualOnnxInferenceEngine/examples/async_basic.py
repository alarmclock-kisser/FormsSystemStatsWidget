import asyncio
from pathlib import Path

from onnx_engine import (
    CudaRuntimeConfig,
    GenerationRequest,
    InferenceEngine,
    SamplingConfig,
    StopConfig,
)


MODEL_DIR = Path(r"D:\Models\MyOnnxModel")


async def main() -> None:
    engine = InferenceEngine(
        MODEL_DIR,
        cuda=CudaRuntimeConfig(device_id=0),
    )

    engine.load()

    try:
        request = GenerationRequest(
            messages=(
                {"role": "user", "content": "Stream a response."},
            ),
            sampling=SamplingConfig(),
            stopping=StopConfig(max_new_tokens=128),
        )

        async for chunk in engine.generate_async(request):
            print(chunk.text, end="", flush=True)
            if chunk.finished:
                break

        print()
    finally:
        engine.unload()


if __name__ == "__main__":
    asyncio.run(main())
