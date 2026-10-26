# 核心 CPU 采样优化与原版对比：2026-10-26

此为上一轮性能快照。随后已修复本文两类半透明图像的合成舍入差异，原门槛下核心图片 **24/24** 通过，见[后续修复与最终复测](ALPHA-COMPATIBILITY-2026-10-26.md)。以下保留当时的原始时间和失败项。

**图片缩放热点已在部分同画质场景达到原版水平或略快；整体 Avalonia 替换仍未完成。** 1024×768 非预乘不透明图片从 46.317 降至 7.423 ms，原版 7.830 ms；Opaque 格式从 8.815 降至 6.415 ms，原版 7.188 ms。这两个场景通过既定原版画质门槛，不能据此概括所有绘图。

本轮仅修改 CPU 核心，尚未发布 NuGet。通用 CPU 回退、SSE 路径、原版质量失败项全部保留；没有通过降低 AA、采样质量或颜色精度换取速度。

[逐进程数据、批次、内存、哈希和验证证据](evidence/core-round8-2026-10-26.json) · [核心基准](../benchmarks/Sampling/Program.cs) · [串行测量脚本](../tools/run-core-sampling-comparison.py)

## 改动

新增 AVX2 八像素批量双线性采样。此前 SSE4.1 每次处理一个像素的四个颜色通道，并把非预乘源图排除在 SIMD 之外；现在一次聚集八个目标像素的四组 texel，按通道并行处理。

非预乘像素先逐 texel 进行精确整数预乘，再过滤；保持原浮点乘加顺序和 ties-to-even 舍入，不使用 FMA。预乘 RGB 仍限制在过滤后的 Alpha 内，保留格式转换、任意 stride、源子矩形、镜像、边缘和裁剪。Opaque 元数据允许采用已经验证的不透明行合成，避免对保证不透明的行再做通用混合。

指针只用于局部固定的源行和 AVX2 gather；索引由前置映射夹紧在源范围内，不保存指针。尾部像素继续用原 SSE/标量内核；不支持 AVX2 或禁用 SIMD 时回退，不新增原生组件或公开 API。

初版把新条件放入单像素 SSE loader，导致预乘路径退化；该候选已排除。最终数据来自 `artifacts/core-round8/final`，初版 `comparison` 与调试测量不混入结果。

## 条件与口径

同机 Intel i7-8700K、12 逻辑处理器、Windows x64、.NET 8.0.32 Release JIT。固定 SkiaSharp 3.119.4，原版仅用于隔离对照进程；Avalonia 固定 12.1.4。显式关闭 tiered compilation / ReadyToRun 的核心基准与 Avalonia 控件测量分开，不把两套环境的时间拼接。

核心基准以相同 512×384 像素、Alpha 元数据、清屏和线性过滤输出 320×240 / 1024×768。前版本、后版本、标量、原版各 3 个独立进程，每组 7 个约 120 ms 批次，合计 96 场景进程记录 / 672 批。轮换顺序，全部串行运行，不与构建、profiler 或其他基准并行。表格为每进程批次中位数再取三进程中位数。

CPU 时间来自进程 TotalProcessorTime，包含整个进程；不是固定帧率 CPU 占用。内存是完整进程，不是纯内核缓冲。核心原版 worker 还持有公共托管 fixture 和原生缓冲，**其内存不用于引擎间比较**；公平的原版内存对照列在真实 Avalonia 表。核心分配计数包含每批计时器/采样开销，迭代数变化会改变折算值，不能把它解释成库分配优化。

## 核心线性采样：1024×768

| 源格式 / Alpha | 优化前 ms | 优化后 ms | 原版 ms | 前后加速 | 原版画质 |
|---|---:|---:|---:|---:|---|
| 非预乘，像素全不透明 | 46.317 | 7.423 | 7.830 | 6.24× | 通过 |
| 非预乘，含透明 | 46.139 | 7.524 | 8.239 | 6.13× | 失败，仅诊断 |
| 预乘，含透明 | 8.043 | 6.630 | 7.568 | 1.21× | 失败，仅诊断 |
| Opaque 元数据 | 8.815 | 6.415 | 7.188 | 1.37× | 通过 |

相对原版，两个通过画质门槛的 1024×768 场景耗时中位数分别低约 5.2% / 10.8%。320×240 对应非预乘不透明场景为 0.749 vs 原版 0.756 ms，接近持平；Opaque 为 0.631 vs 0.682 ms。小差异不代表普遍或统计显著的胜出。

24 组原版画质检查中 12 组通过。不透明场景 1024×768 逐字节一致；320×240 最大误差 1、RMSE 约 0.136。含透明源图最大误差 2，RMSE 约 0.577–0.593，未过既定 RMSE≤0.5 门槛；门槛未放宽。**所有 24 组优化前、优化后和标量输出逐字节相同**，本轮没有引入新的这些差异，但也没有修复既有透明过滤差异。

| 源格式 / Alpha | CPU ms/帧 前→后 | Private MiB 前→后 | WS MiB 前→后 |
|---|---:|---:|---:|
| 非预乘，像素全不透明 | 46.875→7.292 | 20.8→20.7 | 41.4→41.4 |
| 非预乘，含透明 | 46.875→7.812 | 16.3→16.0 | 36.6→36.6 |
| 预乘，含透明 | 8.333→6.434 | 15.7→15.7 | 36.1→36.2 |
| Opaque 元数据 | 8.413→6.076 | 15.9→16.0 | 36.2→36.4 |

没有新增持久缓存，也没有获得一致的常驻内存下降。完整原始范围和全部标量数据保留在 JSON；首个进程往往更快，未挑选最快值替代中位数。

## 实际 Avalonia 控件：CPU

