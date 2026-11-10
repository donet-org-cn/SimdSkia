"""Serial Windows regression sweep before performance collection.

Exit 2 records failed original-reference gates. It never counts them as passes.
No benchmark timing is collected concurrently with this sweep.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
output=args.output.resolve()
output.mkdir(parents=True,exist_ok=False)
rows=[]

def save():
    (output/'results.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def run(name,command,*,reference=False,env=None):
    started=time.monotonic()
    try:
        with (output/(name+'.log')).open('wb') as log:
            result=subprocess.run(command,cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=600)
        code=result.returncode
        status='passed' if code==0 else 'reference-gate-failed' if reference and code==2 else 'failed'
    except subprocess.TimeoutExpired:
        code=None
        status='timeout'
    rows.append(dict(name=name,command=command,exitCode=code,status=status,seconds=time.monotonic()-started))
    save()
    print(name,status,flush=True)

font='baseline/fonts/ZenOldMincho-Regular.ttf'
projects={
    'SimdSkia.Tests':[], 'Features':[font], 'Imaging':['--independent-only'],
    'Optimization':[font], 'FloatPaintSimd':[str(output/'float-paint')], 'RuntimeNumeric':[str(output/'runtime-numeric')],
    'CodecIntegration':[str(output/'codecs')], 'Pdf':[str(output/'pdf')],
    'PlatformFonts':[], 'WindowsSystemFonts':[], 'GlyphBatches':[],
    'GpuPreferred':['--hardware'], 'Gpu':[], 'GpuSampling':[], 'GpuOptimization':[font],
    'Direct2D':[], 'AvaloniaGpu':[], 'CacheControls/Managed':[],
    'CompatibilityExpansion/Managed':[str(output/'compat-expansion')],
    'Avalonia':[font,str(output/'avalonia')],
    'AvaloniaControls':['--frames','5','--output',str(output/'controls')],
}
for project,extra in projects.items():
    run(project.replace('/','-'),['dotnet','run','--project','tests/'+project,'-c','Release','--no-build','--',*extra])

for variable,value,label in [('DOTNET_EnableAVX2','0','sse2'),('DOTNET_EnableHWIntrinsic','0','no-intrinsics')]:
    env=dict(os.environ,**{variable:value})
    for project,extra in [('FloatPaintSimd',[str(output/('float-'+label))]),('RuntimeNumeric',[str(output/('numeric-'+label))]),('CodecIntegration',[str(output/('codec-'+label))]),('Optimization',[font])]:
        run(project+'-'+label,['dotnet','run','--project','tests/'+project,'-c','Release','--no-build','--',*extra],env=env)

for script in sorted((ROOT/'tools').glob('check-*.py')):
    if script.stem in ('check-final-api-baseline','check-optimization-regressions'):
        continue
    run(script.stem,[sys.executable,str(script),'--output',str(output/script.stem)],reference=True)

for suite in ['PathOperations','PathMeasure','FontOperations','ImageFactories','Regions']:
    name='foundations-'+suite
    run(name,[sys.executable,'tools/check-foundations.py','--suite',suite,'--output',str(output/name)],reference=True)
for project in ['GlContext','GlSystem','D3DContext','D3DInterop','VulkanInterop']:
    name='interop-'+project
    run(name,[sys.executable,'tools/check-gl-interop.py','--project',project,'--output',str(output/name)],reference=True)

implemented=output/'implemented.json'
comparison=output/'api-comparison.json'
run('api-extract',['dotnet','run','--project','tools/ApiAudit','-c','Release','--no-build','--','extract',
    'src/SimdSkia.Compatibility/bin/Release/net10.0/SimdSkia.Compatibility.dll',str(implemented)])
run('api-compare',['dotnet','run','--project','tools/ApiAudit','-c','Release','--no-build','--','compare',
    'docs/api/baseline-3.119.4.json',str(implemented),str(comparison)],reference=True)
run('api-scope',[sys.executable,'tools/check-final-api-baseline.py','--comparison',str(comparison)])

files={}
for directory,names,filenames in os.walk(ROOT/'src'):
    names[:]=[name for name in names if name not in ('bin','obj')]
    for filename in filenames:
        path=Path(directory)/filename
        if path.suffix in ('.cs','.csproj','.comp','.spv'):
            files[str(path.relative_to(ROOT))]=hashlib.sha256(path.read_bytes()).hexdigest()
(output/'source-sha256.json').write_text(json.dumps(files,indent=2)+'\n',encoding='utf-8')
raise SystemExit(1 if any(row['status'] in ('failed','timeout') for row in rows) else 2 if any(row['status']=='reference-gate-failed' for row in rows) else 0)
