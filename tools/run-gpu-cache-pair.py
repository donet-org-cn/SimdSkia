"""Paired frozen managed GPU workers; actual Avalonia frames include completion and BGRA readback."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import statistics

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('parent',ROOT/'tools/run-avalonia-aot.py');parent=importlib.util.module_from_spec(spec);spec.loader.exec_module(parent)
p=argparse.ArgumentParser()
for name in ['before','after','output']:p.add_argument('--'+name,type=Path,required=True)
a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True)
workers={name:getattr(a,name).resolve() for name in ['before','after']}
hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for worker in workers.values() for f in worker.parent.rglob('*') if f.is_file() and f.suffix in ['.exe','.dll','.json']}
manifest=dict(method=__doc__,files=hashes,forks=3,warmupSeconds=3,measurementSeconds=5,complete=False)
parent.save_json(a.output/'manifest.json',manifest)
ram=parent.physical_memory();rows=[];device=None
def validate(r):
    global device
    if r['mode']!='JIT GPU complete-frame readback' or r['nativeAot'] or r['completionMs']<=0:raise ValueError('Not a completed GPU JIT worker')
    gpu=r['device'].split(':')[-1].strip()
    if device is None:device=gpu
    if gpu!=device:raise ValueError('GPU changed')
for fork in range(3):
    for scene,scale in [('controls',1),('controls',1.5),('scroll',1)]:
        order=[(version,backend) for version in workers for backend in ['vulkan','direct2d']];order=order[fork%4:]+order[:fork%4]
        for version,backend in order:
            dest=(a.output/f'{version}-{backend}-{scene}-{scale}-{fork}').resolve();dest.mkdir()
            row=parent.measure_process(workers[version],['--backend',backend,'--scene',scene,'--scale',str(scale),'--warmup','3','--seconds','5','--output',str(dest)],dest,ram,validator=validate)
            row.update(version=version,fork=fork,output=str(dest));rows.append(row);parent.save_json(a.output/'runs.json',rows)
            print(f'{version} {backend} {scene}/{scale} #{fork}: {row["meanMs"]:.4f} ms; WS {row["sampledMedianWorkingSetMiB"]:.2f}',flush=True)
summary=[]
for scene,scale in [('controls',1),('controls',1.5),('scroll',1)]:
    for backend in ['vulkan','direct2d']:
        row=dict(scene=scene,scale=scale,backend=backend);blobs=[]
        for version in workers:
            group=[r for r in rows if (r['scene'],r['scale'],r['backend'],r['version'])==(scene,scale,backend,version)]
            row[version]={k:statistics.median(r[k] for r in group) for k in ['meanMs','recordingMs','completionMs','cpuMsPerFrame','cpuPercentMachine','sampledMedianWorkingSetMiB','sampledMedianPrivateMiB','allocatedBytesPerFrame']}
            blobs.extend((Path(r['output'])/'scene.rgba').read_bytes() for r in group)
        assert len({len(b) for b in blobs})==1
        row['beforeAfterExact']=all(b==blobs[0] for b in blobs);row['ratio']=row['after']['meanMs']/row['before']['meanMs'];summary.append(row)
for path,digest in hashes.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,'Frozen binary changed'
manifest['complete']=True;parent.save_json(a.output/'manifest.json',manifest);parent.save_json(a.output/'summary.json',summary)
print(json.dumps(summary,indent=2));raise SystemExit(0 if all(r['beforeAfterExact'] for r in summary) else 2)
