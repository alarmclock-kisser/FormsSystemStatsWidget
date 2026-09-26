#!/usr/bin/env python3
"""
FSSW ONNX GenAI - Python Environment Setup
==========================================
Checks the available Python version, installs missing packages
for ONNX model conversion and inference.

Usage:
    python setup_onnx_env.py [--install] [--user] [--python PATH]

    --install    Install missing packages (default: check only)
    --user       User-wide installation (pip install --user)
    --python     Path to a specific Python interpreter
"""

import sys
import subprocess
import shutil
import os
import argparse
from importlib import import_module

# ---------------------------------------------------------------------------
# Erforderliche Pakete für ONNX-Modell-Konvertierung und -Inferenz
# ---------------------------------------------------------------------------
REQUIRED_PACKAGES = {
    "onnxruntime-gpu": "onnxruntime-gpu>=1.20.0",
    "onnx": "onnx>=1.16.0",
    "safetensors": "safetensors>=0.4.0",
    "transformers": "transformers>=4.40.0",
    "huggingface-hub": "huggingface-hub>=0.24.0",
    "numpy": "numpy>=1.26.0",
    "tokenizers": "tokenizers>=0.19.0",
    "accelerate": "accelerate>=0.30.0",
    "nvidia-ml-py": "nvidia-ml-py>=12.0.0",
}

# onnxruntime-gpu ersetzt die CPU-only onnxruntime (gleicher Import-Name 'onnxruntime').
# Für die Import-Prüfung genügt daher der 'onnxruntime'-Import; die GPU-Variante
# wird zusätzlich über pip installiert, damit der CUDA-Execution-Provider verfügbar ist.
IMPORT_NAME_MAP = {
    "huggingface-hub": "huggingface_hub",
    "onnxruntime-gpu": "onnxruntime",
    "nvidia-ml-py": "pynvml",
}

MIN_PYTHON_VERSION = (3, 10)
MAX_PYTHON_VERSION = (3, 14)


def is_system_python_install() -> bool:
    """Check if Python is installed in a system location (e.g. C:\\PythonXXX)."""
    exe = sys.executable.lower()
    # System installs are typically in C:\PythonXXX or C:\Program Files\...
    if exe.startswith("c:\\python") or "program files" in exe:
        return True
    return False


def print_header(title: str) -> None:
    print(f"\n{'=' * 60}")
    print(f"  {title}")
    print(f"{'=' * 60}\n")


def check_python_version() -> bool:
    """Checkt die Python-Version und gibt True, wenn OK."""
    print_header("Python Version Check")
    version = sys.version_info
    print(f"  Python: {version.major}.{version.minor}.{version.micro}")
    print(f"  Executable: {sys.executable}")

    if version < MIN_PYTHON_VERSION:
        print(f"  [FAIL] Python >= {MIN_PYTHON_VERSION[0]}.{MIN_PYTHON_VERSION[1]} required.")
        return False
    if version[:2] > MAX_PYTHON_VERSION:
        print(f"  [WARN] Python {version.major}.{version.minor} is newer than tested "
              f"({MAX_PYTHON_VERSION[0]}.{MAX_PYTHON_VERSION[1]}). May work, but not guaranteed.")

    print(f"  [OK] Python version is acceptable.")
    return True


def check_package(package_name: str) -> tuple[bool, str]:
    """Checks if a package is importable or installed via pip. Returns (installed, version)."""
    if package_name in {"onnxruntime-gpu", "nvidia-ml-py"}:
        # Both distributions expose an import name that another distribution may also provide.
        try:
            from importlib.metadata import version
            return True, version(package_name)
        except Exception:
            return False, ""

    # First try import (using the correct module name)
    import_name = IMPORT_NAME_MAP.get(package_name, package_name)
    try:
        mod = import_module(import_name)
        version = getattr(mod, "__version__", "unknown")
        return True, version
    except ImportError:
        pass

    # Fallback: check via pip show (handles cases where package is in
    # a different site-packages path than the current process sees)
    try:
        result = subprocess.run(
            [sys.executable, "-m", "pip", "show", package_name],
            capture_output=True, text=True, timeout=15
        )
        if result.returncode == 0 and "Version:" in result.stdout:
            for line in result.stdout.splitlines():
                if line.startswith("Version:"):
                    return True, line.split(":", 1)[1].strip()
    except Exception:
        pass

    return False, ""


def check_all_packages() -> dict[str, tuple[bool, str]]:
    """Checkt alle erforderlichen Pakete."""
    print_header("Package Check")
    results = {}
    for name in REQUIRED_PACKAGES:
        installed, version = check_package(name)
        status = "OK  " if installed else "MISS"
        ver_str = f"  {version}" if installed else "  (not installed)"
        print(f"  [{status}] {name:<25} {ver_str}")
        results[name] = (installed, version)
    return results


def install_packages(user: bool = False) -> bool:
    """Install missing packages without modifying a protected system Python install."""
    print_header("Installing Missing Packages")

    missing = [
        spec for name, spec in REQUIRED_PACKAGES.items()
        if not check_package(name)[0]
    ]

    if not missing:
        print("  All packages already installed. Nothing to do.")
        return True

    print(f"  Missing: {', '.join(missing)}")

    cmd = [sys.executable, "-m", "pip", "install"]
    if user or is_system_python_install():
        cmd.append("--user")
    cmd.extend(missing)
    print(f"  Running: {' '.join(cmd)}")
    print()

    try:
        result = subprocess.run(cmd, capture_output=False, text=True)
        if result.returncode == 0:
            print("\n  [OK] pip install completed.")
            return True
        print(f"\n  [FAIL] pip install exited with code {result.returncode}.")
    except Exception as ex:
        print(f"\n  [FAIL] Error running pip: {ex}")
    return False


