# SimdSkia 0.1.2 正式版：0.1.1以来累计更新

固定SkiaSharp3.119.4基线，892→3230/3235项、累计新增2338项公共元数据签名，63→201类型；按约定保留5项原生合同缺口。扩展路径/描边/渐变/采样/滤镜/Blender/RuntimeEffect子集、Picture/网格、真实Alpha8/浮点颜色、LCD/中文点阵、SVG/XPS/SKP与PDF注释。增加GL/GLES/D3D12/Metal核心及GPU资源互操作，优化Vulkan设备复制/矩形合批/完成读回、SIMD/有界缓存/分配；修复MoveTo、SaveLayer混合范围和纹理生命周期。

Avalonia接受>=12.1.0且<13.0.0，已验证宿主12.1.0–12.1.5。默认SK*解码校正为N32/预乘，直接读取像素的应用应检查ColorType/AlphaType/RowBytes。依赖保持Sdcb.SimdCodecs1.4.0/Clipper2 2.0.0/可选 PdfPig 0.1.16。Metal无Mac GPU实机验证；JPEG、曲线/文字/整页画质、内存及部分GPU/API性能仍有差距。正式版按已实现子集发布，不是全面行为或二进制替换保证；GPU对象要求创建线程显式Dispose。

完整累计更新、性能记录、限制与升级方法见包内CHANGELOG.md和[0.1.2发行说明](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/RELEASE-0.1.2.md)。原始测量按快照保留，不以正式版本号改写画质结果。

## SimdSkia.Pdf 0.1.2

Requires .NET 10.

Optional C# PDF parsing and page rendering, using PdfPig **0.1.16** (Apache-2.0)
for PDF structure/fonts and SimdSkia for rasterization. No Skia, PDFium, MuPDF,
native font library is used for PDF rendering. PDF Flate streams use the managed implementation; image codecs may use the explicitly permitted .NET native compression.

The optional parser is not pulled in by ordinary Avalonia or compatibility users. Keep all direct SimdSkia package versions aligned.

```shell
dotnet add package SimdSkia.Pdf --version 0.1.2
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
