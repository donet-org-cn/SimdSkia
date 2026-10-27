# SimdSkia preview

## 0.1.0-preview.8 更新说明 / What's new

- 通用 x64：SSE2 字形/图像合成与采样、精确填充、字形串缓存及画布分配优化，保留 AVX2/标量回退。
- 本机固定 Avalonia 场景：CPU 吞吐综合耗时低于原版 4.4%，最慢场景落后 3.8%。Vulkan / Direct2D JIT 离屏完成帧含等待与回读低 29.8% / 35.3%；不代表任意场景或窗口呈现。
- 60 FPS 滚动分配 155.14→28.05 KiB/帧，原版 12.87；普通控件工作集 79.89 vs 原版 60.91 MiB，常驻内存未追平。
- 471,569 项精确检查、真实 Avalonia/GPU 功能和 460,050 项真实 AOT 采样通过；原版整页画质通过数仍为 CPU 0/5、GPU 0/12，完整画质未达标，未改判为成功。
- 仍为实验性预览版；字体、完整公共 API、macOS GPU 等限制保留，Avalonia 固定 12.1.4。[完整数据与适用范围](https://github.com/hnlyf168/SimdSkia/blob/main/docs/PARITY-OPTIMIZATION-2026-10-27.md)。

Every package includes `CHANGELOG.md` and NuGet release notes. See the [complete release notes](https://github.com/hnlyf168/SimdSkia/blob/main/docs/RELEASE-NOTES.md) and [final quality/performance report](https://github.com/hnlyf168/SimdSkia/blob/main/docs/ALPHA-COMPATIBILITY-2026-10-26.md) for test hardware, scope and limitations. This release adds precise eight-pixel AVX2 image filtering with scalar fallback and fixes alpha image blending; it does not claim universal renderer parity or lower resident memory.

An independent C# 2D renderer. No Google Skia rendering calls. This release is a **preview**: public API coverage, font support and rendering equivalence are incomplete. It is not a binary replacement for the strong-named SkiaSharp assembly.

Packages target .NET 8 or later:

| Package | Purpose |
| --- | --- |
| SimdSkia | Convenience package for the source compatibility facade |
| SimdSkia.Core | Geometry, commands and pixel storage |
| SimdSkia.Cpu | Scalar/SIMD CPU rasterization and composition |
| SimdSkia.Text | Managed static glyf TrueType parsing and basic Latin/CJK layout |
| SimdSkia.Imaging | Managed PNG/JPEG and supported color conversion |
| SimdSkia.Gpu | Vulkan offscreen rendering; requires the system Vulkan loader/driver |
| SimdSkia.Compatibility | Implemented SkiaSharp namespace / SK* facade |
| SimdSkia.Avalonia | Rendering/font adapter pinned to Avalonia **12.1.4** |
| SimdSkia.Avalonia.Desktop | Windows/Linux X11/macOS OS hosts plus the managed renderer |
| SimdSkia.Avalonia.Gpu | Optional Direct2D native presentation / Vulkan framebuffer drawing |
| SimdSkia.Gpu.Direct2D | Windows hardware Direct2D/D3D11 canvas, C# system API bindings |

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.8
```

An existing Avalonia 12.1.4 application selects the adapter explicitly:

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
```

Windows uses its current message font; Linux/macOS discover supported installed static TrueType families. Explicit `options.FontFiles.Add(path)` overrides automatic selection. Parsing, shaping, glyph coverage and codecs remain C#. Chinese text needs a supported installed Chinese font. Complex shaping, CFF/variable/color fonts and hinting have gaps.

Startup preparation is enabled by default. To disable it for diagnosis, use `.UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)`. It does not hide the window or add a placeholder frame. Keep all directly referenced SimdSkia packages on version `0.1.0-preview.8` and all Avalonia packages on `12.1.4`.

The base adapter provides rendering/font services. The Desktop package supplies OS host selection without Skia/HarfBuzz/ANGLE packages. macOS uses the official MIT libAvaloniaNative window bridge, an explicit window-host exception; application drawing/fonts are managed. The optional GPU adapter defaults Direct2D to a native Windows swapchain; Vulkan currently waits for completion and reads back to the OS framebuffer. macOS GPU/Metal is not implemented. GPU glyphs normally use a managed atlas, and CPU layers/fallbacks are reported. See the [complete quick start](https://github.com/hnlyf168/SimdSkia#readme).

`SimdSkia` and `SimdSkia.Compatibility` expose only the implemented SK* subset. Do not reference them together with the original SkiaSharp types in the same compilation. Avalonia users normally need only the adapter, not the SK* facade.

MIT licensed. Third-party components retain their own licenses and notices, included in the relevant packages. See the repository's [validation and remaining gaps](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VALIDATION.md).
