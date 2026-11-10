# 全面回归与性能验收记录（2026-11-10）

本批为工作区源码优化，正式 NuGet 为 0.1.4。Windows x64 / .NET 10.0.14，原版 SkiaSharp 3.119.4，控件使用 Avalonia 12.1.5。[完整性能、CPU 与内存报告](PERFORMANCE-2026-11-10.md)。

## 执行状态

Release 解决方案构建通过，0 警告、0 错误。有效广泛回归共 82 个命令，56 个通过，25 个原版行为或画质对照返回失败，另 1 个 API 审计因约定的 5 项缺口返回 2。各项原始退出码与命令见 [测试目录](evidence/performance-2026-11-10/validation/)。

通用扫描曾额外调用一次需要专用参数的冻结回放工具，返回 usage 错误。该日志保留，扫描器已分离该入口，随后以完整参数完成 17 个套件回放。有效测试统计单独列出这次工具调用错误。

## 实现侧正确性

| 检查 | 实际结果 |
| --- | --- |
| 位图→画布→清屏→矩形→逐像素、异常与生命周期 | 36 项通过 |
| 路径、描边、AA、裁剪、渐变、中文与 PNG | 13 项通过 |
| 独立图像、颜色、损坏输入与压缩 | 12,410 项通过 |
| 冻结标量渲染器逐字节、stride、缓存淘汰与修改 | 474,413 项通过 |
| 浮点 paint / varying shader，RGB/BGR、alpha、尾部、NaN、越界 RGB | 5,888 项通过 |
| 编解码、输入和像素所有权、长期存活、穷举预乘 | 135,498 项通过 |
| PDF 生成与外部读取 | 22 项通过 |
| Avalonia 文字链、缓存与生命周期 | 596 项通过 |
| 真实 Avalonia 控件、像素、几何、排版、SIMD 与生命周期 | 13,853 项通过 |
| Vulkan 优化路径 / 独立未优化路径 | 599 项通过，含 588 次精确像素比较 |
| Direct2D 实际硬件 | 246,418 项通过 |
| Vulkan / Direct2D、RGBA / BGRA GPU 集成 | 12,174 项通过 |

关闭 AVX2、关闭全部硬件指令后，分别重跑浮点/着色器、编解码与完整优化回归，六个命令全部通过。资源预算、并发清理、系统字体、字形批处理及真实 GPU 初始化也通过。

## Native AOT

| 程序 | 实际执行 |
| --- | --- |
| 采样 portable x64 | 460,050 项，NativeAot=True，AVX2=False / SSE41=False |
| 采样 x86-64-v3 | 460,050 项，NativeAot=True |
| runtime / mesh / patch / 滤镜 / 路径 / 缓存 / PDF | 38 项通过，另跑标量模式，Perlin RGBA 帧完全相同 |
| 编解码 | 135,499 项通过，NativeAot=True |
| 实际 Win32 窗口 | 60 个测量进程完成，逐个验证 PE 与 CLR/JIT 模块，修改前后固定帧相同 |

7 个原生 EXE 的 PE 校验通过，CLR 目录均为 0。修改前后 EXE 哈希不同，ILC 输入及来源记录保留在 [原始证据](evidence/performance-2026-11-10/)。

## 原版差异

公共签名匹配 3230/3235，0 个额外签名。原生句柄与原生版本相关的 5 项合同按用户约定列为缺口。

- `method SkiaSharp.SkiaSharpVersion.CheckNativeLibraryCompatible<>(throwIfIncompatible:System.Boolean:in=False:out=False:optional=True:default=False:requiredmods=):System.Boolean;static=True;virtual=False;abstract=False;constraints=`
- `property SkiaSharp.SKObject.Handle[]:System.IntPtr;get=public,static=False,virtual=True;set=protected,static=False,virtual=True`
- `property SkiaSharp.SkiaSharpVersion.NativeMinimum[]:System.Version;get=public,static=True,virtual=False;set=-`
- `property SkiaSharp.SkiaSharpVersion.Native[]:System.Version;get=public,static=True,virtual=False;set=-`
- `type SkiaSharp.SkiaSharpVersion;kind=class;base=System.Object;abstract=True;sealed=True;interfaces=;layout=Auto,size=0,pack=0;constraints=`

以下 25 个原版对照命令返回 2，原始门槛与具体差异保留在各自 comparison/result 文件。聚合入口包含部分重复子套件。

