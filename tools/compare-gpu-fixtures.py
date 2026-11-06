"""Compare captured common frames without equating different backend capabilities.

Cross-backend comparison checks pixels only. Missing/extra cases and metadata
differences remain in the report and are not counted as behavior compatibility.
The max-channel 3 / RMSE .75 gate is unchanged.
"""
import argparse,json,math
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--original',type=Path,required=True)
p.add_argument('--managed',type=Path,required=True)
p.add_argument('--output',type=Path,required=True)
p.add_argument('--cross-backend',action='store_true')
a=p.parse_args()
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
left=(a.original/'pixels.bin').read_bytes();right=(a.managed/'pixels.bin').read_bytes()
oc=load(a.original/'cases.json');mc=load(a.managed/'cases.json')
key=lambda c:(c['kind'],c['scene'],c['transform'])
lookup={key(c):c for c in mc};results=[];missing=[]
if len(lookup)!=len(mc):raise ValueError('Duplicate managed frame identity.')
for c in oc:
 d=lookup.pop(key(c),None)
 if d is None:missing.append(c);continue
 n=c['length'];x=left[c['offset']:c['offset']+n];y=right[d['offset']:d['offset']+d['length']]
 if len(x)!=n or len(y)!=n or n==0:raise ValueError('Truncated or incompatible frame.')
 delta=[abs(u-v) for u,v in zip(x,y)];maximum=max(delta);rmse=math.sqrt(sum(v*v for v in delta)/n)
 results.append(dict(**c,maximum=maximum,rmse=rmse,passed=maximum<=3 and rmse<=.75))
o=load(a.original/'values.json');m=load(a.managed/'values.json')
differences=[dict(key=k,original=o.get(k),managed=m.get(k)) for k in sorted(o.keys()|m.keys()) if o.get(k)!=m.get(k)]
pixel_pass=bool(results) and all(c['passed'] for c in results)
report=dict(scope='common cross-backend pixel quality only' if a.cross_backend else 'same-backend fixture comparison',
 gate=dict(maximum=3,rmse=.75),cases=results,missingCases=missing,extraCases=list(lookup.values()),metadataDifferences=differences,
 commonPixelGatePassed=pixel_pass,behaviorCompatible=False if a.cross_backend else pixel_pass and not missing and not lookup and not differences)
a.output.parent.mkdir(parents=True,exist_ok=True)
a.output.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(common=len(results),passed=sum(c['passed'] for c in results),missing=len(missing),extra=len(lookup),metadataDifferences=len(differences),scope=report['scope'])))
raise SystemExit(0 if (pixel_pass if a.cross_backend else report['behaviorCompatible']) else 2)
