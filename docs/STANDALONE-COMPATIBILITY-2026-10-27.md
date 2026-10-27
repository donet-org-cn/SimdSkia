# 独立 SkiaSharp 兼容层补齐与 PDF：实现、原版对照和剩余差距

发行关联：本报告记录测量当时的源码与结果；这些增量纳入 0.1.0-preview.9。公开状态见[发布记录](NUGET-PREVIEW.md)，未改写 preview.8 的历史内容或测量成绩。

日期：2026-10-27。**这是尚未发布的源码增量，不是 NuGet 0.1.0-preview.8 的功能说明。** 本轮没有上传包、创建版本或更改公开包。固定基线仍为 SkiaSharp **3.119.4**，Avalonia **12.1.4**；不是对 4.x 或所有版本的兼容承诺。

网友反馈的五组独立 API 缺口已补入真实实现，并分别编译到原版与 SimdSkia 上执行。新增多页 PDF 生成、已有 PDF 读取/页面光栅化和可翻页的 Avalonia 查看器。**完整 API、原版画质及任意 PDF 兼容仍未完成。** 原始测量与检查记录见 [JSON 证据](evidence/standalone-compatibility-2026-10-27/results.json)。

## 反馈项实际修复情况

| 原问题 | 当前源码实现 | 重要边界 |
| --- | --- | --- |
| `SKBitmap.GetPixels()` 抛异常 | 懒固定托管像素缓冲，返回真实地址；支持带 stride、out 长度、直接写入与 GC 后地址稳定 | 地址只在 bitmap 未重分配/释放时有效；不是 Skia native Handle；异步视频写入必须同步生命周期 |
| `SKTypeface.FromFamilyName` 缺失 | 四个重载、系统字体目录发现、TTF/TTC 家族/本地化名称/字重宽度倾斜匹配；补 `SKFontStyle` 和字体属性 | 本机微软雅黑及粗体/中文字形通过；复杂 shaping、hinting VM、逐字符字体回退仍未完成 |
| `SKCodec` / GIF 缺失 | PNG/JPEG/GIF Codec、逐帧解码、延迟/循环、透明度、处置、交错扫描和随机回退 | GIF 编码、增量/扫描线接口、WebP/APNG 等仍缺；Plain Text 扩展明确拒绝 |
| `SKRoundRect` / `SaveLayer` 缺失 | 圆角半径/变换、绘制/路径/圆角裁剪；独立临时层、整组 Alpha、嵌套状态恢复和 Src/SrcOver/Clear | 圆角边缘尚未与原版全部一致；图层使用 CPU 临时像素，不是新 GPU 原生图层实现 |
| `SKRect.Inflate` 缺失 | 同时补完整托管 Point/PointI/Point3、Size/SizeI、Rect/RectI 值操作和相关重载 | 几何值 API 不代表所有路径/矩阵功能齐全 |

附带补齐 `SKData.CreateCopy/Create`、`SKPath.AddRoundRect`、`DrawImage` 重载和 `SKSamplingOptions`，提供最近邻、双线性、Mitchell 采样。Mipmaps、各向异性和其他未支持 cubic 参数直接报错。

