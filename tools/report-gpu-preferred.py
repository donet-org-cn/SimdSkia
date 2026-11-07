"""Freeze the GPU-preferred implementation checks and final real-window comparison."""
from pathlib import Path
import hashlib,json,shutil,sys

source=Path('artifacts/gpu-preferred-2026-11-07')
root=Path('docs/evidence/gpu-preferred-2026-11-07')
assets=Path('docs/assets/gpu-preferred-2026-11-07')
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
save=lambda p,v:p.write_text(json.dumps(v,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
assert not root.exists(),'Do not overwrite retained evidence.'
summary=load(source/'timing-final/summary.json');manifest=load(source/'timing-final/manifest.json')
assert manifest['complete'] and manifest['forks']==3
assert len(load(source/'timing-final/runs.json'))==18
assert all(r['exitCode']==0 for r in load(source/'validation.json'))
assert all(r['pngByteExact'] for r in load(source/'window-final-smoke-comparison.json'))
assert len(load(source/'aot-final-smoke.json'))==2
assert (source/'minimum-host-smoke/result.txt').read_text(encoding='utf-8').startswith('PASS')
root.mkdir(parents=True)
folders=['timing-final','timing-1','aot-final-smoke-auto','aot-final-smoke-cpu','minimum-host-smoke']
folders += [p.name for p in source.glob('smoke-current-*') if p.is_dir()]
files=['validation.json','preference.log','avalonia-gpu.log','gpu-optimization.log','build-final.log','aot-final-publish.log',
       'minimum-host-build.log','package-graph.json','window-final-smoke.json','window-final-smoke-comparison.json','aot-final-smoke.json']
for folder in folders:
    for p in (source/folder).rglob('*'):
        if p.is_file() and p.suffix in ('.json','.txt','.log','.png'):
            dest=root/p.relative_to(source);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
for name in files:shutil.copyfile(source/name,root/name)
save(root/'context.json',dict(published=False,currentNugetVersion='0.1.2',publicNugetDefault='CPU',
    measuredFinalProcesses=18,initialProcesses=18,
    initialScope='Interim measurements before device-vs-surface classification; retained separately, not used in final tables.',
    finalScope=manifest['scope'],baselineSource='f7f0185',
    checks=dict(preference=40,existingGpuIntegration=12174,optimization=566,optimizationExactBytes=556,
                aotWindows=2,currentWindows=7,pairedPresentationPngsExact=3),
    actualLayeredWindowVerified=False,metalHardwareVerified=False,originalSkiaFullPageQualityPassed=False,
    newCrossPlatformHardwareTiming=False,upfrontVulkanPipelines=7))
save(root/'frame-objects.json',{})
save(root/'sha256.json',{p.relative_to(root).as_posix():sha(p) for p in root.rglob('*') if p.is_file()})

names={'before-cpu':'改动前 CPU','after-cpu':'改动后显式 CPU','before-direct2d':'改动前 Direct2D',
       'after-auto':'新默认 Auto → Direct2D','before-vulkan':'改动前 Vulkan','after-vulkan':'改动后 Vulkan'}
table=['| 配置 | 完成绘制均值 ms | p95 ms | CPU ms/帧 | 整机 CPU % | 峰值 WS MiB | 峰值 private MiB | 托管 KiB/帧 |',
       '|---|---:|---:|---:|---:|---:|---:|---:|']
for row in summary:
    table.append(f"| {names[row['variant']]} | {row['drawingMeanMs']:.4f} | {row['drawingP95Ms']:.4f} | {row['cpuMsPerFrame']:.3f} | {row['cpuPercentMachine']:.2f} | {row['peakWorkingSetBytes']/1048576:.2f} | {row['peakPrivateBytes']/1048576:.2f} | {row['allocatedBytesPerFrame']/1024:.2f} |")
lookup={r['variant']:r for r in summary}
auto,cpu,oldD2d,newVk,oldVk=[lookup[n] for n in ['after-auto','after-cpu','before-direct2d','after-vulkan','before-vulkan']]
cpu_ratio=auto['drawingMeanMs']/cpu['drawingMeanMs'];d2d_ratio=auto['drawingMeanMs']/oldD2d['drawingMeanMs'];vk_ratio=newVk['drawingMeanMs']/oldVk['drawingMeanMs']
cpuCost=auto['cpuPercentMachine']/cpu['cpuPercentMachine'];memoryAdded=(auto['peakWorkingSetBytes']-cpu['peakWorkingSetBytes'])/1048576
save(root/'ratios.json',dict(autoToCpuDraw=cpu_ratio,autoToBeforeDirect2dDraw=d2d_ratio,vulkanAfterToBeforeDraw=vk_ratio,
                           autoToCpuWholeMachineCpu=cpuCost,autoAddedPeakWsMiB=memoryAdded))
report='\n\n'.join([
    '# GPU 优先批次：真实控件窗口的完成时间、CPU 与内存',
    '**未发布源码，NuGet0.1.2仍默认CPU。** 本报告比较SimdSkia改动前后，不是新一轮原版Skia对照，不能替代此前严格画质失败的报告。',
    f"新默认Auto实际选择Direct2D原生交换链。相对本轮显式CPU，完成绘制耗时比 **{cpu_ratio:.3f}**，但整机CPU占用比 **{cpuCost:.3f}**、峰值工作集增加 **{memoryAdded:.2f}MiB**。相对改动前已有Direct2D，耗时比 **{d2d_ratio:.3f}**；Vulkan改动后/前耗时比 **{vk_ratio:.3f}**。不能把默认GPU包装成速度、CPU、内存全面改善。",
    '## 方法',
    'Windows11 x64，i7-8700K（6核12线程）、GTX1050Ti，SDK10.0.403/runtime10.0.14，Avalonia12.1.5，JIT。'
    '同一1000×720真实Fluent控件窗口、固定中文字体；定时更新按钮文本并请求重绘。窗口移到屏幕外，仍调用实际OS/交换链与GPU完成事件，'
    '不等同显示器呈现FPS。每项2秒预热、5秒测量、3独立进程中位数；6变体共18进程串行。计时期间没有并行构建/AOT/其他本机基准。',
    '这不是强制整页峰值吞吐，也不是固定60FPS同工作量测试：完成帧数可能不同。'
    'CPU%为进程CPU时间/墙钟/12，另给CPU毫秒/实际完成帧；包含UI、定时器、驱动等进程工作。'
    '峰值内存来自UI定时采样，不是系统精确历史峰值或VRAM；WS/private不可相加；托管分配不含原生堆/显存。',
    '\n'.join(table),
    '![完成时间、CPU与内存](assets/gpu-preferred-2026-11-07/window-comparison.png)',
    '原生Direct2D正常帧不强制回读；Vulkan等待完成后读回OS framebuffer。双方同后端前后比较才用于判断对应改动；'
    '跨后端选择用于评价默认策略。新Auto三个进程初始化失败0、命令级CPU回退0，但文字覆盖率/部分图层仍有CPU准备和处理。'
    '每个指标分别取中位数；全部fork、极值、逐帧时间和实际完成帧数保留在证据中。',
    '本轮Auto比已有Direct2D没有显著提速；Vulkan完成时间略低，CPU占用反而更高，不能只报道较好的时间。'
    '少量进程、单一页面与单块旧GPU的结果有局限，CPU差距的具体调用栈原因尚未定位。没有为获得更好成绩继续重复筛选；'
    '此前中间源码的18进程也保留在timing-1，最终表只用timing-final。',
    '## 实现与行为验证',
    '- 最终Release全解决方案、Native AOT发布零警告/错误。\n'
    '- 新增40项策略/故障注入/缓存生命周期/提前管线检查通过；既有12174项Vulkan/Direct2D集成、566项GPU检查（556精确字节比较）通过。\n'
    '- Auto/显式CPU/Vulkan等7个真实窗口通过；改动前后CPU、Direct2D、Vulkan三组呈现PNG逐字节相同。\n'
    '- 最终Auto与显式CPU两个真正Native AOT窗口通过；GUI PE无CLR头，未加载CLR/JIT或Google Skia/HarfBuzz。\n'
    '- Avalonia最低12.1.0和当前12.1.5真实默认GPU窗口均通过；新平台GPU实机性能没有测试，Metal无Mac GPU验证。\n'
    '- 透明提示没有触发实际分层HWND，只记录不崩溃；不当成已通过逐像素透明。\n'
    '- 同后端相同与新单项检查不改变原版严格整页画质0/18、此前AOT0/10或JPEG21/24。',
    '## 从Impeller吸收的范围',
    '本轮实现GPU优先与可诊断回退、Vulkan离线SPIR-V共享、七条管线在设备初始化准备、按估算载荷/条目有界的字形LRU和在途资源完成后回收。'
    '参考[官方架构目标](https://docs.flutter.dev/perf/impeller)，没有引入其原生引擎。'
    '初始化前移没有消除驱动编译，也不是启动白屏的新验收；原生Vulkan窗口交换链、通用帧图/退休队列及Metal窗口仍未完成。'
    '详见[实现与API合同](GPU-PREFERRED-2026-11-07.md)。',
    '## 复现与证据',
    '```powershell\npython tools/run-gpu-preferred-comparison.py --before <f7f0185独立构建的CrossPlatform.exe> --after examples/CrossPlatform/bin/Release/net10.0/CrossPlatform.exe --output artifacts/new-gpu-preferred-run --forks 3 --warmup 2 --seconds 5\n'
    'python tools/verify-final-evidence.py --directory docs/evidence/gpu-preferred-2026-11-07\n```',
    '[冻结证据](evidence/gpu-preferred-2026-11-07/)、[最终原始结果](evidence/gpu-preferred-2026-11-07/timing-final/runs.json)、'
    '[对照程序集与源码SHA](evidence/gpu-preferred-2026-11-07/timing-final/manifest.json)、[校验清单](evidence/gpu-preferred-2026-11-07/sha256.json)。'
    '本地候选nupkg仅检查Desktop→GPU传递依赖，未上传；不代表发布了0.1.3-preview.1。'])+'\n'
Path('docs/PERFORMANCE-GPU-PREFERRED-2026-11-07.md').write_text(report,encoding='utf-8')

sys.path.insert(0,str(Path('artifacts/report-python').resolve()))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib import font_manager
font=Path('C:/Windows/Fonts/msyh.ttc')
if font.exists():font_manager.fontManager.addfont(str(font));plt.rcParams['font.family']=font_manager.FontProperties(fname=str(font)).get_name()
plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False,'axes.unicode_minus':False,'svg.hashsalt':'gpu-preferred-final'})
assets.mkdir(parents=True,exist_ok=True)
fig,axes=plt.subplots(1,3,figsize=(16,5.4))
colors=['#8293a4','#60768b','#8293a4','#178caf','#8293a4','#178caf']
for ax,key,scale,title,xlabel in zip(axes,['drawingMeanMs','cpuPercentMachine','peakWorkingSetBytes'],[1,1,1/1048576],
    ['完成绘制均值','整机 CPU 占用','采样峰值工作集'],['ms（越低越好）','CPU %（12逻辑线程）','进程 MiB（不含VRAM）']):
    values=[r[key]*scale for r in summary];labels=[names[r['variant']] for r in summary]
    ax.barh(labels,values,color=colors);ax.invert_yaxis();ax.set_xlim(0,max(values)*1.27);ax.set_title(title);ax.set_xlabel(xlabel)
    for i,v in enumerate(values):ax.text(v+max(values)*.012,i,f'{v:.3f}' if key=='drawingMeanMs' else f'{v:.2f}',va='center',fontsize=10)
fig.suptitle('SimdSkia GPU优先：完成时间有收益，CPU与内存仍有代价',fontsize=15)
fig.text(.5,.015,'最终源码 / 18进程 / 真实控件窗口 / JIT；不是原版Skia新对照，也不是同画质整体领先结论',ha='center',fontsize=10)
fig.tight_layout(rect=(0,.06,1,.94))
for extension in ('png','svg'):fig.savefig(assets/('window-comparison.'+extension),dpi=170,metadata={'Date':None} if extension=='svg' else None)
plt.close(fig)
save(assets/'manifest.json',dict(generator=__file__,generatorSha256=sha(Path(__file__)),evidenceManifestSha256=sha(root/'sha256.json'),
    files={p.name:sha(p) for p in assets.iterdir() if p.suffix in ('.png','.svg')}))
print(f'PASS froze {len(load(root/"sha256.json"))} files; generated final 18-process report and graph. NuGet unchanged.')
