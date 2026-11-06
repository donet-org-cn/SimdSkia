"""Plots from frozen measurements; run only after all timed workers finish."""
import os,sys,json
from pathlib import Path
sys.path.insert(0,str(Path('artifacts/report-python').resolve()));os.environ['MPLCONFIGDIR']=str(Path('artifacts/mpl-api99').resolve())
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
root=Path('docs/evidence/public-api-99-2026-11-07');out=Path('docs/images');out.mkdir(exist_ok=True)
load=lambda p:json.loads((root/p).read_text(encoding='utf-8'))
data=load('api99-timing-2/summary.json');y=np.arange(len(data));fig,ax=plt.subplots(figsize=(10,4.6),layout='constrained')
for i,r in enumerate(data):
 a=r['original']['msPerFrame'];b=r['after']['msPerFrame'];ax.plot([a,b],[i,i],color='#b8c7d8',lw=3);ax.scatter(a,i,color='#667487',s=55,label='SkiaSharp 3.119.4' if i==0 else None,zorder=3);ax.scatter(b,i,color='#007eaf',s=55,label='SimdSkia current source' if i==0 else None,zorder=3)
 ax.annotate(f"{b/a:.2f}x original",(max(a,b),i),xytext=(8,0),textcoords='offset points',va='center',fontsize=9)
ax.set(yticks=y,yticklabels=[r['scene']+(' * quality differs' if r['scene']=='bilingual' else '') for r in data],xlabel='Completed CPU frame, ms (log scale; lower is better)',title='New API workloads: throughput remains uneven');ax.set_xscale('log');ax.set_xlim(.01,80);ax.invert_yaxis();ax.grid(axis='x',alpha=.2);ax.legend(loc='lower right');fig.savefig(out/'api99-speed-2026-11-07.png',dpi=160);plt.close(fig)
ui=load('ui-final-timing-2/summary.json');fig,ax=plt.subplots(figsize=(10,4.4),layout='constrained');x=np.arange(len(ui));width=.24
for offset,(v,label,color) in enumerate([('cpu','CPU','#167c94'),('vulkan','Vulkan complete + readback','#dd9234'),('direct2d','Direct2D complete + readback','#8872b3')]):
 ratios=[r['cpuRatioToFasterOriginal'] if v=='cpu' else max(r[v+'RatioToVulkan'],r[v+'RatioToAngle']) for r in ui];bars=ax.bar(x+(offset-1)*width,ratios,width,label=label,color=color);ax.bar_label(bars,fmt='%.2fx',padding=3,fontsize=9)
ax.axhline(1,color='#555',lw=1,ls='--');ax.set(xticks=x,xticklabels=[f"{r['scene']} {r['scale']}x" for r in ui],ylabel='Time / faster original in the same CPU or GPU scope',title='Avalonia completed offscreen frames: speed only; quality gate separate');ax.legend(fontsize=8);ax.set_ylim(0,max(p.get_height() for p in ax.patches)*1.3);ax.grid(axis='y',alpha=.2);fig.savefig(out/'api99-ui-speed-2026-11-07.png',dpi=160);plt.close(fig)
r=next(r for r in ui if r['scene']=='controls' and r['scale']==1);names=['original-cpu-default','cpu','original-vulkan','vulkan','original-angle','direct2d'];x=np.arange(len(names));fig,ax=plt.subplots(figsize=(10,4.4),layout='constrained')
for delta,key,label,color in [(-.18,'sampledMedianWorkingSetMiB','Working set','#597ea7'),(.18,'sampledMedianPrivateMiB','Private bytes','#cf8e57')]:
 bars=ax.bar(x+delta,[r[n][key] for n in names],.36,label=label,color=color);ax.bar_label(bars,fmt='%.1f',padding=3,fontsize=8)
ax.set(xticks=x,xticklabels=['Skia CPU','Simd CPU','Skia Vulkan','Simd Vulkan','Skia ANGLE','Simd Direct2D'],ylabel='MiB, process sampling median',title='Controls 100% memory: process usage, not GPU VRAM');ax.legend();ax.grid(axis='y',alpha=.2);ax.set_ylim(0,max(p.get_height() for p in ax.patches)*1.2);fig.savefig(out/'api99-ui-memory-2026-11-07.png',dpi=160);plt.close(fig)
print('Wrote 3 measured performance/memory figures')
