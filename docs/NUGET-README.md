# SimdSkia 0.1.0

.NET 10 / C# 14；Avalonia **12.1.5**、Sdcb.SimdCodecs **1.4.0**。正式版本支持文档列出的已实现功能子集，SkiaSharp 源码基线 3.119.4；不代表完整 API、原版像素画质或强名称二进制替换。性能、CPU/内存和画质见包内 CHANGELOG 及[0.1.0 报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/STABLE-0.1.0-2026-11-04.md)。

PNG 使用无损样本缩减，保留透明隐藏 RGB 和颜色规则；IDAT 严格检查以固定生命周期的只读流替代整块压缩复制。新增分片/空块/偏移输入/并发 GC 回归。JPEG 及整页原版画质差异仍披露，未放宽原门槛。PNG/TIFF 允许 .NET 自带原生压缩，仍不调用 Google Skia 绘图。

An independent C# 2D renderer. No Google Skia rendering calls. This stable 0.1 release supports the documented implemented subset. Full SkiaSharp API coverage, font support and native rendering equivalence remain incomplete. It is not a binary replacement for the strong-named SkiaSharp assembly.

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
| SimdSkia.Avalonia | Rendering/font adapter pinned to Avalonia **12.1.5** |
| SimdSkia.Avalonia.Desktop | Windows/Linux X11/macOS OS hosts plus the managed renderer |
| SimdSkia.Avalonia.Gpu | Optional Direct2D native presentation / Vulkan framebuffer drawing |
| SimdSkia.Gpu.Direct2D | Windows hardware Direct2D/D3D11 canvas, C# system API bindings |
| SimdSkia.Pdf | Optional managed PDF parsing/rendering; subset only, pinned PdfPig 0.1.16 |

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0
```

An existing Avalonia 12.1.5 application selects the adapter explicitly:

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
```

Windows uses its current message font; Linux/macOS discover supported installed static TrueType families. Explicit `options.FontFiles.Add(path)` overrides automatic selection. Parsing, shaping, glyph coverage and codecs remain C#. Chinese text needs a supported installed Chinese font. Complex shaping, CFF/variable/color fonts and hinting have gaps.

Startup preparation is enabled by default. To disable it for diagnosis, use `.UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)`. It does not hide the window or add a placeholder frame. Keep all directly referenced SimdSkia packages on version `0.1.0` and all Avalonia packages on `12.1.5`.

The base adapter provides rendering/font services. The Desktop package supplies OS host selection without Skia/HarfBuzz/ANGLE packages. macOS uses the official MIT libAvaloniaNative window bridge, an explicit window-host exception; application drawing/fonts are managed. The optional GPU adapter defaults Direct2D to a native Windows swapchain; Vulkan currently waits for completion and reads back to the OS framebuffer. macOS GPU/Metal is not implemented. GPU glyphs normally use a managed atlas, and CPU layers/fallbacks are reported. See the [complete quick start](https://github.com/donet-org-cn/SimdSkia#readme).

`SimdSkia` and `SimdSkia.Compatibility` expose only the implemented SK* subset. Do not reference them together with the original SkiaSharp types in the same compilation. Avalonia users normally need only the adapter, not the SK* facade.

MIT licensed. Third-party components retain their own licenses and notices, included in the relevant packages. See the repository's [validation and remaining gaps](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/VALIDATION.md).
