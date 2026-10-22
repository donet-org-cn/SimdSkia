# 2026-10-22：Avalonia 内存与分配专项优化

后续字体分页、启动白屏和 CPU 裁剪优化见 [新一轮报告](STARTUP-PERFORMANCE-2026-10-22.md)。本页保留上一轮数据。因本机磁盘空间有限，本页原始大体积 RGBA 和诊断 dump 已无损归档至 `artifacts/memory-round4/raw-evidence.zip`，逐项 SHA256 见 `raw-archive-manifest.json`；在该目录解压后可重跑旧像素报告，其余数据保留原位置。

本轮最大的收益是**系统字体窗口的内存**：CPU 工作集 252.6→116.0 MiB，减少 54.1%；Private 197.6→60.6 MiB，减少 69.3%。Direct2D/Vulkan 同样减少约 137 MiB 工作集。固定字体的 CPU 控件平均耗时下降 5.2%，高 DPI 下降 9.2%，每帧分配减少 66.1%/69.5%。滚动平均耗时没有改善，GPU 帧耗时基本不变，不能宣称所有场景都更快。

与原版仍有差距：相同系统字体的 CPU 控件为 0.999 vs 0.906 ms/帧，工作集 111.8 vs 88.4 MiB。整页画质仍未通过原版阈值，因此这些是相同输入下的工程测量，**不是已经通过等画质验收的胜负结论**。

