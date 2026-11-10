using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
namespace SimdSkia.Cpu;

internal static class MultiplyKernel
{
    // Equivalent to the scalar m119 byte equation. Saturating 16-bit additions
    // also preserve clamping for caller-supplied PMA bytes with RGB > alpha.
    internal static int Apply(Span<byte> pixels,PixelFormat format,bool opaque,Rgba32 source)
    {
        int ri=format==PixelFormat.Rgba8888?0:2;ushort first=ri==0?source.R:source.B,third=ri==0?source.B:source.R;
        var s=Vector256.Create(first,(ushort)source.G,third,(ushort)source.A,first,(ushort)source.G,third,(ushort)source.A,first,(ushort)source.G,third,(ushort)source.A,first,(ushort)source.G,third,(ushort)source.A);
        var max=Vector256.Create((ushort)255);var factor=Avx2.Add(Vector256.Create((ushort)(255-source.A)),s);var zero=Vector256<byte>.Zero;
        Vector256<ushort> Blend(Vector256<ushort> d)
        {
            var alpha=Avx2.ShuffleHigh(Avx2.ShuffleLow(d,0xff),0xff);
            var sum=Avx2.AddSaturate(Avx2.MultiplyLow(s,Avx2.Subtract(max,alpha)),Avx2.MultiplyLow(d,factor));
            return Avx2.ShiftRightLogical(Avx2.AddSaturate(sum,max),8);
        }
        int at=0;
        for(;at<=pixels.Length-32;at+=32)
        {
            var d=Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(pixels),(nuint)at);if(opaque)d=Avx2.Or(d,Vector256.Create(0xff000000u).AsByte());
            var low=Blend(Avx2.UnpackLow(d,zero).AsUInt16());var high=Blend(Avx2.UnpackHigh(d,zero).AsUInt16());
            Avx2.PackUnsignedSaturate(low.AsInt16(),high.AsInt16()).StoreUnsafe(ref MemoryMarshal.GetReference(pixels),(nuint)at);
        }
        return at;
    }
}
