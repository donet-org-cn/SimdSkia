# 0.1.2-preview.3 更新

本版修复连续MoveTo点序列和特殊混合模式的SaveLayer范围；原版几何300组/4636值、464组图层/2,375,680字节精确对照通过。PDF依赖说明与项目保持一致，固定使用 PdfPig 0.1.16。

3230/3235项固定基线，保留5项原生合同缺口；实际SVG/XPS/SKP、Alpha8、浮点颜色与GPU资源互操作。Vulkan合批/设备复制/完成读回优化；Metal无Mac GPU，尚未实机验证。Avalonia接受>=12.1.0且<13.0.0，已验证宿主12.1.0–12.1.5。JPEG、文字/曲线/整页画质及性能/内存仍有差距。Sdcb.SimdCodecs保持1.4.0；.NET系统压缩与macOS官方窗口宿主是已允许的原生例外。完整测试、CPU/GPU/内存性能及兼容限制见包内CHANGELOG.md和[本版报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/PUBLIC-API-FINAL-2026-11-07.md)。


Packages target .NET 10 or later:

| Package | Purpose |
| --- | --- |
| SimdSkia | Convenience package for the source compatibility facade |
| SimdSkia.Core | Geometry, commands and pixel storage |
| SimdSkia.Cpu | Scalar/SIMD CPU rasterization and composition |
| SimdSkia.Text | Managed static glyf TrueType parsing and basic Latin/CJK layout |
| SimdSkia.Imaging | Managed Sdcb codecs with validated input, supported colour conversion and permitted BCL compression |
| SimdSkia.Gpu | Vulkan and GL/GLES; experimental Metal core without hardware validation; requires system APIs/drivers |
| SimdSkia.Compatibility | Implemented SkiaSharp namespace / SK* facade |
| SimdSkia.Avalonia | Rendering/font adapter for Avalonia **>=12.1.0 and <13.0.0** |
| SimdSkia.Avalonia.Desktop | Windows/Linux X11/macOS OS hosts plus the managed renderer |
| SimdSkia.Avalonia.Gpu | Optional Direct2D native presentation / Vulkan framebuffer drawing |
| SimdSkia.Gpu.Direct2D | Windows hardware Direct2D/D3D11 canvas, C# system API bindings |
| SimdSkia.Pdf | Optional managed PDF parsing/rendering; subset only, pinned PdfPig 0.1.16 |

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.2-preview.3
```

An existing Avalonia 12.1.5 application selects the adapter explicitly:

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
```

Windows uses its current message font; Linux/macOS discover supported installed static TrueType families. Explicit `options.FontFiles.Add(path)` overrides automatic selection. Parsing, shaping, glyph coverage and codecs remain C#. Chinese text needs a supported installed Chinese font. Complex shaping, CFF/variable/color fonts and hinting have gaps.

Startup preparation is enabled by default. To disable it for diagnosis, use `.UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)`. It does not hide the window or add a placeholder frame. Keep all directly referenced SimdSkia packages on version `0.1.2-preview.3` and all Avalonia packages on `12.1.5`.

The base adapter provides rendering/font services. The Desktop package supplies OS host selection without Skia/HarfBuzz/ANGLE packages. macOS uses the official MIT libAvaloniaNative window bridge, an explicit window-host exception; application drawing/fonts are managed. The optional GPU adapter defaults Direct2D to a native Windows swapchain; Vulkan currently waits for completion and reads back to the OS framebuffer. The Metal core is unverified and is not wired into Avalonia presentation. GPU glyphs normally use a managed atlas, and CPU layers/fallbacks are reported. See the [complete quick start](https://github.com/donet-org-cn/SimdSkia#readme).

`SimdSkia` and `SimdSkia.Compatibility` expose only the implemented SK* subset. Do not reference them together with the original SkiaSharp types in the same compilation. Avalonia users normally need only the adapter, not the SK* facade.

MIT licensed. Third-party components retain their own licenses and notices, included in the relevant packages. See the repository's [validation and remaining gaps](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/VALIDATION.md).
## Standalone SkiaSharp namespace usage

```shell
dotnet add package SimdSkia --version 0.1.2-preview.3
```

```csharp
using SkiaSharp;
using var bitmap = new SKBitmap(160, 100);
using var canvas = new SKCanvas(bitmap);
canvas.Clear(SKColors.White);
using var paint = new SKPaint { Color = SKColors.Blue };
canvas.DrawRect(10, 10, 80, 50, paint);
if (bitmap.GetPixel(20, 20) != SKColors.Blue)
    throw new Exception("Pixel validation failed.");
using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
using var file = File.Create("rectangle.png");
png.SaveTo(file);
```

The facade targets the documented SkiaSharp 3.119.4 subset. The five native contracts remain missing, and rendering/behavior parity is incomplete. Dispose GPU resources explicitly on their creating thread; GC finalization equivalence is not established.
