"""Freeze GPU completion investigations, final AOT windows and drained throughput."""
from pathlib import Path
import hashlib,json,shutil,sys
source=Path('artifacts/gpu-cpu-cost-2026-11-07')
evidence=Path('docs/evidence/gpu-completion-2026-11-07')
assets=Path('docs/assets/gpu-completion-2026-11-07')
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
save=lambda p,v:p.write_text(json.dumps(v,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
assert not evidence.exists(),'Do not overwrite frozen evidence.'
final=load(source/'state-window/summary.json');first=load(source/'comparison/summary.json');throughput=load(source/'state-throughput/summary.json')
for folder,count in [('fence-window',9),('fence-throughput',27),('state-window',9),('state-throughput',27),('final-window',9),('threading-final',9),('comparison',18),('presentation-probe',9),('throughput',27),('throughput-final',27)]:
    assert load(source/folder/'manifest.json')['complete'] and len(load(source/folder/'runs.json'))==count
validation=load(source/'validation-state.json');assert all(r['exitCode']==0 for r in validation)
assert all(r['exitCode']==0 for r in load(source/'validation-selected.json'))
assert len(load(source/'validation-selected.json'))==2 and load(source/'selected-source-check.json')['all23FilesExact']
assert all(r['nativeAot'] and r['presentedPngExactAgainstPriorDeviceFlags'] for r in load(source/'aot-smoke-state/checks.json'))
evidence.mkdir(parents=True)
folders=['comparison','presentation-probe','threading-final','final-window','state-window','fence-window','throughput','throughput-final','state-throughput','fence-throughput','aot-smoke','aot-smoke-final','aot-smoke-product','aot-smoke-state','aot-smoke-fence','event-verify','threads-cpu','threads-gpu']
folders += [p.name for p in source.glob('pilot-*') if p.is_dir()]
for folder in folders:
    for p in (source/folder).rglob('*'):
        if p.is_file() and p.suffix in ('.json','.txt','.log','.png'):
            dest=evidence/p.relative_to(source);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
for p in source.iterdir():
    if p.is_file() and p.suffix in ('.json','.txt','.log'):shutil.copyfile(p,evidence/p.name)
for p in (source/'fence-candidate-source').rglob('*'):
    if p.is_file():
        dest=evidence/p.relative_to(source);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
save(evidence/'context.json',dict(published=False,currentNuget='0.1.2',currentNugetDefault='CPU',
    hardware='i7-8700K 6c/12t, NVIDIA GTX1050Ti, Windows11 26300, 31.93GiB',sdk='10.0.403',runtime='10.0.14',avalonia='12.1.5',
    aotProcesses=63,throughputJitProcesses=108,baselineSource='a54485f',nativeMaximumInFlight=2,
    checks=dict(preferenceAndNative=79,avaloniaGpu=12174,vulkan=566,vulkanExact=556,aotWindows=2),
    sourceNote='Selected final implementation is byte-for-byte identical to the state-window source snapshot; the later timeline-fence and single-thread-device candidates were rejected. All trials are retained, not pooled.',
    caveats=['AOT window measurements are timer-paced; not display FPS or peak throughput.',
             'Frame host time in queued mode is recording/submission/backpressure, not GPU latency.',
             'Completed native work is drained before process CPU/elapsed stop.',
             'Throughput is a separate JIT synthetic rectangle workload, CPU raster only versus GPU render+native present.',
             'Small own-engine pixel gates and same-backend PNG equality do not clear original-Skia full-page quality failures.',
             'Vulkan window readback and macOS Metal presentation remain unresolved.',
             'Sampled-thread-time profiles include waiting and were not treated as CPU utilization profiles.']))
save(evidence/'stage-descriptions.json',{
    'comparison':'Initial 18-process AOT matrix. Thread-safe device; actual a54485f before executable; GPU queued host timing differs from synchronized timing.',
    'presentation-probe':'9-process AOT flip vs legacy blt investigation. Flip retained in final product.',
    'threading-final':'Despite the historical folder name, a 9-process experimental SINGLETHREADED candidate and thread-safe control. Candidate was rejected.',
    'throughput':'27 JIT processes from the rejected SINGLETHREADED candidate. Exploratory, not used in final throughput tables.',
    'final-window':'Intermediate 9-process AOT check after removing the single-thread device candidate, before native state caching.',
    'throughput-final':'Intermediate 27-process JIT check after removing the single-thread device candidate, before native state caching.',
    'state-window':'Selected final 9-process AOT implementation: CPU, cached-state GPU queue, uncached-state GPU queue. Source exactness checked after rejecting timeline fences.',
    'state-throughput':'Selected final 27-process JIT completed-throughput implementation with state cache and Flush1 completion events.',
    'fence-window':'Rejected 9-process AOT timeline-fence experiment. Reduced explicit Flush calls did not give stable CPU reduction.',
    'fence-throughput':'Rejected timeline-fence candidate, 27 JIT processes. Not used for final performance tables.',
    'pilot-*':'Exploratory changing-harness tests. Retained for audit; not pooled with final measurements.'})
save(evidence/'frame-objects.json',{})
measuredSources=load(source/'state-window/manifest.json')['sourceFiles']
snapshots=[]
for folder in ['src/SimdSkia.Gpu.Direct2D','src/SimdSkia.Avalonia.Gpu','tests/GpuPreferred','examples/CrossPlatform']:
    snapshots += [p for p in Path(folder).rglob('*') if p.is_file() and p.suffix in ('.cs','.csproj') and not {'bin','obj'}.intersection(p.parts)]
snapshots.append(Path('src/SimdSkia.Avalonia/SimdSkiaDiagnostics.cs'))
for p in snapshots:
    assert sha(p)==measuredSources[p.as_posix()],f'Measured source changed: {p}'
    dest=evidence/'source-snapshot'/p;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
lookup={r['variant']:r for r in final};cpu=lookup['visible-cpu'];gpu=lookup['visible-queued'];uncached=lookup['visible-uncached-state']
names={'visible-cpu':'C# CPU/SIMD','visible-queued':'Direct2D 默认队列 / 状态缓存','visible-flush1-fences':'Direct2D 队列 / 强制 Flush1 完成事件','visible-uncached-state':'Direct2D 两帧队列 / 关闭状态缓存','visible-queued-thread-safe':'线程安全设备（单线程标志实验的对照组）','visible-synchronous':'Direct2D 每帧同步','visible-vulkan':'Vulkan 同步读回','hidden-before-direct2d':'改动前 Direct2D（屏幕外）','hidden-after-direct2d':'中间版队列 Direct2D（屏幕外）'}
def table(rows,historical=False):
    result=['| 配置 | 绘制会话 ms | p95 ms | CPU ms/帧 | 整机 CPU %（范围） | WS MiB | Private MiB | 托管 KiB/帧 |', '|---|---:|---:|---:|---:|---:|---:|---:|']
    for r in rows:
        label='中间版 Direct2D 队列（未加状态缓存）' if historical and r['variant']=='visible-queued' else names[r['variant']]
        result.append(f"| {label} | {r['drawingMeanMs']:.4f} | {r['drawingP95Ms']:.4f} | {r['cpuMsPerFrame']:.3f} | {r['cpuPercentMachine']:.3f} ({r['cpuPercentMachineMin']:.3f}–{r['cpuPercentMachineMax']:.3f}) | {r['peakWorkingSetBytes']/1048576:.2f} | {r['peakPrivateBytes']/1048576:.2f} | {r['allocatedBytesPerFrame']/1024:.2f} |")
    return '\n'.join(result)
micro=['| 矩形/帧 | CPU 完成 ms/帧 | GPU 队列完成 ms/帧 | GPU 同步完成 ms/帧 | GPU 队列/CPU 耗时比 | CPU ms/帧：CPU → GPU 队列 |','|---|---:|---:|---:|---:|---:|']
for count in (1,64,512):
    group={r['mode']:r for r in throughput if r['rectangles']==count};c,g,s=[group[n] for n in ('cpu','queued','synchronous')]
    micro.append(f"| {count} | {c['millisecondsPerCompletedFrame']:.4f} | {g['millisecondsPerCompletedFrame']:.4f} | {s['millisecondsPerCompletedFrame']:.4f} | {g['millisecondsPerCompletedFrame']/c['millisecondsPerCompletedFrame']:.3f} | {c['cpuMillisecondsPerFrame']:.4f} → {g['cpuMillisecondsPerFrame']:.4f} |")
ratio=gpu['cpuMsPerFrame']/cpu['cpuMsPerFrame'];memory=(gpu['peakWorkingSetBytes']-cpu['peakWorkingSetBytes'])/1048576
save(evidence/'ratios.json',dict(finalGpuToCpuHostSession=gpu['drawingMeanMs']/cpu['drawingMeanMs'],finalGpuToCpuProcessCpuPerFrame=ratio,addedWorkingSetMiB=memory))
save(evidence/'sha256.json',{p.relative_to(evidence).as_posix():sha(p) for p in evidence.rglob('*') if p.is_file()})
text='\n\n'.join([
'# GPU 为什么还吃 CPU：完成等待、两帧队列与性能边界实测',
'**未发布源码，NuGet0.1.2不包含本批优化。** 本报告比较本引擎 CPU/Direct2D/Vulkan 及自身改动前后；不是新一轮原版 Google Skia 同画质验收。',
f"最终三轮 AOT 中，GPU/CPU 绘制会话耗时比为 **{gpu['drawingMeanMs']/cpu['drawingMeanMs']:.3f}**；进程 CPU 毫秒/帧比为 **{ratio:.3f}**，工作集仍多 **{memory:.2f} MiB**。绘制会话在 GPU 队列模式下包含录制、提交和背压等待，**不等于 GPU 执行延迟**；不能据此宣称整应用/GPU 执行全面更快。连续绘制的真实完成吞吐另表给出。",
'## 找到并修复的成本',
'1. 原生 Direct2D 每帧在 `GetData` 查询中 `Thread.Yield()`，试测可出现数十万次查询。现代 Windows 改用 `ID3D11DeviceContext3.Flush1` 完成事件；最终所有原生窗口测量轮询为0。缺少该接口时使用低频查询，显式旧模式只供复现。\n'
'2. 每帧立即等待 GPU 结束，使 CPU 与 GPU 串行。原生窗口现在最多两帧在途，完成事件确认后才释放相应瞬态资源；读取像素、同步 Present、CPU 回退及正常释放会等待实际完成。没有无限堆积提交来制造帧率。\n'
'3. 双缓冲 flip-discard 交换链配合常驻 GPU 渲染纹理保留上一帧，避免丢掉局部更新；正常帧不 CPU 读回，staging 纹理延迟分配。旧 blt 与无保留纹理实验均保留原始结果，未仅凭预期选择实现。\n'
'4. 原生窗口调用/释放继续受创建线程检查。单线程 D3D11 标志候选未获得稳定收益，已经撤回，最终设备保留线程安全创建策略；实验及其对照全部保留。测试读取原生创建标志核验最终策略，并检查跨线程调用报错。\n'
'5. 修复 `Present` 清空待提交标记后，下一帧第一条 CPU 回退读到旧 CPU 副本的问题；现在独立追踪 CPU 副本是否陈旧，格式/stride/预乘 Alpha 保持原有语义。',
'6. 缓存相同矩阵、原生混合和抗锯齿状态，相同设备裁剪在连续命令间复用；结束绘制前关闭裁剪，覆盖层切换时保守失效相关状态。关闭缓存的路径保留用于同设备精确像素和性能对照。',
'7. 另测了 `ID3D11DeviceContext4.Signal` / `ID3D11Fence.SetEventOnCompletion`，每个测量区间的本库主动 Flush 从约270次降到1次，但GPU进程CPU毫秒/帧中位数2.298，对照Flush1为2.095，未获得稳定CPU收益，已撤回。最终代码与上面的状态缓存版本逐文件SHA256一致；失败候选的源码、二进制哈希和全部结果独立保留。显式Flush数不等于驱动内部物理提交数。',
'参考：[Microsoft Flush1 完成通知](https://learn.microsoft.com/en-us/windows/win32/api/d3d11_3/nf-d3d11_3-id3d11devicecontext3-flush1)、[DXGI flip model](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model)、[D3D11 创建标志和单线程限制](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_create_device_flag)。',
'## 最终 AOT 真实可见控件窗口',
'Windows11、i7-8700K/12逻辑线程、GTX1050Ti、.NET10.0.14/SDK10.0.403、Avalonia12.1.5。1000×720、固定中文字体、相同 Fluent 页面，定时更新按钮并请求重绘；8秒预热、8秒测量、3独立进程中位数，串行且无并行构建/基准。计时不开截图/额外 GPU 查询。结束前增加一个同步帧排空队列，尾部等待包含在墙钟、CPU和逐帧样本内。可见原生窗口另保留 DXGI 遮挡计数。',
table(final),
f"同一两帧队列策略下，矩阵/混合/AA/轴对齐裁剪原生调用从 **{uncached['nativeStateChangesPerFrame']:.1f} → {gpu['nativeStateChangesPerFrame']:.1f} 次/帧**；缓存开/关的绘制会话耗时比 **{gpu['drawingMeanMs']/uncached['drawingMeanMs']:.3f}**、进程CPU毫秒/帧比 **{gpu['cpuMsPerFrame']/uncached['cpuMsPerFrame']:.3f}**。此处两条GPU路径的提交/等待语义相同，队列均最终排空。",
'![最终窗口的 CPU 与内存](assets/gpu-completion-2026-11-07/window-cost.png)',
'这套窗口由定时器和渲染循环驱动，观察到的帧数/秒不等于显示器 FPS 或峰值吞吐；各配置帧数不完全相同，因此同时列进程 CPU 毫秒/帧。CPU%为整个进程 CPU 时间/墙钟/12，包含 UI、驱动工作线程和诊断采样。内存是定时采样峰值，不能替代精确系统峰值或 VRAM；WS 与 Private 不可相加。三轮范围显示低负载 CPU 测量有波动，不能承诺所有程序和硬件均降低 CPU。',
'`gpuCompletionMilliseconds`/统计中的 `CompletionMilliseconds` 是 Flush/Present 宿主调用累计时间，不是 GPU 硬件时间戳。新的 NativeSubmittedFrames/NativeCompletedFrames/NativePendingFrames 区分提交和实际退休工作；测量边界可能带入一帧预热工作，因此不能把该完成计数直接当作测量帧率。',
'## 初始完整矩阵与前后对照（保留中间结果）',
'以下为初始18进程矩阵，仍为AOT、8秒预热/8秒测量/3fork。可见与屏幕外窗口分组，不能交叉计算收益。改动前来自实际 a54485f AOT，采用每帧完成等待；中间版队列的绘制会话时长含义发生变化，不能把两个绘制会话数值直接宣传成 GPU 执行提速。CPU/内存列与最终排空后的完成数量单独判断。后续保留呈现方式、单线程设备和撤回后的中间检查点各9进程；最终表是状态缓存加入后新测的9进程，没有筛选其中更有利的旧轮次。',
table(first,historical=True),
'Vulkan 窗口依然执行 GPU→主机读回→OS framebuffer，同步完成时间明显高于该 CPU 页面，本批没有修成原生 Vulkan 窗口交换链。它不能借用 Direct2D 的优化成绩。Linux 窗口仍受这一路径限制，macOS 窗口仍用 CPU；Metal 没有 Mac GPU 实机验收。低负载期间还观测到 GTX1050Ti 的 P8/303MHz 状态，记录为影响因素，没有修改用户显卡/电源设置，也未证明它解释了全部差距。',
'## 连续绘制：GPU 工作确实完成之后的吞吐',
'另用 JIT 的1000×720纯绘制工作负载，1/64/512个整数边界、抗锯齿、半透明 SourceOver 矩形，每帧清屏；每进程预热至少3秒、计时至少2秒，3fork，共27进程。GPU计时末尾排空绘制/纹理复制队列，CPU为光栅本身，GPU额外含原生呈现提交；不测DWM/扫描输出完成。双方读回/画质核验均在计时外。这是合成吞吐测试，不能替代上面的真实Avalonia页面，也不与AOT结果混算。',
'\n'.join(micro),
'![完成吞吐](assets/gpu-completion-2026-11-07/completed-throughput.png)',
'图使用对数纵轴，准确数值见表。完成指本引擎GPU绘制和纹理复制完成，原生呈现调用计时；不代表显示器已经扫描输出。',
'所有结果逐像素对照本引擎 CPU 的固定门槛为最大通道误差≤2；队列最终在途为0，实际完成计数与提交一致。复杂文字、曲线、滤镜、混合回退及整页并未因此变成原版像素一致。轻负载下 GPU 有固定驱动/提交/同步成本，GPU不保证每次绘制都比优化后的CPU更快；负载规模和呈现架构决定收益。',
'## 验证、剩余差距与复现',
'- 最终79项策略/原生HWND/部分帧保留/跨帧CPU回退/两种格式和stride/两帧上限/24个独立GPU查询/创建标志与线程约束/缓存开关精确像素通过；后来80项硬件fence候选通过正确性，但未因功能通过就保留性能无收益的候选。\n'
'- 既有 Avalonia GPU 12174项和 Vulkan 566项（556精确字节比较，含真实中文轮廓）通过，完整Release构建与Native AOT发布通过。\n'
'- 最终Auto/CPU两个实际AOT窗口运行、中文、调整尺寸、呈现成功，无CLR/JIT或Skia/HarfBuzz；同后端呈现PNG与前一设备配置逐字节相同。\n'
'- 原版严格整页画质、JPEG历史差异、Vulkan原生窗口呈现、Metal窗口与跨GPU硬件性能仍有差距；本批不宣称全面兼容、整体速度或内存全面胜出。\n'
'- 全部中间试验保留。sampled-thread-time包含睡眠/等待，未被当成CPU时间热点；Windows与单块旧GPU结果不代表Linux/macOS性能。NuGet0.1.2仍默认CPU，本批未发布新包。',
'```powershell\ndotnet build SimdSkia.sln -c Release\ndotnet run --project tests/GpuPreferred -c Release -- --hardware\n'
'dotnet publish examples/CrossPlatform -c Release -r win-x64 -p:PublishAot=true -p:IlcInstructionSet=x86-64 -o artifacts/current-aot\n'
'python tools/run-gpu-completion-cost.py --after artifacts/current-aot/CrossPlatform.exe --output artifacts/current-cost --state-probe --forks 3 --warmup 8 --seconds 8\n'
'python tools/run-native-gpu-throughput.py --output artifacts/current-throughput --forks 3\n'
'python tools/verify-final-evidence.py --directory docs/evidence/gpu-completion-2026-11-07\n```',
'[冻结证据](evidence/gpu-completion-2026-11-07/)、[最终AOT逐帧与各fork](evidence/gpu-completion-2026-11-07/state-window/runs.json)、[初始矩阵](evidence/gpu-completion-2026-11-07/comparison/runs.json)、[真实完成吞吐](evidence/gpu-completion-2026-11-07/state-throughput/runs.json)、[构建/测试状态](evidence/gpu-completion-2026-11-07/validation-state.json)、[撤回后检查](evidence/gpu-completion-2026-11-07/validation-selected.json)、[SHA256清单](evidence/gpu-completion-2026-11-07/sha256.json)。'])+'\n'
Path('docs/PERFORMANCE-GPU-COMPLETION-2026-11-07.md').write_text(text,encoding='utf-8')
sys.path.insert(0,str(Path('artifacts/report-python').resolve()))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib import font_manager
font=Path('C:/Windows/Fonts/msyh.ttc')
if font.exists():font_manager.fontManager.addfont(str(font));plt.rcParams['font.family']=font_manager.FontProperties(fname=str(font)).get_name()
plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False,'axes.unicode_minus':False,'svg.hashsalt':'gpu-completion-2026-11-07'})
assets.mkdir(parents=True,exist_ok=True)
fig,axes=plt.subplots(1,3,figsize=(16,4.6));labels=['CPU','GPU 状态缓存','GPU 关闭缓存'];colors=['#657b91','#168ca5','#a0abb5']
for ax,key,scale,title in zip(axes,['drawingMeanMs','cpuMsPerFrame','peakWorkingSetBytes'],[1,1,1/1048576],['绘制会话占用 ms（队列≠GPU延迟）','整个进程 CPU ms/帧','采样工作集 MiB（不含VRAM）']):
    vals=[r[key]*scale for r in final];ax.barh(labels,vals,color=colors);ax.invert_yaxis();ax.set_title(title);ax.set_xlim(0,max(vals)*1.22)
    for i,v in enumerate(vals):ax.text(v+max(vals)*.015,i,f'{v:.3f}' if scale==1 else f'{v:.2f}',va='center')
