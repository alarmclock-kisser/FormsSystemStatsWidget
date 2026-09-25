#!/usr/bin/env python3
"""
FSSW ONNX GenAI - Python Environment Setup
==========================================
Checks the available Python version, installs missing packages
for ONNX model conversion and inference.

Usage:
    python setup_onnx_env.py [--install] [--user] [--python PATH] [--elevated]

    --install    Install missing packages (default: check only)
    --user       User-wide installation (pip install --user)
    --python     Path to a specific Python interpreter
    --elevated   Re-run this script with UAC elevation (admin) if needed
"""

import sys
import subprocess
import shutil
import os
import ctypes
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


def is_admin() -> bool:
    """Check if the current process is running with admin privileges."""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin() != 0
    except Exception:
        return False


def is_system_python_install() -> bool:
    """Check if Python is installed in a system location (e.g. C:\\PythonXXX)."""
    exe = sys.executable.lower()
    # System installs are typically in C:\PythonXXX or C:\Program Files\...
    if exe.startswith("c:\\python") or "program files" in exe:
        return True
    return False


def run_elevated(args: list[str]) -> int:
    """Re-run this script with UAC elevation (admin privileges)."""
    print("\n  [INFO] Re-running with elevated privileges (UAC prompt will appear)...")
    print()
    try:
        # Use ShellExecute with "runas" verb to trigger UAC
        result = ctypes.windll.shell32.ShellExecuteW(
            None,           # hwnd
            "runas",        # lpVerb - triggers UAC
            sys.executable, # lpFile
            " ".join(f'"{a}"' if " " in a else a for a in args),  # lpParams
            None,           # lpDirectory
            1              # nShowCmd - SW_SHOWNORMAL
        )
        # ShellExecuteW returns an atom; >32 means success
        if int(result) > 32:
            # Wait for the elevated process to finish
            # ShellExecuteW doesn't give us a handle, so we just return
            # The elevated process will print its own output to its own console.
            print("  [OK] Elevated process launched. Check the new console window for results.")
            return 0
        else:
            print(f"  [FAIL] UAC elevation failed (code: {result}). User may have cancelled.")
            return 1
    except Exception as ex:
        print(f"  [FAIL] Could not launch elevated process: {ex}")
        return 1


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
    if version > MAX_PYTHON_VERSION:
        print(f"  [WARN] Python {version.major}.{version.minor} is newer than tested "
              f"({MAX_PYTHON_VERSION[0]}.{MAX_PYTHON_VERSION[1]}). May work, but not guaranteed.")

    print(f"  [OK] Python version is acceptable.")
    return True


def check_package(package_name: str) -> tuple[bool, str]:
    """Checks if a package is importable or installed via pip. Returns (installed, version)."""
    if package_name == "nvidia-ml-py":
        # The deprecated pynvml distribution exposes the same import module.
        # Check distribution metadata so it cannot satisfy this requirement.
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


def remove_deprecated_pynvml() -> bool:
    """Remove the deprecated distribution that shadows nvidia-ml-py's pynvml module."""
    try:
        from importlib.metadata import PackageNotFoundError, version
        version("pynvml")
    except PackageNotFoundError:
        return True
    except Exception as ex:
        print(f"  [WARN] Could not inspect deprecated pynvml package: {ex}")
        return True

    print("  [INFO] Removing deprecated pynvml distribution (nvidia-ml-py provides the same module).")
    try:
        result = subprocess.run(
            [sys.executable, "-m", "pip", "uninstall", "--yes", "pynvml"],
            capture_output=False, text=True
        )
        if result.returncode == 0:
            print("  [OK] Deprecated pynvml distribution removed.")
            return True
        print(f"  [WARN] Could not remove deprecated pynvml (exit code {result.returncode}).")
    except Exception as ex:
        print(f"  [WARN] Could not remove deprecated pynvml: {ex}")
    return False


def remove_cpu_onnxruntime() -> bool:
    """Remove the CPU distribution when the GPU distribution is also installed."""
    try:
        from importlib.metadata import PackageNotFoundError, version
        version("onnxruntime-gpu")
    except PackageNotFoundError:
        return True
    except Exception as ex:
        print(f"  [WARN] Could not inspect onnxruntime-gpu package: {ex}")
        return True

    try:
        from importlib.metadata import PackageNotFoundError, version
        version("onnxruntime")
    except PackageNotFoundError:
        return True
    except Exception as ex:
        print(f"  [WARN] Could not inspect CPU onnxruntime package: {ex}")
        return True

    print("  [INFO] Removing conflicting CPU-only onnxruntime distribution.")
    try:
        result = subprocess.run(
            [sys.executable, "-m", "pip", "uninstall", "--yes", "onnxruntime"],
            capture_output=False, text=True
        )
        if result.returncode == 0:
            print("  [OK] Conflicting CPU-only onnxruntime distribution removed.")
            return True
        print(f"  [FAIL] Could not remove CPU-only onnxruntime (exit code {result.returncode}).")
    except Exception as ex:
        print(f"  [FAIL] Could not remove CPU-only onnxruntime: {ex}")
    return False


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


