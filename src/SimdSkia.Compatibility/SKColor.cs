using System.Globalization;
using System.Runtime.InteropServices;
using SimdSkia;

namespace SkiaSharp;

[StructLayout(LayoutKind.Sequential)]
public readonly struct SKColor : IEquatable<SKColor>
{
    public static readonly SKColor Empty;
    private readonly uint color;
    public SKColor(uint value) => color=value;
    public SKColor(byte red, byte green, byte blue, byte alpha) => color=((uint)alpha<<24)|((uint)red<<16)|((uint)green<<8)|blue;
    public SKColor(byte red, byte green, byte blue) : this(red,green,blue,255) { }
    public byte Alpha => (byte)(color>>24);
    public byte Red => (byte)(color>>16);
    public byte Green => (byte)(color>>8);
    public byte Blue => (byte)color;
    public SKColor WithAlpha(byte alpha) => new(Red,Green,Blue,alpha);
    public SKColor WithRed(byte red) => new(red,Green,Blue,Alpha);
    public SKColor WithGreen(byte green) => new(Red,green,Blue,Alpha);
    public SKColor WithBlue(byte blue) => new(Red,Green,blue,Alpha);
    public bool Equals(SKColor obj) => color==obj.color;
    public override bool Equals(object? other) => other is SKColor c && Equals(c);
    public override int GetHashCode() => color.GetHashCode();
    public override string ToString() => "#"+color.ToString("x8",CultureInfo.InvariantCulture);
    public static bool operator ==(SKColor left, SKColor right) => left.Equals(right);
    public static bool operator !=(SKColor left, SKColor right) => !left.Equals(right);
    public static implicit operator SKColor(uint color) => new(color);
    public static explicit operator uint(SKColor color) => color.color;
    internal Rgba32 ToCore() => new(Red,Green,Blue,Alpha);
    internal static SKColor FromCore(Rgba32 color) => new(color.R,color.G,color.B,color.A);
}

public struct SKColors
{
    public static SKColor Empty => SKColor.Empty;
    public static SKColor Transparent=new(0x00ffffff);
    public static SKColor Black=new(0xff000000);
    public static SKColor White=new(0xffffffff);
    public static SKColor Red=new(0xffff0000);
    public static SKColor Green=new(0xff008000);
    public static SKColor Blue=new(0xff0000ff);
    public static SKColor Yellow=new(0xffffff00);
}
