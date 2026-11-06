"""Compare identical compiled calls against the pinned SkiaSharp 3.119.4 oracle."""
import argparse
import json
import math
from pathlib import Path
import subprocess

p=argparse.ArgumentParser()
p.add_argument('--output',type=Path,default=Path('artifacts/geometry-compatibility'))
p.add_argument('--no-build',action='store_true')
a=p.parse_args()
if a.output.exists():raise SystemExit('Choose a new output directory; preserve previous results and avoid stale images.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:
    cmd=['dotnet','run','--project',f'tests/GeometryCompatibility/{variant}','-c','Release']
    if a.no_build:cmd+=['--no-build']
    subprocess.run(cmd+['--',str(a.output/variant.lower())],check=True)
ref=json.loads((a.output/'original/results.json').read_text(encoding='utf-8'))
got=json.loads((a.output/'managed/results.json').read_text(encoding='utf-8'))
failures=[];checks=0
def compare(x,y,path):
    global checks
    if isinstance(x,dict) and isinstance(y,dict):
        if set(x)!=set(y):failures.append(path+': key mismatch')
        for k in x.keys()&y.keys():compare(x[k],y[k],path+'.'+k)
    elif isinstance(x,list) and isinstance(y,list):
        if len(x)!=len(y):failures.append(path+': length mismatch')
        for i,(v,w) in enumerate(zip(x,y)):compare(v,w,path+f'[{i}]')
    else:
        checks+=1
        # Geometry is measured in logical coordinate units, not image channels.
        equal=math.isclose(x,y,rel_tol=2e-6,abs_tol=1e-4) if type(x) in (int,float) and type(y) in (int,float) else x==y
        if not equal:failures.append(f'{path}: {x!r} != {y!r}')
compare(ref,got,'root')
named_ref=json.loads((a.output/'original/named-colors.json').read_text())
named_got=json.loads((a.output/'managed/named-colors.json').read_text())
compare(named_ref,named_got,'namedColors')
circle_ref=json.loads((a.output/'original/circle-representation.json').read_text())
circle_got=json.loads((a.output/'managed/circle-representation.json').read_text())
known=[]
if circle_ref!=circle_got:
    known.append(dict(feature='AddCircle point representation',baseline=circle_ref,replacement=circle_got,reason='Native conic segments versus the existing managed cubic circle approximation. Not declared behaviorally equivalent.'))
    if circle_ref!={'points':9,'verbs':6} or circle_got!={'points':13,'verbs':6}:failures.append('Unexpected circle representation regression')
images=[]
for file in sorted((a.output/'original').glob('*.bgra')):
    x=file.read_bytes();y=(a.output/'managed'/file.name).read_bytes()
    assert len(x)==len(y)==128*128*4
    delta=[abs(v-w) for v,w in zip(x,y)];maximum=max(delta);rmse=math.sqrt(sum(v*v for v in delta)/len(delta))
    ok=maximum<=32 and rmse<=2  # Existing whole-page gate, never relaxed here.
    images.append(dict(scene=file.stem,maxError=maximum,rmse=rmse,passed=ok))
    if not ok:failures.append(file.stem+': pixel gate failed')
report=dict(cases=len(ref),valueChecks=checks,numericTolerance=dict(absolute=1e-4,relative=2e-6),pixelThreshold=dict(maximum=32,rmse=2),images=images,failures=failures,knownBehavioralDifferences=known,fullBehavioralCompatibility=False,scopedChecksPassed=not failures and not known,circleRepresentationMatches=circle_ref==circle_got,passed=not failures)
(a.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
raise SystemExit(0 if not failures else 2)