def install_packages(user: bool = False, elevated: bool = False) -> bool:
    """Installs all missing packages via pip.
    
    Strategy:
    1. If system Python install and not admin → try --user first, then UAC elevation
    2. If admin or user install → install directly
    """
    print_header("Installing Missing Packages")

    missing = [
        spec for name, spec in REQUIRED_PACKAGES.items()
        if not check_package(name)[0]
    ]

    if not missing:
        print("  All packages already installed. Nothing to do.")
        return True

    print(f"  Missing: {', '.join(missing)}")

    system_install = is_system_python_install()
    admin = is_admin()

    # Determine install strategy
    if system_install and not admin:
        # System Python, non-admin: try --user first
        print("  [INFO] System Python detected, running without admin.")
        print("  [INFO] Attempting user-level install first...")
        cmd = [sys.executable, "-m", "pip", "install", "--user"]
        cmd.extend(missing)
        print(f"  Running: {' '.join(cmd)}")
        print()

        try:
            result = subprocess.run(cmd, capture_output=False, text=True)
            if result.returncode == 0:
                print("\n  [OK] pip install completed (user-level).")
                # Verify - if it works, we're done
                if verify_after_install():
                    return True
                print("  [WARN] User-level install completed but verification failed.")
                print("  [INFO] Package may be in system site-packages (read-only for this user).")
                print("  [INFO] Attempting UAC elevation for system-wide install...")
        except Exception as ex:
            print(f"\n  [FAIL] Error running pip: {ex}")

        # UAC elevation for system-wide install
        if elevated:
            args = [sys.executable, os.path.abspath(__file__), "--install"]
            return run_elevated(args)
        else:
            print("  [INFO] Use --elevated flag to allow UAC prompt for system-wide install.")
            return False
    else:
        # Admin or non-system Python: install directly
        cmd = [sys.executable, "-m", "pip", "install"]
        if user and not admin:
            cmd.append("--user")
        cmd.extend(missing)
        print(f"  Running: {' '.join(cmd)}")
        print()

        try:
            result = subprocess.run(cmd, capture_output=False, text=True)
            if result.returncode == 0:
                print("\n  [OK] pip install completed.")
                return True
            else:
                print(f"\n  [FAIL] pip install exited with code {result.returncode}.")
                return False
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


def check_gpu_execution_provider() -> bool:
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
    cmd = [sys.executable, "-m", "pip", "install", "--upgrade", "onnxruntime-gpu"]
    print(f"  Running: {' '.join(cmd)}")
    try:
        result = subprocess.run(cmd, capture_output=False, text=True)
        if result.returncode != 0:
            print(f"  [FAIL] onnxruntime-gpu install exited with code {result.returncode}.")
            return False
    except Exception as ex:
        print(f"  [FAIL] Error running pip: {ex}")
        return False

    # Verifikation: CUDA-Provider muss jetzt verfügbar sein
    try:
        import importlib
        import onnxruntime as ort
        importlib.reload(ort)
        available = ort.get_available_providers()
        print(f"  Providers after install: {available}")
        if "CUDAExecutionProvider" in available:
            print("  [OK] CUDAExecutionProvider is now available.")
            return True
        print("  [FAIL] CUDAExecutionProvider still not available after install.")
        return False
    except Exception as ex:
        print(f"  [FAIL] Could not verify CUDA provider after install: {ex}")
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
    parser.add_argument("--elevated", action="store_true",
                        help="Allow UAC elevation prompt if system-wide install is needed")
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
        if args.elevated:
            cmd.append("--elevated")
        result = subprocess.run(cmd)
        return result.returncode

    # 1. Python version check
    if not check_python_version():
        print("\n  Please install Python >= 3.10 from https://www.python.org/downloads/")
        return 1

    if args.install:
        if not remove_cpu_onnxruntime():
            if args.elevated and is_system_python_install() and not is_admin():
                print("  [INFO] Retrying the ONNX Runtime cleanup with administrator privileges...")
                elevated_args = [sys.executable, os.path.abspath(__file__), "--install"]
                if args.user:
                    elevated_args.append("--user")
                return run_elevated(elevated_args)
            print("  [FAIL] CUDA setup cannot continue while CPU and GPU ONNX Runtime packages conflict.")
            return 1
        remove_deprecated_pynvml()

    # 2. Package check
    results = check_all_packages()
    missing = [name for name, (installed, _) in results.items() if not installed]

    if not missing:
        print("\n  [OK] All required packages are installed.")
        # GPU-EP-Check auch bei vollständigem Env (onnxruntime-gpu kann fehlen)
        if not check_gpu_execution_provider():
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
    if not install_packages(user=args.user, elevated=args.elevated):
        return 1

    # 4. Verify
    if not verify_after_install():
        # If system Python and not admin, auto-trigger UAC elevation
        if is_system_python_install() and not is_admin():
            print("\n  [INFO] Verification failed. Package likely needs system-wide install.")
            print("  [INFO] Triggering UAC elevation for admin-level install...")
            args_elevated = [sys.executable, os.path.abspath(__file__), "--install"]
            elev_result = run_elevated(args_elevated)
            if elev_result == 0:
                # Re-verify after elevated install
                import time
                time.sleep(3)
                if verify_after_install():
                    print("\n  [OK] Environment is ready for ONNX model conversion and inference.")
                    return 0
            return 1
        print("\n  [WARN] Some packages could not be verified after install.")
        return 1

    print("\n  [OK] Environment is ready for ONNX model conversion and inference.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

