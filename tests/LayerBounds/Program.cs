using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object>();
using var pixels=File.Create(Path.Combine(output,"pixels.bin"));
foreach(var format in new[]{SKColorType.Bgra8888,SKColorType.Rgba8888})foreach(bool tagged in new[]{false,true})foreach(var mode in Enum.GetValues<SKBlendMode>())foreach(var scene in new[]{"clear","empty","partial-draw","clip-transform"})
{
 using var cs=tagged?SKColorSpace.CreateSrgb():null;using var bitmap=new SKBitmap(new SKImageInfo(40,32,format,SKAlphaType.Premul,cs));using var canvas=new SKCanvas(bitmap);using var paint=new SKPaint{BlendMode=mode};
 canvas.Clear(SKColors.White);
 if(scene=="clip-transform"){canvas.ClipRect(new SKRect(3,4,33,27));canvas.Translate(2,3);}
 canvas.SaveLayer(new SKRect(8,8,20,20),paint);
 if(scene=="clear"||scene=="clip-transform")canvas.Clear(SKColors.Red);
 else if(scene=="partial-draw"){using var red=new SKPaint{Color=SKColors.Red};canvas.DrawRect(4,5,24,18,red);}
 canvas.Restore();
 long offset=pixels.Position;var span=bitmap.GetPixelSpan();pixels.Write(span);
 values[$"{format}-{tagged}-{mode}-{scene}"]=new{offset,length=span.Length,outside=bitmap.GetPixel(0,0).ToString(),inside=bitmap.GetPixel(10,10).ToString()};
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {values.Count} layer bounds/composite cases.");
