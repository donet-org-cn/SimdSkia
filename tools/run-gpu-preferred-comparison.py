"""Serial real-window comparison of GPU preference against a pre-change executable.

Includes completion time, process CPU and sampled process memory. Does not treat
window presentation success as a pixel match against original Google Skia.
"""
import argparse,hashlib,json,os,statistics,subprocess,time
from pathlib import Path

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--before',type=Path,required=True)
p.add_argument('--after',type=Path,default=Path('examples/CrossPlatform/bin/Release/net10.0/CrossPlatform.exe'))
p.add_argument('--output',type=Path,required=True)
p.add_argument('--forks',type=int,default=3)
p.add_argument('--seconds',type=float,default=5)
p.add_argument('--warmup',type=float,default=2)
p.add_argument('--font',type=Path,default=Path('baseline/fonts/ZCOOLXiaoWei-Regular.ttf'))
a=p.parse_args();root=Path(__file__).resolve().parents[1]
if os.name!='nt':p.error('This native-window comparison requires Windows hardware.')
if not 1<=a.forks<=10 or not 1<=a.seconds<=60 or not 0<=a.warmup<=60:p.error('Invalid measurement duration/forks.')
if a.output.exists():p.error('Use a new output directory.')
a.output.mkdir(parents=True);env=os.environ.copy();env['DOTNET_ROOT']=str(root/'artifacts/dotnet-sdk-10.0.403')
env['SIMDSKIA_BENCH_WARMUP_SECONDS']=str(a.warmup);env['SIMDSKIA_BENCH_MEASURE_SECONDS']=str(a.seconds)
modes=[('before-cpu',a.before,[]),('after-cpu',a.after,['--cpu']),
       ('before-direct2d',a.before,['--gpu','direct2d','--allow-cpu-fallback']),('after-auto',a.after,[]),
       ('before-vulkan',a.before,['--gpu','vulkan','--allow-cpu-fallback']),('after-vulkan',a.after,['--gpu','vulkan','--allow-cpu-fallback'])]
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
manifest=dict(measuredAtUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),forks=a.forks,seconds=a.seconds,warmup=a.warmup,
    font=str(a.font.resolve()),fontSha256=sha(a.font),before=str(a.before.resolve()),after=str(a.after.resolve()),
    beforeExeSha256=sha(a.before),afterExeSha256=sha(a.after),baselineSource='f7f0185',
    beforeAssemblies={f.name:sha(f) for f in sorted(a.before.parent.glob('*.dll'))},
    afterAssemblies={f.name:sha(f) for f in sorted(a.after.parent.glob('*.dll'))},
    harnessSha256=sha(Path(__file__)),globalJsonSha256=sha(root/'global.json'),
    scope='Real Fluent window, fixed font/DPI/1000x720; timer-driven button content changes, GPU actual completion; not offscreen peak FPS or original-Skia parity.',
    sourceFiles={str(f.relative_to(root)).replace('\\','/'):sha(f) for folder in ['src','examples/CrossPlatform']
                 for f in (root/folder).rglob('*') if f.is_file() and f.suffix in ('.cs','.csproj','.spv') and not {'bin','obj'}.intersection(f.parts)},complete=False)
(a.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
runs=[]
for fork in range(a.forks):
    order=modes[fork%len(modes):]+modes[:fork%len(modes)]
    for name,exe,flags in order:
        out=(a.output/f'{name}-{fork}').resolve();out.mkdir()
        command=[str(exe.resolve()),'--benchmark','--controls','--font',str(a.font.resolve()),'--output',str(out),*flags]
        with (out/'stdout.log').open('w',encoding='utf-8') as log:
            result=subprocess.run(command,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=a.seconds+a.warmup+35)
        if result.returncode:raise SystemExit(f'{name} failed: {result.returncode}; '+(out/'stdout.log').read_text())
        metrics=json.loads((out/'benchmark.json').read_text(encoding='utf-8'));assert metrics['sampleCountSufficient'] and metrics['timedFrames']>=60
        assert not metrics['diagnosticCaptureDuringMeasurement']
        if name not in ('before-cpu','after-cpu'):assert metrics['gpuCommands']>0 and metrics['gpuCompletedBatches']>0
        if name=='after-auto':assert metrics['selectedRenderer']=='Direct2D native swapchain' and metrics['initializationFailures']==0
        runs.append(dict(variant=name,fork=fork,**metrics));(a.output/'runs.json').write_text(json.dumps(runs,indent=2)+'\n')
        assert len({r['nativeAot'] for r in runs})==1,'Do not mix JIT and Native AOT.'
        print(name,fork,f"mean={metrics['drawingMeanMs']:.3f}ms CPU={metrics['cpuPercentMachine']:.2f}% peakWS={metrics['peakWorkingSetBytes']/1048576:.2f}MiB",flush=True)
rows=[]
for name,_,_ in modes:
    group=[r for r in runs if r['variant']==name]
    row=dict(variant=name,forks=len(group))
    for key in ['drawingMeanMs','drawingP50Ms','drawingP95Ms','cpuPercentMachine','peakWorkingSetBytes','peakPrivateBytes']:
        values=[r[key] for r in group];row[key]=statistics.median(values);row[key+'Min']=min(values);row[key+'Max']=max(values)
    for key,numerator,denominator in [('cpuMsPerFrame','cpuMilliseconds','timedFrames'),('allocatedBytesPerFrame','allocatedBytes','timedFrames')]:
        row[key]=statistics.median(r[numerator]/r[denominator] for r in group)
    rows.append(row)
(a.output/'summary.json').write_text(json.dumps(rows,indent=2)+'\n');manifest['complete']=True
(a.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(f'PASS {len(runs)} serial real-window processes. Pixel quality gates are separate and unchanged.',flush=True)
