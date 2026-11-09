# SimdSkia 0.1.4 Android NuGet 接入

十三包统一0.1.4正式版，新增SimdSkia.Avalonia.Android，固定Avalonia12.1.5，使用C# CPU/通用SIMD。此前Android16 ARM64源码真机通过中文、触摸、输入、动画和前后台恢复，新增包另做独立缓存Release/Mono AOT消费构建。完整使用方法、字体和主题配置见[Android说明](https://github.com/donet-org-cn/SimdSkia/blob/main/examples/Mobile.Android/README.md)，更新与验收见[0.1.4发行说明](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/RELEASE-0.1.4.md)和包内CHANGELOG.md。iOS呈现适配、Android GPU及可变/CFF字体待完善。桌面版本矩阵12.1.0–12.1.5，既有画质/性能差距见报告。可选PDF依赖为PdfPig 0.1.16。

# SimdSkia 0.1.3 正式版：GPU 优先与文字缓存优化

从0.1.2升级：桌面默认GPU优先，Windows Direct2D原生呈现、Linux Vulkan、诊断后回退CPU；macOS窗口仍CPU。`.UseSimdSkiaDesktop(options => options.UseCpu())` 可保留CPU。新增两帧完成队列、状态缓存、跨帧回退像素修复、文字缓存共享计费/不可变遮罩/LRU及Vulkan有序合批。动态中文自身GPU耗时下降81%–93%，分配降至约168–169B/帧，详细CPU/内存和画质限制见[0.1.3发行说明](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/RELEASE-0.1.3.md)及包内CHANGELOG.md。Avalonia仍>=12.1.0且<13.0.0；原版文字/曲线/整页画质、JPEG及部分性能/内存差距保留，整体目标未全部达到。第三方依赖保持可选 PdfPig 0.1.16、Sdcb.SimdCodecs1.4.0与Clipper2 2.0.0，公开状态以发布记录为准。

## 0.1.2 历史累计功能

固定SkiaSharp3.119.4基线，892→3230/3235项、累计新增2338项公共元数据签名，63→201类型；按约定保留5项原生合同缺口。扩展路径/描边/渐变/采样/滤镜/Blender/RuntimeEffect子集、Picture/网格、真实Alpha8/浮点颜色、LCD/中文点阵、SVG/XPS/SKP与PDF注释。增加GL/GLES/D3D12/Metal核心及GPU资源互操作，优化Vulkan设备复制/矩形合批/完成读回、SIMD/有界缓存/分配；修复MoveTo、SaveLayer混合范围和纹理生命周期。

Avalonia接受>=12.1.0且<13.0.0，已验证宿主12.1.0–12.1.5。默认SK*解码校正为N32/预乘，直接读取像素的应用应检查ColorType/AlphaType/RowBytes。依赖保持Sdcb.SimdCodecs1.4.0/Clipper2 2.0.0/可选 PdfPig 0.1.16。Metal无Mac GPU实机验证；JPEG、曲线/文字/整页画质、内存及部分GPU/API性能仍有差距。正式版按已实现子集发布，不是全面行为或二进制替换保证；GPU对象要求创建线程显式Dispose。

完整累计更新、性能记录、限制与升级方法见包内CHANGELOG.md和[0.1.2发行说明](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/RELEASE-0.1.2.md)。原始测量按快照保留，不以正式版本号改写画质结果。

## SimdSkia.Pdf 0.1.3

Requires .NET 10.

Optional C# PDF parsing and page rendering, using PdfPig **0.1.16** (Apache-2.0)
for PDF structure/fonts and SimdSkia for rasterization. No Skia, PDFium, MuPDF,
native font library is used for PDF rendering. PDF Flate streams use the managed implementation; image codecs may use the explicitly permitted .NET native compression.

The optional parser is not pulled in by ordinary Avalonia or compatibility users. Keep all direct SimdSkia package versions aligned.

```shell
dotnet add package SimdSkia.Pdf --version 0.1.4
```

Every package includes `CHANGELOG.md` and NuGet release notes. This release is a
supported subset, not complete SkiaSharp or general PDF compatibility; see the
[release notes](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/RELEASE-NOTES.md).

```csharp
using SimdSkia.Pdf;
using SkiaSharp;

using var document = PdfReader.Open("input.pdf");
using var page = document.RenderPage(1, dpi: 144); // one-based page number
using var output = File.Create("page-1.png");
using var png = page.Bitmap.Encode(SKEncodedImageFormat.Png, 100);
png.SaveTo(output);
// Display the same bitmap in an existing SimdSkia canvas:
// canvas.DrawBitmap(page.Bitmap, 0, 0);
```

`RenderedPdfPage` owns its bitmap; dispose it after use. Returned pages outlive
the reader. Operations on a reader are serialized; avoid disposing page pixels
while another thread is drawing them. Input is limited to 256 MiB, each expanded
Flate stream to 64 MiB, each raster page/image to 16 megapixels.

Supported and tested: multi-page PDF, ordered vector paths/fills, basic strokes,
RGB/Gray images, JPEG, image soft masks, clipping, opacity, embedded TrueType/CFF
outlines, page crop/rotation and managed compressed streams. Fonts without
available outlines are rejected; system substitution is not automatic.

This is a subset renderer, **not a general-purpose Acrobat/PDFium replacement**.
Annotations/form appearances, optional layers, PDF shadings/patterns, page/form
transparency groups, graphics soft masks, non-normal blends, CMYK/ICC painting,
dash/unsupported stroke styles, non-default UserUnit, vertical fonts, Matte
soft-mask unmatting and text clipping are
explicitly rejected. A PDF containing unsupported visible content fails instead
of returning a falsely successful blank/partial page.

PDF generation is separately available through `SKDocument.CreatePdf` in
`SimdSkia.Compatibility`: multiple **raster pages**, configurable DPI, metadata,
lossless RGB or JPEG. It does not produce searchable text/vector content and
does not implement PDF/A. SkiaSharp itself supplies PDF generation, not PDF input.

Examples from the repository root:

```powershell
dotnet run -c Release --project examples/Documents -- artifacts/documents
dotnet run -c Release --project examples/PdfViewer -- tests/Pdf/Fixtures/external.pdf
dotnet run -c Release --project tests/Pdf
```

See `docs/STANDALONE-COMPATIBILITY-2026-10-27.md` for source API coverage, original
comparisons, AOT verification and remaining differences. Windows was tested in
this change; Linux/macOS execution of the new reader/viewer is not yet validated.
