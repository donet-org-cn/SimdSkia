# SimdSkia 0.1.3 正式版：GPU 优先与文字缓存优化

从0.1.2升级：桌面默认GPU优先，Windows Direct2D原生呈现、Linux Vulkan、诊断后回退CPU；macOS窗口仍CPU。`.UseSimdSkiaDesktop(options => options.UseCpu())` 可保留CPU。新增两帧完成队列、状态缓存、跨帧回退像素修复、文字缓存共享计费/不可变遮罩/LRU及Vulkan有序合批。动态中文自身GPU耗时下降81%–93%，分配降至约168–169B/帧，详细CPU/内存和画质限制见[0.1.3发行说明](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/RELEASE-0.1.3.md)及包内CHANGELOG.md。Avalonia仍>=12.1.0且<13.0.0；原版文字/曲线/整页画质、JPEG及部分性能/内存差距保留，整体目标未全部达到。第三方依赖保持可选 PdfPig 0.1.16、Sdcb.SimdCodecs1.4.0与Clipper2 2.0.0，公开状态以发布记录为准。

## 0.1.2 历史累计功能

固定SkiaSharp3.119.4基线，892→3230/3235项、累计新增2338项公共元数据签名，63→201类型；按约定保留5项原生合同缺口。扩展路径/描边/渐变/采样/滤镜/Blender/RuntimeEffect子集、Picture/网格、真实Alpha8/浮点颜色、LCD/中文点阵、SVG/XPS/SKP与PDF注释。增加GL/GLES/D3D12/Metal核心及GPU资源互操作，优化Vulkan设备复制/矩形合批/完成读回、SIMD/有界缓存/分配；修复MoveTo、SaveLayer混合范围和纹理生命周期。

Avalonia接受>=12.1.0且<13.0.0，已验证宿主12.1.0–12.1.5。默认SK*解码校正为N32/预乘，直接读取像素的应用应检查ColorType/AlphaType/RowBytes。依赖保持Sdcb.SimdCodecs1.4.0/Clipper2 2.0.0/可选 PdfPig 0.1.16。Metal无Mac GPU实机验证；JPEG、曲线/文字/整页画质、内存及部分GPU/API性能仍有差距。正式版按已实现子集发布，不是全面行为或二进制替换保证；GPU对象要求创建线程显式Dispose。

完整累计更新、性能记录、限制与升级方法见包内CHANGELOG.md和[0.1.2发行说明](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/RELEASE-0.1.2.md)。原始测量按快照保留，不以正式版本号改写画质结果。

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
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.3
```

An existing Avalonia 12.1.5 application selects the adapter explicitly:

```csharp
using SimdSkia.Avalonia;

AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
```

Windows uses its current message font; Linux/macOS discover supported installed static TrueType families. Explicit `options.FontFiles.Add(path)` overrides automatic selection. Parsing, shaping, glyph coverage and codecs remain C#. Chinese text needs a supported installed Chinese font. Complex shaping, CFF/variable/color fonts and hinting have gaps.

Startup preparation is enabled by default. To disable it for diagnosis, use `.UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)`. It does not hide the window or add a placeholder frame. Keep all directly referenced SimdSkia packages on version `0.1.3` and all Avalonia packages on `12.1.5`.

The base adapter provides rendering/font services. The Desktop package supplies OS host selection without Skia/HarfBuzz/ANGLE packages. macOS uses the official MIT libAvaloniaNative window bridge, an explicit window-host exception; application drawing/fonts are managed. The optional GPU adapter defaults Direct2D to a native Windows swapchain; Vulkan currently waits for completion and reads back to the OS framebuffer. The Metal core is unverified and is not wired into Avalonia presentation. GPU glyphs normally use a managed atlas, and CPU layers/fallbacks are reported. See the [complete quick start](https://github.com/donet-org-cn/SimdSkia#readme).

`SimdSkia` and `SimdSkia.Compatibility` expose only the implemented SK* subset. Do not reference them together with the original SkiaSharp types in the same compilation. Avalonia users normally need only the adapter, not the SK* facade.

MIT licensed. Third-party components retain their own licenses and notices, included in the relevant packages. See the repository's [validation and remaining gaps](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/VALIDATION.md).
## Standalone SkiaSharp namespace usage

```shell
dotnet add package SimdSkia --version 0.1.3
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
