using System.Text.Json;
using SkiaSharp;

internal static class EncodeProbe
{
    internal static string Capture()
    {
        var rows=new List<object>();
        foreach(var alpha in new[]{SKAlphaType.Opaque,SKAlphaType.Premul,SKAlphaType.Unpremul})foreach(byte a in new byte[]{0,32,128,255})foreach(string storage in new[]{"erase","raw"})
        {
            using var image=new SKBitmap(new SKImageInfo(16,16,SKColorType.Rgba8888,alpha));image.Erase(new(180,90,44,a));
            if(storage=="raw")
            {var bytes=image.GetPixelSpan();for(int i=0;i<bytes.Length;i+=4){bytes[i]=(byte)(alpha==SKAlphaType.Premul?(180*a+127)/255:180);bytes[i+1]=(byte)(alpha==SKAlphaType.Premul?(90*a+127)/255:90);bytes[i+2]=(byte)(alpha==SKAlphaType.Premul?(44*a+127)/255:44);bytes[i+3]=a;}}
            using var data=image.Encode(SKEncodedImageFormat.Jpeg,100);using var decoded=SKBitmap.Decode(data.ToArray());var p=decoded.GetPixel(8,8);
            rows.Add(new{alpha=alpha.ToString(),a,storage,r=p.Red,g=p.Green,b=p.Blue,outAlpha=p.Alpha});
        }
        return JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true});
    }
    internal static void Run(string output){File.WriteAllText(output,Capture());Console.WriteLine(output);}
}
