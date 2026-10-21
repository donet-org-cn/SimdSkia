using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Cpu;

internal static class ImageCompositor
{
    internal static void SourceOver(ReadOnlySpan<byte> source,Span<byte> destination,byte opacity,bool sourceOpaque)
    {
        int at=0;
        if(CpuOptions.EnableSimd&&Avx2.IsSupported)
        {
            var indices=Vector256.Create((byte)3,3,3,3,7,7,7,7,11,11,11,11,15,15,15,15,3,3,3,3,7,7,7,7,11,11,11,11,15,15,15,15);
            var scale=Vector256.Create((ushort)opacity);var max=Vector256.Create((ushort)255);var round=Vector256.Create((ushort)127);
            static Vector256<ushort> Divide(Vector256<ushort> x)=>Avx2.ShiftRightLogical(Avx2.Add(Avx2.Add(x,Vector256.Create((ushort)1)),Avx2.ShiftRightLogical(x,8)),8);
            for(;at<=source.Length-32;at+=32)
            {
                var s=Vector256.LoadUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(source), (nuint)at);
                var d=Vector256.LoadUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(destination),(nuint)at);
                if(sourceOpaque)s=Avx2.Or(s,Vector256.Create(0xff000000u).AsByte());
                var zero=Vector256<byte>.Zero;
                var lo=Avx2.UnpackLow(s,zero).AsUInt16();var hi=Avx2.UnpackHigh(s,zero).AsUInt16();
                if(opacity!=255){lo=Divide(Avx2.Add(Avx2.MultiplyLow(lo,scale),round));hi=Divide(Avx2.Add(Avx2.MultiplyLow(hi,scale),round));s=Avx2.PackUnsignedSaturate(lo.AsInt16(),hi.AsInt16());}
                var inverse=Avx2.Subtract(Vector256.Create((byte)255),Avx2.Shuffle(s,indices));
                var factorLo=Avx2.UnpackLow(inverse,zero).AsUInt16();var factorHi=Avx2.UnpackHigh(inverse,zero).AsUInt16();
                var dlo=Avx2.UnpackLow(d,zero).AsUInt16();var dhi=Avx2.UnpackHigh(d,zero).AsUInt16();
                lo=Avx2.Min(max,Avx2.Add(lo,Divide(Avx2.Add(Avx2.MultiplyLow(dlo,factorLo),round))));
                hi=Avx2.Min(max,Avx2.Add(hi,Divide(Avx2.Add(Avx2.MultiplyLow(dhi,factorHi),round))));
                Avx2.PackUnsignedSaturate(lo.AsInt16(),hi.AsInt16()).StoreUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(destination),(nuint)at);
            }
        }
        for(;at<source.Length;at+=4)
        {
            byte a=PixelBuffer.Multiply(sourceOpaque?(byte)255:source[at+3],opacity),remainder=(byte)(255-a);
            for(int c=0;c<4;c++){byte s=c==3&&sourceOpaque?(byte)255:source[at+c];destination[at+c]=(byte)Math.Min(255,PixelBuffer.Multiply(s,opacity)+PixelBuffer.Multiply(destination[at+c],remainder));}
        }
    }
}
