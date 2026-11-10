using System.Numerics;
using SimdSkia;
namespace SkiaSharp;
public partial class SKShader
{
    public static SKShader CreateCompose(SKShader shaderA,SKShader shaderB)=>CreateCompose(shaderA,shaderB,SKBlendMode.SrcOver);
    public static SKShader CreateCompose(SKShader shaderA,SKShader shaderB,SKBlendMode mode)=>CreateBlend(mode,shaderA,shaderB);
    public static SKShader CreateBlend(SKBlendMode mode,SKShader shaderA,SKShader shaderB)=>CreateBlend(SKBlender.CreateBlendMode(mode),shaderA,shaderB);
    public static SKShader CreateBlend(SKBlender blender,SKShader shaderA,SKShader shaderB)
    {
        ArgumentNullException.ThrowIfNull(shaderA);ArgumentNullException.ThrowIfNull(shaderB);ArgumentNullException.ThrowIfNull(blender);
        var a=shaderA.ToCore();var b=shaderB.ToCore();var blend=blender.Capture();var owners=new List<IDisposable>(2);
        if(shaderA.RetainResources() is {} ar)owners.Add(ar);if(shaderB.RetainResources() is {} br)owners.Add(br);
        return new(new ComposedShader(a,b,blend)){resources=owners.Count==0?null:new(owners.ToArray())};
    }
    private sealed class ComposedShader(IColorShader destination,IColorShader source,Func<Vector4,Vector4,Vector4> blend) : IPremultipliedColorShader
    {
        private static Vector4 Sample(IColorShader shader,float x,float y)
        {
            if(shader is IPremultipliedColorShader pm)return pm.SamplePremultiplied(x,y);
            var c=shader.Sample(x,y);float a=c.A/255f;return new(c.R/255f*a,c.G/255f*a,c.B/255f*a,a);
        }
        public Vector4 SamplePremultiplied(float x,float y)=>blend(Sample(source,x,y),Sample(destination,x,y));
        public Rgba32 Sample(float x,float y){var p=SamplePremultiplied(x,y);return p.W<=0?default:new(ColorBlend.Byte(p.X/p.W),ColorBlend.Byte(p.Y/p.W),ColorBlend.Byte(p.Z/p.W),ColorBlend.Byte(p.W));}
    }
}
