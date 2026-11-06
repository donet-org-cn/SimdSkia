"""Serial, rotating CPU worker comparison. Oracle correctness gates run separately."""
import argparse,hashlib,json,statistics,subprocess,os,platform
from pathlib import Path
p=argparse.ArgumentParser()
for name in ['before','after','original','output']:p.add_argument('--'+name,type=Path,required=True)
p.add_argument('--before-description',default='Same API99 worker before removal of temporary mesh wrappers and bounded triangle coverage reuse; not a released version.')
p.add_argument('--worker-warmup',type=float);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True);workers={v:getattr(a,v).resolve() for v in ['original','before','after']}
write=lambda p,v:p.write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8')
hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for folder in workers.values() for f in folder.rglob('*') if f.is_file() and f.suffix in ['.dll','.json']}
manifest=dict(backend='CPU JIT',warmupSeconds=.5,measurementSeconds=.8,forks=3,processes=9,files=hashes,before=a.before_description,machine=dict(platform=platform.platform(),cpuCount=os.cpu_count(),architecture=platform.machine()),complete=False)
write(a.output/'manifest.json',manifest);runs=[]
for fork in range(3):
    order=list(workers);order=order[fork:]+order[:fork]
    for variant in order:
        directory=a.output/f'{variant}-{fork}';dll=workers[variant]/('Original.dll' if variant=='original' else 'Managed.dll')
        command=['dotnet',str(dll),str(directory)]
        if a.worker_warmup is not None:command.append(str(a.worker_warmup))
        subprocess.run(command,check=True,stdout=subprocess.DEVNULL)
        if a.worker_warmup is not None:
            protocol=json.loads((directory/'protocol.json').read_text(encoding='utf-8'));assert protocol['warmupSeconds']==a.worker_warmup and protocol['measurementSeconds']==.8,'Worker must acknowledge the requested protocol'
        runs.append(dict(variant=variant,fork=fork,rows=json.loads((directory/'results.json').read_text(encoding='utf-8'))));write(a.output/'runs.json',runs)
summary=[]
for scene in [r['scene'] for r in runs[0]['rows']]:
    row=dict(scene=scene)
    for variant in workers:
        group=[r for run in runs if run['variant']==variant for r in run['rows'] if r['scene']==scene]
        row[variant]={k:statistics.median(r[k] for r in group) for k in ['msPerFrame','cpuMsPerFrame','cpuPercentOfMachine','allocatedBytesPerFrame','workingSetMiB','privateMiB']}
    row['speedup']=row['before']['msPerFrame']/row['after']['msPerFrame'];row['afterOverOriginal']=row['after']['msPerFrame']/row['original']['msPerFrame'];summary.append(row)
for f,want in hashes.items():assert hashlib.sha256(Path(f).read_bytes()).hexdigest()==want,'Frozen binary changed'
quality=[]
for row in summary:
 scene=row['scene'];old=(a.output/'before-0'/(scene+'.rgba')).read_bytes();new=(a.output/'after-0'/(scene+'.rgba')).read_bytes();native=(a.output/'original-0'/(scene+'.rgba')).read_bytes();assert old==new,'Optimization changed pixels: '+scene
 delta=[abs(x-y) for x,y in zip(native,new)];quality.append(dict(scene=scene,beforeAfterEqual=True,maximum=max(delta),rmse=(sum(x*x for x in delta)/len(delta))**.5))
write(a.output/'quality.json',quality)
manifest['complete']=True;write(a.output/'manifest.json',manifest);write(a.output/'summary.json',summary)
print(json.dumps(summary,indent=2))
