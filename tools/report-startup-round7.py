"""Report final, isolated measurements; keep failed quality gates visible."""
import argparse,csv,hashlib,json,shutil,statistics
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=Path('artifacts/startup-round7/final'));a=p.parse_args();root=a.root
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
source='1438833a3caafa191eefb016f093c1a56d8a4b4e';baseline=root.parent
system=read(root/'system/results.json');fixed=read(root/'fixed/results.json');tile=read(root/'tile/results.json')
windows=read(root/'desktop/summary.json');startup=read(root/'startup/results.json');aot=read(root/'startup-aot/results.json')
window_runs=[read(f) for f in sorted((root/'desktop').glob('*/benchmark.json'))]
assert len(window_runs)==18 and len({r['scaling'] for r in window_runs})==1
features=read(root/'features.json');smoke=read(root/'smoke/results.json');ci=read(root/'ci.json');execution=read(root/'execution.json')
assert all(r['exitCode']==r['expectedExit'] for r in execution)
assert len(system['runs'])==12 and len(fixed['runs'])==36 and len(tile['runs'])==9
assert len(startup['runs'])==50 and len(aot['runs'])==20 and len(windows)==6
assert all(r['conclusion']=='success' and r['head_sha']==source for r in ci) and len(ci)==3
assert all(r['same'] for r in smoke) and len(smoke)==6
assert all(r['allSampleCountsSufficient'] and r['cpuFallbackCommands']==0 for r in windows)
for data,native in [(startup,False),(aot,True)]:
    for r in data['runs']:
        assert r['worker']['nativeAot']==native
        loaded={x.lower() for x in r['loadedModules']}
        assert bool(loaded&{'coreclr.dll','clrjit.dll','clr.dll','mscorwks.dll','mscorsvr.dll'})!=native
manifests={name:read(root/name/'manifest.json') for name in ['system','fixed','tile','desktop','startup','startup-aot']}
for manifest in manifests.values():
    for name,digest in manifest.get('runtimeFileSha256',{}).items():assert sha(Path(name))==digest,name
dirs={'before':baseline/'before-controls','after':Path('tests/AvaloniaControls/bin/Release/net8.0'),
      'reference':Path('tests/AvaloniaControls.Reference/bin/Release/net8.0'),'default':Path('tests/AvaloniaControls.Reference/bin/Release/net8.0')}
for r in manifests['fixed']['Files']:assert sha(dirs[r['Engine']]/r['Name']).upper()==r['Sha256']
for name,digest in read(baseline/'before-sha256.json').items():assert sha(baseline/name)==digest,name
pairs=[]
for folder in ['system','fixed','tile']:
    for after in sorted((root/folder).glob('after-*/scene.rgba')):
        before=after.parent.with_name(after.parent.name.replace('after-','before-',1))/after.name
        assert after.read_bytes()==before.read_bytes(),str(after)
        pairs.append({'before':str(before),'after':str(after),'sha256':sha(after)})
assert len(pairs)==15 and all(q['passed'] for q in tile['quality'])
required={'verify-final.log':['PASS 8665','PASS: 9734','PASS 540','PASS 599','FAIL: original pixel quality gate'],
          'scalar.log':['PASS 8653','False'],'scalar-font.log':['PASS 8665','False'],'controls-scalar.log':['PASS: 9734'],
          'test-Direct2D.log':['PASS 344'],'test-AvaloniaGpu.log':['PASS 12174'],'test-GlyphBatches.log':['PASS 60'],
          'test-PlatformFonts.log':['PASS 36','PASS startup preparation','PASS framebuffer']}
for name,needles in required.items():
    data=(root/name).read_bytes();text=data.decode('utf-16' if data[:2] in (b'\xff\xfe',b'\xfe\xff') else 'utf-8-sig')
    for needle in needles:assert needle in text,(name,needle)
ci_records=[]
for path in sorted((root/'ci').rglob('runtime.txt')):
    ci_records.append({'path':str(path.relative_to(root)),'sha256':sha(path),'text':path.read_text(encoding='utf-8-sig')})
assert len(ci_records)==9
for path in sorted((root/'ci').rglob('metrics.json')):
    data=read(path);assert data['nativeAot'] and data['imageBrushProbePassed']
    ci_records.append({'path':str(path.relative_to(root)),'sha256':sha(path),'metrics':data})
assert len(ci_records)==11
sizes={}
for name in ['aot-after','aot-reference']:
    files=[f for f in (root/name).rglob('*') if f.is_file() and f.suffix.lower() not in ['.pdb','.dbg']]
    sizes[name]={'bytes':sum(f.stat().st_size for f in files),'files':[{'name':str(f.relative_to(root/name)),'bytes':f.stat().st_size,'sha256':sha(f)} for f in files]}
