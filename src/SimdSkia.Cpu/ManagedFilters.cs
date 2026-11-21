using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Cpu;

/// <summary>Separable Gaussian filters in encoded premultiplied RGBA, with transparent extension.</summary>
public static partial class ManagedFilters
{
    private const int Scale=16384;
    private static readonly object gate=new();
    private static readonly Dictionary<float,int[]> kernels=[];
    private static readonly ArrayPool<byte> shadowWork = ArrayPool<byte>.Create(DropShadowBlurCache.MaximumPlaneBytes, 1);
    internal static int[] Kernel(float sigma)
    {
        if(!float.IsFinite(sigma)||sigma<0||sigma>256)throw new ArgumentOutOfRangeException(nameof(sigma),"Gaussian sigma must be finite and in 0..256.");
        if(sigma==0)return [Scale];
        lock(gate)if(kernels.TryGetValue(sigma,out var cached))return cached;
        int radius=(int)Math.Ceiling(sigma*3);var values=new double[radius*2+1];double sum=0;
        for(int i=-radius;i<=radius;i++){double v=Math.Exp(-i*(double)i/(2*sigma*sigma));values[i+radius]=v;sum+=v;}
        var result=new int[values.Length];int total=0;
        for(int i=0;i<result.Length;i++){result[i]=(int)Math.Floor(values[i]/sum*Scale);total+=result[i];}
        // Distribute rounding residual symmetrically. Adding the entire error
        // to the center can produce a negative weight at large sigma.
        int remaining=Scale-total;if((remaining&1)!=0){result[radius]++;remaining--;}
        foreach(int i in Enumerable.Range(0,radius).OrderByDescending(i=>values[i]/sum*Scale-result[i]))
        {if(remaining==0)break;result[i]++;result[result.Length-1-i]++;remaining-=2;}
        lock(gate){if(kernels.Count>=64)kernels.Clear();kernels[sigma]=result;}
        return result;
    }
    public static PixelBuffer Blur(PixelBuffer source,float sigmaX,float sigmaY,RectF? region=null) =>
        BlurCore(source,sigmaX,sigmaY,region,alphaOnly:false);

    internal static PixelBuffer BlurShadowAlpha(PixelBuffer source,float sigmaX,float sigmaY,RectF region,
        int left,int top,int width,int height)
        => BlurCore(source,sigmaX,sigmaY,region,alphaOnly:true, new(left,top,width,height));

    private readonly record struct AlphaWindow(int Left,int Top,int Width,int Height);

