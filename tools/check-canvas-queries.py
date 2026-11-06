import argparse,json,subprocess,math
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True)
for v in ['Original','Managed']:subprocess.run(['dotnet','run','--project',f'tests/CanvasQueries/{v}','-c','Release','--',str(a.output/v.lower())],check=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));left=load(a.output/'original/values.json');right=load(a.output/'managed/values.json');assert set(left)==set(right);errors=[]
def compare(x,y,k):
 if isinstance(x,list) and isinstance(y,list) and len(x)==len(y):
  for i,(v,w) in enumerate(zip(x,y)):compare(v,w,k+f'[{i}]')
 elif isinstance(x,(int,float)) and not isinstance(x,bool) and isinstance(y,(int,float)):
  if abs(x-y)>1e-4:errors.append(dict(key=k,original=x,managed=y))
 elif x!=y:errors.append(dict(key=k,original=x,managed=y))
for k in left:compare(left[k],right[k],k)
r=dict(cases=len(left),passed=not errors,errors=errors);(a.output/'comparison.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in r.items() if k!='errors'},indent=2),len(errors),'differences');raise SystemExit(2 if errors else 0)
