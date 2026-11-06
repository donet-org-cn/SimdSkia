"""Freeze the final-source UI rerun and publish CPU/GPU/process-memory tables."""
import gzip,hashlib,json,shutil,statistics,sys
from pathlib import Path

source=Path('artifacts/api-final-2026-11-07/ui-preview3-final')
root=Path('docs/evidence/ui-preview3-2026-11-07')
assets=Path('docs/assets/ui-preview3-2026-11-07')
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
save=lambda p,v:p.write_text(json.dumps(v,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
sha=lambda b:hashlib.sha256(b).hexdigest()
manifest=load(source/'manifest.json');assert manifest['complete'] and manifest['forks']==3 and manifest['seconds']==5 and manifest['warmup']==3
ui=load(source/'summary.json');runs=load(source/'runs.json');quality=load(source/'quality.json');gate=load(source/'gate.json')
assert len(runs)==63 and len(ui)==3 and len(quality)==18
gpu=[r for r in runs if r['variant'] in ('vulkan','direct2d')]
assert all(r['diagnostics']['cpuFallbackCommands']==0 and r['diagnostics']['completedBatches']==r['frames'] for r in gpu)
if not root.exists():
 root.mkdir(parents=True)
 frames={}
 for file in sorted(source.rglob('*')):
  if not file.is_file() or file.suffix not in ('.json','.txt','.rgba','.png','.csv'):continue
  relative=file.relative_to(source).as_posix();data=file.read_bytes()
  if file.suffix=='.rgba':
   digest=sha(data);name='objects/'+digest+'.rgba.gz';dest=root/name;dest.parent.mkdir(exist_ok=True)
   if not dest.exists():dest.write_bytes(gzip.compress(data,mtime=0))
   frames[relative]=dict(length=len(data),sha256=digest,object=name)
  else:
   dest=root/relative;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
 save(root/'frame-objects.json',frames)
 for label in ('original','managed'):
  shutil.copyfile(f'artifacts/api-final-2026-11-07/ui-preview3-{label}-build.log',root/(label+'-build.log'))
 shutil.copyfile('artifacts/api-final-2026-11-07/ui-preview3-final.log',root/'collection.log')
 shutil.copyfile('artifacts/api-final-2026-11-07/ui-preview3-source-delta.json',root/'source-delta-from-earlier-ui.json')
 old=Path('artifacts/api-final-2026-11-07/ui-final-timing-1');equivalence=[]
 for row in ui:
  for variant in ('original-cpu-default','original-cpu','cpu','original-vulkan','original-angle','vulkan','direct2d'):
   name=f"{row['scene']}-{row['scale']}-{variant}-0/scene.rgba"
   # Harness directory names use integer scales as 1, not 1.0.
   left=(source/name).read_bytes();right=(old/name).read_bytes()
   equivalence.append(dict(path=name,byteExact=left==right,sha256=sha(left),earlierSha256=sha(right)))
 save(root/'output-equivalence.json',equivalence)
 save(root/'context.json',dict(version='0.1.2-preview.3',sourceCommit='82e9575971aff578762e879aea8afdbbdf9c811d',
      scope='JIT offscreen completed frames; not AOT speed or presented-window FPS.',
      allMeasuredGpuFallbackCommands=0,fullQualityPassed=gate['qualityPassed'],earlierOutputsAllByteExact=all(r['byteExact'] for r in equivalence)))
 save(root/'sha256.json',{p.relative_to(root).as_posix():sha(p.read_bytes()) for p in root.rglob('*') if p.is_file()})
for name,digest in load(root/'sha256.json').items():assert sha((root/name).read_bytes())==digest,name

sys.path.insert(0,str(Path('artifacts/report-python').resolve()))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
assets.mkdir(parents=True,exist_ok=True)
keys=['original-cpu-default','original-cpu','cpu','original-vulkan','original-angle','vulkan','direct2d']
labels=['Skia CPU default','Skia CPU gray','SimdSkia CPU','Skia Vulkan','Skia ANGLE','SimdSkia Vulkan','SimdSkia Direct2D']
colors=['#697787','#9aa6b2','#2384c6','#697787','#9aa6b2','#2384c6','#37a38a']
plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False,'figure.facecolor':'white','savefig.facecolor':'white'})
def figure_save(fig,name):
 for extension in ('png','svg'):fig.savefig(assets/(name+'.'+extension),dpi=170,bbox_inches='tight')
 plt.close(fig)