36 个进程，各预热 2.5 秒、绘制 600 帧；相同 Fluent 控件、固定 ZCOOL 字体和尺寸，轮换运行。此处计时 RenderTargetBitmap + ImmediateRenderer，不含窗口呈现或 GPU；原版默认透明度策略与显式完整透明组分别列出，不用较慢的一列代表原版常规行为。

| 场景 / DPI | 实现 | ms/帧 | CPU ms/帧 | KiB/帧 | WS MiB | Private MiB | 保留托管 MiB |
|---|---|---:|---:|---:|---:|---:|---:|
| controls / 1 | 优化前 | 0.701 | 0.729 | 5.67 | 108.1 | 67.8 | 20.0 |
| controls / 1 | 优化后 | 0.666 | 0.677 | 5.67 | 108.1 | 67.3 | 19.9 |
| controls / 1 | 原版默认 | 1.072 | 1.068 | 7.77 | 96.3 | 51.1 | 2.3 |
| controls / 1 | 原版完整透明组 | 3.973 | 3.906 | 7.94 | 95.1 | 50.3 | 2.3 |
| controls / 1.5 | 优化前 | 1.786 | 1.745 | 5.67 | 114.6 | 74.1 | 30.6 |
| controls / 1.5 | 优化后 | 1.716 | 1.719 | 5.67 | 106.4 | 65.7 | 30.6 |
| controls / 1.5 | 原版默认 | 2.383 | 2.396 | 7.77 | 100.0 | 55.0 | 2.3 |
| controls / 1.5 | 原版完整透明组 | 7.770 | 7.760 | 7.94 | 98.5 | 53.6 | 2.3 |
| scroll / 1 | 优化前 | 0.454 | 0.547 | 3.19 | 96.1 | 62.2 | 15.8 |
| scroll / 1 | 优化后 | 0.442 | 0.547 | 3.19 | 96.2 | 62.2 | 15.8 |
| scroll / 1 | 原版默认 | 0.444 | 0.521 | 8.03 | 86.9 | 48.0 | 1.4 |
| scroll / 1 | 原版完整透明组 | 0.440 | 0.547 | 8.03 | 86.8 | 48.2 | 1.4 |

普通页面耗时中位数约下降 5.0%，高 DPI 约 3.9%，滚动约 2.6%；三次进程的时间范围存在重叠，不声称显著或普遍加速。滚动 CPU 时间中位数未下降。托管保留量基本不变；高 DPI Private 快照下降约 8 MiB，但没有相应持久缓存缩减，不把进程快照波动宣称为已解决内存问题。原版实际控件 Private 仍较低。

9 对优化前后完整页面逐字节相同；原版整页仍 0/3 通过（最大误差 96 / 117 / 75，RMSE 2.408 / 2.321 / 2.595）。因此即使表格中某些控件比原版默认耗时更少，也不能认定整个 UI 已同画质领先。

[本轮控件](evidence/core-round8-2026-10-26-images/managed-controls-dpi1.png) · [原版完整组](evidence/core-round8-2026-10-26-images/reference-controls-dpi1.png) · [差异×4](evidence/core-round8-2026-10-26-images/diff-controls-dpi1.png)

## 回归与 AOT

- Release 全解决方案构建：0 警告、0 错误。
- 74,921 项逐字节优化检查通过，含全部 65,536 个颜色/Alpha 组合、AVX2 块边界、格式、stride、镜像、裁剪和物理非法 PMA 值。分别关闭 AVX2、关闭所有硬件指令后同样通过。
- 9,734 控件/生命周期、484 编解码/ICC、595 CPU 文字链、599 GPU 文字链、540 GPU 内核检查通过；25 个原版行为探针无差异。
- 真正发布并运行两份 Native AOT，各通过 66,834 项采样检查；外部确认无 CLR/JIT。通用 x64 的 AVX2/SSE4.1 为 false，验证标量回退；x86-64-v3 的 AVX2 为 true，验证批处理路径。**通用 AOT 不自动获得本轮 AVX2 性能，v3 要求相应硬件；本轮未测 AOT 持续性能。**
- `verify.ps1 -Reference -Gpu` 与独立控件画质报告均保留退出码 2；完整质量仍未通过。API 仍为 425/3235，本轮没有新增兼容 API。

GPU 本轮仅做功能回归，没有重测性能；不能把 CPU 改善用于 GPU 速度或首帧/白屏结论。macOS/ARM/Linux 的本轮性能没有实测。

## 构建与复现

```powershell
dotnet build benchmarks/Sampling -c Release
dotnet run --project tests/Optimization -c Release -- baseline/fonts/ZCOOLXiaoWei-Regular.ttf
powershell -NoProfile -File tools/verify.ps1 -Reference -Gpu
python tools/run-core-sampling-comparison.py --output artifacts/core-round8/new-comparison
powershell -NoProfile -File tools/run-avalonia-controls.ps1 -Output artifacts/core-round8/new-controls -BeforeDirectory artifacts/core-round8/before-controls -Frames 600 -Forks 3 -NoBuild
dotnet publish tests/SamplingAot -c Release -r win-x64 --self-contained true -o artifacts/new-sampling-aot
dotnet publish tests/SamplingAot -c Release -r win-x64 --self-contained true -p:IlcInstructionSet=x86-64-v3 -o artifacts/new-sampling-aot-v3
```

前后比较必须在修改前构建并冻结同一公共基准 host 及依赖：本轮保存在 `artifacts/core-round8/before-sampling` 和 `before-controls`，源版本为 cef51cf。脚本不覆盖旧结果，所有实际测量 DLL 哈希保留并重新核验；不能把当前构建复制成 before 来声称前后加速。标准 `verify` 的代码 2 表示已有质量缺口，不能当作成功或误报为程序崩溃。
