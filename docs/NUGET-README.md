# SimdSkia preview

Preview 5 adds bounded Vulkan coverage caching, conservative edge bins, specialized compute pipelines, direct BGRA framebuffer output, overlap-aware barriers and optional GPU timestamp diagnostics. The measured controls window improved from 22.250 to 2.316 ms on a GTX 1050 Ti; CPU was 2.067 ms and memory did not improve proportionally. [Vulkan performance, memory and validation report](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VULKAN-OPTIMIZATION-2026-10-22.md) retains failed native pixel quality gates; full compatibility is still incomplete.

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
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.5
```

An existing Avalonia 12.1.4 application selects the adapter explicitly:

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
```

Windows uses its current message font; Linux/macOS discover supported installed static TrueType families. Explicit `options.FontFiles.Add(path)` overrides automatic selection. Parsing, shaping, glyph coverage and codecs remain C#. Chinese text needs a supported installed Chinese font. Complex shaping, CFF/variable/color fonts and hinting have gaps.

The base adapter provides rendering/font services. The Desktop package supplies OS host selection without Skia/HarfBuzz/ANGLE packages. macOS uses the official MIT libAvaloniaNative window bridge, an explicit window-host exception; application drawing/fonts are managed. The optional GPU adapter defaults Direct2D to a native Windows swapchain; Vulkan currently waits for completion and reads back to the OS framebuffer. macOS GPU/Metal is not implemented. GPU glyphs normally use a managed atlas, and CPU layers/fallbacks are reported. See the [complete quick start](https://github.com/hnlyf168/SimdSkia#readme).

`SimdSkia` and `SimdSkia.Compatibility` expose only the implemented SK* subset. Do not reference them together with the original SkiaSharp types in the same compilation. Avalonia users normally need only the adapter, not the SK* facade.

MIT licensed. Third-party components retain their own licenses and notices, included in the relevant packages. See the repository's [validation and remaining gaps](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VALIDATION.md).
