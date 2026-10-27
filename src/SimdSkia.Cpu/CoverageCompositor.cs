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
        if(CpuOptions.EnableSimd&&Sse2.IsSupported&&alpha!=AlphaFormat.Straight&&paint.Composite==CompositeMode.SourceOver&&paint.Color==new Rgba32(0,0,0,255))
            x+=ApplyBlackCoverage(pixels[(x*4)..],coverage[x..]);
        // SSE2 is part of the x64 baseline. Native AOT's portable build cannot
        // use the AVX2 path, but must not turn every glyph into scalar spans.
        if(CpuOptions.EnableSimd&&Sse2.IsSupported&&alpha!=AlphaFormat.Straight&&x<=coverage.Length-4)
        {
            var c=paint.Color;bool clear=paint.Composite==CompositeMode.Clear;
            ushort first=clear?(ushort)0:format==PixelFormat.Rgba8888?c.R:c.B;
            ushort green=clear?(ushort)0:c.G,third=clear?(ushort)0:format==PixelFormat.Rgba8888?c.B:c.R;
            ushort sourceAlpha=clear?(ushort)0:(ushort)255;
            var color=Vector128.Create(first,green,third,sourceAlpha,first,green,third,sourceAlpha);
            var opacity=Vector128.Create((ushort)c.A);var maximum=Vector128.Create((ushort)255);
            var rounding=Vector128.Create((ushort)127);var zero=Vector128<byte>.Zero;
            bool binaryColor=(first is 0 or 255)&&(green is 0 or 255)&&(third is 0 or 255);
            for(;x<=coverage.Length-4;x+=4)
            {
                uint mask=Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetReference(coverage),x));
                if(mask==0)continue;
                var bytes=Vector128.CreateScalar(mask).AsByte();
                var pairs=Sse2.UnpackLow(bytes,bytes).AsUInt16();
                var expanded=Sse2.UnpackLow(pairs,pairs).AsByte();
                var covLow=Sse2.UnpackLow(expanded,zero).AsUInt16();
                var covHigh=Sse2.UnpackHigh(expanded,zero).AsUInt16();
                var aLow=c.A==255?covLow:Divide(Sse2.Add(Sse2.MultiplyLow(covLow,opacity),rounding));
                var aHigh=c.A==255?covHigh:Divide(Sse2.Add(Sse2.MultiplyLow(covHigh,opacity),rounding));
                var lowRemain=Sse2.Subtract(maximum,paint.Composite==CompositeMode.SourceOver?aLow:covLow);
                var highRemain=Sse2.Subtract(maximum,paint.Composite==CompositeMode.SourceOver?aHigh:covHigh);
                var destination=Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(pixels),(nuint)(x*4));
                var low=Sse2.UnpackLow(destination,zero).AsUInt16();
                var high=Sse2.UnpackHigh(destination,zero).AsUInt16();
                var sourceLow=binaryColor?Sse2.And(color,aLow):Divide(Sse2.Add(Sse2.MultiplyLow(color,aLow),rounding));
                var sourceHigh=binaryColor?Sse2.And(color,aHigh):Divide(Sse2.Add(Sse2.MultiplyLow(color,aHigh),rounding));
                low=Sse2.Add(Divide(Sse2.Add(Sse2.MultiplyLow(low,lowRemain),rounding)),sourceLow);
                high=Sse2.Add(Divide(Sse2.Add(Sse2.MultiplyLow(high,highRemain),rounding)),sourceHigh);
                // The sum is at most 510, so signed-short saturation to bytes
                // implements the scalar min(255) without SSE4.1 unsigned min.
                Sse2.PackUnsignedSaturate(low.AsInt16(),high.AsInt16()).StoreUnsafe(ref MemoryMarshal.GetReference(pixels),(nuint)(x*4));
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
    private static Vector256<ushort> Divide(Vector256<ushort> value)=>Avx2.MultiplyHigh(Avx2.Add(value,Vector256.Create((ushort)1)),Vector256.Create((ushort)257));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> Divide(Vector128<ushort> value)=>Sse2.MultiplyHigh(Sse2.Add(value,Vector128.Create((ushort)1)),Vector128.Create((ushort)257));

    // The caller has checked SSE2, premultiplied/opaque pixels and opaque black
    // SourceOver once for the entire mask. Keep its short row tails here too.
    internal static void ApplyOpaqueBlackCoverage(Span<byte> pixels,ReadOnlySpan<byte> coverage)
    {
        int x=ApplyBlackCoverage(pixels,coverage);
        for(;x<coverage.Length;x++)
        {
            byte value=coverage[x];if(value==0)continue;
            int offset=x*4;byte remaining=(byte)(255-value);
            pixels[offset]=PixelBuffer.Multiply(pixels[offset],remaining);
            pixels[offset+1]=PixelBuffer.Multiply(pixels[offset+1],remaining);
            pixels[offset+2]=PixelBuffer.Multiply(pixels[offset+2],remaining);
            pixels[offset+3]=(byte)(value+PixelBuffer.Multiply(pixels[offset+3],remaining));
        }
    }

    private static int ApplyBlackCoverage(Span<byte> pixels,ReadOnlySpan<byte> coverage)
    {
        var zero=Vector128<byte>.Zero;var maximum=Vector128.Create((ushort)255);
        var rounding=Vector128.Create((ushort)128);var reciprocal=Vector128.Create((ushort)257);
        var alphaMask=Vector128.Create(0xff000000u).AsByte();int x=0;
        for(;x<=coverage.Length-4;x+=4)
        {
            uint mask=Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetReference(coverage),x));
            if(mask==0)continue;
            var bytes=Vector128.CreateScalar(mask).AsByte();
            var pairs=Sse2.UnpackLow(bytes,bytes).AsUInt16();
            var expanded=Sse2.UnpackLow(pairs,pairs).AsByte();
            var destination=Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(pixels),(nuint)(x*4));
            // Opaque white is a frequent text background. Check the actual
            // pixels (not a retained background assumption): rounded
            // 255*(255-coverage)/255 is exactly 255-coverage for every byte.
            if(Sse2.MoveMask(Sse2.CompareEqual(destination,Vector128<byte>.AllBitsSet))==0xffff)
            {
                Sse2.Or(Sse2.Xor(expanded,Vector128<byte>.AllBitsSet),alphaMask).StoreUnsafe(ref MemoryMarshal.GetReference(pixels),(nuint)(x*4));
                continue;
            }
            var lowFactor=Sse2.Subtract(maximum,Sse2.UnpackLow(expanded,zero).AsUInt16());
            var highFactor=Sse2.Subtract(maximum,Sse2.UnpackHigh(expanded,zero).AsUInt16());
            var low=Sse2.MultiplyHigh(Sse2.Add(Sse2.MultiplyLow(Sse2.UnpackLow(destination,zero).AsUInt16(),lowFactor),rounding),reciprocal);
            var high=Sse2.MultiplyHigh(Sse2.Add(Sse2.MultiplyLow(Sse2.UnpackHigh(destination,zero).AsUInt16(),highFactor),rounding),reciprocal);
            // RGB source is zero; only alpha is added. For any destination
            // alpha in [0,255], coverage + rounded(dst*(255-coverage)/255)
            // is <=255, so byte addition cannot wrap.
            var result=Sse2.Add(Sse2.PackUnsignedSaturate(low.AsInt16(),high.AsInt16()),Sse2.And(expanded,alphaMask));
            result.StoreUnsafe(ref MemoryMarshal.GetReference(pixels),(nuint)(x*4));
        }
        return x;
    }
}
