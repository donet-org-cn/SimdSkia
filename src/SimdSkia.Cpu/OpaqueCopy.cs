using System.Numerics;
using System.Runtime.InteropServices;

namespace SimdSkia.Cpu;

internal static partial class SpanCompositor
{
    // Opaque image metadata makes stored alpha immaterial. Force the destination
    // alpha to 255 even when the source buffer contains other physical bytes.
    internal static void CopyOpaque(ReadOnlySpan<byte> source,Span<byte> destination)
    {
        source.CopyTo(destination);var words=MemoryMarshal.Cast<byte,uint>(destination);int i=0;uint alphaBits=BitConverter.IsLittleEndian?0xff000000u:0x000000ffu;
        if(CpuOptions.EnableSimd&&Vector.IsHardwareAccelerated)
        {
            var alpha=new Vector<uint>(alphaBits);
            for(;i<=words.Length-Vector<uint>.Count;i+=Vector<uint>.Count)
                (new Vector<uint>(words.Slice(i,Vector<uint>.Count))|alpha).CopyTo(words.Slice(i,Vector<uint>.Count));
        }
        for(;i<words.Length;i++)words[i]|=alphaBits;
    }
}
