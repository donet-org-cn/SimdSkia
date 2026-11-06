using System.Numerics;
using SimdSkia;
namespace SkiaSharp;

public class SKBlender : SKObject
{
    private readonly CompositeMode? mode;
    private readonly Vector4 coefficients;
    private readonly bool enforcePMColor,permanent;
    private readonly Func<Vector4,Vector4,Vector4>? runtime;
    private SKBlender(Func<Vector4,Vector4,Vector4> runtime)=>this.runtime=runtime;
    internal static SKBlender Runtime(Func<Vector4,Vector4,Vector4> operation)=>new(operation);
    private SKBlender(CompositeMode mode){this.mode=mode;permanent=true;}
    private SKBlender(float k1,float k2,float k3,float k4,bool enforcePMColor){coefficients=new(k1,k2,k3,k4);this.enforcePMColor=enforcePMColor;}
    private static readonly SKBlender[] Modes=Enumerable.Range(0,29).Select(i=>new SKBlender(SKPaint.GetComposite((SKBlendMode)i))).ToArray();
    protected override bool IgnoreDispose=>permanent;
    internal CompositeMode? Mode {get{ThrowIfDisposed();return mode;}}
    public static SKBlender CreateBlendMode(SKBlendMode mode)=>Enum.IsDefined(mode)?Modes[(int)mode]:throw new ArgumentOutOfRangeException(nameof(mode));
    public static SKBlender CreateArithmetic(float k1,float k2,float k3,float k4,bool enforcePMColor)
    {
        if(!float.IsFinite(k1)||!float.IsFinite(k2)||!float.IsFinite(k3)||!float.IsFinite(k4))throw new ArgumentException("Arithmetic coefficients must be finite.");
        return new(k1,k2,k3,k4,enforcePMColor);
    }
    internal Func<Vector4,Vector4,Vector4> Capture()
    {
        ThrowIfDisposed();if(runtime is not null)return runtime;if(mode is {} blend)return(s,d)=>ColorBlend.ComposeRaster(s,d,blend);
        var c=coefficients;bool constrain=enforcePMColor;
        return(s,d)=>{var result=Vector4.Clamp(c.X*s*d+c.Y*s+c.Z*d+new Vector4(c.W),Vector4.Zero,Vector4.One);if(constrain){result.X=MathF.Min(result.X,result.W);result.Y=MathF.Min(result.Y,result.W);result.Z=MathF.Min(result.Z,result.W);}return result;};
    }
}