    private static PixelBuffer BlurCore(PixelBuffer source,float sigmaX,float sigmaY,RectF? region,bool alphaOnly,AlphaWindow? window=null)
    {
        ArgumentNullException.ThrowIfNull(source);source.ThrowIfDisposed();var xKernel=Kernel(sigmaX);var yKernel=Kernel(sigmaY);
        if(region is {} area&&!area.IsFinite)throw new ArgumentException("Filter bounds must be finite.",nameof(region));
        int width=window?.Width??source.Width,height=window?.Height??source.Height;
        int originX=window?.Left??0,originY=window?.Top??0;
        int pixelBytes = alphaOnly ? 1 : source.BytesPerPixel;
        bool convert = !alphaOnly && pixelBytes == 4 && source.Alpha != AlphaFormat.Premultiplied;
        int batch = 8 / pixelBytes, wideBatch = batch * 4;
        using var input=convert?PixelBuffer.Rent(source.Width,source.Height,source.Format,AlphaFormat.Premultiplied):source.Share();
        var format=alphaOnly?PixelFormat.Alpha8:source.Format;
        using var horizontal=window is null?PixelBuffer.Rent(width,height,format,AlphaFormat.Premultiplied):null;
        var output=PixelBuffer.Rent(width,height,format,AlphaFormat.Premultiplied);
        byte[]? alphaRow=null,windowWork=null;
        try
        {
            // Keep short-lived A8 scratch separate from the reusable RGBA layers.
            // Every byte read by the vertical pass was initialized horizontally.
            int horizontalStride=checked(width*pixelBytes);
            int workSize=checked(horizontalStride*height);
            var horizontalData=horizontal is null?(windowWork=shadowWork.Rent(workSize)).AsSpan(0,workSize):horizontal.GetPixelSpan();
            if(alphaOnly&&(source.Format!=PixelFormat.Alpha8||source.Alpha==AlphaFormat.Opaque))
                alphaRow=ArrayPool<byte>.Shared.Rent(width);
            // Preserve input alpha semantics, including physical A bytes for an
            // opaque source, without reading or writing any stride padding.
            int ri=source.Format==PixelFormat.Rgba8888?0:2;
            for(int y=0;convert&&y<source.Height;y++)
            {
                var src=source.GetRowSpan(y);var dst=input.GetRowSpan(y);
                for(int x=0;x<source.Width;x++)
                {var color=source.ReadPixel(x,y);byte a=source.Alpha==AlphaFormat.Opaque?(byte)255:color.A;dst[x*4+ri]=PixelBuffer.Multiply(color.R,a);dst[x*4+1]=PixelBuffer.Multiply(color.G,a);dst[x*4+2-ri]=PixelBuffer.Multiply(color.B,a);dst[x*4+3]=a;}
            }
            var bounds=region??new(0,0,width,height);
            int left=(int)Math.Clamp(MathF.Floor(bounds.Left),0,width),right=(int)Math.Clamp(MathF.Ceiling(bounds.Right),0,width);
            int top=(int)Math.Clamp(MathF.Floor(bounds.Top),0,height),bottom=(int)Math.Clamp(MathF.Ceiling(bounds.Bottom),0,height);
            int ry=yKernel.Length/2,rx=xKernel.Length/2;
            bool vector=CpuOptions.EnableSimd&&Avx2.IsSupported;
            bool vector128=CpuOptions.EnableSimd&&!vector&&Vector128.IsHardwareAccelerated;
            int batch128=4/pixelBytes,wideBatch128=batch128*4;
            for(int y=Math.Max(0,top-ry);y<Math.Min(height,bottom+ry);y++)
            {
                var row=input.GetRowSpan(y+originY)[(originX*source.BytesPerPixel)..];
                if(alphaRow is not null)
                {
                    // Convolve the source alpha directly, reusing one scanline
                    // instead of materializing a third full-size work buffer.
                    var alpha=alphaRow.AsSpan(0,width);
                    if(source.Alpha==AlphaFormat.Opaque)alpha.Fill(255);
                    else for(int x=0;x<alpha.Length;x++)alpha[x]=row[x*4+3];
                    row=alpha;
                }
                var target=horizontalData.Slice(y*horizontalStride,horizontalStride);
                for(int x=left;x<right;)
                {
                    int start = x * pixelBytes;
                    if(vector&&x>=rx&&x+wideBatch<=Math.Min(right,width-rx))
                    {
                        var a=Vector256<int>.Zero;var b=a;var c=a;var d=a;
                        for(int k=-rx;k<=rx;k++)
                        {var p=row.Slice(start+k*pixelBytes,32);var w=Vector256.Create(xKernel[k+rx]);a=Avx2.Add(a,Avx2.MultiplyLow(Load8(p),w));b=Avx2.Add(b,Avx2.MultiplyLow(Load8(p[8..]),w));c=Avx2.Add(c,Avx2.MultiplyLow(Load8(p[16..]),w));d=Avx2.Add(d,Avx2.MultiplyLow(Load8(p[24..]),w));}
                        Store8(target[start..],a);Store8(target[(start+8)..],b);Store8(target[(start+16)..],c);Store8(target[(start+24)..],d);x+=wideBatch;
                    }
                    else if(vector&&x>=rx&&x+batch<=Math.Min(right,width-rx))
                    {
                        var sum=Vector256<int>.Zero;
                        for(int k=-rx;k<=rx;k++)sum=Avx2.Add(sum,Avx2.MultiplyLow(Load8(row[(start+k*pixelBytes)..]),Vector256.Create(xKernel[k+rx])));
                        Store8(target[start..],sum);x+=batch;
                    }
                    else if(vector128&&x>=rx&&x+wideBatch128<=Math.Min(right,width-rx))
                    {
                        Convolve16(row,start-rx*pixelBytes,pixelBytes,xKernel,target[start..]);
                        x+=wideBatch128;
                    }
                    else{for(int c=0;c<pixelBytes;c++){int sum=0;for(int k=-rx;k<=rx;k++)if(x+k>=0&&x+k<width)sum+=row[start+k*pixelBytes+c]*xKernel[k+rx];target[start+c]=(byte)((sum+Scale/2)/Scale);}x++;}
                }
            }
            for(int y=top;y<bottom;y++)
            {
                var target=output.GetRowSpan(y);
                int first=Math.Max(-ry,-y),last=Math.Min(ry,height-1-y);
                for(int x=left;x<right;)
                {
                    int start = x * pixelBytes;
                    if(vector&&x+wideBatch<=right)
                    {
                        var a=Vector256<int>.Zero;var b=a;var c=a;var d=a;
                        for(int k=first;k<=last;k++)
                        {var p=horizontalData.Slice((y+k)*horizontalStride+start,32);var w=Vector256.Create(yKernel[k+ry]);a=Avx2.Add(a,Avx2.MultiplyLow(Load8(p),w));b=Avx2.Add(b,Avx2.MultiplyLow(Load8(p[8..]),w));c=Avx2.Add(c,Avx2.MultiplyLow(Load8(p[16..]),w));d=Avx2.Add(d,Avx2.MultiplyLow(Load8(p[24..]),w));}
                        Store8(target[start..],a);Store8(target[(start+8)..],b);Store8(target[(start+16)..],c);Store8(target[(start+24)..],d);x+=wideBatch;
                    }
                    else if(vector&&x+batch<=right)
                    {
                        var sum=Vector256<int>.Zero;
                        for(int k=first;k<=last;k++)sum=Avx2.Add(sum,Avx2.MultiplyLow(Load8(horizontalData[((y+k)*horizontalStride+start)..]),Vector256.Create(yKernel[k+ry])));
                        Store8(target[start..],sum);x+=batch;
                    }
                    else if(vector128&&x+wideBatch128<=right)
                    {
                        Convolve16(horizontalData,(y+first)*horizontalStride+start,horizontalStride,
                            yKernel.AsSpan(first+ry,last-first+1),target[start..]);
                        x+=wideBatch128;
                    }
                    else{for(int c=0;c<pixelBytes;c++){int sum=0;for(int k=first;k<=last;k++)sum+=horizontalData[(y+k)*horizontalStride+start+c]*yKernel[k+ry];target[start+c]=(byte)((sum+Scale/2)/Scale);}x++;}
                }
            }
            return output;
        }
        catch{output.Dispose();throw;}
        finally
        {
            if(alphaRow is not null)ArrayPool<byte>.Shared.Return(alphaRow);
            if(windowWork is not null)shadowWork.Return(windowWork);
        }
    }
    private static Vector256<int> Load8(ReadOnlySpan<byte> row)=>Avx2.ConvertToVector256Int32(Vector128.CreateScalar(BinaryPrimitives.ReadUInt64LittleEndian(row)).AsByte());
    private static void Store8(Span<byte> row,Vector256<int> sum)
    {
        var rounded=Avx2.ShiftRightLogical(Avx2.Add(sum,Vector256.Create(Scale/2)),14).AsUInt32();
        var shorts=Vector128.Narrow(rounded.GetLower(),rounded.GetUpper());
        BinaryPrimitives.WriteUInt64LittleEndian(row,Vector128.Narrow(shorts,Vector128<ushort>.Zero).AsUInt64().GetElement(0));
    }

