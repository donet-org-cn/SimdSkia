namespace SimdSkia;

public readonly record struct Rgba32(byte R, byte G, byte B, byte A);
public enum PixelFormat { Rgba8888, Bgra8888 }
public enum AlphaFormat { Opaque, Premultiplied, Straight }
public enum CompositeMode { SourceOver, Source, Clear }
public enum ImageSampling { Nearest, Bilinear, Mitchell }

public readonly record struct RectF(float Left, float Top, float Right, float Bottom)
{
    public bool IsEmpty => Right <= Left || Bottom <= Top;
    public bool IsFinite => float.IsFinite(Left) && float.IsFinite(Top) && float.IsFinite(Right) && float.IsFinite(Bottom);
    public bool Contains(float x, float y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

/// <summary>Row-major homogeneous matrix acting on column vectors.</summary>
public readonly record struct Matrix3(
    float M00, float M01, float M02,
    float M10, float M11, float M12,
    float M20, float M21, float M22)
{
    public static Matrix3 Identity => new(1, 0, 0, 0, 1, 0, 0, 0, 1);
    public bool IsFinite => float.IsFinite(M00) && float.IsFinite(M01) && float.IsFinite(M02)
        && float.IsFinite(M10) && float.IsFinite(M11) && float.IsFinite(M12)
        && float.IsFinite(M20) && float.IsFinite(M21) && float.IsFinite(M22);
    public bool IsAffine => M20 == 0 && M21 == 0 && M22 == 1;

    public (float X, float Y) Map(float x, float y)
    {
        var w = M20 * x + M21 * y + M22;
        return ((M00 * x + M01 * y + M02) / w, (M10 * x + M11 * y + M12) / w);
    }

    public static Matrix3 operator *(Matrix3 a, Matrix3 b) => new(
        a.M00*b.M00 + a.M01*b.M10 + a.M02*b.M20, a.M00*b.M01 + a.M01*b.M11 + a.M02*b.M21, a.M00*b.M02 + a.M01*b.M12 + a.M02*b.M22,
        a.M10*b.M00 + a.M11*b.M10 + a.M12*b.M20, a.M10*b.M01 + a.M11*b.M11 + a.M12*b.M21, a.M10*b.M02 + a.M11*b.M12 + a.M12*b.M22,
        a.M20*b.M00 + a.M21*b.M10 + a.M22*b.M20, a.M20*b.M01 + a.M21*b.M11 + a.M22*b.M21, a.M20*b.M02 + a.M21*b.M12 + a.M22*b.M22);

    public bool TryInvert(out Matrix3 inverse)
    {
        double a=M00, b=M01, c=M02, d=M10, e=M11, f=M12, g=M20, h=M21, i=M22;
        double det = a*(e*i-f*h) - b*(d*i-f*g) + c*(d*h-e*g);
        if (!IsFinite || det == 0 || !double.IsFinite(det)) { inverse = default; return false; }
        inverse = new((float)((e*i-f*h)/det), (float)((c*h-b*i)/det), (float)((b*f-c*e)/det),
            (float)((f*g-d*i)/det), (float)((a*i-c*g)/det), (float)((c*d-a*f)/det),
            (float)((d*h-e*g)/det), (float)((b*g-a*h)/det), (float)((a*e-b*d)/det));
        return inverse.IsFinite;
    }
}

/// <summary>Immutable color function; advertised horizontal/opaque properties must remain true for its lifetime.</summary>
public interface IColorShader
{
    Rgba32 Sample(float x,float y);
    bool IsHorizontal=>false;
    bool IsOpaque=>false;
}
public readonly record struct Paint(Rgba32 Color, CompositeMode Composite,bool Antialias=false,IColorShader? Shader=null);
public enum CommandKind { Clear, FillRectangle,FillPath,DrawImage }
public readonly record struct DrawingCommand(CommandKind Kind, RectF Rectangle, Matrix3 Transform, Paint Paint)
{
    public PathGeometry? Path { get; init; }
    public RectF? Clip { get; init; }
    public PixelBuffer? Image { get; init; }
    public Matrix3? ShaderTransform {get;init;}
    /// <summary>Immutable text geometry may cache coverage independently of an integer device translation.</summary>
    public bool CacheTextCoverage {get;init;}
    public float CurveTolerance {get;init;}=0.125f;
    public CoverageClip? CoverageClip {get;init;}
    public RectF? ImageSource {get;init;}
    public ImageSampling Sampling {get;init;}
    public static DrawingCommand Clear(Rgba32 color) => new(CommandKind.Clear, default, Matrix3.Identity, new(color, CompositeMode.Source));
    public static DrawingCommand FillRectangle(RectF rect, Matrix3 transform, Paint paint) => new(CommandKind.FillRectangle, rect, transform, paint);
}

/// <summary>Backend-independent command boundary. Backends must reject unsupported commands.</summary>
public interface ICanvasBackend : IDisposable
{
    void Execute(in DrawingCommand command);
    void Flush();
}
