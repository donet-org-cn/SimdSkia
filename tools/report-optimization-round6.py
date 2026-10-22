"""Publish all measurements, including regressions and failed native quality gates."""
import argparse,csv,hashlib,json,shutil,zipfile
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=Path('artifacts/optimization-round6'));a=p.parse_args();root=a.root
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
system=read(root/'system-complete/results.json');fixed=read(root/'fixed-complete/results.json');tile=read(root/'tile-complete/results.json')
desktop=read(root/'desktop-complete/summary.json');startup=read(root/'startup-complete/results.json')
features=read(root/'features-complete.json');smoke=read(root/'smoke-complete/results.json');ci=read(root/'ci-complete.json')
assert len(ci)==3 and all(r['conclusion']=='success' and r['sha']=='6c017e77f99572762d9ec701ca1e80401f5fed03' for r in ci)
assert len(system['runs'])==12 and len(fixed['runs'])==36 and len(tile['runs'])==9 and len(startup['runs'])==35
assert all(r['beforeAfterExactlyEqual'] for r in system['quality']+fixed['quality']+tile['quality'])
assert all(r['passed'] for r in tile['quality']) and len(smoke)==6 and all(r['same'] for r in smoke)
assert all(r['allSampleCountsSufficient'] and r['cpuFallbackCommands']==0 for r in desktop)
required={'verify-complete.log':['PASS 8665','PASS: 9664','PASS 595','PASS: 484','PASS 599','PASS 540','FAIL: original pixel quality gate'],
          'scalar-complete.log':['PASS 8665','False'],'controls-scalar-complete.log':['PASS: 9664'],
          'complete-Direct2D.log':['PASS 344'],'complete-AvaloniaGpu.log':['PASS 12174'],'complete-GlyphBatches.log':['PASS 60'],'complete-PlatformFonts.log':['PASS 36','PASS framebuffer']}
for name,needles in required.items():
    b=(root/name).read_bytes();s=b.decode('utf-16' if b[:2] in (b'\xff\xfe',b'\xfe\xff') else 'utf-8-sig')
    for needle in needles:assert needle in s,(name,needle)
manifests={name:read(root/name/'manifest.json') for name in ['system-complete','fixed-complete','tile-complete','desktop-complete','startup-complete']}
# Recheck every measured DLL/config at report time, not just at the end of a run.
for manifest in manifests.values():
    for name,digest in manifest.get('runtimeFileSha256',{}).items():assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==digest,name
fixed_dirs={'before':root/'before-controls','after':Path('tests/AvaloniaControls/bin/Release/net8.0'),
    'reference':Path('tests/AvaloniaControls.Reference/bin/Release/net8.0'),'default':Path('tests/AvaloniaControls.Reference/bin/Release/net8.0')}
for row in manifests['fixed-complete']['Files']:assert hashlib.sha256((fixed_dirs[row['Engine']]/row['Name']).read_bytes()).hexdigest().upper()==row['Sha256']
for name,digest in read(root/'before-sha256.json').items():assert hashlib.sha256((root/name).read_bytes()).hexdigest()==digest,name
frame_manifest=read(root/'complete-cpu-frames-manifest.json')
frame_hashes={r['path']:r['sha256'] for r in frame_manifest}
with zipfile.ZipFile(root/'complete-cpu-frames.zip') as archive:
    for row in frame_manifest:
        data=archive.read(row['path'])
        assert len(data)==row['bytes'] and hashlib.sha256(data).hexdigest()==row['sha256'],row['path']
pairs=[]
for name,digest in frame_hashes.items():
    if '/after-' not in name:continue
    before=name.replace('/after-','/before-')
    assert frame_hashes[before]==digest,(name,before)
    pairs.append({'after':name,'before':before,'sha256':digest})
