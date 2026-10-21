# SimdSkia preview

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

```shell
dotnet add package SimdSkia.Avalonia --version 0.1.0-preview.1
```

An existing Avalonia 12.1.4 application selects the adapter explicitly:

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseStandardRuntimePlatformSubsystem()
    .UseWin32()
    .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
    .UseSimdSkia()
    .StartWithClassicDesktopLifetime(args);
```

Windows uses its current system message font and installed static TrueType fallback fonts. Font parsing, shaping and rendering remain C#. Explicit `options.FontFiles.Add(path)` is supported and required for other operating systems. Missing/unsupported font formats fail explicitly. Chinese text requires an installed Chinese font. Complex shaping, CFF/variable/color fonts and hinting are not fully implemented.

The adapter does not install a native windowing host. Keep the appropriate Avalonia host in the application. To avoid shipping Skia/HarfBuzz/ANGLE assets on Windows, remove Avalonia.Desktop and use Avalonia/Fluent 12.1.4 plus the managed Win32 assemblies as shown in the repository's [integration guide](https://github.com/hnlyf168/SimdSkia/blob/main/docs/AVALONIA-INTEGRATION.md). Avalonia Windows rendering currently uses the CPU framebuffer backend; the Vulkan package does not provide an Avalonia window swapchain.

`SimdSkia` and `SimdSkia.Compatibility` expose only the implemented SK* subset. Do not reference them together with the original SkiaSharp types in the same compilation. Avalonia users normally need only the adapter, not the SK* facade.

MIT licensed. Third-party components retain their own licenses and notices, included in the relevant packages. See the repository's [validation and remaining gaps](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VALIDATION.md).
