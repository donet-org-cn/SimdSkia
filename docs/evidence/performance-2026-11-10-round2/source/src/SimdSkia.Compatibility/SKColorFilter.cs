using System.Numerics;
using SimdSkia;
namespace SkiaSharp;

/// <summary>Immutable float color transforms over premultiplied sRGB samples.</summary>
public class SKColorFilter : SKObject
{
    public const int ColorMatrixSize=20,TableMaxLength=256;
    private readonly Func<Vector4,Vector4> operation;
    private readonly Func<Vector4,Vector4>? rasterOperation;
    private readonly bool lowPrecision;
    private readonly bool permanent;
    private SKColorFilter(Func<Vector4,Vector4> operation,bool permanent=false,Func<Vector4,Vector4>? rasterOperation=null,bool lowPrecision=false){this.operation=operation;this.permanent=permanent;this.rasterOperation=rasterOperation;this.lowPrecision=lowPrecision;}
    internal static SKColorFilter Runtime(Func<Vector4,Vector4> operation)=>new(operation);
    protected override bool IgnoreDispose=>permanent;
    internal Func<Vector4,Vector4> Capture(bool raster=true){ThrowIfDisposed();return raster?rasterOperation??operation:operation;}
    internal IColorFilter ToCore(){ThrowIfDisposed();return new CapturedFilter(operation,rasterOperation??operation,lowPrecision);}
    private sealed record CapturedFilter(Func<Vector4,Vector4> Operation,Func<Vector4,Vector4> Raster,bool UsesLowPrecision) : IColorFilter {public Vector4 Filter(Vector4 premultiplied)=>Operation(premultiplied);public Vector4 FilterRaster(Vector4 premultiplied)=>Raster(premultiplied);}
    private static Vector4 Straight(Vector4 p)=>p.W>0?new(p.X/p.W,p.Y/p.W,p.Z/p.W,p.W):Vector4.Zero;
    private static Vector4 Premul(Vector4 p)=>new(p.X*p.W,p.Y*p.W,p.Z*p.W,p.W);
    private static Vector4 Clamp(Vector4 p)=>Vector4.Clamp(p,Vector4.Zero,Vector4.One);
    private static float ToLinear(float x)=>x<=.04045f?x/12.92f:MathF.Pow((x+.055f)/1.055f,2.4f);
    private static float ToSrgb(float x)=>x<=.0031308f?12.92f*x:1.055f*MathF.Pow(x,1f/2.4f)-.055f;
    private static readonly SKColorFilter SrgbToLinear=new(p=>{var c=Straight(p);return Premul(new(ToLinear(c.X),ToLinear(c.Y),ToLinear(c.Z),c.W));},true);
    private static readonly SKColorFilter LinearToSrgb=new(p=>{var c=Straight(p);return Premul(new(ToSrgb(c.X),ToSrgb(c.Y),ToSrgb(c.Z),c.W));},true);
    public static SKColorFilter CreateSrgbToLinearGamma()=>SrgbToLinear;
    public static SKColorFilter CreateLinearToSrgbGamma()=>LinearToSrgb;
    public static SKColorFilter CreateBlendMode(SKColor c,SKBlendMode mode)
    {
        var blend=SKPaint.GetComposite(mode);if(mode==SKBlendMode.Dst)return null!;float a=c.Alpha/255f;var s=new Vector4(c.Red/255f*a,c.Green/255f*a,c.Blue/255f*a,a);
        bool low=mode is not (SKBlendMode.ColorDodge or SKBlendMode.ColorBurn or SKBlendMode.SoftLight or SKBlendMode.Hue or SKBlendMode.Saturation or SKBlendMode.Color or SKBlendMode.Luminosity);
        return new(d=>ColorBlend.ComposePremultiplied(s,d,blend),rasterOperation:d=>ColorBlend.ComposeRaster(s,d,blend),lowPrecision:low);
    }
    public static SKColorFilter CreateLighting(SKColor mul,SKColor add)
    {
        var m=new Vector3(mul.Red,mul.Green,mul.Blue)/255;var a=new Vector3(add.Red,add.Green,add.Blue)/255;
        return new(p=>{var c=Straight(p);var rgb=Vector3.Clamp(new Vector3(c.X,c.Y,c.Z)*m+a,Vector3.Zero,Vector3.One);return Premul(new(rgb,c.W));});
    }
    public static SKColorFilter CreateCompose(SKColorFilter outer,SKColorFilter inner)
    {ArgumentNullException.ThrowIfNull(outer);ArgumentNullException.ThrowIfNull(inner);var a=outer.Capture(false);var b=inner.Capture(false);var ar=outer.Capture();var br=inner.Capture();return new(p=>a(b(p)),rasterOperation:p=>ar(br(p)));}
    public static SKColorFilter CreateLerp(float weight,SKColorFilter filter0,SKColorFilter filter1)
    {ArgumentNullException.ThrowIfNull(filter0);ArgumentNullException.ThrowIfNull(filter1);if(!float.IsFinite(weight))throw new ArgumentOutOfRangeException(nameof(weight));float w=Math.Clamp(weight,0,1);var a=filter0.Capture();var b=filter1.Capture();return new(p=>Vector4.Lerp(a(p),b(p),w));}
    public static SKColorFilter CreateColorMatrix(float[] matrix){ArgumentNullException.ThrowIfNull(matrix);return CreateColorMatrix(matrix.AsSpan());}
    public static SKColorFilter CreateColorMatrix(ReadOnlySpan<float> matrix)=>Matrix(matrix,false);
    public static SKColorFilter CreateHslaColorMatrix(ReadOnlySpan<float> matrix)=>Matrix(matrix,true);
    private static SKColorFilter Matrix(ReadOnlySpan<float> matrix,bool hsla)
    {
        if(matrix.Length!=20)throw new ArgumentException("Matrix must have a length of 20.",nameof(matrix));foreach(float f in matrix)if(!float.IsFinite(f))return null!;
        var m=matrix.ToArray();return new(p=>{var c=Straight(p);if(hsla)c=new(RgbToHsl(new(c.X,c.Y,c.Z)),c.W);var result=new Vector4();for(int row=0;row<4;row++)result[row]=m[row*5]*c.X+m[row*5+1]*c.Y+m[row*5+2]*c.Z+m[row*5+3]*c.W+m[row*5+4];if(hsla)result=new(HslToRgb(new(result.X,result.Y,result.Z)),result.W);return Premul(Clamp(result));});
    }
    public static SKColorFilter CreateLumaColor()=>new(p=>new(0,0,0,.2126f*p.X+.7152f*p.Y+.0722f*p.Z));
    public static SKColorFilter CreateTable(byte[] table){ArgumentNullException.ThrowIfNull(table);return CreateTable(table.AsSpan());}
    public static SKColorFilter CreateTable(ReadOnlySpan<byte> table)
    {ValidateTable(table,nameof(table));return CreateTable(table,table,table,table);}
    public static SKColorFilter CreateTable(byte[] tableA,byte[] tableR,byte[] tableG,byte[] tableB)
    {ArgumentNullException.ThrowIfNull(tableA);ArgumentNullException.ThrowIfNull(tableR);ArgumentNullException.ThrowIfNull(tableG);ArgumentNullException.ThrowIfNull(tableB);return CreateTable(tableA.AsSpan(),tableR.AsSpan(),tableG.AsSpan(),tableB.AsSpan());}
    public static SKColorFilter CreateTable(ReadOnlySpan<byte> tableA,ReadOnlySpan<byte> tableR,ReadOnlySpan<byte> tableG,ReadOnlySpan<byte> tableB)
    {
        ValidateTable(tableA,nameof(tableA));ValidateTable(tableR,nameof(tableR));ValidateTable(tableG,nameof(tableG));ValidateTable(tableB,nameof(tableB));var a=tableA.ToArray();var r=tableR.ToArray();var g=tableG.ToArray();var b=tableB.ToArray();
        return new(p=>{var c=Straight(p);return Premul(new(r[ColorBlend.Byte(c.X)]/255f,g[ColorBlend.Byte(c.Y)]/255f,b[ColorBlend.Byte(c.Z)]/255f,a[ColorBlend.Byte(c.W)]/255f));});
    }
    private static void ValidateTable(ReadOnlySpan<byte> table,string name){if(table.Length!=256)throw new ArgumentException("Table must have a length of 256.",name);}
    public static SKColorFilter CreateHighContrast(bool grayscale,SKHighContrastConfigInvertStyle invertStyle,float contrast)=>CreateHighContrast(new(grayscale,invertStyle,contrast));
    public static SKColorFilter CreateHighContrast(SKHighContrastConfig config)
    {
        if(!config.IsValid)return null!;float contrast=Math.Clamp(config.Contrast,-1+1.1920929e-7f,1-1.1920929e-7f),scale=(1+contrast)/(1-contrast);
        // SkHighContrastFilter (Google, BSD-3) works on straight linear-light RGB.
        return new(p=>{var c=Straight(p);var rgb=new Vector3(ToLinear(c.X),ToLinear(c.Y),ToLinear(c.Z));if(config.Grayscale)rgb=new(Vector3.Dot(rgb,new(.2126f,.7152f,.0722f)));if(config.InvertStyle==SKHighContrastConfigInvertStyle.InvertBrightness)rgb=Vector3.One-rgb;else if(config.InvertStyle==SKHighContrastConfigInvertStyle.InvertLightness){var hsl=RgbToHsl(rgb);hsl.Z=1-hsl.Z;rgb=HslToRgb(hsl);}rgb=Vector3.Clamp(new Vector3(.5f)+(rgb-new Vector3(.5f))*scale,Vector3.Zero,Vector3.One);return Premul(new(ToSrgb(rgb.X),ToSrgb(rgb.Y),ToSrgb(rgb.Z),c.W));});
    }
    private static Vector3 RgbToHsl(Vector3 c)
    {
        float max=MathF.Max(c.X,MathF.Max(c.Y,c.Z)),min=MathF.Min(c.X,MathF.Min(c.Y,c.Z)),d=max-min,l=(max+min)*.5f;if(d==0)return new(0,0,l);
        float h=max==c.X?(c.Y-c.Z)/d+(c.Y<c.Z?6:0):max==c.Y?(c.Z-c.X)/d+2:(c.X-c.Y)/d+4;return new(h/6,d/(1-MathF.Abs(2*l-1)),l);
    }
    private static Vector3 HslToRgb(Vector3 hsl)
    {
        float h=(hsl.X-MathF.Floor(hsl.X))*6,s=Math.Clamp(hsl.Y,0,1),l=Math.Clamp(hsl.Z,0,1),c=(1-MathF.Abs(2*l-1))*s,x=c*(1-MathF.Abs(h%2-1));
        Vector3 rgb=h<1?new(c,x,0):h<2?new(x,c,0):h<3?new(0,c,x):h<4?new(0,x,c):h<5?new(x,0,c):new(c,0,x);return rgb+new Vector3(l-c*.5f);
    }
}
