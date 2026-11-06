"""Fixed SkiaSharp 3.119.4 pixel oracle; do not relax the predeclared gates to make it pass."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh output directory.')
a.output.mkdir(parents=True)
write=lambda f,v:f.write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8')
for variant in ['Original','Managed']:
    subprocess.run(['dotnet','run','--project','tests/PixelCompatibility/'+variant,'-c','Release','--',str(a.output/variant.lower())],check=True)
load=lambda f:json.loads(f.read_text(encoding='utf-8-sig'))
original=load(a.output/'original/values.json');managed=load(a.output/'managed/values.json');differences=[];values=0
def compare(x,y,key):
    global values
    if isinstance(x,dict):
        if not isinstance(y,dict) or set(x)!=set(y):differences.append(dict(key=key,original=x,managed=y));return
        for k in x:compare(x[k],y[k],key+'.'+k)
    elif isinstance(x,list):
        if not isinstance(y,list) or len(x)!=len(y):differences.append(dict(key=key,original=x,managed=y));return
        for i,(v,w) in enumerate(zip(x,y)):compare(v,w,key+f'[{i}]')
    else:
        values+=1
        if x!=y and not (type(x) in [int,float] and type(y) in [int,float] and abs(x-y)<=1e-5*max(1,abs(x))):differences.append(dict(key=key,original=x,managed=y))
compare(original,managed,'values')
transfers=[];ta=load(a.output/'original/transfers.json');tb=load(a.output/'managed/transfers.json');assert len(ta)==len(tb)
for x,y in zip(ta,tb):
    assert x['name']==y['name'];name=x['name'];left=(a.output/'original'/(name+'.bin')).read_bytes();right=(a.output/'managed'/(name+'.bin')).read_bytes()
    assert len(left)==len(right);delta=[abs(v-w) for v,w in zip(left,right)];maximum=max(delta,default=0);rmse=math.sqrt(sum(d*d for d in delta)/len(delta))
    transfers.append(dict(name=name,kind=x['kind'],originalResult=x['ok'],managedResult=y['ok'],maximum=maximum,rmse=rmse,threshold=x['threshold'],passed=x['ok']==y['ok'] and maximum<=x['threshold']))
layout=[d for d in differences if d['key'].startswith(('values.span-padding-','values.span-typed-offset-'))];other=[d for d in differences if d not in layout]
report=dict(metadataCases=len(original),metadataValues=values,metadataPassed=not differences,metadataOutsideSpanPassed=not other,knownPaddedSpanDifferences=layout,otherDifferences=other,transfers=transfers,pixelPass=sum(x['passed'] for x in transfers),pixelTotal=len(transfers),fullCompatibility=False,
    spanPolicy='Intentional difference: managed spans include stride through the last pixel and use physical stride for offsets; baseline 3.119.4 uses compact Info size/byte offsets and y*Height+x for typed offsets. Pointer/ReadPixels APIs are separately compared.')
write(a.output/'comparison.json',report)
files=[f for folder in ['src/SimdSkia.Core','src/SimdSkia.Cpu','src/SimdSkia.Compatibility','tests/PixelCompatibility'] for f in Path(folder).rglob('*.cs') if not any(part in ['bin','obj'] for part in f.parts)]
write(a.output/'source-hashes.json',{str(f):hashlib.sha256(f.read_bytes()).hexdigest() for f in files})
print(json.dumps({k:report[k] for k in ['metadataCases','metadataValues','metadataPassed','metadataOutsideSpanPassed','pixelPass','pixelTotal','fullCompatibility']},indent=2))
raise SystemExit(0 if not differences and all(x['passed'] for x in transfers) else 2)
