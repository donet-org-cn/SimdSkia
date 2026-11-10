using SimdSkia;
using System.Runtime.CompilerServices;
namespace SkiaSharp;
public partial class SKCanvas
{
    private sealed record ClipDescription(RectF? Hard,CoverageClip? Coverage,SKRectI Bounds,bool Rectangular);
    private ClipDescription? clipDescription;
    internal SKSurface? OwnerSurface {get;set;}
    public SKSurface? Surface {get{Validate();return OwnerSurface;}}
    private ClipDescription DescribeClip()
    {
        Validate();if(recording is not null)return RecordingClipDescription();if(clipDescription is {} old&&old.Hard==clip&&ReferenceEquals(old.Coverage,coverageClip))return old;
        int l=0,t=0,r=CanvasWidth,b=CanvasHeight;
        if(clip is {} c){l=(int)Math.Clamp(MathF.Floor(c.Left),0,r);t=(int)Math.Clamp(MathF.Floor(c.Top),0,b);r=(int)Math.Clamp(MathF.Ceiling(c.Right),l,r);b=(int)Math.Clamp(MathF.Ceiling(c.Bottom),t,b);}
        bool rectangular=l<r&&t<b;
        if(coverageClip is {} mask)
        {
            l=Math.Max(l,mask.Left);t=Math.Max(t,mask.Top);r=Math.Min(r,mask.Left+mask.Width);b=Math.Min(b,mask.Top+mask.Height);
            int left=r,top=b,right=l,bottom=t;
            for(int y=t;y<b;y++)
            {
                var row=mask.GetRow(y);for(int x=l;x<r;x++)if(row[x-mask.Left]!=0)
                {left=Math.Min(left,x);right=Math.Max(right,x+1);top=Math.Min(top,y);bottom=Math.Max(bottom,y+1);}
            }
            l=left;t=top;r=right;b=bottom;rectangular=l<r&&t<b;
            if(rectangular)for(int y=t;y<b&&rectangular;y++)rectangular=mask.GetRow(y).Slice(l-mask.Left,r-l).IndexOfAnyExcept((byte)255)<0;
        }
        var bounds=l<r&&t<b?new SKRectI(l,t,r,b):SKRectI.Empty;
        return clipDescription=new(clip,coverageClip,bounds,rectangular);
    }
    public SKRectI DeviceClipBounds=>DescribeClip().Bounds;
    public bool IsClipEmpty=>DeviceClipBounds.IsEmpty;
    public bool IsClipRect=>DescribeClip().Rectangular;
    public bool GetDeviceClipBounds(out SKRectI bounds){bounds=DeviceClipBounds;return !bounds.IsEmpty;}
    public SKRect LocalClipBounds=>GetLocalClipBounds(out var b)?b:SKRect.Empty;
    public bool GetLocalClipBounds(out SKRect bounds)
    {
        bounds=default;var b=DeviceClipBounds;if(b.IsEmpty||!matrix.TryInvert(out var inverse))return false;
        var a=inverse.Map(b.Left-1,b.Top-1);var c=inverse.Map(b.Right+1,b.Top-1);var d=inverse.Map(b.Left-1,b.Bottom+1);var e=inverse.Map(b.Right+1,b.Bottom+1);
        bounds=new(MathF.Min(MathF.Min(a.X,c.X),MathF.Min(d.X,e.X)),MathF.Min(MathF.Min(a.Y,c.Y),MathF.Min(d.Y,e.Y)),MathF.Max(MathF.Max(a.X,c.X),MathF.Max(d.X,e.X)),MathF.Max(MathF.Max(a.Y,c.Y),MathF.Max(d.Y,e.Y)));return bounds.ToCore().IsFinite;
    }
    public bool QuickReject(SKPath path){ArgumentNullException.ThrowIfNull(path);Validate();return path.IsEmpty||QuickReject(path.Bounds);}
    public bool QuickReject(SKRect rect)
    {
        Validate();if(!rect.ToCore().IsFinite)return true;if(rect.IsEmpty)return true;var b=DeviceClipBounds;if(b.IsEmpty)return true;
        var a=matrix.Map(rect.Left,rect.Top);var c=matrix.Map(rect.Right,rect.Top);var d=matrix.Map(rect.Left,rect.Bottom);var e=matrix.Map(rect.Right,rect.Bottom);
        float left=MathF.Min(MathF.Min(a.X,c.X),MathF.Min(d.X,e.X)),top=MathF.Min(MathF.Min(a.Y,c.Y),MathF.Min(d.Y,e.Y)),right=MathF.Max(MathF.Max(a.X,c.X),MathF.Max(d.X,e.X)),bottom=MathF.Max(MathF.Max(a.Y,c.Y),MathF.Max(d.Y,e.Y));
        if(left>=right||top>=bottom)return true;const int margin=1;
        return right<=b.Left-margin||left>=b.Right+margin||bottom<=b.Top-margin||top>=b.Bottom+margin;
    }
    public SKMatrix44 TotalMatrix44 {get{Validate();return new(TotalMatrix);}}
    private static SKMatrix PlaneMatrix(in SKMatrix44 m)
    {
        if(m.M02!=0||m.M12!=0||m.M20!=0||m.M21!=0||m.M23!=0||m.M32!=0||m.M22!=1)throw new NotSupportedException("Three-dimensional canvas matrix composition is not implemented; use an affine 2D plane.");
        return new(){ScaleX=m.M00,SkewX=m.M10,TransX=m.M30,SkewY=m.M01,ScaleY=m.M11,TransY=m.M31,Persp0=m.M03,Persp1=m.M13,Persp2=m.M33};
    }
    public void SetMatrix(in SKMatrix44 matrix){var m=PlaneMatrix(in matrix);SetMatrix(in m);}
    public void Concat(in SKMatrix44 m){var p=PlaneMatrix(in m);Concat(in p);}
    [Obsolete("Use SetMatrix(in SKMatrix) instead.",true)]
    public void SetMatrix(SKMatrix matrix)=>SetMatrix(in matrix);
}
