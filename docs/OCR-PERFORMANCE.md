> 2026-10-21：PNG SIMD 与缓冲复用已更新，已重新运行真实 OCR 对照。最新数据见 [本轮优化报告的 OCR 部分](OPTIMIZATION-2026-10-21.md)。下文保留 2026-10-12 的结果。

# Qcxt.OCR.SkiaSharp 专项性能评估：2026-10-12

结论：以当前实现和这个模块的职责看，建议继续引用 SkiaSharp。SkiaSharp 在这里负责解码/像素接入，OCR 的缩放、透视裁剪、归一化、检测和识别由 Qcxt.OCR 自己实现。SimdSkia 的矩形、路径、字体和 Vulkan 绘图性能不决定这条识别流程的速度。

## 实际基线与兼容范围

已读取 F:/基础/qcxt.framework/src/Qcxt.OCR.SkiaSharp/Qcxt.OCR.SkiaSharp.csproj 及 project.assets.json，实际使用 SkiaSharp 与 Linux NativeAssets **4.153.1**，不是此前 Avalonia 基线 3.119.4。此评估只针对 4.153.1 的 OCR 输入接口，未宣称 SimdSkia 全部 API 已兼容 4.x。

原模块不调用 SKCanvas、绘字、路径、渐变、GPU 绘图。RunBytes/RunFile/RunStream 解码后调用 RunBitmap；RunBitmap 直接将 GetPixelSpan、尺寸、RowBytes 与 RGBA/BGRA 格式送给 ocr.Run。RunImage 则经 SKPixmap/PeekPixels 访问像素。

未改动 qcxt.framework。基准链接它当前的 OCR 源码、ONNX 模型和原版扩展文件，只在 SimdSkia 工作区生成构建输出。Reference 保留完整原版 SkiaOcrExtensions；Managed 提取同一个 RunBitmap 方法，只把 PNG 字节解码换成 SimdSkia.Decode。当前核心库链接构建保留原有 5 个编译警告，没有修改目标源码来消除警告。

未修改的完整扩展编译到当前 SimdSkia **失败**，日志 artifacts/ocr/compatibility-probe.log 首先报缺少 SKStream/SKPixmap；源码还使用 SKFileStream、SKMemoryStream、SKManagedStream、Decode(SKStream)、SKImage.PeekPixels。当前不是可直接换引用的兼容版本。目标包还包含 net6.0；当前 SimdSkia 产品仅 net8.0，不能直接满足其 .NET 6 多目标构建。

## 实测方法

运行时 .NET 10.0.12，当前机器 i7-8700K/12 逻辑核；CPU 推理，PaddleOcrOptions 使用当前默认自动线程预算。两个进程类型链接同一 Qcxt.OCR.dll，SHA 相同。每条件 3 个独立进程，每进程 5 批，目标每批 100 ms；初始化/模型加载不在计时内，预热 3 次，再校准。逐条件单独新进程，避免模型驻留污染只解码的内存统计。共 36 进程、180 批，串行且顺序打乱。TieredCompilation/ReadyToRun/ServerGC=0。

图像使用目标模块现有 sample.ppm（500×500）真实 OCR 夹具，用 SkiaSharp 4.153.1 压缩为共同 PNG 输入。1024/2048 是这张图按最近邻放大产生的解码尺寸实验，不冒充额外真实 OCR 文档；整链识别仅测原始 500×500。

耗时为 15 个批均值中位数，CPU ms 为整个进程累计处理器时间/图片数；WS/private 是当前模式预热后的整个进程。总托管分配通过 GC.GetTotalAllocatedBytes 统计所有托管线程（含 OCR 并行任务），不含原生 malloc；同时保留计时线程分配。没有用额外图像扫描作为耗时工作，像素/识别核查在计时之外。

## 时间、CPU 与内存

表中每个箭头为 SkiaSharp 原版→SimdSkia；RunBitmap 行仅统计已解码图片之后的识别。

| 场景 | 原版 ms | Simd ms | Simd/原耗时 | CPU ms/图 原→Simd | WS MiB 原→Simd | private MiB 原→Simd | 全线程托管 MiB/图 原→Simd |
| --- | ---: | ---: | ---: | --- | --- | --- | --- |
| PNG 解码 500×500 | 2.293 | 7.641 | 3.33× | 2.30→7.66 | 36.7→36.2 | 17.2→17.3 | 0.002→1.582 |
| PNG 解码 1024×1024 | 4.855 | 18.348 | 3.78× | 4.76→18.23 | 37.0→39.0 | 16.5→21.2 | 0.002→4.653 |
| PNG 解码 2048×2048 | 11.844 | 49.139 | 4.15× | 11.88→50.00 | 37.2→112.1 | 16.6→98.6 | 0.002→17.154 |
| RGB24 直接 OCR 500×500 | 64.549 | 63.936 | 0.99× | 493.23→497.40 | 102.7→102.5 | 81.2→81.5 | 1.640→1.655 |
| 已解码 RunBitmap 500×500 | 59.964 | 59.184 | 0.99× | 485.94→480.21 | 104.7→94.7 | 82.5→73.9 | 1.648→1.651 |
| PNG 解码 + 真实 OCR 500×500 | 64.427 | 72.540 | 1.13× | 485.42→486.46 | 103.7→98.7 | 81.6→77.7 | 1.653→3.242 |

