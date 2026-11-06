import argparse,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a new evidence directory.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:subprocess.run(['dotnet','run','--project',f'tests/RuntimeValues/{variant}','-c','Release','--',str(a.output/variant.lower())],check=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));o=load(a.output/'original/values.json');m=load(a.output/'managed/values.json');diff=[dict(key=k,original=v,managed=m.get(k)) for k,v in o.items() if v!=m.get(k)];report=dict(cases=len(o),differences=diff,passed=not diff,scope='Real uniform byte serialization and typed child values; runtime language compilation is not implemented by this leaf-value batch.');(a.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps(dict(cases=len(o),differences=len(diff),passed=not diff)));raise SystemExit(0 if not diff else 2)
