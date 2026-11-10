"""Rotating original / frozen-before / current scalar / current SIMD CPU workers.

Each frame completes synchronously. Same input, format, dimensions and font.
Three processes per variant, five 100 ms batches, 500 ms warmup per scene.
"""
import argparse
import array
import hashlib
import json
import math
import os
from pathlib import Path
import statistics
import subprocess

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output',type=Path,required=True)
parser.add_argument('--before',type=Path,required=True)
args=parser.parse_args()
output=args.output.resolve()
output.mkdir(parents=True,exist_ok=False)
workers={
    'original':ROOT/'benchmarks/Reference/bin/Release/net10.0/SimdSkia.Benchmark.Reference.dll',
    'before':args.before.resolve(),
    'scalar':ROOT/'benchmarks/Managed/bin/Release/net10.0/SimdSkia.Benchmark.Managed.dll',
    'simd':ROOT/'benchmarks/Managed/bin/Release/net10.0/SimdSkia.Benchmark.Managed.dll',
}
files={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for parent in {p.parent for p in workers.values()}
       for p in parent.rglob('*') if p.is_file() and p.suffix in ('.dll','.json','.exe')}
env=dict(os.environ,DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0')
common=['--font','baseline/fonts/ZCOOLXiaoWei-Regular.ttf','--sizes','256x256,1024x768',
        '--samples','5','--sample-ms','100','--warmup-ms','500','--fixtures',str(output/'fixtures')]
def save(name,value):
    (output/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')

manifest=dict(method=__doc__,hashes=files,environment={k:env[k] for k in ['DOTNET_TieredCompilation','DOTNET_ReadyToRun','DOTNET_gcServer']},complete=False)
save('manifest.json',manifest)
subprocess.run(['dotnet',str(workers['original']),'--make-fixtures',*common],cwd=ROOT,env=env,check=True)
runs=[]
for fork in range(3):
    order=list(workers);order=order[fork:]+order[:fork]
    for variant in order:
        dest=output/f'{variant}-{fork}'
        with (output/f'{variant}-{fork}.log').open('wb') as log:
            subprocess.run(['dotnet',str(workers[variant]),'--engine','original' if variant=='original' else 'scalar' if variant=='scalar' else 'simd',
                            '--fork',str(fork),'--out',str(dest),*common],cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=240)
        worker=json.loads((dest/'results.json').read_text(encoding='utf-8-sig'))
        for row in worker['Workloads']:
            samples=row['Samples'];ops=sum(s['Operations'] for s in samples)
            elapsed=sum(s['TotalMilliseconds'] for s in samples);cpu=sum(s['CpuMilliseconds'] for s in samples)
            ticks=array.array('q');ticks.frombytes((dest/row['LatencyFile']).read_bytes())
            ordered=sorted(ticks);p95=ordered[min(len(ordered)-1,int((len(ordered)-1)*.95))]*1000/worker['Runtime']['StopwatchFrequency']
            runs.append(dict(variant=variant,fork=fork,scene=row['Scene'],width=row['Width'],height=row['Height'],
                milliseconds=statistics.median(s['TotalMilliseconds']/s['Operations'] for s in samples),
                p95Milliseconds=p95,cpuMilliseconds=cpu/ops,cpuPercentMachine=cpu/elapsed/worker['Runtime']['LogicalProcessors']*100,
                allocatedBytes=sum(s['AllocatedBytes'] for s in samples)/ops,
                workingSetMiB=row['WarmResident']['WorkingSetBytes']/1048576,privateMiB=row['WarmResident']['PrivateBytes']/1048576,
                encodedBytes=row['EncodedBytes'],allowedError=row['AllowedPixelError'],compressionComparable=row['CompressionComparable']))
        save('runs.json',runs)
        print(variant,fork,'complete',flush=True)
summary=[]
for scene,width,height in dict.fromkeys((r['scene'],r['width'],r['height']) for r in runs):
    entry=dict(scene=scene,width=width,height=height)
    matching=[r for r in runs if (r['scene'],r['width'],r['height'])==(scene,width,height)]
    frames={}
    for variant in workers:
        group=[r for r in matching if r['variant']==variant]
        entry[variant]={k:statistics.median(r[k] for r in group) for k in ['milliseconds','p95Milliseconds','cpuMilliseconds','cpuPercentMachine','allocatedBytes','workingSetMiB','privateMiB','encodedBytes']}
        captures=[(output/f'{variant}-{f}'/f'{scene}-{width}x{height}.rgba').read_bytes() for f in range(3)]
        assert all(frame==captures[0] for frame in captures),'Unstable validation capture'
        frames[variant]=captures[0]
    assert frames['simd']==frames['before'],'Optimization changed fixed-scene pixels: '+scene
    delta=[abs(x-y) for x,y in zip(frames['simd'],frames['original'])]
    entry.update(beforeAfterExact=True,scalarSimdExact=frames['scalar']==frames['simd'],maximumError=max(delta),rmse=math.sqrt(sum(d*d for d in delta)/len(delta)),
                 allowedError=matching[0]['allowedError'],nativeQualityPassed=max(delta)<=matching[0]['allowedError'],compressionComparable=matching[0]['compressionComparable'],
                 afterOverBefore=entry['simd']['milliseconds']/entry['before']['milliseconds'],afterOverOriginal=entry['simd']['milliseconds']/entry['original']['milliseconds'])
    summary.append(entry)
for name,digest in files.items():
    assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==digest,'Measured binary changed'
save('summary.json',summary)
manifest['complete']=True;save('manifest.json',manifest)