def has_nvidia_gpu() -> bool:
    """Prüft, ob eine NVIDIA GPU mit Treiber vorhanden ist (nvidia-smi)."""
    try:
        result = subprocess.run(
            ["nvidia-smi", "--query-gpu=name", "--format=csv,noheader"],
            capture_output=True, text=True, timeout=15
        )
        return result.returncode == 0 and bool(result.stdout.strip())
    except Exception:
        return False


def check_gpu_execution_provider(user: bool = False) -> bool:
    """Prüft, ob der CUDA-Execution-Provider in der installierten onnxruntime verfügbar ist.

    Bei NVIDIA GPU + fehlendem CUDA-Provider wird onnxruntime-gpu installiert
    (Multi-GPU wird über pynvml + device_id-Provider-Instanzen unterstützt).
    """
    print_header("GPU Execution Provider Check")

    if not has_nvidia_gpu():
        print("  [INFO] No NVIDIA GPU detected. Skipping CUDA provider check.")
        return True

    print("  [OK] NVIDIA GPU detected (nvidia-smi).")

    try:
        import onnxruntime as ort
        available = ort.get_available_providers()
        print(f"  Available providers: {available}")
        if "CUDAExecutionProvider" in available:
            print("  [OK] CUDAExecutionProvider is available.")
            return True
        print("  [MISS] CUDAExecutionProvider NOT available in current onnxruntime build.")
    except Exception as ex:
        print(f"  [WARN] Could not query onnxruntime providers: {ex}")
        return True

    print("  [INFO] Installing onnxruntime-gpu (replaces CPU-only onnxruntime)...")
    cmd = [sys.executable, "-m", "pip", "install", "--upgrade"]
    if user or is_system_python_install():
        cmd.append("--user")
    cmd.append("onnxruntime-gpu")
    print(f"  Running: {' '.join(cmd)}")
    try:
        result = subprocess.run(cmd, capture_output=False, text=True)
        if result.returncode != 0:
            print(f"  [FAIL] onnxruntime-gpu install exited with code {result.returncode}.")
            return False
    except Exception as ex:
        print(f"  [FAIL] Error running pip: {ex}")
        return False

    # Check in a fresh interpreter so a previously imported CPU module cannot mask the user install.
    provider_check = subprocess.run(
        [sys.executable, "-c", "import onnxruntime as ort; print('\\n'.join(ort.get_available_providers()))"],
        capture_output=True, text=True, timeout=60
    )
    if provider_check.returncode != 0:
        print(f"  [FAIL] Could not verify CUDA provider after install: {provider_check.stderr.strip()}")
        return False
    available = provider_check.stdout.splitlines()
    print(f"  Providers after install: {available}")
    if "CUDAExecutionProvider" in available:
        print("  [OK] CUDAExecutionProvider is now available.")
        return True
    print("  [FAIL] CUDAExecutionProvider still not available after install.")
    return False


def verify_after_install() -> bool:
    """Verifiziert nach Installation, dass alle Pakete importierbar sind."""
    print_header("Post-Install Verification")
    all_ok = True
    for name in REQUIRED_PACKAGES:
        installed, version = check_package(name)
        status = "OK  " if installed else "FAIL"
        ver_str = f"  {version}" if installed else "  (still missing)"
        print(f"  [{status}] {name:<25} {ver_str}")
        if not installed:
            all_ok = False
    return all_ok


def main() -> int:
    parser = argparse.ArgumentParser(description="FSSW ONNX GenAI Python Environment Setup")
    parser.add_argument("--install", action="store_true",
                        help="Install missing packages (default: check only)")
    parser.add_argument("--user", action="store_true",
                        help="Use pip install --user (user-wide, no admin needed)")
    parser.add_argument("--python", type=str, default=None,
                        help="Path to a specific Python interpreter")
    args = parser.parse_args()

    # Optional: specific Python interpreter
    if args.python:
        if not shutil.which(args.python):
            print(f"  [FAIL] Python interpreter not found: {args.python}")
            return 1
        cmd = [args.python, os.path.abspath(__file__)]
        if args.install:
            cmd.append("--install")
        if args.user:
            cmd.append("--user")
        result = subprocess.run(cmd)
        return result.returncode

    # 1. Python version check
    if not check_python_version():
        print("\n  Please install Python >= 3.10 from https://www.python.org/downloads/")
        return 1

    # 2. Package check
    results = check_all_packages()
    missing = [name for name, (installed, _) in results.items() if not installed]

    if not missing:
        print("\n  [OK] All required packages are installed.")
        # GPU-EP-Check auch bei vollständigem Env (onnxruntime-gpu kann fehlen)
        if not check_gpu_execution_provider(user=args.user):
            print("\n  [WARN] GPU execution provider setup did not complete successfully.")
            print("         The server will fall back to CPU. You can retry later.")
            return 1
        print("  [OK] Environment is ready.")
        return 0

    print(f"\n  {len(missing)} package(s) missing: {', '.join(missing)}")

    if not args.install:
        print("\n  Run with --install to install missing packages:")
        print(f"    python {os.path.abspath(__file__)} --install")
        return 2

    # 3. Install
    if not install_packages(user=args.user):
        return 1

    # 4. Verify
    if not verify_after_install():
        print("\n  [WARN] Some packages could not be verified after install.")
        return 1

    if not check_gpu_execution_provider(user=args.user):
        print("\n  [WARN] GPU execution provider setup did not complete successfully.")
        print("         The server will fall back to CPU. You can retry later.")
        return 1

    print("\n  [OK] Environment is ready for ONNX model conversion and inference.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

