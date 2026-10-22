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
p.add_argument('--before-executable',type=Path,help='Frozen previous build; rotate before/after order in separate processes.')
p.add_argument('--output',type=Path,default=Path('artifacts/desktop-gpu-performance/final'))
fonts=p.add_mutually_exclusive_group()
fonts.add_argument('--font',type=Path)
fonts.add_argument('--system-fonts',action='store_true',help='Use installed system fonts, without an embedded fixture.')
p.add_argument('--runs',type=int,default=3)
p.add_argument('--scene',choices=['simple','controls'],default='controls')
p.add_argument('--backends',nargs='+',choices=['cpu','direct2d','vulkan'],default=['cpu','direct2d','vulkan'])
args=p.parse_args()
if args.runs<1:raise SystemExit('runs must be positive')
if (args.output/'manifest.json').exists():raise SystemExit('Choose a new output directory to preserve the previous measurement.')
args.output.mkdir(parents=True,exist_ok=True)
binary=args.executable.resolve();font=None if args.system_fonts else (args.font or Path('baseline/fonts/ZCOOLXiaoWei-Regular.ttf')).resolve()
engines={'after':binary}
if args.before_executable:engines={'before':args.before_executable.resolve(),**engines}
measured_files=sorted({path for executable in engines.values() for path in [executable,*executable.parent.glob('*.dll'),*executable.parent.glob('*.deps.json'),*executable.parent.glob('*.runtimeconfig.json')]})
hashes={str(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in measured_files}
manifest={'executable':str(binary),'engines':{key:str(value) for key,value in engines.items()},'executableSha256':hashes[str(binary)],'runtimeFileSha256':hashes,'fontMode':'system' if args.system_fonts else 'fixed file','fontSha256':hashlib.sha256(font.read_bytes()).hexdigest() if font else None,'runs':args.runs,'scene':args.scene,'backends':args.backends,'windowPosition':'offscreen on Windows; measures render/present service time, not visible monitor FPS','note':'GPU timers include fence/event completion; capture disabled during timed frames. CPU glyph atlas construction and CPU layers are recorded, not called GPU rasterization.'}
manifest['runtimeEnvironment']={key:os.environ.get(key) for key in ['DOTNET_TieredCompilation','DOTNET_TieredPGO','DOTNET_EnableHWIntrinsic','COMPlus_TieredCompilation','COMPlus_TieredPGO','COMPlus_EnableHWIntrinsic']}
(args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
rows=[]
variants=[(engine,backend) for backend in args.backends for engine in engines]
for run in range(args.runs):
    order=variants[run%len(variants):]+variants[:run%len(variants)]
    for engine,backend in order:
        name=f'{engine}-{backend}-{run+1}' if args.before_executable else f'{backend}-{run+1}'
        dest=args.output/name;dest.mkdir()
        command=[str(engines[engine]),'--benchmark','--output',str(dest)]
        if font:command+=['--font',str(font)]
        if args.scene=='controls':command+=['--controls']
        if backend!='cpu':command+=['--gpu',backend,'--allow-cpu-fallback']
        result=subprocess.run(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=90,creationflags=0x08000000 if os.name=='nt' else 0)
        (dest/'run.log').write_bytes(result.stdout)
        if result.returncode:raise SystemExit(f'{backend} run {run+1} failed: see {dest}/run.log')
        row=json.loads((dest/'benchmark.json').read_text(encoding='utf-8'));row['run']=run+1;row['engine']=engine;rows.append(row)
        if row.get('gpuTimestampProfilingEnabled',False):raise SystemExit('Serializing timestamp diagnostics must be disabled during measurements.')
        if any(row[key]!=rows[0][key] for key in ['width','height','scaling','scene','nativeAot','os','architecture']):raise SystemExit('Window size/DPI/scene/runtime changed during measurement.')
        print(f"{engine} {backend} #{run+1}: P50={row['drawingP50Ms']:.3f}ms P95={row['drawingP95Ms']:.3f}ms CPU={row['cpuPercentMachine']:.2f}% WS={row['peakWorkingSetBytes']/1048576:.1f}MiB",flush=True)
summary=[]
for engine,backend in variants:
    group=[r for r in rows if r['backend'].lower()==backend and r['engine']==engine]
    summary.append({'engine':engine,'backend':backend,'runs':len(group),'allSampleCountsSufficient':all(r['sampleCountSufficient'] for r in group),**{key:statistics.median(r[key] for r in group) for key in ['drawingP50Ms','drawingP95Ms','drawingMeanMs','cpuPercentMachine','cpuPercentOneCore','peakWorkingSetBytes','peakPrivateBytes']},'allocationBytesPerFrame':statistics.median(r['allocatedBytes']/r['timedFrames'] for r in group),'frames':sum(r['timedFrames'] for r in group),'cpuFallbackCommands':sum(r['cpuFallbackCommands'] for r in group)})
(args.output/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
for path,digest in hashes.items():
    if hashlib.sha256(Path(path).read_bytes()).hexdigest()!=digest:raise SystemExit(f'Runtime file changed during measurement: {path}')
print('PASS completed measurements; summary.json and raw per-frame samples retained')
