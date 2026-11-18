using SimdSkia;
using SimdSkia.Cpu;
using SimdSkia.Gpu;
using SimdSkia.Gpu.Direct2D;
using SkiaSharp;
using System.Runtime.InteropServices;
int checks=0;void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
var raw=new byte[32*4];var pin=GCHandle.Alloc(raw,GCHandleType.Pinned);int releases=0;
try
{
    var b=new SKBitmap();Check(b.InstallPixels(new(4,4,SKColorType.Rgba8888,SKAlphaType.Premul),pin.AddrOfPinnedObject(),32,(_,_)=>releases++),"Install");b.Erase(SKColors.Red);b.SetImmutable();var image=SKImage.FromBitmap(b);var filter=SKImageFilter.CreateImage(image,new SKSamplingOptions(SKFilterMode.Nearest));var paint=new SKPaint{ImageFilter=filter};b.Dispose();image.Dispose();filter.Dispose();Check(releases==0,"Paint owns image");
    using var bitmap=new SKBitmap(16,16);using var canvas=new SKCanvas(bitmap);int saved=canvas.SaveLayer(paint);paint.Dispose();Check(releases==0,"Layer retains immutable image graph");using(var fill=new SKPaint())canvas.DrawRect(0,0,8,8,fill);GC.Collect();GC.WaitForPendingFinalizers();Check(releases==0,"GC cannot release live layer graph");canvas.RestoreToCount(saved);Check(bitmap.GetPixel(1,1)==SKColors.Red,"Disposed source still draws at restore");Check(releases==1,"Last restore releases pixels once");canvas.Dispose();Check(releases==1,"Idempotent disposal");
}
finally{pin.Free();}
// Ordinary factory graphs are immutable even when all original wrappers are disposed.
using(var b=new SKBitmap(29,23))using(var c=new SKCanvas(b))using(var f=SKImageFilter.CreateBlur(1,2))using(var p=new SKPaint{Color=SKColors.Blue,ImageFilter=f})
{
    f.Dispose();using var copy=p.Clone();p.Dispose();c.DrawRect(7,6,8,7,copy);Check(b.GetPixel(6,7).Alpha>0,"Clone retains node");copy.Reset();Check(copy.ImageFilter is null,"Reset clears filter");
}
using(var imageFilter=SKImageFilter.CreateBlur(1,1))
{
    imageFilter.Dispose();bool threw=false;try{using var p=new SKPaint{ImageFilter=imageFilter};}catch(ObjectDisposedException){threw=true;}Check(threw,"Disposed filter assignment rejects");
}
using(var a=new PixelBuffer(16,12,PixelFormat.Bgra8888,AlphaFormat.Premultiplied))
{
    void Reject(ICanvasBackend gpu)
    {
        gpu.Execute(DrawingCommand.Clear(new(15,30,60,255)));gpu.Flush();var before=a.CopyBytes();
        var filter=RasterImageFilter.Blur(1,1,3);bool rejected=false;try{gpu.Execute(DrawingCommand.FillRectangle(new(1,1,9,7),Matrix3.Identity,new Paint(new(200,40,80,128),CompositeMode.SourceOver){ImageFilter=filter}));}catch(NotSupportedException){rejected=true;}gpu.Flush();Check(rejected&&a.GetPixelSpan().SequenceEqual(before),"GPU rejects unsupported filter before mutation");
    }
    using(var vulkan=new VulkanCanvas(a))Reject(vulkan);
    if(OperatingSystem.IsWindows()){using var direct2D=new Direct2DCanvas(a);Reject(direct2D);}
}
// Zero and sub-pixel sigma, large-window/tent blur, all padded physical bytes,
// sampled stages and both fallback settings must preserve the same result.
foreach(var format in new[]{PixelFormat.Rgba8888,PixelFormat.Bgra8888})foreach(var alpha in Enum.GetValues<AlphaFormat>())foreach(float sigma in new[]{0,.2f,.8f,1.2f,4,136})
{
    using var a=new PixelBuffer(17,9,format,alpha,80);using var b=new PixelBuffer(17,9,format,alpha,80);using var x=new RasterCanvas(a);using var y=new RasterCanvas(b);var f=RasterImageFilter.Blur(sigma,0,3);
    var command=DrawingCommand.FillRectangle(new(2,1,11,7),Matrix3.Identity,new Paint(new(20,170,80,99),CompositeMode.SourceOver){ImageFilter=f});
    CpuOptions.EnableSimd=false;x.Execute(command);CpuOptions.EnableSimd=true;y.Execute(command);
    for(int row=0;row<9;row++)for(int i=0;i<80;i++)Check(a.GetRowSpan(row)[i]==b.GetRowSpan(row)[i],"SIMD/fallback filter byte equality including stride");
}
foreach(var size in new[]{(1,1),(2,7),(9,3),(31,17)})foreach(float sigma in new[]{0,.2f,.7f,.8f,1.1f,1.4f,2.8f,7.3f,15.7f,60.4f,134,135,136,255.9f,256})
{
    using var p=new PixelBuffer(size.Item1,size.Item2,PixelFormat.Bgra8888,AlphaFormat.Premultiplied,size.Item1*4+12);var random=new Random(size.Item1*17+(int)(sigma*10));
    for(int y=0;y<p.Height;y++)for(int x=0;x<p.Width;x++)p.WritePixel(x,y,new((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256)));
    using var frame=new FilterFrame(p.Share(),-3,5);var filter=RasterImageFilter.Blur(sigma,sigma*.61f,3,null,frame.Bounds);CpuOptions.EnableSimd=false;using var scalar=filter.Apply(frame,Matrix3.Identity,frame.Bounds);CpuOptions.EnableSimd=true;using var vector=filter.Apply(frame,Matrix3.Identity,frame.Bounds);
    Check(scalar.Bounds==vector.Bounds,"Blur frame origin and cropping");var a=scalar.Pixels!.GetPixelSpan();var b=vector.Pixels!.GetPixelSpan();for(int i=0;i<a.Length;i++)Check(a[i]==b[i],"Fused ring/reciprocal division versus independent 64-bit three-pass scalar");
}
// Compare overlapping and partial windows against a direct byte-wise 2D reduction.
foreach (var format in new[] { PixelFormat.Bgra8888, PixelFormat.Rgba8888 })
foreach (var alpha in Enum.GetValues<AlphaFormat>())
{
    using var pixels = new PixelBuffer(17, 9, format, alpha, 73);
    for (int y = 0; y < pixels.Height; y++)
    for (int x = 0; x < pixels.Width; x++)
        pixels.WritePixel(x, y, new((byte)(x * 13 + y * 29), (byte)(x * 79 + y * 11),
            (byte)(x * 19 + y * 23), (byte)(x * 97 + y * 17)));
    using var source = new FilterFrame(pixels.Share(), -3, 5);
    var crop = new RectF(-5, 4, 12, 14);
    foreach (var (rx, ry) in new[] { (0, 0), (2, 1), (1, 3), (4, 2), (34, 0), (0, 36) })
    foreach (bool dilate in new[] { false, true })
    {
        var filter = RasterImageFilter.Morphology(rx, ry, dilate, crop: crop);
        using var result = filter.Apply(source, Matrix3.Identity, crop);
        var output = result.Pixels!;
        int mx = dilate ? rx : 0, my = dilate ? ry : 0;
        var expectedBounds = new RectF(Math.Max(crop.Left, source.Bounds.Left - mx),
            Math.Max(crop.Top, source.Bounds.Top - my), Math.Min(crop.Right, source.Bounds.Right + mx),
            Math.Min(crop.Bottom, source.Bounds.Bottom + my));
        Check(result.Bounds == expectedBounds, "Morphology result bounds and origin");
        for (int y = 0; y < output.Height; y++)
        for (int x = 0; x < output.Width; x++)
        for (int c = 0; c < 4; c++)
        {
            byte expected = dilate ? (byte)0 : (byte)255;
            int centerX = result.Left + x - source.Left, centerY = result.Top + y - source.Top;
            for (int dy = -ry; dy <= ry; dy++)
            for (int dx = -rx; dx <= rx; dx++)
            {
                int sx = centerX + dx, sy = centerY + dy;
                byte value = 0;
                if ((uint)sx < (uint)pixels.Width && (uint)sy < (uint)pixels.Height)
                {
                    var input = pixels.GetRowSpan(sy).Slice(sx * 4, 4);
                    int channel = format == PixelFormat.Rgba8888 && (c == 0 || c == 2) ? 2 - c : c;
                    value = c == 3 && alpha == AlphaFormat.Opaque ? (byte)255 : input[channel];
                    if (c < 3 && alpha == AlphaFormat.Straight)
                        value = PixelBuffer.Multiply(value, input[3]);
                }
                expected = dilate ? Math.Max(expected, value) : Math.Min(expected, value);
            }
            Check(output.GetRowSpan(y)[x * 4 + c] == expected, "Morphology with stride, alpha, crop and transparent edges");
        }
    }
}
CpuOptions.EnableSimd=true;Console.WriteLine($"PASS {checks} image-filter lifetime, GPU rejection and scalar/SIMD checks");