assert len(pairs)==15
evidence={'sourceCommit':'6c017e77f99572762d9ec701ca1e80401f5fed03','baselineCommit':'ba5ad14039634fc21d3c5286e11d5e514f4d23f0',
          'baseline':'Avalonia 12.1.4 / SkiaSharp 3.119.4','manifests':manifests,'system':system,'fixed':fixed,'mutableImage':tile,
          'desktop':{'summary':desktop,'runs':[read(p) for p in sorted((root/'desktop-complete').glob('*/benchmark.json'))]},'startup':startup,
          'features':features,'windowFrames':smoke,'ci':ci,'ciRuntimeRecords':read(root/'ci-records-complete.json'),'api':{k:v for k,v in read(Path('docs/api/comparison.json')).items() if k not in ['MissingOrDifferent','AddedOrDifferent']},'hardware':read(root/'hardware.json'),
          'systemFontTableAudit':read(root/'system-complete/font-tables.json'),
          'verificationLogSha256':{n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in required},'runtimeFilesReverified':True,
          'cpuFrameArchiveManifest':frame_manifest,'allBeforeAfterCpuFrames':pairs,'archiveHashesReverified':True}
out=Path('docs/evidence');out.mkdir(exist_ok=True)
stem='optimization-round6-2026-10-22'
images=out/(stem+'-images');images.mkdir(exist_ok=True)
image_sources={'controls-managed.png':root/'system-complete/managed-controls-dpi1.png',
               'controls-original.png':root/'system-complete/reference-controls-dpi1.png',
               'controls-difference.png':root/'system-complete/diff-controls-dpi1.png',
               'image-brush-managed.png':root/'tile-complete/after-1/scene.png',
               'image-brush-original.png':root/'tile-complete/reference-1/scene.png'}
for name,path in image_sources.items():shutil.copyfile(path,images/name)
evidence['publishedImages']={name:hashlib.sha256(path.read_bytes()).hexdigest() for name,path in image_sources.items()}
(out/(stem+'.json')).write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
rows=[]
for mode,data in [('system',system),('fixed',fixed)]:
    for row in data['summary']:rows.append({'fontMode':mode,**row})
with (out/(stem+'-cpu.csv')).open('w',encoding='utf-8',newline='') as f:
    columns=['fontMode','scenario','scale','variant','millisecondsPerFrame','cpuMillisecondsPerFrame','allocatedBytesPerFrame','workingSetMiB','privateMiB','managedRetainedAfterGcMiB']
    w=csv.DictWriter(f,columns,extrasaction='ignore');w.writeheader();w.writerows(rows)
with (out/(stem+'-window.csv')).open('w',encoding='utf-8',newline='') as f:
    w=csv.DictWriter(f,list(desktop[0]));w.writeheader();w.writerows(desktop)
labels={'before':'优化前','after':'优化后','default':'原版默认','reference':'原版完整组'}
def perf_table(data,extra=False):
    lines=['| 场景 / DPI | 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | Private MiB | GC 后托管 MiB |','|---|---|---:|---:|---:|---:|---:|---:|']
    for r in sorted(data['summary'],key=lambda r:(r['scenario'],r['scale'],list(labels).index(r['variant']))):
        lines.append(f"| {r['scenario']} / {r['scale']} | {labels[r['variant']]} | {r['millisecondsPerFrame']:.3f} | {r['cpuMillisecondsPerFrame']:.3f} | {r['allocatedBytesPerFrame']/1024:.2f} | {r['workingSetMiB']:.1f} | {r['privateMiB']:.1f} | {r['managedRetainedAfterGcMiB']:.1f} |")
    return lines
