# SimdSkia

独立 C# 2D 引擎，内部使用 SimdSkia，源码兼容层使用 SkiaSharp.SK*。固定基线是 Avalonia.Skia 12.1.4 实际解析的 SkiaSharp 3.119.4。不使用 Google Skia 绘图；字体、排版和编解码都是 C#。Core/Cpu/Text/Gpu 无 NuGet 包，Imaging/Compatibility 使用固定版本纯托管 JpegLibrary；可选 Avalonia 层另有纯托管依赖，见 [许可证与依赖](docs/DEPENDENCIES.md)。

路径、抗锯齿、基本中英文 glyf TTF/TTC 字体、PNG/JPEG/部分 ICC、图像/视觉画刷、模糊/阴影、SIMD 与 Vulkan 离屏均有真实实现。**完整 Avalonia 替换尚未通过验收**：文字/曲线及部分新功能画质仍失败，通用字体排版与 Vulkan 窗口等仍有缺口。

最新交付：[测试报告](docs/TEST-REPORT-2026-10-21.md)、[CPU/内存/滤镜性能](docs/PERFORMANCE-FINAL-2026-10-21.md)、[原版 Vulkan GPU 对比](docs/PERFORMANCE-GPU-FINAL-2026-10-21.md)、[实现范围与缺口](docs/IMPLEMENTATION-DETAILS-2026-10-21.md)。报告保留未通过项，不承诺替代原版强名称程序集。

2026-10-21 的 CPU、GPU、真实 OCR、CPU 时间和内存对比见 [本轮优化报告](docs/OPTIMIZATION-2026-10-21.md)。保留优化前数据及失败的画质检查。

[真实 Avalonia 接入](docs/AVALONIA-INTEGRATION.md)：`UseSimdSkia` 已运行实际控件与 Win32 软件窗口，8109 项控件/字形/滤镜/字体集合断言通过。较早的[字形/首绘优化](docs/AVALONIA-TEXT-CACHE.md)及通用报告保留为历史数据；当前结论以本轮报告为准。

```powershell
dotnet build SimdSkia.sln -c Release
powershell -NoProfile -File tools/fetch-avalonia-font.ps1
dotnet run --project examples/AvaloniaControls -c Release
# Windows 交互窗口
dotnet run --project examples/AvaloniaControls -c Release -- --window
```

![SimdSkia 实际 Avalonia 控件](docs/images/avalonia-text-cache-managed.png)

## 构建和运行

需要 .NET 10 SDK 和 .NET 8 runtime。

    dotnet build SimdSkia.sln -c Release
    dotnet run --project tests/SimdSkia.Tests -c Release
    dotnet run --project tests/Features -c Release
    dotnet run --project tests/Avalonia -c Release
    dotnet run --project examples/AvaloniaText -c Release
    dotnet run --project tests/Gpu -c Release
    dotnet run --project examples/Features -c Release
    dotnet run --project examples/Features -c Release -- --gpu

组合示例绘制渐变、曲线、孔洞、描边、中英文和经过 PNG 编解码的图片，输出 artifacts/features-cpu.png / features-vulkan.png。GPU 实际执行覆盖率与合成，等待 fence 完成后读取像素，没有 CPU fallback 冒充 GPU。

字体须是真实 glyf TrueType 文件。Windows 示例默认使用系统 SimHei，其他环境传入字体路径：

    powershell -NoProfile -File tools/fetch-test-font.ps1
    dotnet run --project tests/Features -c Release -- baseline/fonts/ZenOldMincho-Regular.ttf
    dotnet run --project examples/Features -c Release -- baseline/fonts/ZenOldMincho-Regular.ttf --gpu

固定 OFL 测试字体含 CJK/拉丁字形；区域字形风格应使用应用自己的中文字体。没有字体时明确失败或标记字体测试未执行，不生成假字形。GPU 需要系统 Vulkan loader/驱动：Windows vulkan-1.dll，Linux libvulkan.so.1；当前为 64 位小端 RGBA/Premul 离屏目标，不提供 swapchain 或 Skia GR 句柄替换。

## 实际验收

