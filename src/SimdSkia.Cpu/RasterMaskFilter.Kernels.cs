// Discrete Bessel kernel and analytic box-spline profile from Skia m119,
// Copyright Google Inc., BSD-3-Clause; see licenses/Skia-BSD.txt.
using System.Buffers;
namespace SimdSkia.Cpu;
public sealed partial class RasterMaskFilter
{
    private static CoverageClip SmallBlur(CoverageClip source,float sigma)
    {
        double variance=(double)sigma*sigma,t=variance*variance/4;
        double Bessel(int order)
        {double factor=order==0?1:variance/2,sum=factor;for(int k=1;factor>1e-6;k++){factor*=t/(k*(k+order));sum+=factor;}return sum;}
        Span<double> b=stackalloc double[6],g=stackalloc double[6];double denominator=Math.Exp(variance);b[0]=Bessel(0);b[1]=Bessel(1);g[0]=b[0]/denominator;g[1]=b[1]/denominator;int n=1;
        while(g[n]>.01&&n<5){b[n+1]=-(2*n/variance)*b[n]+b[n-1];g[n+1]=b[n+1]/denominator;n++;}
        double total=0;for(int i=n-1;i>=1;i--)total+=2*g[i];total+=g[0];for(int i=0;i<n;i++)g[i]/=total;total=0;for(int i=n-1;i>=1;i--)total+=2*g[i];g[0]=1-total;
        Span<int> weights=stackalloc int[6];for(int i=0;i<n;i++)weights[i]=(int)Math.Round(g[i]*65536,MidpointRounding.AwayFromZero);int radius=n-1,w=source.Width+2*radius,h=source.Height+2*radius;
        if((long)w*h>32*1024*1024)throw new NotSupportedException("Blur mask exceeds 32 MiB.");
        byte[] temp=ArrayPool<byte>.Shared.Rent(checked(source.Width*h));var result=new byte[checked(w*h)];
        try
        {
            for(int y=0;y<h;y++)for(int x=0;x<source.Width;x++)
            {
                int sum=128;for(int k=-radius;k<=radius;k++)sum+=((source.At(source.Left+x,source.Top+y-radius+k)<<8)*weights[Math.Abs(k)])>>16;
                temp[y*source.Width+x]=(byte)(sum>>8);
            }
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int sum=128;for(int k=-radius;k<=radius;k++){int at=x-radius+k;if((uint)at<(uint)source.Width)sum+=((temp[y*source.Width+at]<<8)*weights[Math.Abs(k)])>>16;}
                result[y*w+x]=(byte)(sum>>8);
            }
            return new(source.Left-radius,source.Top-radius,w,h,result);
        }
        finally{ArrayPool<byte>.Shared.Return(temp);}
    }
    internal bool TryFilterRectangle(RectF rectangle,Matrix3 transform,out CoverageClip? mask)
    {
        mask=null;if(table is not null||style is 2 or 3)return false;float s=Sigma(transform);int size=(int)MathF.Ceiling(6*s),pad=size/2;
        if(size==0||rectangle.Right-rectangle.Left<size+3||rectangle.Bottom-rectangle.Top<size+3)return false;
        int left=(int)MathF.Floor(rectangle.Left-pad+.5f),top=(int)MathF.Floor(rectangle.Top-pad+.5f),right=(int)MathF.Floor(rectangle.Right+pad+.5f),bottom=(int)MathF.Floor(rectangle.Bottom+pad+.5f),w=right-left,h=bottom-top;
        if(w<=0||h<=0)return false;if((long)w*h>32*1024*1024)throw new NotSupportedException("Analytic blur mask exceeds 32 MiB.");
        int sourceWidth=(int)MathF.Ceiling(rectangle.Right)-(int)MathF.Floor(rectangle.Left),sourceHeight=(int)MathF.Ceiling(rectangle.Bottom)-(int)MathF.Floor(rectangle.Top);
        int centerX=w-sourceWidth+2,centerY=h-sourceHeight+2,dx=sourceWidth-centerX-1,dy=sourceHeight-centerY-1;
        if(dx<0||dy<0)return false;
        var small=new RectF(rectangle.Left,rectangle.Top,rectangle.Right-dx,rectangle.Bottom-dy);
        int smallWidth=(int)MathF.Floor(small.Right+pad+.5f)-left,smallHeight=(int)MathF.Floor(small.Bottom+pad+.5f)-top;
        if(smallWidth<=centerX||smallHeight<=centerY)return false;
        var profile=new byte[size];profile[0]=255;float inverseRadius=1/(2*s);int center=size>>1;
        for(int x=1;x<size;x++)profile[x]=(byte)(255-(byte)(255*Integral((center-x-.5f)*inverseRadius)));
        byte Scan(int at,int length)
        {int sharp=length-size,inner=sharp-((size&~1)-1),dx=Math.Abs(((at<<1)+1)-length)-inner;return profile[Math.Clamp(dx>>1,0,size-1)];}
        var smallBytes=new byte[checked(smallWidth*smallHeight)];int sw=(int)MathF.Floor(small.Right-small.Left),sh=(int)MathF.Floor(small.Bottom-small.Top);
        for(int y=0;y<smallHeight;y++)for(int x=0;x<smallWidth;x++)smallBytes[y*smallWidth+x]=style==1&&x>=pad&&x<pad+sw&&y>=pad&&y<pad+sh?(byte)255:PixelBuffer.Multiply(Scan(x,smallWidth),Scan(y,smallHeight));
        int innerRight=w+centerX+1-smallWidth,innerBottom=h+centerY+1-smallHeight;var bytes=new byte[checked(w*h)];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            bool midX=x>=centerX&&x<innerRight,midY=y>=centerY&&y<innerBottom;
            int sx=x<centerX?x:x>=innerRight?smallWidth-(w-x):centerX,sy=y<centerY?y:y>=innerBottom?smallHeight-(h-y):centerY;
            bytes[y*w+x]=midX&&midY?(byte)255:smallBytes[sy*smallWidth+sx];
        }
        mask=new(left,top,w,h,bytes);return true;
    }
    private static float Integral(float x)
    {if(x>1.5f)return 0;if(x< -1.5f)return 1;float x2=x*x,x3=x2*x;return x>.5f?.5625f-(x3/6-3*x2*.25f+1.125f*x):x>-.5f?.5f-(.75f*x-x3/3):.4375f+(-x3/6-3*x2*.25f-1.125f*x);}
}
