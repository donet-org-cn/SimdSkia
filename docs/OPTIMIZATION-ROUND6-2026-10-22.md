# 图片采样、画刷缓存与窗口复测：2026-10-22

后续的启动准备、边界/字形缓冲优化及新一轮测量见 [桌面启动与性能报告](STARTUP-ROUND7-2026-10-22.md)。本页保留历史数据，不与后续不同 DPI、不同采样间隔的测量拼接。

本轮在上一轮源码上继续优化，生产提交 [6c017e7](https://github.com/hnlyf168/SimdSkia/commit/6c017e77f99572762d9ec701ca1e80401f5fed03)，对照基线 `ba5ad14`（其生产实现为上一轮 `1c6af37`）。系统字体控件 **0.793→0.694 ms/帧**；连续更新 ImageBrush **7.553→1.681 ms/帧**，该压力场景 GC 后 Private **58.6→27.9 MiB**。普通控件内存没有改善，原版画质/API 缺口与白屏仍需继续处理。

**尚未发布新 NuGet。** 公开的 preview.5 不包含本轮及前两轮内存/启动改进。没有用 CPU 成绩替代 GPU 成绩，也没有改变画质阈值。

公开原始数据：[逐进程 JSON、SHA、CI](evidence/optimization-round6-2026-10-22.json) · [CPU CSV](evidence/optimization-round6-2026-10-22-cpu.csv) · [窗口 CSV](evidence/optimization-round6-2026-10-22-window.csv)。

最终控件截图：[SimdSkia](evidence/optimization-round6-2026-10-22-images/controls-managed.png) · [原版](evidence/optimization-round6-2026-10-22-images/controls-original.png) · [差异图](evidence/optimization-round6-2026-10-22-images/controls-difference.png)。连续换图：[SimdSkia](evidence/optimization-round6-2026-10-22-images/image-brush-managed.png) · [原版](evidence/optimization-round6-2026-10-22-images/image-brush-original.png)。PNG 压缩大小不用于性能/内存比较，像素比较使用解码后 RGBA。

## 实际修复

1. 双线性缩放增加 x86 SSE4.1 四颜色通道并行路径，保持原浮点运算顺序、ties-to-even 舍入、预乘 Alpha 限制及格式转换；不使用 FMA 改变舍入。直通 Alpha、不支持的指令集和显式禁用 SIMD 时使用原标量路径。图片内部恒定裁剪段复用向量化扫描。
2. 抗锯齿 1×1/16×16 网格的非负计数除法与取模改为精确移位/掩码；采样点数、填充规则、浮点求交和边缘精度不变。
3. ImageBrush 缓存使用弱关联身份，不再通过键持有原图片或视觉树；源位图版本更新后释放不可再次命中的旧缓存项。已有 shader lease 独立持有像素，淘汰不会提前释放它们。
4. 最近邻、不透明、整数平移的画刷按行复制，先验证整行不透明再写目标；分数 DPI、旋转/缩放、透明、过滤、混合和裁剪等不满足条件的情况保留通用路径。不是把所有画刷当作不透明。
5. 重复矩形绘制复用几何身份，转路径时启用紧凑覆盖段；避免每帧创建新路径并让 LRU 保留几十张相同的整块覆盖图。新增热身后 100 次重绘的分配上限回归。
6. 增加连续换图原版对照工具；578 项双线性/标量检查、1,281 项画刷行采样检查、84 项缓存更新与生命周期检查。没有新增第三方或原生绘图库依赖。

## 测量条件

Windows x64，i7-8700K / 12 逻辑处理器，GTX 1050 Ti；Avalonia 12.1.4、SkiaSharp 3.119.4。CPU 控件为 .NET 8.0.32 Release JIT，实际窗口为 .NET 10.0.13 JIT；原版在独立进程。正式测量串行进行，不同时运行 profiler 或构建。轮换进程顺序，每配置 3 个进程；启动每配置 5 次。没有清空系统/驱动缓存，不是冷开机测量。
系统字体双方明确选 Microsoft YaHei UI，并在计时后核对 glyf/cmap/head/hmtx/OS/2 表 SHA。固定文件为相同 ZCOOLXiaoWei-Regular.ttf。普通控件预热 2.5 秒、测量 600 帧，48 进程合计 28,800 帧。原版默认与完整 opacity 组分别列出。
CPU 时间统计整个进程；满速离屏循环不能当作固定帧率 CPU 占用。窗口机器 CPU%=进程 CPU 时间/墙钟时间/12。WS、Private 是进程内存，GC 后托管堆不包含原版原生分配，显存未计入。表中为每进程指标的中位数；原始范围、P95/P99、GC 和首绘均在 JSON。

## CPU：真实 Fluent 控件

系统字体：

| 场景 / DPI | 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | Private MiB | GC 后托管 MiB |
|---|---|---:|---:|---:|---:|---:|---:|
| controls / 1 | 优化前 | 0.793 | 0.807 | 5.67 | 92.2 | 46.1 | 14.9 |
| controls / 1 | 优化后 | 0.694 | 0.703 | 5.67 | 94.9 | 48.6 | 14.9 |
| controls / 1 | 原版默认 | 0.910 | 0.911 | 7.77 | 88.3 | 42.7 | 2.6 |
| controls / 1 | 原版完整组 | 3.293 | 3.307 | 7.94 | 88.1 | 42.8 | 2.6 |

本轮系统字体耗时下降 **12.5%**。GC 后托管保留量约 14.9 MiB，基本不变；WS/Private 本次中位数反而分别增加 2.7/2.6 MiB，普通页面内存问题尚未解决。原版整页画质仍 FAIL，不能由这张表宣称整体胜过原版。

固定文件字体：

| 场景 / DPI | 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | Private MiB | GC 后托管 MiB |
|---|---|---:|---:|---:|---:|---:|---:|
| controls / 1 | 优化前 | 0.749 | 0.755 | 5.67 | 110.2 | 69.9 | 20.0 |
| controls / 1 | 优化后 | 0.650 | 0.651 | 5.67 | 109.9 | 69.6 | 20.0 |
| controls / 1 | 原版默认 | 0.895 | 0.885 | 7.77 | 96.1 | 51.1 | 2.3 |
| controls / 1 | 原版完整组 | 3.309 | 3.307 | 7.94 | 95.8 | 50.9 | 2.3 |
| controls / 1.5 | 优化前 | 1.761 | 1.771 | 5.67 | 112.3 | 72.1 | 30.5 |
| controls / 1.5 | 优化后 | 1.494 | 1.484 | 5.67 | 114.3 | 73.8 | 30.6 |
| controls / 1.5 | 原版默认 | 1.686 | 1.667 | 7.77 | 99.9 | 54.8 | 2.3 |
| controls / 1.5 | 原版完整组 | 6.978 | 6.953 | 7.94 | 98.4 | 53.5 | 2.3 |
| scroll / 1 | 优化前 | 0.401 | 0.417 | 3.22 | 95.8 | 62.1 | 15.8 |
| scroll / 1 | 优化后 | 0.407 | 0.443 | 3.22 | 96.0 | 62.2 | 15.8 |
| scroll / 1 | 原版默认 | 0.391 | 0.443 | 8.03 | 86.6 | 47.9 | 1.4 |
| scroll / 1 | 原版完整组 | 0.388 | 0.417 | 8.03 | 86.6 | 47.8 | 1.4 |

固定字体标准页面和高 DPI 耗时分别下降约 13.2% 和 15.2%；滚动耗时约增加 1.5%，没有改善。高 DPI 的进程内存也没有下降。三轮结果只能支持这些具体场景的判断，不能推导整个业务应用的收益。

## 连续换图：速度和内存

使用默认系统字体配置，负载不绘文字；不注册未使用的控件字体资源。真实 WriteableBitmap 512×512，8 次预热，160 次更新源像素并通过 ImageBrush 绘制到 320×240；每次均做真实复制、版本失效、绘制。新旧使用相同测试宿主，只替换引擎 DLL；与原版也使用相同公开 Avalonia 调用。该场景专门覆盖频繁更新图片，不代表整个应用。此处预热按次数，计时较短，不能作为充分预热后的长期稳态吞吐。WS/Private 在相同完整 GC 后采样，不是计时阶段峰值；托管保留量也不能代替包含原生资源的总内存。

| 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | Private MiB | GC 后托管 MiB |
|---|---:|---:|---:|---:|---:|---:|
| 优化前 | 7.553 | 8.984 | 178.18 | 89.9 | 58.6 | 24.0 |
| 优化后 | 1.681 | 1.855 | 1.90 | 57.8 | 27.9 | 3.5 |
| 原版 | 0.729 | 0.684 | 3.95 | 54.5 | 26.5 | 1.6 |

耗时下降 **77.7%**，托管保留量减少 **20.51 MiB**。三轮最终 RGBA 与优化前、原版均逐字节相同。仍约为原版 **2.31 倍耗时**；托管分配更少，但 WS/Private 仍略高。托管分配不包含原版内部原生分配。该短压力循环含运行时后台工作，CPU ms/帧可能超过墙钟 ms/帧。

## 实际 CPU / GPU 窗口

同一动态控件、系统字体、1000×720、100% DPI，预热 8 秒、测量至少 8 秒/60 帧；默认 JIT 设置。窗口移至屏幕外，统计服务时间，不是显示器 FPS。Direct2D 等待 GPU event 并走 DXGI 交换链；Vulkan 等待 fence、回读到平台 framebuffer。GPU 字形图集、软件层仍有 CPU 工作；不支持命令回退计数均为 0。

| 后端 | 实现 | P50 ms | P95 ms | CPU 机器% | 峰值 WS MiB | 峰值 Private MiB | KiB/帧 |
|---|---|---:|---:|---:|---:|---:|---:|
| cpu | 优化前 | 0.763 | 1.037 | 0.651 | 102.5 | 45.7 | 17.58 |
| cpu | 优化后 | 0.744 | 1.072 | 0.667 | 102.3 | 45.4 | 17.55 |
| direct2d | 优化前 | 1.301 | 1.545 | 0.924 | 125.4 | 95.4 | 14.38 |
| direct2d | 优化后 | 1.292 | 1.513 | 0.958 | 125.9 | 96.1 | 14.38 |
| vulkan | 优化前 | 2.491 | 3.726 | 0.796 | 141.3 | 120.7 | 16.38 |
| vulkan | 优化后 | 2.489 | 4.263 | 0.878 | 141.6 | 122.2 | 16.34 |

真实窗口 CPU/Direct2D/Vulkan 的 P50 分别仅变化约 −2.5%/−0.7%/−0.1%，不能把离屏控件的收益等比例套到窗口。Vulkan 的 P95 由 3.726 增至 4.264 ms、进程 CPU 中位数由 0.796% 增至 0.878%，峰值 Private 增加约 1.5 MiB；本轮不能声称改善了 Vulkan 的整体性能。

GPU 这一节只有本实现优化前后，没有新测原版 GPU；不得拿上面的原版 CPU 数字判定 GPU 胜负。逐进程波动保留，8 秒也不代表长时间业务负载。GPU 时钟未单独记录，不能把变化全部归因于驱动/JIT。

## 启动与白屏

35 次真实可见 Win32 启动，外部单次 BitBlt 屏幕读回观察相同蓝色边框；没有延迟 Show、隐藏窗口、启动遮罩或假首帧。白色阶段是离散采样估计，通常约 31 ms 一次，包含 DWM 动画和读回误差；不能解释为精确到毫秒的连续白屏时间。

| 实现 | 启动→内容 ms | 可见→内容 ms | 白色阶段估计 ms |
|---|---:|---:|---:|
| before-cpu | 1213 | 291 | 249 |
| before-direct2d | 1354 | 447 | 375 |
| before-vulkan | 1415 | 512 | 468 |
| after-cpu | 1212 | 299 | 248 |
| after-direct2d | 1366 | 455 | 403 |
| after-vulkan | 1433 | 522 | 469 |
| reference | 1038 | 164 | 93 |

原版在此环境也有约 93 ms 的白色阶段；SimdSkia CPU 约 248 ms，优化前约 249 ms，本轮未实质缩短。Direct2D/Vulkan 的白色阶段更长，且此次 Direct2D 结果比优化前多一个约 31 ms 的采样间隔。没有消除启动白屏，不能将计时波动视作修复。这里测的是仓库共享场景及 JIT，未测业务程序或 AOT 启动时长。

## 回归与剩余差距

Release 全解决方案构建 0 警告/0 错误；8665 优化、9664 控件及生命周期检查通过，完全关闭硬件指令也通过；Direct2D 344、Vulkan 540、GPU 采样 590、GPU 集成 12174、字形批次 60、平台字体 36 项通过。12 对控件帧、3 对换图帧、6 对窗口/呈现截图与优化前逐字节相同。

原版功能探针 **82/114**。整页标准/高 DPI/滚动及系统字体画质仍失败，阈值维持 max≤32、RMSE≤2；旧图像 108/120，SK* API 425/3235。`verify.ps1 -Reference -Gpu` 仍返回 **2**，完整兼容验收未通过。

| 原版功能探针 | 通过 / 总数 |
|---|---:|
| ImageBrush | 38 / 40 |
| VisualBrush | 5 / 10 |
| Blur | 7 / 8 |
| DropShadow | 8 / 8 |
| BoxShadow | 24 / 48 |

仍需处理的重点是普通控件的托管保留量、VisualBrush/BoxShadow 画质、Vulkan 回读与呈现成本，以及首次内容出现前的初始化。此次没有把这些缺口计入已完成清单。

三平台及 AOT CI 记录：

- [Windows / Linux / macOS managed and scalar](https://github.com/hnlyf168/SimdSkia/actions/runs/39771031055)：success；源码 6c017e7。
- [Three-platform JIT / Native AOT / nupkg windows](https://github.com/hnlyf168/SimdSkia/actions/runs/39771031167)：success；源码 6c017e7。
- [Windows original and managed Native AOT](https://github.com/hnlyf168/SimdSkia/actions/runs/39771031036)：success；源码 6c017e7。

CI 是功能与包消费验证，不是云机器性能成绩。

## 构建和复现

```powershell
dotnet build SimdSkia.sln -c Release
powershell -NoProfile -File tools/verify.ps1 -Reference -Gpu
python tools/run-system-font-comparison.py --family "Microsoft YaHei UI" --output artifacts/new-system --runs 3
powershell -NoProfile -File tools/run-avalonia-controls.ps1 -Output artifacts/new-fixed -Frames 600 -Forks 3
python tools/run-tile-cache-comparison.py --output artifacts/new-tile --before-directory artifacts/frozen-old-engine-with-current-host --runs 3 --system-fonts
python tools/run-desktop-benchmarks.py --output artifacts/new-window --system-fonts --runs 3 --warmup-seconds 8 --measurement-seconds 8
dotnet build tests/Startup/Managed -c Release
dotnet build tests/Startup/Reference -c Release
python tools/run-startup-comparison.py --output artifacts/new-startup --runs 5 --backends cpu direct2d vulkan
```

优化前后比较需先单独构建并冻结基线 DLL，传入 `--before-directory` / `-BeforeDirectory` / `--before-executable` / `--before`；换图压力测试把当前 AvaloniaControls 主程序放到冻结引擎目录，保留其原依赖和运行时配置，避免重新构建覆盖引擎。测试脚本记录并复核全部 DLL 的 SHA。
本机原始结果位于 `artifacts/optimization-round6`，开发中间试跑不作为最终成绩。磁盘不足时原始 RGBA/诊断堆无损归档，逐文件 SHA 校验后才移除未压缩副本；报告公开 JSON/CSV、最终图片和所有失败项保持可查看。
