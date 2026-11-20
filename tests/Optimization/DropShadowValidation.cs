using SimdSkia;
using SimdSkia.Cpu;

internal static class DropShadowValidation
{
    internal static int Run()
    {
        var random=new Random(20261120);
        int checks=0;
        bool originalSimd=CpuOptions.EnableSimd;
        try
        {
            foreach(bool simd in new[]{false,true})
            foreach(var format in new[]{PixelFormat.Rgba8888,PixelFormat.Bgra8888})
            foreach(var alpha in Enum.GetValues<AlphaFormat>())
            foreach(int width in new[]{1,37,96})
            {
                CpuOptions.EnableSimd=simd;
                using var source=new PixelBuffer(width,23,format,alpha,width*4+11);
                random.NextBytes(source.GetPixelSpan());
                foreach((float sigmaX,float sigmaY) in new (float,float)[]{(0,0),(.7f,2.3f),(4,3)})
                foreach((float x,float y) in new (float,float)[]{(0,0),(-3,2),(.375f,-1.7f)})
                foreach(RectF? region in new RectF?[]{null,new(-2.2f,1.25f,width-0.6f,19.75f)})
                {
                    var color=new Rgba32(43,173,119,201);
                    using var reference=Reference(source,sigmaX,sigmaY,x,y,color,region);
                    using var actual=ManagedFilters.DropShadow(source,sigmaX,sigmaY,x,y,color,region);
                    if(!actual.GetPixelSpan().SequenceEqual(reference.GetPixelSpan()))
                        throw new InvalidOperationException($"Drop shadow differed for {format}/{alpha}, width {width}, sigma {sigmaX},{sigmaY}, offset {x},{y}, region {region}, SIMD {simd}.");
                    checks++;
                }
            }
        }
        finally { CpuOptions.EnableSimd=originalSimd; }
        return checks;
    }

    // Full RGBA convolution followed by colorization, as in the previous engine.
    private static PixelBuffer Reference(PixelBuffer source,float sigmaX,float sigmaY,float x,float y,Rgba32 color,RectF? region)
    {
        static byte Multiply(byte value,byte alpha)=>(byte)((value*alpha+127)/255);
        using var rgba=ManagedFilters.Blur(source,sigmaX,sigmaY,region);
        int red=source.Format==PixelFormat.Rgba8888?0:2;
        for(int row=0;row<rgba.Height;row++)
        {
            var bytes=rgba.GetRowSpan(row);
            for(int i=0;i<bytes.Length;i+=4)
            {
                byte a=Multiply(bytes[i+3],color.A);
                bytes[i+red]=Multiply(color.R,a);
                bytes[i+1]=Multiply(color.G,a);
                bytes[i+2-red]=Multiply(color.B,a);
                bytes[i+3]=a;
            }
        }
        var result=new PixelBuffer(source.Width,source.Height,source.Format,AlphaFormat.Premultiplied);
        using var canvas=new RasterCanvas(result);
        var rect=new RectF(0,0,source.Width,source.Height);
        var paint=new Paint(new(255,255,255,255),CompositeMode.SourceOver);
        canvas.Execute(new DrawingCommand(CommandKind.DrawImage,rect,new(1,0,x,0,1,y,0,0,1),paint)
            {Image=rgba,ImageSource=rect,Sampling=ImageSampling.Bilinear});
        canvas.Execute(new DrawingCommand(CommandKind.DrawImage,rect,Matrix3.Identity,paint){Image=source});
        return result;
    }
}
