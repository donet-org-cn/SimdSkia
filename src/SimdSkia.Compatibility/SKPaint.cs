using SimdSkia;

namespace SkiaSharp;

public class SKPaint : SKObject
{
    private SKColor color=new(0xff000000);
    private bool antialias;
    private SKPaintStyle style;
    private SKBlendMode blend=SKBlendMode.SrcOver;
    private float strokeWidth,strokeMiter=4;
    private SKStrokeCap strokeCap;
    private SKStrokeJoin strokeJoin;
    private SKShader? shader;
    private LinearGradient? retainedGradient;
    public SKPaint() { }
    public SKColor Color { get { ThrowIfDisposed(); return color; } set { ThrowIfDisposed(); color=value; } }
    public bool IsAntialias { get { ThrowIfDisposed(); return antialias; } set { ThrowIfDisposed(); antialias=value; } }
    public SKPaintStyle Style { get { ThrowIfDisposed(); return style; } set { ThrowIfDisposed(); if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); style=value; } }
    public SKBlendMode BlendMode { get { ThrowIfDisposed(); return blend; } set { ThrowIfDisposed(); if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); blend=value; } }
    public float StrokeWidth {get{ThrowIfDisposed();return strokeWidth;}set{ThrowIfDisposed();if(!float.IsFinite(value)||value<0)throw new ArgumentOutOfRangeException(nameof(value));strokeWidth=value;}}
    public float StrokeMiter {get{ThrowIfDisposed();return strokeMiter;}set{ThrowIfDisposed();if(!float.IsFinite(value)||value<1)throw new ArgumentOutOfRangeException(nameof(value));strokeMiter=value;}}
    public SKStrokeCap StrokeCap {get{ThrowIfDisposed();return strokeCap;}set{ThrowIfDisposed();if(!Enum.IsDefined(value))throw new ArgumentOutOfRangeException(nameof(value));strokeCap=value;}}
    public SKStrokeJoin StrokeJoin {get{ThrowIfDisposed();return strokeJoin;}set{ThrowIfDisposed();if(!Enum.IsDefined(value))throw new ArgumentOutOfRangeException(nameof(value));strokeJoin=value;}}
    public SKShader? Shader {get{ThrowIfDisposed();return shader;}set{ThrowIfDisposed();retainedGradient=value?.ToCore();shader=value;}}
    internal Paint ToCore()
    {
        ThrowIfDisposed();
        if(style==SKPaintStyle.StrokeAndFill)throw new NotSupportedException("StrokeAndFill union is not implemented.");
        if(style==SKPaintStyle.Stroke&&(strokeCap!=SKStrokeCap.Butt||strokeJoin!=SKStrokeJoin.Miter))throw new NotSupportedException("Only butt caps and miter joins are currently supported.");
        CompositeMode mode=blend switch
        { SKBlendMode.SrcOver => CompositeMode.SourceOver, SKBlendMode.Src => CompositeMode.Source, SKBlendMode.Clear => CompositeMode.Clear, _ => throw new NotSupportedException($"Blend mode {blend} is not implemented.") };
        return new(color.ToCore(),mode,antialias,retainedGradient);
    }
}
