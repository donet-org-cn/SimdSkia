#pragma warning disable CS0618
using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);using var stream=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<object>();var values=new SortedDictionary<string,object?>();int offset=0,checks=0;
void Capture(SKBitmap bitmap,object metadata){var bytes=bitmap.GetPixelSpan();stream.Write(bytes);cases.Add(new{metadata,offset,length=bytes.Length});offset+=bytes.Length;}
SKBitmap Input(){var b=new SKBitmap(new SKImageInfo(5,4,SKColorType.Rgba8888,SKAlphaType.Premul));var bytes=b.GetPixelSpan();for(int y=0;y<4;y++)for(int x=0;x<5;x++){int i=(y*5+x)*4;byte a=(byte)((x+y)%4==0?0:(x+y)%4==1?91:255);bytes[i]=(byte)(((x*51)*a+127)/255);bytes[i+1]=(byte)(((y*83)*a+127)/255);bytes[i+2]=(byte)(((251-x*35-y*11)*a+127)/255);bytes[i+3]=a;}return b;}
foreach(var tx in Enum.GetValues<SKShaderTileMode>())foreach(var ty in Enum.GetValues<SKShaderTileMode>())foreach(int sampling in new[]{0,1,2,3})foreach(int matrix in new[]{0,1,2})foreach(int shape in new[]{0,1,2})
{
 using var source=Input();using var image=SKImage.FromBitmap(source);SKSamplingOptions options=sampling switch{1=>new(SKFilterMode.Linear),2=>new(SKCubicResampler.Mitchell),3=>new(SKCubicResampler.CatmullRom),_=>default};var local=matrix==0?SKMatrix.Identity:SKMatrix.CreateScale(2.3f,1.7f);if(matrix==2)local=SKMatrix.Concat(SKMatrix.CreateRotationDegrees(24),local);
 using var shader=image.ToShader(tx,ty,options,local);using var paint=new SKPaint{Color=new SKColor(0,0,0,193),Shader=shader,IsAntialias=shape==1};if(shape==2){paint.Color=SKColors.White;paint.BlendMode=SKBlendMode.Src;}using var bitmap=new SKBitmap(new SKImageInfo(32,24,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(71,135,89,173));canvas.Translate(7,5);if(shape!=1)canvas.DrawRect(-7,-5,32,24,paint);else canvas.DrawCircle(7,6,9,paint);Capture(bitmap,new{tx=(int)tx,ty=(int)ty,sampling,matrix,shape});
}
// Mutable bitmap shaders snapshot pixels; image/shader wrappers may be disposed
// while a paint/clone/derived shader retains the underlying image lease.
foreach(int scene in new[]{0,1,2,3})
{
 using var source=Input();var shader=source.ToShader(SKShaderTileMode.Repeat,SKShaderTileMode.Mirror);using var derived=shader.WithLocalMatrix(SKMatrix.CreateScale(2,2));var paint=new SKPaint{Shader=scene==1?derived:shader};using var clone=paint.Clone();shader.Dispose();source.Erase(SKColors.Red);if(scene>=2)paint.Reset();if(scene==3)paint.Dispose();using var bitmap=new SKBitmap(new SKImageInfo(32,24,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.White);canvas.DrawRect(0,0,32,24,clone);Capture(bitmap,new{lifetime=scene});paint.Dispose();checks++;
}

var pairs=new[]{(new SKColor(250,80,170,191),new SKColor(20,140,90,119)),(SKColors.Transparent,SKColors.Red),(SKColors.Red,SKColors.Transparent),(SKColors.Black,SKColors.White),(new SKColor(12,99,172,37),new SKColor(181,84,9,234)),(new SKColor(255,255,255,1),new SKColor(0,0,0,1)),(new SKColor(119,91,211,255),new SKColor(9,218,110,255)),(new SKColor(253,1,123,253),new SKColor(1,253,89,127))};
foreach(var mode in Enum.GetValues<SKBlendMode>())for(int pair=0;pair<pairs.Length;pair++)
{
 using var a=SKShader.CreateColor(pairs[pair].Item1);using var b=SKShader.CreateColor(pairs[pair].Item2);var blender=SKBlender.CreateBlendMode(mode);blender.Dispose();using var shader=SKShader.CreateBlend(blender,a,b);using var paint=new SKPaint{Shader=shader,BlendMode=SKBlendMode.Src};using var bitmap=new SKBitmap(new SKImageInfo(2,2,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.DrawRect(0,0,2,2,paint);Capture(bitmap,new{compose=(int)mode,pair});
}
float[][] coefficients=[[0,1,0,0],[0,0,1,0],[0,.5f,.5f,0],[1,0,0,0],[-1,1,1,0],[0,0,0,.25f],[2,.3f,-.1f,.2f],[-2,1.2f,.5f,-.1f]];
for(int arithmetic=0;arithmetic<coefficients.Length;arithmetic++)foreach(bool enforce in new[]{false,true})for(int pair=0;pair<pairs.Length;pair++)
{
 var k=coefficients[arithmetic];using var a=SKShader.CreateColor(pairs[pair].Item1);using var b=SKShader.CreateColor(pairs[pair].Item2);var blender=SKBlender.CreateArithmetic(k[0],k[1],k[2],k[3],enforce);using var shader=SKShader.CreateBlend(blender,a,b);blender.Dispose();a.Dispose();b.Dispose();using var paint=new SKPaint{Shader=shader,BlendMode=SKBlendMode.Src};shader.Dispose();using var bitmap=new SKBitmap(new SKImageInfo(2,2,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.DrawRect(0,0,2,2,paint);Capture(bitmap,new{arithmetic,enforce,pair});
}
values["local-lifetime-checks"]=checks;File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {cases.Count} raster shader images, {checks} lifetime checks");
