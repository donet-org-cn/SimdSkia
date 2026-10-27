using System.Numerics;
using System.Runtime.InteropServices;

namespace SimdSkia.Cpu;

public static class CpuOptions
{
    public static bool EnableSimd {get;set;}=true;
    public static string ClearImplementation=>EnableSimd&&Vector.IsHardwareAccelerated?$"Vector{Vector<uint>.Count*32}":"Scalar";
}

public static class ClearKernel
{
    public static void Fill(PixelBuffer target,Rgba32 color,bool useSimd)
    {
        target.ThrowIfDisposed();byte a=color.A,r=color.R,g=color.G,b=color.B;
        if(target.Alpha!=AlphaFormat.Straight){r=PixelBuffer.Multiply(r,a);g=PixelBuffer.Multiply(g,a);b=PixelBuffer.Multiply(b,a);}
        byte first=target.Format==PixelFormat.Rgba8888?r:b,third=target.Format==PixelFormat.Rgba8888?b:r;
        uint packed=BitConverter.IsLittleEndian?(uint)(first|(g<<8)|(third<<16)|(a<<24)):(uint)((first<<24)|(g<<16)|(third<<8)|a);
        // Span.Fill uses the runtime's unrolled vector stores. Treat a tight
        // image as one span, while never writing into padded scanlines.
        if(useSimd&&Vector.IsHardwareAccelerated&&target.RowBytes==target.Width*4)
        {
            var pixels=target.GetPixelSpan();
            if(first==g&&first==third&&first==a)pixels.Fill(first);
            else MemoryMarshal.Cast<byte,uint>(pixels).Fill(packed);
            return;
        }
        for(int y=0;y<target.Height;y++)
        {
            var row=MemoryMarshal.Cast<byte,uint>(target.GetRowSpan(y)[..checked(target.Width*4)]);
            if(useSimd&&Vector.IsHardwareAccelerated)
                row.Fill(packed);
            else for(int i=0;i<row.Length;i++)row[i]=packed;
        }
    }
}
