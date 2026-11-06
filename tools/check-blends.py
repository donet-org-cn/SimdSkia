import argparse,json,subprocess,math
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh output directory.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:subprocess.run(['dotnet','run','--project',f'tests/BlendOperations/{variant}','-c','Release','--',str(a.output/variant.lower())],check=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
rows=load(a.output/'original/cases.json');assert rows==load(a.output/'managed/cases.json');left=(a.output/'original/pixels.bin').read_bytes();right=(a.output/'managed/pixels.bin').read_bytes();assert len(left)==len(right)
results=[]
for r in rows:
 l=left[r['offset']:r['offset']+r['length']];m=right[r['offset']:r['offset']+r['length']];delta=[abs(x-y) for x,y in zip(l,m)];results.append(dict(**r,maximum=max(delta),rmse=math.sqrt(sum(x*x for x in delta)/len(delta)),passed=max(delta)<=1))
result=dict(gate=1,passed=all(r['passed'] for r in results),passedCases=sum(r['passed'] for r in results),totalCases=len(results),cases=results);(a.output/'comparison.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='cases'},indent=2));raise SystemExit(0 if result['passed'] else 2)
