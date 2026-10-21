using System.Buffers.Binary;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SimdSkia.Cpu;

/// <summary>Separable Gaussian filters in encoded premultiplied RGBA, with transparent extension.</summary>
public static class ManagedFilters
{
    private const int Scale=16384;
    private static readonly object gate=new();
    private static readonly Dictionary<float,int[]> kernels=[];
    private static int[] Kernel(float sigma)
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
    public static PixelBuffer Blur(PixelBuffer source,float sigmaX,float sigmaY,RectF? region=null)
    {
        ArgumentNullException.ThrowIfNull(source);source.ThrowIfDisposed();var xKernel=Kernel(sigmaX);var yKernel=Kernel(sigmaY);
        if(region is {} area&&!area.IsFinite)throw new ArgumentException("Filter bounds must be finite.",nameof(region));
        using var input=source.Alpha==AlphaFormat.Premultiplied?source.Share():PixelBuffer.Rent(source.Width,source.Height,source.Format,AlphaFormat.Premultiplied);
        using var horizontal=PixelBuffer.Rent(source.Width,source.Height,source.Format,AlphaFormat.Premultiplied);
        var output=PixelBuffer.Rent(source.Width,source.Height,source.Format,AlphaFormat.Premultiplied);
        try
        {
            // Preserve input alpha semantics, including physical A bytes for an
            // opaque source, without reading or writing any stride padding.
            int ri=source.Format==PixelFormat.Rgba8888?0:2;
            for(int y=0;source.Alpha!=AlphaFormat.Premultiplied&&y<source.Height;y++)
            {
                var src=source.GetRowSpan(y);var dst=input.GetRowSpan(y);
                for(int x=0;x<source.Width;x++)
                {var color=source.ReadPixel(x,y);byte a=source.Alpha==AlphaFormat.Opaque?(byte)255:color.A;dst[x*4+ri]=PixelBuffer.Multiply(color.R,a);dst[x*4+1]=PixelBuffer.Multiply(color.G,a);dst[x*4+2-ri]=PixelBuffer.Multiply(color.B,a);dst[x*4+3]=a;}
            }
            var bounds=region??new(0,0,source.Width,source.Height);
            int left=(int)Math.Clamp(MathF.Floor(bounds.Left),0,source.Width),right=(int)Math.Clamp(MathF.Ceiling(bounds.Right),0,source.Width);
            int top=(int)Math.Clamp(MathF.Floor(bounds.Top),0,source.Height),bottom=(int)Math.Clamp(MathF.Ceiling(bounds.Bottom),0,source.Height);
            int ry=yKernel.Length/2,rx=xKernel.Length/2;
            bool vector=CpuOptions.EnableSimd&&Avx2.IsSupported;
            for(int y=Math.Max(0,top-ry);y<Math.Min(source.Height,bottom+ry);y++)
            {
                var row=input.GetRowSpan(y);var target=horizontal.GetRowSpan(y);
                for(int x=left;x<right;)
                {
                    if(vector&&x>=rx&&x+8<=Math.Min(right,source.Width-rx))
                    {
                        var a=Vector256<int>.Zero;var b=a;var c=a;var d=a;
                        for(int k=-rx;k<=rx;k++)
                        {var p=row.Slice((x+k)*4,32);var w=Vector256.Create(xKernel[k+rx]);a=Avx2.Add(a,Avx2.MultiplyLow(Load8(p),w));b=Avx2.Add(b,Avx2.MultiplyLow(Load8(p[8..]),w));c=Avx2.Add(c,Avx2.MultiplyLow(Load8(p[16..]),w));d=Avx2.Add(d,Avx2.MultiplyLow(Load8(p[24..]),w));}
                        Store8(target[(x*4)..],a);Store8(target[(x*4+8)..],b);Store8(target[(x*4+16)..],c);Store8(target[(x*4+24)..],d);x+=8;
                    }
                    else if(vector&&x>=rx&&x+2<=Math.Min(right,source.Width-rx))
                    {
                        var sum=Vector256<int>.Zero;
                        for(int k=-rx;k<=rx;k++)sum=Avx2.Add(sum,Avx2.MultiplyLow(Load8(row[((x+k)*4)..]),Vector256.Create(xKernel[k+rx])));
                        Store8(target[(x*4)..],sum);x+=2;
                    }
                    else{for(int c=0;c<4;c++){int sum=0;for(int k=-rx;k<=rx;k++)if(x+k>=0&&x+k<source.Width)sum+=row[(x+k)*4+c]*xKernel[k+rx];target[x*4+c]=(byte)((sum+Scale/2)/Scale);}x++;}
                }
            }
            var horizontalData=horizontal.GetPixelSpan();int horizontalStride=horizontal.RowBytes;
            for(int y=top;y<bottom;y++)
            {
                var target=output.GetRowSpan(y);
                int first=Math.Max(-ry,-y),last=Math.Min(ry,source.Height-1-y);
                for(int x=left;x<right;)
                {
                    if(vector&&x+8<=right)
                    {
                        var a=Vector256<int>.Zero;var b=a;var c=a;var d=a;
                        for(int k=first;k<=last;k++)
                        {var p=horizontalData.Slice((y+k)*horizontalStride+x*4,32);var w=Vector256.Create(yKernel[k+ry]);a=Avx2.Add(a,Avx2.MultiplyLow(Load8(p),w));b=Avx2.Add(b,Avx2.MultiplyLow(Load8(p[8..]),w));c=Avx2.Add(c,Avx2.MultiplyLow(Load8(p[16..]),w));d=Avx2.Add(d,Avx2.MultiplyLow(Load8(p[24..]),w));}
                        Store8(target[(x*4)..],a);Store8(target[(x*4+8)..],b);Store8(target[(x*4+16)..],c);Store8(target[(x*4+24)..],d);x+=8;
                    }
                    else if(vector&&x+2<=right)
                    {
                        var sum=Vector256<int>.Zero;
                        for(int k=first;k<=last;k++)sum=Avx2.Add(sum,Avx2.MultiplyLow(Load8(horizontalData[((y+k)*horizontalStride+x*4)..]),Vector256.Create(yKernel[k+ry])));
                        Store8(target[(x*4)..],sum);x+=2;
                    }
                    else{for(int c=0;c<4;c++){int sum=0;for(int k=first;k<=last;k++)sum+=horizontalData[(y+k)*horizontalStride+x*4+c]*yKernel[k+ry];target[x*4+c]=(byte)((sum+Scale/2)/Scale);}x++;}
                }
            }
            return output;
        }
        catch{output.Dispose();throw;}
    }
    private static Vector256<int> Load8(ReadOnlySpan<byte> row)=>Avx2.ConvertToVector256Int32(Vector128.CreateScalar(BinaryPrimitives.ReadUInt64LittleEndian(row)).AsByte());
    private static void Store8(Span<byte> row,Vector256<int> sum)
    {
        var rounded=Avx2.ShiftRightLogical(Avx2.Add(sum,Vector256.Create(Scale/2)),14).AsUInt32();
        var shorts=Vector128.Narrow(rounded.GetLower(),rounded.GetUpper());
        BinaryPrimitives.WriteUInt64LittleEndian(row,Vector128.Narrow(shorts,Vector128<ushort>.Zero).AsUInt64().GetElement(0));
    }

