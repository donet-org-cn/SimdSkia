"""Compare path queries/GetFillPath and rendered caps/joins with the pinned native oracle."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess

p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh output directory.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:
    subprocess.run(['dotnet','run','--project',f'tests/StrokeCompatibility/{variant}','-c','Release','--',str(a.output/variant.lower())],check=True)
load=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
ref=load(a.output/'original/results.json');got=load(a.output/'managed/results.json');failures=[];checks=0
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
        if not (math.isclose(x,y,abs_tol=1e-4,rel_tol=2e-6) if type(x) in (float,int) else x==y):failures.append(f'{key}: {x!r} != {y!r}')
compare(ref,got,'root');images=[];equivalent=[]
for file in sorted((a.output/'original').glob('*.bgra')):
    x=file.read_bytes();y=(a.output/'managed'/file.name).read_bytes();assert len(x)==len(y)==128*128*4
    delta=[abs(v-w) for v,w in zip(x,y)];maximum=max(delta);rmse=math.sqrt(sum(v*v for v in delta)/len(delta));ok=maximum<=32 and rmse<=2
    images.append(dict(scene=file.stem,maxError=maximum,rmse=rmse,passed=ok))
for file in (a.output/'managed').glob('stroke-*.bgra'):
    q=a.output/'managed'/file.name.replace('stroke-','outline-',1);ok=q.read_bytes()==file.read_bytes();equivalent.append(dict(scene=file.stem,exact=ok))
    if not ok:failures.append(file.stem+': GetFillPath != direct stroke')
for variant in ['original','managed']:
    styles=[(a.output/variant/f'line-style-{style}.bgra').read_bytes() for style in ['Fill','Stroke','StrokeAndFill']]
    if len(set(styles))!=1:failures.append(variant+': DrawLine incorrectly depends on paint style')
report=dict(baseline='SkiaSharp 3.119.4',cases=len(ref),metadataChecks=checks,metadataFailures=failures,metadataPassed=not failures,pixelThreshold=dict(maximum=32,rmse=2),images=images,directOutline=equivalent,managedChecks=load(a.output/'managed/managed-checks.json'),fullCompatibility=False)
report['passed']=report['metadataPassed'] and all(i['passed'] for i in images)
(a.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
sources={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for d in ['src/SimdSkia.Core','src/SimdSkia.Compatibility','tests/StrokeCompatibility'] for f in Path(d).rglob('*.cs') if not {'obj','bin'}&set(f.parts)}
(a.output/'source-hashes.json').write_text(json.dumps(sources,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(cases=len(ref),checks=checks,metadataPassed=report['metadataPassed'],pixelPassed=sum(x['passed'] for x in images),pixelTotal=len(images),managedChecks=report['managedChecks'],metadataFailures=failures[:15],passed=report['passed']),indent=2))
raise SystemExit(0 if report['passed'] else 2)
