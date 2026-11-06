using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool b){checks++;if(!b)throw new Exception("Drawing additions check "+checks);}
byte[] Capture(SKSurface s,string kind,int scene,int transform){using var image=s.Snapshot();using var b=new SKBitmap(new SKImageInfo(96,80,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(b.Info,b.GetPixels(),b.RowBytes));var data=b.GetPixelSpan().ToArray();cases.Add(new{kind,scene,transform,offset=pixels.Position,length=data.Length});pixels.Write(data);return data;}
for(int curved=0;curved<2;curved++)for(int color=0;color<2;color++)for(int material=0;material<2;material++)for(int transform=0;transform<3;transform++)
{
 SKPoint[] p=[new(8,10),new(26,10-curved*7),new(45,10+curved*8),new(66,10),new(66+curved*8,25),new(66-curved*7,48),new(66,62),new(48,62+curved*7),new(27,62-curved*5),new(8,62),new(8-curved*6,48),new(8+curved*5,25)];
 SKColor[]? colors=color==0?null:[new(240,30,50,180),new(40,220,70,140),new(30,70,230,240),new(220,170,30,210)];SKPoint[] tex=[new(0,0),new(64,0),new(64,64),new(0,64)];
 using var shader=material==0?null:SKShader.CreateLinearGradient(new(0,0),new(64,64),[new SKColor(30,80,210,200),new SKColor(210,60,40,130)],SKShaderTileMode.Clamp);using var paint=new SKPaint{Color=new(120,80,40,190),Shader=shader};
 using var s=SKSurface.Create(new SKImageInfo(96,80,SKColorType.Rgba8888,SKAlphaType.Premul))!;var c=s.Canvas;if(transform==1){c.Translate(6,4);c.Scale(.9f,.8f);}if(transform==2){c.Translate(15,0);c.RotateDegrees(9);}
 c.Clear(new SKColor(20,30,40));c.DrawPatch(p,colors,tex,SKBlendMode.Modulate,paint);var first=Capture(s,"patch",curved*4+color*2+material,transform);c.Clear(new SKColor(20,30,40));c.DrawPatch(p,colors,tex,SKBlendMode.Modulate,paint);using(var image=s.Snapshot()){using var read=new SKBitmap(new SKImageInfo(96,80,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));Check(first.AsSpan().SequenceEqual(read.GetPixelSpan()));}
 p[1].Y+=9;c.Clear(new SKColor(20,30,40));c.DrawPatch(p,colors,tex,paint);Capture(s,"patch-mutated",curved*4+color*2+material,transform);
}
using(var recorder=new SKPictureRecorder())
{
 var rc=recorder.BeginRecording(new(0,0,48,40));using(var paint=new SKPaint{Color=SKColors.Red})rc.DrawRect(4,5,22,16,paint);using(var paint=new SKPaint{Color=new(20,120,230,150)})rc.DrawRect(18,13,24,18,paint);using var picture=recorder.EndRecording();
 for(int crop=0;crop<2;crop++)for(int transform=0;transform<3;transform++)
 {using var filter=crop==0?SKImageFilter.CreatePicture(picture):SKImageFilter.CreatePicture(picture,new(8,9,35,29));using var paint=new SKPaint{ImageFilter=filter};using var s=SKSurface.Create(new SKImageInfo(96,80,SKColorType.Rgba8888,SKAlphaType.Premul))!;var c=s.Canvas;c.Clear(new SKColor(20,30,40));c.Translate(10,8);if(transform==1)c.Scale(1.5f,1.2f);if(transform==2)c.RotateDegrees(12);c.DrawPaint(paint);Capture(s,"picture-filter",crop,transform);}
}
// These obsolete-error factories are present in the baseline but cannot be called from C# source.
for(int material=0;material<2;material++)for(int crop=0;crop<2;crop++)
{using var srcShader=material==0?null:SKShader.CreateColor(new SKColor(60,100,230,170));using var source=new SKPaint{Color=new(80,160,220,190),Shader=srcShader};var types=crop==0?new[]{typeof(SKPaint)}:new[]{typeof(SKPaint),typeof(SKRect)};var method=typeof(SKImageFilter).GetMethod("CreatePaint",types)!;using var filter=(SKImageFilter)method.Invoke(null,crop==0?[source]:[source,new SKRect(12,10,40,30)])!;source.Color=SKColors.Red;srcShader?.Dispose();using var paint=new SKPaint{ImageFilter=filter};using var s=SKSurface.Create(new SKImageInfo(96,80,SKColorType.Rgba8888,SKAlphaType.Premul))!;s.Canvas.Clear(new SKColor(20,30,40));s.Canvas.Translate(5,7);s.Canvas.DrawPaint(paint);Capture(s,"paint-filter",crop,material);}
using(var s=SKSurface.CreateNull(100000,100000)){Check(s is not null);s!.Canvas.Clear(SKColors.Red);using var p=new SKPaint{Color=SKColors.Blue};s.Canvas.DrawRect(0,0,100000,100000,p);Check(s.Snapshot() is null);using var b=new SKBitmap(1,1);Check(!s.ReadPixels(b.Info,b.GetPixels(),b.RowBytes,0,0));}
File.WriteAllText(Path.Combine(output,"values.json"),"{}");File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"Captured {cases.Count} drawing additions; {checks} checks");
