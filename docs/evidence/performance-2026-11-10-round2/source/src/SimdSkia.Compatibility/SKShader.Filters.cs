using System.Numerics;
using SimdSkia;
namespace SkiaSharp;
public partial class SKShader
{
    public static SKShader CreateColorFilter(SKShader shader,SKColorFilter filter){ArgumentNullException.ThrowIfNull(shader);return shader.WithColorFilter(filter);}
    public SKShader WithColorFilter(SKColorFilter filter)
    {ArgumentNullException.ThrowIfNull(filter);var source=ToCore();var operation=filter.Capture();return new(new FilteredShader(source,operation)){resources=resources?.Retain()};}
    private sealed class FilteredShader(IColorShader shader,Func<Vector4,Vector4> filter) : IPremultipliedColorShader
    {
        public Vector4 SamplePremultiplied(float x,float y)
        {
            if(shader is IPremultipliedColorShader pm)return filter(pm.SamplePremultiplied(x,y));var c=shader.Sample(x,y);float a=c.A/255f;return filter(new(c.R/255f*a,c.G/255f*a,c.B/255f*a,a));
        }
        public Rgba32 Sample(float x,float y){var p=SamplePremultiplied(x,y);return p.W<=0?default:new(ColorBlend.Byte(p.X/p.W),ColorBlend.Byte(p.Y/p.W),ColorBlend.Byte(p.Z/p.W),ColorBlend.Byte(p.W));}
    }
}
