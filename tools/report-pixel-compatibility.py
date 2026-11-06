"""Archive the dated pixel/API evidence and render developer-only performance charts."""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import shutil
import sys
import zipfile
p=argparse.ArgumentParser();p.add_argument('--input',type=Path,default=Path('artifacts/pixel-api-2026-11-06'));p.add_argument('--output',type=Path,default=Path('docs/evidence/pixel-api-2026-11-06'));a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
load=lambda f:json.loads(f.read_text(encoding='utf-8-sig'))
write=lambda f,v:f.write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8')
for name in ['manifest','summary','runs']:shutil.copy2(a.input/'performance-final'/(name+'.json'),a.output/('performance-'+name+'.json'))
for src,dst in [('checks-complete/comparison.json','comparison.json'),('checks-complete/source-hashes.json','source-hashes.json'),('stroke/comparison.json','stroke-regression.json'),('regression.json','regression.json')]:shutil.copy2(a.input/src,a.output/dst)
shutil.copy2(a.input/'pixel-before-after.json',a.output/'pixel-before-after.json')
for name in ['build','basic','basic-old-stride-assertion','pixel-release','pixel','pixel-fallback','optimization','optimization-fallback','codec','pdf','features','controls','gpu','direct2d','geometry','shader','stroke','imaging','example','aot-build','aot','performance-final']:
    shutil.copy2(a.input/(name+'.log'),a.output/(name+'.log'))
shutil.copy2('docs/api/comparison.json',a.output/'api-comparison.json');shutil.copy2('artifacts/pixels.png',a.output/'example.png')
with zipfile.ZipFile(a.output/'pixel-oracle.zip','w',zipfile.ZIP_DEFLATED) as archive:
    for variant in ['original','managed']:
        for f in sorted((a.input/'checks-complete'/variant).iterdir()):
            if f.suffix in ['.bin','.json']:archive.write(f,variant+'/'+f.name)
old=load(Path('docs/evidence/stroke-api-2026-11-06/api-comparison.json'));current=load(Path('docs/api/comparison.json'))
added=sorted(set(old['MissingOrDifferent'])-set(current['MissingOrDifferent']));assert len(added)==133;write(a.output/'new-api.json',added)
import re
groups=collections.Counter(re.search(r'SkiaSharp\.([\w]+)',s)[1] for s in current['MissingOrDifferent']);write(a.output/'remaining-by-type.json',dict(groups.most_common()))
checks=load(a.output/'comparison.json');perf=load(a.output/'performance-summary.json')
status=dict(published=False,apiMatches=current['MatchingEntries'],missing=len(current['MissingOrDifferent']),extra=len(current['AddedOrDifferent']),newMetadata=133,publicTypes=70,fullCompatibility=False,metadataPassed=checks['metadataPassed'],metadataOutsideSpanPassed=checks['metadataOutsideSpanPassed'],spanDifferenceFields=len(checks['knownPaddedSpanDifferences']),pixelPass=checks['pixelPass'],pixelTotal=checks['pixelTotal'],managedChecks=1358,nativeAotChecks=10,formalPerformanceProcesses=9,allMicrobenchmarkQualityPassed=all(r['qualityPassed'] for r in perf),newWholeAvaloniaPerformance=False,newGpuPerformance=False,strictJpegGateStillFails=True,strictStrokeGateStillFails=True)
previous=load(a.output/'pixel-before-after.json');assert previous['commonCases']==246 and not previous['changedCases'];status['beforeAfterExactCases']=246
write(a.output/'status.json',status)
write(a.output/'upstream-source-hashes.json',{str(f.name):hashlib.sha256(f.read_bytes()).hexdigest() for f in (a.input/'upstream').glob('*.cs')})
sys.path.insert(0,str(Path('artifacts/report-python').resolve()));import matplotlib
matplotlib.use('Agg');import matplotlib.pyplot as plt
fig,axes=plt.subplots(1,2,figsize=(13,5),layout='constrained');labels=['Read 1080p','RGBA/BGRA 1080p','Nearest 512','Linear 512']
for i,variant in enumerate(['original','before','after']):
    x=[v+(i-1)*.25 for v in range(4)]
    bars=axes[0].bar(x,[r[variant]['msPerFrame'] for r in perf],width=.24,label=variant);axes[0].bar_label(bars,fmt='%.3f',fontsize=7,padding=2)
    axes[1].bar(x,[r[variant]['workingSetMiB'] for r in perf],width=.24,label=variant)
axes[0].set_yscale('log');axes[0].set_ylim(.08,22);axes[0].set_ylabel('ms per operation (log)');axes[1].set_ylabel('Process working set, MiB');axes[1].set_ylim(0,90)
for axis in axes:axis.set_xticks(range(4),labels,rotation=12);axis.legend();axis.grid(axis='y',alpha=.2);axis.set_axisbelow(True)
fig.suptitle('Pixel API: .NET 10.0.14 CPU JIT, 3 rotating serial forks\nFour reused-buffer scenes per process; memory includes prior scenes and managed heap retention',fontsize=11)
fig.savefig(a.output/'cpu-memory.png',dpi=150);plt.close(fig)
write(a.output/'sha256.json',{str(f.relative_to(a.output)):hashlib.sha256(f.read_bytes()).hexdigest() for f in a.output.rglob('*') if f.is_file() and f.name!='sha256.json'})
print(json.dumps(status,indent=2))
