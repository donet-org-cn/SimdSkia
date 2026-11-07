# 依赖与许可证

## 0.1.2 正式版累计发行

依赖版本沿用0.1.1/preview.3：Sdcb.SimdCodecs1.4.0、Clipper2 2.0.0、可选PdfPig0.1.16，未因正式化升级第三方依赖。Core复用纯托管Clipper2；C#系统GPU调用及已允许的压缩/macOS窗口宿主例外、许可证和Metal未验证状态保持。详见[累计说明](RELEASE-0.1.2.md)。下方保留历史时点。

## 0.1.2-preview.1 的依赖边界

本轮未新增生产NuGet/原生绘图组件。路径使用纯托管Clipper2 2.0.0（BSL-1.0）；编解码固定Sdcb.SimdCodecs十五包1.4.0（MIT），可选PDF读取使用PdfPig0.1.16（Apache-2.0）。CPU几何/光栅/字体/基本排版为C#，PNG/TIFF允许.NET系统压缩的原生实现，macOS官方窗口宿主为经允许的另一例外。

GL/GLES、Vulkan、D3D11/Direct2D、D3D12的D3D11On12以及Objective-C/Metal均通过C#互操作调用操作系统/驱动，不分发Google Skia或第三方GPU绑定库。Metal没有Mac GPU，仍未实机验证。原版SkiaSharp3.119.4和ANGLE原生宿主仅用于隔离测试；FontTools、Edge/WPF、Matplotlib及glslang仅用于开发/独立验证，不进入产品。系统宋体文件只从测试机读取，未复制进NuGet或仓库。源算法和API包装保留Skia BSD/SkiaSharp MIT归属，随对应包提供许可证。

生产依赖检查包含Clipper2的传递引用以及新增Direct2D→Gpu边，不再错误地将历史“零NuGet包”描述当作本版事实。详见[本轮报告](PUBLIC-API-FINAL-2026-11-07.md)与锁文件；以下为历史记录。

## 2026-11-07 公共 API 99项缺口源码批次

Core新增对既已用于Avalonia层的 **Clipper2 [2.0.0]** 直接引用，用于C#路径布尔运算，BSL-1.0，纯托管、无原生组件；复用 `licenses/Clipper2-BSL-1.0.txt` 随Core包分发。RuntimeEffect、Perlin、Coons与固定边规则均由C#执行，移植算法保留固定基线Skia BSD许可；值合同/旧包装转发保留SkiaSharp MIT许可。不调用Google Skia原生绘图。ANGLE/原版native assets仍仅隔离测试；系统GPU驱动调用与此前允许的.NET压缩/macOS窗口宿主例外保持。当前版本和锁文件随完整Release构建验证，尚未发布。下面保留此前时点依赖记录。

## 2026-11-06 公共 API 补全（未发布）

没有新增生产 NuGet 或原生组件。矩阵、流、格式和值合同及旧 Paint 转发采用固定 SkiaSharp 3.119.4 的 MIT C# 源码并保留归属；skcms 转移函数、路径测量/椭圆弧及 CSS 字体评分根据 Google Skia chrome/m119 的 BSD-3 算法改写为 C#。许可证已分别随 Compatibility 包分发 `SkiaSharp-MIT.txt` 和 `Skia-BSD.txt`；这是算法的托管实现，不加载或调用 Google Skia 原生库。Core 增加必要的外部内存 Span 边界，字体文件页使用 .NET 文件 API；编解码继续保留此前允许的 .NET 系统压缩例外。原版包及 native assets 只在新增的 Original 对照项目内。


2026-11-06 像素API一轮未新增生产包或原生分发组件。HSL/HSV浮点颜色算法采用SkiaSharp3.119.4的MIT实现，保留头部与既有SkiaSharp-MIT许可证；没有调用其原生颜色函数。像素视图/转换、Vector<uint>交换和采样调度为C#实现，双线性复用本库既有CPU内核。unsafe仅用于调用方指针到有界Span的互操作边界和泛型sizeof，原版3.119.4仅为隔离oracle。图表仍仅使用artifacts开发依赖，不进入NuGet。

2026-11-06 描边/查询扩展没有新增生产依赖、unsafe或原生组件。高级描边、几何查询和有界缓存由C#实现；冻结旧算法仅在测试项目中。SkiaSharp3.119.4仍只用于隔离对照，Matplotlib图表只用于开发报告，不进入产品。

