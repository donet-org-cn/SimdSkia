"""Frozen CPU/JIT stroke API workers; three serial rotating forks, no builds while timing."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import statistics
import subprocess
p=argparse.ArgumentParser()
for name in ['before','after','original','output']:p.add_argument('--'+name,type=Path,required=True)
a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True);workers={v:getattr(a,v).resolve() for v in ['original','before','after']}
files={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for d in workers.values() for f in d.rglob('*') if f.is_file() and f.suffix in ['.dll','.json']}
manifest=dict(files=files,backend='CPU .NET 10 JIT',warmupSeconds=.4,measurementSeconds=.8,forks=3,drawsPerFrame=32,outlineInputPoints=128,before='New stroke API initial implementation, before normal-buffer/destination reuse optimization; not published 0.1.1.',complete=False)
write=lambda path,obj:path.write_text(json.dumps(obj,indent=2)+'\n',encoding='utf-8')
write(a.output/'manifest.json',manifest);runs=[]
for fork in range(3):
    order=list(workers);order=order[fork:]+order[:fork]
    for variant in order:
        out=a.output/f'{variant}-{fork}';dll=workers[variant]/('Original.dll' if variant=='original' else 'Managed.dll')
        subprocess.run(['dotnet',str(dll),str(out)],check=True);runs.append(dict(variant=variant,fork=fork,**json.loads((out/'metrics.json').read_text(encoding='utf-8'))));write(a.output/'runs.json',runs)
summary=[]
for scene in ['outline-hot','outline-changing','round-lines']:
    row=dict(scene=scene)
    for variant in workers:
        group=[r for run in runs if run['variant']==variant for r in run['rows'] if r['scene']==scene]
        row[variant]={k:statistics.median(r[k] for r in group) for k in ['msPerFrame','cpuMsPerFrame','allocatedBytesPerFrame','workingSetMiB','privateMiB']}
    if scene=='round-lines':
        blobs={(v,f):(a.output/f'{v}-{f}'/(scene+'.bgra')).read_bytes() for v in workers for f in range(3)}
        assert all(len(b)==128*128*4 for b in blobs.values());assert len({blobs[v,f] for v in ['before','after'] for f in range(3)})==1,'Changed managed pixels'
        assert len({blobs['original',f] for f in range(3)})==1,'Unstable reference'
        delta=[abs(v-w) for v,w in zip(blobs['original',0],blobs['after',0])];maximum=max(delta);rmse=math.sqrt(sum(d*d for d in delta)/len(delta));row['quality']=dict(beforeAfterExact=True,maximum=maximum,rmse=rmse,passed=maximum<=32 and rmse<=2)
    row['speedup']=row['before']['msPerFrame']/row['after']['msPerFrame'];summary.append(row)
for path,digest in files.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,'Frozen binary changed'
manifest['complete']=True;write(a.output/'manifest.json',manifest);write(a.output/'summary.json',summary);print(json.dumps(summary,indent=2))
raise SystemExit(0 if all(r.get('quality',{}).get('passed',True) for r in summary) else 2)