RGB24 两组是同一 OCR 核心直接调用的控制组，本应有相同算法成本；其速度差是环境/调度波动的参照，不能归功于 SkiaSharp/SimdSkia。RunBitmap 也未调用渲染器，它的轻量 Span 接入不足以创造推理速度优势。只解码时 SkiaSharp 的 Bitmap 像素多在原生内存，故其托管分配低；比较分配必须同时看进程内存，不能仅比较 GC 字节宣布省内存。

原版 RunBytes 的 new SKMemoryStream(encodedImage) 未用 using，也没有在私有 RunStream 里 Dispose，依赖最终回收。Reference 忠实保留该代码；基准强制 GC 在批段外回收。建议在目标项目独立修复这处生命周期，可能减少连续请求的原生存储滞留；这不需要更换整个绘图引擎，本轮未改目标项目。

## 正确性及格式限制

9 对尺寸/进程的 canonical RGBA 解码结果逐字节一致；18 次 OCR 验证（两实现×3识别模式×3进程）文本、检测框、置信度和结果字段完全一致，每次识别 16 行，包含 220ml。说明当前这张不透明 PNG 的适配比较真实可用，不代表其他格式、透明图像或 OCR 模型全部兼容。

JPEG 夹具原版解码成功，SimdSkia 明确失败。当前 SimdSkia 仅支持其已实现的 PNG 子集，不能承担模块承诺的 JPEG/WebP/BMP/GIF/ICO 等输入范围。

透明 PNG 探针还有重要行为差异：原版默认解码 Bgra8888/Premul，第一个物理像素 BGRA=[20,50,100,128]；SimdSkia 返回 Rgba8888/Unpremul，RGBA=[200,100,40,128]。目标模块直接将物理 RGB 送入识别并忽略 Alpha，因此简单更换解码器会改变透明图的模型输入。必须明确维持原版预乘语义或统一做背景合成，并重新验证 OCR 结果；本轮未靠忽略这个差异宣称普遍等价。

## 建议

如果重点是吞吐、延迟及接受常见图片格式：保留这个 SkiaSharp 扩展。本项目的绘图 SIMD/GPU 优化在这里没有入口，当前 PNG 解码反而更慢；换库不会改变检测/识别的模型计算。

如果你已经从相机、截图或其他管道拿到 RGB/BGR/RGBA/BGRA 像素：直接调用 Qcxt.OCR 的 Run(span,width,height,stride,format)，既不需要 SkiaSharp，也不需要 SimdSkia。不要为了获得 Span 再编码、解码一遍。

如果“纯 C#、不带原生解码依赖”是硬要求：可以把范围收缩为独立的托管图像解码接入/PNG 专用模块，复用 SimdSkia.Imaging 和 PixelBuffer，不必先补字体/路径/Vulkan或整套 SkiaSharp API。优先补实际输入格式、预乘 Alpha/色彩及解码优化，再在真实样本集验证。

CPU 推理本次为当前默认配置，未测 Qcxt.OCR 的 Vulkan/Metal 模型后端；那些后端属于 OCR 自己的张量计算，和 SimdSkia Vulkan 字体渲染是不同路径。OCR 模型、配置、输入大小、并发度或硬件改变后需重测，不能将本次百分比套用于全部业务。

## 数据与复现

[原始摘要](../artifacts/ocr/qcxt-4.153.1-2026-10-12/summary.json) · [CSV](../artifacts/ocr/qcxt-4.153.1-2026-10-12/results.csv) · [配置及源文件哈希](../artifacts/ocr/qcxt-4.153.1-2026-10-12/run.json)

```powershell
powershell -NoProfile -File tools/run-ocr-benchmarks.ps1 -Output artifacts/ocr/my-run
python tools/report-ocr-benchmarks.py artifacts/ocr/my-run
dotnet build benchmarks/Ocr/CompatibilityProbe -c Release
```

首次运行需配置 benchmarks/Ocr/Target.local.props 的 QcxtRoot、TargetSkiaRoot、TargetNativeSkiaRoot（只读链接源码/本地 NuGet 资产）；该机器路径文件不提交，参见 benchmarks/Ocr/README.md。最后一个编译探针目前预期失败，不能把它当通过。主引擎仍按 3.119.4 调查，4.153.1 仅作为此实际目标的独立测试基线。