2026-11-06 圆锥API/GPU缓存一轮未新增NuGet或原生分发依赖；有理曲线和缓存逻辑为本仓库C#实现，Direct2D通过已有系统COM绑定。原版3.119.4和ANGLE仍只用于隔离对照；Matplotlib/NumPy仅在artifacts开发目录生成性能图，不进入产品。字体沿用OFL夹具。新测试只在Windows x64实机执行。


2026-11-06 渐变扩展未新增NuGet或原生运行组件。扫描渐变采用Skia m119中xy_to_unit_angle多项式的标量C#翻译，以匹配基线接缝方向；不是链接或调用Skia。来源为src/opts/SkRasterPipeline_opts.h，Copyright 2018 Google Inc.，BSD-3-Clause，归属及条款随Compatibility输出/NuGet保留于licenses/Skia-BSD.txt。其他多色标/矩阵/擦除及Vector<T>内核为本仓库C#实现。原版3.119.4仅在隔离oracle/benchmark使用，图表工具仍仅为artifacts内开发依赖。


2026-11-06 本轮生产依赖未增加。固定原版3.119.4仅在隔离测试/基准使用；命名颜色值由该基线核对，原MIT归属继续随兼容层保留。报告绘图开发依赖Matplotlib3.11.2（Matplotlib 1.3.0及以后版本许可协议，基于PSF/BSD条款）、NumPy2.5.4（BSD-3-Clause AND 0BSD AND MIT AND Zlib AND CC0-1.0，含原生扩展）及其传递依赖仅安装于忽略的artifacts/report-python目录，不改系统Python、不进入C#产品、NuGet或AOT发布。字体仍用既有固定OFL夹具。

## 0.1.1 版本兼容

渲染适配器以 Avalonia 12.1.0 编译，允许 [12.1.0,13.0.0)。桌面包保存 Avalonia 12.1.0–12.1.5 的 Win32/X11/FreeDesktop/AtSpi/Native 托管宿主与对应官方 macOS 窗口桥接库，均为 Avalonia MIT（包内 licenses/Avalonia-MIT.txt）；构建按实际核心版本选择一套，其他版本不进入应用输出。libAvaloniaNative.dylib 是此前允许的 macOS 窗口系统原生例外，不负责 Google Skia 绘图。Linux 原有托管 DBus 依赖 Tmds.DBus.Protocol 0.94.1（MIT，net9 资产传递 System.IO.Pipelines 8.0.0，无 native 资产）由传递改为显式引用；用途和最终依赖没有扩张。SDK >=10.0.403，采用已修复安全问题的 .NET 10.0.14 AOT 工具链，保留 NuGet 审计。

## 0.1.0 候选：2026-11-04

直接依赖升级至 Avalonia 家族 12.1.5、Sdcb.SimdCodecs 1.4.0（相同十五包图，MIT、托管）；Clipper2 2.0.0、PdfPig 0.1.16 已是查询时最新稳定版本。Avalonia 仍 MIT，官方 macOS 窗口宿主原生例外保留；PNG/TIFF 的 .NET 系统原生压缩例外保留。没有增加 Google Skia 绘图/字体依赖。SDK 包元数据、测试与实测见[0.1.0 报告](STABLE-0.1.0-2026-11-04.md)。下文保留旧版依赖调查的历史时点。


preview.10 已将下列完整 .NET 10 / Sdcb.SimdCodecs 接入公开发布并完成十二包下载核验；实际记录见[发布记录](NUGET-PREVIEW.md)。下文“本地试用/未公开”是接入验证时的快照，旧 preview.9 依赖与包内容不改写。

## 当前源码完整试用：2026-11-01

用户已明确允许 **.NET 自带原生压缩**，并要求完整替换试用。当前源码默认 .NET 10 / C# 14；Imaging 直接精确引用 **`Sdcb.SimdCodecs [1.2.0]`**，传递图为 15 个同版本 MIT 托管包：Facade、Abstractions、Bmp、Color.Icc、Gif、Ico、Jbig2、Jpeg、Jpeg.GainMap、Jpeg2000、Png、Pnm、Qoi、Tiff、Webp。包源码提交 `02b1f5aab83fee545748801639b12002ee7d2462`，锁文件固定实际内容；随 Imaging 输出/打包保留 `Sdcb.SimdCodecs-MIT.txt`。

