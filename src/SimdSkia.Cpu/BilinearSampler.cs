using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Cpu;

internal static class BilinearSampler
{
    // Four lanes are the four physical color channels. Keep the scalar kernel's
    // operation order and ties-to-even rounding; do not fuse multiply/adds.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Sample(ReadOnlySpan<byte> row0,ReadOnlySpan<byte> row1,int first,int second,float fx,float fy,bool opaque)
    {
        static Vector128<float> Load(ReadOnlySpan<byte> row,int x,bool opaque)
        {
            uint pixel=BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(x*4,4));
            if(opaque)pixel|=0xff000000;
            return Sse2.ConvertToVector128Single(Sse41.ConvertToVector128Int32(Vector128.CreateScalar(pixel).AsByte()));
        }
        float w00=(1-fx)*(1-fy),w10=fx*(1-fy),w01=(1-fx)*fy,w11=fx*fy;
        var sum=Sse.Add(Sse.Multiply(Load(row0,first,opaque),Vector128.Create(w00)),Sse.Multiply(Load(row0,second,opaque),Vector128.Create(w10)));
        sum=Sse.Add(sum,Sse.Multiply(Load(row1,first,opaque),Vector128.Create(w01)));
        sum=Sse.Add(sum,Sse.Multiply(Load(row1,second,opaque),Vector128.Create(w11)));
        var rounded=Sse2.ConvertToVector128Int32WithTruncation(Sse41.RoundToNearestInteger(sum));
        // Clamp RGB to the interpolated alpha even for caller-provided PMA
        // buffers containing physical RGB values greater than their alpha.
        var alpha=Sse2.Shuffle(rounded,0xff);
        rounded=Sse41.Min(rounded,alpha);
        return Sse2.PackUnsignedSaturate(Sse2.PackSignedSaturate(rounded,rounded),Vector128<short>.Zero).AsUInt32().GetElement(0);
    }
}
