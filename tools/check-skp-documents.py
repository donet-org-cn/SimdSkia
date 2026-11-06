"""Native SKP cross-read: both engines read each other's actual vector files."""
import argparse,json,math,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh evidence directory.')
a.output.mkdir(parents=True)
for project,dest,cross in [('Original','Seed',None),('Managed','Managed','Seed'),('Original','Original','Managed')]:
 cmd=['dotnet','run','--project',f'tests/SkpDocuments/{project}','-c','Release','--',str(a.output/dest)]
 if cross:cmd.append(str(a.output/cross))
 with (a.output/(dest+'.log')).open('w',encoding='utf-8') as f:subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,check=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));o=load(a.output/'Original/values.json');m=load(a.output/'Managed/values.json');diff=[dict(key=k,original=v,managed=m.get(k)) for k,v in o.items() if v!=m.get(k)];left=(a.output/'Original/pixels.bin').read_bytes();right=(a.output/'Managed/pixels.bin').read_bytes();oc=load(a.output/'Original/cases.json');mc=load(a.output/'Managed/cases.json');assert len(oc)==len(mc);cases=[]
for c,d in zip(oc,mc):
 assert (c['kind'],c['scene'],c['length'])==(d['kind'],d['scene'],d['length']);n=c['length'];delta=[abs(x-y) for x,y in zip(left[c['offset']:c['offset']+n],right[d['offset']:d['offset']+n])];maximum=max(delta);rmse=math.sqrt(sum(x*x for x in delta)/n);cases.append(dict(**c,maximum=maximum,rmse=rmse,passed=maximum<=3 and rmse<=.75))
result=dict(metadata=len(o),differences=diff,cases=cases,gate=dict(maximum=3,rmse=.75),passed=not diff and all(c['passed'] for c in cases));(a.output/'comparison.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps(dict(metadata=len(o),differences=len(diff),passed=sum(c['passed'] for c in cases),total=len(cases))));raise SystemExit(0 if result['passed'] else 2)
