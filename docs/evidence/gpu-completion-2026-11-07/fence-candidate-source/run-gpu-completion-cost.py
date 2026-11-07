"""Serial Native AOT GPU/CPU comparison, including queue-drain and CPU cost.

Visible current-window comparisons and historical off-screen comparisons remain
separate. Frame recording time is not labeled GPU execution/display latency.
"""
import argparse,hashlib,json,os,statistics,subprocess,time
from pathlib import Path

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--after',type=Path,required=True)
p.add_argument('--before',type=Path)
p.add_argument('--output',type=Path,required=True)
p.add_argument('--forks',type=int,default=3)
p.add_argument('--warmup',type=float,default=8)
p.add_argument('--seconds',type=float,default=8)
p.add_argument('--presentation-probe',action='store_true')
p.add_argument('--native-only',action='store_true')
p.add_argument('--state-probe',action='store_true')
p.add_argument('--fence-probe',action='store_true')
a=p.parse_args();root=Path(__file__).resolve().parents[1]
if os.name!='nt' or not 1<=a.forks<=10 or not 1<=a.seconds<=60 or not 0<=a.warmup<=60:p.error('Windows hardware, valid duration/forks required.')
if a.output.exists():p.error('Use a new output directory.')
a.output.mkdir(parents=True);font=root/'baseline/fonts/ZCOOLXiaoWei-Regular.ttf'
sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
env=os.environ.copy();env['DOTNET_ROOT']=str(root/'artifacts/dotnet-sdk-10.0.403')
env['SIMDSKIA_BENCH_WARMUP_SECONDS']=str(a.warmup);env['SIMDSKIA_BENCH_MEASURE_SECONDS']=str(a.seconds)
modes=[('visible-cpu',a.after,['--visible-benchmark','--cpu']),
       ('visible-queued',a.after,['--visible-benchmark']),
       ('visible-synchronous',a.after,['--visible-benchmark','--synchronous-gpu']),
       ('visible-vulkan',a.after,['--visible-benchmark','--gpu','vulkan','--allow-cpu-fallback'])]
if a.presentation_probe:modes=[('visible-cpu',a.after,['--visible-benchmark','--cpu']),('visible-queued',a.after,['--visible-benchmark']),('visible-queued-blt',a.after,['--visible-benchmark','--legacy-gpu-swapchain'])]
elif a.fence_probe:modes=[modes[0],modes[1],('visible-flush1-fences',a.after,['--visible-benchmark','--flush1-gpu-fences'])]
elif a.state_probe:modes=[modes[0],modes[1],('visible-uncached-state',a.after,['--visible-benchmark','--uncached-gpu-state'])]
elif a.native_only:modes=modes[:3]
elif a.before:modes += [('hidden-before-direct2d',a.before,[]),('hidden-after-direct2d',a.after,[])]
manifest=dict(complete=False,beforeSource='a54485f',measuredAtUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),forks=a.forks,warmup=a.warmup,seconds=a.seconds,
    fontSha256=sha(font),executables={str(exe.resolve()):sha(exe) for _,exe,_ in modes},harnessSha256=sha(Path(__file__)),
    sourceFiles={str(f.relative_to(root)).replace('\\','/'):sha(f) for folder in ['src','examples/CrossPlatform','tests/GpuPreferred'] for f in (root/folder).rglob('*') if f.is_file() and f.suffix in ('.cs','.csproj','.spv') and not {'bin','obj'}.intersection(f.parts)},
    scope='Native AOT only; real 1000x720 Fluent window; timer-paced; visible and historical hidden groups separate; GPU queue fully drained; frame CPU time is recording/submission, not GPU latency or display FPS.')
(a.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
runs=[]
for fork in range(a.forks):
    order=modes[fork%len(modes):]+modes[:fork%len(modes)]
    for name,exe,flags in order:
        out=(a.output/f'{name}-{fork}').resolve();out.mkdir()
        command=[str(exe.resolve()),'--benchmark','--controls','--font',str(font),'--output',str(out),*flags]
        with (out/'stdout.log').open('w',encoding='utf-8') as log:
            result=subprocess.run(command,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=a.warmup+a.seconds+45)
        if result.returncode:raise SystemExit(f'{name} failed: {result.returncode}; '+(out/'stdout.log').read_text())
        d=json.loads((out/'benchmark.json').read_text());assert d['nativeAot'] and d['sampleCountSufficient'] and not d['diagnosticCaptureDuringMeasurement']
        if name!='visible-cpu':assert d['gpuCommands']>0 and d['gpuCompletedBatches']>0
        if 'nativePendingFrames' in d and name not in ('visible-cpu','visible-vulkan'):
            assert d['nativePendingFrames']==0 and d['nativeCompletedFrames']>=d['nativeSubmittedFrames']>0
            assert d['completionPolls']==0 and d['initializationFailures']==0
        if a.fence_probe and name=='visible-queued':assert d['nativeTimelineSignals']>0,'The fence probe requires actual hardware timeline signals.'
        if a.fence_probe and name=='visible-flush1-fences':assert d['nativeTimelineSignals']==0 and d['nativeExplicitCompletionFlushes']>0
        runs.append(dict(variant=name,fork=fork,command=command,**d));(a.output/'runs.json').write_text(json.dumps(runs,indent=2)+'\n')
        print(name,fork,f"frame-host={d['drawingMeanMs']:.3f}ms CPU={d['cpuPercentMachine']:.3f}% cpu/frame={d['cpuMilliseconds']/d['timedFrames']:.3f}ms WS={d['peakWorkingSetBytes']/1048576:.2f}MiB",flush=True)
rows=[]
for name,_,_ in modes:
    group=[r for r in runs if r['variant']==name];row=dict(variant=name,forks=len(group))
    for key in ['drawingMeanMs','drawingP50Ms','drawingP95Ms','cpuPercentMachine','peakWorkingSetBytes','peakPrivateBytes']:
        values=[r[key] for r in group];row[key]=statistics.median(values);row[key+'Min']=min(values);row[key+'Max']=max(values)
    for key,numerator,denominator in [('cpuMsPerFrame','cpuMilliseconds','timedFrames'),('allocatedBytesPerFrame','allocatedBytes','timedFrames'),('completedFramesPerSecond','timedFrames','measurementSeconds')]:row[key]=statistics.median(r[numerator]/r[denominator] for r in group)
    row['nativeStateChangesPerFrame']=statistics.median(r.get('nativeStateChanges',0)/r['timedFrames'] for r in group)
    rows.append(row)
(a.output/'summary.json').write_text(json.dumps(rows,indent=2)+'\n');manifest['complete']=True
(a.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print(f'PASS {len(runs)} serial AOT processes, all current native queues drained.',flush=True)
