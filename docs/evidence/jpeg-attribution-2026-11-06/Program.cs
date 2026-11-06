using SkiaSharp;
using System.Text.Json;
Directory.CreateDirectory(args[1]);
if(args[0].StartsWith("capture"))
{
 foreach(var alpha in new[]{SKAlphaType.Opaque,SKAlphaType.Premul,SKAlphaType.Unpremul})foreach(byte a in new byte[]{0,32,128,255})foreach(string storage in new[]{"erase","raw"})
 {
  string name=$"{alpha}-{a}-{storage}";using var bitmap=new SKBitmap(new SKImageInfo(16,16,SKColorType.Rgba8888,alpha));bitmap.Erase(new(180,90,44,a));
  if(storage=="raw"){var s=bitmap.GetPixelSpan();for(int i=0;i<s.Length;i+=4){s[i]=(byte)(alpha==SKAlphaType.Premul?(180*a+127)/255:180);s[i+1]=(byte)(alpha==SKAlphaType.Premul?(90*a+127)/255:90);s[i+2]=(byte)(alpha==SKAlphaType.Premul?(44*a+127)/255:44);s[i+3]=a;}}
  File.WriteAllBytes(Path.Combine(args[1],name+".input"),bitmap.GetPixelSpan().ToArray());
#if MANAGED
using var jpeg=bitmap.Encode(SKEncodedImageFormat.Jpeg,100);
#else
using var pixmap=bitmap.PeekPixels()!;using var jpeg=args[0]=="capture444"?pixmap.Encode(new SKJpegEncoderOptions(100,SKJpegEncoderDownsample.Downsample444,SKJpegEncoderAlphaOption.Ignore)):bitmap.Encode(SKEncodedImageFormat.Jpeg,100);
#endif
File.WriteAllBytes(Path.Combine(args[1],name+".jpg"),jpeg!.ToArray());
 }
}
else foreach(string variant in new[]{"original","original444","managed"})foreach(string file in Directory.EnumerateFiles(Path.Combine(args[2],variant),"*.jpg"))
{
 var data=File.ReadAllBytes(file);
#if MANAGED
 using var decoded=SKBitmap.Decode(data);
#else
 using var decoded=SKBitmap.Decode(data,new SKImageInfo(16,16,SKColorType.Rgba8888,SKAlphaType.Unpremul));
#endif
 File.WriteAllBytes(Path.Combine(args[1],variant+"-"+Path.GetFileNameWithoutExtension(file)+".rgba"),decoded.GetPixelSpan().ToArray());
}