fig,axes=plt.subplots(1,3,figsize=(15,4.8))
for ax,row in zip(axes,ui):
 values=[row[k]['meanMs'] for k in keys];ax.barh(labels,values,color=colors);ax.invert_yaxis();ax.set_xlim(0,max(values)*1.24)
 ax.set_title(f"{row['scene']} / scale {row['scale']}");ax.set_xlabel('Completed frame, ms (lower is faster)');ax.grid(axis='x',alpha=.2);ax.set_axisbelow(True)
 for i,value in enumerate(values):ax.text(value+max(values)*.015,i,f'{value:.3f}',va='center',fontsize=9)
fig.suptitle('Final preview.3 source / Avalonia 12.1.5 / JIT / 3 forks',fontsize=15)
fig.text(.5,-.02,'Whole-page quality gate: FAIL. Completed GPU readback included. Not presentation FPS.',ha='center',color='#a02d36');fig.tight_layout();figure_save(fig,'ui-time')
for name,metrics,titles,unit in [('ui-memory',['sampledMedianWorkingSetMiB','sampledMedianPrivateMiB'],['Working set','Private memory'],'Process MiB'),('ui-cpu',['cpuMsPerFrame','cpuPercentMachine'],['Process CPU ms/frame','Whole-machine CPU %'],'')]:
 fig,axes=plt.subplots(1,2,figsize=(12,4.8))
 for ax,metric,title in zip(axes,metrics,titles):
  values=[ui[0][k][metric] for k in keys];ax.barh(labels,values,color=colors);ax.invert_yaxis();ax.set_title(title+' / controls 1x');ax.set_xlabel(unit);ax.set_xlim(0,max(values)*1.24)
  for i,value in enumerate(values):ax.text(value+max(values)*.012,i,f'{value:.3f}' if name=='ui-cpu' else f'{value:.1f}',va='center',fontsize=9)
 fig.suptitle('Final preview.3 / sampled process memory, not VRAM' if name=='ui-memory' else 'Throughput mode: CPU time/frame and machine utilization are different metrics',fontsize=13)
 fig.tight_layout();figure_save(fig,name)
save(assets/'manifest.json',dict(generator=__file__,generatorSha256=sha(Path(__file__).read_bytes()),evidenceSha256=sha((root/'sha256.json').read_bytes()),files={p.name:sha(p.read_bytes()) for p in assets.iterdir() if p.suffix in ('.png','.svg')}))

