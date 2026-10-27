"""Actual Avalonia control tree: original Vulkan/ANGLE versus managed Vulkan/Direct2D.

All variants include completed BGRA8888 GPU readback on the same hardware.
These are JIT offscreen results, not swapchain/presentation or AOT results.
"""
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import statistics

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location("aot_runner",ROOT/"tools/run-avalonia-aot.py")
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
p=argparse.ArgumentParser(description=__doc__)
p.add_argument("--output",type=Path,required=True)
p.add_argument("--forks",type=int,default=3)
p.add_argument("--seconds",type=float,default=5)
p.add_argument("--warmup",type=float,default=3)
args=p.parse_args()
if args.output.exists():raise SystemExit("Choose a fresh output directory.")
args.output.mkdir(parents=True)
base=ROOT/"benchmarks/AvaloniaGpuParity"
original=base/"Original/bin/Release/net10.0/Original.exe"
managed=base/"Managed/bin/Release/net10.0/Managed.exe"
variants={"original-vulkan":(original,[]),"original-angle":(original,["--angle"]),
          "vulkan":(managed,["--backend","vulkan"]),"direct2d":(managed,["--backend","direct2d"])}
cases=[("controls",1),("controls",1.5),("scroll",1)]
files={str(f.resolve()):hashlib.sha256(f.read_bytes()).hexdigest()
       for parent in [original.parent,managed.parent] for f in parent.rglob("*")
       if f.is_file() and f.suffix in (".exe",".dll",".json")}
source={str(f.relative_to(ROOT)):hashlib.sha256(f.read_bytes()).hexdigest()
        for directory in [ROOT/"src",base] for f in directory.rglob("*.cs") if not {"obj","bin"}.intersection(f.parts)}
manifest={"cases":cases,"forks":args.forks,"seconds":args.seconds,"warmup":args.warmup,
          "runtimeSha256":files,"sourceSha256":source,"method":__doc__,"complete":False}
runner.save_json(args.output/"manifest.json",manifest)
expected_device=None
def validate(row):
    global expected_device
    if row["nativeAot"] or row["mode"]!="JIT GPU complete-frame readback":raise ValueError("Wrong runtime scope")
    if "coreclr.dll" not in {n.lower() for n in row["loadedModules"]}:raise ValueError("JIT runtime missing")
    if row["frames"]<1 or row["completionMs"]<=0:raise ValueError("GPU completion absent")
    if expected_device is None:expected_device=row["device"]
    if expected_device not in row["device"]:raise ValueError("Physical GPU differs between workers")
ram=runner.physical_memory();rows=[]
for fork in range(args.forks):
    for scene,scale in cases:
        order=list(variants);order=order[fork%len(order):]+order[:fork%len(order)]
        for variant in order:
            exe,extra=variants[variant];dest=(args.output/f"{scene}-{scale}-{variant}-{fork}").resolve();dest.mkdir()
            row=runner.measure_process(exe,[*extra,"--scene",scene,"--scale",str(scale),"--seconds",str(args.seconds),
                "--warmup",str(args.warmup),"--output",str(dest)],dest,ram,validator=validate)
            row.update(variant=variant,fork=fork,output=str(dest));rows.append(row)
            runner.save_json(args.output/"runs.json",rows)
            print(f"{variant} {scene}/{scale} #{fork}: {row['meanMs']:.4f}ms completed; CPU {row['cpuPercentMachine']:.2f}%; WS {row['sampledMedianWorkingSetMiB']:.1f}MiB",flush=True)
summary=[];quality=[]
for scene,scale in cases:
    entry={"scene":scene,"scale":scale}
    frame_shape=None
    for variant in variants:
        group=[r for r in rows if (r["scene"],r["scale"],r["variant"])==(scene,scale,variant)]
        captures=[Path(r["output"]).joinpath("scene.rgba").read_bytes() for r in group]
        assert all(c==captures[0] for c in captures),f"Unstable capture: {scene}/{scale}/{variant}"
        for r in group:
            shape=(r["width"],r["height"],r["fontSha256"])
            if frame_shape is None:frame_shape=shape
            assert shape==frame_shape,"Different frame dimensions or font between workers"
        entry[variant]={key:statistics.median(r[key] for r in group) for key in
            ["meanMs","recordingMs","completionMs","p95Ms","cpuMsPerFrame","cpuPercentMachine","sampledMedianWorkingSetMiB","sampledMedianPrivateMiB","allocatedBytesPerFrame"]}
    for variant in ["vulkan","direct2d"]:
        entry[variant+"RatioToVulkan"]=entry[variant]["meanMs"]/entry["original-vulkan"]["meanMs"]
        entry[variant+"RatioToAngle"]=entry[variant]["meanMs"]/entry["original-angle"]["meanMs"]
        for reference in ["original-vulkan","original-angle"]:
            a=next(r for r in rows if (r["scene"],r["scale"],r["variant"])==(scene,scale,variant))
            b=next(r for r in rows if (r["scene"],r["scale"],r["variant"])==(scene,scale,reference))
            quality.append({"scene":scene,"scale":scale,"variant":variant,"reference":reference,
                **runner.quality_compare(Path(a["output"])/"scene.rgba",Path(b["output"])/"scene.rgba",a["width"],a["height"])})
    summary.append(entry)
gates={}
for variant in ["vulkan","direct2d"]:
    ratios=[max(r[variant+"RatioToVulkan"],r[variant+"RatioToAngle"]) for r in summary]
    mean=math.exp(statistics.mean(map(math.log,ratios)))
    gates[variant]={"geometricMeanToFasterOriginal":mean,"worstRatio":max(ratios),"performancePassed":mean<=1 and max(ratios)<=1.1}
gates["qualityPassed"]=all(r["passed"] for r in quality)
gates["formalCollection"]=args.forks>=3 and args.seconds>=5 and args.warmup>=3
gates["releaseEligible"]=gates["qualityPassed"] and all(gates[v]["performancePassed"] for v in ["vulkan","direct2d"]) and gates["formalCollection"]
for name,digest in files.items():assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==digest,"Measured binary changed"
for name,digest in source.items():assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==digest,"Product source changed"
manifest["complete"]=True
for name,data in [("manifest",manifest),("summary",summary),("quality",quality),("gate",gates)]:runner.save_json(args.output/(name+".json"),data)
print(json.dumps(gates,indent=2),flush=True)
raise SystemExit(0 if gates["releaseEligible"] else 2)