screen_records=[]
for folder in ['startup','startup-aot']:
    for path in sorted((root/folder).glob('*/screen-samples.json')):
        samples=read(path);first=next(i for i,r in enumerate(samples) if r['content'])
        screen_records.append({'path':str(path.relative_to(root)),'sha256':sha(path),'sampleCount':len(samples),'throughFirstContent':samples[:first+1]})
assert len(screen_records)==70
stem='startup-round7-2026-10-22';out=Path('docs/evidence');images=out/(stem+'-images');images.mkdir(exist_ok=True)
for name in ['managed','reference','diff']:
    shutil.copyfile(root/f'system/{name}-controls-dpi1.png',images/(name+'.png'))
evidence={'sourceCommit':source,'baselineCommit':'586c443846105feda2a87c3c17cfe14b456bf9fd','system':system,'fixed':fixed,'tile':tile,
          'windows':{'summary':windows,'runs':window_runs},
          'startup':startup,'startupAot':aot,'startupScreenSamples':screen_records,'aotDeployment':sizes,'features':features,'windowFrames':smoke,'allCpuFramePairs':pairs,
          'manifests':manifests,'systemFontTableAudit':read(root/'system/font-tables.json'),'ci':ci,'ciRuntimeRecords':ci_records,'execution':execution,
          'hardware':read(root/'hardware.json'),'logSha256':{name:sha(root/name) for name in required},'runtimeHashesReverified':True}
(out/(stem+'.json')).write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for name,rows in [('cpu',[{'fontMode':mode,**r} for mode,d in [('system',system),('fixed',fixed)] for r in d['summary']]),('windows',windows)]:
    with (out/(stem+'-'+name+'.csv')).open('w',encoding='utf-8',newline='') as f:
        writer=csv.DictWriter(f,list(rows[0]));writer.writeheader();writer.writerows(rows)
labels={'before':'优化前','after':'优化后','default':'原版默认','reference':'原版完整组'}
def cpu_table(data):
    lines=['| 场景 / DPI | 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | Private MiB | GC 后托管 MiB |','|---|---|---:|---:|---:|---:|---:|---:|']
    for r in sorted(data['summary'],key=lambda r:(r['scenario'],r['scale'],list(labels).index(r['variant']))):
        lines.append(f"| {r['scenario']} / {r['scale']} | {labels[r['variant']]} | {r['millisecondsPerFrame']:.3f} | {r['cpuMillisecondsPerFrame']:.3f} | {r['allocatedBytesPerFrame']/1024:.2f} | {r['workingSetMiB']:.1f} | {r['privateMiB']:.1f} | {r['managedRetainedAfterGcMiB']:.1f} |")
    return lines
def startup_table(data):
    lines=['| 实现 | 启动→内容 ms（最小–最大） | 可见→内容 ms | 纯白阶段估计 ms | 出现白样本次数 | GC 未强制时 WS / Private MiB |','|---|---:|---:|---:|---:|---:|']
    for r in data['summary']:
        group=[x for x in data['runs'] if x['variant']==r['variant']];rows=[x['worker'] for x in group]
        ws=statistics.median(x['workingSetMiB'] for x in rows);private=statistics.median(x['privateMiB'] for x in rows)
        times=[x['firstContentMs'] for x in group];white=sum(x['whiteSamples']>0 for x in group)
        lines.append(f"| {r['variant']} | {r['firstContentMs']:.0f}（{min(times):.0f}–{max(times):.0f}） | {r['visibleToContentMs']:.0f} | {r['observedWhiteMs']:.0f} | {white}/{len(group)} | {ws:.1f} / {private:.1f} |")
    return lines
