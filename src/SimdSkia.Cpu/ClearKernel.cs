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
    public static void FillRectangle(PixelBuffer target,Rgba32 color,int left,int top,int right,int bottom,bool useSimd)
    {
        target.ThrowIfDisposed();left=Math.Max(0,left);top=Math.Max(0,top);right=Math.Min(target.Width,right);bottom=Math.Min(target.Height,bottom);
        if(right<=left||bottom<=top)return;
        if(target.Format==PixelFormat.Alpha8){for(int y=top;y<bottom;y++){var row=target.GetRowSpan(y).Slice(left,right-left);if(useSimd)row.Fill(color.A);else for(int i=0;i<row.Length;i++)row[i]=color.A;}return;}
        uint packed=Pack(target,color);
        if(left==0&&right==target.Width&&target.RowBytes==right*4)
        {
            FillSpan(target.GetPixelSpan().Slice(top*target.RowBytes,(bottom-top)*target.RowBytes),packed,useSimd);
            return;
        }
        for(int y=top;y<bottom;y++)
            FillSpan(target.GetRowSpan(y).Slice(left*4,(right-left)*4),packed,useSimd);
    }
    private static uint Pack(PixelBuffer target,Rgba32 color)
    {
        byte a=color.A,r=color.R,g=color.G,b=color.B;
        if(target.Alpha!=AlphaFormat.Straight){r=PixelBuffer.Multiply(r,a);g=PixelBuffer.Multiply(g,a);b=PixelBuffer.Multiply(b,a);}
        byte first=target.Format==PixelFormat.Rgba8888?r:b,third=target.Format==PixelFormat.Rgba8888?b:r;
        return BitConverter.IsLittleEndian?(uint)(first|(g<<8)|(third<<16)|(a<<24)):(uint)((first<<24)|(g<<16)|(third<<8)|a);
    }
    public static void Fill(PixelBuffer target,Rgba32 color,bool useSimd)
        =>FillRectangle(target,color,0,0,target.Width,target.Height,useSimd);

    private static void FillSpan(Span<byte> pixels,uint packed,bool useSimd)
    {
        var words=MemoryMarshal.Cast<byte,uint>(pixels);
        if(useSimd&&Vector.IsHardwareAccelerated)
        {
            if(packed==(byte)packed*0x01010101u)pixels.Fill((byte)packed);
            else words.Fill(packed);
            return;
        }
        for(int i=0;i<words.Length;i++)words[i]=packed;
    }
}