JPEG/PNG 像素编解码和 GIF LZW/合成全部使用该依赖，生产 `JpegLibrary` 引用已移除。颜色转换、严格输入验证、像素预算、stride/Alpha 和生命周期仍由适配层承担；通用 ManagedZlib 保留用于 PDF/元数据等既有合同，不作为旧图片解码回退。PNG/TIFF 的 BCL 压缩会调用 .NET 自带原生实现；仍不引入 Google Skia/libpng/libjpeg 等第三方原生绘图库。Core/Cpu/Text/Gpu 的无 NuGet 包约束保持，Avalonia/Compatibility 传递引入新编解码图。

本次仅生成本地 `0.1.0-codecs-trial.1` 包，不上传公共源；公开 preview.9 保持原 .NET 8/JpegLibrary 版本。独立测试使用 Pillow/libwebp/OpenJPEG 作为黑盒生成自有图案夹具，未将其代码或二进制加入产品。实际接入、测试、颜色差距和性能见[完整试用报告](SIMDCODECS-INTEGRATION-2026-11-01.md)。

## 下列为切换之前的调查/发行快照

2026-11-01 隔离编解码评估：`benchmarks/CodecEvaluation` 通过 NuGet 精确引用 `Sdcb.SimdCodecs.Jpeg`、`Sdcb.SimdCodecs.Png` **1.2.0**，传递引入同版本 Abstractions、Color.Icc；四者均 MIT、仅 net10.0。包不含原生绘图库，PNG 使用 BCL ZLibStream，实测加载 `System.IO.Compression.Native.dll`，因此没有将其视为满足当前全 C# 压缩约束的生产实现。JPEG/PNG 性能、颜色转换失败和实际 Native AOT 对照见[评估报告](SIMDCODECS-EVALUATION-2026-11-01.md)。这些依赖只进入独立基准和 AOT 探针；生产源项目、net8.0 基线和包依赖图保持不变，未复制上游算法。

VelaShell 整体兼容验证：固定公开提交 `3c8a20ef739ef4fc2d3363a9d7aaf8ba15ca7b7c`，第三方宿主按其 AGPL-3.0/商业双授权条款保留于隔离 artifacts。未将目标应用代码或原版绘图库加入 SimdSkia 生产依赖；报告截图/测试补丁保留目标归属。只读性能定位工具 dotnet-stack 10.0.750501 安装于实验目录，不进入产品或包。本轮无新增生产 NuGet 依赖，未发布任何新包。

未发布兼容扩展：可选 `SimdSkia.Pdf` 新增精确 `[0.1.16]` **PdfPig**（Apache-2.0，C# PDF 结构/字形轮廓解析）。包记录源码 `a7bb35662bbbf405efddad50aedc9bcdcf515afc`；net8.0 资产没有额外包依赖、native/runtime assets，SHA-512 固定于模块锁文件。阅读器使用自有 `ManagedZlib` 替换默认 Flate 解码；不调用 Skia、PDFium、MuPDF、FreeType 或 native zlib。未引用 PDF 模块的 Compatibility/Avalonia 不增加这一依赖。许可及说明见 `licenses/Apache-2.0.txt`、`licenses/PdfPig-NOTICE.txt`。

`Geometry.cs`、几何值声明及新增枚举/选项采用固定 SkiaSharp 3.119.4 的 MIT 托管值类型代码，保留归属并随包分发 `licenses/SkiaSharp-MIT.txt`；没有调用或复制原生 Skia 绘制实现。PDF 测试夹具包含 OFL ZCOOL XiaoWei 字体子集并附许可。独立开发验证使用 ReportLab 5.1.0（BSD）、Pillow 13.0.0（HPND）、pypdf 6.21.2（BSD-3-Clause）和 PyMuPDF 2.0（AGPL/商业双许可，含原生组件）；它们只安装在被忽略的隔离 `artifacts` 测试目录，**不进入产品、NuGet、AOT 输出或 C# 测试依赖图**。仓库只保留自有夹具、验证脚本与结果，未分发参考渲染库。

2026-10-27 的 `benchmarks/AvaloniaGpuParity/Original` 新增固定 `Avalonia.Angle.Windows.Natives 2.1.27548.20260419`，仅用于原版 Skia 经 EGL/OpenGL ES → 硬件 D3D11 的隔离对照，包含 `av_libglesv2.dll`。包内 LICENSE 为 ANGLE 三条款 BSD 许可，源码归属提交 `1c89805903c1482166356d3b950d474973180e61`；许可随隔离输出复制。该包不进入 SimdSkia 生产项目。`tests/FontOutlineProbe` 仅使用既有原版 SkiaSharp 3.119.4 导出诊断轮廓，禁止把这些数据用于生产字体渲染。

