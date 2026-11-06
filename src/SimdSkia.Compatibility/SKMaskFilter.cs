using SimdSkia.Cpu;
namespace SkiaSharp;
public class SKMaskFilter : SKObject
{
    public const int TableMaxLength=256;
    private RasterMaskFilter? node;
    private SKMaskFilter(RasterMaskFilter node)=>this.node=node;
    internal RasterMaskFilter ToCore(){ThrowIfDisposed();return node!;}
    public static float ConvertRadiusToSigma(float radius)=>radius>0?.57735f*radius+.5f:0;
    public static float ConvertSigmaToRadius(float sigma)=>sigma>.5f?(sigma-.5f)/.57735f:0;
    public static SKMaskFilter CreateBlur(SKBlurStyle blurStyle,float sigma)=>CreateBlur(blurStyle,sigma,true);
    public static SKMaskFilter CreateBlur(SKBlurStyle blurStyle,float sigma,bool respectCTM)=>Enum.IsDefined(blurStyle)&&sigma>0&&float.IsFinite(sigma)?new(RasterMaskFilter.Blur(sigma,(int)blurStyle,respectCTM)):null!;
    public static SKMaskFilter CreateTable(byte[] table)
    {ArgumentNullException.ThrowIfNull(table);if(table.Length!=256)throw new ArgumentException("Table must have a length of {SKColorTable.MaxLength}.",nameof(table));return new(RasterMaskFilter.Table(table));}
    public static SKMaskFilter CreateGamma(float gamma)
    {
        if(!float.IsFinite(gamma))throw new ArgumentOutOfRangeException(nameof(gamma));var table=new byte[256];
        float x=0,step=1f/255;for(int i=0;i<256;i++,x+=step)table[i]=(byte)MathF.Floor(Math.Clamp(MathF.Pow(x,gamma),0,1)*255+.5f);return CreateTable(table);
    }
    public static SKMaskFilter CreateClip(byte min,byte max)
    {
        var table=new byte[256];if(max==0)max=1;if(min>=max)min=(byte)(max-1);int scale=65536*255/(max-min);
        for(int i=0;i<256;i++)table[i]=i<=min?(byte)0:i>=max?(byte)255:(byte)((scale*(i-min)+32768)>>16);return CreateTable(table);
    }
    protected override void Dispose(bool disposing)=>node=null;
}
