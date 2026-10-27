using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Cpu;

/// <summary>Constant coverage/source spans, with identical integer rounding on every backend.</summary>
internal static partial class SpanCompositor
{
    public static void Apply(Span<byte> pixels, PixelFormat format, AlphaFormat alpha, Paint paint, byte coverage = 255)
    {
        if (pixels.IsEmpty || coverage == 0) return;
        var c = paint.Color;
        int ri = format == PixelFormat.Rgba8888 ? 0 : 2, bi = 2-ri;
        if (coverage == 255)
        {
            if (paint.Composite == CompositeMode.Clear) { pixels.Clear(); return; }
            if (paint.Composite == CompositeMode.Source || (paint.Composite == CompositeMode.SourceOver && c.A == 255))
            {
                byte r=c.R,g=c.G,b=c.B;
                if (alpha != AlphaFormat.Straight) { r=PixelBuffer.Multiply(r,c.A);g=PixelBuffer.Multiply(g,c.A);b=PixelBuffer.Multiply(b,c.A); }
                Fill(pixels, format == PixelFormat.Rgba8888 ? r : b,g,format == PixelFormat.Rgba8888 ? b : r,c.A);
                return;
            }
            if (paint.Composite == CompositeMode.SourceOver && c.A == 0) return;
        }
        byte sa=PixelBuffer.Multiply(c.A,coverage);
        byte remainder=paint.Composite == CompositeMode.SourceOver ? (byte)(255-sa) : (byte)(255-coverage);
        byte sr=paint.Composite == CompositeMode.Clear ? (byte)0 : PixelBuffer.Multiply(c.R,sa);
        byte sg=paint.Composite == CompositeMode.Clear ? (byte)0 : PixelBuffer.Multiply(c.G,sa);
        byte sb=paint.Composite == CompositeMode.Clear ? (byte)0 : PixelBuffer.Multiply(c.B,sa);
        byte sourceAlpha=paint.Composite == CompositeMode.Clear ? (byte)0 : sa;
        int offset=0;
        if (alpha != AlphaFormat.Straight && CpuOptions.EnableSimd && Vector.IsHardwareAccelerated && pixels.Length >= Vector<byte>.Count)
        {
            Span<byte> pattern=stackalloc byte[Vector<byte>.Count];
            for(int j=0;j<pattern.Length;j+=4) {pattern[j+ri]=sr;pattern[j+1]=sg;pattern[j+bi]=sb;pattern[j+3]=sourceAlpha;}
            Vector.Widen(new Vector<byte>(pattern),out Vector<ushort> slo,out Vector<ushort> shi);
            var factor=new Vector<ushort>(remainder);
            for(;offset<=pixels.Length-Vector<byte>.Count;offset+=Vector<byte>.Count)
            {
                Vector.Widen(new Vector<byte>(pixels[offset..]),out Vector<ushort> lo,out Vector<ushort> hi);
                lo=Vector.Min(new Vector<ushort>(255),Divide255(lo*factor+new Vector<ushort>(127))+slo);
                hi=Vector.Min(new Vector<ushort>(255),Divide255(hi*factor+new Vector<ushort>(127))+shi);
                Vector.Narrow(lo,hi).CopyTo(pixels[offset..]);
            }
        }
        for(;offset<pixels.Length;offset+=4)
        {
            var p=pixels.Slice(offset,4);byte da=p[3],r=p[ri],g=p[1],b=p[bi];
            if(alpha == AlphaFormat.Straight) {r=PixelBuffer.Multiply(r,da);g=PixelBuffer.Multiply(g,da);b=PixelBuffer.Multiply(b,da);}
            byte oa=(byte)Math.Min(255,PixelBuffer.Multiply(da,remainder)+sourceAlpha);
            r=(byte)Math.Min(255,PixelBuffer.Multiply(r,remainder)+sr);
            g=(byte)Math.Min(255,PixelBuffer.Multiply(g,remainder)+sg);
            b=(byte)Math.Min(255,PixelBuffer.Multiply(b,remainder)+sb);
            if(alpha == AlphaFormat.Straight) {r=PixelBuffer.Unmultiply(r,oa);g=PixelBuffer.Unmultiply(g,oa);b=PixelBuffer.Unmultiply(b,oa);}
            p[ri]=r;p[1]=g;p[bi]=b;p[3]=oa;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<ushort> Divide255(Vector<ushort> n)
    {
        // n = channel * alpha + 127, so 0 <= n <= 65152.
        // floor((n+1)*257/65536) is exactly floor((n)/255) on this
        // input domain. Size guards make the value reinterpretations exact.
        if(Avx2.IsSupported&&Vector<ushort>.Count==16)
        {
            var value=Unsafe.As<Vector<ushort>,Vector256<ushort>>(ref n);
            var result=Avx2.MultiplyHigh(Avx2.Add(value,Vector256.Create((ushort)1)),Vector256.Create((ushort)257));
            return Unsafe.As<Vector256<ushort>,Vector<ushort>>(ref result);
        }
        if(Sse2.IsSupported&&Vector<ushort>.Count==8)
        {
            var value=Unsafe.As<Vector<ushort>,Vector128<ushort>>(ref n);
            var result=Sse2.MultiplyHigh(Sse2.Add(value,Vector128.Create((ushort)1)),Vector128.Create((ushort)257));
            return Unsafe.As<Vector128<ushort>,Vector<ushort>>(ref result);
        }
        return (n+Vector<ushort>.One+(n>>8))>>8;
    }

    internal static void Fill(Span<byte> pixels,byte first,byte second,byte third,byte fourth)
    {
        uint packed=BitConverter.IsLittleEndian ? (uint)(first|second<<8|third<<16|fourth<<24) : (uint)(first<<24|second<<16|third<<8|fourth);
        var words=MemoryMarshal.Cast<byte,uint>(pixels);
        if(CpuOptions.EnableSimd && Vector.IsHardwareAccelerated)
        {
            // Equal-byte pixels (including transparent clear and opaque white)
            // can use the runtime's bulk byte fill instead of generic uint fill.
            if(first==second&&first==third&&first==fourth)pixels.Fill(first);
            else if(Sse2.IsSupported&&!Avx2.IsSupported&&pixels.Length is >=16384 and <=262144)
            {
                // Measured portable-AOT hotspot: medium solid rectangles fit in
                // cache. Repeating an initialized 1 KiB block lets the runtime
                // bulk copy accelerate this range, without raising the ISA floor.
                // Large surfaces keep Fill to avoid extra memory-read traffic.
                words[..256].Fill(packed);int filled=1024;
                while(filled<pixels.Length)
                {int count=Math.Min(filled,pixels.Length-filled);pixels[..count].CopyTo(pixels[filled..]);filled+=count;}
            }
            else words.Fill(packed);
            return;
        }
        for(int i=0;i<words.Length;i++)words[i]=packed;
    }
}
