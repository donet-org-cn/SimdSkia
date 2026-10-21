"""Measure the same real Avalonia window in separate CPU/Direct2D/Vulkan processes."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import statistics
import subprocess

p=argparse.ArgumentParser()
p.add_argument('--executable',type=Path,default=Path('examples/CrossPlatform/bin/Release/net10.0/CrossPlatform.exe'))
p.add_argument('--output',type=Path,default=Path('artifacts/desktop-gpu-performance/final'))
p.add_argument('--font',type=Path,default=Path('baseline/fonts/ZCOOLXiaoWei-Regular.ttf'))
p.add_argument('--runs',type=int,default=3)
p.add_argument('--scene',choices=['simple','controls'],default='controls')
p.add_argument('--backends',nargs='+',choices=['cpu','direct2d','vulkan'],default=['cpu','direct2d','vulkan'])
args=p.parse_args()
if args.runs<1:raise SystemExit('runs must be positive')
if (args.output/'manifest.json').exists():raise SystemExit('Choose a new output directory to preserve the previous measurement.')
args.output.mkdir(parents=True,exist_ok=True)
binary=args.executable.resolve();font=args.font.resolve()
measured_files=sorted({binary,*binary.parent.glob('*.dll'),*binary.parent.glob('*.deps.json'),*binary.parent.glob('*.runtimeconfig.json')})
hashes={str(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in measured_files}
manifest={'executable':str(binary),'executableSha256':hashes[str(binary)],'runtimeFileSha256':hashes,'fontSha256':hashlib.sha256(font.read_bytes()).hexdigest(),'runs':args.runs,'scene':args.scene,'backends':args.backends,'windowPosition':'offscreen on Windows; measures render/present service time, not visible monitor FPS','note':'GPU timers include fence/event completion; capture disabled during timed frames. CPU glyph atlas construction and CPU layers are recorded, not called GPU rasterization.'}
(args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
rows=[]
for run in range(args.runs):
    order=args.backends[run%len(args.backends):]+args.backends[:run%len(args.backends)]
    for backend in order:
        dest=args.output/f'{backend}-{run+1}';dest.mkdir()
        command=[str(binary),'--benchmark','--font',str(font),'--output',str(dest)]
        if args.scene=='controls':command+=['--controls']
        if backend!='cpu':command+=['--gpu',backend,'--allow-cpu-fallback']
        result=subprocess.run(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=90,creationflags=0x08000000 if os.name=='nt' else 0)
        (dest/'run.log').write_bytes(result.stdout)
        if result.returncode:raise SystemExit(f'{backend} run {run+1} failed: see {dest}/run.log')
        row=json.loads((dest/'benchmark.json').read_text(encoding='utf-8'));row['run']=run+1;rows.append(row)
        print(f"{backend} #{run+1}: P50={row['drawingP50Ms']:.3f}ms P95={row['drawingP95Ms']:.3f}ms CPU={row['cpuPercentMachine']:.2f}% WS={row['peakWorkingSetBytes']/1048576:.1f}MiB",flush=True)
summary=[]
for backend in args.backends:
    group=[r for r in rows if r['backend'].lower()==backend]
    summary.append({'backend':backend,'runs':len(group),'allSampleCountsSufficient':all(r['sampleCountSufficient'] for r in group),**{key:statistics.median(r[key] for r in group) for key in ['drawingP50Ms','drawingP95Ms','drawingMeanMs','cpuPercentMachine','cpuPercentOneCore','peakWorkingSetBytes','peakPrivateBytes']},'allocationBytesPerFrame':statistics.median(r['allocatedBytes']/r['timedFrames'] for r in group),'frames':sum(r['timedFrames'] for r in group),'cpuFallbackCommands':sum(r['cpuFallbackCommands'] for r in group)})
(args.output/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
for path,digest in hashes.items():
    if hashlib.sha256(Path(path).read_bytes()).hexdigest()!=digest:raise SystemExit(f'Runtime file changed during measurement: {path}')
print('PASS completed measurements; summary.json and raw per-frame samples retained')
