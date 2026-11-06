using SkiaSharp;
string output=args.FirstOrDefault()??"artifacts/strokes.png";
using var bitmap=new SKBitmap(640,330);using var canvas=new SKCanvas(bitmap);canvas.DrawColor(new(24,32,49));
for(int row=0;row<3;row++)for(int col=0;col<3;col++)
{
    int saved=canvas.Save();canvas.Translate(col*210,row*105);
    using var path=new SKPath();path.MoveTo(22,80);path.LineTo(95,20);path.LineTo(180,80);
    using var paint=new SKPaint{Style=SKPaintStyle.Stroke,StrokeWidth=13,StrokeCap=(SKStrokeCap)col,StrokeJoin=(SKStrokeJoin)row,IsAntialias=true,Color=new SKColor(50,190,220,210)};
    canvas.DrawPath(path,paint);
    using var outline=paint.GetFillPath(path);if(outline is null||outline.IsEmpty)throw new Exception("Expected a real filled outline.");
    canvas.RestoreToCount(saved);
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);using var data=bitmap.Encode(SKEncodedImageFormat.Png,100);File.WriteAllBytes(output,data.ToArray());
Console.WriteLine(Path.GetFullPath(output)+"; columns: butt/round/square; rows: miter/round/bevel");
