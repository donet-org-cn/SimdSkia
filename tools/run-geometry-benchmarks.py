"""Measure three frozen CPU workers. Never build or change a worker while sampling."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import statistics
import subprocess

p=argparse.ArgumentParser()
for name in ['before','after','original','output']:p.add_argument('--'+name,type=Path,required=True)
p.add_argument('--forks',type=int,default=3)
a=p.parse_args()
if a.output.exists():raise SystemExit('Use a new output directory.')
if a.forks<3:raise SystemExit('Use at least three isolated forks.')
a.output.mkdir(parents=True)
directories={k:getattr(a,k).resolve() for k in ['original','before','after']}
files={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for d in directories.values() for p in d.rglob("*") if p.is_file() and p.suffix in ['.dll','.json']}
manifest=dict(backend='CPU; SKBitmap, synchronous drawing; BGRA8888 premul',size=[128,128],drawsPerFrame=dict(lines=32,circles=32,mixed=64,boundsQueries=32),boundsPoints=513,warmupSecondsPerScene=.3,measurementSecondsPerScene=.6,forks=a.forks,files=files,processor=os.environ.get('PROCESSOR_IDENTIFIER'),logicalProcessors=os.cpu_count(),os=platform.platform(),newGpuBenchmark=False)
(a.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
rows=[]
for fork in range(a.forks):
    order=['original','before','after'];order=order[fork%3:]+order[:fork%3]
    for variant in order:
        dll=directories[variant]/('Original.dll' if variant=='original' else 'Managed.dll');out=a.output/f'{variant}-{fork}'
        subprocess.run(['dotnet',str(dll),str(out)],check=True)
        rows.append(dict(variant=variant,fork=fork,**json.loads((out/'results.json').read_text(encoding='utf-8'))))
        (a.output/'runs.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
summary=[]
for scene in ['bounds','lines','circles','mixed']:
    row=dict(scene=scene)
    for variant in directories:
        group=[r for run in rows if run['variant']==variant for r in run['results'] if r['scene']==scene]
        row[variant]={key:statistics.median(r[key] for r in group) for key in ['msPerFrame','cpuMsPerFrame','allocatedBytesPerFrame','workingSetMiB','privateMiB']}
    row['speedupVsBefore']=row['before']['msPerFrame']/row['after']['msPerFrame']
    row['afterOriginalRatio']=row['after']['msPerFrame']/row['original']['msPerFrame']
    if scene!='bounds':
        blobs={(v,f):(a.output/f'{v}-{f}'/(scene+'.rgba')).read_bytes() for v in directories for f in range(a.forks)}
        assert len({blobs[(v,f)] for v in ['before','after'] for f in range(a.forks)})==1, 'Changed managed pixels: '+scene
        assert len({blobs['original',f] for f in range(a.forks)})==1, 'Unstable reference pixels: '+scene
        errors=[abs(x-y) for x,y in zip(blobs['original',0],blobs['after',0])]
        row['quality']=dict(beforeAfterExact=True,maxError=max(errors),rmse=math.sqrt(sum(v*v for v in errors)/len(errors)))
        row['quality']['passed']=row['quality']['maxError']<=32 and row['quality']['rmse']<=2
    summary.append(row)
for path,digest in files.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,'Frozen worker changed'
(a.output/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print(json.dumps(summary,indent=2))
raise SystemExit(2 if any(not r.get('quality',{}).get('passed',True) for r in summary) else 0)
