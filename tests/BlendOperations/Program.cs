using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var rows=new List<object>();using var binary=File.Create(Path.Combine(output,"pixels.bin"));var random=new Random(548391);int offset=0;
// Predeclared channel gate is 1 for isolated compositing of full/quarter pixels;
// the same gate applies to the established three modes and the new equations.
foreach(var format in new[]{SKColorType.Rgba8888,SKColorType.Bgra8888})foreach(var alpha in new[]{SKAlphaType.Premul,SKAlphaType.Unpremul,SKAlphaType.Opaque})foreach(var mode in Enum.GetValues<SKBlendMode>())for(int sample=0;sample<16;sample++)
{
    byte a=new byte[]{0,1,17,64,127,128,254,255}[sample%8];var source=new SKColor((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256),a);var destination=new SKColor((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256),alpha==SKAlphaType.Opaque?(byte)255:(byte)random.Next(256));
    using var bitmap=new SKBitmap(new SKImageInfo(8,5,format,alpha));using var canvas=new SKCanvas(bitmap);canvas.Clear(destination);using var paint=new SKPaint{Color=source,BlendMode=mode,IsAntialias=sample>=8};canvas.DrawRect(sample>=8?new SKRect(.25f,.5f,6.5f,3.5f):new SKRect(0,0,6,4),paint);var bytes=bitmap.GetPixelSpan();binary.Write(bytes);rows.Add(new{kind="rectangle",format,alpha,mode,sample,source=(uint)source,destination=(uint)destination,offset,length=bytes.Length});offset+=bytes.Length;
}
foreach(var format in new[]{SKColorType.Rgba8888,SKColorType.Bgra8888})foreach(var alpha in new[]{SKAlphaType.Premul,SKAlphaType.Unpremul,SKAlphaType.Opaque})foreach(byte da in new byte[]{0,1,127,255})foreach(byte sa in new byte[]{0,1,17,64,127,128,254,255})
{using var bitmap=new SKBitmap(new SKImageInfo(8,5,format,alpha));bitmap.Erase(new SKColor(53,123,221,da));var source=new SKColor(171,45,117,sa);bitmap.SetPixel(2,2,source);var bytes=bitmap.GetPixelSpan();binary.Write(bytes);rows.Add(new{kind="set-pixel",format,alpha,mode=SKBlendMode.SrcOver,sample=(int)da,source=(uint)source,destination=(uint)new SKColor(53,123,221,da),offset,length=bytes.Length});offset+=bytes.Length;}
File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {rows.Count} blend images");
