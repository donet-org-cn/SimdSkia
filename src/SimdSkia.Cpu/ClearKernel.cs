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
        var vector=new Vector<uint>(packed);
        for(int y=0;y<target.Height;y++)
        {
            var row=MemoryMarshal.Cast<byte,uint>(target.GetRowSpan(y)[..checked(target.Width*4)]);int i=0;
            if(useSimd&&Vector.IsHardwareAccelerated)
                for(;i<=row.Length-Vector<uint>.Count;i+=Vector<uint>.Count)vector.CopyTo(row[i..]);
            for(;i<row.Length;i++)row[i]=packed;
        }
    }
}