    private static void Convolve16(ReadOnlySpan<byte> source,int offset,int step,ReadOnlySpan<int> weights,Span<byte> output)
    {
        var a=Vector128<int>.Zero;var b=a;var c=a;var d=a;
        foreach(int weight in weights)
        {
            var values=Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(source),(nuint)offset);
            var lo=Vector128.WidenLower(values);var hi=Vector128.WidenUpper(values);
            var scale=Vector128.Create(weight);
            a+=Vector128.WidenLower(lo).AsInt32()*scale;
            b+=Vector128.WidenUpper(lo).AsInt32()*scale;
            c+=Vector128.WidenLower(hi).AsInt32()*scale;
            d+=Vector128.WidenUpper(hi).AsInt32()*scale;
            offset+=step;
        }
        var half=Vector128.Create(Scale/2);
        var first=Vector128.Narrow(Vector128.ShiftRightLogical((a+half).AsUInt32(),14),
            Vector128.ShiftRightLogical((b+half).AsUInt32(),14));
        var second=Vector128.Narrow(Vector128.ShiftRightLogical((c+half).AsUInt32(),14),
            Vector128.ShiftRightLogical((d+half).AsUInt32(),14));
        Vector128.Narrow(first,second).CopyTo(output);
    }

    /// <summary>Colorize a blurred alpha plane, offset it with PMA bilinear filtering, then draw the original.</summary>
    public static PixelBuffer DropShadow(PixelBuffer source,float sigmaX,float sigmaY,float offsetX,float offsetY,Rgba32 color,RectF? region=null)
        => DropShadow(source,sigmaX,sigmaY,offsetX,offsetY,color,region,null);

    internal static PixelBuffer DropShadow(PixelBuffer source,float sigmaX,float sigmaY,float offsetX,float offsetY,
        Rgba32 color,RectF? region,DropShadowBlurCache? cache)
    {
        if(!float.IsFinite(offsetX)||!float.IsFinite(offsetY))throw new ArgumentException("Shadow offsets must be finite.");
        int left=0,top=0;
        using var blurred=cache?.Blur(source,sigmaX,sigmaY,region,out left,out top)
            ??BlurCore(source,sigmaX,sigmaY,region,alphaOnly:true);
        using var shadow=PixelBuffer.Rent(source.Width,source.Height,source.Format,AlphaFormat.Premultiplied);
        int ri=source.Format==PixelFormat.Rgba8888?0:2;
        for(int y=0;y<blurred.Height;y++)
        {
            var coverage=blurred.GetRowSpan(y);
            var row=shadow.GetRowSpan(top+y)[(left*shadow.BytesPerPixel)..];
            if(shadow.Format==PixelFormat.Alpha8)
            {
                for(int x=0;x<coverage.Length;x++)row[x]=PixelBuffer.Multiply(coverage[x],color.A);
                continue;
            }
            for(int x=0;x<coverage.Length;x++)
            {
                byte a=PixelBuffer.Multiply(coverage[x],color.A);
                row[x*4+ri]=PixelBuffer.Multiply(color.R,a);
                row[x*4+1]=PixelBuffer.Multiply(color.G,a);
                row[x*4+2-ri]=PixelBuffer.Multiply(color.B,a);
                row[x*4+3]=a;
            }
        }
        var result=PixelBuffer.Rent(source.Width,source.Height,source.Format,AlphaFormat.Premultiplied);
        try
        {
            using var canvas=new RasterCanvas(result);
            canvas.Execute(new(CommandKind.DrawImage,new(0,0,source.Width,source.Height),new(1,0,offsetX,0,1,offsetY,0,0,1),new(new(255,255,255,255),CompositeMode.SourceOver))
            {Image=shadow,ImageSource=new(0,0,source.Width,source.Height),Sampling=ImageSampling.Bilinear});
            canvas.Execute(new(CommandKind.DrawImage,new(0,0,source.Width,source.Height),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver)){Image=source});
            return result;
        }
        catch{result.Dispose();throw;}
    }

}
