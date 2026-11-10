using SimdSkia;
namespace SkiaSharp;
public partial class SKCanvas
{
    public void DrawRegion(SKRegion region,SKPaint paint)
    {Validate();ArgumentNullException.ThrowIfNull(region);ArgumentNullException.ThrowIfNull(paint);using var boundary=region.GetBoundaryPath();if(boundary is not null)DrawPath(boundary,paint);}
    public void ClipRegion(SKRegion region,SKClipOperation operation=SKClipOperation.Intersect)
    {
        Validate();ArgumentNullException.ThrowIfNull(region);if(!Enum.IsDefined(operation))throw new ArgumentOutOfRangeException(nameof(operation));var b=region.Bounds;
        if(forwarding is not null)foreach(var child in forwarding)child.ClipRegion(region,operation);
        if(recording is not null){using var path=region.GetBoundaryPath();recording.Add(new(PictureCommands.Kind.Clip,Matrix3.Identity,Path:path.ToCore(),ClipOperation:operation));return;}
        int l=Math.Clamp(b.Left,0,CanvasWidth),r=Math.Clamp(b.Right,l,CanvasWidth),t=Math.Clamp(b.Top,0,CanvasHeight),bottom=Math.Clamp(b.Bottom,t,CanvasHeight);var bytes=new byte[checked((r-l)*(bottom-t))];
        using(var iterator=region.CreateRectIterator())while(iterator.Next(out var rect)){int x0=Math.Clamp(rect.Left,l,r),x1=Math.Clamp(rect.Right,x0,r),y0=Math.Clamp(rect.Top,t,bottom),y1=Math.Clamp(rect.Bottom,y0,bottom);for(int y=y0;y<y1;y++)bytes.AsSpan((y-t)*(r-l)+x0-l,x1-x0).Fill(255);}
        var next=new CoverageClip(l,t,r-l,bottom-t,bytes);
        if(operation==SKClipOperation.Intersect){coverageClip=coverageClip is null?next:CoverageClip.Intersect(coverageClip,next);return;}
        // Region coordinates are device coordinates, independent of the canvas matrix.
        var extent=clip??new RectF(0,0,CanvasWidth,CanvasHeight);l=(int)Math.Clamp(extent.Left,0,CanvasWidth);t=(int)Math.Clamp(extent.Top,0,CanvasHeight);r=Math.Clamp((int)extent.Right,l,CanvasWidth);bottom=Math.Clamp((int)extent.Bottom,t,CanvasHeight);bytes=new byte[checked((r-l)*(bottom-t))];
        for(int y=t;y<bottom;y++)for(int x=l;x<r;x++)bytes[(y-t)*(r-l)+x-l]=PixelBuffer.Multiply(coverageClip?.At(x,y)??255,(byte)(255-next.At(x,y)));
        coverageClip=new(l,t,r-l,bottom-t,bytes);
    }
}
