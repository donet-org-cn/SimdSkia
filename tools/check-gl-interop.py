"""Isolated SkiaSharp 3.119.4 versus real managed GL rendering/interop.

Both workers require the same hardware ANGLE host and complete framebuffer
readback. No timing is inferred from command submission. Error gates match the
pre-existing GPU context gate (max channel 3, RMSE .75).
"""
import argparse,json,math,subprocess
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--project',choices=['GlInterop','GlContext','GlSystem','D3DContext','D3DInterop','MetalContext','VulkanInterop'],default='GlInterop');p.add_argument('--reuse',action='store_true');a=p.parse_args()
if not a.reuse:
 if a.output.exists():raise SystemExit('Use a new evidence directory.')
 a.output.mkdir(parents=True)
 for variant in ['Original','Managed']:subprocess.run(['dotnet','run','--project',f'tests/{a.project}/{variant}','-c','Release','--',str(a.output/variant.lower())],check=True)
def read(variant,name):return json.loads((a.output/variant/name).read_text(encoding='utf-8-sig'))
o=read('original','values.json');m=read('managed','values.json');differences=[dict(key=k,original=o.get(k),managed=m.get(k)) for k in sorted(o.keys()|m.keys()) if o.get(k)!=m.get(k)]
left=(a.output/'original/pixels.bin').read_bytes();right=(a.output/'managed/pixels.bin').read_bytes();oc=read('original','cases.json');mc=read('managed','cases.json');lookup={(c['kind'],c['scene'],c['transform']):c for c in mc};results=[]
for c in oc:
 key=(c['kind'],c['scene'],c['transform']);other=lookup.pop(key,None)
 if other is None:results.append(dict(**c,passed=False,missing=True));continue
 x=left[c['offset']:c['offset']+c['length']];y=right[other['offset']:other['offset']+other['length']]
 if len(x)!=c['length'] or len(y)!=c['length']:raise SystemExit('Truncated or unequal capture.')
 errors=[abs(u-v) for u,v in zip(x,y)];maximum=max(errors,default=0);rmse=math.sqrt(sum(e*e for e in errors)/len(errors));results.append(dict(**c,maximum=maximum,rmse=rmse,passed=maximum<=3 and rmse<=.75))
report=dict(project=a.project,metadata=len(o),differences=differences,extraCases=list(lookup),cases=results,local=[read(v,'local.json') for v in ['original','managed']],gate=dict(maximum=3,rmse=.75),passed=not differences and not lookup and all(c['passed'] for c in results));(a.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps(dict(metadata=len(o),differences=len(differences),passedCases=sum(c['passed'] for c in results),total=len(results),local=report['local']),indent=2));raise SystemExit(0 if report['passed'] else 2)
