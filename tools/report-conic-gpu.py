"""Archive the completed API/GPU batch and draw its measured CPU/memory charts."""
import argparse
import difflib
import hashlib
import json
from pathlib import Path
import shutil
import statistics
import sys

p=argparse.ArgumentParser();p.add_argument('--input',type=Path,default=Path('artifacts/conic-2026-11-06'));p.add_argument('--output',type=Path,default=Path('docs/evidence/conic-gpu-2026-11-06'));a=p.parse_args()
root=a.input;out=a.output;out.mkdir(parents=True,exist_ok=True)
shutil.copy2('artifacts/conics.png',out/'example.png')
load=lambda f:json.loads(Path(f).read_text(encoding='utf-8-sig'))
assert load(root/'ui-backends/manifest.json')['complete'];assert load(root/'gpu-cache-pair/manifest.json')['complete']
for folder,names in [('ui-backends',['manifest','runs','summary','quality','gate']),('gpu-cache-pair',['manifest','runs','summary']),('desktop',['manifest','summary']),('cpu-conics',['manifest','runs','summary'])]:
    for name in names:shutil.copy2(root/folder/(name+'.json'),out/(folder+'-'+name+'.json'))
desktop_runs=[dict(folder=f.parent.name,metrics=load(f)) for f in sorted((root/'desktop').glob('*/benchmark.json'))];assert len(desktop_runs)==9
(out/'desktop-runs.json').write_text(json.dumps(desktop_runs,indent=2)+'\n',encoding='utf-8')
for source,dest in [('final-v4/comparison.json','conic-comparison.json'),('final-v4/source-hashes.json','conic-source-hashes.json'),('gpu-all/results.json','conic-gpu.json'),('geometry-final/comparison.json','geometry-comparison.json'),('cache-regression.json','cache-regression.json'),('regression.json','regression.json')]:shutil.copy2(root/source,out/dest)
for name in ['conic-final','gpu-all','optimization','optimization-cache','aot-cache','build-cache','gpu-cache-regression','gpu-cache-sampling','gpu-cache-kernels','d2d-cache-tests','avalonia-cache','ui-backends','gpu-cache-pair','desktop']:
    shutil.copy2(root/(name+'.log'),out/(name+'.log'))
for name in ['optimization-fallback.log','build-final.log']:shutil.copy2(root/name,out/name)
shutil.copy2('docs/api/comparison.json',out/'api-comparison.json')
old=load('docs/evidence/shader-compatibility-2026-11-06/api-comparison.json');current=load('docs/api/comparison.json')
added=sorted(set(old['MissingOrDifferent'])-set(current['MissingOrDifferent']));assert len(added)==15
(out/'api-new-signatures.json').write_text(json.dumps(added,indent=2)+'\n',encoding='utf-8')
status=dict(published=False,matchingApi=1120,baselineApi=3235,remainingApi=2115,extraSignatures=0,defaultBackend='CPU',fullCompatibility=False,conicMetadataPassed=True,conicPixelPassed=False,wholePagePixelPassed=False,timedProcesses=129,cacheBeforeAfterExact=True,gpuDefaultEnabled=False,gpuDefaultReason='CPU is faster and smaller in the tested controls; transparent native windows/device startup need explicit choices.',platformMeasured='Windows x64 only; .NET 10.0.14 JIT and GTX 1050 Ti',vramMeasured=False)
(out/'status.json').write_text(json.dumps(status,indent=2)+'\n',encoding='utf-8')
micro=[]
for backend in ['vulkan','direct2d']:
    row=dict(backend=backend);hashes=set();runs=[]
    for version in ['before','after']:
        group=[load(f) for f in (root/f'gradient-{version}-runs').glob(backend+'-*/metrics.json')];assert len(group)==3
        for r in group:hashes.add(r['pixelSha256']);runs.append(dict(version=version,**r))
        row[version]={key:statistics.median(r[key] for r in group) for key in ['msPerFrame','cpuMsPerFrame','allocatedBytesPerFrame','workingSetMiB','privateMiB']}
    row['beforeAfterExact']=len(hashes)==1;assert row['beforeAfterExact'];row['runs']=runs;micro.append(row)
