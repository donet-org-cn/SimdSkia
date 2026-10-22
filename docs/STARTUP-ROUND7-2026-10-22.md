# 桌面启动白屏与绘图开销：2026-10-22

生产源码 [1438833](https://github.com/hnlyf168/SimdSkia/commit/1438833a3caafa191eefb016f093c1a56d8a4b4e)；冻结基线 `586c443`（生产实现为上一轮 `6c017e7`）。本轮重点是缩短首帧等待，同时避免启动准备给无窗口程序增加开销。**尚未发布新 NuGet，preview.5 不包含这些改动。**

CPU 桌面可见到内容的中位数 **291→260 ms**；从进程启动到内容 **1184→1164 ms**。纯白阶段采样估计 **114→61 ms**，同机原版 CPU 为 **49 ms**。**白屏已缩短，尚未完全消除；持续 GPU 性能和常驻内存没有明显改善。**

证据：[完整 JSON、进程样本与哈希](evidence/startup-round7-2026-10-22.json) · [CPU CSV](evidence/startup-round7-2026-10-22-cpu.csv) · [窗口 CSV](evidence/startup-round7-2026-10-22-windows.csv) · [控件图](evidence/startup-round7-2026-10-22-images/managed.png) · [原版图](evidence/startup-round7-2026-10-22-images/reference.png) · [差异图](evidence/startup-round7-2026-10-22-images/diff.png)。

## 改动与验证依据

- 启动采样发现首帧需要现编译字形覆盖、描边、裁剪和合成路径。增加一次性的桌面准备：在 Avalonia 建立经典桌面生命周期后、构建窗口内容前，用独立线程对 16×16 内存目标执行实际绘图。窗口保持正常显示流程，准备任务不提交也不计数窗口帧。
- 准备线程执行完即退出；无窗口/位图程序不启动它，Native AOT 也跳过。首次试验曾使用线程池并对无窗口程序执行准备，增加了私有内存；该方案已替换，最终测试包含同一程序关闭准备的对照。
- 不变几何复用精确边界；路径编辑后立即失效，保留贝塞尔曲线极值与生命周期检查。
- 字形合批可转移自己拥有的覆盖缓冲并原地作颜色校正；共享原始字形掩码仍保持不可变。重叠字形与不同颜色的缓存隔离逐项验证。

## 测量口径

同机 Windows x64 / i7-8700K（12 逻辑处理器）/ GTX 1050 Ti，Avalonia 12.1.4 / SkiaSharp 3.119.4。离屏控件 .NET 8.0.32；真实窗口及启动 .NET 10.0.13。原版独立进程，正式运行串行进行，不与构建、profiler 或其他基准重叠。表格为逐进程指标中位数。
最终数据全部来自 `artifacts/startup-round7/final`；前期桌面试验有一组被构建干扰，已中止并明确标记无效，不纳入报告。冻结 DLL 与最终程序的哈希再次校验；不同轮次数据不拼接。

## 启动与白屏：JIT

每配置 5 次，共 50 次真实可见启动。before 为冻结旧版本，after 为最终默认设置，disabled 为同一最终可执行文件添加 `--no-preparation`，reference 为原版 CPU。系统字体均选 Microsoft YaHei UI。外部 BitBlt 采样相同内容边框；时间从启动进程计算，包含系统调度和 DWM 动画，磁盘/驱动缓存没有清空。
“纯白阶段”只累计采到纯白且未出现内容的采样区间，不覆盖所有可能的空白/动画状态；没有采到白色不能证明连续时间上的白屏绝对为零。逐次统计、到首帧为止的每次 RGB/时间及完整记录 SHA 在公开 JSON。内存为窗口打开约两秒后的进程快照，非峰值。本轮采样间隔与此前轮次不同，不能把历史白屏数字与本轮拼接计算收益。

| 实现 | 启动→内容 ms（最小–最大） | 可见→内容 ms | 纯白阶段估计 ms | 出现白样本次数 | GC 未强制时 WS / Private MiB |
|---|---:|---:|---:|---:|---:|
| before-cpu | 1184（1176–1224） | 291 | 114 | 5/5 | 92.5 / 44.1 |
| before-direct2d | 1364（1356–1365） | 460 | 289 | 5/5 | 119.4 / 102.0 |
| before-vulkan | 1440（1415–1564） | 526 | 363 | 5/5 | 137.9 / 128.6 |
| after-cpu | 1164（1107–1194） | 260 | 61 | 5/5 | 92.8 / 44.4 |
| after-direct2d | 1287（1278–1314） | 372 | 198 | 5/5 | 119.5 / 102.1 |
| after-vulkan | 1356（1344–1421） | 441 | 266 | 5/5 | 138.4 / 127.9 |
| reference | 1131（1094–1142） | 251 | 49 | 5/5 | 86.7 / 37.1 |
| disabled-cpu | 1206（1183–1211） | 292 | 114 | 5/5 | 92.6 / 44.3 |
| disabled-direct2d | 1378（1366–1409） | 471 | 313 | 5/5 | 119.7 / 102.0 |
| disabled-vulkan | 1422（1409–1445） | 521 | 348 | 5/5 | 138.0 / 127.8 |

Direct2D/Vulkan 白色阶段分别为 **289→198 ms**、**363→266 ms**。默认与关闭准备使用同一最终程序，CPU 白色阶段为 61 vs 114 ms；这比只比较旧版本更能说明准备工作的效果。GPU 首次设备/管线创建仍在所属线程完成，本轮没有跨线程提前创建上下文。

首帧仍受样本间隔、DWM 动画和机器状态影响。本轮所有配置都在 5/5 次启动中采到白色，不能根据前期少量未采到白色的试验宣称消除白屏。没有通过延迟 Show、隐藏窗口或纯色占位帧改变判定。

## 实际 Native AOT 启动

每配置 5 次，共 20 次，与上面的 JIT 结果分开。两个程序都实际发布为 Native AOT；采到首帧后枚举进程模块，确认没有加载 CoreCLR/JIT，不能只根据 RuntimeFeature 标志判断。本次没有旧版本 AOT 启动基线。

| 实现 | 启动→内容 ms（最小–最大） | 可见→内容 ms | 纯白阶段估计 ms | 出现白样本次数 | GC 未强制时 WS / Private MiB |
|---|---:|---:|---:|---:|---:|
| after-cpu | 337（334–364） | 265 | 83 | 5/5 | 49.0 / 27.6 |
| after-direct2d | 336（333–342） | 263 | 61 | 5/5 | 76.5 / 87.1 |
| after-vulkan | 336（329–343） | 263 | 67 | 5/5 | 94.3 / 111.8 |
| reference | 337（329–368） | 265 | 66 | 5/5 | 43.8 / 20.5 |

可运行发布目录（不含 PDB）：SimdSkia **26.99 MiB**，原版 **38.33 MiB**。字体均来自系统，没有携带测试字体；逐文件大小和 SHA 在证据中。此处未测 AOT 持续绘图吞吐。

AOT 首帧中位数约 337 ms，与原版 337 ms 接近，CPU 白色阶段仍为 83 vs 原版 66 ms。AOT 跳过启动准备，因此不能把这组成绩归功于后台准备；工作集和 Private 仍高于原版。

## CPU 持续绘图与内存

真实 Fluent 控件，预热 2.5 秒、每进程 600 帧、每配置 3 个进程。系统字体双方还核对正常字面的 glyf/cmap/head/hmtx/OS/2 表哈希；固定文件组使用同一 ZCOOL 字体。CPU ms 是整个进程消耗，不能当作固定帧率 CPU 占用。托管分配/堆不包含原版原生分配，判断进程内存请同时看 WS/Private。
原版默认使用逐次绘制的 opacity；“原版完整组”强制透明组隔离，以匹配本实现采用的语义，因此耗时较高。两组都保留，不拿较慢组代表原版日常性能。整页画质仍未通过，计时只作诊断。

| 场景 / DPI | 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | Private MiB | GC 后托管 MiB |
|---|---|---:|---:|---:|---:|---:|---:|
| controls / 1 | 优化前 | 0.695 | 0.703 | 5.67 | 94.8 | 48.6 | 15.0 |
| controls / 1 | 优化后 | 0.666 | 0.703 | 5.83 | 94.8 | 48.6 | 15.0 |
| controls / 1 | 原版默认 | 0.913 | 0.911 | 7.77 | 88.4 | 42.8 | 2.6 |
| controls / 1 | 原版完整组 | 3.308 | 3.281 | 7.94 | 88.4 | 43.1 | 2.6 |

| 场景 / DPI | 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | Private MiB | GC 后托管 MiB |
|---|---|---:|---:|---:|---:|---:|---:|
| controls / 1 | 优化前 | 0.640 | 0.625 | 5.83 | 110.1 | 69.9 | 20.0 |
| controls / 1 | 优化后 | 0.617 | 0.651 | 5.83 | 110.2 | 69.9 | 20.0 |
| controls / 1 | 原版默认 | 0.895 | 0.885 | 7.77 | 96.2 | 51.0 | 2.3 |
| controls / 1 | 原版完整组 | 3.297 | 3.281 | 7.94 | 95.8 | 50.9 | 2.3 |
| controls / 1.5 | 优化前 | 1.533 | 1.562 | 5.67 | 114.4 | 73.9 | 30.6 |
| controls / 1.5 | 优化后 | 1.511 | 1.536 | 5.83 | 112.3 | 72.0 | 30.6 |
| controls / 1.5 | 原版默认 | 1.706 | 1.693 | 7.77 | 99.9 | 54.7 | 2.3 |
| controls / 1.5 | 原版完整组 | 7.029 | 7.005 | 7.94 | 98.5 | 53.8 | 2.3 |
| scroll / 1 | 优化前 | 0.405 | 0.469 | 3.22 | 95.9 | 62.2 | 15.8 |
| scroll / 1 | 优化后 | 0.400 | 0.417 | 3.19 | 96.2 | 62.6 | 15.8 |
| scroll / 1 | 原版默认 | 0.373 | 0.391 | 8.03 | 86.8 | 48.1 | 1.4 |
| scroll / 1 | 原版完整组 | 0.381 | 0.417 | 8.03 | 86.6 | 47.9 | 1.4 |

系统字体绘制耗时变化 **-4.3%**，Private 变化 **+0.0 MiB**。这次仅 3 个进程，前后耗时范围有重叠，不宣称普遍或统计显著的提升；完整控件画质仍 FAIL，也不能据此证明整体胜过原版。原版默认 Private 为 42.8 MiB，当前为 48.6 MiB，常驻差距没有消除。

连续更新图片保留上一轮相同的短批次（8 次预热、160 次更新、3 个进程），源图片 512×512、输出 320×240，不绘文字，使用默认系统字体配置；WS/Private 在完整 GC 后采样，非计时峰值。

| 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | GC 后 Private MiB |
|---|---:|---:|---:|---:|---:|
| 优化前 | 1.655 | 1.855 | 1.90 | 57.8 | 28.0 |
| 优化后 | 1.697 | 1.855 | 1.90 | 57.9 | 28.1 |
| 原版 | 0.707 | 0.684 | 3.95 | 54.4 | 26.5 |

换图画质通过，但本轮计时略慢于优化前，仍明显慢于原版；分配和常驻内存基本持平，不计为新的优化收益。

## 实际 CPU / GPU 窗口

同一动态控件、系统字体、1000×720 像素、125% DPI，8 秒预热和至少 8 秒测量，每配置 3 个进程。本轮前后所有进程尺寸/DPI 一致；历史轮次 DPI 不同，不与历史窗口数据计算增益。GPU 等待完成；Direct2D 走原生交换链，Vulkan fence 后回读并复制到平台 framebuffer。窗口移至屏幕外测服务耗时，非显示器 FPS。CPU% 除以 12 个逻辑处理器；显存未纳入内存列。

| 后端 | 实现 | P50 ms | P95 ms | CPU 机器% | 峰值 WS MiB | 峰值 Private MiB | KiB/帧 |
|---|---|---:|---:|---:|---:|---:|---:|
| cpu | 优化前 | 1.040 | 1.459 | 0.911 | 103.0 | 48.5 | 19.24 |
| cpu | 优化后 | 1.055 | 1.592 | 0.925 | 103.7 | 48.9 | 16.05 |
| direct2d | 优化前 | 0.708 | 1.018 | 0.927 | 126.6 | 103.3 | 14.28 |
| direct2d | 优化后 | 0.709 | 0.960 | 1.218 | 127.6 | 103.7 | 14.37 |
| vulkan | 优化前 | 1.952 | 2.231 | 1.493 | 149.5 | 133.7 | 16.24 |
| vulkan | 优化后 | 1.943 | 2.367 | 1.525 | 148.3 | 132.5 | 16.22 |

CPU 窗口每帧托管分配减少约 16.6%，但 P50 由 1.040 增至 1.055 ms，P95 也上升。Direct2D P50 基本不变，进程 CPU 中位数 0.927%→1.218%；Vulkan P95 为 2.231→2.367 ms。不能宣称稳态 CPU/GPU 全面提速。

GPU 进程波动较大：优化后 Direct2D 的各进程 P50 为 1.600/0.709/0.708 ms，Vulkan 为 1.943/1.942/3.266 ms，异常较慢进程仍保留在汇总和证据中。没有测 GPU 时钟，不能确定波动来自驱动、设备状态还是其他系统因素。

本节是 SimdSkia 自身前后对照，没有重测原版 GPU。不得拿原版 CPU 当作 GPU 对手；8 秒测量也不能代替长期业务负载。

## 回归与尚未完成项

Release 构建零警告/错误；8665 优化检查、9734 控件/生命周期检查通过，完全禁用硬件指令后也通过。首次标量命令未传字体路径，计数为 8653；补传 ZCOOL 中文字体后，额外 12 项字体参数/缓存检查也通过，总计 8665。Direct2D 344、Vulkan 540、GPU 采样 590、GPU 集成 12174、字形合批 60、平台字体 36 及启动准备/帧提交检查通过。12 对普通控件帧、3 对换图帧、6 对窗口截图与冻结版本逐字节相同；换图帧也与原版一致。

原版功能探针 **82/114**，旧图像 **108/120**，SK* 公共 API **425/3235**；完整控件画质仍不满足 max≤32/RMSE≤2。`verify.ps1 -Reference -Gpu` 仍返回 **2**，没有改低门槛或把已有失败计为通过。

三平台托管/标量、实际桌面 JIT/AOT/nupkg 消费及 Windows 双实现 AOT：

- [managed.yml](https://github.com/hnlyf168/SimdSkia/actions/runs/39782824212)：success，源码 1438833。
- [desktop.yml](https://github.com/hnlyf168/SimdSkia/actions/runs/39782824193)：success，源码 1438833。
- [native-aot.yml](https://github.com/hnlyf168/SimdSkia/actions/runs/39782824150)：success，源码 1438833。

CI 是功能验证，不作为云端性能成绩。macOS/Linux 启动白屏时长本轮未实测；本机的结果不能直接推及不同主题、驱动和业务窗口。

## 使用与复现

当前改动在源码中，尚未更新 NuGet。经典桌面 JIT 默认启用，无需修改窗口 Show 流程。需要排查或自行管理预备工作时，可在新源码版本中关闭：

```csharp
AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)
    .StartWithClassicDesktopLifetime(args);
```

```powershell
dotnet build SimdSkia.sln -c Release
powershell -NoProfile -File tools/verify.ps1 -Reference -Gpu
dotnet build tests/Startup/Managed -c Release -p:PublishAot=false
dotnet build tests/Startup/Reference -c Release -p:PublishAot=false
python tools/run-startup-comparison.py --output artifacts/new-startup --runs 5 --backends cpu direct2d vulkan --include-preparation-disabled
dotnet publish tests/Startup/Managed -c Release -r win-x64 --self-contained true -p:PublishAot=true -o artifacts/new-aot-managed
dotnet publish tests/Startup/Reference -c Release -r win-x64 --self-contained true -p:PublishAot=true -o artifacts/new-aot-reference
python tools/run-startup-comparison.py --output artifacts/new-aot-startup --managed artifacts/new-aot-managed/Startup.Managed.exe --reference artifacts/new-aot-reference/Startup.Reference.exe --runs 5 --backends cpu direct2d vulkan
```

前后对照先冻结旧版本，再传入 `--before` / `--before-directory` / `--before-executable` / `-BeforeDirectory`。CPU、窗口和完整测试命令均保留在公开证据的 execution 字段中。启动测量需要可见桌面。