fig.suptitle('真实可见 Avalonia 窗口：Native AOT，3fork；GPU队列在测量结束前排空',fontsize=13);fig.tight_layout(rect=(0,0,1,.9))
for ext in ('png','svg'):fig.savefig(assets/('window-cost.'+ext),dpi=170,metadata={'Date':None} if ext=='svg' else None)
plt.close(fig)
fig,axes=plt.subplots(1,2,figsize=(12,4.5));x=list(range(3));width=.25
for ax,key,title in zip(axes,['millisecondsPerCompletedFrame','cpuMillisecondsPerFrame'],['排空队列后的真实完成 ms/帧','进程 CPU ms/帧']):
    for j,(mode,label,color) in enumerate([('cpu','CPU 光栅','#657b91'),('queued','GPU 两帧队列','#168ca5'),('synchronous','GPU 每帧同步','#a0abb5')]):
        vals=[next(r[key] for r in throughput if r['mode']==mode and r['rectangles']==n) for n in (1,64,512)]
        ax.bar([i+(j-1)*width for i in x],vals,width,label=label,color=color)
    ax.set_xticks(x,['1个矩形','64个矩形','512个矩形']);ax.set_title(title);ax.set_yscale('log');ax.legend(fontsize=8)
fig.suptitle('独立 JIT 合成吞吐：CPU无OS呈现；GPU含呈现提交并等绘制完成，像素门槛≤2',fontsize=12)
fig.text(.5,.015,'对数纵轴；数值见报告。完成仅指绘制和纹理复制，不含 DWM/扫描输出。',ha='center',fontsize=10);fig.tight_layout(rect=(0,.075,1,.9))
for ext in ('png','svg'):fig.savefig(assets/('completed-throughput.'+ext),dpi=170,metadata={'Date':None} if ext=='svg' else None)
plt.close(fig)
save(assets/'manifest.json',dict(generatorSha256=sha(Path(__file__)),evidenceManifestSha256=sha(evidence/'sha256.json'),files={p.name:sha(p) for p in assets.iterdir() if p.suffix in ('.png','.svg')}))
print(f'PASS {len(load(evidence/"sha256.json"))} evidence hashes, AOT and JIT separated, GPU completion checked; NuGet unchanged.')
