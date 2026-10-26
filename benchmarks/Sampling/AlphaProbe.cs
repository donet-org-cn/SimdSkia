using System.Text.Json;
using SkiaSharp;

internal static class AlphaProbe
{
    internal static void Run(string output)
    {
        var rows=new List<object>();
        foreach(bool straight in new[]{false,true})
        foreach(int da in new[]{0,73,128,255})
        foreach(int opacity in new[]{0,1,64,173,254,255})
        for(int alpha=0;alpha<256;alpha++)
        {
            using var source=new SKBitmap(new SKImageInfo(2,2,SKColorType.Rgba8888,straight?SKAlphaType.Unpremul:SKAlphaType.Premul));
            byte Scale(int v)=>(byte)((v*alpha+127)/255);
            byte[] src=[straight?(byte)82:Scale(82),straight?(byte)143:Scale(143),straight?(byte)219:Scale(219),(byte)alpha];
            for(int at=0;at<16;at+=4)src.CopyTo(source.GetPixelSpan().Slice(at,4));
            using var image=SKImage.FromBitmap(source);
            using var target=new SKBitmap(new SKImageInfo(1,1,SKColorType.Rgba8888,SKAlphaType.Premul));
            using var canvas=new SKCanvas(target);
            canvas.Clear(new SKColor(10,20,30,(byte)da));byte[] dst=target.GetPixelSpan().ToArray();
            using var paint=new SKPaint{Color=new SKColor(255,255,255,(byte)opacity)};
            canvas.DrawImage(image,new SKRect(0,0,1,1),new SKSamplingOptions(SKFilterMode.Linear,SKMipmapMode.None),paint);
            rows.Add(new{straight,alpha,opacity,da,src,dst,native=target.GetPixelSpan().ToArray()});
        }
        File.WriteAllText(Path.Combine(output,"alpha-probe.json"),JsonSerializer.Serialize(rows));
        Console.WriteLine($"Generated {rows.Count} native transparent blend cases.");
    }
}
