"""Archive measured evidence alongside reviewable reports; never changes quality gates."""
import json,pathlib,shutil

workspace=pathlib.Path(__file__).resolve().parents[1];docs=workspace/'docs';root=workspace/'artifacts/completion'
cpu_root=root/'cpu-delivery';gpu_root=root/'gpu-delivery';filter_root=root/'filter-final-unrolled'
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def copy(source,target):
 target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
for source,target in [
 (cpu_root/'results.json','cpu-performance-results.json'),(cpu_root/'manifest.json','cpu-performance-manifest.json'),
 (gpu_root/'summary.json','gpu-performance-results.json'),(gpu_root/'results.csv','gpu-performance-results.csv'),(gpu_root/'run.json','gpu-performance-manifest.json'),
 (filter_root/'results.json','filter-performance-results.json'),(filter_root/'manifest.json','filter-performance-manifest.json'),
 (root/'features-final-quality.json','feature-quality-results.json'),(root/'gpu-sampling-tests.json','gpu-sampling-test-results.json'),
 (workspace/'artifacts/diff/results.json','compatibility-pixel-results.json')]:copy(source,docs/target)
for name in ['latency-256x256.svg','latency-1024x768.svg']:copy(gpu_root/name,docs/'images'/('gpu-final-'+name))
for name,target in [('managed-controls-dpi1.png','completion-controls-managed.png'),('reference-controls-dpi1.png','completion-controls-reference.png'),('diff-controls-dpi1.png','completion-controls-diff.png')]:copy(cpu_root/name,docs/'images'/target)
copy(root/'feature-diff/image-Tile-Fill-1.5.png',docs/'images/completion-imagebrush-diff.png')
copy(root/'features-final/visual-Tile-1.5.png',docs/'images/completion-visualbrush.png')
copy(root/'feature-diff/visual-Tile-1.5.png',docs/'images/completion-visualbrush-diff.png')
gpu_path=docs/'PERFORMANCE-GPU-FINAL-2026-10-21.md';gpu_text=gpu_path.read_text(encoding='utf-8-sig')
for old,new in [('summary.json','gpu-performance-results.json'),('results.csv','gpu-performance-results.csv'),('run.json','gpu-performance-manifest.json')]:gpu_text=gpu_text.replace('../artifacts/completion/gpu-delivery/'+old,new)
for name in ['latency-256x256.svg','latency-1024x768.svg']:gpu_text=gpu_text.replace('../artifacts/completion/gpu-delivery/'+name,'images/gpu-final-'+name)
gpu_text=gpu_text.replace('CPU 结果见 PERFORMANCE.md','CPU/滤镜/内存结果见 [本轮汇总](PERFORMANCE-FINAL-2026-10-21.md)')
gpu_path.write_text(gpu_text,encoding='utf-8')

cpu=read(cpu_root/'results.json');gpu=read(gpu_root/'summary.json');filters=read(filter_root/'results.json')
def c(scene,scale,variant):return next(r for r in cpu['summary'] if (r['scenario'],r['scale'],r['variant'])==(scene,scale,variant))
labels={'before':'本轮前 c4e1481','after':'SimdSkia 最终','default':'原版默认 opacity','reference':'原版完整 opacity 组'}
lines=['# 最终性能与内存报告：2026-10-21','','**现在不建议替换生产 Avalonia。** 功能范围已经扩大，Gaussian SIMD 有明显收益；完整控件相对原版默认仍较慢、内存较高，且 CPU/GPU 画质验收未通过。耗时更短的失败场景不能判为同画质胜出。','',
'硬件：i7-8700K（12 逻辑 CPU）、GTX 1050 Ti、Windows 10.0.26300、.NET 8.0.32 x64。固定 Avalonia 12.1.4 / SkiaSharp 3.119.4。全部进程串行运行，CPU、Gaussian 与 GPU 没有并发争用；没有修改 OS 电源计划或锁定 Turbo。完整测试见 [测试报告](TEST-REPORT-2026-10-21.md)，算法与未完成项见 [实现说明](IMPLEMENTATION-DETAILS-2026-10-21.md)。','',
'## 实际 Avalonia 控件：CPU 软件光栅','',
'36 个独立进程：优化前/最终/原版默认/原版完整组 × 3 配置 × 3 fork，旋转执行顺序。每进程预热 2.5 秒、测量 600 帧；以下是三进程中位数。字体、控件场景、尺寸固定，格式 BGRA8888/PMA。源码与当时 DLL SHA 在 [配置清单](cpu-performance-manifest.json)，含每个进程数据的 [完整 JSON](cpu-performance-results.json)。before 是本轮开始时 c4e1481 的完整构建输出，不是混用同一组后来被覆盖的 DLL。','',
'计时范围是实际控件经 `RenderTargetBitmap + ImmediateRenderer` 的软件绘制；字体注册、布局准备、PNG 保存与窗口呈现不在循环计时内。首绘也仅指第一次 Render，不含进程/字体/布局启动。这不是屏幕刷新率或整个应用延迟。','',
'| 场景 | 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | 工作集 MiB | Private MiB | 保留托管堆 MiB |',
'|---|---|---:|---:|---:|---:|---:|---:|']
for scene,scale in [('controls',1),('controls',1.5),('scroll',1)]:
 for variant in ['before','after','default','reference']:
  r=c(scene,scale,variant);name=f'{"控件" if scene=="controls" else "列表滚动"} {scale*100:g}%'
  lines.append(f"| {name} | {labels[variant]} | {r['millisecondsPerFrame']:.3f} | {r['cpuMillisecondsPerFrame']:.3f} | {r['allocatedBytesPerFrame']/1024:.2f} | {r['workingSetMiB']:.1f} | {r['privateMiB']:.1f} | {r['managedRetainedAfterGcMiB']:.1f} |")
