using System.Buffers;
using System.Runtime.InteropServices;
using System.Numerics;
using SimdSkia;
using SimdSkia.Cpu;

namespace SkiaSharp;

/// <summary>Stride-aware pixel transfers. No retained images or per-pixel objects.</summary>
internal static class PixelOperations
{
    internal static PixelBuffer External(SKImageInfo info,IntPtr address,int rowBytes,Action? release=null)=>ExternalPixelOperations.Create(info,address,rowBytes,release);
    internal static void Validate(SKImageInfo info,int stride)
    {
        if(info.ColorType is not (SKColorType.Alpha8 or SKColorType.Rgba8888 or SKColorType.Bgra8888 or SKColorType.Unknown))
            throw new NotSupportedException($"Pixel format {info.ColorType} is not implemented.");
        if(info.ColorSpace is not null)_=info.ColorSpace.IsSrgb;
        if(!info.IsEmpty&&info.ColorType!=SKColorType.Unknown)
        {
            if(info.AlphaType is not (SKAlphaType.Opaque or SKAlphaType.Premul or SKAlphaType.Unpremul))throw new NotSupportedException("Unknown alpha cannot be accessed.");
            if(stride<info.RowBytes||(info.BytesPerPixel==4&&(stride&3)!=0))throw new ArgumentOutOfRangeException(nameof(stride));
            _=Length(info,stride);
        }
    }
    internal static int Length(SKImageInfo info,int stride)=>info.IsEmpty||info.ColorType==SKColorType.Unknown?0:checked((info.Height-1)*stride+info.RowBytes);
    // The caller owns external memory. Keep the unsafe boundary here; all kernels use bounded Spans.
    internal static unsafe Span<byte> Wrap(SKImageInfo info,IntPtr address,int stride)
    {Validate(info,stride);return address==IntPtr.Zero?Span<byte>.Empty:new Span<byte>((void*)address,Length(info,stride));}
    internal static SKColor ReadColor(ReadOnlySpan<byte> bytes,int at,SKImageInfo info)
    {
        if(info.ColorType==SKColorType.Alpha8)return new(0,0,0,bytes[at]);
        byte a=bytes[at+3];int red=info.ColorType==SKColorType.Rgba8888?0:2;
        byte r=bytes[at+red],g=bytes[at+1],b=bytes[at+2-red];
        if(info.AlphaType==SKAlphaType.Premul){r=PixelBuffer.Unmultiply(r,a);g=PixelBuffer.Unmultiply(g,a);b=PixelBuffer.Unmultiply(b,a);}
        return new(r,g,b,a);
    }
    internal static SKColorF ReadColorF(ReadOnlySpan<byte> bytes,int at,SKImageInfo info)
    {
        if(info.ColorType==SKColorType.Alpha8)return new(0,0,0,info.IsOpaque?1:bytes[at]*(1f/255));
        int red=info.ColorType==SKColorType.Rgba8888?0:2;float a=info.AlphaType==SKAlphaType.Opaque?1:bytes[at+3]*(1f/255);
        float scale=info.AlphaType==SKAlphaType.Premul?(a==0?0:1f/(255*a)):1f/255;
        return new(bytes[at+red]*scale,bytes[at+1]*scale,bytes[at+2-red]*scale,a);
    }
    internal static void WriteColor(Span<byte> bytes,int at,SKImageInfo info,SKColor color)
    {
        if(info.ColorType==SKColorType.Alpha8){bytes[at]=color.Alpha;return;}
        byte r=color.Red,g=color.Green,b=color.Blue,a=color.Alpha;
        if(info.AlphaType!=SKAlphaType.Unpremul){r=PixelBuffer.Multiply(r,a);g=PixelBuffer.Multiply(g,a);b=PixelBuffer.Multiply(b,a);}
        int red=info.ColorType==SKColorType.Rgba8888?0:2;
        bytes[at+red]=r;bytes[at+1]=g;bytes[at+2-red]=b;bytes[at+3]=a;
    }
    internal static void WriteColorF(Span<byte> bytes,int at,SKImageInfo info,Vector4 straight)
    {
        if(info.ColorType==SKColorType.Alpha8){bytes[at]=SKColorF.ToByte(straight.W);return;}
        straight.W=Math.Clamp(straight.W,0,1);
        if(info.AlphaType==SKAlphaType.Opaque)straight.W=1;
        if(info.AlphaType==SKAlphaType.Premul){straight.X*=straight.W;straight.Y*=straight.W;straight.Z*=straight.W;}
        straight=Vector4.Clamp(straight,Vector4.Zero,Vector4.One);
        static byte Byte(float v)=>(byte)Math.Clamp((int)MathF.Floor(v*255+.5f),0,255);
        int red=info.ColorType==SKColorType.Rgba8888?0:2;
        bytes[at+red]=Byte(straight.X);bytes[at+1]=Byte(straight.Y);bytes[at+2-red]=Byte(straight.Z);bytes[at+3]=Byte(straight.W);
    }
    internal static void WriteTaggedColor(Span<byte> bytes,int at,SKImageInfo info,SKColor color)
    {
        if(info.ColorSpace is null||info.ColorSpace.IsSrgb){WriteColor(bytes,at,info,color);return;}
        var transform=new SKColorSpace.Converter(SKColorSpace.CreateSrgbLinear(),info.ColorSpace);
        WriteColorF(bytes,at,info,transform.Convert(SKColorSpace.DecodeColor(color)));
    }
    internal static bool Read(ReadOnlySpan<byte> source,SKImageInfo srcInfo,int srcStride,Span<byte> destination,SKImageInfo dstInfo,int dstStride,int srcX,int srcY)
    {
        Validate(srcInfo,srcStride);Validate(dstInfo,dstStride);
        if(source.IsEmpty||destination.IsEmpty||srcInfo.IsEmpty||dstInfo.IsEmpty)return false;
        long dx=Math.Max(0L,-(long)srcX),dy=Math.Max(0L,-(long)srcY),sx=Math.Max(0L,srcX),sy=Math.Max(0L,srcY);
        long width=Math.Min((long)srcInfo.Width-sx,dstInfo.Width-dx),height=Math.Min((long)srcInfo.Height-sy,dstInfo.Height-dy);
        if(width<=0||height<=0)return false;
        if(srcInfo.ColorType==SKColorType.Alpha8||dstInfo.ColorType==SKColorType.Alpha8)return AlphaPixelOperations.Transfer(source,srcInfo,srcStride,destination,dstInfo,dstStride,(int)sx,(int)sy,(int)dx,(int)dy,(int)width,(int)height);
        int from=checked((int)(sy*srcStride+sx*srcInfo.BytesPerPixel)),to=checked((int)(dy*dstStride+dx*4)),w=(int)width,h=(int)height;
        source=source[from..];destination=destination[to..];
        // Snapshot only genuinely overlapping transfers, including aliases with different strides.
        byte[]? rented=null;
        try
        {
            if(source.Overlaps(destination))
            {
                int length=checked(w*h*4);rented=ArrayPool<byte>.Shared.Rent(length);
                for(int y=0;y<h;y++)source.Slice(y*srcStride,w*4).CopyTo(rented.AsSpan(y*w*4,w*4));
                source=rented.AsSpan(0,length);srcStride=w*4;
            }
            bool convert=SKColorSpace.NeedsConversion(srcInfo.ColorSpace,dstInfo.ColorSpace);
            var transform=convert?new SKColorSpace.Converter(srcInfo.ColorSpace,dstInfo.ColorSpace):default;
            bool raw=!convert&&(srcInfo.AlphaType==dstInfo.AlphaType||srcInfo.AlphaType==SKAlphaType.Opaque||dstInfo.AlphaType==SKAlphaType.Opaque);
            if(raw&&srcInfo.ColorType==dstInfo.ColorType&&srcStride==w*4&&dstStride==w*4)
            {source[..checked(w*h*4)].CopyTo(destination);return true;}
            for(int y=0;y<h;y++)
            {
                var src=source.Slice(y*srcStride,w*4);var dst=destination.Slice(y*dstStride,w*4);
                if(raw&&srcInfo.ColorType==dstInfo.ColorType){src.CopyTo(dst);continue;}
                if(raw){PixelTransferKernel.SwapRedBlue(src,dst,CpuOptions.EnableSimd);continue;}
                if(!convert&&srcInfo.AlphaType==SKAlphaType.Premul&&dstInfo.AlphaType==SKAlphaType.Unpremul)
                {PixelTransferKernel.Unpremultiply(src,dst,srcInfo.ColorType!=dstInfo.ColorType,CpuOptions.EnableSimd);continue;}
                for(int x=0;x<w;x++)
                {
                    int at=x*4;
                    if(convert){var c=ReadColorF(src,at,srcInfo);WriteColorF(dst,at,dstInfo,transform.Convert(new(c.Red,c.Green,c.Blue,c.Alpha)));continue;}
                    WriteColor(dst,at,dstInfo,ReadColor(src,at,srcInfo));
                }
            }
            return true;
        }
        finally{if(rented is not null)ArrayPool<byte>.Shared.Return(rented);}
    }
    internal static bool Erase(Span<byte> bytes,SKImageInfo info,int stride,SKColor color,SKRectI rect)
    {
        Validate(info,stride);if(bytes.IsEmpty)return false;
        int l=Math.Max(0,rect.Left),t=Math.Max(0,rect.Top),r=Math.Min(info.Width,rect.Right),b=Math.Min(info.Height,rect.Bottom);
        if(l>=r||t>=b)return false;
        if(info.ColorType==SKColorType.Alpha8){for(int y=t;y<b;y++)bytes.Slice(y*stride+l,r-l).Fill(color.Alpha);return true;}
        Span<byte> pixel=stackalloc byte[4];WriteTaggedColor(pixel,0,info,color);uint packed=MemoryMarshal.Read<uint>(pixel);
        for(int y=t;y<b;y++)MemoryMarshal.Cast<byte,uint>(bytes.Slice(y*stride+l*4,(r-l)*4)).Fill(packed);
        return true;
    }
    internal static void ValidateSampling(SKSamplingOptions sampling)
    {
        if(sampling.IsAniso||sampling.Mipmap!=SKMipmapMode.None)throw new NotSupportedException("Mipmaps and anisotropic scaling are not implemented.");
        if(sampling.UseCubic){if(!float.IsFinite(sampling.Cubic.B)||!float.IsFinite(sampling.Cubic.C))throw new ArgumentException("Cubic parameters must be finite.",nameof(sampling));}
        else if(!Enum.IsDefined(sampling.Filter))throw new ArgumentOutOfRangeException(nameof(sampling));
    }
    internal static bool Scale(ReadOnlySpan<byte> source,SKImageInfo srcInfo,int srcStride,Span<byte> destination,SKImageInfo dstInfo,int dstStride,SKSamplingOptions sampling)
    {
        ValidateSampling(sampling);Validate(srcInfo,srcStride);Validate(dstInfo,dstStride);
        if(source.IsEmpty||destination.IsEmpty||srcInfo.IsEmpty||dstInfo.IsEmpty)return false;
        if(srcInfo.Size==dstInfo.Size)return Read(source,srcInfo,srcStride,destination,dstInfo,dstStride,0,0);
        if(SKColorSpace.NeedsConversion(srcInfo.ColorSpace,dstInfo.ColorSpace))throw new NotSupportedException("Resampling across different color spaces is not yet implemented. Convert pixels at their original size first.");
        byte[]? rented=null;
        try
        {
            if(source.Overlaps(destination)){rented=ArrayPool<byte>.Shared.Rent(source.Length);source.CopyTo(rented);source=rented.AsSpan(0,source.Length);}
            if(srcInfo.BytesPerPixel==4&&dstInfo.BytesPerPixel==4&&!sampling.UseCubic&&srcInfo.AlphaType==dstInfo.AlphaType)
            {
                bool swap=srcInfo.ColorType!=dstInfo.ColorType;
                if(sampling.Filter==SKFilterMode.Nearest)
                {PixelTransferKernel.ScaleNearest32(source,srcInfo.Width,srcInfo.Height,srcStride,destination,dstInfo.Width,dstInfo.Height,dstStride,swap);return true;}
                if(srcInfo.AlphaType!=SKAlphaType.Unpremul)
                {PixelTransferKernel.ScaleLinear32(source,srcInfo.Width,srcInfo.Height,srcStride,destination,dstInfo.Width,dstInfo.Height,dstStride,srcInfo.IsOpaque,swap,CpuOptions.EnableSimd);return true;}
            }
            float scaleX=(float)srcInfo.Width/dstInfo.Width,scaleY=(float)srcInfo.Height/dstInfo.Height;
            bool straightInterpolation=srcInfo.AlphaType==SKAlphaType.Unpremul&&dstInfo.AlphaType==SKAlphaType.Unpremul;
            for(int y=0;y<dstInfo.Height;y++)
            {
                float py=(y+.5f)*scaleY-.5f;int iy=(int)MathF.Floor(py);float fy=py-iy;
                var row=destination.Slice(y*dstStride,dstInfo.Width*dstInfo.BytesPerPixel);
                for(int x=0;x<dstInfo.Width;x++)
                {
                    float px=(x+.5f)*scaleX-.5f;int ix=(int)MathF.Floor(px);float fx=px-ix;
                    if(!sampling.UseCubic&&sampling.Filter==SKFilterMode.Nearest)
                    {
                        int sx=Math.Clamp((int)MathF.Floor(px+.5f),0,srcInfo.Width-1),sy=Math.Clamp((int)MathF.Floor(py+.5f),0,srcInfo.Height-1),at=sy*srcStride+sx*srcInfo.BytesPerPixel;
                        if(srcInfo.AlphaType==dstInfo.AlphaType&&srcInfo.ColorType==dstInfo.ColorType)source.Slice(at,srcInfo.BytesPerPixel).CopyTo(row.Slice(x*dstInfo.BytesPerPixel,dstInfo.BytesPerPixel));
                        else WriteColor(row,x*dstInfo.BytesPerPixel,dstInfo,ReadColor(source,at,srcInfo));
                        continue;
                    }
                    float r=0,g=0,b=0,a=0;
                    int first=sampling.UseCubic?-1:0,last=sampling.UseCubic?2:1;
                    for(int j=first;j<=last;j++)for(int i=first;i<=last;i++)
                    {
                        float weight=sampling.UseCubic?Cubic(i-fx,sampling.Cubic.B,sampling.Cubic.C)*Cubic(j-fy,sampling.Cubic.B,sampling.Cubic.C):(i==0?1-fx:fx)*(j==0?1-fy:fy);
                        int sx=Math.Clamp(ix+i,0,srcInfo.Width-1),sy=Math.Clamp(iy+j,0,srcInfo.Height-1);
                        var c=ReadColorF(source,sy*srcStride+sx*srcInfo.BytesPerPixel,srcInfo);float alpha=c.Alpha;
                        float factor=straightInterpolation?weight:alpha*weight;
                        r+=c.Red*factor;g+=c.Green*factor;b+=c.Blue*factor;a+=alpha*weight;
                    }
                    a=Math.Clamp(a,0,1);float maximum=straightInterpolation?1:a;r=Math.Clamp(r,0,maximum);g=Math.Clamp(g,0,maximum);b=Math.Clamp(b,0,maximum);
                    if(!straightInterpolation&&dstInfo.AlphaType!=SKAlphaType.Premul){float inverse=a==0?0:1/a;r*=inverse;g*=inverse;b*=inverse;}
                    if(dstInfo.ColorType==SKColorType.Alpha8){row[x]=SKColorF.ToByte(a);continue;}
                    int red=dstInfo.ColorType==SKColorType.Rgba8888?0:2,offset=x*4;
                    row[offset+red]=SKColorF.ToByte(r);row[offset+1]=SKColorF.ToByte(g);row[offset+2-red]=SKColorF.ToByte(b);row[offset+3]=dstInfo.AlphaType==SKAlphaType.Opaque?(byte)255:SKColorF.ToByte(a);
                }
            }
            return true;
        }
        finally{if(rented is not null)ArrayPool<byte>.Shared.Return(rented);}
    }
    private static float Cubic(float value,float b,float c)
    {
        float x=MathF.Abs(value);
        return x<1?((12-9*b-6*c)*x*x*x+(-18+12*b+6*c)*x*x+6-2*b)/6:x<2?((-b-6*c)*x*x*x+(6*b+30*c)*x*x+(-12*b-48*c)*x+8*b+24*c)/6:0;
    }
}