    /// <summary>Colorize a blurred alpha plane, offset it with PMA bilinear filtering, then draw the original.</summary>
    public static PixelBuffer DropShadow(PixelBuffer source,float sigmaX,float sigmaY,float offsetX,float offsetY,Rgba32 color,RectF? region=null)
    {
        if(!float.IsFinite(offsetX)||!float.IsFinite(offsetY))throw new ArgumentException("Shadow offsets must be finite.");
        using var blurred=Blur(source,sigmaX,sigmaY,region);int ri=source.Format==PixelFormat.Rgba8888?0:2;
        for(int y=0;y<blurred.Height;y++){var row=blurred.GetRowSpan(y);for(int x=0;x<blurred.Width;x++){byte a=PixelBuffer.Multiply(row[x*4+3],color.A);row[x*4+ri]=PixelBuffer.Multiply(color.R,a);row[x*4+1]=PixelBuffer.Multiply(color.G,a);row[x*4+2-ri]=PixelBuffer.Multiply(color.B,a);row[x*4+3]=a;}}
        var result=PixelBuffer.Rent(source.Width,source.Height,source.Format,AlphaFormat.Premultiplied);
        try
        {
            using var canvas=new RasterCanvas(result);
            canvas.Execute(new(CommandKind.DrawImage,new(0,0,source.Width,source.Height),new(1,0,offsetX,0,1,offsetY,0,0,1),new(new(255,255,255,255),CompositeMode.SourceOver))
            {Image=blurred,ImageSource=new(0,0,source.Width,source.Height),Sampling=ImageSampling.Bilinear});
            canvas.Execute(new(CommandKind.DrawImage,new(0,0,source.Width,source.Height),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver)){Image=source});
            return result;
        }
        catch{result.Dispose();throw;}
    }
}
