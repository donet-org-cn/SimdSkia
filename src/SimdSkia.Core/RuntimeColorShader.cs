using System.Numerics;
namespace SimdSkia;
public sealed class RuntimeColorShader(RuntimeEffectProgram.BoundRuntimeEffect effect,Matrix3 inverse) : IPremultipliedColorShader,IPremultipliedBatchShader
{
    public bool IsHorizontal=>effect.TryGetConstant(out _);
    public bool IsOpaque=>effect.TryGetConstant(out var c)&&c.W>=1;
    public Vector4 SamplePremultiplied(float x,float y)
    {var point=inverse.Map(x,y);return Vector4.Clamp(effect.Evaluate(new(point.X,point.Y,0,0)),Vector4.Zero,Vector4.One);}
    void IPremultipliedBatchShader.SampleBatch(Span<Vector4> samples)
    {
        for(int i=0;i<samples.Length;i++){var p=inverse.Map(samples[i].X,samples[i].Y);samples[i]=new(p.X,p.Y,0,0);}
        effect.EvaluateBatch(samples);
        for(int i=0;i<samples.Length;i++)samples[i]=Vector4.Clamp(samples[i],Vector4.Zero,Vector4.One);
    }
    public Rgba32 Sample(float x,float y)
    {var c=SamplePremultiplied(x,y);return c.W>0?new(ColorBlend.Byte(c.X/c.W),ColorBlend.Byte(c.Y/c.W),ColorBlend.Byte(c.Z/c.W),ColorBlend.Byte(c.W)):default;}
}
