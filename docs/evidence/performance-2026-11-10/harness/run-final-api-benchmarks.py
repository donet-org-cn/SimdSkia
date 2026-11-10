"""Three rotating original/current CPU API workers; no mixed CPU/GPU timings.

Each worker measures the same six 256x192 scenes sequentially. Process WS/private
are end-of-scene diagnostics, not sampled peaks or per-operation retained bytes.
"""
import argparse,hashlib,json,math,platform,statistics,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',type=Path,required=True);p.add_argument('--before',type=Path);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True)
save=lambda path,obj:path.write_text(json.dumps(obj,indent=2)+'\n',encoding='utf-8')
workers={v:Path(f'benchmarks/Api99/{v}/bin/Release/net10.0/{v}.dll').resolve() for v in ['Original','Managed']}
if a.before:workers['Before']=a.before.resolve()
hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for w in workers.values() for f in w.parent.glob('*') if f.is_file()}
manifest=dict(method=__doc__,platform=platform.platform(),forks=3,warmupSeconds=.5,measureSeconds=.8,hashes=hashes,complete=False)
save(a.output/'manifest.json',manifest);runs=[]
for fork in range(3):
 order=list(workers);order=order[fork%len(order):]+order[:fork%len(order)]
 for variant in order:
  dest=a.output/f'{variant}-{fork}'
  with (a.output/(dest.name+'.log')).open('w',encoding='utf-8') as log:subprocess.run(['dotnet',str(workers[variant]),str(dest)],stdout=log,stderr=subprocess.STDOUT,check=True)
  rows=json.loads((dest/'results.json').read_text(encoding='utf-8-sig'));runs.extend(dict(variant=variant,fork=fork,**row) for row in rows)
  save(a.output/'runs.json',runs);print(variant,fork,'complete',flush=True)
summary=[]
for scene in dict.fromkeys(r['scene'] for r in runs):
 row=dict(scene=scene)
 for variant in workers:
  group=[r for r in runs if r['scene']==scene and r['variant']==variant]
  row[variant]={k:statistics.median(r[k] for r in group) for k in ['msPerFrame','cpuMsPerFrame','cpuPercentOfMachine','allocatedBytesPerFrame','workingSetMiB','privateMiB']}
  frames=[(a.output/f'{variant}-{i}'/(scene+'.rgba')).read_bytes() for i in range(3)]
  assert all(x==frames[0] for x in frames),'Unstable timed capture.'
 x=(a.output/'Original-0'/(scene+'.rgba')).read_bytes();y=(a.output/'Managed-0'/(scene+'.rgba')).read_bytes()
 assert len(x)==len(y)==256*192*4
 d=[abs(u-v) for u,v in zip(x,y)];maximum=max(d);rmse=math.sqrt(sum(v*v for v in d)/len(d))
 row['quality']=dict(maximum=maximum,rmse=rmse,passed=maximum<=3 and rmse<=.75)
 row['managedOverOriginal']=row['Managed']['msPerFrame']/row['Original']['msPerFrame'];summary.append(row)
 if a.before:
  before=(a.output/'Before-0'/(scene+'.rgba')).read_bytes()
  assert before==y,'Optimization changed fixed-scene pixels: '+scene
  row['beforeAfterExact']=True;row['afterOverBefore']=row['Managed']['msPerFrame']/row['Before']['msPerFrame']
for path,digest in hashes.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,'Worker changed during measurement.'
save(a.output/'summary.json',summary);manifest['complete']=True;save(a.output/'manifest.json',manifest)
