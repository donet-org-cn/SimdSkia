#pragma warning disable CS0618 // Baseline legacy image-sampling forwarding.
using SimdSkia;
using SimdSkia.Cpu;

namespace SkiaSharp;

public partial class SKPaint : SKObject
{
    private SKColor color=new(0xff000000);
    private bool antialias;
    private SKPaintStyle style;
    private SKBlendMode blend=SKBlendMode.SrcOver;
    private float strokeWidth,strokeMiter=4;
    private SKStrokeCap strokeCap;
    private SKStrokeJoin strokeJoin;
    private SKFilterQuality filterQuality;
    [Obsolete("Use SKSamplingOptions instead.")]
    public SKFilterQuality FilterQuality {get{ThrowIfDisposed();return filterQuality;}set{ThrowIfDisposed();if(!Enum.IsDefined(value))throw new ArgumentOutOfRangeException(nameof(value));filterQuality=value;}}
    internal SKSamplingOptions LegacySampling {get{ThrowIfDisposed();return filterQuality.ToSamplingOptions();}}
    internal IColorShader? RetainedShader {get{ThrowIfDisposed();return retainedGradient;}}
    private IColorShader? meshColorShader;
    private SKColor meshColor;
    internal IColorShader MeshColorShader
    {get{ThrowIfDisposed();var opaque=color.WithAlpha(255);if(meshColorShader is null||meshColor!=opaque){meshColor=opaque;meshColorShader=new SolidColorShader(opaque.ToCore());}return meshColorShader;}}
    private SKShader? shader;
    private IColorShader? retainedGradient;
    private ShaderResources? shaderResources;
    internal ShaderResources? RetainShaderResources(){ThrowIfDisposed();return shaderResources?.Retain();}
    private SKColorFilter? colorFilter;
    private IColorFilter? retainedColorFilter;
    private FilteredConstant? filteredConstant;
    private SKImageFilter? imageFilter;
    private RasterImageFilter? retainedImageFilter;
    private ShaderResources? imageFilterResources;
    private SKMaskFilter? maskFilter;
    private RasterMaskFilter? retainedMaskFilter;
    public SKMaskFilter? MaskFilter {get{ThrowIfDisposed();return maskFilter;}set{ThrowIfDisposed();var next=value?.ToCore();maskFilter=value;retainedMaskFilter=next;}}
    public SKImageFilter? ImageFilter {get{ThrowIfDisposed();return imageFilter;}set{ThrowIfDisposed();var next=value?.ToCore();var owner=value?.RetainResources();imageFilterResources?.Dispose();imageFilterResources=owner;imageFilter=value;retainedImageFilter=next;}}
    internal ShaderResources? RetainImageFilterResources(){ThrowIfDisposed();return imageFilterResources?.Retain();}
    public SKColorFilter? ColorFilter {get{ThrowIfDisposed();return colorFilter;}set{ThrowIfDisposed();var next=value?.ToCore();colorFilter=value;retainedColorFilter=next;filteredConstant=null;}}
    public SKPaint() { }
    public SKColor Color { get { ThrowIfDisposed(); return color; } set { ThrowIfDisposed(); color=value;floatingColor=null; } }
    public bool IsAntialias { get { ThrowIfDisposed(); return antialias; } set { ThrowIfDisposed(); antialias=value;if(legacyFont is not null)legacyFont.Edging=value?(lcdRenderText?SKFontEdging.SubpixelAntialias:SKFontEdging.Antialias):SKFontEdging.Alias; } }
    public SKPaintStyle Style { get { ThrowIfDisposed(); return style; } set { ThrowIfDisposed(); if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); style=value; } }
    public SKBlendMode BlendMode { get { ThrowIfDisposed(); return blend; } set { ThrowIfDisposed(); if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); blend=value;blender=null;retainedBlender=null; } }
    public float StrokeWidth {get{ThrowIfDisposed();return strokeWidth;}set{ThrowIfDisposed();if(!float.IsFinite(value)||value<0)throw new ArgumentOutOfRangeException(nameof(value));strokeWidth=value;}}
    public float StrokeMiter {get{ThrowIfDisposed();return strokeMiter;}set{ThrowIfDisposed();if(!float.IsFinite(value)||value<0)throw new ArgumentOutOfRangeException(nameof(value));strokeMiter=value;}}
    public SKStrokeCap StrokeCap {get{ThrowIfDisposed();return strokeCap;}set{ThrowIfDisposed();if(!Enum.IsDefined(value))throw new ArgumentOutOfRangeException(nameof(value));strokeCap=value;}}
    public SKStrokeJoin StrokeJoin {get{ThrowIfDisposed();return strokeJoin;}set{ThrowIfDisposed();if(!Enum.IsDefined(value))throw new ArgumentOutOfRangeException(nameof(value));strokeJoin=value;}}
    public SKShader? Shader {get{ThrowIfDisposed();return shader;}set{ThrowIfDisposed();var next=value?.ToCore();var owner=value?.RetainResources();shaderResources?.Dispose();shaderResources=owner;retainedGradient=next;shader=value;}}
    public bool IsStroke {get=>Style!=SKPaintStyle.Fill;set=>Style=value?SKPaintStyle.Stroke:SKPaintStyle.Fill;}
    public SKPaint Clone()
    {
        ThrowIfDisposed();return new(){lcdRenderText=lcdRenderText,floatingColor=floatingColor,dither=dither,blender=blender,retainedBlender=retainedBlender,filterQuality=filterQuality,maskFilter=maskFilter,retainedMaskFilter=retainedMaskFilter,pathEffect=pathEffect,retainedPathEffect=retainedPathEffect,color=color,antialias=antialias,style=style,blend=blend,strokeWidth=strokeWidth,strokeMiter=strokeMiter,strokeCap=strokeCap,strokeJoin=strokeJoin,shader=shader,retainedGradient=retainedGradient,shaderResources=shaderResources?.Retain(),colorFilter=colorFilter,retainedColorFilter=retainedColorFilter,imageFilter=imageFilter,retainedImageFilter=retainedImageFilter,imageFilterResources=imageFilterResources?.Retain(),legacyFont=legacyFont?.CloneManaged(),textAlign=textAlign,textEncoding=textEncoding};
    }
    public void Reset()
    {
        ThrowIfDisposed();
        filterQuality=default;maskFilter=null;retainedMaskFilter=null;pathEffect=null;retainedPathEffect=null;ClearEffectCache();
        lcdRenderText=false;floatingColor=null;dither=false;blender=null;retainedBlender=null;cachedStages=null;
        imageFilterResources?.Dispose();imageFilterResources=null;imageFilter=null;retainedImageFilter=null;
        ThrowIfDisposed();color=new(0xff000000);antialias=false;style=SKPaintStyle.Fill;blend=SKBlendMode.SrcOver;strokeWidth=0;strokeMiter=4;strokeCap=SKStrokeCap.Butt;strokeJoin=SKStrokeJoin.Miter;colorFilter=null;retainedColorFilter=null;filteredConstant=null;shaderResources?.Dispose();shaderResources=null;shader=null;retainedGradient=null;legacyFont?.Dispose();legacyFont=null;textAlign=default;textEncoding=default;ClearStrokeCache();
    }
    internal Paint ToCore(bool ignoreStyle=false)
    {
        ThrowIfDisposed();
        if(!ignoreStyle&&style==SKPaintStyle.StrokeAndFill)throw new NotSupportedException("StrokeAndFill union is not implemented.");
        CompositeMode mode=GetComposite(blend);
        Paint result=retainedGradient is SolidColorShader solid?new(solid.Color with {A=PixelBuffer.Multiply(solid.Color.A,color.Alpha)},mode,antialias):new(color.ToCore(),mode,antialias,retainedGradient);
        if(retainedColorFilter is {} cf&&result.Shader is null&&floatingColor is null)
        {
            var c=result.Color;
            if(filteredConstant is null||filteredConstant.Input!=c||!ReferenceEquals(filteredConstant.Filter,cf)){float a=c.A/255f;filteredConstant=new(c,cf,cf.Filter(new(c.R/255f*a,c.G/255f*a,c.B/255f*a,a)));}
            result=result with {FilteredConstant=filteredConstant};
        }
        return result with {ColorFilter=retainedColorFilter,ImageFilter=retainedImageFilter,MaskFilter=retainedMaskFilter,Stages=Stages()};
    }
    internal static CompositeMode GetComposite(SKBlendMode blend)=>blend switch
    {SKBlendMode.SrcOver=>CompositeMode.SourceOver,SKBlendMode.Src=>CompositeMode.Source,SKBlendMode.Clear=>CompositeMode.Clear,SKBlendMode.Dst=>CompositeMode.Destination,_=>Enum.IsDefined(blend)?(CompositeMode)(int)blend:throw new ArgumentOutOfRangeException(nameof(blend))};
}
