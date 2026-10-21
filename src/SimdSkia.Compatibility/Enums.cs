namespace SkiaSharp;

public enum SKAlphaType { Unknown=0, Opaque=1, Premul=2, Unpremul=3 }
public enum SKColorType
{
    Unknown=0, Alpha8=1, Rgb565=2, Argb4444=3, Rgba8888=4, Rgb888x=5, Bgra8888=6,
    Rgba1010102=7, Rgb101010x=8, Gray8=9, RgbaF16=10, RgbaF16Clamped=11, RgbaF32=12,
    Rg88=13, AlphaF16=14, RgF16=15, Alpha16=16, Rg1616=17, Rgba16161616=18,
    Bgra1010102=19, Bgr101010x=20, Bgr101010xXR=21, Srgba8888=22, R8Unorm=23, Rgba10x6=24
}
public enum SKPaintStyle { Fill=0, Stroke=1, StrokeAndFill=2 }
public enum SKPathFillType { Winding=0,EvenOdd=1,InverseWinding=2,InverseEvenOdd=3 }
public enum SKPathDirection { Clockwise=0,CounterClockwise=1 }
public enum SKStrokeCap { Butt=0,Round=1,Square=2 }
public enum SKStrokeJoin { Miter=0,Round=1,Bevel=2 }
public enum SKClipOperation { Difference=0,Intersect=1 }
public enum SKShaderTileMode { Clamp=0,Repeat=1,Mirror=2,Decal=3 }
public enum SKBlendMode
{
    Clear=0, Src=1, Dst=2, SrcOver=3, DstOver=4, SrcIn=5, DstIn=6, SrcOut=7,
    DstOut=8, SrcATop=9, DstATop=10, Xor=11, Plus=12, Modulate=13, Screen=14,
    Overlay=15, Darken=16, Lighten=17, ColorDodge=18, ColorBurn=19, HardLight=20,
    SoftLight=21, Difference=22, Exclusion=23, Multiply=24, Hue=25, Saturation=26, Color=27, Luminosity=28
}
