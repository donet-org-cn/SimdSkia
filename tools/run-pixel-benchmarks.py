"""Nine serial CPU workers, frozen files, rotating order and per-scene medians."""
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
if a.output.exists():raise SystemExit('Use a fresh output directory.')
a.output.mkdir(parents=True);workers={v:getattr(a,v).resolve() for v in ['original','before','after']}
write=lambda f,v:f.write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8')
hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for folder in workers.values() for f in folder.rglob('*') if f.is_file() and f.suffix in ['.dll','.json']}
manifest=dict(backend='CPU JIT',warmupSeconds=.4,measurementSeconds=.8,forks=3,processes=9,files=hashes,before='Initial new pixel API before row-copy/SIMD/swizzle/tap reuse optimization; not the published 0.1.1 package.',complete=False)
write(a.output/'manifest.json',manifest);runs=[]
for fork in range(3):
    order=list(workers);order=order[fork:]+order[:fork]
    for variant in order:
        directory=a.output/f'{variant}-{fork}';dll=workers[variant]/('Original.dll' if variant=='original' else 'Managed.dll')
        subprocess.run(['dotnet',str(dll),str(directory)],check=True,stdout=subprocess.DEVNULL)
        runs.append(dict(variant=variant,fork=fork,rows=json.loads((directory/'results.json').read_text(encoding='utf-8'))));write(a.output/'runs.json',runs)
summary=[]
for scene in ['read-copy-1080p','read-swap-1080p','nearest-512','linear-512']:
    row=dict(scene=scene)
    for variant in workers:
        group=[r for run in runs if run['variant']==variant for r in run['rows'] if r['scene']==scene]
        row[variant]={k:statistics.median(r[k] for r in group) for k in ['msPerFrame','cpuMsPerFrame','cpuPercentOfMachine','allocatedBytesPerFrame','workingSetMiB','privateMiB']}
    blobs={(v,f):(a.output/f'{v}-{f}'/(scene+'.bin')).read_bytes() for v in workers for f in range(3)}
    for v in workers:assert len({blobs[v,f] for f in range(3)})==1,'Unstable output: '+v
    def difference(left,right):
        assert len(left)==len(right)
        if left==right:return dict(maximum=0,rmse=0,changedChannels=0)
        squared=0;maximum=0;changed=0
        for x,y in zip(left,right):
            d=abs(x-y);maximum=max(maximum,d);squared+=d*d;changed+=d!=0
        return dict(maximum=maximum,rmse=math.sqrt(squared/len(left)),changedChannels=changed)
    row['beforeAfter']=difference(blobs['before',0],blobs['after',0]);row['originalAfter']=difference(blobs['original',0],blobs['after',0])
    # Gates declared before timing. Linear rounds in the existing byte-space CPU kernel.
    gate=3 if scene=='linear-512' else 1
    row['qualityPassed']=row['originalAfter']['maximum']<=gate and row['beforeAfter']['maximum']<=(1 if scene=='linear-512' else 0)
    row['speedup']=row['before']['msPerFrame']/row['after']['msPerFrame'];row['afterOverOriginal']=row['after']['msPerFrame']/row['original']['msPerFrame'];summary.append(row)
for f,want in hashes.items():assert hashlib.sha256(Path(f).read_bytes()).hexdigest()==want,'Frozen binary changed'
manifest['complete']=True;write(a.output/'manifest.json',manifest);write(a.output/'summary.json',summary)
print(json.dumps(summary,indent=2));raise SystemExit(0 if all(r['qualityPassed'] for r in summary) else 2)