lines+=['','与原版默认比较：','']
for scene,scale in [('controls',1),('controls',1.5),('scroll',1)]:
 r=c(scene,scale,'after');n=c(scene,scale,'default');b=c(scene,scale,'before')
 lines.append(f"- {scene} {scale*100:g}%：耗时 {r['millisecondsPerFrame']/n['millisecondsPerFrame']:.2f} 倍；工作集多 {r['workingSetMiB']-n['workingSetMiB']:.1f} MiB，Private 多 {r['privateMiB']-n['privateMiB']:.1f} MiB。本轮帧时间变化 {(r['millisecondsPerFrame']/b['millisecondsPerFrame']-1)*100:+.1f}%。")
lines+=['','本轮对既有控件循环的收益很小，列表变化属于噪声量级；没有因为新增很多功能就宣称完整 UI 大幅提速。高 DPI 仍有较明显差距。原版默认是逐 draw opacity，原版完整组使用离屏透明组；场景中的半透明区域行为不同，必须同时列出，不能只挑较慢的完整组。所有完整页面画质都未达标，所以即使比完整组更快也不宣布同画质胜出。','',
'CPU 时间是整个进程累计 TotalProcessorTime，含后台线程，不能当作硬件 GPU 利用率；Windows 的 CPU 计时粒度会影响约 0.25 秒的列表样本，小差异不作严格排名。B/帧仅为托管分配，不含 native malloc；工作集/Private 包括运行时和 native/driver，保留堆不是总内存。不能将工作集、Private 与托管堆相加。','',
'### 尾延迟、吞吐与首绘','',
'| 场景 | 实现 | P95 ms | P99 ms | 绘制吞吐 帧/s | 首绘 ms | GC0/600帧 |',
'|---|---|---:|---:|---:|---:|---:|']
for scene,scale in [('controls',1),('controls',1.5),('scroll',1)]:
 for variant in ['before','after','default','reference']:
  r=c(scene,scale,variant);lines.append(f"| {scene} {scale*100:g}% | {labels[variant]} | {r['p95Ms']:.3f} | {r['p99Ms']:.3f} | {r['framesPerSecond']:.0f} | {r['firstRenderMs']:.2f} | {r['gc0']:.0f} |")
lines+=['','列表使用 17 个整数滚动位置循环，允许正常命中缓存；不是不断出现新文本/新路径的成本。CPU 循环没有新增的大面积 Blur，滤镜成本单独列在下节。','',
'### 画质：0/3，仍然失败','',
'| 场景 | 最大通道误差 | RMSE | before/after 逐字节一致 |',
'|---|---:|---:|---|']
for r in cpu['quality']:lines.append(f"| {r['scenario']} {r['scale']*100:g}% | {r['maximumChannelError']} | {r['rmse']:.3f} | {r['beforeAfterExactlyEqual']} |")
lines+=['','门槛保持 max ≤32、RMSE ≤2。未降低 AA 16×16、曲线容差或颜色处理来提速。','','![最终实际控件](images/completion-controls-managed.png)',
'[原版页面](images/completion-controls-reference.png) · [差异图](images/completion-controls-diff.png)','',
'## Gaussian 内核：标量与 SIMD 的相同算法','',
'3 个独立进程/实现，交替顺序；每配置预热 750 ms，采样至少 1500 ms。比较同一个 16384 权重、两次分离卷积的实现，硬件测试另验证逐字节相同。这里不是与原版 Skia 的模糊耗时比较。[全部原始进程数据](filter-performance-results.json) / [参数](filter-performance-manifest.json)。','',
'| 尺寸 | σ | 实现 | ms/次 | CPU ms/次 | B/次 | 工作集 MiB | Private MiB |',
'|---|---:|---|---:|---:|---:|---:|---:|']
for r in filters['summary']:lines.append(f"| {r['width']}×{r['height']} | {r['sigma']:g} | {r['mode']} | {r['ms']:.3f} | {r['cpuMs']:.3f} | {r['allocatedBytes']:.0f} | {r['workingSetMiB']:.1f} | {r['privateMiB']:.1f} |")
ratios=[]
for r in filters['summary']:
 if r['mode']=='simd':
  scalar=next(s for s in filters['summary'] if (s['width'],s['height'],s['sigma'],s['mode'])==(r['width'],r['height'],r['sigma'],'scalar'));ratios.append(scalar['ms']/r['ms'])