`SaveLayer(limit, paint)` 的 limit 是边界提示，不能当作无条件裁剪：实测原版在 Src/Clear 会扩展到父裁剪区域。本实现按这条规则处理，保留专门回归。需要严格裁剪时显式调用 `ClipRect`。该行为也与 [Skia 图层实现说明](https://skia.googlesource.com/skia/+/181b6f93ad2ded7ab64517e49b2250645b487fae/src/core/SkCanvas.cpp) 一致；实际验收仍以固定原版二进制为准。

## API 清单、二进制与生命周期

| 指标 | 已发布 preview.8 | 当前源码 |
| --- | ---: | ---: |
| 匹配公开类型 | 37 | 63 |
| 匹配公共条目 | 425 / 3235 | **892 / 3235** |
| 缺失或不同 | 2810 | **2343** |
| 已暴露但不匹配基线的条目 | 0 | 0 |

清单包含类型、枚举、值类型、重载、默认参数和明确拒绝的方法；不能把 892 项理解为 892 项完整绘图功能。详见 [API 清单](api/implemented.json)、[逐项差异](api/comparison.json) 和 [兼容报告](COMPATIBILITY.md)。

二进制兼容依旧不成立：程序集是 `SimdSkia.Compatibility`，不是带原版强名称的 `SkiaSharp`。普通项目不能同时全局引用两个同名类型集合；可在隔离测试中使用 assembly alias，但两个程序集中的类型不可互相赋值。没有网友 SDL3/LibVLC 客户端源码，本轮不宣称其整应用已迁移。

- `GetPixels()` 不复制像素；地址保持到重分配或 Dispose。停止原生回调、等待使用者结束后才可释放，不可将指针留在失效的 bitmap 外。
- `SKCanvas` 保留像素视图，bitmap 先释放后，既有 canvas 仍能完成绘图；这不延长已经公开的 bitmap 指针合同。
- `SKCodec.Create(Stream)` 接管该流，Codec Dispose 时关闭。`SKDocument.CreatePdf(Stream)` 保留调用者流，Close/Dispose 不关闭它。
- PDF 页面 canvas 在 `EndPage` 后失效；阅读器返回的页面位图独立存活，必须由调用者 Dispose。
- Native Handle、外部 InstallPixels/SetPixels、释放 callback、完整 GR API 未实现，不能因 GetPixels 可用就宣称原生对象兼容。

## PDF 生成和读取

生成位于 `SimdSkia.Compatibility`，使用 `SKDocument.CreatePdf`、`BeginPage`、原有 `SKCanvas`、`EndPage`、`Close`。支持多个页面、DPI、Unicode 元数据、无损 RGB 或 JPEG 页面。为控制内存，每页完成即写出，不保留全文件的页面位图。

**当前是栅格 PDF：没有矢量内容、可搜索文字或字体嵌入；PDF/A 明确拒绝。** 它能生成可打开、可打印的 PDF，但不等价于原版 Skia 的矢量 PDF 后端。默认 72 DPI，示例使用 144 DPI；单页光栅预算 16 MP，过高 DPI 会报错。

读取属于额外品牌模块 **`SimdSkia.Pdf`**，不伪造 SkiaSharp 的 PDF 读取 API。它引用固定的纯托管 PdfPig 0.1.16（Apache-2.0）解析结构/字体轮廓，由 SimdSkia C# 光栅器绘制。普通 Compatibility/Avalonia 引用不包含该 PDF 解析器。Flate 使用自身 C# ManagedZlib，不使用 PdfPig 默认的 BCL DeflateStream；详见 [依赖和许可](DEPENDENCIES.md)。

| PDF 输入特征 | 本轮范围 |
| --- | --- |
| 分页、页面旋转、裁切、压缩内容流 | 已实现，独立两页夹具验证旋转 |
| 路径、基本填充/描边、裁剪、Alpha、绘制顺序 | 已实现受支持子集 |
| RGB/Gray、RGB 图片、JPEG、图像软遮罩 | 已实现受支持子集；独立 RGBA 软遮罩夹具通过 |
| 嵌入 TrueType 中文/英文 | 独立 ReportLab PDF 真实渲染通过 |
| CFF 轮廓 | 接入 PdfPig 可提供的规范化轮廓，尚缺独立 CFF PDF 语料验收 |
| 未嵌入字体、标准 14 字体 | 无轮廓时明确失败，不自动替换成别的系统字体 |
| 注释/表单外观、可选图层、shading/pattern、透明组、图形软遮罩 | 未实现，明确拒绝 |
| CMYK/ICC 绘制、非 Normal 混合、dash/部分描边、文字裁剪/竖排、Matte 还原、非默认 UserUnit | 未实现，明确拒绝；含 UserUnit=1.5 的边界回归 |

输入上限 256 MiB，单个 Flate 解压上限 64 MiB，单页/图片上限 16 MP。以上预算不是整个文档的总进程内存上限。阅读器还不能作为通用 Acrobat/PDFium 替代品，复杂业务 PDF 需要实际语料验证。

## 原版行为、画质和独立 PDF 对照

相同 `tests/CompatibilityExpansion/Program.cs` 分别引用原版 SkiaSharp 3.119.4 与项目实现：原版 **26 项**、SimdSkia **37 项**检查通过；差出的 11 项验证本实现的拒绝行为、释放、损坏数据及托管解压预算。Windows 微软雅黑检查取决于本机是否安装该字体。

新捕获共 **14 组，11 组通过、3 组失败**。沿用精确整数几何、合成 1 LSB、路径/文字 AA 最大通道误差 32 的规则，未放宽门槛。

| 捕获 | 最大通道误差 | RMSE | 结果 |
| --- | ---: | ---: | --- |
| 5 个 GIF 帧 + 两次回退 | 0 | 0 | 7/7 逐字节一致 |
| 图层 bounds/变换 | 0 | 0 | 通过 |
| Src 图层 | 0 | 0 | 通过 |
| 整组 Alpha | 1 | 0.442 | 通过 |
| AA clip + 半透明层 | 20 | 0.818 | 通过 |
| 圆角 AA | 34 | 1.653 | **失败**，门槛 32 |
| 圆角硬裁剪 | 255 | 9.202 | **失败**，8 个边缘像素选择不同 |
| 微软雅黑中英文 | 79 | 8.551 | **失败**，字体边缘覆盖率仍不同 |

这意味着“可以调用和绘出内容”已改善，但整页画质问题并未全部解决；之前 CPU/GPU 整页失败结果没有被本轮改判。圆角用 cubic 轮廓进入当前光栅器，仍与原版专用曲线/裁剪覆盖规则有差异；文字测试已显式对齐 None hinting、AA、Subpixel、LinearMetrics、BaselineSnap，仍不能逐像素匹配。

PDF 阅读普通套件 **18 项**；另读取原版 SKDocument 生成的两个页面后共 **21 项通过**。包含尺寸/颜色断言、重复 DPI、页面独立寿命、已释放对象、CMYK/shading/可选内容/UserUnit 显式失败。不是只检查“文件存在”或“能解析”。

独立 pypdf 验证本实现导出的 PDF 页树和中文元数据；独立 PyMuPDF 渲染 ReportLab 夹具后比较：第一页最大误差 **62、RMSE 1.542**，旋转页 **逐字节一致**，软遮罩页最大误差 **1、RMSE 0.418**。不同 PDF 光栅器的这些数字是诊断结果，不代表已建立覆盖全部 PDF 特性的质量验收门槛。参考工具只在隔离开发环境运行，不进入引擎或发布依赖。

## 视频像素复制性能：只测这条热路径

同机 Windows 10.0.26300、i7-8700K、12 逻辑处理器，.NET 8.0.32 Release JIT，固定 SkiaSharp 3.119.4。1920×1080 BGRA8888、Alpha=255，源 stride 多 64 字节；每帧调用 GetPixels 并逐行 Buffer.MemoryCopy。200 帧预热，每个新进程采样 3 秒，原版/替代串行交替，各 3 次取中位数；每行输出均校验相等。

| 指标 | SkiaSharp | SimdSkia |
| --- | ---: | ---: |
| 耗时 ms/帧 | 0.5921 | **0.5907** |
| CPU ms/帧 | 0.5921 | **0.5879** |
| 整机 CPU 占用 | 8.332% | **8.331%** |
| 工作集 MiB | 42.63 | **40.53** |
| Private MiB | **24.45** | 24.66 |
| 采样期分配/帧 | 0.216 B | 0.216 B |

耗时比 **0.9976**，视为基本持平；几个百分点的差异不能当作稳定优势。预跑曾测到 1.0371，该轮也保留在证据中，不挑选并拼接不同轮次。分配数字包含固定计时/进程测量开销，不是每帧创建位图；CPU 为未限帧单线程拷贝，占满一核约等于这台机器的 8.33%。工作集是采样后的快照，不是长时间峰值。

**这不是 LibVLC 解码、SDL3 纹理上传、页面合成、GPU 或 7×24 常驻应用的性能结果。** 没有原网友客户端，不能据此推翻 preview.8 Avalonia 控件页常驻内存比原版高的旧结果。本轮没有完整重跑 Avalonia CPU/GPU 性能，也没有承诺全面追平。

分配方面：GIF 帧索引缓冲按最大帧复用，Codec 直接转换内部合成帧，避免每帧克隆一份完整 RGBA；restore-previous 和倒序回放仍可能分配。字体目录只缓存元数据，字体数据使用弱引用及既有有界文件页缓存。SaveLayer 使用全目标尺寸的池化临时像素，深层嵌套仍增加峰值内存。

## 构建、AOT 和依赖体积

- 整仓 Release：**0 警告、0 错误**。
- 现有回归：36 基础、13 功能、484 编解码、471,569 精确优化、595 文字链路、9,741 Avalonia 控件检查通过。
- 实际设备回归：Vulkan 采样 590、内核 540、GPU 文字 599、Direct2D 344、GPU 集成 12,174 项通过；这是功能回归，不是本轮 GPU 性能比较。
- 最终实际 Native AOT：兼容调用链 **38 项**；PDF **22 项**（包含原版生成页面和 AOT 运行时断言）通过，无 IL 裁剪/AOT 警告。
- PDF 查看器已在本机真实窗口呈现页面并捕获 framebuffer，支持 Open/Previous/Next。Linux/macOS 新阅读器及新查看器本轮未实际运行；CI 增加托管三平台回归和 Windows AOT/窗口测试，尚未声称远端 CI 已完成。

本机 win-x64 AOT 测试 EXE：兼容套件 **2,719,232 B（2.59 MiB）**，含 PDF 阅读套件 **11,517,952 B（10.98 MiB）**，均无依赖 DLL；独立夹具和 PDB 不在 EXE 数字中。这是测试程序体积，不是 Avalonia 应用或“库只有这么大”的承诺。

当前普通独立消费者的 7 个引擎/编解码 DLL 共 **647,168 B（632 KiB）**，其中兼容层 **113,152 B**。不能只拿兼容层 DLL 与原版整个原生库比较。可选 PDF 解析器有额外体积；它不被默认绘图引用引入。

## 复现和使用

使用 .NET 10 SDK，正常运行目标仍含 net8.0；原版隔离测试当前固定 Win32 native assets。

```powershell
dotnet build SimdSkia.sln -c Release
dotnet run -c Release --project tests/CompatibilityExpansion/Managed -- artifacts/compat-expansion/managed
dotnet run -c Release --project tests/CompatibilityExpansion/Original -- artifacts/compat-expansion/original
python tools/compare-standalone-compatibility.py # 当前返回 2，保留 3 项质量失败

dotnet run -c Release --project tests/Pdf -- artifacts/compat-expansion/pdf artifacts/compat-expansion/original/pages.pdf
dotnet run -c Release --project examples/Documents -- artifacts/documents
dotnet run -c Release --project examples/PdfViewer -- tests/Pdf/Fixtures/external.pdf
# 查看你自己的文件：把最后一个参数换成 PDF 路径。

dotnet publish tests/CompatibilityExpansion/Managed -c Release -r win-x64 -p:NativeAotTest=true -p:StripSymbols=true -o artifacts/compat-expansion/compat-aot
artifacts/compat-expansion/compat-aot/Managed.exe artifacts/compat-expansion/compat-aot-results
dotnet publish tests/Pdf -c Release -r win-x64 -p:NativeAotTest=true -p:StripSymbols=true -o artifacts/compat-expansion/pdf-aot
artifacts/compat-expansion/pdf-aot/Pdf.exe artifacts/compat-expansion/pdf-aot-results artifacts/compat-expansion/original/pages.pdf

dotnet build benchmarks/PixelAccess/Original -c Release
dotnet build benchmarks/PixelAccess/Managed -c Release
python tools/benchmark-pixel-access.py
```

新功能目前用 ProjectReference 接入：普通 SK* 使用 `src/SimdSkia.Compatibility/SimdSkia.Compatibility.csproj`；读取 PDF 额外使用 `src/SimdSkia.Pdf/SimdSkia.Pdf.csproj`。不要安装公开 preview.8 后期望这些新增 API 存在。PDF 生成/读取代码见 [Documents 示例](../examples/Documents/Program.cs)、[查看器](../examples/PdfViewer/Program.cs) 和 [模块使用说明](../src/SimdSkia.Pdf/README.md)。

可选独立 PDF 验证需要隔离安装 ReportLab/Pillow/pypdf/PyMuPDF，再运行 `tools/generate-pdf-fixtures.py`、`tools/verify-pdf-independent.py`。普通 C# 测试直接读取仓库夹具，无需这些参考库。原版绘制和 PDF 参考工具始终在独立进程，不参与生产实际绘图。

## 仍需完成

还有 **2343 项公共 API 缺口**、圆角/字体覆盖率差异、完整 hinting/shaping、更多 blend/filter/codec、外部像素所有权及原生对象接口；PDF 矢量导出、文字嵌入及复杂阅读特征也尚未完成。新增 API 仅在已列出的调用与边界上验收，不等于完整行为兼容。

后续发布需使用新版本统一打包，并把可选 PDF 包加入发行验证；当前只做本地 PDF 包内容检查，未触发发布流水线。实际 SDL3/LibVLC 迁移、目标应用内存驻留和多平台 PDF 语料验证仍需以真实应用为验收对象。