ram=statistics.median(r['workingSetMiB']*1048576*100/r['workingSetPercentPhysicalRam'] for r in runs)
name_map=dict(zip(keys,['原版CPU默认','原版CPU灰度','SimdSkia CPU','原版Vulkan','原版ANGLE','SimdSkia Vulkan','SimdSkia Direct2D']))
cpu_ratios=[r['cpuRatioToFasterOriginal'] for r in ui]
import math
cpu_geomean=math.exp(statistics.mean(map(math.log,cpu_ratios)))
lines=['# SimdSkia 0.1.2-preview.3：最终源码的CPU/GPU与内存复测','',
'完成连续MoveTo与特殊混合SaveLayer修复后，全量重新构建两个对照进程，再采集63个独立进程。源码提交固定为`82e9575971aff578762e879aea8afdbbdf9c811d`；旧结果不覆盖，以下是最终源码的新测量。','',
f"CPU综合耗时比为较快原版的 **{cpu_geomean:.3f}**，最差场景 **{max(cpu_ratios):.3f}**；Vulkan/Direct2D相对两条原版GPU中较快者的综合耗时比 **{gate['vulkan']['geometricMeanToFasterOriginal']:.3f}/{gate['direct2d']['geometricMeanToFasterOriginal']:.3f}**。**整页严格画质仍{sum(q['passed'] for q in quality)}/18，内存仍有差距；这些耗时不能宣传为同画质整体领先。**",'',
'Windows x64，i7-8700K（6核12线程）、GTX1050Ti；SDK10.0.403、.NET10.0.14、Avalonia12.1.5、原版SkiaSharp3.119.4。相同控件、固定字体、尺寸和DPI；3秒预热/5秒测量，3进程中位数，25ms采样。全部进程串行执行，期间没有并行构建/AOT或其他本机基准；只读CI状态轮询仍在进行。','',
'CPU直接绘制BGRA缓冲；GPU每帧等待完成并读回同样缓冲。Vulkan与原版Vulkan是同API；Direct2D与原版ANGLE是同GPU不同API，不称为同后端。计时GPU进程的回退命令均为0，每帧完成批次已检查；路径/字形覆盖仍可能由C#在CPU生成，再由GPU合成。不是窗口交换链FPS、AOT速度或GPU纯着色器耗时。','',
'| 场景 | 实现 | ms/帧 | P95 ms | CPU ms/帧 | 整机CPU% | WS MiB | 私有MiB | WS/物理RAM% | 托管B/帧 |',
'|---|---|---:|---:|---:|---:|---:|---:|---:|---:|']
for row in ui:
 for key in keys:
  r=row[key];physical=r['sampledMedianWorkingSetMiB']*1048576/ram*100
  lines.append(f"| {row['scene']} ×{row['scale']} | {name_map[key]} | {r['meanMs']:.4f} | {r['p95Ms']:.4f} | {r['cpuMsPerFrame']:.4f} | {r['cpuPercentMachine']:.2f} | {r['sampledMedianWorkingSetMiB']:.2f} | {r['sampledMedianPrivateMiB']:.2f} | {physical:.3f} | {r['allocatedBytesPerFrame']:.1f} |")
lines+=['',f'物理RAM为{ram/1073741824:.2f}GiB。CPU%按全部12逻辑线程归一化；吞吐模式下更快实现会处理更多帧，不能只看CPU%。托管分配不含原生堆或驱动分配；WS是物理工作集、私有内存含已提交页面，均不是显存；未测VRAM。峰值和逐次采样见原始JSON，不用缓存预算代替进程内存。','',
'![完成帧耗时](assets/ui-preview3-2026-11-07/ui-time.png)','',
'![进程CPU](assets/ui-preview3-2026-11-07/ui-cpu.png)','',
'![进程内存](assets/ui-preview3-2026-11-07/ui-memory.png)','',
'画质门槛仍为最大通道32、RMSE2。质量失败的计时只作工程诊断；不能从这三页推导所有功能/设备性能达标。先前同GL/Vulkan/D3D12微场景、固定60FPS、CPU API/WebP和所有API/文字/JPEG质量差距，继续见[完整报告](PUBLIC-API-FINAL-2026-11-07.md)。Metal没有Mac GPU，仍未实机验证。','',
'完整数据包含63次运行、每次父进程采样、原始像素gzip对象、二进制/源文件/字体SHA256、门槛与GPU诊断。[冻结证据](evidence/ui-preview3-2026-11-07/)；[结果摘要](evidence/ui-preview3-2026-11-07/summary.json)、[质量](evidence/ui-preview3-2026-11-07/quality.json)、[门槛](evidence/ui-preview3-2026-11-07/gate.json)。','',
'```powershell','dotnet build benchmarks/AvaloniaGpuParity/Original -c Release','dotnet build benchmarks/AvaloniaGpuParity/Managed -c Release','python tools/run-ui-backend-parity.py --output artifacts/reproduce/preview3-ui --forks 3 --seconds 5 --warmup 3','```','',
'运行前将DOTNET_ROOT/PATH指向同一SDK/runtime并取得固定许可字体；需要实际GPU驱动。新输出目录必须不存在；当前画质失败会返回2，不能把正常采集完成改写成画质通过。']
Path('docs/PERFORMANCE-PREVIEW3-2026-11-07.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps(dict(forks=len(runs),cpuGeomean=cpu_geomean,gate=gate,frozenFiles=len(load(root/'sha256.json')),frameReferences=len(load(root/'frame-objects.json'))),indent=2))
