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
    "onnxruntime": "onnxruntime>=1.20.0",
    "onnx": "onnx>=1.16.0",
    "safetensors": "safetensors>=0.4.0",
    "transformers": "transformers>=4.40.0",
    "huggingface-hub": "huggingface-hub>=0.24.0",
    "numpy": "numpy>=1.26.0",
    "tokenizers": "tokenizers>=0.19.0",
    "accelerate": "accelerate>=0.30.0",
}

MIN_PYTHON_VERSION = (3, 10)
MAX_PYTHON_VERSION = (3, 13)


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


# Map pip package names to their actual Python import module names
# (pip names use hyphens, Python modules use underscores)
IMPORT_NAME_MAP = {
    "huggingface-hub": "huggingface_hub",
}

def check_package(package_name: str) -> tuple[bool, str]:
    """Checks if a package is importable or installed via pip. Returns (installed, version)."""
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

    # 2. Package check
    results = check_all_packages()
    missing = [name for name, (installed, _) in results.items() if not installed]

    if not missing:
        print("\n  [OK] All required packages are installed. Environment is ready.")
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
