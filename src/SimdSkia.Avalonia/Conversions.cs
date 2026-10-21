using Avalonia;
using Avalonia.Media;
using AFormat=Avalonia.Platform.PixelFormat;
using AAlpha=Avalonia.Platform.AlphaFormat;

namespace SimdSkia.Avalonia;

internal static class ConvertTo
{
    internal static Matrix3 Matrix(Matrix m)
    {
        if(m.M13!=0||m.M23!=0||m.M33!=1)throw new NotSupportedException("Perspective transforms are not implemented.");
        var value=new Matrix3((float)m.M11,(float)m.M21,(float)m.M31,(float)m.M12,(float)m.M22,(float)m.M32,0,0,1);
        if(!value.IsFinite)throw new ArgumentException("Matrix components must be finite and fit float32.");return value;
    }
    internal static RectF Rect(Rect r)=>new((float)r.Left,(float)r.Top,(float)r.Right,(float)r.Bottom);
    internal static Rect Rect(RectF r)=>new(r.Left,r.Top,Math.Max(0,r.Right-r.Left),Math.Max(0,r.Bottom-r.Top));
    internal static PointF Point(Point p)=>new((float)p.X,(float)p.Y);
    internal static Rgba32 Color(Color c)=>new(c.R,c.G,c.B,c.A);
    internal static PixelFormat Format(AFormat format)=>format==global::Avalonia.Platform.PixelFormats.Rgba8888?PixelFormat.Rgba8888:format==global::Avalonia.Platform.PixelFormats.Bgra8888?PixelFormat.Bgra8888:throw new NotSupportedException($"Pixel format {format} is not supported.");
    internal static AlphaFormat Alpha(AAlpha format)=>format switch {AAlpha.Opaque=>AlphaFormat.Opaque,AAlpha.Premul=>AlphaFormat.Premultiplied,AAlpha.Unpremul=>AlphaFormat.Straight,_=>throw new NotSupportedException("Unknown alpha format.")};
    internal static AFormat Format(PixelFormat format)=>format==PixelFormat.Rgba8888?global::Avalonia.Platform.PixelFormats.Rgba8888:global::Avalonia.Platform.PixelFormats.Bgra8888;
    internal static AAlpha Alpha(AlphaFormat format)=>format switch {AlphaFormat.Opaque=>AAlpha.Opaque,AlphaFormat.Premultiplied=>AAlpha.Premul,_=>AAlpha.Unpremul};
}
