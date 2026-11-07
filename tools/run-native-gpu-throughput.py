"""Unpaced CPU/Direct2D throughput. Every GPU measurement includes queue drain.

This JIT rectangle workload complements, and must not be merged into, the
Native AOT Avalonia window comparison. CPU raster excludes OS presentation.
"""
import argparse,hashlib,json,os,statistics,subprocess,time
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',type=Path,required=True);p.add_argument('--forks',type=int,default=3);a=p.parse_args()
root=Path(__file__).resolve().parents[1]
if os.name!='nt' or not 1<=a.forks<=10 or a.output.exists():p.error('Windows hardware, fresh output and 1..10 forks required.')
a.output.mkdir(parents=True);env=os.environ.copy();sdk=root/'artifacts/dotnet-sdk-10.0.403';env['DOTNET_ROOT']=str(sdk)
assembly=root/'tests/GpuPreferred/bin/Release/net10.0/SimdSkia.GpuPreferred.Tests.dll'
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
manifest=dict(complete=False,measuredAtUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),forks=a.forks,warmupSeconds=3,minimumMeasurementSeconds=2,
    assemblies={f.name:sha(f) for f in assembly.parent.glob('*.dll')},harnessSha256=sha(Path(__file__)),sourceSha256=sha(root/'tests/GpuPreferred/NativeThroughput.cs'),
    scope='JIT, 1000x720, integer-edged AA SourceOver rectangles, CPU raster only vs GPU render and native present; complete queue drain included, pixels checked outside timing; not a real Avalonia page or Google Skia comparison.')
(a.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');runs=[]
cases=[(mode,count) for count in (1,64,512) for mode in ('cpu','queued','synchronous')]
for fork in range(a.forks):
    order=cases[fork%len(cases):]+cases[:fork%len(cases)]
    for mode,count in order:
        output=(a.output/f'{mode}-{count}-{fork}.json').resolve()
        with output.with_suffix('.log').open('w',encoding='utf-8') as log:
            result=subprocess.run([str(sdk/'dotnet.exe'),str(assembly),'--native-throughput',mode,str(count),str(output)],env=env,stdout=log,stderr=subprocess.STDOUT,timeout=45)
        if result.returncode:raise SystemExit(output.with_suffix('.log').read_text())
        d=json.loads(output.read_text());assert d['pendingFrames']==0 and d['maxPixelError']<=2 and d['frames']>=32 and d['completedBatchMilliseconds']>=2000
        runs.append(dict(fork=fork,**d));(a.output/'runs.json').write_text(json.dumps(runs,indent=2)+'\n')
        print(mode,count,fork,f"completed={d['millisecondsPerCompletedFrame']:.4f}ms CPU={d['cpuMillisecondsPerFrame']:.4f}ms/frame maxerr={d['maxPixelError']}",flush=True)
summary=[]
for mode,count in cases:
    group=[r for r in runs if r['mode']==mode and r['rectangles']==count];row=dict(mode=mode,rectangles=count)
    for key in ['millisecondsPerCompletedFrame','cpuMillisecondsPerFrame','allocatedBytesPerFrame']:
        values=[r[key] for r in group];row[key]=statistics.median(values);row[key+'Min']=min(values);row[key+'Max']=max(values)
    summary.append(row)
(a.output/'summary.json').write_text(json.dumps(summary,indent=2)+'\n');manifest['complete']=True
(a.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print(f'PASS {len(runs)} serial processes; GPU work drained, small-scene pixel gates passed.',flush=True)
