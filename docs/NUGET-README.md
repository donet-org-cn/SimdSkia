# SimdSkia 0.1.0-preview.10

Requires **.NET 10 / C# 14**. Avalonia remains pinned to **12.1.4**, source facade baseline SkiaSharp **3.119.4**. Keep .NET 8 applications on preview.9 until migrating.

## 更新与测试记录 / Changes and test results

- 完整接入 **Sdcb.SimdCodecs 1.2.0**，替换 JPEG/PNG/GIF 像素编解码，并扩展 WebP/动画、TIFF 等格式；保留颜色、Alpha/stride、预算、严格输入校验及生命周期。允许 .NET 自带原生压缩，仍不调用 Google Skia 绘图。
- **123** 个独立计时进程原版对照，.NET 10.0.13 JIT / Windows x64：CPU 控件 0.891→0.549 ms/帧，JPEG 解码本例快约 2.27–2.90 倍；Vulkan / Direct2D 完成等待及回读综合耗时比为较快原版的 0.678 / 0.624。
- 内存及慢项：CPU 控件 WS 90.39→109.81 MiB、Private 47.36→71.08 MiB；纹理 PNG 解码耗时 2.03 倍、UI PNG 编码慢约 19%。JPEG 编码较快但 UI 文件大约增加 52%，不是相同码率效率比较。
- 画质仍未通过：CPU 0/3、GPU 0/12；JPEG 联合回读 3/24 失败，最大差 2 / 原门槛 1，未放宽阈值。预览发布例外显式记录三项已知 JPEG 差异，不把 CI 发布通过当作画质通过。
- 本地编解码集成 1,416、标量 1,416、真实 AOT 1,417、精确优化 471,569、真实控件 9,750 项通过。继承 preview.9 的 GetPixels、GIF/SKCodec、圆角/图层/采样、系统字体和受限 PDF 功能。

Every package includes `CHANGELOG.md`, usage README, NuGet release notes and licenses. The [full measured report](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/SKIA-COMPARISON-2026-11-01.md) contains CPU, memory, charts, quality failures and reproduction commands; see [release status](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/NUGET-PREVIEW.md) and [complete changelog](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/RELEASE-NOTES.md). Measurements are local JIT workloads, not universal rendering/AOT/presentation parity.

An independent C# 2D renderer. No Google Skia rendering calls. This release is a **preview**: public API coverage, font support and rendering equivalence are incomplete. It is not a binary replacement for the strong-named SkiaSharp assembly.

Packages target .NET 10 or later:

| Package | Purpose |
| --- | --- |
| SimdSkia | Convenience package for the source compatibility facade |
| SimdSkia.Core | Geometry, commands and pixel storage |
| SimdSkia.Cpu | Scalar/SIMD CPU rasterization and composition |
| SimdSkia.Text | Managed static glyf TrueType parsing and basic Latin/CJK layout |
| SimdSkia.Imaging | Managed Sdcb codecs with validated input, supported colour conversion and permitted BCL compression |
| SimdSkia.Gpu | Vulkan offscreen rendering; requires the system Vulkan loader/driver |
| SimdSkia.Compatibility | Implemented SkiaSharp namespace / SK* facade |
| SimdSkia.Avalonia | Rendering/font adapter pinned to Avalonia **12.1.4** |
| SimdSkia.Avalonia.Desktop | Windows/Linux X11/macOS OS hosts plus the managed renderer |
| SimdSkia.Avalonia.Gpu | Optional Direct2D native presentation / Vulkan framebuffer drawing |
| SimdSkia.Gpu.Direct2D | Windows hardware Direct2D/D3D11 canvas, C# system API bindings |
| SimdSkia.Pdf | Optional managed PDF parsing/rendering; subset only, pinned PdfPig 0.1.16 |

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.10
```

An existing Avalonia 12.1.4 application selects the adapter explicitly:

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
```

Windows uses its current message font; Linux/macOS discover supported installed static TrueType families. Explicit `options.FontFiles.Add(path)` overrides automatic selection. Parsing, shaping, glyph coverage and codecs remain C#. Chinese text needs a supported installed Chinese font. Complex shaping, CFF/variable/color fonts and hinting have gaps.

Startup preparation is enabled by default. To disable it for diagnosis, use `.UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)`. It does not hide the window or add a placeholder frame. Keep all directly referenced SimdSkia packages on version `0.1.0-preview.10` and all Avalonia packages on `12.1.4`.

The base adapter provides rendering/font services. The Desktop package supplies OS host selection without Skia/HarfBuzz/ANGLE packages. macOS uses the official MIT libAvaloniaNative window bridge, an explicit window-host exception; application drawing/fonts are managed. The optional GPU adapter defaults Direct2D to a native Windows swapchain; Vulkan currently waits for completion and reads back to the OS framebuffer. macOS GPU/Metal is not implemented. GPU glyphs normally use a managed atlas, and CPU layers/fallbacks are reported. See the [complete quick start](https://github.com/donet-org-cn/SimdSkia#readme).

`SimdSkia` and `SimdSkia.Compatibility` expose only the implemented SK* subset. Do not reference them together with the original SkiaSharp types in the same compilation. Avalonia users normally need only the adapter, not the SK* facade.

MIT licensed. Third-party components retain their own licenses and notices, included in the relevant packages. See the repository's [validation and remaining gaps](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/VALIDATION.md).
