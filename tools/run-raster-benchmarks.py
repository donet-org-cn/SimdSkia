"""Serial, rotating CPU worker comparison. Oracle correctness gates run separately."""
import argparse,hashlib,json,statistics,subprocess,os,platform,datetime
from pathlib import Path
p=argparse.ArgumentParser()
for name in ['after','original','output']:p.add_argument('--'+name,type=Path,required=True)
a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True);workers={v:getattr(a,v).resolve() for v in ['original','after']}
write=lambda p,v:p.write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8')
hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for folder in workers.values() for f in folder.rglob('*') if f.is_file() and f.suffix in ['.dll','.json']}
manifest=dict(startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),backend='CPU JIT',warmupSeconds=.4,measurementSeconds=.8,forks=3,processes=6,files=hashes,description="Native SkiaSharp 3.119.4 versus current CPU raster build; no before-release speedup claim",machine=dict(platform=platform.platform(),cpuCount=os.cpu_count(),architecture=platform.machine()),complete=False)
write(a.output/'manifest.json',manifest);runs=[]
for fork in range(3):
    order=list(workers);order=order[fork:]+order[:fork]
    for variant in order:
        directory=a.output/f'{variant}-{fork}';dll=workers[variant]/('Original.dll' if variant=='original' else 'Managed.dll')
        subprocess.run(['dotnet',str(dll),str(directory)],check=True,stdout=subprocess.DEVNULL)
        runs.append(dict(variant=variant,fork=fork,rows=json.loads((directory/'results.json').read_text(encoding='utf-8'))));write(a.output/'runs.json',runs)
summary=[]
for scene in [r['scene'] for r in runs[0]['rows']]:
    row=dict(scene=scene)
    for variant in workers:
        group=[r for run in runs if run['variant']==variant for r in run['rows'] if r['scene']==scene]
        row[variant]={k:statistics.median(r[k] for r in group) for k in ['msPerFrame','cpuMsPerFrame','cpuPercentOfMachine','allocatedBytesPerFrame','workingSetMiB','privateMiB']}
    row['afterOverOriginal']=row['after']['msPerFrame']/row['original']['msPerFrame'];summary.append(row)
for f,want in hashes.items():assert hashlib.sha256(Path(f).read_bytes()).hexdigest()==want,'Frozen binary changed'
manifest['complete']=True;manifest['finishedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat();write(a.output/'manifest.json',manifest);write(a.output/'summary.json',summary)
print(json.dumps(summary,indent=2))
