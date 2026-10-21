using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Cpu;

internal static partial class SpanCompositor
{
    public static void ApplyCoverage(Span<byte> pixels,ReadOnlySpan<byte> coverage,PixelFormat format,AlphaFormat alpha,Paint paint)
    {
        int x=0;
        if(CpuOptions.EnableSimd&&Avx2.IsSupported&&alpha!=AlphaFormat.Straight)
        {
            var c=paint.Color;bool clear=paint.Composite==CompositeMode.Clear;
            ushort first=clear?(ushort)0:format==PixelFormat.Rgba8888?c.R:c.B;
            ushort green=clear?(ushort)0:c.G,third=clear?(ushort)0:format==PixelFormat.Rgba8888?c.B:c.R,sourceAlpha=clear?(ushort)0:(ushort)255;
            var color=Vector256.Create(first,green,third,sourceAlpha,first,green,third,sourceAlpha,first,green,third,sourceAlpha,first,green,third,sourceAlpha);
            var opacity=Vector256.Create((ushort)c.A);var maximum=Vector256.Create((ushort)255);var rounding=Vector256.Create((ushort)127);
            var shuffle=Vector256.Create((byte)0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3,4,4,4,4,5,5,5,5,6,6,6,6,7,7,7,7);
            for(;x<=coverage.Length-8;x+=8)
            {
                ulong mask=Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetReference(coverage),x));
                var bytes=Vector128.CreateScalar(mask).AsByte();
                var expanded=Avx2.Shuffle(Vector256.Create(bytes,bytes),shuffle);
                var covLow=Avx2.ConvertToVector256Int16(expanded.GetLower()).AsUInt16();
                var covHigh=Avx2.ConvertToVector256Int16(expanded.GetUpper()).AsUInt16();
                var aLow=Divide(Avx2.Add(Avx2.MultiplyLow(covLow,opacity),rounding));
                var aHigh=Divide(Avx2.Add(Avx2.MultiplyLow(covHigh,opacity),rounding));
                var lowRemain=Avx2.Subtract(maximum,paint.Composite==CompositeMode.SourceOver?aLow:covLow);
                var highRemain=Avx2.Subtract(maximum,paint.Composite==CompositeMode.SourceOver?aHigh:covHigh);
                var destination=Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(pixels),(nuint)(x*4));
                var low=Avx2.ConvertToVector256Int16(destination.GetLower()).AsUInt16();
                var high=Avx2.ConvertToVector256Int16(destination.GetUpper()).AsUInt16();
                low=Avx2.Add(Divide(Avx2.Add(Avx2.MultiplyLow(low,lowRemain),rounding)),Divide(Avx2.Add(Avx2.MultiplyLow(color,aLow),rounding)));
                high=Avx2.Add(Divide(Avx2.Add(Avx2.MultiplyLow(high,highRemain),rounding)),Divide(Avx2.Add(Avx2.MultiplyLow(color,aHigh),rounding)));
                var result=Avx2.Permute4x64(Avx2.PackUnsignedSaturate(low.AsInt16(),high.AsInt16()).AsUInt64(),0xd8).AsByte();
                result.StoreUnsafe(ref MemoryMarshal.GetReference(pixels),(nuint)(x*4));
            }
        }
        for(;x<coverage.Length;)
        {
            byte value=coverage[x];int end=x+1;
            while(end<coverage.Length&&coverage[end]==value)end++;
            if(value!=0)Apply(pixels.Slice(x*4,(end-x)*4),format,alpha,paint,value);
            x=end;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ushort> Divide(Vector256<ushort> value)=>Avx2.ShiftRightLogical(Avx2.Add(Avx2.Add(value,Vector256.Create((ushort)1)),Avx2.ShiftRightLogical(value,8)),8);
}
