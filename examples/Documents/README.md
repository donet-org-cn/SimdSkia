# PDF export and reading

This source example requires the current unpublished changes; NuGet preview.8
does not have these APIs yet. Use the .NET 10 SDK from the repository root:

```powershell
dotnet run -c Release --project examples/Documents -- artifacts/documents
dotnet run -c Release --project examples/Documents -- artifacts/documents C:/input.pdf
dotnet run -c Release --project examples/PdfViewer -- C:/input.pdf
```

The first command generates a two-page 144 DPI **raster** PDF, reopens it and
renders both pages to PNG. The second renders an existing PDF to PNG instead;
the third opens the window viewer with Open / Previous / Next controls. Windows
uses `WinExe` for the viewer, so an extra console window is not created.

Generation uses `SKDocument.CreatePdf`, `BeginPage`, ordinary `SKCanvas` commands,
`EndPage` and `Close`. Caller-owned streams remain open. Page canvases become
invalid at `EndPage`; the document releases its current raster page then.

Reading uses `PdfReader.Open` and `RenderPage(1, dpi)`. A returned page owns its
`SKBitmap`; draw it on any existing canvas with `DrawBitmap`. PDF reading is
additional functionality, not a SkiaSharp native API replacement. The sample
viewer converts the bitmap to PNG for the Avalonia `Image` control; this bridge
is for demonstration and is not used by the pixel-access benchmark.

The reader supports a documented subset. Unsupported visible content raises an
error in the viewer instead of displaying incomplete pages as success. See
[module documentation](../../src/SimdSkia.Pdf/README.md) and the
[validation report](../../docs/STANDALONE-COMPATIBILITY-2026-10-27.md).
