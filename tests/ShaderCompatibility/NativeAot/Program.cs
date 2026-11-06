using SkiaSharp;
int checks=0;
void Check(bool ok){if(!ok)throw new Exception("AOT shader/erase check failed: "+checks);checks++;}
Check(!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
using var b=new SKBitmap(64,64);using var canvas=new SKCanvas(b);
foreach(var mode in Enum.GetValues<SKShaderTileMode>())
{
    using var s=SKShader.CreateLinearGradient(new(8,0),new(56,64),[SKColors.Red,SKColors.Green,SKColors.Blue],[0,.3f,1],mode);
    using var local=s.WithLocalMatrix(SKMatrix.CreateTranslation(2,3));using var paint=new SKPaint{Shader=local};
    canvas.Clear(SKColors.White);canvas.DrawRect(new(0,0,64,64),paint);Check(b.GetPixel(32,32)!=SKColors.White);
}
using(var s=SKShader.CreateRadialGradient(new(32,32),30,[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp))
using(var p=new SKPaint{Shader=s}){canvas.DrawRect(new(0,0,64,64),p);Check(b.GetPixel(32,32).Red>200);}
using(var s=SKShader.CreateSweepGradient(new(32,32),[SKColors.Red,SKColors.Blue]))
using(var p=new SKPaint{Shader=s}){canvas.DrawRect(new(0,0,64,64),p);Check(b.GetPixel(32,32)!=b.GetPixel(16,48));}
using(var s=SKShader.CreateColor(SKColors.Blue))using(var p=new SKPaint{Shader=s}){s.Dispose();canvas.DrawRect(new(0,0,64,64),p);Check(b.GetPixel(20,20)==SKColors.Blue);}
b.Erase(SKColors.Red,new(-3,2,40,70));Check(b.GetPixel(10,2)==SKColors.Red&&b.GetPixel(50,20)==SKColors.Blue);
using(var s=SKShader.CreateLinearGradient(new(0,0),new(64,64),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp))
using(var p=new SKPaint{Shader=s}){canvas.DrawRect(new(0,0,64,64),p);Check(b.GetPixel(20,30)==new SKColor(153,0,102,255));}
foreach(var mode in Enum.GetValues<SKShaderTileMode>())
{
    using var s=SKShader.CreateSweepGradient(new(32,32),[SKColors.Red,SKColors.Blue],mode,90,90);
    using var p=new SKPaint{Shader=s};canvas.Clear(SKColors.White);canvas.DrawRect(new(0,0,64,64),p);
    Check(mode==SKShaderTileMode.Decal?b.GetPixel(20,20)==SKColors.White:b.GetPixel(20,20)!=SKColors.White);
}
Console.WriteLine("PASS "+checks+" actual Native AOT shader/erase checks");
