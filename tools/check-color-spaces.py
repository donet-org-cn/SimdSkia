import argparse,json,subprocess,math
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a new evidence directory.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:subprocess.run(['dotnet','run','--project',f'tests/ColorSpaces/{variant}','-c','Release','--',str(a.output/variant.lower())],check=True)
load=lambda path:json.loads(path.read_text(encoding='utf-8-sig'));o=load(a.output/'original/values.json');m=load(a.output/'managed/values.json')
def equal(a,b):
 if isinstance(a,list) and isinstance(b,list):return len(a)==len(b) and all(equal(x,y) for x,y in zip(a,b))
 if isinstance(a,(int,float)) and not isinstance(a,bool) and isinstance(b,(int,float)) and not isinstance(b,bool):return abs(a-b)<=.00015
 return a==b
diff=[dict(key=k,original=v,managed=m.get(k)) for k,v in o.items() if not equal(v,m.get(k))]
assert load(a.output/'original/cases.json')==load(a.output/'managed/cases.json')
left=(a.output/'original/pixels.bin').read_bytes();right=(a.output/'managed/pixels.bin').read_bytes();results=[]
for index,case in enumerate(load(a.output/'original/cases.json')):
 s=case['offset'];n=case['length'];d=[abs(x-y) for x,y in zip(left[s:s+n],right[s:s+n])];maximum=max(d);rmse=math.sqrt(sum(x*x for x in d)/n);results.append(dict(index=index,maximum=maximum,rmse=rmse,passed=maximum<=2 and rmse<=.5))
report=dict(values=len(o),differences=diff,pixelCases=results,passed=not diff and all(x['passed'] for x in results));(a.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps(dict(values=len(o),differences=len(diff),pixelPassed=sum(r['passed'] for r in results),pixelTotal=len(results)),indent=2));raise SystemExit(0 if report['passed'] else 2)
