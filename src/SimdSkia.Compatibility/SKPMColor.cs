using SimdSkia;
namespace SkiaSharp;

public readonly struct SKPMColor:IEquatable<SKPMColor>
{
    private readonly uint color;
    public SKPMColor(uint value)=>color=value;
    public byte Alpha=>(byte)(color>>24);
    public byte Red=>(byte)(color>>16);
    public byte Green=>(byte)(color>>8);
    public byte Blue=>(byte)color;
    public static SKPMColor PreMultiply(SKColor color)=>new(((uint)color.Alpha<<24)|((uint)PixelBuffer.Multiply(color.Red,color.Alpha)<<16)|((uint)PixelBuffer.Multiply(color.Green,color.Alpha)<<8)|PixelBuffer.Multiply(color.Blue,color.Alpha));
    public static SKPMColor[] PreMultiply(SKColor[] colors)
    {ArgumentNullException.ThrowIfNull(colors);var result=new SKPMColor[colors.Length];for(int i=0;i<colors.Length;i++)result[i]=PreMultiply(colors[i]);return result;}
    public static SKColor UnPreMultiply(SKPMColor pmcolor)
    {
        byte alpha=pmcolor.Alpha;if(alpha==0)return SKColor.Empty;uint scale=(uint)((((ulong)255<<24)+alpha/2u)/alpha);
        byte Expand(byte value)=>(byte)((scale*value+(1u<<23))>>24);
        return new(Expand(pmcolor.Red),Expand(pmcolor.Green),Expand(pmcolor.Blue),alpha);
    }
    public static SKColor[] UnPreMultiply(SKPMColor[] pmcolors)
    {ArgumentNullException.ThrowIfNull(pmcolors);var result=new SKColor[pmcolors.Length];for(int i=0;i<result.Length;i++)result[i]=UnPreMultiply(pmcolors[i]);return result;}
    public static explicit operator SKPMColor(SKColor color)=>PreMultiply(color);
    public static explicit operator SKColor(SKPMColor color)=>UnPreMultiply(color);
    public static implicit operator SKPMColor(uint color)=>new(color);
    public static explicit operator uint(SKPMColor color)=>color.color;
    public bool Equals(SKPMColor obj)=>color==obj.color;
    public override bool Equals(object? other)=>other is SKPMColor c&&Equals(c);
    public override int GetHashCode()=>color.GetHashCode();
    public override string ToString()=>$"#{Alpha:x2}{Red:x2}{Green:x2}{Blue:x2}";
    public static bool operator ==(SKPMColor left,SKPMColor right)=>left.Equals(right);
    public static bool operator !=(SKPMColor left,SKPMColor right)=>!left.Equals(right);
}

public partial struct SKColor
{
    public float Hue {get{ToHsv(out var h,out _,out _);return h;}}
    public static SKColor FromHsl(float h,float s,float l,byte a=255)=>((SKColor)SKColorF.FromHsl(h,s,l)).WithAlpha(a);
    public static SKColor FromHsv(float h,float s,float v,byte a=255)=>((SKColor)SKColorF.FromHsv(h,s,v)).WithAlpha(a);
    public void ToHsl(out float h,out float s,out float l)=>((SKColorF)this).ToHsl(out h,out s,out l);
    public void ToHsv(out float h,out float s,out float v)=>((SKColorF)this).ToHsv(out h,out s,out v);
}
