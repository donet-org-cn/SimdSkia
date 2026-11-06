using SkiaSharp;
string output=args.FirstOrDefault()??"artifacts/conics.png";
using var bitmap=new SKBitmap(640,320);using var canvas=new SKCanvas(bitmap);
using(var background=SKShader.CreateLinearGradient(new(0,0),new(640,320),[new SKColor(20,30,50),new SKColor(45,55,80)],SKShaderTileMode.Clamp))
using(var paint=new SKPaint{Shader=background})canvas.DrawPaint(paint);
using var fill=new SKPaint{Color=new SKColor(40,200,170,210),IsAntialias=true};
using var stroke=new SKPaint{Color=new SKColor(90,170,255),IsAntialias=true,Style=SKPaintStyle.Stroke,StrokeWidth=4};
canvas.DrawOval(new(20,30,190,140),fill);canvas.DrawOval(new(20,30,190,140),stroke);
canvas.DrawArc(new(220,25,390,155),-45,250,true,fill);
canvas.DrawArc(new(420,25,600,155),-45,250,false,stroke);
using var path=new SKPath();path.MoveTo(25,270);path.ConicTo(85,130,195,270,.4f);path.LineTo(25,270);canvas.DrawPath(path,fill);
path.Reset();path.MoveTo(235,275);path.ConicTo(295,135,395,275,2);canvas.DrawPath(path,stroke);
path.Reset();path.MoveTo(430,280);path.ArcTo(new SKPoint(520,170),new SKPoint(605,280),40);canvas.DrawPath(path,stroke);
using var circle=new SKPath();circle.AddCircle(600,295,10);
if(circle.PointCount!=9)throw new Exception("Circle must retain its nine-point conic representation.");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);using var png=bitmap.Encode(SKEncodedImageFormat.Png,100);File.WriteAllBytes(output,png.ToArray());
Console.WriteLine(Path.GetFullPath(output)+"; circle points="+circle.PointCount);
