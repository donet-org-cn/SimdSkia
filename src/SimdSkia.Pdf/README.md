# 0.1.2-preview.2 更新

CI复核新增修正：连续MoveTo保留空轮廓/点；Src/Clear/SrcIn/DstIn/SrcOut/DstATop/Modulate的SaveLayer bounds按原版扩展到父裁剪。原版几何300组/4636值、464组图层组合和兼容扩展均通过。preview.1标签的CI失败，未上传NuGet；本次使用preview.2保留追溯。

3230/3235项固定基线，保留5项原生合同缺口；实际SVG/XPS/SKP、Alpha8、浮点颜色与GPU资源互操作。Vulkan合批/设备复制/完成读回优化；Metal无Mac GPU，尚未实机验证。Avalonia接受>=12.1.0且<13.0.0，已验证宿主12.1.0–12.1.5。JPEG、文字/曲线/整页画质及性能/内存仍有差距。Sdcb.SimdCodecs保持1.4.0；.NET系统压缩与macOS官方窗口宿主是已允许的原生例外。完整测试、CPU/GPU/内存性能及兼容限制见包内CHANGELOG.md和[本版报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/PUBLIC-API-FINAL-2026-11-07.md)。


# SimdSkia.Pdf 0.1.2-preview.2

Requires .NET 10.

Optional C# PDF parsing and page rendering, using PdfPig **0.1.2-preview.26** (Apache-2.0)
for PDF structure/fonts and SimdSkia for rasterization. No Skia, PDFium, MuPDF,
native font library is used for PDF rendering. PDF Flate streams use the managed implementation; image codecs may use the explicitly permitted .NET native compression.

The optional parser is not pulled in by ordinary Avalonia or compatibility users. Keep all direct SimdSkia package versions aligned.

```shell
dotnet add package SimdSkia.Pdf --version 0.1.2-preview.2
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
