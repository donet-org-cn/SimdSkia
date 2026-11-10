using System.Buffers;
namespace SimdSkia.Cpu;
public static partial class ManagedFilters
{
    // Skia 7dbfc07 / m119 CPU image filters use the SVG three-box Gaussian
    // approximation (two-box tent at very large sigma), rather than point-
    // sampled Gaussian weights. Accumulate all boxes before rounding each axis.
    // Algorithm reference: SkBlurImageFilter.cpp, BSD-3-Clause (Skia-BSD.txt).
    internal static PixelBuffer CompatibleBlur(PixelBuffer source,float sigmaX,float sigmaY)
    {
        using var horizontal=PixelBuffer.Rent(source.Width,source.Height,source.Format);
        var result=PixelBuffer.Rent(source.Width,source.Height,source.Format);
        try{BoxAxis(source,horizontal,sigmaX,false);BoxAxis(horizontal,result,sigmaY,true);return result;}
        catch{result.Dispose();throw;}
    }
    internal static FilterFrame CompatibleBlur(FilterFrame source,float sigmaX,float sigmaY,RectF bounds)
    {
        if(source.Pixels is not {} p)return FilterFrame.Rent(bounds);
        using var horizontal=FilterFrame.Rent(new(bounds.Left,source.Top,bounds.Right,source.Top+p.Height));
        var result=FilterFrame.Rent(bounds);
        try
        {
            if(horizontal.Pixels is {} h&&result.Pixels is {} d)
            {BoxAxis(p,h,sigmaX,false,horizontal.Left-source.Left);BoxAxis(h,d,sigmaY,true,result.Top-horizontal.Top);}
            return result;
        }
        catch{result.Dispose();throw;}
    }
    private static void BoxAxis(PixelBuffer source,PixelBuffer destination,float sigma,bool vertical,int offset=0)
    {
        if(CpuOptions.EnableSimd&&System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated){BoxAxisVector(source,destination,sigma,vertical,offset);return;}
        int window=Math.Max(1,(int)Math.Floor(sigma*3*Math.Sqrt(2*Math.PI)/4+.5));
        int passes=3,last=(window&1)==0?window+1:window;
        if(window>=255){window=3*window/2;passes=2;last=window;}
        int extra=(passes-1)*(window-1)+last-1,border=extra/2,n=vertical?source.Height:source.Width,lines=vertical?source.Width:source.Height,outputCount=vertical?destination.Height:destination.Width;
        int capacity=checked((n+extra)*4);long divisor=1;for(int i=0;i<passes;i++)divisor*=i==passes-1?last:window;
        var a=ArrayPool<long>.Shared.Rent(capacity);var b=ArrayPool<long>.Shared.Rent(capacity);
        try
        {
            for(int line=0;line<lines;line++)
            {
                var first=a;var second=b;
                if(vertical)for(int i=0;i<n;i++){var row=source.GetRowSpan(i);int p=line*4;for(int c=0;c<4;c++)first[i*4+c]=row[p+c];}
                else{var row=source.GetRowSpan(line);for(int i=0;i<n*4;i++)first[i]=row[i];}
                int count=n;
                for(int pass=0;pass<passes;pass++)
                {
                    int w=pass==passes-1?last:window,len=count+w-1;long s0=0,s1=0,s2=0,s3=0;
                    for(int i=0;i<len;i++)
                    {
                        int p=i*4;if(i<count){s0+=first[p];s1+=first[p+1];s2+=first[p+2];s3+=first[p+3];}
                        if(i>=w){int q=(i-w)*4;s0-=first[q];s1-=first[q+1];s2-=first[q+2];s3-=first[q+3];}
                        second[p]=s0;second[p+1]=s1;second[p+2]=s2;second[p+3]=s3;
                    }
                    (first,second)=(second,first);count=len;
                }
                for(int i=0;i<outputCount;i++)
                {
                    int position=i+offset+border;if((uint)position>=(uint)count)continue;
                    var row=vertical?destination.GetRowSpan(i):destination.GetRowSpan(line);int q=position*4,p=vertical?line*4:i*4;
                    for(int c=0;c<4;c++)row[p+c]=(byte)((first[q+c]+divisor/2)/divisor);
                }
            }
        }
        finally{ArrayPool<long>.Shared.Return(a);ArrayPool<long>.Shared.Return(b);}
    }
}
