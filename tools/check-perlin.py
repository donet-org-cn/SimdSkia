import argparse,json,subprocess,math
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a new evidence directory.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:subprocess.run(['dotnet','run','--project',f'tests/Perlin/{variant}','-c','Release','--',str(a.output/variant.lower())],check=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));o=load(a.output/'original/values.json');m=load(a.output/'managed/values.json');diff=[dict(key=k,original=v,managed=m.get(k)) for k,v in o.items() if v!=m.get(k)];oc=load(a.output/'original/cases.json');mc=load(a.output/'managed/cases.json');left=(a.output/'original/pixels.bin').read_bytes();right=(a.output/'managed/pixels.bin').read_bytes();results=[];lookup={(c['kind'],c['scene'],c['transform']):c for c in mc}
for c in oc:
 key=(c['kind'],c['scene'],c['transform']);other=lookup.get(key)
 if other is None:results.append(dict(**c,passed=False,missing=True));continue
 s=c['offset'];n=c['length'];ss=other['offset'];d=[abs(x-y) for x,y in zip(left[s:s+n],right[ss:ss+n])];maximum=max(d);rmse=math.sqrt(sum(x*x for x in d)/n);results.append(dict(**c,maximum=maximum,rmse=rmse,passed=maximum<=3 and rmse<=.75))
report=dict(metadata=len(o),differences=diff,cases=results,local=[load(a.output/v/'local.json') for v in ['original','managed']],gate=dict(maximum=3,rmse=.75),passed=not diff and all(c['passed'] for c in results));(a.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps(dict(metadata=len(o),differences=len(diff),passedCases=sum(c['passed'] for c in results),total=len(results),local=report['local']),indent=2));raise SystemExit(0 if report['passed'] else 2)
