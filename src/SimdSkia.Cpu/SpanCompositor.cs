using System.Numerics;
using System.Runtime.InteropServices;

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

    private static Vector<ushort> Divide255(Vector<ushort> n) => (n+Vector<ushort>.One+(n>>8))>>8;

    internal static void Fill(Span<byte> pixels,byte first,byte second,byte third,byte fourth)
    {
        uint packed=BitConverter.IsLittleEndian ? (uint)(first|second<<8|third<<16|fourth<<24) : (uint)(first<<24|second<<16|third<<8|fourth);
        var words=MemoryMarshal.Cast<byte,uint>(pixels);int i=0;
        if(CpuOptions.EnableSimd && Vector.IsHardwareAccelerated)
        {var v=new Vector<uint>(packed);for(;i<=words.Length-Vector<uint>.Count;i+=Vector<uint>.Count)v.CopyTo(words[i..]);}
        for(;i<words.Length;i++)words[i]=packed;
    }
}
