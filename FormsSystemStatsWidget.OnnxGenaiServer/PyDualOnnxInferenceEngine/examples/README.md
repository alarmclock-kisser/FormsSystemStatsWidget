# Examples

1. Copy `model_io.json` into the model directory and adapt it to the actual ONNX graph.
2. Edit `MODEL_DIR` in `basic.py`.
3. Install dependencies:

       python -m pip install -r requirements.txt

4. Run:

       python examples/basic.py

The `model_io.json` example is intentionally only a schema example.
The actual input/output names and past/present state ordering must match the
real ONNX graph.