(out/'gradient-micro.json').write_text(json.dumps(micro,indent=2)+'\n',encoding='utf-8')
micro_files={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for version in ['before','after'] for f in (root/f'gradient-{version}').rglob('*') if f.is_file() and f.suffix in ['.dll','.json']}
(out/'gradient-micro-manifest.json').write_text(json.dumps(dict(files=micro_files,recordedAfterRuns=True,note='Frozen output folders retained; unlike UI/pair runners these micro hashes were archived after collection.'),indent=2)+'\n',encoding='utf-8')
patch=''
for filename in load(root/'gpu-source-before/sha256.json'):
    before=(root/'gpu-source-before'/filename).read_text(encoding='utf-8');after=Path(filename).read_text(encoding='utf-8')
    patch+=''.join(difflib.unified_diff(before.splitlines(True),after.splitlines(True),fromfile='before/'+filename,tofile='after/'+filename))
(out/'gpu-cache.patch').write_text(patch,encoding='utf-8')
sources={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for d in ['src','tests/ConicCompatibility','benchmarks/GradientCache','benchmarks/AvaloniaGpuParity'] for f in Path(d).rglob('*.cs') if not {'obj','bin'}&set(f.parts)}
(out/'source-final.json').write_text(json.dumps(sources,indent=2)+'\n',encoding='utf-8')

sys.path.insert(0,str(Path('artifacts/report-python').resolve()))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
data=load(root/'ui-backends/summary.json');paired=load(root/'gpu-cache-pair/summary.json')
fig,axes=plt.subplots(2,2,figsize=(14,9),layout='constrained');labels=['Controls 100%','Controls 150%','Scroll']
for j,v in enumerate(['original-cpu-default','original-cpu','cpu']):
    axes[0,0].bar([i+(j-1)*.24 for i in range(3)],[r[v]['meanMs'] for r in data],width=.23,label=v)
for j,v in enumerate(['original-vulkan','original-angle','vulkan','direct2d']):
    values=[r[v]['meanMs'] if v.startswith('original') else next(x for x in paired if (x['scene'],x['scale'],x['backend'])==(r['scene'],r['scale'],v))['after']['meanMs'] for r in data]
    axes[0,1].bar([i+(j-1.5)*.2 for i in range(3)],values,width=.19,label=v)
for axis,key,title in [(axes[1,0],'sampledMedianWorkingSetMiB','Process working set'),(axes[1,1],'sampledMedianPrivateMiB','Process private memory')]:
    for j,v in enumerate(['original-cpu-default','cpu','original-angle','vulkan','direct2d']):
        values=[r[v][key] if v in ['original-cpu-default','cpu','original-angle'] else next(x for x in paired if (x['scene'],x['scale'],x['backend'])==(r['scene'],r['scale'],v))['after'][key] for r in data]
        axis.bar([i+(j-2)*.17 for i in range(3)],values,width=.16,label=v)
    axis.set_ylabel('MiB (VRAM not included)');axis.set_title(title)
axes[0,0].set_title('CPU complete frame');axes[0,1].set_title('GPU complete frame + readback')
for axis in axes[0]:axis.set_ylabel('ms/frame, lower is faster')
for axis in axes.flat:axis.set_xticks(range(3),labels);axis.legend(fontsize=7);axis.grid(axis='y',alpha=.2);axis.set_axisbelow(True)
fig.suptitle('Real Avalonia 12.1.5 controls, Windows x64 .NET 10.0.14 JIT\n63 reference/baseline processes + 36 paired GPU processes; original pixel gates still FAIL',fontsize=12)
fig.savefig(out/'ui-cpu-gpu-memory.png',dpi=150);plt.close(fig)
fig,axes=plt.subplots(1,2,figsize=(10,4),layout='constrained')
for j,version in enumerate(['before','after']):
    axes[0].bar([i+(j-.5)*.3 for i in range(2)],[r[version]['msPerFrame'] for r in micro],width=.28,label=version)
    axes[1].bar([i+(j-.5)*.3 for i in range(2)],[r[version]['allocatedBytesPerFrame'] for r in micro],width=.28,label=version)
axes[0].set_ylabel('ms/frame (log)');axes[0].set_yscale('log');axes[1].set_ylabel('managed allocated bytes/frame')
for axis in axes:axis.set_xticks(range(2),['Vulkan','Direct2D']);axis.legend();axis.grid(axis='y',alpha=.2);axis.set_axisbelow(True)
fig.suptitle('64 reused gradient tiles: completed GPU fence/readback\n3 forks, 384 x 256; before/after pixels exactly equal; not whole-app speedup',fontsize=11)
fig.savefig(out/'gradient-cache.png',dpi=150);plt.close(fig)
hashes={str(f.relative_to(out)):hashlib.sha256(f.read_bytes()).hexdigest() for f in out.rglob('*') if f.is_file() and f.name!='sha256.json'}
(out/'sha256.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8');print(str(out))