s={r['variant']:r for r in system['summary']};t={r['engine']:r for r in tile['summary']}
lines=['# 图片采样、画刷缓存与窗口复测：2026-10-22', '',
f"本轮在上一轮源码上继续优化，生产提交 [6c017e7](https://github.com/hnlyf168/SimdSkia/commit/6c017e77f99572762d9ec701ca1e80401f5fed03)，对照基线 `ba5ad14`（其生产实现为上一轮 `1c6af37`）。系统字体控件 **{s['before']['millisecondsPerFrame']:.3f}→{s['after']['millisecondsPerFrame']:.3f} ms/帧**；连续更新 ImageBrush **{t['before']['millisecondsPerFrame']:.3f}→{t['after']['millisecondsPerFrame']:.3f} ms/帧**，该压力场景 GC 后 Private **{t['before']['privateMiB']:.1f}→{t['after']['privateMiB']:.1f} MiB**。普通控件内存没有改善，原版画质/API 缺口与白屏仍需继续处理。",'',
'**尚未发布新 NuGet。** 公开的 preview.5 不包含本轮及前两轮内存/启动改进。没有用 CPU 成绩替代 GPU 成绩，也没有改变画质阈值。','',
f'公开原始数据：[逐进程 JSON、SHA、CI](evidence/{stem}.json) · [CPU CSV](evidence/{stem}-cpu.csv) · [窗口 CSV](evidence/{stem}-window.csv)。','',
f'最终控件截图：[SimdSkia](evidence/{stem}-images/controls-managed.png) · [原版](evidence/{stem}-images/controls-original.png) · [差异图](evidence/{stem}-images/controls-difference.png)。连续换图：[SimdSkia](evidence/{stem}-images/image-brush-managed.png) · [原版](evidence/{stem}-images/image-brush-original.png)。PNG 压缩大小不用于性能/内存比较，像素比较使用解码后 RGBA。','',
'## 实际修复','',
'1. 双线性缩放增加 x86 SSE4.1 四颜色通道并行路径，保持原浮点运算顺序、ties-to-even 舍入、预乘 Alpha 限制及格式转换；不使用 FMA 改变舍入。直通 Alpha、不支持的指令集和显式禁用 SIMD 时使用原标量路径。图片内部恒定裁剪段复用向量化扫描。',
'2. 抗锯齿 1×1/16×16 网格的非负计数除法与取模改为精确移位/掩码；采样点数、填充规则、浮点求交和边缘精度不变。',
'3. ImageBrush 缓存使用弱关联身份，不再通过键持有原图片或视觉树；源位图版本更新后释放不可再次命中的旧缓存项。已有 shader lease 独立持有像素，淘汰不会提前释放它们。',
'4. 最近邻、不透明、整数平移的画刷按行复制，先验证整行不透明再写目标；分数 DPI、旋转/缩放、透明、过滤、混合和裁剪等不满足条件的情况保留通用路径。不是把所有画刷当作不透明。',
'5. 重复矩形绘制复用几何身份，转路径时启用紧凑覆盖段；避免每帧创建新路径并让 LRU 保留几十张相同的整块覆盖图。新增热身后 100 次重绘的分配上限回归。',
'6. 增加连续换图原版对照工具；578 项双线性/标量检查、1,281 项画刷行采样检查、84 项缓存更新与生命周期检查。没有新增第三方或原生绘图库依赖。','',
'## 测量条件','',
'Windows x64，i7-8700K / 12 逻辑处理器，GTX 1050 Ti；Avalonia 12.1.4、SkiaSharp 3.119.4。CPU 控件为 .NET 8.0.32 Release JIT，实际窗口为 .NET 10.0.13 JIT；原版在独立进程。正式测量串行进行，不同时运行 profiler 或构建。轮换进程顺序，每配置 3 个进程；启动每配置 5 次。没有清空系统/驱动缓存，不是冷开机测量。',
'系统字体双方明确选 Microsoft YaHei UI，并在计时后核对 glyf/cmap/head/hmtx/OS/2 表 SHA。固定文件为相同 ZCOOLXiaoWei-Regular.ttf。普通控件预热 2.5 秒、测量 600 帧，48 进程合计 28,800 帧。原版默认与完整 opacity 组分别列出。',
'CPU 时间统计整个进程；满速离屏循环不能当作固定帧率 CPU 占用。窗口机器 CPU%=进程 CPU 时间/墙钟时间/12。WS、Private 是进程内存，GC 后托管堆不包含原版原生分配，显存未计入。表中为每进程指标的中位数；原始范围、P95/P99、GC 和首绘均在 JSON。','',
'## CPU：真实 Fluent 控件','', '系统字体：','',*perf_table(system),'',
f"本轮系统字体耗时下降 **{(1-s['after']['millisecondsPerFrame']/s['before']['millisecondsPerFrame'])*100:.1f}%**。GC 后托管保留量约 {s['after']['managedRetainedAfterGcMiB']:.1f} MiB，基本不变；WS/Private 本次中位数反而分别增加 {s['after']['workingSetMiB']-s['before']['workingSetMiB']:.1f}/{s['after']['privateMiB']-s['before']['privateMiB']:.1f} MiB，普通页面内存问题尚未解决。原版整页画质仍 FAIL，不能由这张表宣称整体胜过原版。",'',
'固定文件字体：','',*perf_table(fixed),'',
'固定字体标准页面和高 DPI 耗时分别下降约 13.2% 和 15.2%；滚动耗时约增加 1.5%，没有改善。高 DPI 的进程内存也没有下降。三轮结果只能支持这些具体场景的判断，不能推导整个业务应用的收益。','',
'## 连续换图：速度和内存','',
'使用默认系统字体配置，负载不绘文字；不注册未使用的控件字体资源。真实 WriteableBitmap 512×512，8 次预热，160 次更新源像素并通过 ImageBrush 绘制到 320×240；每次均做真实复制、版本失效、绘制。新旧使用相同测试宿主，只替换引擎 DLL；与原版也使用相同公开 Avalonia 调用。该场景专门覆盖频繁更新图片，不代表整个应用。此处预热按次数，计时较短，不能作为充分预热后的长期稳态吞吐。WS/Private 在相同完整 GC 后采样，不是计时阶段峰值；托管保留量也不能代替包含原生资源的总内存。','',
'| 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | Private MiB | GC 后托管 MiB |','|---|---:|---:|---:|---:|---:|---:|']
for r in tile['summary']:
    lines.append(f"| {'原版' if r['engine']=='reference' else labels[r['engine']]} | {r['millisecondsPerFrame']:.3f} | {r['cpuMillisecondsPerFrame']:.3f} | {r['allocatedBytesPerFrame']/1024:.2f} | {r['workingSetMiB']:.1f} | {r['privateMiB']:.1f} | {r['managedRetainedAfterGcMiB']:.1f} |")
