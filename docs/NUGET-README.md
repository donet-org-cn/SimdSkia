# SimdSkia preview

## 0.1.0-preview.9 更新说明 / What's new

- 补齐 `SKBitmap.GetPixels` 真实像素指针、`SKTypeface.FromFamilyName`、GIF `SKCodec`、`SKRoundRect`、`SaveLayer`、`Inflate` 和采样重载；公共条目匹配 892/3235，仍非全面兼容或二进制替换。
- 新增多页栅格 PDF 导出和可选 `SimdSkia.Pdf` 阅读/页面绘制；普通 Compatibility/Avalonia 引用不引入 PdfPig。导出无矢量/可搜索文字，阅读器仅支持明确子集，超出支持范围报错。
- 修复 VelaShell 字体 Try 回退和图片布局浮点边界；9,750 控件回归通过，CPU/Direct2D/Vulkan 各 23/23 常规场景通过。深层页面仍有失败/超时，组合字符和 Skia 专属预览仍有缺口。
- 本轮没有重测完整 Avalonia CPU/GPU 性能及常驻内存；之前 preview.8 的性能报告是历史测量，不能当作 preview.9 成绩。独立带 stride 像素复制 0.5907 vs 原版 0.5921 ms/帧，仅说明该热路径基本持平。
- 圆角、文字、整页画质和完整字体仍未通过全面验收；Avalonia 固定 12.1.4、SkiaSharp 源码基线 3.119.4。12 个包统一版本，保持实验性预览定位。

Every package includes `CHANGELOG.md` and NuGet release notes. See the [complete release notes](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/RELEASE-NOTES.md), [standalone/PDF report](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/STANDALONE-COMPATIBILITY-2026-10-27.md) and [VelaShell compatibility report](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/VELASHELL-COMPATIBILITY-2026-10-27.md) for measured results and remaining failures. This release expands the source facade and fixes real integration defects; it does not claim full application replacement, general PDF support or renderer performance parity.

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
| SimdSkia.Pdf | Optional managed PDF parsing/rendering; subset only, pinned PdfPig 0.1.16 |

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.9
```

An existing Avalonia 12.1.4 application selects the adapter explicitly:

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
```

Windows uses its current message font; Linux/macOS discover supported installed static TrueType families. Explicit `options.FontFiles.Add(path)` overrides automatic selection. Parsing, shaping, glyph coverage and codecs remain C#. Chinese text needs a supported installed Chinese font. Complex shaping, CFF/variable/color fonts and hinting have gaps.

Startup preparation is enabled by default. To disable it for diagnosis, use `.UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)`. It does not hide the window or add a placeholder frame. Keep all directly referenced SimdSkia packages on version `0.1.0-preview.9` and all Avalonia packages on `12.1.4`.

The base adapter provides rendering/font services. The Desktop package supplies OS host selection without Skia/HarfBuzz/ANGLE packages. macOS uses the official MIT libAvaloniaNative window bridge, an explicit window-host exception; application drawing/fonts are managed. The optional GPU adapter defaults Direct2D to a native Windows swapchain; Vulkan currently waits for completion and reads back to the OS framebuffer. macOS GPU/Metal is not implemented. GPU glyphs normally use a managed atlas, and CPU layers/fallbacks are reported. See the [complete quick start](https://github.com/donet-org-cn/SimdSkia#readme).

`SimdSkia` and `SimdSkia.Compatibility` expose only the implemented SK* subset. Do not reference them together with the original SkiaSharp types in the same compilation. Avalonia users normally need only the adapter, not the SK* facade.

MIT licensed. Third-party components retain their own licenses and notices, included in the relevant packages. See the repository's [validation and remaining gaps](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/VALIDATION.md).
