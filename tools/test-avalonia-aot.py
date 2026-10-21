"""Audit measured AOT artifacts and exercise a real IL/JIT false-positive probe."""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("aot_runner", ROOT / "tools/run-avalonia-aot.py")
runner = importlib.util.module_from_spec(spec); spec.loader.exec_module(runner)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--portable", type=Path, default=ROOT / "artifacts/aot-comparison/portable-measured")
    parser.add_argument("--v3", type=Path, default=ROOT / "artifacts/aot-comparison/v3-measured")
    options = parser.parse_args()
    output = ROOT / "artifacts/aot-comparison/validation"; output.mkdir(parents=True, exist_ok=True)
    positive = 0
    for isa, directory in (("portable", options.portable.resolve()), ("v3", options.v3.resolve())):
        rows = json.loads((directory / "results.json").read_text(encoding="utf-8"))
        assert len(rows) == 45
        for row in rows:
            runner.verify_aot_metrics(row)
            assert row["avx2"] == (isa == "v3") and row["cpuCyclesPerFrame"] > 0
            positive += 1
        manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
        for binary in manifest["binaries"].values():
            assert runner.native_pe(Path(binary["path"])) == binary["sha256"], "Published binary changed after measurement"
        sources = json.loads((directory / "source-files.json").read_text(encoding="utf-8"))
        for name, digest in sources.items():
            # Only the parent verifier was hardened after measurement, with a frozen copy retained.
            path = directory / "run-harness.py.txt" if name == "tools/run-avalonia-aot.py" else ROOT / name
            assert hashlib.sha256(path.read_bytes()).hexdigest() == digest, "Measured source changed: " + name
        sizes = json.loads((directory / "sizes.json").read_text(encoding="utf-8"))
        for name in ("simd", "original"):
            with zipfile.ZipFile(directory / f"{name}-runtime.zip") as archive:
                assert archive.testzip() is None
                files = {f["path"]: f for f in sizes[name]["files"] if f["category"] != "symbols"}
                assert set(archive.namelist()) == set(files)
                for filename, entry in files.items():
                    assert hashlib.sha256(archive.read(filename)).hexdigest() == entry["sha256"]
        if isa == "portable": executable = Path(manifest["binaries"]["simd"]["path"])
    # The AOT runtimeconfig sets IsDynamicCodeSupported=false even under a real
    # dotnet-hosted IL execution. The parent must reject its CoreCLR module list.
    jit_directory = output / "jit-rejected"
    dll = ROOT / "examples/AvaloniaAot/Original/bin/Release/net10.0/win-x64/AvaloniaAot.Original.dll"
    try:
        runner.measure_process(Path(shutil.which("dotnet")), [str(dll), "--hidden", "--output", str(jit_directory),
            "--warmup", "0", "--seconds", "0.2"], jit_directory, runner.physical_memory())
    except RuntimeError as error:
        if "CoreCLR/JIT" not in str(error): raise
    else: raise AssertionError("The real JIT process was falsely accepted as AOT")
    jit_metrics = json.loads((jit_directory / "metrics.json").read_text(encoding="utf-8"))
    assert jit_metrics["nativeAot"] and "coreclr.dll" in [m.lower() for m in jit_metrics["loadedModules"]]
    # A managed library must fail the PE check as well.
    try: runner.native_pe(dll)
    except RuntimeError: pass
    else: raise AssertionError("IL PE accepted as a native executable")
    rejected = subprocess.run([str(executable), "--hidden", "--output", str(output / "invalid-duration"), "--seconds", "-1"],
                              capture_output=True, text=True, encoding="utf-8", errors="replace")
    assert rejected.returncode == 1 and "ArgumentOutOfRangeException" in rejected.stderr
    (output / "invalid-duration.txt").write_text(rejected.stdout + rejected.stderr, encoding="utf-8")
    result = {"status": "PASS", "positiveAotProcesses": positive, "unchangedNativePeBinaries": 4,
              "completeRuntimeZips": 4, "measuredSourceHashesVerified": True,
              "realJitWithAotFlagRejected": True, "managedPeRejected": True, "invalidDurationRejected": True}
    (output / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__": main()
