namespace SimdSkia.Cpu;

// Invert the eventual encoded-space blend to approximate a linear-light text
// edge. This is a text-mask compatibility policy, not a change to shape AA or
// the destination color space. Tables contain no measured/hand-fitted values.
internal static class GlyphMaskGamma
{
    private static readonly byte[][] tables=Enumerable.Range(0,8).Select(Build).ToArray();
    internal static ReadOnlySpan<byte> Table(int luminance)=>tables[luminance];
    private static int Canonical(int value){int bits=value>>5;return (bits<<5)|(bits<<2)|(bits>>1);}
    internal static int Luminance(Paint paint)
    {
        if(paint.Shader is not null)return 4;
        var c=paint.Color;
        return ((Canonical(c.R)*54+Canonical(c.G)*183+Canonical(c.B)*19)>>8)>>5;
    }
    private static double Linear(double value)=>value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4);
    private static double Encoded(double value)=>value<=.0031308?value*12.92:1.055*Math.Pow(value,1/2.4)-.055;
    private static byte[] Build(int luminance)
    {
        double source=Canonical(luminance<<5)/255d,destination=1-source;
        double s=Linear(source),d=Linear(destination),contrast=128/255d*d;
        var result=new byte[256];
        for(int i=0;i<result.Length;i++)
        {
            double coverage=i/255d;coverage+=coverage*(1-coverage)*contrast;
            double output=Encoded(s*coverage+d*(1-coverage));
            double corrected=Math.Abs(source-destination)<1/256d?coverage:(output-destination)/(source-destination);
            result[i]=(byte)Math.Clamp(Math.Floor(corrected*255+.5),0,255);
        }
        result[0]=0;result[255]=255;return result;
    }
}
