using System.Runtime.InteropServices;

namespace SkiaSharp;

[StructLayout(LayoutKind.Sequential)]
public struct SKPoint : IEquatable<SKPoint>
{
    public static readonly SKPoint Empty;
    public SKPoint(float x, float y) { X=x; Y=y; }
    public float X { readonly get; set; }
    public float Y { readonly get; set; }
    public readonly bool IsEmpty => X==0 && Y==0;
    public readonly bool Equals(SKPoint obj) => X==obj.X && Y==obj.Y;
    public override readonly bool Equals(object? obj) => obj is SKPoint other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(X,Y);
    public static bool operator ==(SKPoint left, SKPoint right) => left.Equals(right);
    public static bool operator !=(SKPoint left, SKPoint right) => !left.Equals(right);
}

[StructLayout(LayoutKind.Sequential)]
public struct SKRect : IEquatable<SKRect>
{
    public static readonly SKRect Empty;
    public SKRect(float left, float top, float right, float bottom) { Left=left; Top=top; Right=right; Bottom=bottom; }
    public float Left { readonly get; set; }
    public float Top { readonly get; set; }
    public float Right { readonly get; set; }
    public float Bottom { readonly get; set; }
    public readonly float Width => Right-Left;
    public readonly float Height => Bottom-Top;
    public readonly float MidX => (Left+Right)*0.5f;
    public readonly float MidY => (Top+Bottom)*0.5f;
    // Baseline SKRect.IsEmpty means the all-zero sentinel, rather than zero area.
    public readonly bool IsEmpty => this==Empty;
    public static SKRect Create(float width, float height) => new(0,0,width,height);
    public static SKRect Create(float x, float y, float width, float height) => new(x,y,x+width,y+height);
    public readonly bool Contains(float x, float y) => x>=Left && x<Right && y>=Top && y<Bottom;
    public readonly bool Contains(SKPoint pt) => Contains(pt.X,pt.Y);
    public readonly bool Equals(SKRect obj) => Left==obj.Left && Top==obj.Top && Right==obj.Right && Bottom==obj.Bottom;
    public override readonly bool Equals(object? obj) => obj is SKRect other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Left,Top,Right,Bottom);
    public static bool operator ==(SKRect left, SKRect right) => left.Equals(right);
    public static bool operator !=(SKRect left, SKRect right) => !left.Equals(right);
    internal readonly SimdSkia.RectF ToCore() => new(Left,Top,Right,Bottom);
}
