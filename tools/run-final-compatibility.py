"""Reproduce the final API additions, keeping failed quality gates nonzero.

Metal is recorded as unavailable on Windows, never as a passing hardware test.
The native D3D12 factory may similarly return unavailable; its exit 3 is retained.
"""
import argparse, datetime, json, subprocess
from pathlib import Path

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--output',type=Path,required=True)
a=p.parse_args()
if a.output.exists():raise SystemExit('Use a new evidence directory.')
a.output.mkdir(parents=True)
rows=[]
def run(name,cmd):
    started=datetime.datetime.now(datetime.timezone.utc).isoformat()
    with (a.output/(name+'.log')).open('w',encoding='utf-8') as log:
        result=subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT)
    rows.append(dict(suite=name,command=cmd,startedUtc=started,exitCode=result.returncode))
    (a.output/'status.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
    print(name,result.returncode,flush=True)
def project(name,path,args=None):
    run(name,['dotnet','run','--project',path,'-c','Release','--',*(args or [str(a.output/name)])])

for name in ['final-raster','alpha-compatibility','final-paint','skp-documents','embedded-bitmaps','svg-documents']:
    run(name,['python',f'tools/check-{name}.py','--output',str(a.output/name)])
project('skp-validation','tests/SkpDocuments/Validation')
project('float-simd','tests/FloatPaintSimd')
for variant in ['Original','Managed']:
    project('xps-'+variant,'tests/XpsDocuments/'+variant)
    project('xps-read-'+variant,'tests/XpsDocuments/Reader',
            [str(a.output/('xps-'+variant)/'pages.xps'),str(a.output/('xps-read-'+variant))])
    project('lcd-'+variant,'tests/LcdText/'+variant)
for name in ['GlContext','GlSystem','GlInterop','VulkanInterop','D3DContext','D3DInterop']:
    run(name,['python','tools/check-gl-interop.py','--project',name,'--output',str(a.output/name)])
for name in ['MetalContext']:
    for variant in ['Original','Managed']:
        project(name+'-'+variant,'tests/'+name+'/'+variant)
run('gpu-pending',['python','tools/check-gpu-pending.py','--output',str(a.output/'gpu-pending')])
project('gpu-optimization','tests/GpuOptimization',['baseline/fonts/ZenOldMincho-Regular.ttf'])
raise SystemExit(0 if all(row['exitCode']==0 for row in rows) else 2)
