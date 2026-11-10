using System.Numerics;
using System.Runtime.InteropServices;

namespace SimdSkia.Cpu;

public static partial class PixelTransferKernel
{
    internal static bool IsOpaque(ReadOnlySpan<byte> pixels,bool enableSimd)
    {
        var words=MemoryMarshal.Cast<byte,uint>(pixels);
        int at=0;
        if(enableSimd&&Vector.IsHardwareAccelerated)
        {
            var mask=new Vector<uint>(0xff000000);
            for(;at<=words.Length-Vector<uint>.Count;at+=Vector<uint>.Count)
                if(!Vector.EqualsAll(new Vector<uint>(words[at..])&mask,mask))return false;
        }
        for(;at<words.Length;at++)if((words[at]&0xff000000)!=0xff000000)return false;
        return true;
    }

    internal static void Premultiply(Span<byte> pixels,bool swap,bool enableSimd)
    {
        var words=MemoryMarshal.Cast<byte,uint>(pixels);
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
                (rb|(green<<8)|(source&alphaMask)).CopyTo(words[at..]);
            }
        }
        for(;at<words.Length;at++)
        {
            uint source=words[at];byte alpha=(byte)(source>>24);
            uint r=PixelBuffer.Multiply((byte)source,alpha),g=PixelBuffer.Multiply((byte)(source>>8),alpha),b=PixelBuffer.Multiply((byte)(source>>16),alpha);
            words[at]=(swap?b:r)|(g<<8)|((swap?r:b)<<16)|(source&0xff000000);
        }
    }
}