性能报告绘图使用隔离 Python 环境中的 matplotlib/numpy，字体调查另用 fontTools；均为开发分析工具，不进入 NuGet 引用图。`artifacts` 中的 SharpFont/LayoutFarm 字体解释器是 MIT 的隔离调查副本，尚未集成产品，不能把实验结果当作已实现 TrueType hinting。

preview.2 新模块：`SimdSkia.Gpu.Direct2D` 无 NuGet 依赖，自写 C# ABI 调用系统 d2d1/d3d11/DXGI，不分发这些 Windows 系统 DLL。`SimdSkia.Avalonia.Gpu` 仅引用已有托管引擎/适配器。`SimdSkia.Avalonia.Desktop` 加入官方 MIT Avalonia.FreeDesktop/AtSpi/Native 12.1.4、MIT Tmds.DBus.Protocol 0.94.1 和 MIT System.IO.Pipelines 8.0.0；X11/Win32 通过固定 PackageDownload 仅分发托管宿主 DLL及 Avalonia MIT 通知。macOS 按用户最新选择允许官方 `libAvaloniaNative.dylib`，仅作窗口/系统交互；绘图、字体解析/覆盖率和编解码依旧为 C#。Linux 需部署系统 X11/XWayland/GLib 等环境库。原型 Vortice/SharpGen 因 AOT 反射裁剪问题已移除，不进入任何交付包。

新增 `examples/AvaloniaAot` 是 .NET 10 Native AOT / Win32 软件窗口对照，继续固定 Avalonia/Fluent 12.1.4。SimdSkia 版只使用上述托管渲染依赖和操作系统 API；Original 版单独引用 Avalonia.Skia 12.1.4、SkiaSharp 3.119.4、Avalonia.HarfBuzz 12.1.4、HarfBuzzSharp 8.3.1.3，仅此对照进程携带原版原生 DLL。两边均携带同一 OFL 字体与许可证，Original 还携带包内 SkiaSharp/HarfBuzzSharp LICENSE；体积清单逐文件记录。Native AOT 的 .NET 10 运行时随 C# 程序静态编译链接，不要求用户额外安装 .NET。基准父程序为 Python 标准库，Windows 资源 API 仅用于测量；报告差异图的 zlib 不进入产品引用图。

当前生产依赖：Core/Cpu/Text/Gpu 没有 NuGet 包。Imaging 新增精确 `[0.4.32]` 的 **JpegLibrary**（MIT，纯 C# JPEG 熵编码、IDCT 和编码器；net8 使用其 net6 托管资产，没有传递包或 native 资产），Compatibility 与 Avalonia 适配层传递引用该包。字体和编解码全部 C# 的要求保持；此前“六模块零包”是历史实现状态，现已变更。

