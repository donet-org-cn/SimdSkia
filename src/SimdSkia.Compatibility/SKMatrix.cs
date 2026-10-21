using System.Runtime.InteropServices;
using SimdSkia;

namespace SkiaSharp;

[StructLayout(LayoutKind.Sequential)]
public struct SKMatrix : IEquatable<SKMatrix>
{
    public static readonly SKMatrix Empty;
    public float ScaleX { readonly get; set; }
    public float SkewX { readonly get; set; }
    public float TransX { readonly get; set; }
    public float SkewY { readonly get; set; }
    public float ScaleY { readonly get; set; }
    public float TransY { readonly get; set; }
    public float Persp0 { readonly get; set; }
    public float Persp1 { readonly get; set; }
    public float Persp2 { readonly get; set; }
    public SKMatrix(float scaleX,float skewX,float transX,float skewY,float scaleY,float transY,float persp0,float persp1,float persp2)
    { ScaleX=scaleX; SkewX=skewX; TransX=transX; SkewY=skewY; ScaleY=scaleY; TransY=transY; Persp0=persp0; Persp1=persp1; Persp2=persp2; }
    public static readonly SKMatrix Identity=FromCore(Matrix3.Identity);
    public static SKMatrix CreateIdentity() => Identity;
    public readonly bool IsIdentity => this==Identity;
    public static SKMatrix CreateTranslation(float x,float y) => new(1,0,x,0,1,y,0,0,1);
    public static SKMatrix CreateScale(float x,float y) => new(x,0,0,0,y,0,0,0,1);
    public static SKMatrix CreateRotation(float radians)
    {
        float c=MathF.Cos(radians), s=MathF.Sin(radians);
        return new(c,-s,0,s,c,0,0,0,1);
    }
    public static SKMatrix CreateRotationDegrees(float degrees) => CreateRotation(degrees*MathF.PI/180);
    public readonly SKPoint MapPoint(SKPoint point) => MapPoint(point.X,point.Y);
    public readonly SKPoint MapPoint(float x,float y) { var p=ToCore().Map(x,y); return new(p.X,p.Y); }
    public readonly bool TryInvert(out SKMatrix inverse) { bool ok=ToCore().TryInvert(out var m); inverse=FromCore(m); return ok; }
    public readonly bool Equals(SKMatrix obj) => ScaleX==obj.ScaleX && SkewX==obj.SkewX && TransX==obj.TransX
        && SkewY==obj.SkewY && ScaleY==obj.ScaleY && TransY==obj.TransY
        && Persp0==obj.Persp0 && Persp1==obj.Persp1 && Persp2==obj.Persp2;
    public override readonly bool Equals(object? obj) => obj is SKMatrix other && Equals(other);
    public override readonly int GetHashCode() => ToCore().GetHashCode();
    public static bool operator ==(SKMatrix left,SKMatrix right) => left.Equals(right);
    public static bool operator !=(SKMatrix left,SKMatrix right) => !left.Equals(right);
    internal readonly Matrix3 ToCore() => new(ScaleX,SkewX,TransX,SkewY,ScaleY,TransY,Persp0,Persp1,Persp2);
    internal static SKMatrix FromCore(Matrix3 m) => new(m.M00,m.M01,m.M02,m.M10,m.M11,m.M12,m.M20,m.M21,m.M22);
}
