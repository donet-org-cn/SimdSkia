using SimdSkia;

namespace SkiaSharp;

public partial class SKShader : SKObject
{
    private IColorShader gradient;
    private ShaderResources? resources;
    private SKShader(IColorShader gradient)=>this.gradient=gradient;
    public static SKShader CreateEmpty()=>CreateColor(SKColor.Empty);
    public static SKShader CreateColor(SKColor color)=>new(new SolidColorShader(color.ToCore()));
    public static SKShader CreateLinearGradient(SKPoint start,SKPoint end,SKColor[] colors,SKShaderTileMode mode)=>CreateLinearGradient(start,end,colors,null,mode);
    public static SKShader CreateLinearGradient(SKPoint start,SKPoint end,SKColor[] colors,float[]? colorPos,SKShaderTileMode mode)
        =>CreateLinearGradient(start,end,colors,colorPos,mode,SKMatrix.Identity);
    public static SKShader CreateLinearGradient(SKPoint start,SKPoint end,SKColor[] colors,float[]? colorPos,SKShaderTileMode mode,SKMatrix localMatrix)
        =>CreateGradient(GradientKind.Linear,start,end,0,colors,colorPos,mode,0,360,localMatrix);
    public static SKShader CreateRadialGradient(SKPoint center,float radius,SKColor[] colors,SKShaderTileMode mode)=>CreateRadialGradient(center,radius,colors,null,mode);
    public static SKShader CreateRadialGradient(SKPoint center,float radius,SKColor[] colors,float[]? colorPos,SKShaderTileMode mode)=>CreateRadialGradient(center,radius,colors,colorPos,mode,SKMatrix.Identity);
    public static SKShader CreateRadialGradient(SKPoint center,float radius,SKColor[] colors,float[]? colorPos,SKShaderTileMode mode,SKMatrix localMatrix)
        =>CreateGradient(GradientKind.Radial,center,default,radius,colors,colorPos,mode,0,360,localMatrix);
    public static SKShader CreateSweepGradient(SKPoint center,SKColor[] colors)=>CreateSweepGradient(center,colors,null);
    public static SKShader CreateSweepGradient(SKPoint center,SKColor[] colors,float[]? colorPos)=>CreateSweepGradient(center,colors,colorPos,SKShaderTileMode.Clamp,0,360);
    public static SKShader CreateSweepGradient(SKPoint center,SKColor[] colors,float[]? colorPos,SKMatrix localMatrix)=>CreateSweepGradient(center,colors,colorPos,SKShaderTileMode.Clamp,0,360,localMatrix);
    public static SKShader CreateSweepGradient(SKPoint center,SKColor[] colors,SKShaderTileMode tileMode,float startAngle,float endAngle)=>CreateSweepGradient(center,colors,null,tileMode,startAngle,endAngle);
    public static SKShader CreateSweepGradient(SKPoint center,SKColor[] colors,float[]? colorPos,SKShaderTileMode tileMode,float startAngle,float endAngle)=>CreateSweepGradient(center,colors,colorPos,tileMode,startAngle,endAngle,SKMatrix.Identity);
    public static SKShader CreateSweepGradient(SKPoint center,SKColor[] colors,float[]? colorPos,SKShaderTileMode tileMode,float startAngle,float endAngle,SKMatrix localMatrix)
        =>CreateGradient(GradientKind.Sweep,center,default,0,colors,colorPos,tileMode,startAngle,endAngle,localMatrix);
    public static SKShader CreateLocalMatrix(SKShader shader,SKMatrix localMatrix)
    {ArgumentNullException.ThrowIfNull(shader);return shader.WithLocalMatrix(localMatrix);}
    public SKShader WithLocalMatrix(SKMatrix localMatrix)
    {
        ThrowIfDisposed();var matrix=localMatrix.ToCore();if(!matrix.TryInvert(out var inverse))return null!;
        return new SKShader(matrix==Matrix3.Identity||gradient is SolidColorShader?gradient:gradient is IContextualColorShader contextual?new ContextualTransformedShader(contextual,matrix,inverse):gradient is IPremultipliedColorShader pm?new PremultipliedTransformedShader(pm,inverse):new TransformedColorShader(gradient,inverse)){resources=resources?.Retain()};
    }
    private static SKShader CreateGradient(GradientKind kind,SKPoint start,SKPoint end,float radius,SKColor[] colors,float[]? colorPos,SKShaderTileMode mode,float startAngle,float endAngle,SKMatrix localMatrix)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if(colorPos is not null&&colors.Length!=colorPos.Length)throw new ArgumentException("The number of colors must match the number of color positions.");
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        if(colors.Length==0||!float.IsFinite(start.X)||!float.IsFinite(start.Y)||!float.IsFinite(end.X)||!float.IsFinite(end.Y)||!float.IsFinite(radius)||radius<0||!float.IsFinite(startAngle)||!float.IsFinite(endAngle)||kind==GradientKind.Sweep&&startAngle>endAngle||!localMatrix.ToCore().TryInvert(out var inverse))return null!;
        IColorShader result;
        if(colors.Length==1)result=new SolidColorShader(colors[0].ToCore());
        // Keep the established two-stop clamp fast path and its cached horizontal rows.
        else if(kind==GradientKind.Linear&&mode==SKShaderTileMode.Clamp&&colors.Length==2&&(colorPos is null||colorPos[0]==0&&colorPos[1]==1))
            result=new LinearGradient(new(start.X,start.Y),new(end.X,end.Y),colors[0].ToCore(),colors[1].ToCore());
        else
        {
            var stops=new LinearColorStop[colors.Length];float previous=0;
            for(int i=0;i<stops.Length;i++)
            {
                float offset=colorPos is null?(float)i/(stops.Length-1):colorPos[i];
                if(!float.IsFinite(offset))throw new NotSupportedException("Nonfinite gradient stops are not supported.");
                previous=Math.Clamp(offset,previous,1);stops[i]=new(previous,colors[i].ToCore());
            }
            result=new MultiStopGradient(kind,new(start.X,start.Y),new(end.X,end.Y),radius,stops,(int)mode,startAngle,endAngle);
        }
        return new(localMatrix.IsIdentity||result is SolidColorShader?result:new TransformedColorShader(result,inverse));
    }
    internal IColorShader ToCore(){ThrowIfDisposed();return gradient;}
}