| 套件 | 记录 |
| --- | --- |
| check-alpha-compatibility | [原始日志](evidence/performance-2026-11-10/validation/check-alpha-compatibility.log) |
| check-api99-batch | [原始日志](evidence/performance-2026-11-10/validation/check-api99-batch.log) |
| check-blends | [原始日志](evidence/performance-2026-11-10/validation/check-blends.log) |
| check-canvas-operations | [原始日志](evidence/performance-2026-11-10/validation/check-canvas-operations.log) |
| check-canvas-queries | [原始日志](evidence/performance-2026-11-10/validation/check-canvas-queries.log) |
| check-conic-compatibility | [原始日志](evidence/performance-2026-11-10/validation/check-conic-compatibility.log) |
| check-embedded-bitmaps | [原始日志](evidence/performance-2026-11-10/validation/check-embedded-bitmaps.log) |
| check-final-paint | [原始日志](evidence/performance-2026-11-10/validation/check-final-paint.log) |
| check-gl-interop | [原始日志](evidence/performance-2026-11-10/validation/check-gl-interop.log) |
| check-image-filters | [原始日志](evidence/performance-2026-11-10/validation/check-image-filters.log) |
| check-image-operations | [原始日志](evidence/performance-2026-11-10/validation/check-image-operations.log) |
| check-jpeg-parity | [原始日志](evidence/performance-2026-11-10/validation/check-jpeg-parity.log) |
| check-mask-filters | [原始日志](evidence/performance-2026-11-10/validation/check-mask-filters.log) |
| check-path-boolean | [原始日志](evidence/performance-2026-11-10/validation/check-path-boolean.log) |
| check-path-effects | [原始日志](evidence/performance-2026-11-10/validation/check-path-effects.log) |
| check-pictures | [原始日志](evidence/performance-2026-11-10/validation/check-pictures.log) |
| check-pixel-compatibility | [原始日志](evidence/performance-2026-11-10/validation/check-pixel-compatibility.log) |
| check-raster-shaders | [原始日志](evidence/performance-2026-11-10/validation/check-raster-shaders.log) |
| check-skp-documents | [原始日志](evidence/performance-2026-11-10/validation/check-skp-documents.log) |
| check-stroke-compatibility | [原始日志](evidence/performance-2026-11-10/validation/check-stroke-compatibility.log) |
| check-svg-documents | [原始日志](evidence/performance-2026-11-10/validation/check-svg-documents.log) |
| check-text-blobs | [原始日志](evidence/performance-2026-11-10/validation/check-text-blobs.log) |
| foundations-FontOperations | [原始日志](evidence/performance-2026-11-10/validation/foundations-FontOperations.log) |
| foundations-Regions | [原始日志](evidence/performance-2026-11-10/validation/foundations-Regions.log) |
| interop-VulkanInterop | [原始日志](evidence/performance-2026-11-10/validation/interop-VulkanInterop.log) |

JPEG 严格编码门槛为 1 LSB，结果 21/24，通过例外授权的 3 项蓝通道差异也按失败记录。GPU 整页严格画质 0/12，AOT 整页 0/10。CPU 的图像采样、部分文字/曲线与透明叠加也有原版差异，详细像素误差列于性能表。

本批选取其中 17 个相关套件，以相同测试程序和完整冻结的旧引擎回放，561 个输出文件精确相同。初轮遗漏旧兼容层和 SKP 交叉输入的回放已作废，最终回放使用完整模块及完整参数。

## 性能采集完整性

最终性能包含 294 个工作进程，CPU 基础 12、CPU API 9、编解码 90、Vulkan 微场景 12、GPU 控件页 54、AOT 窗口 60、长期图片内存 45、GPU 中文加长复测 12。编码捕获、交叉解码与上述正确性测试另计。

所有计时串行，同尺寸、格式、字体及输入。GPU 包含提交后的完成等待和主机读回。原版测试运行于隔离程序，产品实现使用 C# 及系统 GPU API。

两处旧基准的默认像素格式假设已修正为显式 RGBA8888/Unpremul，编解码修改前程序重新链接冻结旧引擎。新增16图长期持有及混合尺寸测试，验证解码转移的容量保护。

## 范围与复现

本轮 Windows x64 CPU、Vulkan、Direct2D 和真实 AOT 窗口完成实测。Linux、macOS、ARM64、Android GPU、iOS、Metal 的本轮实机复测待补。

```powershell
dotnet build SimdSkia.sln -c Release -p:UsePackedSimdSkia=false
python tools/run-performance-validation.py --output artifacts/validation-fresh
python tools/verify-final-evidence.py --directory docs/evidence/performance-2026-11-10
```

原版对照失败时，聚合验证返回 2。完整命令与结果见 [证据索引](evidence/performance-2026-11-10/)，性能图表与各场景 CPU/内存见 [性能报告](PERFORMANCE-2026-11-10.md)。
