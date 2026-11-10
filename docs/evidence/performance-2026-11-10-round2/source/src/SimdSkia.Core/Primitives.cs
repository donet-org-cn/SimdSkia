namespace SimdSkia;

public readonly record struct Rgba32(byte R, byte G, byte B, byte A);
public enum PixelFormat { Rgba8888, Bgra8888, Alpha8 }
public enum AlphaFormat { Opaque, Premultiplied, Straight }
public enum CompositeMode
{
    SourceOver=0,Source=1,Clear=2,Destination=3,DestinationOver=4,SourceIn=5,DestinationIn=6,SourceOut=7,DestinationOut=8,SourceAtop=9,DestinationAtop=10,Xor=11,Plus=12,Modulate=13,Screen=14,Overlay=15,Darken=16,Lighten=17,ColorDodge=18,ColorBurn=19,HardLight=20,SoftLight=21,Difference=22,Exclusion=23,Multiply=24,Hue=25,Saturation=26,Color=27,Luminosity=28
}
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
    /// <summary>Untagged destinations retain legacy values; tagged sRGB targets transform color-managed sources.</summary>
    IColorShader ForDestination(bool taggedSrgb)=>this;
    bool IsHorizontal=>false;
    bool IsOpaque=>false;
    bool TryGetLinearGradient(out LinearGradientDescription? gradient){gradient=null;return false;}
}
/// <summary>A shader whose raster context includes device coordinates and consumes paint opacity.</summary>
public interface IContextualColorShader : IColorShader
{
    IColorShader Bind(Matrix3 drawingMatrix,byte opacity);
    IColorShader Bind(Matrix3 drawingMatrix,System.Numerics.Vector4 paint)=>Bind(drawingMatrix,ColorBlend.Byte(paint.W));
}
public enum GradientExtendMode { Clamp, Repeat, Mirror }
public readonly record struct LinearColorStop(float Offset,Rgba32 Color);
public sealed class LinearGradientDescription
{
    private readonly LinearColorStop[] stops;
    public PointF Start{get;}
    public PointF End{get;}
    public ReadOnlyMemory<LinearColorStop> Stops=>stops;
    public GradientExtendMode Extend{get;}
    public bool HasUnitStops{get;}
    /// <summary>Maps drawing-local points into the gradient's coordinate system.</summary>
    public Matrix3 SampleTransform{get;}
    public LinearGradientDescription(PointF start,PointF end,IEnumerable<LinearColorStop> stops,GradientExtendMode extend=GradientExtendMode.Clamp,Matrix3? sampleTransform=null)
    {
        Start=start;End=end;this.stops=stops.ToArray();Extend=extend;SampleTransform=sampleTransform??Matrix3.Identity;
        if(!float.IsFinite(start.X)||!float.IsFinite(start.Y)||!float.IsFinite(end.X)||!float.IsFinite(end.Y)||!SampleTransform.IsFinite||!SampleTransform.IsAffine)throw new ArgumentException("Gradient coordinates must be finite and affine.");
        if(!Enum.IsDefined(extend)||this.stops.Length==0||this.stops.Any(s=>!float.IsFinite(s.Offset)))throw new ArgumentException("Gradient stops and spread must be valid.");
        for(int i=1;i<this.stops.Length;i++)if(this.stops[i].Offset<this.stops[i-1].Offset)throw new ArgumentException("Gradient stops must be ordered.");
        HasUnitStops=this.stops[0].Offset>=0&&this.stops[^1].Offset<=1;
    }
}
public interface IColorFilter
{
    bool UsesLowPrecision=>false;
    System.Numerics.Vector4 Filter(System.Numerics.Vector4 premultiplied);
    System.Numerics.Vector4 FilterRaster(System.Numerics.Vector4 premultiplied)=>Filter(premultiplied);
}
public readonly record struct Paint(Rgba32 Color, CompositeMode Composite,bool Antialias=false,IColorShader? Shader=null)
{
    public IMaskFilter? MaskFilter {get;init;}
    public IImageFilter? ImageFilter {get;init;}
    public IColorFilter? ColorFilter {get;init;}
    private readonly PaintStages? stages;
    internal PaintStages? Stages {get=>stages;init=>stages=value;}
    public System.Numerics.Vector4? FloatingColor {get=>stages?.Color;init=>stages=PaintStages.Create(value,Blender,Dither);}
    public Func<System.Numerics.Vector4,System.Numerics.Vector4,System.Numerics.Vector4>? Blender {get=>stages?.Blender;init=>stages=PaintStages.Create(FloatingColor,value,Dither);}
    public bool Dither {get=>stages?.Dither??false;init=>stages=PaintStages.Create(FloatingColor,Blender,value);}
    internal FilteredConstant? FilteredConstant {get;init;}
}
internal sealed record PaintStages(System.Numerics.Vector4? Color,Func<System.Numerics.Vector4,System.Numerics.Vector4,System.Numerics.Vector4>? Blender,bool Dither)
{
    internal static PaintStages? Create(System.Numerics.Vector4? color,Func<System.Numerics.Vector4,System.Numerics.Vector4,System.Numerics.Vector4>? blender,bool dither)
        =>color is null&&blender is null&&!dither?null:new(color,blender,dither);
}
internal sealed class FilteredConstant(Rgba32 input,IColorFilter filter,System.Numerics.Vector4 value)
{
    internal Rgba32 Input {get;}=input;
    internal IColorFilter Filter {get;}=filter;
    internal System.Numerics.Vector4 Value {get;}=value;
    internal Rgba32 Premultiplied {get;}=new(ColorBlend.Byte(value.X),ColorBlend.Byte(value.Y),ColorBlend.Byte(value.Z),ColorBlend.Byte(value.W));
    internal Rgba32 Straight {get;}=value.W>0?new(ColorBlend.Byte(value.X/value.W),ColorBlend.Byte(value.Y/value.W),ColorBlend.Byte(value.Z/value.W),ColorBlend.Byte(value.W)):default;
}
public enum CommandKind { Clear, FillRectangle,FillPath,DrawImage,DrawGlyphRun }
public readonly record struct DrawingCommand(CommandKind Kind, RectF Rectangle, Matrix3 Transform, Paint Paint)
{
    public PathGeometry? Path { get; init; }
    public GlyphRunGeometry? GlyphRun {get;init;}
    public bool SnapGlyphBaseline {get;init;}
    /// <summary>sRGB text mask preblend, with luminance-based contrast, for the Avalonia raster compatibility profile.</summary>
    public bool TextMaskPreblend {get;init;}
    /// <summary>0=grayscale/unknown; 1/2=horizontal RGB/BGR; 3/4=vertical RGB/BGR.
    /// LCD coverage is only valid on an opaque destination.</summary>
    public byte TextPixelGeometry {get;init;}
    public RectF? Clip { get; init; }
    public PixelBuffer? Image { get; init; }
    // An actual backend resource, used only when the destination can consume it
    // directly. Other destinations receive Image pixels through the API layer.
    internal object? ImageResource {get;init;}
    /// <summary>Producer guarantees unchanged source bytes for its lifetime; permits cached GPU uploads without rescanning.</summary>
    public bool ImageIsImmutable {get;init;}
    public Matrix3? ShaderTransform {get;init;}
    public Matrix3? ImageFilterTransform {get;init;}
    /// <summary>SaveLayer opacity applies after image filtering; ordinary draw opacity is part of the input.</summary>
    public bool ImageFilterAlphaAfter {get;init;}
    /// <summary>Immutable text geometry may cache coverage independently of an integer device translation.</summary>
    public bool CacheTextCoverage {get;init;}
    public float CurveTolerance {get;init;}=0.125f;
    public CoverageClip? CoverageClip {get;init;}
    public RectF? ImageSource {get;init;}
    public ImageSampling Sampling {get;init;}
    public bool NearestLowerBoundary {get;init;}
    /// <summary>Lattice cells use aliased rectangle rounding, with lower-boundary nearest taps.</summary>
    public bool LatticeRectangleEdges {get;init;}
    /// <summary>Skia canvas image rectangles round aliased half edges up.</summary>
    public bool AliasedRectangleEdges {get;init;}
    /// <summary>Indexed mesh coverage uses the baseline's fixed 26.6 line edge walker.</summary>
    public bool FixedTriangleEdges {get;init;}
    public static DrawingCommand Clear(Rgba32 color) => new(CommandKind.Clear, default, Matrix3.Identity, new(color, CompositeMode.Source));
    public static DrawingCommand FillRectangle(RectF rect, Matrix3 transform, Paint paint) => new(CommandKind.FillRectangle, rect, transform, paint);
}

/// <summary>Backend-independent command boundary. Backends must reject unsupported commands.</summary>
public interface ICanvasBackend : IDisposable
{
    bool IsFaulted=>false;
    void Execute(in DrawingCommand command);
    void Flush();
}