JpegLibrary 固定源码提交 `d4dcb149dd4cd4a18994bf5bf9009f5691e39e75`，来自 [上游](https://github.com/yigolden-oss/JpegLibrary/tree/d4dcb149dd4cd4a18994bf5bf9009f5691e39e75)。其第三方通知包含 2018 SixLabors.ImageSharp 的 Apache-2.0 归属，不代表引用当前 ImageSharp 包。随 Imaging 输出和打包保留 `JpegLibrary-MIT.txt`、`JpegLibrary-THIRD-PARTY-NOTICES.md`、`Apache-2.0.txt`；锁文件记录包内容哈希。四分量渐进解码有经夹具发现的上游扫描 flush 问题，本引擎显式拒绝，详见最新测试报告。

可选第七模块 `SimdSkia.Avalonia` 另用 `Avalonia 12.1.4`（MIT，控件平台契约）、`Clipper2 2.0.0`（BSL-1.0，布尔几何/描边）；传递包 `Avalonia.BuildServices 11.3.2`、`Avalonia.Remote.Protocol 12.1.4`、`MicroCom.Runtime 0.11.6`（MIT，构建/协议/托管 interop）。包括 JPEG 共六个固定纯托管包，不调用 Skia/HarfBuzz 绘制或排版。许可副本在 `docs/licenses`，适配层输出携带上游通知。

真实 UI 示例另用 `Avalonia.Themes.Fluent 12.1.4`（MIT，托管主题资源）。`Avalonia.Win32`、`Avalonia.Headless` 的固定 12.1.4 包仅通过 PackageDownload 取托管 DLL，未解析 ANGLE/HarfBuzz 传递依赖；Win32 自身调用操作系统 API。无头初始化仅用于示例/测试，固定契约的私有入口明确记录在 `HeadlessHost.cs`。隔离 `AvaloniaControls.Reference` 才安装官方 Avalonia.Skia/Avalonia.HarfBuzz 和其原生组件。生产引用图和输出由 `check-production-dependencies.ps1` 检查。

新增 ZCOOL XiaoWei Regular 固定 OFL 简体中文 TTF 夹具，不含原生代码；源提交、SHA 与许可证获取固定在 `fetch-avalonia-font.ps1`，许可证副本 `docs/licenses/ZCOOLXiaoWei-OFL.txt`。其字形图像/排版数字用于验收，字体二进制位于被忽略的 baseline，不再分发系统字体。

以下为原六模块和此前隔离测试依赖记录；其中“Avalonia 仅探针/非产品”的描述不包含新适配层。

以下旧记录中的原六程序集无包状态已由上述 JPEG 接入替代。字体、排版、图片编解码均由 C# 实现，不调用 Skia、FreeType、HarfBuzz、System.Drawing 或 native zlib。GPU 按用户授权调用系统 Vulkan loader/驱动，shader 是 GLSL/SPIR-V。

| 组件 | 用途 | 许可证/原生性质 | 产品关系 |
| --- | --- | --- | --- |
| .NET 8 runtime/BCL、SDK 10 | 执行/构建 C# | .NET 发行许可证/主要 MIT，运行时本身含平台组件 | 标准执行环境 |
| 系统 Vulkan loader/驱动 | Windows/Linux GPU API 与硬件执行 | Khronos loader 通常 Apache-2.0；驱动许可证由厂商决定；原生 | GPU 运行环境，不打包替代系统库 |
| glslang 16.6.0 独立工具 | 开发阶段 GLSL -> SPIR-V | 官方 LICENSE 列 BSD/MIT/Apache 等组成；原生可执行 | 不作为产品依赖，不安装系统软件 |
| Vulkan-Headers v1.3.290 | 仅 ABI/prototype 调查 | 官方 headers license（Apache-2.0/MIT 相关文件） | 自写 C# interop，未复制头文件到产品 |
| Zen Old Mincho Regular | 可下载的真实 CJK/Latin 测试字体 | SIL OFL 1.1，无原生代码；license 随夹具保留 | 可选测试数据，不是字体引擎库 |
| 系统 SimHei/Arial | 本机对照/示例字体文件 | 用户已有系统字体许可证，不再分发 | 只读实际安装字体 |
| Avalonia.Skia/Avalonia 12.1.4 | 隔离版本恢复探针 | MIT，transitive 原生 Skia/HarfBuzz assets | 不在生产引用图 |
| SkiaSharp/NativeAssets 3.119.4 | 隔离 API/行为/像素/CPU 基准 | MIT 包和随包第三方 notices；原生 Skia | 仅 ReferenceRenderer |
| HarfBuzzSharp 8.3.1.3/NativeAssets | 探针 transitive | 随包 MIT/HarfBuzz notices；原生 | 不用于本引擎 shaping |
| MicroCom.Runtime 0.11.6 | 探针 transitive | MIT，托管 interop | 非产品 |
| Avalonia.BuildServices 11.3.2 | 探针 transitive 构建任务 | MIT、托管 DLL | 非产品 |

[glslang 官方许可证](https://github.com/KhronosGroup/glslang/blob/main/LICENSE.txt) 是混合许可说明，不能只写一个简化标签代表所有组成。[字体 OFL](https://github.com/google/fonts/blob/dac28ca2a883c27a817104b72bfa8d5e7e973954/ofl/zenoldmincho/OFL.txt) 与固定文件一起保留在 baseline/fonts。字体 commit 和 SHA-256 固定在 fetch-test-font.ps1。

baseline/ 被忽略；原版 DLL 只复制至独立测试输出。tools/check-production-dependencies.ps1 检查六个产品 assets 和输出。参考 Python/zlib 只生成一个 547 B fixed Huffman 测试数据，不是产品/测试运行依赖。

Avalonia 专项额外读取固定 12.1.4 官方 MIT 源码及隔离 Avalonia.Skia.dll，做 IL/API 调查；源码 archive 与 54 个 C# 文件 SHA 在 avalonia-source-baseline.json。没有将 Avalonia 或其原生依赖引入六个生产程序集。新增 Python 报告中的 zlib 仅用于测试差异图 PNG 的编码，产品编解码仍全部 C#；独立 zlib/PNG 回归夹具的完整计数见 VALIDATION.md。

已编译 SPIR-V 随源码提供，普通构建无需 glslang。shader 源码/编译器/输出版本与哈希见 shader-build.json。SimdSkia 按用户确认采用根目录 MIT 许可证；各上游组件保留其各自许可证。


OCR 专项基准额外只读链接 Qcxt.OCR（Apache-2.0 源码及其原有第三方/模型 notices）、Qcxt.OCR.SkiaSharp（MIT）和本机缓存 SkiaSharp/Win32 NativeAssets 4.153.1（MIT 包及随包 notices，原生组件，仅 Reference）。目标资源保持原项目的许可归属；这些项目不属于六个生产程序集的引用图。基准使用 .NET 10 执行实际目标 CPU 内核，不修改全局兼容基线，也不把旧版/本项目的 native handle 或二进制兼容结果推广到 4.x。

2026-11-06 图像着色器/全混合/颜色滤镜增量没有引入生产包或原生库。ToShader/工厂转发按固定 SkiaSharp 3.119.4 MIT 源码核对；字节混合舍入、矩阵和高对比度工作色域参考 Google Skia m119 的 BSD-3-Clause 算法，以 C# 实现（不调用 Skia）。已有 Skia-BSD 与 SkiaSharp-MIT 条款继续随库输出保留。原版 DLL 仍仅进入 Original 测试/基准。

2026-11-06 字体程序实现增量：使用 Michael Popoloski SharpFont 的 MIT C# TrueType 字节码算法，经 LayoutFarm/Typography 固定提交 5877180c7c5271091379a0eaf9f03ab6ebd256b3 核对。仅移植解释器/点结构，不引用 Typography 的窗口、SkiaSharp、原生字体或图像后端；没有新增生产 NuGet 包。SimdSkia 改为托管 IUP、显式失败与每次程序 100 万指令预算；许可证在 docs/licenses/SharpFont-MIT.txt 并随 Text 输出/包保留。仅 em 轮廓程序链路，扫描控制状态被记录；不据此声称完整字号 hinting、dropout 或复杂排版已实现。隔离 FontTools 与去指令 OFL 派生字体只用于 artifacts 下的归因实验，不进入产品或 NuGet。

2026-11-07 余项开发：SVG/XPS/SKP/WebP/浮点/LCD/EBDT未新增生产NuGet或自带原生组件。SKP v103布局、画笔打包、8x8抖动参考固定Skia m119 BSD-3源码并用C#独立实现，继续保留Skia-BSD。EBDT/EBLC按Microsoft OpenType规范实现。独立测试使用Edge的SVG渲染、Windows WPF XPS阅读器、FontTools 4.68.0解析系统宋体；这些仅在隔离测试/工具中，未进入生产引用。系统字体文件不复制入产品或包；测试记录字体哈希及有限渲染输出。参考规范：[EBLC](https://learn.microsoft.com/en-us/typography/opentype/spec/eblc)、[EBDT](https://learn.microsoft.com/en-us/typography/opentype/spec/ebdt)。

2026-11-07 GPU兼容入口增量：Compatibility 新增对本仓库纯C# `SimdSkia.Gpu.Direct2D` 项目的引用，用于通过系统 D3D11On12 将 C# Direct2D 驱动映射到调用方实际 D3D12 device/queue。调用Windows自带d3d11/d3d12/d2d1/dxgi、系统OpenGL，以及macOS自带Metal/Objective-C系统API；不复制或分发这些系统库，不携带新的渲染/字体/编解码原生组件。GPU着色器分别为GLSL与MSL。Metal部分没有Mac GPU实机结果，不能由Windows编译成功推断运行支持。

隔离GL测试宿主固定引用 `Avalonia.Angle.Windows.Natives 2.1.27548.20260419`（随包上游许可证及notices，包含原生ANGLE）；仅进入Original/Managed测试，不进入产品包。另有WGL系统OpenGL测试与基准，不需要ANGLE。原版测试固定SkiaSharp/NativeAssets 3.119.4，macOS实机测试工程引用其macOS NativeAssets；后者在本机明确为未执行。D3D11On12系统接口参考[Microsoft官方文档](https://learn.microsoft.com/en-us/windows/win32/api/d3d11on12/nf-d3d11on12-d3d11on12createdevice)，Metal完成语义参考[Apple waitUntilCompleted](https://developer.apple.com/documentation/metal/mtlcommandbuffer/waituntilcompleted())；这不构成Metal实机通过记录。
