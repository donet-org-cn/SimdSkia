using SkiaSharp;
using SimdSkia.Pdf;

string output=Path.GetFullPath(args.Length>0?args[0]:"artifacts/documents");Directory.CreateDirectory(output);
string file=Path.Combine(output,"simdskia.pdf");
using(var document=SKDocument.CreatePdf(file,new SKDocumentPdfMetadata(144f){Title="SimdSkia managed PDF example"}))
{
    using var typeface=SKTypeface.FromFamilyName(OperatingSystem.IsWindows()?"Microsoft YaHei":null);using var font=new SKFont(typeface,20);
    using var paint=new SKPaint{Color=new SKColor(30,90,180),IsAntialias=true};
    for(int page=1;page<=2;page++)
    {
        var canvas=document.BeginPage(360,200);canvas.Clear(SKColors.White);canvas.DrawRoundRect(new SKRect(16,16,344,184),14,14,paint);
        paint.Color=SKColors.White;canvas.DrawText($"SimdSkia PDF - {page}",32,60,font,paint);
        if(typeface.GetGlyph(0x4e2d)!=0)canvas.DrawText("中文字体 / C# rendering",32,100,font,paint);
        paint.Color=new SKColor(30,90,180);document.EndPage();
    }document.Close();
}
// Supplying a second argument renders an existing PDF instead of the generated one.
using(var reader=PdfReader.Open(args.Length>1?args[1]:file))
{
    for(int i=1;i<=reader.PageCount;i++)
    {using var page=reader.RenderPage(i,144);using var png=page.Bitmap.Encode(SKEncodedImageFormat.Png,100);using var target=File.Create(Path.Combine(output,$"page-{i}.png"));png.SaveTo(target);}
}
Console.WriteLine($"Generated {file}; rendered page PNGs in {output}");
