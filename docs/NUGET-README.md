# SimdSkia preview

Preview 6 includes managed system-font paging and shared TTC storage, reusable raster buffers, exact SIMD bilinear sampling, image-brush cache lifetime fixes, geometry bounds reuse and reduced glyph coverage copying. Classic desktop JIT applications prepare common drawing paths on a short-lived background thread; headless applications and Native AOT skip preparation. Windows startup white intervals were reduced but not eliminated. The latest round did not materially improve steady GPU throughput or resident memory. See the [startup, CPU/GPU, memory and AOT report](https://github.com/hnlyf168/SimdSkia/blob/main/docs/STARTUP-ROUND7-2026-10-22.md), including original renderer comparisons and remaining quality failures.

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
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.6
```

An existing Avalonia 12.1.4 application selects the adapter explicitly:

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
```

Windows uses its current message font; Linux/macOS discover supported installed static TrueType families. Explicit `options.FontFiles.Add(path)` overrides automatic selection. Parsing, shaping, glyph coverage and codecs remain C#. Chinese text needs a supported installed Chinese font. Complex shaping, CFF/variable/color fonts and hinting have gaps.

Startup preparation is enabled by default. To disable it for diagnosis, use `.UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)`. It does not hide the window or add a placeholder frame. Keep all directly referenced SimdSkia packages on version `0.1.0-preview.6` and all Avalonia packages on `12.1.4`.

The base adapter provides rendering/font services. The Desktop package supplies OS host selection without Skia/HarfBuzz/ANGLE packages. macOS uses the official MIT libAvaloniaNative window bridge, an explicit window-host exception; application drawing/fonts are managed. The optional GPU adapter defaults Direct2D to a native Windows swapchain; Vulkan currently waits for completion and reads back to the OS framebuffer. macOS GPU/Metal is not implemented. GPU glyphs normally use a managed atlas, and CPU layers/fallbacks are reported. See the [complete quick start](https://github.com/hnlyf168/SimdSkia#readme).

`SimdSkia` and `SimdSkia.Compatibility` expose only the implemented SK* subset. Do not reference them together with the original SkiaSharp types in the same compilation. Avalonia users normally need only the adapter, not the SK* facade.

MIT licensed. Third-party components retain their own licenses and notices, included in the relevant packages. See the repository's [validation and remaining gaps](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VALIDATION.md).
