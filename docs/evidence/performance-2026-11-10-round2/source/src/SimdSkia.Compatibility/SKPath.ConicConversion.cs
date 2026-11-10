namespace SkiaSharp;

public partial class SKPath
{
    public static SKPoint[] ConvertConicToQuads(SKPoint p0,SKPoint p1,SKPoint p2,float w,int pow2)
    {ConvertConicToQuads(p0,p1,p2,w,out var pts,pow2);return pts;}
    public static int ConvertConicToQuads(SKPoint p0,SKPoint p1,SKPoint p2,float w,out SKPoint[] pts,int pow2)
    {ValidateSubdivision(w,pow2);pts=new SKPoint[2*(1<<pow2)+1];return ConvertConicToQuads(p0,p1,p2,w,pts,pow2);}
    public static int ConvertConicToQuads(SKPoint p0,SKPoint p1,SKPoint p2,float w,SKPoint[] pts,int pow2)
    {
        ArgumentNullException.ThrowIfNull(pts);ValidateSubdivision(w,pow2);int count=1<<pow2;if(pts.Length<2*count+1)throw new ArgumentException("Insufficient point capacity.",nameof(pts));int at=1;pts[0]=p0;
        void Split(SKPoint a,SKPoint b,SKPoint c,float weight,int depth)
        {
            if(depth==0){pts[at++]=b;pts[at++]=c;return;}
            float factor=1/(1+weight),next=MathF.Sqrt((1+weight)*.5f);
            var left=new SKPoint((a.X+weight*b.X)*factor,(a.Y+weight*b.Y)*factor);
            var right=new SKPoint((weight*b.X+c.X)*factor,(weight*b.Y+c.Y)*factor);
            var mid=new SKPoint((left.X+right.X)*.5f,(left.Y+right.Y)*.5f);
            Split(a,left,mid,next,depth-1);Split(mid,right,c,next,depth-1);
        }
        Split(p0,p1,p2,w==0?1:w,pow2);return count;
    }
    private static void ValidateSubdivision(float w,int pow2)
    {if(!float.IsFinite(w)||w<0)throw new ArgumentOutOfRangeException(nameof(w));if((uint)pow2>10)throw new ArgumentOutOfRangeException(nameof(pow2),"Subdivision is limited to 1024 quads.");}
}
