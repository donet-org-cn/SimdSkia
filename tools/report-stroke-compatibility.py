"""Archive the dated stroke/API batch. Matplotlib is only a local dev dependency."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sys
p=argparse.ArgumentParser();p.add_argument('--input',type=Path,default=Path('artifacts/stroke-2026-11-06'));p.add_argument('--output',type=Path,default=Path('docs/evidence/stroke-api-2026-11-06'));a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
load=lambda f:json.loads(Path(f).read_text(encoding='utf-8-sig'))
for name in ['manifest','runs','summary']:shutil.copy2(a.input/'performance-lru'/(name+'.json'),a.output/('performance-'+name+'.json'))
for path,name in [('checks-final/comparison.json','initial-checks.json'),('checks-release/comparison.json','comparison.json'),('checks-release/source-hashes.json','source-hashes.json'),('gpu/results.json','gpu.json'),('regression.json','regression.json')]:shutil.copy2(a.input/path,a.output/name)
for name in ['build','basic','basic-old-round-rejection','optimization','optimization-fallback','codec','pdf','features','controls','geometry','shaders','gpu-existing','d2d-existing','gpu','aot','aot-build','checks-final-run','performance-lru','example']:
    shutil.copy2(a.input/(name+'.log'),a.output/(name+'.log'))
shutil.copy2('artifacts/strokes.png',a.output/'example.png');shutil.copy2('docs/api/comparison.json',a.output/'api-comparison.json')
old=load('docs/evidence/conic-gpu-2026-11-06/api-comparison.json');current=load('docs/api/comparison.json');added=sorted(set(old['MissingOrDifferent'])-set(current['MissingOrDifferent']));assert len(added)==26
(a.output/'new-api.json').write_text(json.dumps(added,indent=2)+'\n',encoding='utf-8')
checks=load(a.output/'comparison.json');gpu=load(a.output/'gpu.json')
status=dict(published=False,apiMatches=current['MatchingEntries'],missing=len(current['MissingOrDifferent']),extra=len(current['AddedOrDifferent']),newMetadata=26,fullCompatibility=False,metadataPassed=checks['metadataPassed'],imagePass=sum(r['passed'] for r in checks['images']),imageTotal=len(checks['images']),gpuChecks=gpu['checks'],gpuPassed=gpu['passed'],nativeAotChecks=11,formalPerformanceProcesses=9,newWholeAvaloniaPerformance=False,unmodifiedFailureGates=True)
(a.output/'status.json').write_text(json.dumps(status,indent=2)+'\n',encoding='utf-8')
sys.path.insert(0,str(Path('artifacts/report-python').resolve()));import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
rows=load(a.output/'performance-summary.json');labels=['Repeated outline','8 reusable outlines','32 rounded lines']
fig,axes=plt.subplots(1,2,figsize=(13,4.8),layout='constrained')
for j,v in enumerate(['original','before','after']):
    x=[i+(j-1)*.25 for i in range(3)]
    bars=axes[0].bar(x,[r[v]['msPerFrame'] for r in rows],width=.24,label=v);axes[0].bar_label(bars,fmt='%.4f',fontsize=7,padding=2)
    axes[1].bar(x,[r[v]['workingSetMiB'] for r in rows],width=.24,label=v)
axes[0].set_yscale('log');axes[0].set_ylim(.0005,2);axes[0].set_ylabel('ms per 32 calls/draws (log)');axes[1].set_ylabel('Process working set, MiB');axes[1].set_ylim(0,55)
for axis in axes:axis.set_xticks(range(3),labels,rotation=10);axis.legend();axis.grid(axis='y',alpha=.2);axis.set_axisbelow(True)
fig.suptitle('Stroke API: .NET 10.0.14 CPU JIT, 3 serial forks\nOutline scenes measure reuse, not continually deforming paths; original hard-edge quality still fails',fontsize=11)
fig.savefig(a.output/'cpu-memory.png',dpi=150);plt.close(fig)
hashes={str(f.relative_to(a.output)):hashlib.sha256(f.read_bytes()).hexdigest() for f in a.output.rglob('*') if f.is_file() and f.name!='sha256.json'}
(a.output/'sha256.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8');print(json.dumps(status,indent=2))