lines+=['',f'最终 SIMD 相对当前标量约 **{min(ratios):.1f}–{max(ratios):.1f} 倍**；多累加器展开和行访问复用同时改善标量/AVX2。1024×768、σ=8 仍需约 42 ms，不能视为所有滤镜均满足实时帧预算。已有 Blur 8 个原版画质对照只有 7 个通过，没有用不同近似核换成绩。','',
'## Vulkan：单独的 GPU round-trip','',
'[完整 GPU 报告](PERFORMANCE-GPU-FINAL-2026-10-21.md) 比较同一 GTX 1050 Ti、同样 RGBA8888/PMA；3 fork、5 批/配置、每批目标 70 ms，预热 150 ms 且至少 3 帧。包含录制、提交、等待 fence、完整主机回读。没有把纯提交时间当成完成时间；窗口 swapchain 仍未实现。','',
'以下同时列出较快、较慢和画质失败的代表场景。GPU 内存列为整个进程，不是显存；托管分配不包含原版/驱动的 native 分配。','',
'| 1024×768 场景 | 实现 | 完成 ms | CPU ms | B/帧 | 工作集 MiB | Private MiB | 画质 |',
'|---|---|---:|---:|---:|---:|---:|---|']
for scene in ['clear','blend','image','chinese']:
 for engine in ['original','simd']:
  r=next(r for r in gpu['Rows'] if (r['Scene'],r['Width'],r['Engine'])==(scene,1024,engine))
  status='原版基准' if engine=='original' else 'PASS' if r['PixelPass'] else f"FAIL max={r['MaxPixelError']}"
  lines.append(f"| {scene} | {engine} | {r['MedianMs']:.3f} | {r['CpuMsPerOp']:.3f} | {r['ManagedBytesPerOp']:.0f} | {r['WorkingSetBytes']/1048576:.1f} | {r['PrivateBytes']/1048576:.1f} | {status} |")
lines+=['','GPU 只通过 12/24 个原版像素 gate。图像、AA 和文字失败行仅为诊断，不能据此给性能胜负结论。包含回读的领先也不能推出不回读的屏幕渲染更快；没有硬件 GPU timestamp 或显存分配量统计。新增 GPU 采样/裁剪与 CPU 数值对照通过，不会消除所有原版行为差异。','',
'## 复现','',
'```powershell','powershell -NoProfile -File tools/run-avalonia-controls.ps1 -Output artifacts/my-cpu',
'powershell -NoProfile -File tools/run-filter-benchmarks.ps1 -Output artifacts/my-filters',
'powershell -NoProfile -File tools/run-gpu-benchmarks.ps1 -Output artifacts/my-gpu -Forks 3 -Samples 5 -SampleMilliseconds 70 -WarmupMilliseconds 150 -Report artifacts/my-gpu/report.md','```','',
'CPU/GPU 原版画质失败时脚本返回 2，数据仍完整保留；这是验收失败，不是要求重试或忽略它。若要复现 before 比较，先从 c4e1481 构建并保留完整测试输出目录，再传 `-BeforeDirectory`，不要覆盖快照。本地 `artifacts/completion` 保存全部运行、逐帧 ticks、PNG/RGBA 和日志；随 Git 保存的 JSON/CSV/manifest 足够核查统计和软件/字体指纹。','',
'当前取舍：如果目标是立即替换整个生产 Avalonia 后端，继续使用原版更合适。SimdSkia 已有真实功能与可复测数据，但剩余画质、API 和 GPU 窗口工作仍然实质性存在。']
(docs/'PERFORMANCE-FINAL-2026-10-21.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
logs=docs/'test-logs';logs.mkdir(exist_ok=True)
for name in ['delivery-validation.log','scalar-imaging-final.log','scalar-optimization-final.log','scalar-text-final.log','scalar-controls-final.log']:
 data=(root/name).read_bytes();encoding='utf-16' if data[:2] in [b'\xff\xfe',b'\xfe\xff'] else 'utf-8-sig'
 text=data.decode(encoding).replace('\r\n','\n').replace('\r','\n')
 (logs/(name[:-4]+'.txt')).write_text(text,encoding='utf-8',newline='\n')
print('Archived CPU/GPU/filter measurements, quality results, logs, charts and reviewable reports.')
