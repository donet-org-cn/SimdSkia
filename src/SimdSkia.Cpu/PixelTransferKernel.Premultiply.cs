using System.Numerics;
using System.Runtime.InteropServices;

namespace SimdSkia.Cpu;

public static partial class PixelTransferKernel
{
    internal static bool IsOpaque(ReadOnlySpan<byte> pixels,bool enableSimd) =>
        PixelBuffer.HasOnlyOpaqueAlpha(pixels, enableSimd);

    internal static void Premultiply(Span<byte> pixels,bool swap,bool enableSimd)
        => Premultiply(pixels, pixels, swap, enableSimd);

    private static void Premultiply(ReadOnlySpan<byte> pixels, Span<byte> destination, bool swap, bool enableSimd)
    {
        var words=MemoryMarshal.Cast<byte,uint>(pixels);
        var output=MemoryMarshal.Cast<byte,uint>(destination);
        int at=0;
        if(enableSimd&&Vector.IsHardwareAccelerated)
        {
            var pairs=new Vector<uint>(0x00ff00ff);
            var rounding=new Vector<uint>(0x00800080);
            var channel=new Vector<uint>(255);
            var alphaMask=new Vector<uint>(0xff000000);
            for(;at<=words.Length-Vector<uint>.Count;at+=Vector<uint>.Count)
            {
                var source=new Vector<uint>(words[at..]);
                var alpha=source>>24;
                var rb=(source&pairs)*alpha+rounding;
                rb=((rb+((rb>>8)&pairs))>>8)&pairs;
                var green=((source>>8)&channel)*alpha+new Vector<uint>(128);
                green=(green+(green>>8))>>8;
                if(swap)rb=((rb&channel)<<16)|((rb>>16)&channel);
                (rb|(green<<8)|(source&alphaMask)).CopyTo(output[at..]);
            }
        }
        for(;at<words.Length;at++)
        {
            uint source=words[at];byte alpha=(byte)(source>>24);
            uint r=PixelBuffer.Multiply((byte)source,alpha),g=PixelBuffer.Multiply((byte)(source>>8),alpha),b=PixelBuffer.Multiply((byte)(source>>16),alpha);
            output[at]=(swap?b:r)|(g<<8)|((swap?r:b)<<16)|(source&0xff000000);
        }
    }

    internal static void CopyToPremultiplied(PixelBuffer source, Span<byte> destination, PixelFormat format)
    {
        int inputWidth = checked(source.Width * source.BytesPerPixel);
        int outputWidth = checked(source.Width * 4);
        destination = destination[..checked(outputWidth * source.Height)];
        if (source.RowBytes == inputWidth)
            Convert(source.GetPixelSpan(), destination);
        else for (int y = 0; y < source.Height; y++)
            Convert(source.GetRowSpan(y)[..inputWidth], destination.Slice(y * outputWidth, outputWidth));

        void Convert(ReadOnlySpan<byte> input, Span<byte> output)
        {
            if (source.Format == PixelFormat.Alpha8)
            {
                var words = MemoryMarshal.Cast<byte, uint>(output);
                for (int i = 0; i < input.Length; i++) words[i] = (uint)input[i] << 24;
                return;
            }
            bool swap = source.Format != format;
            bool simd = CpuOptions.EnableSimd;
            if (source.Alpha == AlphaFormat.Straight) Premultiply(input, output, swap, simd);
            else if (source.Alpha == AlphaFormat.Opaque) CopyOpaque(input, output, swap, simd);
            else if (swap) SwapRedBlue(input, output, simd);
            else input.CopyTo(output);
        }
    }

    private static void CopyOpaque(ReadOnlySpan<byte> input, Span<byte> output, bool swap, bool simd)
    {
        var source = MemoryMarshal.Cast<byte, uint>(input);
        var dest = MemoryMarshal.Cast<byte, uint>(output);
        const uint alpha = 0xff000000;
        int i = 0;
        if (simd && Vector.IsHardwareAccelerated)
        {
            var opaque = new Vector<uint>(alpha);
            var red = new Vector<uint>(255);
            var green = new Vector<uint>(0xff00);
            for (; i <= source.Length - Vector<uint>.Count; i += Vector<uint>.Count)
            {
                var p = new Vector<uint>(source[i..]);
                if (swap) p = (p & green) | ((p & red) << 16) | ((p >> 16) & red);
                (p | opaque).CopyTo(dest[i..]);
            }
        }
        for (; i < source.Length; i++)
        {
            uint p = source[i];
            dest[i] = alpha | (swap ? (p & 0xff00) | ((p & 255) << 16) | ((p >> 16) & 255) : p);
        }
    }
}
