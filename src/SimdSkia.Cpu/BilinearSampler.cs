using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Cpu;

internal static class BilinearSampler
{
    // Gather exactly eight validated texels per tap. Pointers are pinned only
    // for the gathers; row indices were clamped when the mapping was built.
    internal static unsafe void Sample8(ReadOnlySpan<byte> row0,ReadOnlySpan<byte> row1,
        ReadOnlySpan<RasterCanvas.ImageTap> taps,float fy,bool opaque,bool straight,bool swap,Span<byte> output)
    {
        var first=Vector256.Create(taps[0].First,taps[1].First,taps[2].First,taps[3].First,taps[4].First,taps[5].First,taps[6].First,taps[7].First);
        var second=Vector256.Create(taps[0].Second,taps[1].Second,taps[2].Second,taps[3].Second,taps[4].Second,taps[5].Second,taps[6].Second,taps[7].Second);
        var fx=Vector256.Create(taps[0].Fraction,taps[1].Fraction,taps[2].Fraction,taps[3].Fraction,taps[4].Fraction,taps[5].Fraction,taps[6].Fraction,taps[7].Fraction);
        Vector256<int> p00,p10,p01,p11;
        fixed(byte* top=row0)fixed(byte* bottom=row1)
        {
            p00=Avx2.GatherVector256((int*)top,first,4);p10=Avx2.GatherVector256((int*)top,second,4);
            p01=Avx2.GatherVector256((int*)bottom,first,4);p11=Avx2.GatherVector256((int*)bottom,second,4);
        }
        var mask=Vector256.Create(255);var zero=Vector256<int>.Zero;
        var a00=opaque?mask:Avx2.ShiftRightLogical(p00,24);var a10=opaque?mask:Avx2.ShiftRightLogical(p10,24);
        var a01=opaque?mask:Avx2.ShiftRightLogical(p01,24);var a11=opaque?mask:Avx2.ShiftRightLogical(p11,24);
        var other=Avx.Subtract(Vector256.Create(1f),fx);
        var w00=Avx.Multiply(other,Vector256.Create(1-fy));var w10=Avx.Multiply(fx,Vector256.Create(1-fy));
        var w01=Avx.Multiply(other,Vector256.Create(fy));var w11=Avx.Multiply(fx,Vector256.Create(fy));
        Vector256<int> Mix(Vector256<int> c00,Vector256<int> c10,Vector256<int> c01,Vector256<int> c11)
        {
            var sum=Avx.Add(Avx.Multiply(Avx.ConvertToVector256Single(c00),w00),Avx.Multiply(Avx.ConvertToVector256Single(c10),w10));
            sum=Avx.Add(sum,Avx.Multiply(Avx.ConvertToVector256Single(c01),w01));
            sum=Avx.Add(sum,Avx.Multiply(Avx.ConvertToVector256Single(c11),w11));
            return Avx.ConvertToVector256Int32WithTruncation(Avx.RoundToNearestInteger(sum));
        }
        var alpha=Avx2.Min(mask,Avx2.Max(zero,Mix(a00,a10,a01,a11)));
        Vector256<int> Premultiply(Vector256<int> color,Vector256<int> a)
        {
            if(!straight)return color;
            var n=Avx2.Add(Avx2.MultiplyLow(color,a),Vector256.Create(127));
            return Avx2.ShiftRightLogical(Avx2.Add(Avx2.Add(n,Vector256.Create(1)),Avx2.ShiftRightLogical(n,8)),8);
        }
        Vector256<int> Channel(Vector256<int> c00,Vector256<int> c10,Vector256<int> c01,Vector256<int> c11)=>
            Avx2.Min(alpha,Avx2.Max(zero,Mix(Premultiply(Avx2.And(c00,mask),a00),Premultiply(Avx2.And(c10,mask),a10),Premultiply(Avx2.And(c01,mask),a01),Premultiply(Avx2.And(c11,mask),a11))));
        var red=Channel(p00,p10,p01,p11);
        var green=Channel(Avx2.ShiftRightLogical(p00,8),Avx2.ShiftRightLogical(p10,8),Avx2.ShiftRightLogical(p01,8),Avx2.ShiftRightLogical(p11,8));
        var blue=Channel(Avx2.ShiftRightLogical(p00,16),Avx2.ShiftRightLogical(p10,16),Avx2.ShiftRightLogical(p01,16),Avx2.ShiftRightLogical(p11,16));
        if(swap)(red,blue)=(blue,red);
        var packed=Avx2.Or(Avx2.Or(red,Avx2.ShiftLeftLogical(green,8)),Avx2.Or(Avx2.ShiftLeftLogical(blue,16),Avx2.ShiftLeftLogical(alpha,24)));
        packed.AsByte().CopyTo(output);
    }
    // Four lanes are the four physical color channels. Keep the scalar kernel's
    // operation order and ties-to-even rounding; do not fuse multiply/adds.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Sample(ReadOnlySpan<byte> row0,ReadOnlySpan<byte> row1,int first,int second,float fx,float fy,bool opaque)
    {
        static Vector128<float> Load(ReadOnlySpan<byte> row,int x,bool opaque)
        {
            uint pixel=BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(x*4,4));
            if(opaque)pixel|=0xff000000;
            var bytes=Vector128.CreateScalar(pixel).AsByte();
            var words=Sse2.UnpackLow(bytes,Vector128<byte>.Zero).AsUInt16();
            return Sse2.ConvertToVector128Single(Sse2.UnpackLow(words,Vector128<ushort>.Zero).AsInt32());
        }
        float w00=(1-fx)*(1-fy),w10=fx*(1-fy),w01=(1-fx)*fy,w11=fx*fy;
        var sum=Sse.Add(Sse.Multiply(Load(row0,first,opaque),Vector128.Create(w00)),Sse.Multiply(Load(row0,second,opaque),Vector128.Create(w10)));
        sum=Sse.Add(sum,Sse.Multiply(Load(row1,first,opaque),Vector128.Create(w01)));
        sum=Sse.Add(sum,Sse.Multiply(Load(row1,second,opaque),Vector128.Create(w11)));
        Vector128<int> rounded;
        if(Sse41.IsSupported)rounded=Sse2.ConvertToVector128Int32WithTruncation(Sse41.RoundToNearestInteger(sum));
        else
        {
            // Explicit ties-to-even, independent of the caller's MXCSR rounding
            // mode. Samples are nonnegative, so truncation gives floor here.
            var floor=Sse2.ConvertToVector128Int32WithTruncation(sum);
            var fraction=Sse.Subtract(sum,Sse2.ConvertToVector128Single(floor));
            var half=Vector128.Create(.5f);var one=Vector128.Create(1);
            var odd=Sse2.CompareEqual(Sse2.And(floor,one),one);
            var increment=Sse2.Or(Sse.CompareGreaterThan(fraction,half).AsInt32(),Sse2.And(Sse.CompareEqual(fraction,half).AsInt32(),odd));
            rounded=Sse2.Add(floor,Sse2.And(increment,one));
        }
        // Clamp RGB to the interpolated alpha even for caller-provided PMA
        // buffers containing physical RGB values greater than their alpha.
        var alpha=Sse2.Shuffle(rounded,0xff);
        var above=Sse2.CompareGreaterThan(rounded,alpha);
        rounded=Sse2.Or(Sse2.And(above,alpha),Sse2.AndNot(above,rounded));
        return Sse2.PackUnsignedSaturate(Sse2.PackSignedSaturate(rounded,rounded),Vector128<short>.Zero).AsUInt32().GetElement(0);
    }
}
