// Value contracts and managed algorithms from SkiaSharp v3.119.4 (MIT). See licenses/SkiaSharp-MIT.txt.
#nullable disable
using System.Runtime.InteropServices;
namespace SkiaSharp;

public enum GRBackend : int {

    Dawn = 3,

    Direct3D = 4,

    Metal = 0,

    OpenGL = 1,

    Unsupported = 5,

    Vulkan = 2
}
[Flags]
public enum GRBackendState : uint {

    All = 4294967295,

    None = 0
}
[Flags]
public enum GRGlBackendState : uint {

    All = 65535,

    Blend = 8,

    FixedFunction = 512,

    MSAAEnable = 16,

    Misc = 1024,

    None = 0,

    PathRendering = 2048,

    PixelStore = 128,

    Program = 256,

    RenderTarget = 1,

    Stencil = 64,

    TextureBinding = 2,

    Vertex = 32,

    View = 4
}

public enum GRSurfaceOrigin : int {

    BottomLeft = 1,

    TopLeft = 0
}
[Flags]
public enum SKBitmapAllocFlags : int {

    None = 0,

    ZeroPixels = 1
}

public enum SKBlurStyle : int {

    Inner = 3,

    Normal = 0,

    Outer = 2,

    Solid = 1
}

public enum SKCanvasSaveLayerRecFlags : int {

    F16ColorType = 16,

    InitializeWithPrevious = 4,

    None = 0,

    PreserveLcdText = 2
}

public enum SKCodecScanlineOrder : int {

    BottomUp = 1,

    TopDown = 0
}

public enum SKColorChannel : int {

    A = 3,

    B = 2,

    G = 1,

    R = 0
}

public enum SKHighContrastConfigInvertStyle : int {

    InvertBrightness = 1,

    InvertLightness = 2,

    NoInvert = 0
}

public enum SKJpegEncoderAlphaOption : int {

    BlendOnBlack = 1,

    Ignore = 0
}

public enum SKJpegEncoderDownsample : int {

    Downsample420 = 0,

    Downsample422 = 1,

    Downsample444 = 2
}

public enum SKLatticeRectType : int {

    Default = 0,

    FixedColor = 2,

    Transparent = 1
}
[Obsolete("Use SKFontHinting instead.")]
public enum SKPaintHinting : int {

    Full = 3,

    NoHinting = 0,

    Normal = 2,

    Slight = 1
}

public enum SKPath1DPathEffectStyle : int {

    Morph = 2,

    Rotate = 1,

    Translate = 0
}

public enum SKPathArcSize : int {

    Large = 1,

    Small = 0
}

public enum SKPathConvexity : int {

    Concave = 2,

    Convex = 1,

    Unknown = 0
}
[Flags]
public enum SKPathMeasureMatrixFlags : int {

    GetPosition = 1,

    GetPositionAndTangent = 3,

    GetTangent = 2
}

public enum SKPathOp : int {

    Difference = 0,

    Intersect = 1,

    ReverseDifference = 4,

    Union = 2,

    Xor = 3
}

public enum SKPathVerb : int {

    Close = 5,

    Conic = 3,

    Cubic = 4,

    Done = 6,

    Line = 1,

    Move = 0,

    Quad = 2
}

public enum SKPixelGeometry : int {

    BgrHorizontal = 2,

    BgrVertical = 4,

    RgbHorizontal = 1,

    RgbVertical = 3,

    Unknown = 0
}
[Flags]
public enum SKPngEncoderFilterFlags : int {

    AllFilters = 248,

    Avg = 64,

    NoFilters = 0,

    None = 8,

    Paeth = 128,

    Sub = 16,

    Up = 32
}

public enum SKPointMode : int {

    Lines = 1,

    Points = 0,

    Polygon = 2
}

public enum SKRegionOperation : int {

    Difference = 0,

    Intersect = 1,

    Replace = 5,

    ReverseDifference = 4,

    Union = 2,

    XOR = 3
}
[Flags]
public enum SKSurfacePropsFlags : int {

    None = 0,

    UseDeviceIndependentFonts = 1
}

public enum SKTextEncoding : int {

    GlyphId = 3,

    Utf16 = 1,

    Utf32 = 2,

    Utf8 = 0
}

public enum SKTrimPathEffectMode : int {

    Inverted = 1,

    Normal = 0
}

public enum SKVertexMode : int {

    TriangleFan = 2,

    TriangleStrip = 1,

    Triangles = 0
}

public enum SKWebpEncoderCompression : int {

    Lossless = 1,

    Lossy = 0
}
