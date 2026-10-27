"""Build one source candidate and freeze source/binary provenance before timing."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser()
p.add_argument("--output",type=Path,required=True)
args=p.parse_args();out=args.output.resolve()
if out.exists():raise SystemExit("Fresh candidate output required.")
out.mkdir(parents=True)
def sources():
    files=[ROOT/"Directory.Build.props",ROOT/"tests/AvaloniaControls/ControlScene.cs"]
    for directory in [ROOT/"src",ROOT/"examples/AvaloniaAot"]:
        files += [f for f in directory.rglob("*") if f.is_file() and not {"bin","obj"}.intersection(f.parts)
                  and f.suffix in (".cs",".csproj",".props",".targets",".comp",".spv")]
    return {str(f.relative_to(ROOT)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(set(files))}
before=sources()
command=["dotnet","publish","examples/AvaloniaAot/SimdSkia/AvaloniaAot.SimdSkia.csproj","-c","Release","-r","win-x64",
         "-p:ComparisonIsa=portable","-p:UsePackedSimdSkia=false","-o",str(out)]
with (out/"build.log").open("w",encoding="utf-8") as log:
    result=subprocess.run(command,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
if result.returncode:raise SystemExit(result.returncode)
if sources()!=before:raise SystemExit("Source changed during publish; candidate is not eligible.")
manifest={"command":command,"sourceSha256":before,"runtimeSha256":
    {str(f.relative_to(out)):hashlib.sha256(f.read_bytes()).hexdigest() for f in out.rglob("*") if f.is_file() and f.suffix in (".exe",".dll",".json",".ttf")}}
(out/"build-manifest.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
print("PASS frozen portable AOT candidate",out,flush=True)
