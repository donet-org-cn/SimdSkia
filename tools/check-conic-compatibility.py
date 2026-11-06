"""Native 3.119.4 path metadata and strict oval/conic raster comparison; failures stay failures."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess

p=argparse.ArgumentParser()
p.add_argument('--output',required=True,type=Path)
a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh output directory.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:
    subprocess.run(['dotnet','run','--project',f'tests/ConicCompatibility/{variant}','-c','Release','--',str(a.output/variant.lower())],check=True)
ref=json.loads((a.output/'original/results.json').read_text(encoding='utf-8'));got=json.loads((a.output/'managed/results.json').read_text(encoding='utf-8'))
failures=[];checks=0
def compare(x,y,key):
    global checks
    if isinstance(x,dict):
        if set(x)!=set(y):failures.append(key+': keys differ')
        for k in x.keys()&y.keys():compare(x[k],y[k],key+'.'+k)
    elif isinstance(x,list):
        if len(x)!=len(y):failures.append(key+': length differs')
        for i,(v,w) in enumerate(zip(x,y)):compare(v,w,key+f'[{i}]')
    else:
        checks+=1
        if not (math.isclose(x,y,abs_tol=1e-4,rel_tol=2e-6) if isinstance(x,(float,int)) else x==y):failures.append(f'{key}: {x} != {y}')
compare(ref,got,'root');images=[]
for f in sorted((a.output/'original').glob('*.bgra')):
    x=f.read_bytes();y=(a.output/'managed'/f.name).read_bytes();assert len(x)==len(y)==128*128*4
    delta=[abs(u-v) for u,v in zip(x,y)];maximum=max(delta);rmse=math.sqrt(sum(d*d for d in delta)/len(delta));passed=maximum<=32 and rmse<=2
    images.append(dict(scene=f.stem,maxError=maximum,rmse=rmse,passed=passed))
report=dict(baseline='SkiaSharp 3.119.4',cases=len(ref),metadataChecks=checks,metadataPassed=not failures,metadataFailures=failures,pixelThreshold=dict(maxError=32,rmse=2),images=images,pixelsPassed=all(r['passed'] for r in images),fullCompatibility=False)
report['passed']=report['metadataPassed'] and report['pixelsPassed']
(a.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
paths=[*Path('src/SimdSkia.Compatibility').glob('*.cs'),Path('src/SimdSkia.Core/PathGeometry.cs'),Path('src/SimdSkia.Gpu.Direct2D/Direct2DCanvas.cs'),Path('tests/ConicCompatibility/Program.cs')]
(a.output/'source-hashes.json').write_text(json.dumps({str(f):hashlib.sha256(f.read_bytes()).hexdigest() for f in paths},indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(cases=len(ref),metadataChecks=checks,metadataPassed=report['metadataPassed'],pixelPass=sum(r['passed'] for r in images),pixelTotal=len(images),passed=report['passed'],metadataFailures=failures[:12]),indent=2))
raise SystemExit(0 if report['passed'] else 2)
