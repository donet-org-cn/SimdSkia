import argparse,json,subprocess,math
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a new evidence directory.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:subprocess.run(['dotnet','run','--project',f'tests/MaskFilters/{variant}','-c','Release','--',str(a.output/variant.lower())],check=True)
load=lambda path:json.loads(path.read_text(encoding='utf-8-sig'));o=load(a.output/'original/values.json');m=load(a.output/'managed/values.json');cases=load(a.output/'original/cases.json');assert cases==load(a.output/'managed/cases.json');left=(a.output/'original/pixels.bin').read_bytes();right=(a.output/'managed/pixels.bin').read_bytes();results=[]
for case in cases:
 s=case['offset'];n=case['length'];d=[abs(x-y) for x,y in zip(left[s:s+n],right[s:s+n])];maximum=max(d);rmse=math.sqrt(sum(x*x for x in d)/n);results.append(dict(**case,maximum=maximum,rmse=rmse,passed=maximum<=3 and rmse<=.75))
report=dict(values=len(o),metadataPassed=o==m,pixelCases=results,gate=dict(maximum=3,rmse=.75),passed=o==m and all(x['passed'] for x in results));(a.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps(dict(values=len(o),metadataPassed=o==m,pixelPassed=sum(r['passed'] for r in results),pixelTotal=len(results)),indent=2));raise SystemExit(0 if report['passed'] else 2)
