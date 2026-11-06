"""Native-oracle check: a queued GL draw retains its texture until completion."""
import argparse,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:
 with (a.output/(variant+'.log')).open('w',encoding='utf-8') as log:
  subprocess.run(['dotnet','run','--project',f'tests/GlInterop/{variant}','-c','Release','--',str(a.output/variant),'--pending'],stdout=log,stderr=subprocess.STDOUT,check=True)
load=lambda name:json.loads((a.output/name/'values.json').read_text(encoding='utf-8-sig'))
o=load('Original');m=load('Managed');equal=(a.output/'Original/pixels.bin').read_bytes()==(a.output/'Managed/pixels.bin').read_bytes()
result=dict(original=o,managed=m,exactPixels=equal,passed=o==m and equal)
(a.output/'comparison.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps(result));raise SystemExit(0 if result['passed'] else 2)
