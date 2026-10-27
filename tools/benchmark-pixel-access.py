"""Build PixelAccess/Original and Managed in Release before running this driver.
Three fresh serial process pairs; never run alongside other benchmarks.
"""
import json
import platform
import statistics
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[1]
out = root / "artifacts/compat-expansion/pixel-benchmark"
out.mkdir(parents=True, exist_ok=True)
data = {"Original": [], "Managed": []}
for fork in range(3):
    for backend in ("Original", "Managed"):
        dll = root / f"benchmarks/PixelAccess/{backend}/bin/Release/net8.0/{backend}.dll"
        target = out / f"{backend.lower()}-{fork}.json"
        subprocess.run(["dotnet", str(dll), str(target)], cwd=root, check=True, capture_output=True, text=True)
        data[backend].append(json.loads(target.read_text(encoding="utf-8")))
        print(backend, fork, "complete", flush=True)
fields = ["msPerFrame", "cpuMsPerFrame", "cpuPercent", "allocatedBytesPerFrame", "workingSetMiB", "privateMiB"]
summary = {name: {key: statistics.median(row[key] for row in rows) for key in fields} for name, rows in data.items()}
summary["frameTimeRatio"] = summary["Managed"]["msPerFrame"] / summary["Original"]["msPerFrame"]
summary["scope"] = "1920x1080 synchronous CPU row copy, 64-byte source padding, 200 warmup frames, 3 seconds per process"
summary["os"] = platform.platform()
summary["runtime"] = subprocess.check_output(["dotnet", "--info"], text=True, encoding="utf-8", errors="replace")
(out / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
print(json.dumps(summary, indent=2))
