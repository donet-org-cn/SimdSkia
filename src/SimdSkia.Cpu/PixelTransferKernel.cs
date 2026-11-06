using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Cpu;

/// <summary>Allocation-free row kernels for nonoverlapping 32-bit pixels; scalar fallback is always available.</summary>
public static partial class PixelTransferKernel
{
    public static void SwapRedBlue(ReadOnlySpan<byte> source,Span<byte> destination,bool enableSimd=true)
    {
        if((source.Length&3)!=0||destination.Length<source.Length)throw new ArgumentException("Whole 32-bit pixels are required.");
        var src=MemoryMarshal.Cast<byte,uint>(source);var dst=MemoryMarshal.Cast<byte,uint>(destination[..source.Length]);int x=0;
        if(enableSimd&&Vector.IsHardwareAccelerated)
        {
            var keep=new Vector<uint>(0xff00ff00);var red=new Vector<uint>(255);int count=Vector<uint>.Count;
            for(;x<=src.Length-count;x+=count)
            {
                var pixels=new Vector<uint>(src.Slice(x,count));
                ((pixels&keep)|((pixels&red)<<16)|((pixels>>16)&red)).CopyTo(dst.Slice(x,count));
            }
        }
        for(;x<src.Length;x++){uint p=src[x];dst[x]=(p&0xff00ff00)|((p&255)<<16)|((p>>16)&255);}
    }
    public static void ScaleNearest32(ReadOnlySpan<byte> source,int sourceWidth,int sourceHeight,int sourceStride,
        Span<byte> destination,int width,int height,int stride,bool swap)
    {
        Validate(source,sourceWidth,sourceHeight,sourceStride,destination,width,height,stride);
        int[]? rented=null;Span<int> taps=width<=1024?stackalloc int[width]:(rented=ArrayPool<int>.Shared.Rent(width)).AsSpan(0,width);
        try
        {
            float sx=(float)sourceWidth/width,sy=(float)sourceHeight/height;
            for(int x=0;x<width;x++)taps[x]=Math.Clamp((int)MathF.Floor((x+.5f)*sx),0,sourceWidth-1);
            for(int y=0;y<height;y++)
            {
                int iy=Math.Clamp((int)MathF.Floor((y+.5f)*sy),0,sourceHeight-1);
                var src=MemoryMarshal.Cast<byte,uint>(source.Slice(iy*sourceStride,sourceWidth*4));var dst=MemoryMarshal.Cast<byte,uint>(destination.Slice(y*stride,width*4));
                for(int x=0;x<width;x++){uint p=src[taps[x]];dst[x]=swap?(p&0xff00ff00)|((p&255)<<16)|((p>>16)&255):p;}
            }
        }
        finally{if(rented is not null)ArrayPool<int>.Shared.Return(rented);}
    }
    public static void ScaleLinear32(ReadOnlySpan<byte> source,int sourceWidth,int sourceHeight,int sourceStride,
        Span<byte> destination,int width,int height,int stride,bool opaque,bool swap,bool enableSimd=true)
    {
        Validate(source,sourceWidth,sourceHeight,sourceStride,destination,width,height,stride);
        RasterCanvas.ImageTap[]? rented=null;
        Span<RasterCanvas.ImageTap> taps=width<=1024?stackalloc RasterCanvas.ImageTap[width]:(rented=ArrayPool<RasterCanvas.ImageTap>.Shared.Rent(width)).AsSpan(0,width);
        try
        {
            float sx=(float)sourceWidth/width,sy=(float)sourceHeight/height;
            for(int x=0;x<width;x++){float p=(x+.5f)*sx-.5f;int i=(int)MathF.Floor(p);taps[x]=new(Math.Clamp(i,0,sourceWidth-1),Math.Clamp(i+1,0,sourceWidth-1),p-i);}
            for(int y=0;y<height;y++)
            {
                float p=(y+.5f)*sy-.5f;int iy=(int)MathF.Floor(p);float fy=p-iy;
                var top=source.Slice(Math.Clamp(iy,0,sourceHeight-1)*sourceStride,sourceWidth*4);var bottom=source.Slice(Math.Clamp(iy+1,0,sourceHeight-1)*sourceStride,sourceWidth*4);
                var dst=destination.Slice(y*stride,width*4);int x=0;
                if(enableSimd&&Avx2.IsSupported)
                    for(;x<=width-8;x+=8)BilinearSampler.Sample8(top,bottom,taps.Slice(x,8),fy,opaque,false,swap,dst.Slice(x*4,32));
                for(;x<width;x++)
                {
                    var tap=taps[x];uint pixel=SampleScalar(top,bottom,tap.First,tap.Second,tap.Fraction,fy,opaque);
                    if(swap)pixel=(pixel&0xff00ff00)|((pixel&255)<<16)|((pixel>>16)&255);
                    BinaryPrimitives.WriteUInt32LittleEndian(dst.Slice(x*4,4),pixel);
                }
            }
        }
        finally{if(rented is not null)ArrayPool<RasterCanvas.ImageTap>.Shared.Return(rented);}
    }
    private static uint SampleScalar(ReadOnlySpan<byte> top,ReadOnlySpan<byte> bottom,int first,int second,float fx,float fy,bool opaque)
    {
        uint a=BinaryPrimitives.ReadUInt32LittleEndian(top.Slice(first*4,4)),b=BinaryPrimitives.ReadUInt32LittleEndian(top.Slice(second*4,4)),c=BinaryPrimitives.ReadUInt32LittleEndian(bottom.Slice(first*4,4)),d=BinaryPrimitives.ReadUInt32LittleEndian(bottom.Slice(second*4,4));
        if(opaque){a|=0xff000000;b|=0xff000000;c|=0xff000000;d|=0xff000000;}
        float w00=(1-fx)*(1-fy),w10=fx*(1-fy),w01=(1-fx)*fy,w11=fx*fy;
        int Channel(int shift)
        {
            float value=((a>>shift)&255)*w00+((b>>shift)&255)*w10;value+=((c>>shift)&255)*w01;value+=((d>>shift)&255)*w11;
            return Math.Clamp((int)MathF.Round(value),0,255);
        }
        int alpha=Channel(24);return (uint)(Math.Min(alpha,Channel(0))|(Math.Min(alpha,Channel(8))<<8)|(Math.Min(alpha,Channel(16))<<16)|(alpha<<24));
    }
    private static void Validate(ReadOnlySpan<byte> source,int sw,int sh,int ss,Span<byte> destination,int w,int h,int ds)
    {
        if(sw<=0||sh<=0||w<=0||h<=0||ss<(long)sw*4||ds<(long)w*4||
            source.Length<(long)(sh-1)*ss+sw*4L||destination.Length<(long)(h-1)*ds+w*4L)throw new ArgumentException("Pixel geometry exceeds the supplied spans.");
        if(source.Overlaps(destination))throw new ArgumentException("Scale source and destination must not overlap.");
    }
}
