"""Exact original/current SaveLayer bounds, blend modes and parent clip checks."""
import argparse
import json
from pathlib import Path
import subprocess

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--output',type=Path,required=True)
p.add_argument('--no-build',action='store_true')
a=p.parse_args()
if a.output.exists():raise SystemExit('Choose a fresh evidence directory.')
a.output.mkdir(parents=True)
for variant in ('Original','Managed'):
    cmd=['dotnet','run','--project',f'tests/LayerBounds/{variant}','-c','Release']
    if a.no_build:cmd.append('--no-build')
    subprocess.run(cmd+['--',str(a.output/variant.lower())],check=True)
load=lambda v:json.loads((a.output/v/'values.json').read_text(encoding='utf-8-sig'))
original=load('original');managed=load('managed')
assert original.keys()==managed.keys(),'Case inventory mismatch'
assert len(original)==464,'Unexpected test coverage'
left=(a.output/'original/pixels.bin').read_bytes()
right=(a.output/'managed/pixels.bin').read_bytes()
cases=[]
for name,value in original.items():
    other=managed[name]
    start=value['offset'];length=value['length']
    a_bytes=left[start:start+length]
    b_bytes=right[other['offset']:other['offset']+other['length']]
    assert len(a_bytes)==len(b_bytes)==40*32*4
    delta=[abs(x-y) for x,y in zip(a_bytes,b_bytes)]
    cases.append(dict(case=name,metadataMatch=value==other,maximum=max(delta),
                      differentBytes=sum(d!=0 for d in delta),passed=value==other and a_bytes==b_bytes))
report=dict(cases=len(cases),passedCases=sum(c['passed'] for c in cases),
            comparedBytes=len(left),threshold='byte exact',results=cases,
            passed=all(c['passed'] for c in cases))
(a.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='results'},indent=2))
raise SystemExit(0 if report['passed'] else 2)
