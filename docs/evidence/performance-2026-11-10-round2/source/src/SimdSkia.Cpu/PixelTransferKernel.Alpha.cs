using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
namespace SimdSkia.Cpu;
public static partial class PixelTransferKernel
{
    /// <summary>Skia raster-pipeline PMA to straight conversion, including float operation order and ties-to-even.</summary>
    public static void Unpremultiply(ReadOnlySpan<byte> source,Span<byte> destination,bool swap=false,bool enableSimd=true)
    {
        if((source.Length&3)!=0||destination.Length<source.Length)throw new ArgumentException("Whole 32-bit pixels are required.");
        if(source.Overlaps(destination,out int overlap)&&overlap!=0)throw new ArgumentException("Partial pixel overlap requires a snapshot.");
        var src=MemoryMarshal.Cast<byte,uint>(source);var dst=MemoryMarshal.Cast<byte,uint>(destination[..source.Length]);int x=0;
        if(enableSimd&&Vector256.IsHardwareAccelerated)
            for(;x<=src.Length-8;x+=8)Convert(Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(src),(nuint)x),swap).StoreUnsafe(ref MemoryMarshal.GetReference(dst),(nuint)x);
        if(enableSimd&&Vector128.IsHardwareAccelerated)
            for(;x<=src.Length-4;x+=4)Convert(Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(src),(nuint)x),swap).StoreUnsafe(ref MemoryMarshal.GetReference(dst),(nuint)x);
        for(;x<src.Length;x++)
        {
            uint p=src[x],a=p>>24;if(a==0){dst[x]=0;continue;}float inverse=1/(a*(1f/255));
            uint Channel(int shift){float value=(((p>>shift)&255)*(1f/255))*inverse*255;return(uint)Math.Clamp((int)MathF.Round(value,MidpointRounding.ToEven),0,255);}
            uint r=Channel(swap?16:0),g=Channel(8),b=Channel(swap?0:16);dst[x]=r|(g<<8)|(b<<16)|(a<<24);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> Convert(Vector256<uint> pixels,bool swap)
    {
        var mask=Vector256.Create(255u);var normalized=Vector256.Create(1f/255);var scale=Vector256.Create(255f);var alpha=pixels>>24;
        var inverse=Vector256.Create(1f)/(Vector256.ConvertToSingle(alpha.AsInt32())*normalized);
        Vector256<uint> Channel(Vector256<uint> c)
        {var f=Vector256.ConvertToSingle(c.AsInt32())*normalized;f=f*inverse;f=f*scale;f=Vector256.Min(scale,Vector256.Max(Vector256<float>.Zero,f));return Vector256.ConvertToInt32(Vector256.Round(f)).AsUInt32();}
        var r=Channel(pixels&mask);var g=Channel((pixels>>8)&mask);var b=Channel((pixels>>16)&mask);
        var result=(swap?b:r)|(g<<8)|((swap?r:b)<<16)|(alpha<<24);return result&~Vector256.Equals(alpha,Vector256<uint>.Zero);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> Convert(Vector128<uint> pixels,bool swap)
    {
        var mask=Vector128.Create(255u);var normalized=Vector128.Create(1f/255);var scale=Vector128.Create(255f);var alpha=pixels>>24;
        var inverse=Vector128.Create(1f)/(Vector128.ConvertToSingle(alpha.AsInt32())*normalized);
        Vector128<uint> Channel(Vector128<uint> c)
        {var f=Vector128.ConvertToSingle(c.AsInt32())*normalized;f=f*inverse;f=f*scale;f=Vector128.Min(scale,Vector128.Max(Vector128<float>.Zero,f));return Vector128.ConvertToInt32(Vector128.Round(f)).AsUInt32();}
        var r=Channel(pixels&mask);var g=Channel((pixels>>8)&mask);var b=Channel((pixels>>16)&mask);
        var result=(swap?b:r)|(g<<8)|((swap?r:b)<<16)|(alpha<<24);return result&~Vector128.Equals(alpha,Vector128<uint>.Zero);
    }
}
