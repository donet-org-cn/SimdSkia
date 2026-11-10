using System.Numerics;
namespace SimdSkia;
/// <summary>An A8 image is a coverage material using the drawing paint's
/// unpremultiplied RGB, rather than a black RGBA texture.</summary>
internal sealed class AlphaImageShader(IPremultipliedColorShader source):IContextualColorShader,IPremultipliedColorShader
{
    private readonly Dictionary<Vector4,IColorShader> bindings=[];
    public bool IsOpaque=>source.IsOpaque;
    public IColorShader Bind(Matrix3 matrix,byte opacity)=>Bind(matrix,new Vector4(0,0,0,opacity/255f));
    public IColorShader Bind(Matrix3 matrix,Vector4 paint)
    {lock(bindings){if(bindings.TryGetValue(paint,out var found))return found;var bound=new Bound(source,paint);if(bindings.Count>=8)bindings.Clear();bindings.Add(paint,bound);return bound;}}
    public Vector4 SamplePremultiplied(float x,float y)=>source.SamplePremultiplied(x,y);
    public Rgba32 Sample(float x,float y)=>source.Sample(x,y);
    private sealed class Bound(IPremultipliedColorShader source,Vector4 paint):IPremultipliedColorShader
    {
        public bool IsOpaque=>source.IsOpaque&&paint.W>=1;
        public Vector4 SamplePremultiplied(float x,float y){float a=source.SamplePremultiplied(x,y).W*paint.W;return new(paint.X*a,paint.Y*a,paint.Z*a,a);}
        public Rgba32 Sample(float x,float y){var p=SamplePremultiplied(x,y);return p.W>0?new(ColorBlend.Byte(p.X/p.W),ColorBlend.Byte(p.Y/p.W),ColorBlend.Byte(p.Z/p.W),ColorBlend.Byte(p.W)):default;}
    }
}
