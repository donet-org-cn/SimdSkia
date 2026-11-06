import argparse,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True)
for v in ['Original','Managed']:subprocess.run(['dotnet','run','--project',f'tests/Utilities/{v}','-c','Release','--',str(a.output/v.lower())],check=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));x=load(a.output/'original/values.json');y=load(a.output/'managed/values.json');assert set(x)==set(y);errors=[dict(key=k,original=x[k],managed=y[k]) for k in x if x[k]!=y[k]]
r=dict(cases=len(x),passed=not errors,errors=errors);(a.output/'comparison.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8');print(json.dumps(r,indent=2));raise SystemExit(2 if errors else 0)
