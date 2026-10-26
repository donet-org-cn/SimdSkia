# SimdSkia preview

## 0.1.0-preview.7 更新说明 / What's new

- CPU：新增精确八像素 AVX2 双线性图片采样，保留 SSE/标量及通用 Native AOT 回退。
- 画质：对齐固定 SkiaSharp 3.119.4 x64 图像 SourceOver 舍入，修复非预乘/预乘半透明图片；统一现代最近邻、线性采样、原尺寸拷贝和裁剪层回贴。同步 Vulkan 合成与 CoverageClip 采样分流修复。
- 实测：i7-8700K / Windows x64，1024×768 四类线性采样场景为 **5.412–6.758 ms**，原版 **6.804–7.714 ms**；核心图片质量 **24/24** 通过原门槛。仅代表指定 CPU 微基准，不能推断全部 Avalonia/GPU 都更快。
- 回归：468,137 项精确 SIMD/标量检查通过；真实控件、Direct2D、Vulkan 和通用/v3 AOT 已验证。
- 限制：常驻内存没有明显改善，完整字体/路径/整页质量、公共 API 仍有缺口，macOS GPU 未实现。Avalonia 仍锁定 **12.1.4**。

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
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.7
```

An existing Avalonia 12.1.4 application selects the adapter explicitly:

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
```

Windows uses its current message font; Linux/macOS discover supported installed static TrueType families. Explicit `options.FontFiles.Add(path)` overrides automatic selection. Parsing, shaping, glyph coverage and codecs remain C#. Chinese text needs a supported installed Chinese font. Complex shaping, CFF/variable/color fonts and hinting have gaps.

Startup preparation is enabled by default. To disable it for diagnosis, use `.UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)`. It does not hide the window or add a placeholder frame. Keep all directly referenced SimdSkia packages on version `0.1.0-preview.7` and all Avalonia packages on `12.1.4`.

The base adapter provides rendering/font services. The Desktop package supplies OS host selection without Skia/HarfBuzz/ANGLE packages. macOS uses the official MIT libAvaloniaNative window bridge, an explicit window-host exception; application drawing/fonts are managed. The optional GPU adapter defaults Direct2D to a native Windows swapchain; Vulkan currently waits for completion and reads back to the OS framebuffer. macOS GPU/Metal is not implemented. GPU glyphs normally use a managed atlas, and CPU layers/fallbacks are reported. See the [complete quick start](https://github.com/hnlyf168/SimdSkia#readme).

`SimdSkia` and `SimdSkia.Compatibility` expose only the implemented SK* subset. Do not reference them together with the original SkiaSharp types in the same compilation. Avalonia users normally need only the adapter, not the SK* facade.

MIT licensed. Third-party components retain their own licenses and notices, included in the relevant packages. See the repository's [validation and remaining gaps](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VALIDATION.md).
