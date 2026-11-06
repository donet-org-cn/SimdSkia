"""Capture current independent regression statuses; known failures remain nonzero."""
import argparse,datetime,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh directory.')
a.output.mkdir(parents=True);runs=[]
font='baseline/fonts/ZenOldMincho-Regular.ttf'
suites=[('basic','tests/SimdSkia.Tests',[]),('pixels','tests/PixelCompatibility/Managed',[str(a.output/'pixels-data')]),('codecs','tests/CodecIntegration',[]),('pdf','tests/Pdf',[]),('features','tests/Features',[font]),('platform-fonts','tests/PlatformFonts',[]),('optimization','tests/Optimization',[font]),('avalonia','tests/Avalonia',[font,str(a.output/'avalonia-data')]),('controls','tests/AvaloniaControls',['--frames','5','--output',str(a.output/'controls-data')]),('direct2d','tests/Direct2D',[]),('vulkan','tests/Gpu',[]),('gpu-sampling','tests/GpuSampling',[]),('jpeg-known-failure','tests/Imaging',[])]
for name,project,args in suites:
 cmd=['dotnet','run','--project',project,'-c','Release','--no-build','--',*args];start=datetime.datetime.now(datetime.timezone.utc).isoformat()
 with (a.output/(name+'.log')).open('w',encoding='utf-8') as f:r=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
 runs.append(dict(suite=name,command=cmd,startedUtc=start,exitCode=r.returncode));(a.output/'status.json').write_text(json.dumps(runs,indent=2)+'\n',encoding='utf-8');print(name,r.returncode,flush=True)
raise SystemExit(0 if all(r['exitCode']==0 for r in runs) else 2)