lines+=['',f"耗时下降 **{(1-t['after']['millisecondsPerFrame']/t['before']['millisecondsPerFrame'])*100:.1f}%**，托管保留量减少 **{t['before']['managedRetainedAfterGcMiB']-t['after']['managedRetainedAfterGcMiB']:.2f} MiB**。三轮最终 RGBA 与优化前、原版均逐字节相同。仍约为原版 **{t['after']['millisecondsPerFrame']/t['reference']['millisecondsPerFrame']:.2f} 倍耗时**；托管分配更少，但 WS/Private 仍略高。托管分配不包含原版内部原生分配。该短压力循环含运行时后台工作，CPU ms/帧可能超过墙钟 ms/帧。",'',
'## 实际 CPU / GPU 窗口','',
'同一动态控件、系统字体、1000×720、100% DPI，预热 8 秒、测量至少 8 秒/60 帧；默认 JIT 设置。窗口移至屏幕外，统计服务时间，不是显示器 FPS。Direct2D 等待 GPU event 并走 DXGI 交换链；Vulkan 等待 fence、回读到平台 framebuffer。GPU 字形图集、软件层仍有 CPU 工作；不支持命令回退计数均为 0。','',
'| 后端 | 实现 | P50 ms | P95 ms | CPU 机器% | 峰值 WS MiB | 峰值 Private MiB | KiB/帧 |','|---|---|---:|---:|---:|---:|---:|---:|']
for r in desktop:
    lines.append(f"| {r['backend']} | {labels[r['engine']]} | {r['drawingP50Ms']:.3f} | {r['drawingP95Ms']:.3f} | {r['cpuPercentMachine']:.3f} | {r['peakWorkingSetBytes']/1048576:.1f} | {r['peakPrivateBytes']/1048576:.1f} | {r['allocationBytesPerFrame']/1024:.2f} |")
