"""Fresh serial per-scene processes; end-of-loop process memory is diagnostic.
Full UI process sampling is a separate benchmark. A8 reference uses identical
managed binary and the validated original full RGBA working-plane algorithm.
"""
import argparse,hashlib,json,platform,statistics,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh evidence directory.')
a.output.mkdir(parents=True);write=lambda f,j:f.write_text(json.dumps(j,indent=2)+'\n',encoding='utf-8');workers={k:Path(f'benchmarks/FinalRaster/{k}/bin/Release/net10.0/{k}.dll').resolve() for k in ['Original','Managed']};hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for w in workers.values() for f in w.parent.glob('*') if f.is_file()};runs=[];write(a.output/'manifest.json',dict(platform=platform.platform(),runtimeBinaries=hashes,warmupSeconds=.6,measureSeconds=.8,forks=3,complete=False,memory='End-of-loop process WorkingSet64 and PrivateMemorySize64; diagnostics, not sampled peak or VRAM.'))
for fork in range(3):
 for scene in ['alpha8','float-color','arithmetic','skp-write','skp-read','webp-lossless']:
  variants=['Original','Managed']+(['Reference'] if scene=='alpha8' else []);variants=variants[fork%len(variants):]+variants[:fork%len(variants)]
  for variant in variants:
   dest=a.output/f'{scene}-{variant}-{fork}';cmd=['dotnet',str(workers['Managed' if variant=='Reference' else variant]),str(dest),scene]+(['--alpha-reference'] if variant=='Reference' else [])
   with (a.output/(dest.name+'.log')).open('w',encoding='utf-8') as f:subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,check=True)
   row=json.loads((dest/'result.json').read_text(encoding='utf-8'));runs.append(dict(variant=variant,fork=fork,**row));write(a.output/'runs.json',runs);print(f"{scene} {variant} #{fork}: {row['msPerFrame']:.4f} ms, {row['allocatedBytesPerFrame']:.1f} B/frame",flush=True)
summary=[]
for scene in dict.fromkeys(r['scene'] for r in runs):
 row=dict(scene=scene)
 for variant in dict.fromkeys(r['variant'] for r in runs if r['scene']==scene):
  group=[r for r in runs if r['scene']==scene and r['variant']==variant];row[variant]={k:statistics.median(r[k] for r in group) for k in ['msPerFrame','cpuMsPerFrame','cpuPercentOfMachine','allocatedBytesPerFrame','workingSetMiB','privateMiB']}
 row['managedOverOriginal']=row['Managed']['msPerFrame']/row['Original']['msPerFrame'];summary.append(row)
assert (a.output/'alpha8-Managed-0/pixels.bin').read_bytes()==(a.output/'alpha8-Reference-0/pixels.bin').read_bytes(),'A8 optimization changed timed output'
for f,want in hashes.items():assert hashlib.sha256(Path(f).read_bytes()).hexdigest()==want,'Frozen worker changed'
write(a.output/'summary.json',summary);manifest=json.loads((a.output/'manifest.json').read_text(encoding='utf-8'));manifest['complete']=True;write(a.output/'manifest.json',manifest)