源码优化提交：[cd9f5d8](https://github.com/hnlyf168/SimdSkia/commit/cd9f5d84121f3c016b394b9e50f8fb6c70bc16a7)；优化前为 `2b6fc5c`，绘图代码与已发布 preview.5 一致。**本次没有发布新 NuGet；已公开的 `0.1.0-preview.5` 不包含这些新优化。**

原始逐进程指标、帧样本、运行文件 SHA256、字体表审计与 CI 窗口记录保存在 [JSON 证据](evidence/memory-performance-2026-10-22.json)；便于筛选的 [CSV](evidence/memory-performance-2026-10-22.csv) 明确区分 CPU 离屏与实际窗口口径。

## 实现与内存归因

使用 `dotnet-dump 10.0.750501` 对独立诊断进程完整 GC 后取堆，`dumpheap -stat`/`gcroot` 定位到字体数组、平台/离屏位图和复用图层。固定字体场景的 `byte[]` 共 18,182,247 字节，主要大对象是约 6.0 MiB 字体和四块约 2.75 MiB 像素缓冲。统计中的 `Free` 不算存活对象；也不能把仍供窗口或离屏绘制使用的缓冲都称为泄漏。该诊断没有进入任何正式计时。

本轮做了以下实际改动：

1. 系统字体先读家族、字重、样式等元数据，首次使用某个字面时才加载字形数据；枚举家族和样式不会加载所有回退字体。缺字匹配仍按原顺序查找，遇到需要的回退字体才加载。
2. 同一个 TTC 的多个字面共享文件快照，直接按原始表偏移解析；只有调用字体流接口时才导出独立 SFNT。字体摘要改为首次去重需要时计算。公共 `TrueTypeFont(ReadOnlySpan<byte>)` 仍复制输入，避免调用方修改原始内存。
3. 位图与 CPU 窗口复用光栅器及扫描线缓冲。重叠绘图上下文仍独立；位图先 Dispose、上下文后 Dispose 的像素租约行为保持原样。
4. 渐变快照用弱键缓存，逐项检查颜色站点、位置、变换、中心、半径和目标尺寸；透明度每次校验，修改后重新生成。已加入连续修改渐变的逐字节回归。
5. 复用内部裁剪/图层栈，最多保留 8 份；超大栈不进入池，归还时清空引用。公开绘图上下文不会被回收再用，已 Dispose 的对象不会在下一帧恢复有效。

没有更换字体、关闭抗锯齿、删掉颜色处理或减少绘图功能。系统字体模式把完整 glyf/loca 等校验推迟到字面首次使用；显式 `FontFiles` 仍在注册时完整校验。文件在发现与使用之间改变家族/样式时明确报错。变量字体、CFF、彩色字体等既有不支持项没有冒充实现。

缓存仍有成本：全局像素池最多 32 MiB/4 块；每个保留的光栅器仍受原覆盖率 16 MiB、渐变行 4 MiB 等上限约束，Dispose 会释放引用。大量同时存活的渲染目标可能累积各自缓存；本轮没有消除所有 framebuffer 暂存或把字体改为操作系统文件映射。

## 测量条件

- 同一 Windows x64 / Core i7-8700K（12 逻辑处理器）/ GTX 1050 Ti，Avalonia 12.1.4，原版 SkiaSharp 3.119.4；Release JIT，未使用 profiler、逐帧截图或串行 GPU 时间戳诊断。
- CPU：.NET 8.0.32，真实 Fluent 控件模板与布局，`RenderTargetBitmap`；预热 2.5 秒，每进程 600 帧，3 轮轮换顺序。固定字体 36 进程、系统字体 9 进程，共 27,000 帧。此表不含窗口呈现时间。
- 窗口：.NET 10.0.13，1000×720、100% DPI，真实 Win32 窗口。固定字体/系统字体 × 优化前后 × CPU/Direct2D/Vulkan × 3 进程，共 36 进程。预热 2 秒，观测至少 3 秒/60 帧。窗口移出屏幕，测绘制到完成的服务时间，不代表显示器 FPS。
- GPU：Direct2D 包含 GPU 完成事件和 DXGI 呈现；Vulkan 包含 fence、像素回读及平台 framebuffer。GPU 字形图集构建、软件层仍有 CPU 工作；本轮窗口未触发不支持命令的回退，但不等于全部操作都在 GPU。
- 表格为各进程指标的中位数；工作集/Private 是实际进程值，GPU 显存未计入。CPU 全机占用 = 进程 CPU 时间 / 墙钟时间 / 12。托管分配/GC 后堆只统计 .NET 内存，原版原生分配不在其中，不能据此比较总 RAM。

固定字体为同一 ZCOOLXiaoWei-Regular.ttf；系统字体两边明确选择 Microsoft YaHei UI。CPU 原版/托管进程的 `glyf/cmap/head/hmtx/OS/2` 五张实际字形/度量表 SHA256 全部相同。两种字体测试独立列示，不能横向用字体差异解释引擎收益。

## CPU 与原版比较

固定字体：

| 场景 / DPI | 实现 | 平均 ms/帧 | CPU ms/帧 | 分配 KiB/帧 | 工作集 MiB | Private MiB | GC 后托管 MiB |
|---|---|---:|---:|---:|---:|---:|---:|
| controls / 1 | 优化后 | 0.951 | 0.964 | 5.67 | 110.3 | 70.1 | 20.1 |
| controls / 1 | 优化前 | 1.003 | 1.016 | 16.76 | 119.2 | 78.7 | 20.0 |
| controls / 1 | 原版默认透明度 | 0.891 | 0.885 | 7.77 | 96.1 | 51.0 | 2.3 |
| controls / 1 | 原版完整透明度组 | 3.329 | 3.333 | 7.94 | 95.7 | 51.0 | 2.3 |
| controls / 1.5 | 优化后 | 2.060 | 2.057 | 5.67 | 114.7 | 74.4 | 30.7 |
| controls / 1.5 | 优化前 | 2.270 | 2.266 | 18.62 | 129.7 | 89.3 | 30.6 |
| controls / 1.5 | 原版默认透明度 | 1.722 | 1.719 | 7.77 | 99.9 | 54.7 | 2.3 |
| controls / 1.5 | 原版完整透明度组 | 7.049 | 7.057 | 7.94 | 98.4 | 53.6 | 2.3 |
| scroll / 1 | 优化后 | 0.422 | 0.469 | 3.22 | 96.1 | 62.4 | 15.9 |
| scroll / 1 | 优化前 | 0.408 | 0.469 | 4.63 | 103.0 | 69.5 | 15.9 |
| scroll / 1 | 原版默认透明度 | 0.388 | 0.417 | 8.03 | 86.6 | 47.9 | 1.4 |
| scroll / 1 | 原版完整透明度组 | 0.376 | 0.417 | 8.03 | 86.5 | 47.8 | 1.4 |

标准控件平均耗时下降 5.2%，高 DPI 下降 9.2%；滚动平均耗时反而上升 3.3%，P50 从 0.396 降至 0.382 ms，CPU ms/帧相同。滚动存在尾部波动，不能称为稳定提速。优化后相对原版默认的平均耗时差距分别约 6.7%、19.6%、8.8%。

原版“完整透明度组”在重叠透明内容上的行为更接近本实现，但与原版默认成本明显不同。两种模式都保留，评价实际替换时应首先看用户默认配置，不能只挑较慢的完整组作对手。

系统字体（同一微软雅黑 UI 字面）：

| 场景 / DPI | 实现 | 平均 ms/帧 | CPU ms/帧 | 分配 KiB/帧 | 工作集 MiB | Private MiB | GC 后托管 MiB |
|---|---|---:|---:|---:|---:|---:|---:|
| controls / 1 | 优化后 | 0.999 | 1.016 | 5.67 | 111.8 | 66.8 | 32.8 |
| controls / 1 | 原版默认透明度 | 0.906 | 0.911 | 7.77 | 88.4 | 42.8 | 2.6 |
| controls / 1 | 原版完整透明度组 | 3.303 | 3.281 | 7.94 | 88.2 | 42.9 | 2.6 |

优化后仍比原版默认慢约 10.3%，工作集多 23.4 MiB、Private 多 24.0 MiB。首次离屏渲染中位数 19.8 vs 6.1 ms；它是第一次 `Render` 调用，**不包含完整应用启动时间**。托管堆 32.8 vs 2.6 MiB 的差异不能直接当成总内存差，因为原版字体与像素还占用原生内存。

## 实际 CPU/GPU 窗口

系统字体：

| 后端 | 实现 | P50 ms | P95 ms | CPU 全机 % | 工作集峰值 MiB | Private 峰值 MiB | 分配 KiB/帧 |
|---|---|---:|---:|---:|---:|---:|---:|
| cpu | 优化前 | 0.765 | 1.050 | 2.73 | 252.6 | 197.6 | 20.21 |
| cpu | 优化后 | 0.762 | 1.131 | 2.60 | 116.0 | 60.6 | 18.87 |
| direct2d | 优化前 | 0.734 | 1.004 | 3.34 | 279.0 | 250.4 | 16.53 |
| direct2d | 优化后 | 0.750 | 1.018 | 3.45 | 141.5 | 114.5 | 15.30 |
| vulkan | 优化前 | 1.440 | 1.737 | 3.07 | 293.4 | 275.9 | 17.29 |
| vulkan | 优化后 | 1.438 | 1.823 | 3.64 | 156.2 | 138.2 | 17.00 |

CPU/Direct2D/Vulkan 工作集分别减少 54.1%/49.3%/46.8%；Private 分别减少 69.3%/54.3%/49.9%。这是本轮主要收益。CPU P50 基本不变，Direct2D P50 略增约 2.1%，Vulkan 基本不变；P95 也没有全面改善。

固定字体，保留与前轮相同输入：

| 后端 | 实现 | P50 ms | P95 ms | CPU 全机 % | 工作集峰值 MiB | Private 峰值 MiB | 分配 KiB/帧 |
|---|---|---:|---:|---:|---:|---:|---:|
| cpu | 优化前 | 2.023 | 2.626 | 3.76 | 112.4 | 57.4 | 28.29 |
| cpu | 优化后 | 1.946 | 2.649 | 3.86 | 102.1 | 47.8 | 21.42 |
| direct2d | 优化前 | 1.625 | 2.098 | 4.37 | 137.3 | 107.5 | 21.65 |
| direct2d | 优化后 | 1.577 | 2.067 | 4.06 | 129.9 | 100.8 | 19.48 |
| vulkan | 优化前 | 2.297 | 2.598 | 3.61 | 154.8 | 138.5 | 23.34 |
| vulkan | 优化后 | 2.291 | 2.746 | 4.78 | 148.1 | 133.4 | 21.25 |

固定字体窗口工作集分别减少约 10.3/7.5/6.7 MiB。CPU 与 Direct2D P50 小幅改善；Vulkan 仍比当前 CPU 慢约 17.8%，本轮没有获得新的 GPU 吞吐跃升。原版 GPU 的旧对照见 [Vulkan 专项报告](VULKAN-OPTIMIZATION-2026-10-22.md)，本轮没有重跑原版 GPU，不能把旧值当作本轮新成绩。

### CPU 占用诊断与限制

默认配置下，固定字体 Vulkan 的全机 CPU 中位数从 3.61% 升到 4.78%；系统字体从 3.07% 升到 3.64%。这些不利结果照录。2 秒预热的真实窗口仍可能包含后台分层 JIT/运行时启动成本，所以另做了**双方同时 `DOTNET_TieredCompilation=0`** 的 6 进程诊断：Vulkan P50 2.297→2.307 ms，CPU 中位数 0.560%→0.519%。优化后单次 CPU 范围仍有 0.30%–2.03%，并不稳定。

编译配置改变后的 CPU 差异说明短窗口观测对运行时行为很敏感；它不能证明所有默认配置回归都由 JIT 引起，也不能替换上面的正式成绩。本次不修改生产应用的运行时设置，不宣称 GPU 长期 CPU 占用已经降低。稳定长期 CPU/AOT 吞吐需要更长观测及独立原版 GPU 对照；本轮 AOT 仅做功能验收。

## 功能、像素与跨平台验收

| 检查 | 结果 |
|---|---|
| 完整 Release 构建 | 0 警告、0 错误 |
| 基础行为 / 独立图片 / 标量一致性优化 | 36 / 484 / 7845 通过 |
| Avalonia 控件、字形、渐变修改、Dispose/重叠上下文 | 8245 通过 |
| CPU / Vulkan Avalonia 文字链 | 595 / 599 通过 |
| 平台字体 / Windows 系统字体 / 显式字体 | 23 / 13 / 10 通过，含首次使用、字重、中文和文件变化拒绝 |
| GPU 采样 / Vulkan 内核优化 | 590 / 540 通过 |
| Direct2D / Avalonia GPU 集成 / 字形合批 | 344 / 12174 / 60 通过 |
| 三场景三轮 CPU 优化前后帧 | 9/9 对逐字节相同 |
| 两字体 × 三后端窗口及实际呈现截图 | 12/12 对逐字节相同 |
| 原版固定字体整页阈值 | 0/3，仍 FAIL |
| 原版系统字体整页阈值 | 0/1，仍 FAIL |
| SK* 公共 API | 425/3235；本轮没有宣称完整兼容 |

原版固定字体三场景最大误差 96/117/75，RMSE 2.408/2.321/2.595；系统字体最大误差 96、RMSE 3.359。阈值仍为最大通道误差 ≤32、RMSE ≤2，没有放宽。旧中文/路径 AA 原版图像仍 108/120，完整 `verify.ps1 -Reference -Gpu` 返回 **2**，不是全绿。完整整页输出与优化前保持相同，说明此次性能收益没有引入新的像素退化。

同一优化源码的云端验收全部通过：

- [三平台托管、图片、文字、控件与关闭硬件指令回退](https://github.com/hnlyf168/SimdSkia/actions/runs/39752120807)。
- [Windows x64 / Linux x64 / macOS ARM64 的实际 JIT、Native AOT 与 nupkg 消费窗口](https://github.com/hnlyf168/SimdSkia/actions/runs/39752120567)。下载核对 9 条窗口记录，3 条 `NativeAot=True`。云端构建的包只用于验证，没有推送 NuGet.org。
- [Windows SimdSkia 与原版双实现 Native AOT](https://github.com/hnlyf168/SimdSkia/actions/runs/39752120609)。

性能数字只来自上述本机 Windows 测量，不能外推 macOS/Linux 性能。

## 构建与复现

```powershell
dotnet build SimdSkia.sln -c Release
powershell -NoProfile -File tools/verify.ps1 -Reference -Gpu
dotnet run --project tests/PlatformFonts -c Release
dotnet run --project tests/WindowsSystemFonts -c Release
dotnet run --project tests/Direct2D -c Release
dotnet run --project tests/AvaloniaGpu -c Release
dotnet run --project examples/CrossPlatform -c Release -- --smoke --controls
```

CPU 测量（`verify` 的既有画质失败应单独处理，不能吞掉返回码）：

```powershell
powershell -NoProfile -File tools/run-avalonia-controls.ps1 -Output artifacts/my-cpu-comparison -Frames 600 -Forks 3
python tools/run-system-font-comparison.py --family "Microsoft YaHei UI" --output artifacts/my-system-font-comparison --runs 3
```

窗口测量：

```powershell
dotnet build examples/CrossPlatform -c Release
python tools/run-desktop-benchmarks.py --output artifacts/my-window-fixed --runs 3
python tools/run-desktop-benchmarks.py --output artifacts/my-window-system --system-fonts --runs 3
```

若重现优化前后，在独立 checkout 编译 `2b6fc5c`，冻结对应输出目录；CPU 使用 `-BeforeDirectory`，窗口使用 `--before-executable`。不要在测量期间重建被测 DLL。各轮目录必须全新，以保存已有证据。本机本轮原始日志、dump、像素与差异图在 `artifacts/memory-round4`。

后续主要差距是固定字体常驻像素/文件内存、复杂场景高 DPI 开销、GPU 回读/呈现成本，以及既有 hinting/复杂排版/曲线与完整 API 兼容性。当前结果支持“显著减少系统字体场景的内存浪费”，尚不支持“全面追平或替代原版”。
