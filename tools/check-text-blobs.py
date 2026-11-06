import argparse,json,subprocess,math
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True)
for v in ['Original','Managed']:subprocess.run(['dotnet','run','--project',f'tests/TextBlobs/{v}','-c','Release','--',str(a.output/v.lower())],check=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));rows=load(a.output/'original/cases.json');assert rows==load(a.output/'managed/cases.json');l=(a.output/'original/pixels.bin').read_bytes();m=(a.output/'managed/pixels.bin').read_bytes();results=[]
for r in rows:
 x=l[r['offset']:r['offset']+r['length']];y=m[r['offset']:r['offset']+r['length']];d=[abs(x-y) for x,y in zip(x,y)];maximum=max(d);rmse=math.sqrt(sum(v*v for v in d)/len(d));results.append(dict(**r,maximum=maximum,rmse=rmse,passed=maximum<=32 and rmse<=2))
left=load(a.output/'original/values.json');right=load(a.output/'managed/values.json');metadataErrors=[]
def compare(x,y,key):
 if isinstance(x,list) and isinstance(y,list) and len(x)==len(y):
  for i,(v,w) in enumerate(zip(x,y)):compare(v,w,key+f'[{i}]')
 elif isinstance(x,(int,float)) and not isinstance(x,bool) and isinstance(y,(int,float)):
  if abs(x-y)>1e-4:metadataErrors.append(dict(key=key,original=x,managed=y))
 elif x!=y:metadataErrors.append(dict(key=key,original=x,managed=y))
assert set(left)==set(right)
for key in left:compare(left[key],right[key],key)
result=dict(gate=dict(maximum=32,rmse=2),passed=all(r['passed'] for r in results),passedCases=sum(r['passed'] for r in results),cases=results,metadataPassed=not metadataErrors,metadataErrors=metadataErrors);(a.output/'comparison.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['cases','metadataErrors']},indent=2));raise SystemExit(0 if result['passed'] and result['metadataPassed'] else 2)
