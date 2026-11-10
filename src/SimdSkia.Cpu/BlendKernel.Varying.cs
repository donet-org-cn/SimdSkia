using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Cpu;

public static partial class BlendKernel
{
    internal static void ApplyVaryingSourceOver(Span<byte> pixels,ReadOnlySpan<Vector4> sources,PixelFormat format,AlphaFormat alpha)
    {
        int at=0;
        if(CpuOptions.EnableSimd&&Avx2.IsSupported)
        {
            var one=Vector256.Create(1f);
            var scale=Vector256.Create(1f/255);
            var bytes=Vector256.Create(255f);
            var values=MemoryMarshal.Cast<Vector4,Vector256<float>>(sources);
            bool swap=format==PixelFormat.Bgra8888,opaque=alpha==AlphaFormat.Opaque;
            var alphaMask=Vector256.Create(-1,-1,-1,0,-1,-1,-1,0).AsSingle();
            var opaqueAlpha=Vector256.Create(0f,0,0,1,0,0,0,1);
            for(;at<=sources.Length-2;at+=2)
            {
                ulong raw=System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(pixels.Slice(at*4,8));
                var channels=Avx2.ConvertToVector256Int32(Vector128.CreateScalar(raw).AsByte());
                var destination=Avx.Multiply(Avx.ConvertToVector256Single(channels),scale);
                if(opaque)destination=Avx.Or(Avx.And(destination,alphaMask),opaqueAlpha);
                var source=values[at/2];
                if(swap)source=Avx.Shuffle(source,source,0xc6);
                var value=Avx.Add(source,Avx.Multiply(destination,Avx.Subtract(one,Avx.Shuffle(source,source,255))));
                value=Avx.Min(one,Avx.Max(Vector256<float>.Zero,value));
                var rounded=Avx.ConvertToVector256Int32(Avx.Multiply(value,bytes));
                var packed=Sse2.PackSignedSaturate(rounded.GetLower(),rounded.GetUpper());
                ulong output=Sse2.PackUnsignedSaturate(packed,Vector128<short>.Zero).AsUInt64().GetElement(0);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(pixels.Slice(at*4,8),output);
            }
        }
        if(CpuOptions.EnableSimd&&Sse2.IsSupported)
        {
            var zero=Vector128<float>.Zero;
            var one=Vector128.Create(1f);
            var scale=Vector128.Create(1f/255);
            var bytes=Vector128.Create(255f);
            var words=MemoryMarshal.Cast<byte,uint>(pixels);
            var values=MemoryMarshal.Cast<Vector4,Vector128<float>>(sources);
            bool swap=format==PixelFormat.Bgra8888,opaque=alpha==AlphaFormat.Opaque;
            for(;at<sources.Length;at++)
            {
                var raw=Vector128.CreateScalar(words[at]).AsByte();
                var channels=Sse2.UnpackLow(Sse2.UnpackLow(raw,Vector128<byte>.Zero).AsUInt16(),Vector128<ushort>.Zero).AsInt32();
                var destination=Sse.Multiply(Sse2.ConvertToVector128Single(channels),scale);
                if(opaque)destination=destination.WithElement(3,1);
                var source=values[at];
                if(swap)source=Sse.Shuffle(source,source,0xc6);
                var value=Sse.Add(source,Sse.Multiply(destination,Sse.Subtract(one,Sse.Shuffle(source,source,255))));
                // Keep NaNs in the second operand. Conversion/packing maps them
                // to zero, matching ColorBlend.Byte, including invalid RGB data.
                value=Sse.Min(one,Sse.Max(zero,value));
                var rounded=Sse2.ConvertToVector128Int32(Sse.Multiply(value,bytes));
                var packed=Sse2.PackSignedSaturate(rounded,Vector128<int>.Zero);
                words[at]=Sse2.PackUnsignedSaturate(packed,Vector128<short>.Zero).AsUInt32().GetElement(0);
            }
        }
        for(;at<sources.Length;at++)
            ApplyVector(pixels.Slice(at*4,4),format,alpha,sources[at],CompositeMode.SourceOver,255,false);
    }
}
