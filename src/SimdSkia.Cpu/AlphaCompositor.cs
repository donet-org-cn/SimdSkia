using System.Numerics;
namespace SimdSkia.Cpu;
internal static class AlphaCompositor
{
    internal static bool TryApply(Span<byte> pixels,Paint paint,byte coverage)
    {
        if(paint.Blender is not null||paint.FloatingColor is not null||paint.ColorFilter is not null||paint.Composite is not (CompositeMode.SourceOver or CompositeMode.Source or CompositeMode.Clear or CompositeMode.Destination))return false;
        if(pixels.IsEmpty||coverage==0||paint.Composite==CompositeMode.Destination)return true;
        if(coverage==255&&(paint.Composite is CompositeMode.Source or CompositeMode.Clear||paint.Color.A==255))
        {pixels.Fill(paint.Composite==CompositeMode.Clear?(byte)0:paint.Color.A);return true;}
        byte sa=paint.Composite==CompositeMode.Clear?(byte)0:PixelBuffer.Multiply(paint.Color.A,coverage);if(paint.Composite==CompositeMode.SourceOver&&sa==0)return true;
        byte remain=paint.Composite==CompositeMode.SourceOver?(byte)(255-sa):(byte)(255-coverage);int at=0;
        if(CpuOptions.EnableSimd&&Vector.IsHardwareAccelerated)
        {
            var factor=new Vector<ushort>(remain);var source=new Vector<ushort>(sa);var half=new Vector<ushort>(127);var max=new Vector<ushort>(255);
            for(;at<=pixels.Length-Vector<byte>.Count;at+=Vector<byte>.Count)
            {Vector.Widen(new Vector<byte>(pixels[at..]),out Vector<ushort> lo,out Vector<ushort> hi);lo=Vector.Min(max,SpanCompositor.Divide255(lo*factor+half)+source);hi=Vector.Min(max,SpanCompositor.Divide255(hi*factor+half)+source);Vector.Narrow(lo,hi).CopyTo(pixels[at..]);}
        }
        for(;at<pixels.Length;at++)pixels[at]=(byte)Math.Min(255,sa+PixelBuffer.Multiply(pixels[at],remain));return true;
    }
}