- Release 构建 0 警告、0 错误，原有 36 项测试通过。
- 扩展功能测试带原版 PNG 夹具时 14 项通过，包含真实中文字体、换行/折行、独立面积、三类 DEFLATE 块和 SIMD 等价性。
- 150 个格式/Alpha/stride/尾部组合一致，另实际禁用硬件指令验证标量回退。
- GTX 1050 Ti 上，GPU/CPU 的矩形、AA 路径、中文、渐变、描边、图片/裁剪和批次边界逐像素一致；验证了缓存内容变化、淘汰、线程约束和 Dispose。
- 原版 120 个 CPU 像素场景中 108 达到预设阈值，12 个曲线 AA/文字场景未通过；25 个行为和字体度量探针一致。失败未隐藏或放宽阈值。
- 425/3235 条公共元数据匹配，仍有 2810 条缺口；Avalonia 引用的成员中 120/346 个存在签名，尚未完整接入，未承诺二进制兼容。Linux 尚未实机运行。

## 复现验证

    powershell -NoProfile -File tools/verify.ps1 -Gpu
    powershell -NoProfile -File tools/check-production-dependencies.ps1
    powershell -NoProfile -File tools/fetch-baseline.ps1 -IncludeWindowsReferenceRuntime
    powershell -NoProfile -File tools/verify.ps1 -Reference
    dotnet run --project tests/Features -c Release -- --reference-pngs
    dotnet run --project tools/CpuBench -c Release
    dotnet run --project tests/ReferenceRenderer -c Release -- --benchmark

-Reference 当前因上述 12 个像素差异非零退出，查看 artifacts/diff/results.json 和差异图。原版只在隔离对照进程。测试使用可执行断言程序，不应将 dotnet test 的无测试输出当作通过。

项目附 GLSL 和 SPIR-V，普通构建不需要 shader 编译器。更改 shader 后使用 tools/build-shader.ps1 -Compiler <glslang路径>；编译器仅供开发。

Git 仓库保留源码、测试夹具和文档。`baseline/`、`artifacts/`、`results/`、构建缓存及 OCR 本机配置不上传；报告中的这些路径指本机原始证据，可按所列命令重新生成。当前项目尚未选择发布授权许可证，依赖许可证单独记录在下方文档中。

[进度](docs/PROGRESS.md) · [兼容报告](docs/COMPATIBILITY.md) · [验证与性能](docs/VALIDATION.md) · [架构](docs/ARCHITECTURE.md) · [依赖与许可证](docs/DEPENDENCIES.md)

## 原版性能对比

[多场景性能报告](docs/PERFORMANCE.md) 已实测原版/标量/SIMD：12 场景、两尺寸、每实现 3 个独立进程及每场景 7 批采样，包含逐帧 P95、托管分配、GC、进程内存、CPU 使用率、PNG 大小和像素 gate。画质未通过的场景只作诊断。随后实际优化引擎并复测，见 [优化前后实测](docs/OPTIMIZATION.md)；旧结果、失败门槛和像素证据保留。

[GPU(Vulkan)对比](docs/PERFORMANCE-GPU.md) 用同一台 GTX 1050 Ti 实测原版 SkiaSharp Vulkan 光栅后端与 SimdSkia compute 管线：每帧完整 round-trip（绘制+提交+等待+回读）计时，含托管分配、进程内存和 CPU 使用率；未过像素 gate 的场景只作诊断。

复现时指定新的输出目录，以保留旧数据：

    powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-run
    powershell -NoProfile -File tools/run-gpu-benchmarks.ps1 -Output artifacts/performance-gpu/my-run

CPU 光栅结果见 PERFORMANCE.md；GPU(Vulkan) round-trip 对照见 PERFORMANCE-GPU.md，二者不混用。原版依赖仍仅在 benchmarks/Reference 与 benchmarks/GpuReference，benchmark 项目不属于生产引用图。

CPU 优化包括连续像素 SIMD 合成、AA 矩形覆盖、路径活动边与区间计数、有界布局/描边/覆盖率缓存、图片分段及托管 PNG/DEFLATE。GPU 优化包括包围盒限定 dispatch、FillPath 边缓存、持久化 arena 与预分配描述符、源图身份+快照缓存；两者细节与实测见 [优化前后实测](docs/OPTIMIZATION.md)。额外 [变化场景基准](docs/PERFORMANCE-CHANGING.md) 每帧移动并超过缓存容量，用于查看未命中成本。

当前修复版本的 [CPU/GPU、内存与继续投入评估](docs/REASSESSMENT.md) 已重新实测，包含相对原版和上轮的变化、每帧 CPU 成本与进程内存。独立 HOST_CACHED 实验验证了 GPU 回读优化空间；复杂 GPU 文字仍需要改变算法。
