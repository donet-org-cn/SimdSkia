namespace SimdSkia;

/// <summary>An owned PMA raster with a device-space origin. Filters never mutate their input.</summary>
public sealed class FilterFrame : IDisposable
{
    public PixelBuffer? Pixels {get;}
    public int Left {get;}
    public int Top {get;}
    public RectF TileBounds {get;init;}
    public RectF Bounds=>Pixels is {} p?new(Left,Top,Left+p.Width,Top+p.Height):default;
    public FilterFrame(PixelBuffer? pixels,int left=0,int top=0){Pixels=pixels;Left=left;Top=top;TileBounds=Bounds;}
    public FilterFrame Share()=>new(Pixels?.Share(),Left,Top){TileBounds=TileBounds};
    public void Dispose()=>Pixels?.Dispose();
    public static FilterFrame Rent(RectF bounds)
    {
        if(!bounds.IsFinite)throw new ArgumentException("Filter bounds must be finite.");
        if(bounds.IsEmpty)return new(null);
        double l=Math.Floor(bounds.Left),t=Math.Floor(bounds.Top),r=Math.Ceiling(bounds.Right),b=Math.Ceiling(bounds.Bottom);
        if(l<int.MinValue||t<int.MinValue||r>int.MaxValue||b>int.MaxValue||(r-l)*(b-t)>32*1024*1024)
            throw new NotSupportedException("One filter intermediate exceeds the 128 MiB raster budget.");
        return new(PixelBuffer.Rent(checked((int)(r-l)),checked((int)(b-t))),(int)l,(int)t);
    }
}

/// <summary>Immutable image-filter graph. The result owns its storage independently of the source.</summary>
public interface IImageFilter
{
    FilterFrame Apply(FilterFrame source,Matrix3 transform,RectF outputClip);
}
