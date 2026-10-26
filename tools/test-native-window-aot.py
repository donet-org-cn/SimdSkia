"""Publish and actually run the standalone SimdSkia Win32 window, then measure files.

Requires Windows, the .NET 10 Native AOT toolchain, and a visible desktop.
This measures deployment bytes, not rendering speed or resident memory.
"""
import argparse
import hashlib
import json
import platform
import shutil
import struct
import subprocess
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "examples/NativeWindow/NativeWindow.csproj"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_pe(path):
    data = path.read_bytes()
    offset = struct.unpack_from("<I", data, 0x3C)[0]
    assert data[:2] == b"MZ" and data[offset:offset + 4] == b"PE\0\0"
    optional = offset + 24
    assert struct.unpack_from("<H", data, optional)[0] == 0x20B, "Expected x64 PE32+"
    assert struct.unpack_from("<H", data, optional + 68)[0] == 2, "Expected GUI subsystem, without a console"
    assert struct.unpack_from("<II", data, optional + 112 + 14 * 8) == (0, 0), "IL header found"


def check_bitmap(path, toggled, text):
    data = path.read_bytes()
    assert data[:2] == b"BM"
    offset = struct.unpack_from("<I", data, 10)[0]
    width, height = struct.unpack_from("<ii", data, 18)
    assert height < 0 and struct.unpack_from("<H", data, 28)[0] == 32
    height = -height
    assert len(data) == offset + width * height * 4

    def pixel(x, y):
        start = offset + (y * width + x) * 4
        b, g, r, a = data[start:start + 4]
        return r, g, b, a

    assert pixel(4, 4) == (242, 245, 250, 255)
    assert pixel(48, 48) == ((25, 160, 115, 255) if toggled else (44, 105, 210, 255))
    assert pixel(240, 60) == (25, 160, 115, 255)
    # A curved edge must contain fractional coverage, not just two flat colors.
    edge = {pixel(x, y) for y in range(24, 97) for x in range(204, 277)}
    assert len(edge) > 10, "Antialiased circle was not rendered"
    ink = sum(pixel(x, y) != (242, 245, 250, 255) for y in range(170, 205) for x in range(24, 300))
    assert (ink > 200) if text else (ink == 0), "Unexpected Chinese text pixels"
    return {"path": str(path.relative_to(ROOT)), "sha256": sha(path), "width": width,
            "height": height, "chineseInkPixels": ink, "circleColors": len(edge)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/native-window-aot")
    options = parser.parse_args()
    if sys.platform != "win32":
        raise RuntimeError("This example uses the Windows system window host.")
    output = options.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    result = {"measuredAtUtc": datetime.now(timezone.utc).isoformat(), "platform": platform.platform(),
              "sdk": subprocess.check_output(["dotnet", "--version"], cwd=ROOT, text=True).strip(),
              "baseCommit": subprocess.check_output(["git", "-c", f"safe.directory={ROOT.as_posix()}", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
              "scope": "Windows x64 standalone CPU window. No Avalonia. System font only. PDB excluded; licenses retained. Size preference; no UPX or manual binary stripping. Includes verification code.",
              "sourceFiles": {}, "variants": {}}
    for label, include_text in [("shapes", False), ("text", True)]:
        directory = output / label
        command = ["dotnet", "publish", str(PROJECT), "-c", "Release", "-r", "win-x64",
                   f"-p:IncludeText={str(include_text).lower()}", "-o", str(directory)]
        print("Publishing " + label, flush=True)
        with (output / f"publish-{label}.log").open("w", encoding="utf-8") as log:
            subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=600)
        executable = directory / "NativeWindow.exe"
        verify_pe(executable)
        runtime_files = [p for p in sorted(directory.rglob("*")) if p.is_file() and p.suffix.lower() != ".pdb"]
        assert not any(p.suffix.lower() in {".dll", ".ttf", ".ttc", ".otf", ".so", ".dylib"} for p in runtime_files), "Unexpected bundled native library/font"
        # Run a clean deployment copy, not the publish directory with its symbols.
        portable = output / f"portable-{label}"
        portable.mkdir(exist_ok=True)
        for path in runtime_files:
            dest = portable / path.relative_to(directory)
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, dest)
        assert {p.relative_to(portable).as_posix() for p in portable.rglob("*") if p.is_file()} == {p.relative_to(directory).as_posix() for p in runtime_files}, "Deployment contains stale files; choose a new output directory"
        verify = output / f"verify-{label}"
        if (verify / "metrics.json").exists():
            (verify / "metrics.json").unlink()
        run = subprocess.run([str(portable / "NativeWindow.exe"), "--verify", str(verify)],
                             cwd=ROOT, capture_output=True, timeout=45, creationflags=subprocess.CREATE_NO_WINDOW)
        if run.returncode:
            error = (verify / "error.txt").read_text(encoding="utf-8") if (verify / "error.txt").exists() else str(run.stderr)
            raise RuntimeError(f"{label} window failed ({run.returncode}): {error}")
        metrics = json.loads((verify / "metrics.json").read_text(encoding="utf-8"))
        modules = [m.lower() for m in metrics["loadedModules"]]
        assert metrics["nativeAot"] and metrics["frames"] >= 3 and metrics["verifiedFrames"] == 3
        assert metrics["resizeVerified"] and metrics["clickVerified"] and metrics["includesText"] == include_text
        assert not any(any(s in m for s in ["coreclr", "clrjit", "skiasharp", "harfbuzz", "avalonia"]) for m in modules)
        bitmaps = [check_bitmap(verify / f"frame-{i}.bmp", i == 2, include_text) for i in range(3)]
        assert bitmaps[1]["width"] > bitmaps[0]["width"] and bitmaps[1]["height"] > bitmaps[0]["height"]
        archive_path = output / f"native-window-{label}.zip"
        with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for path in runtime_files:
                archive.write(path, path.relative_to(directory))
        with zipfile.ZipFile(archive_path) as archive:
            assert archive.testzip() is None
            for path in runtime_files:
                assert archive.read(path.relative_to(directory).as_posix()) == path.read_bytes()
        files = [{"path": p.relative_to(directory).as_posix(), "bytes": p.stat().st_size, "sha256": sha(p)} for p in runtime_files]
        result["variants"][label] = {"publishCommand": command, "exeBytes": executable.stat().st_size,
            "runtimeBytes": sum(f["bytes"] for f in files), "zipBytes": archive_path.stat().st_size,
            "pdbBytes": sum(p.stat().st_size for p in directory.rglob("*.pdb")), "files": files,
            "metrics": metrics, "bitmaps": bitmaps, "nativePeGuiVerified": True, "cleanDeploymentRunPassed": True}
        print(f"PASS {label}: EXE {executable.stat().st_size / 1048576:.3f} MiB, deployment {sum(f['bytes'] for f in files) / 1048576:.3f} MiB", flush=True)
    for path in [PROJECT, PROJECT.with_name("Program.cs"), Path(__file__)]:
        result["sourceFiles"][path.relative_to(ROOT).as_posix()] = sha(path)
    result["rendererSources"] = {}
    tracked = subprocess.check_output(["git", "-c", f"safe.directory={ROOT.as_posix()}", "ls-files", "src"], cwd=ROOT, text=True).splitlines()
    for name in tracked:
        if Path(name).suffix in {".cs", ".csproj", ".props", ".targets"}:
            result["rendererSources"][name] = sha(ROOT / name)
    (output / "results.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Evidence: " + str(output / "results.json"), flush=True)


if __name__ == "__main__":
    # RID-specific restore adds entries to referenced libraries' locks. Keep the
    # original source-library lock files intact even if compilation/tests fail.
    locks = {p: p.read_bytes() for p in (ROOT / "src").glob("*/packages.lock.json")}
    try:
        main()
    finally:
        for path, contents in locks.items():
            if path.read_bytes() != contents:
                path.write_bytes(contents)
