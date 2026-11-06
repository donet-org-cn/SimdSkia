using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool value){checks++;if(!value)throw new Exception("Mesh/atlas assertion "+checks);}
byte[] Capture(SKSurface surface,string kind,int scene,int transform)
{using var image=surface.Snapshot();using var read=new SKBitmap(new SKImageInfo(80,64,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));var bytes=read.GetPixelSpan().ToArray();cases.Add(new{kind,scene,transform,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);return bytes;}
SKBlendMode[] modes=[SKBlendMode.Src,SKBlendMode.Dst,SKBlendMode.SrcOver,SKBlendMode.Modulate,SKBlendMode.Multiply];
for(int material=0;material<3;material++)for(int vmode=0;vmode<3;vmode++)for(int blend=0;blend<modes.Length;blend++)
{
 SKPoint[] positions=[new(7,6),new(57,9),new(17,47),new(66,51)];SKPoint[] texs=[new(3,4),new(50,6),new(8,40),new(60,48)];SKColor[] colors=[new(230,30,60,210),new(40,220,70,130),new(50,80,230,240),new(180,90,30,170)];
 using var vertices=SKVertices.CreateCopy((SKVertexMode)vmode,positions,texs,colors);positions[0]=new(0,0);colors[0]=SKColors.Black;
 using var shader=material==1?SKShader.CreateColor(new SKColor(20,60,230,170)):material==2?SKShader.CreateLinearGradient(new(0,0),new(70,0),[new SKColor(40,190,230,220),new SKColor(220,40,80,150)],SKShaderTileMode.Clamp):null;
 using var paint=new SKPaint{Color=new SKColor(150,90,40,180),Shader=shader};shader?.Dispose();using var surface=SKSurface.Create(new SKImageInfo(80,64,SKColorType.Rgba8888,SKAlphaType.Premul))!;surface.Canvas.Clear(new SKColor(30,40,50));surface.Canvas.DrawVertices(vertices,modes[blend],paint);var first=Capture(surface,"mesh",material*3+vmode,blend);surface.Canvas.Clear(new SKColor(30,40,50));surface.Canvas.DrawVertices(vertices,modes[blend],paint);using var snapshot=surface.Snapshot();using var read=new SKBitmap(new SKImageInfo(80,64,SKColorType.Rgba8888,SKAlphaType.Premul));Check(snapshot.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));Check(first.AsSpan().SequenceEqual(read.GetPixelSpan()));
}
using(var source=new SKBitmap(16,16))
{
 for(int y=0;y<16;y++)for(int x=0;x<16;x++)source.SetPixel(x,y,new((byte)(x*16),(byte)(y*16),(byte)(x*7+y*5),(byte)(80+(x+y)*5)));
 using var image=SKImage.FromBitmap(source);SKRect[] sprites=[new(0,0,8,8),new(8,0,16,8),new(0,8,8,16),new(8,8,16,16)];SKColor[] colors=[new(240,70,50,200),new(40,200,80,140),new(40,90,240,240),new(190,70,190,170)];
 for(int filter=0;filter<2;filter++)for(int blend=0;blend<modes.Length;blend++)for(int tint=0;tint<2;tint++)
 {
  SKRotationScaleMatrix[] transforms=[SKRotationScaleMatrix.CreateDegrees(2,0,5,4,0,0),SKRotationScaleMatrix.CreateDegrees(2.2f,12,36,5,0,0),SKRotationScaleMatrix.CreateDegrees(2,-8,8,35,0,0),SKRotationScaleMatrix.CreateDegrees(2.1f,7,40,35,0,0)];using var surface=SKSurface.Create(new SKImageInfo(80,64,SKColorType.Rgba8888,SKAlphaType.Premul))!;using var paint=new SKPaint{Color=new SKColor(255,255,255,190)};var sampling=new SKSamplingOptions((SKFilterMode)filter);var c=surface.Canvas;c.Clear(new SKColor(30,40,50));
  void Draw(){if(tint==0)c.DrawAtlas(image,sprites,transforms,sampling,paint);else c.DrawAtlas(image,sprites,transforms,colors,modes[blend],sampling,new SKRect(0,0,80,64),paint);}
  Draw();var first=Capture(surface,"atlas",filter*2+tint,blend*2);c.Clear(new SKColor(30,40,50));Draw();using(var result=surface.Snapshot()){using var read=new SKBitmap(new SKImageInfo(80,64,SKColorType.Rgba8888,SKAlphaType.Premul));Check(result.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));Check(first.AsSpan().SequenceEqual(read.GetPixelSpan()));}
  transforms[0]=SKRotationScaleMatrix.CreateTranslation(2,3);c.Clear(new SKColor(30,40,50));Draw();Capture(surface,"atlas",filter*2+tint,blend*2+1);
 }
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"Captured {cases.Count} mesh/atlas images and {checks} immutable-copy/cache assertions");
