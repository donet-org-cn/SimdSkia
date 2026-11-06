"""Rebuild/run scoped native-oracle suites; retain nonzero quality failures."""
import argparse,datetime,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh evidence directory.')
a.output.mkdir(parents=True);runs=[]
for name in ['runtime-values','runtime-effects','lattice-resources','pictures','gpu-context','float-shaders','image-operations','path-boolean','canvas-views','perlin','mesh-atlas','drawing-additions']:
 cmd=['python',f'tools/check-{name}.py','--output',str(a.output/name)]
 with (a.output/(name+'.log')).open('w',encoding='utf-8') as f:r=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
 runs.append(dict(suite=name,command=cmd,exitCode=r.returncode));(a.output/'status.json').write_text(json.dumps(runs,indent=2)+'\n',encoding='utf-8');print(name,r.returncode,flush=True)
for variant in ['Original','Managed']:
 cmd=['dotnet','run','--project',f'tests/CacheControls/{variant}','-c','Release','--no-build']
 with (a.output/f'cache-{variant}.json').open('w',encoding='utf-8') as f:r=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
 runs.append(dict(suite='cache-'+variant,command=cmd,exitCode=r.returncode));(a.output/'status.json').write_text(json.dumps(runs,indent=2)+'\n',encoding='utf-8');print('cache-'+variant,r.returncode,flush=True)
raise SystemExit(0 if all(x['exitCode']==0 for x in runs) else 2)
