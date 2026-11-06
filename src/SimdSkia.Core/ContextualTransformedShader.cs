using System.Numerics;
namespace SimdSkia;
public sealed class ContextualTransformedShader(IContextualColorShader source,Matrix3 local,Matrix3 inverse) : IContextualColorShader,IPremultipliedColorShader
{
    private readonly IColorShader initial=source.Bind(local,255);
    private readonly Dictionary<(Matrix3,byte),IColorShader> bindings=[];
    private readonly Dictionary<(Matrix3,Vector4),IColorShader> colorBindings=[];
    public IColorShader Bind(Matrix3 drawingMatrix,Vector4 paint)
    {
        lock(colorBindings)
        {var key=(drawingMatrix,paint);if(colorBindings.TryGetValue(key,out var found))return found;var child=source.Bind(drawingMatrix*local,paint);IColorShader result=child is IPremultipliedColorShader pm?new PremultipliedTransformedShader(pm,inverse):throw new NotSupportedException("Contextual shader bindings must preserve premultiplied sampling.");if(colorBindings.Count>=8)colorBindings.Clear();colorBindings.Add(key,result);return result;}
    }
    public IColorShader Bind(Matrix3 drawingMatrix,byte opacity)
    {
        lock(bindings)
        {var key=(drawingMatrix,opacity);if(bindings.TryGetValue(key,out var found))return found;var child=source.Bind(drawingMatrix*local,opacity);IColorShader result=child is IPremultipliedColorShader pm?new PremultipliedTransformedShader(pm,inverse):throw new NotSupportedException("Contextual shader bindings must preserve premultiplied sampling.");if(bindings.Count>=8)bindings.Clear();bindings.Add(key,result);return result;}
    }
    public Vector4 SamplePremultiplied(float x,float y)
    {var p=inverse.Map(x,y);return ((IPremultipliedColorShader)initial).SamplePremultiplied(p.X,p.Y);}
    public Rgba32 Sample(float x,float y){var p=SamplePremultiplied(x,y);return p.W>0?new(ColorBlend.Byte(p.X/p.W),ColorBlend.Byte(p.Y/p.W),ColorBlend.Byte(p.Z/p.W),ColorBlend.Byte(p.W)):default;}
}
