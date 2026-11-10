using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
namespace SimdSkia.Cpu;
public static partial class BlendKernel
{
    // Float stages retain their full precision. Eight PMA pixels are unpacked
    // and rounded together; no conversion to a byte paint precedes blending.
    private static int UniformFloat(Span<byte> pixels,PixelFormat format,AlphaFormat alpha,Vector4 source,CompositeMode mode,bool constrainPma)
    {
        if(!CpuOptions.EnableSimd||alpha==AlphaFormat.Straight||!float.IsFinite(source.X)||!float.IsFinite(source.Y)||!float.IsFinite(source.Z)||!float.IsFinite(source.W))return 0;
        if(mode is CompositeMode.Source or CompositeMode.Clear)
        {
            var s=mode==CompositeMode.Clear?Vector4.Zero:source;float a=Math.Clamp(s.W,0,1),limit=constrainPma?a:1;
            byte r=ColorBlend.Byte(Math.Clamp(s.X,0,limit)),g=ColorBlend.Byte(Math.Clamp(s.Y,0,limit)),b=ColorBlend.Byte(Math.Clamp(s.Z,0,limit));SpanCompositor.Fill(pixels,format==PixelFormat.Rgba8888?r:b,g,format==PixelFormat.Rgba8888?b:r,ColorBlend.Byte(a));return pixels.Length;
        }
        if(mode!=CompositeMode.SourceOver)return 0;
        if(!Avx2.IsSupported)return UniformFloat128(pixels,format,alpha,source,constrainPma);
        if(pixels.Length<32)return 0;
        float r0=format==PixelFormat.Rgba8888?source.X:source.Z,b0=format==PixelFormat.Rgba8888?source.Z:source.X;
        var color=Vector256.Create(r0,source.Y,b0,source.W,r0,source.Y,b0,source.W);var factor=Vector256.Create(1-source.W);var scale=Vector256.Create(1f/255);var bytes=Vector256.Create(255f);var zero=Vector256<float>.Zero;var one=Vector256.Create(1f);var clearAlpha=Vector256.Create(-1,-1,-1,0,-1,-1,-1,0).AsSingle();var opaqueAlpha=Vector256.Create(0f,0,0,1,0,0,0,1);bool opaque=alpha==AlphaFormat.Opaque;
        Vector256<int> Blend(Vector256<int> v)
        {
            var d=Avx.Multiply(Avx.ConvertToVector256Single(v),scale);if(opaque)d=Avx.Or(Avx.And(d,clearAlpha),opaqueAlpha);
            var result=Avx.Add(color,Avx.Multiply(d,factor));var limit=constrainPma?Avx.Min(Avx.Max(Avx.Shuffle(result,result,255),zero),one):one;result=Avx.Min(Avx.Max(result,zero),limit);
            return Avx.ConvertToVector256Int32(Avx.Multiply(result,bytes));
        }
        int at=0;var z=Vector256<byte>.Zero;var zw=Vector256<ushort>.Zero;
        for(;at<=pixels.Length-32;at+=32)
        {
            var d=Vector256.LoadUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(pixels),(nuint)at);var lo=Avx2.UnpackLow(d,z).AsUInt16();var hi=Avx2.UnpackHigh(d,z).AsUInt16();
            var first=Avx2.PackSignedSaturate(Blend(Avx2.UnpackLow(lo,zw).AsInt32()),Blend(Avx2.UnpackHigh(lo,zw).AsInt32()));var second=Avx2.PackSignedSaturate(Blend(Avx2.UnpackLow(hi,zw).AsInt32()),Blend(Avx2.UnpackHigh(hi,zw).AsInt32()));Avx2.PackUnsignedSaturate(first,second).StoreUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(pixels),(nuint)at);
        }
        return at;
    }
    private static int UniformFloat128(Span<byte> pixels,PixelFormat format,AlphaFormat alpha,Vector4 source,bool constrainPma)
    {
        if(!Sse2.IsSupported||pixels.Length<16)return 0;
        float r=format==PixelFormat.Rgba8888?source.X:source.Z,b=format==PixelFormat.Rgba8888?source.Z:source.X;
        var color=Vector128.Create(r,source.Y,b,source.W);var factor=Vector128.Create(1-source.W);var scale=Vector128.Create(1f/255);var bytes=Vector128.Create(255f);var zero=Vector128<float>.Zero;var one=Vector128.Create(1f);var clearAlpha=Vector128.Create(-1,-1,-1,0).AsSingle();var opaqueAlpha=Vector128.Create(0f,0,0,1);bool opaque=alpha==AlphaFormat.Opaque;
        Vector128<int> Blend(Vector128<int> v)
        {
            var d=Sse.Multiply(Sse2.ConvertToVector128Single(v),scale);if(opaque)d=Sse.Or(Sse.And(d,clearAlpha),opaqueAlpha);
            var result=Sse.Add(color,Sse.Multiply(d,factor));var limit=constrainPma?Sse.Min(Sse.Max(Sse.Shuffle(result,result,255),zero),one):one;result=Sse.Min(Sse.Max(result,zero),limit);return Sse2.ConvertToVector128Int32(Sse.Multiply(result,bytes));
        }
        int at=0;var z=Vector128<byte>.Zero;var zw=Vector128<ushort>.Zero;
        for(;at<=pixels.Length-16;at+=16)
        {
            var d=Vector128.LoadUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(pixels),(nuint)at);var lo=Sse2.UnpackLow(d,z).AsUInt16();var hi=Sse2.UnpackHigh(d,z).AsUInt16();
            var first=Sse2.PackSignedSaturate(Blend(Sse2.UnpackLow(lo,zw).AsInt32()),Blend(Sse2.UnpackHigh(lo,zw).AsInt32()));var second=Sse2.PackSignedSaturate(Blend(Sse2.UnpackLow(hi,zw).AsInt32()),Blend(Sse2.UnpackHigh(hi,zw).AsInt32()));Sse2.PackUnsignedSaturate(first,second).StoreUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(pixels),(nuint)at);
        }
        return at;
    }
}
