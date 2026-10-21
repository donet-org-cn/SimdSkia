using SkiaSharp;

string fontPath=args.Length>0?args[0]:"baseline/fonts/ZenOldMincho-Regular.ttf";
using var face=SKTypeface.FromFile(fontPath);using var font=new SKFont(face,20){Hinting=SKFontHinting.None,Edging=SKFontEdging.Antialias,Subpixel=true};
var codepoints="SimdSkia 中文列表 0123".EnumerateRunes().Select(r=>r.Value).ToArray();var glyphs=font.GetGlyphs(codepoints);
var widths=new float[glyphs.Length];font.GetGlyphWidths(glyphs,widths,Span<SKRect>.Empty);
var positions=new SKPoint[glyphs.Length];for(int i=1;i<positions.Length;i++)positions[i]=new(positions[i-1].X+widths[i-1],0);
using var builder=new SKTextBlobBuilder();var run=builder.AllocatePositionedRun(font,glyphs.Length);run.SetGlyphs(glyphs);run.SetPositions(positions);
using var blob=builder.Build()!;using var bitmap=new SKBitmap(new SKImageInfo(640,240,SKColorType.Rgba8888,SKAlphaType.Premul));
using var paint=new SKPaint{Color=new SKColor(235,240,250),IsAntialias=true};
for(int frame=0;frame<8;frame++)
{
    using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(24,28,38));canvas.Scale(1.5f);canvas.ClipRect(new SKRect(8,8,418,150));
    for(int row=0;row<4;row++)canvas.DrawText(blob,16,35+row*32-frame*.25f,paint);canvas.Flush();
}
Directory.CreateDirectory("artifacts/avalonia");using var png=bitmap.Encode(SKEncodedImageFormat.Png,100);using var output=File.Create("artifacts/avalonia/text-ui.png");png.SaveTo(output);
Console.WriteLine("Wrote artifacts/avalonia/text-ui.png using managed positioned text. This is the drawing call chain, not an integrated Avalonia window.");
