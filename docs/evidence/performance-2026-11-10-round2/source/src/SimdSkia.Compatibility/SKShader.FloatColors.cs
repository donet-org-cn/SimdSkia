using System.Numerics;
using SimdSkia;
namespace SkiaSharp;

public partial class SKShader
{
    internal static Vector4 ConvertColor(SKColorF color,SKColorSpace? colorspace)
    {
        var value=new Vector4(color.Red,color.Green,color.Blue,color.Alpha);
        if(!float.IsFinite(value.X)||!float.IsFinite(value.Y)||!float.IsFinite(value.Z)||!float.IsFinite(value.W))throw new ArgumentException("Color components must be finite.",nameof(color));
        if(colorspace is not null&&!colorspace.IsSrgb)value=new SKColorSpace.Converter(colorspace,SKColorSpace.CreateSrgb()).Convert(value);
        return Vector4.Clamp(value,Vector4.Zero,Vector4.One);
    }
    public static SKShader CreateColor(SKColorF color,SKColorSpace? colorspace)
    {var legacy=new FloatColorShader(ConvertColor(color,null));return new(colorspace is null||colorspace.IsSrgb?legacy:new DestinationColorShader(legacy,new FloatColorShader(ConvertColor(color,colorspace))));}
    private static SKShader FloatGradient(FloatGradientKind kind,SKPoint start,SKPoint end,float radius0,float radius1,SKColorF[] colors,SKColorSpace? colorspace,float[]? colorPos,SKShaderTileMode mode,float startAngle,float endAngle,SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(colors);if(colorPos is not null&&colorPos.Length!=colors.Length)throw new ArgumentException("Colors and positions must have equal length.",nameof(colorPos));
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        if(colors.Length==0||!float.IsFinite(start.X)||!float.IsFinite(start.Y)||!float.IsFinite(end.X)||!float.IsFinite(end.Y)||!float.IsFinite(radius0)||!float.IsFinite(radius1)||radius0<0||radius1<0||!float.IsFinite(startAngle)||!float.IsFinite(endAngle)||startAngle>endAngle||!localMatrix.ToCore().TryInvert(out var inverse))return null!;
        var values=new Vector4[colors.Length];var positions=new float[colors.Length];float previous=0;
        for(int i=0;i<colors.Length;i++){values[i]=ConvertColor(colors[i],null);float p=colorPos is null?colors.Length==1?0:(float)i/(colors.Length-1):colorPos[i];if(!float.IsFinite(p))throw new ArgumentException("Gradient positions must be finite.",nameof(colorPos));positions[i]=previous=Math.Clamp(p,previous,1);}
        if(values.Length==1)return CreateColor(colors[0],colorspace);
        var legacy=new FloatGradientShader(kind,new(start.X,start.Y),new(end.X,end.Y),radius0,radius1,values,positions,(int)mode,startAngle,endAngle,inverse);
        if(colorspace is null||colorspace.IsSrgb)return new(legacy);
        for(int i=0;i<colors.Length;i++)values[i]=ConvertColor(colors[i],colorspace);
        var managed=new FloatGradientShader(kind,new(start.X,start.Y),new(end.X,end.Y),radius0,radius1,values,positions,(int)mode,startAngle,endAngle,inverse);
        return new(new DestinationColorShader(legacy,managed));
    }
    public static SKShader CreateLinearGradient(SKPoint start,SKPoint end,SKColorF[] colors,SKColorSpace colorspace,float[] colorPos,SKShaderTileMode mode)=>FloatGradient(FloatGradientKind.Linear,start,end,0,0,colors,colorspace,colorPos,mode,0,360,SKMatrix.Identity);
    public static SKShader CreateLinearGradient(SKPoint start,SKPoint end,SKColorF[] colors,SKColorSpace colorspace,float[] colorPos,SKShaderTileMode mode,SKMatrix localMatrix)=>FloatGradient(FloatGradientKind.Linear,start,end,0,0,colors,colorspace,colorPos,mode,0,360,localMatrix);
    public static SKShader CreateLinearGradient(SKPoint start,SKPoint end,SKColorF[] colors,SKColorSpace colorspace,SKShaderTileMode mode)=>FloatGradient(FloatGradientKind.Linear,start,end,0,0,colors,colorspace,null,mode,0,360,SKMatrix.Identity);
    public static SKShader CreateRadialGradient(SKPoint center,float radius,SKColorF[] colors,SKColorSpace colorspace,float[] colorPos,SKShaderTileMode mode)=>FloatGradient(FloatGradientKind.Radial,center,default,radius,0,colors,colorspace,colorPos,mode,0,360,SKMatrix.Identity);
    public static SKShader CreateRadialGradient(SKPoint center,float radius,SKColorF[] colors,SKColorSpace colorspace,float[] colorPos,SKShaderTileMode mode,SKMatrix localMatrix)=>FloatGradient(FloatGradientKind.Radial,center,default,radius,0,colors,colorspace,colorPos,mode,0,360,localMatrix);
    public static SKShader CreateRadialGradient(SKPoint center,float radius,SKColorF[] colors,SKColorSpace colorspace,SKShaderTileMode mode)=>FloatGradient(FloatGradientKind.Radial,center,default,radius,0,colors,colorspace,null,mode,0,360,SKMatrix.Identity);
    public static SKShader CreateSweepGradient(SKPoint center,SKColorF[] colors,SKColorSpace colorspace)=>FloatGradient(FloatGradientKind.Sweep,center,default,0,0,colors,colorspace,null,SKShaderTileMode.Clamp,0,360,SKMatrix.Identity);
    public static SKShader CreateSweepGradient(SKPoint center,SKColorF[] colors,SKColorSpace colorspace,float[] colorPos)=>FloatGradient(FloatGradientKind.Sweep,center,default,0,0,colors,colorspace,colorPos,SKShaderTileMode.Clamp,0,360,SKMatrix.Identity);
    public static SKShader CreateSweepGradient(SKPoint center,SKColorF[] colors,SKColorSpace colorspace,float[] colorPos,SKMatrix localMatrix)=>FloatGradient(FloatGradientKind.Sweep,center,default,0,0,colors,colorspace,colorPos,SKShaderTileMode.Clamp,0,360,localMatrix);
    public static SKShader CreateSweepGradient(SKPoint center,SKColorF[] colors,SKColorSpace colorspace,float[] colorPos,SKShaderTileMode tileMode,float startAngle,float endAngle)=>FloatGradient(FloatGradientKind.Sweep,center,default,0,0,colors,colorspace,colorPos,tileMode,startAngle,endAngle,SKMatrix.Identity);
    public static SKShader CreateSweepGradient(SKPoint center,SKColorF[] colors,SKColorSpace colorspace,float[] colorPos,SKShaderTileMode tileMode,float startAngle,float endAngle,SKMatrix localMatrix)=>FloatGradient(FloatGradientKind.Sweep,center,default,0,0,colors,colorspace,colorPos,tileMode,startAngle,endAngle,localMatrix);
    public static SKShader CreateSweepGradient(SKPoint center,SKColorF[] colors,SKColorSpace colorspace,SKShaderTileMode tileMode,float startAngle,float endAngle)=>FloatGradient(FloatGradientKind.Sweep,center,default,0,0,colors,colorspace,null,tileMode,startAngle,endAngle,SKMatrix.Identity);
    public static SKShader CreateTwoPointConicalGradient(SKPoint start,float startRadius,SKPoint end,float endRadius,SKColorF[] colors,SKColorSpace colorspace,float[] colorPos,SKShaderTileMode mode)=>FloatGradient(FloatGradientKind.Conical,start,end,startRadius,endRadius,colors,colorspace,colorPos,mode,0,360,SKMatrix.Identity);
    public static SKShader CreateTwoPointConicalGradient(SKPoint start,float startRadius,SKPoint end,float endRadius,SKColorF[] colors,SKColorSpace colorspace,float[] colorPos,SKShaderTileMode mode,SKMatrix localMatrix)=>FloatGradient(FloatGradientKind.Conical,start,end,startRadius,endRadius,colors,colorspace,colorPos,mode,0,360,localMatrix);
    public static SKShader CreateTwoPointConicalGradient(SKPoint start,float startRadius,SKPoint end,float endRadius,SKColorF[] colors,SKColorSpace colorspace,SKShaderTileMode mode)=>FloatGradient(FloatGradientKind.Conical,start,end,startRadius,endRadius,colors,colorspace,null,mode,0,360,SKMatrix.Identity);
    public static SKShader CreateTwoPointConicalGradient(SKPoint start,float startRadius,SKPoint end,float endRadius,SKColor[] colors,float[] colorPos,SKShaderTileMode mode)=>FloatGradient(FloatGradientKind.Conical,start,end,startRadius,endRadius,colors.Select(c=>(SKColorF)c).ToArray(),null,colorPos,mode,0,360,SKMatrix.Identity);
    public static SKShader CreateTwoPointConicalGradient(SKPoint start,float startRadius,SKPoint end,float endRadius,SKColor[] colors,float[] colorPos,SKShaderTileMode mode,SKMatrix localMatrix)=>FloatGradient(FloatGradientKind.Conical,start,end,startRadius,endRadius,colors.Select(c=>(SKColorF)c).ToArray(),null,colorPos,mode,0,360,localMatrix);
    public static SKShader CreateTwoPointConicalGradient(SKPoint start,float startRadius,SKPoint end,float endRadius,SKColor[] colors,SKShaderTileMode mode)=>FloatGradient(FloatGradientKind.Conical,start,end,startRadius,endRadius,colors.Select(c=>(SKColorF)c).ToArray(),null,null,mode,0,360,SKMatrix.Identity);
}
