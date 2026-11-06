using System.Numerics;
using SimdSkia;
namespace SkiaSharp;
public partial class SKPaint
{
    private Vector4? floatingColor;
    private bool dither;
    private SKBlender? blender;
    private Func<Vector4,Vector4,Vector4>? retainedBlender;
    private PaintStages? cachedStages;
    private PaintStages? Stages()
    {
        var f=floatingColor;
        if(f is {} value&&retainedGradient is SolidColorShader solid)f=new(solid.Color.R/255f,solid.Color.G/255f,solid.Color.B/255f,solid.Color.A/255f*value.W);
        if(f is null&&retainedBlender is null&&!dither)return null;
        if(cachedStages is null||cachedStages.Color!=f||cachedStages.Blender!=retainedBlender||cachedStages.Dither!=dither)cachedStages=new(f,retainedBlender,dither);
        return cachedStages;
    }
    public bool IsDither {get{ThrowIfDisposed();return dither;}set{ThrowIfDisposed();dither=value;}}
    public SKColorF ColorF
    {
        get{ThrowIfDisposed();var f=floatingColor??new Vector4(color.Red/255f,color.Green/255f,color.Blue/255f,color.Alpha/255f);return new(f.X,f.Y,f.Z,f.W);}
        set=>SetColor(value,null!);
    }
    public void SetColor(SKColorF color,SKColorSpace colorspace)
    {
        ThrowIfDisposed();var f=new Vector4(color.Red,color.Green,color.Blue,color.Alpha);
        if(!float.IsFinite(f.X)||!float.IsFinite(f.Y)||!float.IsFinite(f.Z)||!float.IsFinite(f.W))throw new ArgumentException("Paint colors must be finite.",nameof(color));
        if(colorspace is not null&&!colorspace.IsSrgb)f=new SKColorSpace.Converter(colorspace,SKColorSpace.CreateSrgb()).Convert(f);
        f.W=Math.Clamp(f.W,0,1);floatingColor=f;this.color=new(ColorBlend.Byte(f.X),ColorBlend.Byte(f.Y),ColorBlend.Byte(f.Z),ColorBlend.Byte(f.W));filteredConstant=null;
    }
    public SKBlender? Blender
    {
        get{ThrowIfDisposed();return blender??(blend==SKBlendMode.SrcOver?null:SKBlender.CreateBlendMode(blend));}
        set{ThrowIfDisposed();var mode=value?.Mode;var captured=value is not null&&mode is null?value.Capture():null;blend=mode switch{CompositeMode.SourceOver=>SKBlendMode.SrcOver,CompositeMode.Clear=>SKBlendMode.Clear,CompositeMode.Destination=>SKBlendMode.Dst,null=>SKBlendMode.SrcOver,_=>(SKBlendMode)(int)mode.Value};blender=mode is null?value:null;retainedBlender=captured;}
    }
}
