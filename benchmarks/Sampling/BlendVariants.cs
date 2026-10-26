using SkiaSharp;

internal static class BlendVariants
{
    internal static void Run()
    {
        foreach(var filter in new[]{SKFilterMode.Nearest,SKFilterMode.Linear})
        foreach(int size in new[]{1,2,3})
        foreach(int opacity in new[]{173,255})
        {
            using var source=new SKBitmap(new SKImageInfo(2,2,SKColorType.Rgba8888,SKAlphaType.Premul));
            for(int at=0;at<16;at+=4)new byte[]{0,1,2,17}.CopyTo(source.GetPixelSpan().Slice(at,4));
            using var image=SKImage.FromBitmap(source);
            using var target=new SKBitmap(new SKImageInfo(size,size,SKColorType.Rgba8888,SKAlphaType.Premul));
            using var canvas=new SKCanvas(target);using var paint=new SKPaint{Color=new SKColor(255,255,255,(byte)opacity)};
            canvas.Clear(new SKColor(10,20,30));canvas.DrawImage(image,new SKRect(0,0,size,size),new SKSamplingOptions(filter,SKMipmapMode.None),paint);
            Console.WriteLine($"{filter}/{size}/{opacity}: {Convert.ToHexString(target.GetPixelSpan()[..4])}");
        }
    }
}
