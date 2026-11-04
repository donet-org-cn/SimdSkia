"""Publish reproducible local CPU/GPU/codec results without changing any gates."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('input',type=Path)
p.add_argument('--output',type=Path,required=True)
p.add_argument('--date',default='2026-11-01')
args=p.parse_args();source=args.input.resolve();out=args.output.resolve()
if (out/'sha256.json').exists():raise ValueError('Completed evidence exists; choose a new output directory')
out.mkdir(parents=True,exist_ok=True)  # Resume a failed plotting dependency setup.
def read(path):return json.loads((source/path).read_text(encoding='utf-8-sig'))
cpu=read('cpu/results.json');gpu=read('gpu/summary.json');codec=read('codecs/summary.json')
evidence={'cpu':cpu,'gpu':{'summary':gpu,'runs':read('gpu/runs.json'),'quality':read('gpu/quality.json'),'gate':read('gpu/gate.json')},
          'codecs':{'summary':codec,'runs':read('codecs/runs.json'),'quality':read('codecs/quality.json')},
          'manifests':{key:read(path) for key,path in [('cpu','cpu/manifest.json'),('gpu','gpu/manifest.json'),('codecs','codecs/manifest.json')]},
          'scope':'Windows x64 .NET 10 JIT; local unpublished Sdcb integration. Not Native AOT or monitor FPS. Existing failed gates retained.'}
hardware=source/'hardware.json'
if hardware.exists():evidence['hardware']=json.loads(hardware.read_text(encoding='utf-8-sig'))
(out/'results.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for path in ['cpu/summary.md','cpu/diff-controls-dpi1.png']:
    shutil.copyfile(source/path,out/Path(path).name)

# Standard plotting only; these figures visualize the recorded medians.
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
cases=[('controls',1),('controls',1.5),('scroll',1)]
labels=['Controls 100%','Controls 150%','Scroll 100%']
fig,axes=plt.subplots(1,2,figsize=(11,4.2),layout='constrained')
for offset,variant,label,color in [(-.19,'default','SkiaSharp','#64748b'),(.19,'after','SimdSkia','#0284c7')]:
    rows=[next(r for r in cpu['summary'] if (r['scenario'],r['scale'],r['variant'])==(scene,scale,variant)) for scene,scale in cases]
    for ax,field,title in zip(axes,['millisecondsPerFrame','workingSetMiB'],['CPU rendering (ms/frame, lower is better)','CPU process working set (MiB, lower is better)']):
        bars=ax.bar([i+offset for i in range(3)],[r[field] for r in rows],.36,label=label,color=color)
        ax.bar_label(bars,fmt='%.2f',fontsize=8,padding=3);ax.set_title(title,fontsize=10);ax.set_xticks(range(3),labels,fontsize=9)
        ax.set_ylim(0,max(r[field] for r in cpu['summary'] if r['variant'] in ('default','after'))*1.2);ax.spines[['top','right']].set_visible(False)
axes[0].legend();fig.suptitle(f'{args.date} | .NET 10 JIT | CPU quality gate: FAIL',fontsize=11)
fig.savefig(out/'cpu.png',dpi=160);plt.close(fig)
fig,axes=plt.subplots(1,2,figsize=(11,4.2),layout='constrained')
for offset,variant,label,color in [(-.30,'original-vulkan','Skia Vulkan','#64748b'),(-.10,'original-angle','Skia ANGLE','#94a3b8'),(.10,'vulkan','Simd Vulkan','#0284c7'),(.30,'direct2d','Simd Direct2D','#0d9488')]:
    for ax,field,title in zip(axes,['meanMs','sampledMedianWorkingSetMiB'],['GPU completed frame + readback (ms)','GPU process working set (MiB)']):
        ax.bar([i+offset for i in range(3)],[r[variant][field] for r in gpu],.19,label=label,color=color)
        ax.set_title(title,fontsize=10);ax.set_xticks(range(3),labels,fontsize=9);ax.spines[['top','right']].set_visible(False)
axes[0].legend(fontsize=8,ncols=2);fig.suptitle(f'{args.date} | .NET 10 JIT | GPU quality gate: FAIL',fontsize=11)
fig.savefig(out/'gpu.png',dpi=160);plt.close(fig)
fig,ax=plt.subplots(figsize=(9,4.5),layout='constrained')
names=[r['scene']+' / '+r['operation'] for r in codec]
ratios=[r['simdTimeRatio'] for r in codec]
bars=ax.barh(names,ratios,color=['#0284c7' if r<=1 else '#dc2626' for r in ratios]);ax.axvline(1,color='#475569',linestyle='--')
ax.bar_label(bars,fmt='%.2fx',padding=3,fontsize=8);ax.set_xlim(0,max(ratios)*1.23)
ax.set_xlabel('SimdSkia time / original SkiaSharp time (lower is better)');ax.invert_yaxis();ax.spines[['top','right']].set_visible(False)
ax.set_title('1080p codec adapter comparison | .NET 10 JIT | 3-process medians',fontsize=11)
fig.savefig(out/'codecs.png',dpi=160);plt.close(fig)
hashes={f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in out.iterdir() if f.is_file()}
(out/'sha256.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8')
print(f'Saved evidence and three performance charts: {out}')
