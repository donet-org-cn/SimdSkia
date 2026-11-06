"""Strict isolated SkiaSharp oracle for real image-filter drawing and lifetime."""
import argparse,json,subprocess,math
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True)
for v in ['Original','Managed']:subprocess.run(['dotnet','run','--project',f'tests/ImageFilters/{v}','-c','Release','--',str(a.output/v.lower())],check=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));rows=load(a.output/'original/cases.json');assert rows==load(a.output/'managed/cases.json');l=(a.output/'original/pixels.bin').read_bytes();m=(a.output/'managed/pixels.bin').read_bytes();results=[]
for r in rows:
 x=l[r['offset']:r['offset']+r['length']];y=m[r['offset']:r['offset']+r['length']];d=[abs(x-y) for x,y in zip(x,y)];maximum=max(d);rmse=math.sqrt(sum(v*v for v in d)/len(d));results.append(dict(**r,maximum=maximum,rmse=rmse,passed=maximum<=3 and rmse<=.75))
result=dict(gate=dict(maximum=3,rmse=.75),passed=all(r['passed'] for r in results),passedCases=sum(r['passed'] for r in results),cases=results,metadataPassed=load(a.output/'original/values.json')==load(a.output/'managed/values.json'));(a.output/'comparison.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='cases'},indent=2));raise SystemExit(0 if result['passed'] and result['metadataPassed'] else 2)
