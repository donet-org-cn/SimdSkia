using SimdSkia;

namespace SkiaSharp;

public class SKShader : SKObject
{
    private readonly LinearGradient gradient;
    private SKShader(LinearGradient gradient)=>this.gradient=gradient;
    public static SKShader CreateLinearGradient(SKPoint start,SKPoint end,SKColor[] colors,SKShaderTileMode mode)=>CreateLinearGradient(start,end,colors,null,mode);
    public static SKShader CreateLinearGradient(SKPoint start,SKPoint end,SKColor[] colors,float[]? colorPos,SKShaderTileMode mode)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if(colors.Length!=2||mode!=SKShaderTileMode.Clamp||(colorPos is not null&&(colorPos.Length!=2||colorPos[0]!=0||colorPos[1]!=1)))throw new NotSupportedException("Only two-stop clamp linear gradients are supported.");
        if(!float.IsFinite(start.X)||!float.IsFinite(start.Y)||!float.IsFinite(end.X)||!float.IsFinite(end.Y))throw new ArgumentException("Gradient coordinates must be finite.");
        return new(new(new(start.X,start.Y),new(end.X,end.Y),colors[0].ToCore(),colors[1].ToCore()));
    }
    internal LinearGradient ToCore(){ThrowIfDisposed();return gradient;}
}
