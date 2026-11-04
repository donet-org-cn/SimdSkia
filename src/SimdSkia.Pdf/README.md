# SimdSkia.Pdf 0.1.0

Requires .NET 10. The inherited image provider is Sdcb.SimdCodecs 1.4.0. This stable release supports the documented PDF subset; native-equivalent 画质 and full compatibility are not claimed. PNG lossless reduction and IDAT buffer optimization are included. 性能/CPU/memory results and original quality failures are recorded in CHANGELOG and the [0.1.0 report](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/STABLE-0.1.0-2026-11-04.md).


Optional C# PDF parsing and page rendering, using PdfPig **0.1.16** (Apache-2.0)
for PDF structure/fonts and SimdSkia for rasterization. No Skia, PDFium, MuPDF,
native font library is used for PDF rendering. PDF Flate streams use the managed implementation; image codecs may use the explicitly permitted .NET native compression.

**0.1.0-preview.10 requires .NET 10.** Full **Sdcb.SimdCodecs 1.2.0** integration is inherited through Imaging. The **123**-process CPU/GPU/codec test record is included in `CHANGELOG.md`: CPU controls 0.891→0.549 ms, but working set 90.39→109.81 MiB, slow PNG cases and original 画质 failures remain. JPEG 3/24 strict cases and CPU/GPU full-page quality still fail; no thresholds were relaxed. See the [measured report](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/SKIA-COMPARISON-2026-11-01.md). This is not a general PDF compatibility or performance claim.

**New in 0.1.0-preview.9:** optional PDF reading/rendering. The same release adds
`SKBitmap.GetPixels`, GIF `SKCodec`, system font family discovery and the PDF
writer in the compatibility module. Ordinary `SimdSkia.Compatibility` and
`SimdSkia.Avalonia` users do not acquire the PDF parser dependency. Keep all
direct SimdSkia package references on the same preview version.

```shell
dotnet add package SimdSkia.Pdf --version 0.1.0
```

Every package includes `CHANGELOG.md` and NuGet release notes. This preview is a
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