lines+=['','真实窗口 CPU/Direct2D/Vulkan 的 P50 分别仅变化约 −2.5%/−0.7%/−0.1%，不能把离屏控件的收益等比例套到窗口。Vulkan 的 P95 由 3.726 增至 4.264 ms、进程 CPU 中位数由 0.796% 增至 0.878%，峰值 Private 增加约 1.5 MiB；本轮不能声称改善了 Vulkan 的整体性能。','',
'GPU 这一节只有本实现优化前后，没有新测原版 GPU；不得拿上面的原版 CPU 数字判定 GPU 胜负。逐进程波动保留，8 秒也不代表长时间业务负载。GPU 时钟未单独记录，不能把变化全部归因于驱动/JIT。','',
'## 启动与白屏','',
'35 次真实可见 Win32 启动，外部单次 BitBlt 屏幕读回观察相同蓝色边框；没有延迟 Show、隐藏窗口、启动遮罩或假首帧。白色阶段是离散采样估计，通常约 31 ms 一次，包含 DWM 动画和读回误差；不能解释为精确到毫秒的连续白屏时间。','',
'| 实现 | 启动→内容 ms | 可见→内容 ms | 白色阶段估计 ms |','|---|---:|---:|---:|']
for r in startup['summary']:lines.append(f"| {r['variant']} | {r['firstContentMs']:.0f} | {r['visibleToContentMs']:.0f} | {r['observedWhiteMs']:.0f} |")
lines+=['','原版在此环境也有约 93 ms 的白色阶段；SimdSkia CPU 约 248 ms，优化前约 249 ms，本轮未实质缩短。Direct2D/Vulkan 的白色阶段更长，且此次 Direct2D 结果比优化前多一个约 31 ms 的采样间隔。没有消除启动白屏，不能将计时波动视作修复。这里测的是仓库共享场景及 JIT，未测业务程序或 AOT 启动时长。','',
'## 回归与剩余差距','',
'Release 全解决方案构建 0 警告/0 错误；8665 优化、9664 控件及生命周期检查通过，完全关闭硬件指令也通过；Direct2D 344、Vulkan 540、GPU 采样 590、GPU 集成 12174、字形批次 60、平台字体 36 项通过。12 对控件帧、3 对换图帧、6 对窗口/呈现截图与优化前逐字节相同。','',
f"原版功能探针 **{features['passed']}/{features['total']}**。整页标准/高 DPI/滚动及系统字体画质仍失败，阈值维持 max≤32、RMSE≤2；旧图像 108/120，SK* API 425/3235。`verify.ps1 -Reference -Gpu` 仍返回 **2**，完整兼容验收未通过。",'',
'| 原版功能探针 | 通过 / 总数 |','|---|---:|']
for group in dict.fromkeys(r['feature'] for r in features['cases']):
    cases=[r for r in features['cases'] if r['feature']==group]
    lines.append(f"| {group} | {sum(r['passed'] for r in cases)} / {len(cases)} |")
lines+=['','仍需处理的重点是普通控件的托管保留量、VisualBrush/BoxShadow 画质、Vulkan 回读与呈现成本，以及首次内容出现前的初始化。此次没有把这些缺口计入已完成清单。','',
'三平台及 AOT CI 记录：','']
for r in ci:lines.append(f"- [{r['name']}]({r['url']})：{r['conclusion']}；源码 {r['sha'][:7]}。")
lines+=['','CI 是功能与包消费验证，不是云机器性能成绩。','',
'## 构建和复现','',
'```powershell','dotnet build SimdSkia.sln -c Release','powershell -NoProfile -File tools/verify.ps1 -Reference -Gpu',
'python tools/run-system-font-comparison.py --family "Microsoft YaHei UI" --output artifacts/new-system --runs 3',
'powershell -NoProfile -File tools/run-avalonia-controls.ps1 -Output artifacts/new-fixed -Frames 600 -Forks 3',
'python tools/run-tile-cache-comparison.py --output artifacts/new-tile --before-directory artifacts/frozen-old-engine-with-current-host --runs 3 --system-fonts',
'python tools/run-desktop-benchmarks.py --output artifacts/new-window --system-fonts --runs 3 --warmup-seconds 8 --measurement-seconds 8',
'dotnet build tests/Startup/Managed -c Release','dotnet build tests/Startup/Reference -c Release',
'python tools/run-startup-comparison.py --output artifacts/new-startup --runs 5 --backends cpu direct2d vulkan','```','',
'优化前后比较需先单独构建并冻结基线 DLL，传入 `--before-directory` / `-BeforeDirectory` / `--before-executable` / `--before`；换图压力测试把当前 AvaloniaControls 主程序放到冻结引擎目录，保留其原依赖和运行时配置，避免重新构建覆盖引擎。测试脚本记录并复核全部 DLL 的 SHA。',
'本机原始结果位于 `artifacts/optimization-round6`，开发中间试跑不作为最终成绩。磁盘不足时原始 RGBA/诊断堆无损归档，逐文件 SHA 校验后才移除未压缩副本；报告公开 JSON/CSV、最终图片和所有失败项保持可查看。','']
Path('docs/OPTIMIZATION-ROUND6-2026-10-22.md').write_text('\n'.join(lines),encoding='utf-8')
print('Published full report and evidence; native quality failures retained.')
