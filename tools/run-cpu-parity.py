"""Frozen preview.7, current portable AOT and original default on identical CPU scenes.

No builds are performed while sampling. Exit 2 preserves valid measurements
whose performance or native-pixel gates fail. No publication is triggered.
"""
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import statistics

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("aot_runner", ROOT / "tools/run-avalonia-aot.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)

p = argparse.ArgumentParser()
p.add_argument("--candidate", type=Path, required=True)
p.add_argument("--output", type=Path, required=True)
p.add_argument("--baseline", type=Path, default=ROOT / "artifacts/cnblogs-performance-2026-10-26/publish/portable")
p.add_argument("--forks", type=int, default=3)
p.add_argument("--seconds", type=float, default=5)
p.add_argument("--warmup", type=float, default=2)
p.add_argument("--throughput-only", action="store_true")
args = p.parse_args()
if args.output.exists():
    raise SystemExit("Use a new output directory; baseline measurements must be preserved.")
args.output.mkdir(parents=True)
engines = {
    "original": args.baseline / "original/AvaloniaAot.Original.exe",
    "preview7": args.baseline / "simd/AvaloniaAot.SimdSkia.exe",
    "candidate": args.candidate / "AvaloniaAot.SimdSkia.exe",
}
hashes = {name: runner.native_pe(exe) for name, exe in engines.items()}
cases = runner.CASES[:3] if args.throughput_only else runner.CASES
manifest = {"executables": {k: str(v.resolve()) for k, v in engines.items()}, "sha256": hashes,
            "cases": cases, "forks": args.forks, "seconds": args.seconds, "warmup": args.warmup,
            "backend": "Win32 software / RenderTargetBitmap synchronous CPU", "complete": False,
            "productSourceSha256": {str(f.relative_to(ROOT)): hashlib.sha256(f.read_bytes()).hexdigest()
                for f in sorted((ROOT / "src").rglob("*.cs")) if "obj" not in f.parts and "bin" not in f.parts}}
provenance=args.candidate/"build-manifest.json"
if provenance.exists():
    manifest["candidateBuild"]=json.loads(provenance.read_text(encoding="utf-8"))
    for name,digest in manifest["candidateBuild"]["runtimeSha256"].items():
        assert hashlib.sha256((args.candidate/name).read_bytes()).hexdigest()==digest,"Frozen candidate binary changed"
else:
    manifest["candidateBuildNote"]="Exploratory candidate; workspace hashes alone do not prove build provenance. Not release eligible."
runner.save_json(args.output / "manifest.json", manifest)
ram = runner.physical_memory()
rows = []
for fork in range(args.forks):
    for scene, scale, mode in cases:
        order = list(engines)
        order = order[fork % len(order):] + order[:fork % len(order)]
        for name in order:
            dest = (args.output / f"{scene}-{scale}-{mode}-{name}-{fork}").resolve()
            dest.mkdir()
            arguments = ["--hidden", "--output", str(dest), "--scene", scene, "--scale", str(scale),
                         "--mode", mode, "--seconds", str(args.seconds), "--warmup", str(args.warmup)]
            sampled = runner.measure_process(engines[name].resolve(), arguments, dest, ram)
            metrics = json.loads((dest / "metrics.json").read_text(encoding="utf-8"))
            runner.verify_aot_metrics(metrics)
            if metrics["avx2"]:
                raise RuntimeError("Portable comparison unexpectedly enabled AVX2.")
            row = {"variant": name, "fork": fork, "output": str(dest), "metrics": metrics, "external": sampled}
            rows.append(row)
            runner.save_json(args.output / "runs.json", rows)
            print(f"{name} {scene}/{scale}/{mode} #{fork}: {metrics['drawMsPerFrame']:.4f} ms", flush=True)
summary = []
for scene, scale, mode in cases:
    entry = {"scene": scene, "scale": scale, "mode": mode}
    for name in engines:
        group = [r["metrics"] for r in rows if r["variant"] == name and
                 (r["metrics"]["scene"], r["metrics"]["scale"], r["metrics"]["mode"]) == (scene, scale, mode)]
        entry[name] = {key: statistics.median(r[key] for r in group) for key in
                      ("drawMsPerFrame", "p95Ms", "cpuMsPerFrame", "cpuPercentMachine", "workingSetMiB",
                       "privateMiB", "allocatedBytesPerFrame", "managedRetainedMiB")}
    entry["ratio"] = entry["candidate"]["drawMsPerFrame"] / entry["original"]["drawMsPerFrame"]
    summary.append(entry)
quality = []
for scene, scale, mode in cases:
    def frames(name):
        return [Path(r["output"]).joinpath("scene.rgba").read_bytes() for r in rows if r["variant"] == name and
                (r["metrics"]["scene"], r["metrics"]["scale"], r["metrics"]["mode"]) == (scene, scale, mode)]
    before, after, original = frames("preview7"), frames("candidate"), frames("original")
    assert all(a == after[0] for a in after), "Candidate captures differ across forks"
    assert all(a == original[0] for a in original), "Original captures differ across forks"
    assert all(a == before[0] for a in before), "Preview captures differ across forks"
    assert len(after[0]) == len(original[0])
    differences = [int(a) - int(b) for a, b in zip(after[0], original[0])]
    maximum = max(map(abs, differences))
    rmse = math.sqrt(sum(d*d for d in differences) / len(differences))
    quality.append({"scene": scene, "scale": scale, "mode": mode, "unchangedFromPreview7": before[0] == after[0],
                    "max": maximum, "rmse": rmse, "passed": maximum <= 32 and rmse <= 2})
ratios = [r["ratio"] for r in summary if r["mode"] == "throughput"]
gate = {"geometricMean": math.exp(statistics.mean(map(math.log, ratios))), "worstRatio": max(ratios),
        "pixelQualityPassed": all(r["passed"] for r in quality)}
all_ratios = [r["ratio"] for r in summary]
gate["allScenesGeometricMean"] = math.exp(statistics.mean(map(math.log, all_ratios)))
gate["allScenesWorstRatio"] = max(all_ratios)
gate["performancePassed"] = (gate["geometricMean"] <= 1 and gate["allScenesGeometricMean"] <= 1
                             and gate["allScenesWorstRatio"] <= 1.1)
gate["formalCollection"] = (args.forks >= 3 and args.seconds >= 5 and args.warmup >= 2
                             and not args.throughput_only and provenance.exists())
gate["releaseEligible"] = gate["performancePassed"] and gate["pixelQualityPassed"] and gate["formalCollection"]
for name, exe in engines.items():
    assert runner.native_pe(exe) == hashes[name], "Executable changed during sampling"
manifest["complete"] = True
runner.save_json(args.output / "manifest.json", manifest)
runner.save_json(args.output / "summary.json", summary)
runner.save_json(args.output / "quality.json", quality)
runner.save_json(args.output / "gate.json", gate)
print(json.dumps(gate, indent=2), flush=True)
raise SystemExit(0 if gate["releaseEligible"] else 2)