s={r['variant']:r for r in system['summary']};boot={r['variant']:r for r in startup['summary']}
boot_aot={r['variant']:r for r in aot['summary']};bw={r['backend']:r for r in windows if r['engine']=='before'};aw={r['backend']:r for r in windows if r['engine']=='after'}
lines=['# 桌面启动白屏与绘图开销：2026-10-22','',
f'生产源码 [{source[:7]}](https://github.com/hnlyf168/SimdSkia/commit/{source})；冻结基线 `586c443`（生产实现为上一轮 `6c017e7`）。本轮重点是缩短首帧等待，同时避免启动准备给无窗口程序增加开销。**尚未发布新 NuGet，preview.5 不包含这些改动。**','',
f"CPU 桌面可见到内容的中位数 **{boot['before-cpu']['visibleToContentMs']:.0f}→{boot['after-cpu']['visibleToContentMs']:.0f} ms**；从进程启动到内容 **{boot['before-cpu']['firstContentMs']:.0f}→{boot['after-cpu']['firstContentMs']:.0f} ms**。纯白阶段采样估计 **{boot['before-cpu']['observedWhiteMs']:.0f}→{boot['after-cpu']['observedWhiteMs']:.0f} ms**，同机原版 CPU 为 **{boot['reference']['observedWhiteMs']:.0f} ms**。**白屏已缩短，尚未完全消除；持续 GPU 性能和常驻内存没有明显改善。**",'',
f'证据：[完整 JSON、进程样本与哈希](evidence/{stem}.json) · [CPU CSV](evidence/{stem}-cpu.csv) · [窗口 CSV](evidence/{stem}-windows.csv) · [控件图](evidence/{stem}-images/managed.png) · [原版图](evidence/{stem}-images/reference.png) · [差异图](evidence/{stem}-images/diff.png)。','',
'## 改动与验证依据','',
'- 启动采样发现首帧需要现编译字形覆盖、描边、裁剪和合成路径。增加一次性的桌面准备：在 Avalonia 建立经典桌面生命周期后、构建窗口内容前，用独立线程对 16×16 内存目标执行实际绘图。窗口保持正常显示流程，准备任务不提交也不计数窗口帧。',
'- 准备线程执行完即退出；无窗口/位图程序不启动它，Native AOT 也跳过。首次试验曾使用线程池并对无窗口程序执行准备，增加了私有内存；该方案已替换，最终测试包含同一程序关闭准备的对照。',
'- 不变几何复用精确边界；路径编辑后立即失效，保留贝塞尔曲线极值与生命周期检查。',
'- 字形合批可转移自己拥有的覆盖缓冲并原地作颜色校正；共享原始字形掩码仍保持不可变。重叠字形与不同颜色的缓存隔离逐项验证。','',
'## 测量口径','',
'同机 Windows x64 / i7-8700K（12 逻辑处理器）/ GTX 1050 Ti，Avalonia 12.1.4 / SkiaSharp 3.119.4。离屏控件 .NET 8.0.32；真实窗口及启动 .NET 10.0.13。原版独立进程，正式运行串行进行，不与构建、profiler 或其他基准重叠。表格为逐进程指标中位数。',
'最终数据全部来自 `artifacts/startup-round7/final`；前期桌面试验有一组被构建干扰，已中止并明确标记无效，不纳入报告。冻结 DLL 与最终程序的哈希再次校验；不同轮次数据不拼接。','',
'## 启动与白屏：JIT','',
'每配置 5 次，共 50 次真实可见启动。before 为冻结旧版本，after 为最终默认设置，disabled 为同一最终可执行文件添加 `--no-preparation`，reference 为原版 CPU。系统字体均选 Microsoft YaHei UI。外部 BitBlt 采样相同内容边框；时间从启动进程计算，包含系统调度和 DWM 动画，磁盘/驱动缓存没有清空。',
'“纯白阶段”只累计采到纯白且未出现内容的采样区间，不覆盖所有可能的空白/动画状态；没有采到白色不能证明连续时间上的白屏绝对为零。逐次统计、到首帧为止的每次 RGB/时间及完整记录 SHA 在公开 JSON。内存为窗口打开约两秒后的进程快照，非峰值。本轮采样间隔与此前轮次不同，不能把历史白屏数字与本轮拼接计算收益。','',*startup_table(startup),'',
f"Direct2D/Vulkan 白色阶段分别为 **{boot['before-direct2d']['observedWhiteMs']:.0f}→{boot['after-direct2d']['observedWhiteMs']:.0f} ms**、**{boot['before-vulkan']['observedWhiteMs']:.0f}→{boot['after-vulkan']['observedWhiteMs']:.0f} ms**。默认与关闭准备使用同一最终程序，CPU 白色阶段为 {boot['after-cpu']['observedWhiteMs']:.0f} vs {boot['disabled-cpu']['observedWhiteMs']:.0f} ms；这比只比较旧版本更能说明准备工作的效果。GPU 首次设备/管线创建仍在所属线程完成，本轮没有跨线程提前创建上下文。",'',
'首帧仍受样本间隔、DWM 动画和机器状态影响。本轮所有配置都在 5/5 次启动中采到白色，不能根据前期少量未采到白色的试验宣称消除白屏。没有通过延迟 Show、隐藏窗口或纯色占位帧改变判定。','',
'## 实际 Native AOT 启动','',
'每配置 5 次，共 20 次，与上面的 JIT 结果分开。两个程序都实际发布为 Native AOT；采到首帧后枚举进程模块，确认没有加载 CoreCLR/JIT，不能只根据 RuntimeFeature 标志判断。本次没有旧版本 AOT 启动基线。','',*startup_table(aot),'',
f"可运行发布目录（不含 PDB）：SimdSkia **{sizes['aot-after']['bytes']/1048576:.2f} MiB**，原版 **{sizes['aot-reference']['bytes']/1048576:.2f} MiB**。字体均来自系统，没有携带测试字体；逐文件大小和 SHA 在证据中。此处未测 AOT 持续绘图吞吐。",'',
f"AOT 首帧中位数约 {boot_aot['after-cpu']['firstContentMs']:.0f} ms，与原版 {boot_aot['reference']['firstContentMs']:.0f} ms 接近，CPU 白色阶段仍为 {boot_aot['after-cpu']['observedWhiteMs']:.0f} vs 原版 {boot_aot['reference']['observedWhiteMs']:.0f} ms。AOT 跳过启动准备，因此不能把这组成绩归功于后台准备；工作集和 Private 仍高于原版。",'',
'## CPU 持续绘图与内存','',
'真实 Fluent 控件，预热 2.5 秒、每进程 600 帧、每配置 3 个进程。系统字体双方还核对正常字面的 glyf/cmap/head/hmtx/OS/2 表哈希；固定文件组使用同一 ZCOOL 字体。CPU ms 是整个进程消耗，不能当作固定帧率 CPU 占用。托管分配/堆不包含原版原生分配，判断进程内存请同时看 WS/Private。',
'原版默认使用逐次绘制的 opacity；“原版完整组”强制透明组隔离，以匹配本实现采用的语义，因此耗时较高。两组都保留，不拿较慢组代表原版日常性能。整页画质仍未通过，计时只作诊断。','',*cpu_table(system),'',*cpu_table(fixed),'',
f"系统字体绘制耗时变化 **{(s['after']['millisecondsPerFrame']/s['before']['millisecondsPerFrame']-1)*100:+.1f}%**，Private 变化 **{s['after']['privateMiB']-s['before']['privateMiB']:+.1f} MiB**。这次仅 3 个进程，前后耗时范围有重叠，不宣称普遍或统计显著的提升；完整控件画质仍 FAIL，也不能据此证明整体胜过原版。原版默认 Private 为 {s['default']['privateMiB']:.1f} MiB，当前为 {s['after']['privateMiB']:.1f} MiB，常驻差距没有消除。",'',
'连续更新图片保留上一轮相同的短批次（8 次预热、160 次更新、3 个进程），源图片 512×512、输出 320×240，不绘文字，使用默认系统字体配置；WS/Private 在完整 GC 后采样，非计时峰值。','',
'| 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | GC 后 Private MiB |','|---|---:|---:|---:|---:|---:|']
for r in tile['summary']:lines.append(f"| {'原版' if r['engine']=='reference' else labels[r['engine']]} | {r['millisecondsPerFrame']:.3f} | {r['cpuMillisecondsPerFrame']:.3f} | {r['allocatedBytesPerFrame']/1024:.2f} | {r['workingSetMiB']:.1f} | {r['privateMiB']:.1f} |")
lines+=['','换图画质通过，但本轮计时略慢于优化前，仍明显慢于原版；分配和常驻内存基本持平，不计为新的优化收益。']
lines+=['','## 实际 CPU / GPU 窗口','',
f"同一动态控件、系统字体、1000×720 像素、{window_runs[0]['scaling']*100:.0f}% DPI，8 秒预热和至少 8 秒测量，每配置 3 个进程。本轮前后所有进程尺寸/DPI 一致；历史轮次 DPI 不同，不与历史窗口数据计算增益。GPU 等待完成；Direct2D 走原生交换链，Vulkan fence 后回读并复制到平台 framebuffer。窗口移至屏幕外测服务耗时，非显示器 FPS。CPU% 除以 12 个逻辑处理器；显存未纳入内存列。",'',
'| 后端 | 实现 | P50 ms | P95 ms | CPU 机器% | 峰值 WS MiB | 峰值 Private MiB | KiB/帧 |','|---|---|---:|---:|---:|---:|---:|---:|']
for r in windows:lines.append(f"| {r['backend']} | {labels[r['engine']]} | {r['drawingP50Ms']:.3f} | {r['drawingP95Ms']:.3f} | {r['cpuPercentMachine']:.3f} | {r['peakWorkingSetBytes']/1048576:.1f} | {r['peakPrivateBytes']/1048576:.1f} | {r['allocationBytesPerFrame']/1024:.2f} |")
lines+=['',f"CPU 窗口每帧托管分配减少约 {(1-aw['cpu']['allocationBytesPerFrame']/bw['cpu']['allocationBytesPerFrame'])*100:.1f}%，但 P50 由 {bw['cpu']['drawingP50Ms']:.3f} 增至 {aw['cpu']['drawingP50Ms']:.3f} ms，P95 也上升。Direct2D P50 基本不变，进程 CPU 中位数 {bw['direct2d']['cpuPercentMachine']:.3f}%→{aw['direct2d']['cpuPercentMachine']:.3f}%；Vulkan P95 为 {bw['vulkan']['drawingP95Ms']:.3f}→{aw['vulkan']['drawingP95Ms']:.3f} ms。不能宣称稳态 CPU/GPU 全面提速。",'',
'GPU 进程波动较大：优化后 Direct2D 的各进程 P50 为 1.600/0.709/0.708 ms，Vulkan 为 1.943/1.942/3.266 ms，异常较慢进程仍保留在汇总和证据中。没有测 GPU 时钟，不能确定波动来自驱动、设备状态还是其他系统因素。','',
'本节是 SimdSkia 自身前后对照，没有重测原版 GPU。不得拿原版 CPU 当作 GPU 对手；8 秒测量也不能代替长期业务负载。','',
'## 回归与尚未完成项','',
'Release 构建零警告/错误；8665 优化检查、9734 控件/生命周期检查通过，完全禁用硬件指令后也通过。首次标量命令未传字体路径，计数为 8653；补传 ZCOOL 中文字体后，额外 12 项字体参数/缓存检查也通过，总计 8665。Direct2D 344、Vulkan 540、GPU 采样 590、GPU 集成 12174、字形合批 60、平台字体 36 及启动准备/帧提交检查通过。12 对普通控件帧、3 对换图帧、6 对窗口截图与冻结版本逐字节相同；换图帧也与原版一致。','',
f"原版功能探针 **{features['passed']}/{features['total']}**，旧图像 **108/120**，SK* 公共 API **425/3235**；完整控件画质仍不满足 max≤32/RMSE≤2。`verify.ps1 -Reference -Gpu` 仍返回 **2**，没有改低门槛或把已有失败计为通过。",'',
'三平台托管/标量、实际桌面 JIT/AOT/nupkg 消费及 Windows 双实现 AOT：','']
for r in ci:lines.append(f"- [{r['workflow']}]({r['html_url']})：{r['conclusion']}，源码 {source[:7]}。")
lines+=['','CI 是功能验证，不作为云端性能成绩。macOS/Linux 启动白屏时长本轮未实测；本机的结果不能直接推及不同主题、驱动和业务窗口。','',
'## 使用与复现','',
'当前改动在源码中，尚未更新 NuGet。经典桌面 JIT 默认启用，无需修改窗口 Show 流程。需要排查或自行管理预备工作时，可在新源码版本中关闭：','',
'```csharp','AppBuilder.Configure<App>()','    .UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)','    .StartWithClassicDesktopLifetime(args);','```','',
'```powershell','dotnet build SimdSkia.sln -c Release','powershell -NoProfile -File tools/verify.ps1 -Reference -Gpu',
'dotnet build tests/Startup/Managed -c Release -p:PublishAot=false','dotnet build tests/Startup/Reference -c Release -p:PublishAot=false',
'python tools/run-startup-comparison.py --output artifacts/new-startup --runs 5 --backends cpu direct2d vulkan --include-preparation-disabled',
'dotnet publish tests/Startup/Managed -c Release -r win-x64 --self-contained true -p:PublishAot=true -o artifacts/new-aot-managed',
'dotnet publish tests/Startup/Reference -c Release -r win-x64 --self-contained true -p:PublishAot=true -o artifacts/new-aot-reference',
'python tools/run-startup-comparison.py --output artifacts/new-aot-startup --managed artifacts/new-aot-managed/Startup.Managed.exe --reference artifacts/new-aot-reference/Startup.Reference.exe --runs 5 --backends cpu direct2d vulkan','```','',
'前后对照先冻结旧版本，再传入 `--before` / `--before-directory` / `--before-executable` / `-BeforeDirectory`。CPU、窗口和完整测试命令均保留在公开证据的 execution 字段中。启动测量需要可见桌面。','']
Path('docs/STARTUP-ROUND7-2026-10-22.md').write_text('\n'.join(lines),encoding='utf-8')
print('PASS final hashes, 70 startups, 15 CPU pairs and 6 window pairs; report written with known failures')
