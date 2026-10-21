using Scenes;
using SkiaSharp;

if(args.Length==2&&args[0]=="--encode-probe"){EncodeProbe.Run(args[1]);return;}
if(args.Length==2&&args[0]=="--behavior-only"){Directory.CreateDirectory(args[1]);Behavior.Record(args[1]);return;}
if(args.Contains("--benchmark")){ClearBenchmark.Run();return;}

if(args.Contains("--probe-aa"))
{
    using var path=new SKPath();path.MoveTo(3,24);path.QuadTo(32,1,60,24);path.CubicTo(55,44,8,44,3,24);path.Close();
    int covered=0;for(int y=0;y<64;y++)for(int x=0;x<64;x++)if(path.Contains(14+(x+.5f)/64,16+(y+.5f)/64))covered++;
    Console.WriteLine($"Native Contains pixel(14,16) coverage={covered/4096.0}; bounds={path.Bounds}; svg={path.ToSvgPathData()}");
    using var bitmap=new SKBitmap(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);using var paint=new SKPaint {Color=SKColors.Red,IsAntialias=true};canvas.Clear();canvas.DrawPath(path,paint);Console.WriteLine($"Native DrawPath pixel(14,16)={bitmap.GetPixel(14,16)}; AA={paint.IsAntialias}");
    return;
}

Suite.Render(args.Length>0 ? args[0] : "artifacts/reference");
Behavior.Record(args.Length>0 ? args[0] : "artifacts/reference");
