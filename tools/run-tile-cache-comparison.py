"""Same mutable ImageBrush workload in isolated managed/native processes."""
import argparse, hashlib, json, math, statistics, subprocess
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--output',type=Path,required=True)
p.add_argument('--before-directory',type=Path,required=True,help='New common test host paired with frozen old engine DLLs.')
p.add_argument('--runs',type=int,default=3)
p.add_argument('--system-fonts',action='store_true',help='Use normal installed-font setup instead of registering the unused control-test font file; the workload draws no text.')
a=p.parse_args()
if a.runs<1:raise SystemExit('runs must be positive')
if (a.output/'manifest.json').exists():raise SystemExit('Use a fresh output directory')
a.output.mkdir(parents=True,exist_ok=True)
engines={'before':a.before_directory/'SimdSkia.Avalonia.Controls.Tests.dll',
         'after':Path('tests/AvaloniaControls/bin/Release/net8.0/SimdSkia.Avalonia.Controls.Tests.dll'),
         'reference':Path('tests/AvaloniaControls.Reference/bin/Release/net8.0/AvaloniaControls.Reference.dll')}
files=sorted({p.resolve() for binary in engines.values() for pattern in ['*.dll','*.deps.json','*.runtimeconfig.json'] for p in binary.parent.glob(pattern)})
hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
(a.output/'manifest.json').write_text(json.dumps({'scenario':'160 updates of one 512x512 source / 320x240 ImageBrush tile; 8 warmup updates; CPU only',
    'engines':{k:str(v.resolve()) for k,v in engines.items()},'runtimeFileSha256':hashes,'runs':a.runs,
    'fontSetup':'automatic system discovery; no text or font face selection' if a.system_fonts else 'existing controls worker file-font registration; no text'},indent=2)+'\n')
rows=[];names=list(engines)
for run in range(a.runs):
    for name in names[run%3:]+names[:run%3]:
        dest=a.output/f'{name}-{run+1}';dest.mkdir()
        with (dest/'process.log').open('wb') as log:
            extra=['--bench','--system-font-family','Microsoft YaHei UI'] if a.system_fonts else []
            result=subprocess.run(['dotnet',str(engines[name]),'--tile-cache-probe','--output',str(dest),*extra],stdout=log,stderr=subprocess.STDOUT,timeout=90)
        if result.returncode:raise SystemExit(f'Worker failed: {dest}')
        row=json.loads((dest/'metrics.json').read_text());row.update(engine=name,run=run+1);rows.append(row)
        print(f"{name} #{run+1}: {row['millisecondsPerFrame']:.3f}ms, retained {row['managedRetainedAfterGcMiB']:.2f}MiB, private {row['privateMiB']:.1f}MiB",flush=True)
quality=[]
for run in range(1,a.runs+1):
    before=(a.output/f'before-{run}/scene.rgba').read_bytes();after=(a.output/f'after-{run}/scene.rgba').read_bytes();native=(a.output/f'reference-{run}/scene.rgba').read_bytes()
    assert len(before)==len(after)==len(native)==320*240*4
    differences=[abs(x-y) for x,y in zip(after,native)]
    maximum=max(differences);rmse=math.sqrt(sum(d*d for d in differences)/len(differences))
    quality.append({'run':run,'beforeAfterExactlyEqual':before==after,'nativeMaximum':maximum,'nativeRmse':rmse,'maximumThreshold':2,'rmseThreshold':.5,'passed':maximum<=2 and rmse<=.5,
        'sha256':{name:hashlib.sha256((a.output/f'{name}-{run}/scene.rgba').read_bytes()).hexdigest() for name in names}})
summary=[]
for name in names:
    group=[r for r in rows if r['engine']==name]
    summary.append({'engine':name,**{key:statistics.median(r[key] for r in group) for key in ['millisecondsPerFrame','cpuMillisecondsPerFrame','allocatedBytesPerFrame','workingSetMiB','privateMiB','managedRetainedAfterGcMiB']}})
for path,sha in hashes.items():
    if hashlib.sha256(Path(path).read_bytes()).hexdigest()!=sha:raise SystemExit('Worker binary changed during measurement')
(a.output/'results.json').write_text(json.dumps({'runs':rows,'summary':summary,'quality':quality},indent=2)+'\n')
if not all(q['beforeAfterExactlyEqual'] and q['passed'] for q in quality):raise SystemExit(2)
print('PASS unchanged pixels and original image quality gate')
