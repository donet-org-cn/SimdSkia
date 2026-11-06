"""Archive reproducible shader evidence. Plot dependencies are dev-only in artifacts."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sys

p=argparse.ArgumentParser()
p.add_argument('--input',type=Path,default=Path('artifacts/shader-2026-11-06'))
p.add_argument('--output',type=Path,default=Path('docs/evidence/shader-compatibility-2026-11-06'))
a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
for name in ['regression.json','build-final.log','verify.log','optimization-final.log','optimization-fallback.log','shader-fallback.log','aot.log','aot-build.log','direct2d.log','vulkan-api.log','gpu-sampling.log','gpu-optimization.log','avalonia-controls.log','glyph-batches.log','shaders.log','geometry.log','benchmark-build.log','pack.log','package-check.json']:
    shutil.copy2(a.input/name,a.output/name)
for name in ['summary.json','manifest.json','runs.json']:
    shutil.copy2(a.input/'performance-final'/name,a.output/('performance-'+name))
shutil.copy2(a.input/'api-added.json',a.output/'api-added.json')
for name in ['comparison.json','source-hashes.json','managed/managed-checks.json']:
    shutil.copy2(a.input/'checks-final'/name,a.output/Path(name).name)
for scene in ['linear-hard-stops','sweep-Repeat','degenerate-alpha-Mirror','local-nested']:
    for variant in ['original','managed']:
        shutil.copy2(a.input/'checks-final'/variant/(scene+'.png'),a.output/(scene+'-'+variant+'.png'))
    shutil.copy2(a.input/'checks-final'/(scene+'-diff-x8.png'),a.output/(scene+'-diff-x8.png'))
shutil.copy2('artifacts/gradients.png',a.output/'example.png')
shutil.copy2('benchmarks/ShaderCompatibility/Program.cs',a.output/'benchmark-program.cs.txt')
api=json.loads(Path('docs/api/comparison.json').read_text(encoding='utf-8'))
shutil.copy2('docs/api/comparison.json',a.output/'api-comparison.json')
status=dict(published=False,baseline='SkiaSharp 3.119.4',apiMatches=api['MatchingEntries'],baselineEntries=api['BaselineEntries'],missing=len(api['MissingOrDifferent']),extraSignatures=len(api['AddedOrDifferent']),newMatchesThisBatch=15,fullCompatibility=False,newGpuTiming=False,newWholeAvaloniaMemoryTiming=False,knownFailures=['Default Imaging strict JPEG gate remains failing (previously recorded 21/24).','Prior real whole-page original CPU/GPU pixel gates remain failing; not replaced by the scoped shader gate.'],scope='50 gradient scenes; 30 exact erase fixtures; 480 erase scalar checks; 472085 exact optimization checks with SIMD enabled and hardware intrinsics disabled; actual Native AOT 14 checks; Windows Vulkan/Direct2D regressions')
(a.output/'status.json').write_text(json.dumps(status,indent=2)+'\n',encoding='utf-8')

sys.path.insert(0,str(Path('artifacts/report-python').resolve()))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
rows=json.loads((a.output/'performance-summary.json').read_text(encoding='utf-8'))
labels=['Erase: tight','Erase: padded','Gradient: horizontal','Gradient: diagonal']
colors={'original':'#64748b','before':'#e28d41','after':'#1b9e8d'}
fig,axes=plt.subplots(1,2,figsize=(14,5),layout='constrained')
for j,variant in enumerate(['original','before','after']):
    x=[i+(j-1)*.25 for i in range(4)]
    bars=axes[0].bar(x,[r[variant]['msPerFrame'] for r in rows],width=.24,label=variant,color=colors[variant])
    axes[0].bar_label(bars,fmt='%.3f',fontsize=7,padding=2)
    axes[1].bar(x,[r[variant]['workingSetMiB'] for r in rows],width=.24,label=variant,color=colors[variant])
axes[0].set_yscale('log');axes[0].set_ylim(.01,30);axes[0].set_ylabel('Wall time, ms/frame (log scale)');axes[0].set_title('512 x 512 CPU: lower is faster')
axes[1].set_ylabel('Process working set, MiB');axes[1].set_ylim(0,55);axes[1].set_title('Memory includes runtime / JIT; lower is smaller')
for axis in axes:
    axis.set_xticks(range(4),labels,rotation=14);axis.legend();axis.grid(axis='y',alpha=.2);axis.set_axisbelow(True)
fig.suptitle('SimdSkia: shader / erase batch, Windows x64 .NET 10.0.14 JIT\n3 serial forks; opaque two-stop gradient; before/after pixels exactly equal',fontsize=12)
fig.savefig(a.output/'cpu-memory.png',dpi=150);plt.close(fig)
hashes={str(path.relative_to(a.output)):hashlib.sha256(path.read_bytes()).hexdigest() for path in a.output.rglob('*') if path.is_file() and path.name!='sha256.json'}
(a.output/'sha256.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8')
print(json.dumps(status,indent=2))
