namespace SkiaSharp;

public struct SKImageInfo : IEquatable<SKImageInfo>
{
    public static readonly SKImageInfo Empty;
    // The native N32 format on the currently supported little-endian hosts is BGRA.
    public static readonly SKColorType PlatformColorType=SKColorType.Bgra8888;
    public static readonly int PlatformColorAlphaShift=24;
    public static readonly int PlatformColorRedShift=16;
    public static readonly int PlatformColorGreenShift=8;
    public static readonly int PlatformColorBlueShift=0;
    public int Width { readonly get; set; }
    public int Height { readonly get; set; }
    public SKColorType ColorType { readonly get; set; }
    public SKAlphaType AlphaType { readonly get; set; }
    public SKColorSpace? ColorSpace { readonly get; set; }
    public SKImageInfo(int width,int height) : this(width,height,PlatformColorType,SKAlphaType.Premul) { }
    public SKImageInfo(int width,int height,SKColorType colorType) : this(width,height,colorType,SKAlphaType.Premul) { }
    public SKImageInfo(int width,int height,SKColorType colorType,SKAlphaType alphaType) : this(width,height,colorType,alphaType,null) { }
    public SKImageInfo(int width,int height,SKColorType colorType,SKAlphaType alphaType,SKColorSpace? colorspace)
    { Width=width; Height=height; ColorType=colorType; AlphaType=alphaType; ColorSpace=colorspace; }
    public readonly int BytesPerPixel => ColorType switch
    {
        SKColorType.Unknown => 0,
        SKColorType.Rgba8888 or SKColorType.Bgra8888 => 4,
        _ => throw new NotSupportedException($"Pixel format {ColorType} is not implemented.")
    };
    public readonly int BitsPerPixel => BytesPerPixel*8;
    public readonly int BitShiftPerPixel => ColorType switch
    { SKColorType.Unknown => 0, SKColorType.Rgba8888 or SKColorType.Bgra8888 => 2, _ => throw new NotSupportedException($"Pixel format {ColorType} is not implemented.") };
    public readonly int RowBytes => checked(Width*BytesPerPixel);
    public readonly long RowBytes64 => (long)Width*BytesPerPixel;
    public readonly int BytesSize => checked(RowBytes*Height);
    public readonly long BytesSize64 => RowBytes64*Height;
    public readonly bool IsEmpty => Width<=0 || Height<=0;
    public readonly bool IsOpaque => AlphaType==SKAlphaType.Opaque;
    public readonly SKImageInfo WithSize(int width,int height) => new(width,height,ColorType,AlphaType,ColorSpace);
    public readonly SKImageInfo WithColorType(SKColorType newColorType) => new(Width,Height,newColorType,AlphaType,ColorSpace);
    public readonly SKImageInfo WithAlphaType(SKAlphaType newAlphaType) => new(Width,Height,ColorType,newAlphaType,ColorSpace);
    public readonly SKImageInfo WithColorSpace(SKColorSpace? newColorSpace) => new(Width,Height,ColorType,AlphaType,newColorSpace);
    public readonly bool Equals(SKImageInfo obj) => Width==obj.Width && Height==obj.Height && ColorType==obj.ColorType && AlphaType==obj.AlphaType && ColorSpace==obj.ColorSpace;
    public override readonly bool Equals(object? obj) => obj is SKImageInfo other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Width,Height,ColorType,AlphaType,ColorSpace);
    public static bool operator ==(SKImageInfo left,SKImageInfo right) => left.Equals(right);
    public static bool operator !=(SKImageInfo left,SKImageInfo right) => !left.Equals(right);
}

/// <summary>Only the sRGB identity profile is supported in this milestone.</summary>
public class SKColorSpace : SKObject
{
    private SKColorSpace() { }
    public static SKColorSpace CreateSrgb() => new();
    public static SKColorSpace CreateSrgbLinear() => throw new NotSupportedException("Linear-light color spaces require the phase 4 color pipeline.");
    public bool IsSrgb { get { ThrowIfDisposed(); return true; } }
}
