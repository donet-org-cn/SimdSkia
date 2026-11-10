using System.Numerics;
using SimdSkia;
namespace SimdSkia.Cpu;
/// <summary>Scalar reference for the full blend set. Established integer SIMD paths
/// continue to handle SourceOver/Source/Clear without changing their rounding.</summary>
public static partial class BlendKernel
{
    public static void Apply(Span<byte> pixels,PixelFormat format,AlphaFormat alpha,Rgba32 color,CompositeMode mode,byte coverage=255)
    {
        if(format==PixelFormat.Alpha8)throw new NotSupportedException("The 32-bit blend kernel requires RGBA/BGRA; use RasterCanvas for Alpha8 targets.");
        if(pixels.Length%4!=0)throw new ArgumentException("Pixel span must contain complete pixels.",nameof(pixels));if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));if(coverage==0||mode==CompositeMode.Destination)return;
        if(alpha!=AlphaFormat.Straight&&LowPrecision(mode)){ApplyLowPrecision(pixels,format,alpha,new(PixelBuffer.Multiply(color.R,color.A),PixelBuffer.Multiply(color.G,color.A),PixelBuffer.Multiply(color.B,color.A),color.A),mode,coverage);return;}
        float sa=color.A*(1f/255);var source=new Vector4(color.R*sa/255,color.G*sa/255,color.B*sa/255,sa);
        ApplyVector(pixels,format,alpha,source,mode,coverage);
    }
    internal static void ApplyPremultiplied(Span<byte> pixels,PixelFormat format,AlphaFormat alpha,Rgba32 color,byte opacity,CompositeMode mode,byte coverage)
    {float scale=opacity/(255f*255);ApplyVector(pixels,format,alpha,new Vector4(color.R,color.G,color.B,color.A)*scale,mode,coverage);}
    internal static void ApplyPremultiplied(Span<byte> pixels,PixelFormat format,AlphaFormat alpha,Vector4 source,byte opacity,CompositeMode mode,byte coverage)
        =>ApplyVector(pixels,format,alpha,source*(opacity*(1f/255)),mode,coverage,false);
    internal static void ApplyCustom(Span<byte> pixels,PixelFormat format,AlphaFormat alpha,Vector4 source,CompositeMode mode,byte coverage,Func<Vector4,Vector4,Vector4>? blender)
        =>ApplyVector(pixels,format,alpha,source,mode,coverage,false,blender);
    private static void ApplyVector(Span<byte> pixels,PixelFormat format,AlphaFormat alpha,Vector4 source,CompositeMode mode,byte coverage,bool constrainPma=true,Func<Vector4,Vector4,Vector4>? blender=null)
    {
        if(coverage==0||blender is null&&mode==CompositeMode.Destination)return;int ri=format==PixelFormat.Rgba8888?0:2,bi=2-ri;float cov=coverage*(1f/255);
        int first=blender is null&&coverage==255?UniformFloat(pixels,format,alpha,source,mode,constrainPma):0;
        for(int at=first;at<pixels.Length;at+=4)
        {
            var p=pixels.Slice(at,4);float da=alpha==AlphaFormat.Opaque?1:p[3]*(1f/255);var destination=new Vector4(p[ri]*(1f/255),p[1]*(1f/255),p[bi]*(1f/255),da);if(alpha==AlphaFormat.Straight)destination=new(destination.X*da,destination.Y*da,destination.Z*da,da);
            var value=blender is not null?blender(source,destination):ColorBlend.ComposePremultiplied(mode==CompositeMode.Plus?source*cov:source,destination,mode);if(coverage!=255&&(blender is not null||mode!=CompositeMode.Plus))value=destination+(value-destination)*cov;float oa=Math.Clamp(value.W,0,1);float limit=constrainPma?oa:1;float r=Math.Clamp(value.X,0,limit),g=Math.Clamp(value.Y,0,limit),b=Math.Clamp(value.Z,0,limit);if(alpha==AlphaFormat.Straight&&oa>0){r/=oa;g/=oa;b/=oa;}
            p[ri]=ColorBlend.Byte(r);p[1]=ColorBlend.Byte(g);p[bi]=ColorBlend.Byte(b);p[3]=ColorBlend.Byte(oa);
        }
    }
    private static bool LowPrecision(CompositeMode mode)=>mode is not (CompositeMode.ColorDodge or CompositeMode.ColorBurn or CompositeMode.SoftLight or CompositeMode.Hue or CompositeMode.Saturation or CompositeMode.Color or CompositeMode.Luminosity);
    // Byte-stage formulas and rounding follow Skia m119 SkRasterPipeline_opts.h
    // (Google, BSD-3). High precision remains available for unpremultiplied targets.
    private static void ApplyLowPrecision(Span<byte> pixels,PixelFormat format,AlphaFormat alpha,Rgba32 source,CompositeMode mode,byte coverage)
    {
        static int Approx(int value)=>(value+255)>>8;
        static int Accurate(int value){value+=128;return(value+(value>>8))>>8;}
        int sr=source.R,sg=source.G,sb=source.B,sa=source.A,ri=format==PixelFormat.Rgba8888?0:2,bi=2-ri;
        bool scale=mode is CompositeMode.DestinationOver or CompositeMode.SourceAtop or CompositeMode.DestinationOut or CompositeMode.Xor or CompositeMode.Plus or CompositeMode.Screen;
        if(scale&&coverage!=255){sr=Approx(sr*coverage);sg=Approx(sg*coverage);sb=Approx(sb*coverage);sa=Approx(sa*coverage);}
        int Channel(int s,int d,int da)=>mode switch
        {
            CompositeMode.SourceIn=>Accurate(s*da),CompositeMode.DestinationIn=>Accurate(d*sa),
            CompositeMode.SourceOut=>Accurate(s*(255-da)),CompositeMode.DestinationOut=>Accurate(d*(255-sa)),
            CompositeMode.SourceAtop=>Approx(s*da+d*(255-sa)),CompositeMode.DestinationAtop=>Approx(d*sa+s*(255-da)),
            CompositeMode.DestinationOver=>d+Accurate(s*(255-da)),CompositeMode.Xor=>Approx(s*(255-da)+d*(255-sa)),
            CompositeMode.Plus=>Math.Min(255,s+d),CompositeMode.Modulate=>Accurate(s*d),CompositeMode.Screen=>s+d-Accurate(s*d),
            CompositeMode.Multiply=>Approx(s*(255-da)+d*(255-sa)+s*d),
            CompositeMode.Darken=>s+d-Approx(Math.Max(s*da,d*sa)),CompositeMode.Lighten=>s+d-Approx(Math.Min(s*da,d*sa)),
            CompositeMode.Difference=>s+d-2*Approx(Math.Min(s*da,d*sa)),CompositeMode.Exclusion=>s+d-2*Approx(s*d),
            CompositeMode.HardLight=>Approx(s*(255-da)+d*(255-sa)+(2*s<=sa?2*s*d:sa*da-2*(sa-s)*(da-d))),
            CompositeMode.Overlay=>Approx(s*(255-da)+d*(255-sa)+(2*d<=da?2*s*d:sa*da-2*(sa-s)*(da-d))),
            CompositeMode.Clear=>0,CompositeMode.Source=>s,CompositeMode.Destination=>d,CompositeMode.SourceOver=>s+Accurate(d*(255-sa)),
            _=>throw new ArgumentOutOfRangeException(nameof(mode))
        };
        int first=mode==CompositeMode.Multiply&&coverage==255&&CpuOptions.EnableSimd&&System.Runtime.Intrinsics.X86.Avx2.IsSupported?MultiplyKernel.Apply(pixels,format,alpha==AlphaFormat.Opaque,source):0;
        for(int at=first;at<pixels.Length;at+=4)
        {
            var p=pixels.Slice(at,4);int da=alpha==AlphaFormat.Opaque?255:p[3];int r=Channel(sr,p[ri],da),g=Channel(sg,p[1],da),b=Channel(sb,p[bi],da);
            int a=mode is CompositeMode.Darken or CompositeMode.Lighten or CompositeMode.Difference or CompositeMode.Exclusion or CompositeMode.HardLight or CompositeMode.Overlay?sa+Approx(da*(255-sa)):Channel(sa,da,da);
            if(!scale&&coverage!=255){r=Approx(p[ri]*(255-coverage)+r*coverage);g=Approx(p[1]*(255-coverage)+g*coverage);b=Approx(p[bi]*(255-coverage)+b*coverage);a=Approx(da*(255-coverage)+a*coverage);}
            p[ri]=(byte)Math.Clamp(r,0,255);p[1]=(byte)Math.Clamp(g,0,255);p[bi]=(byte)Math.Clamp(b,0,255);p[3]=(byte)Math.Clamp(a,0,255);
        }
    }

}
