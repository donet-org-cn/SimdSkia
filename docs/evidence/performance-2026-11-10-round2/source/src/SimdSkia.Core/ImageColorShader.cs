using System.Numerics;
namespace SimdSkia;

/// <summary>Immutable raster snapshot sampler. Coordinates are pixel edges;
/// interpolation occurs on premultiplied channels before conversion to straight color.</summary>
internal sealed class ImageColorShader(PixelBuffer pixels,int tileX,int tileY,ImageSampling sampling,float cubicB=1f/3,float cubicC=1f/3,bool raw=false) : IPremultipliedColorShader,IImageShader,IPremultipliedBatchShader
{
    public bool TryGetImage(out ImageColorShader image,out Matrix3 inverse){image=this;inverse=Matrix3.Identity;return true;}
    internal bool IsNearest=>sampling==ImageSampling.Nearest;
    internal int Row(float y)=>float.IsFinite(y)?Index(Nearest(Reduce(y,pixels.Height,tileY)),pixels.Height,tileY):-1;
    internal void FillNearestRow(Span<byte> output,PixelFormat format,Matrix3 inverse,int x0,int row)
    {
        int ri=format==PixelFormat.Rgba8888?0:2;
        if(row<0){output.Clear();return;}
        for(int i=0;i<output.Length/4;i++)
        {
            float x=inverse.M00*(x0+i+.5f)+inverse.M02;var p=float.IsFinite(x)?Read(Nearest(Reduce(x,pixels.Width,tileX)),row):default;int at=i*4;
            output[at+ri]=ColorBlend.Byte(p.X);output[at+1]=ColorBlend.Byte(p.Y);output[at+2-ri]=ColorBlend.Byte(p.Z);output[at+3]=ColorBlend.Byte(p.W);
        }
    }
    public bool IsOpaque=>pixels.Alpha==AlphaFormat.Opaque&&tileX!=3&&tileY!=3;
    public Rgba32 Sample(float x,float y)=>Straight(SampleVector(x,y));
    public Vector4 SamplePremultiplied(float x,float y)=>SampleVector(x,y);
    void IPremultipliedBatchShader.SampleBatch(Span<Vector4> samples)
    {
        if(sampling!=ImageSampling.Nearest)
        {for(int i=0;i<samples.Length;i++)samples[i]=SampleVector(samples[i].X,samples[i].Y);return;}
        int previous=-1;ReadOnlySpan<byte> row=default;
        for(int i=0;i<samples.Length;i++)
        {
            float x=samples[i].X,y=samples[i].Y;
            if(!float.IsFinite(x)||!float.IsFinite(y)){samples[i]=default;continue;}
            int ix=Index(Nearest(Reduce(x,pixels.Width,tileX)),pixels.Width,tileX),iy=Row(y);
            if(ix<0||iy<0){samples[i]=default;continue;}
            if(iy!=previous){row=pixels.GetRowSpan(iy);previous=iy;}
            samples[i]=ReadRow(row,ix);
        }
    }
    private Vector4 SampleVector(float x,float y)
    {
        if(!float.IsFinite(x)||!float.IsFinite(y))return default;
        // Reduce before int conversion, also for very large finite coordinates.
        x=Reduce(x,pixels.Width,tileX);y=Reduce(y,pixels.Height,tileY);
        if(sampling==ImageSampling.Nearest)return Read(Nearest(x),Nearest(y));
        x-=.5f;y-=.5f;float ix=MathF.Floor(x),iy=MathF.Floor(y),fx=x-ix,fy=y-iy;
        Vector4 result;
        if(sampling==ImageSampling.Bilinear)result=Vector4.Lerp(Vector4.Lerp(Read(ix,iy),Read(ix+1,iy),fx),Vector4.Lerp(Read(ix,iy+1),Read(ix+1,iy+1),fx),fy);
        else
        {
            result=Vector4.Zero;
            for(int j=-1;j<=2;j++){float wy=Cubic(j-fy);for(int i=-1;i<=2;i++)result+=Read(ix+i,iy+j)*(Cubic(i-fx)*wy);}
            result=Vector4.Clamp(result,Vector4.Zero,Vector4.One);result.X=MathF.Min(result.X,result.W);result.Y=MathF.Min(result.Y,result.W);result.Z=MathF.Min(result.Z,result.W);
        }
        return result;
    }
    private float Cubic(float value)
    {
        float x=MathF.Abs(value),b=cubicB,c=cubicC;
        return x<1?((12-9*b-6*c)*x*x*x+(-18+12*b+6*c)*x*x+6-2*b)/6:x<2?((-b-6*c)*x*x*x+(6*b+30*c)*x*x+(-12*b-48*c)*x+8*b+24*c)/6:0;
    }
    private static float Reduce(float x,int length,int mode)=>mode switch{0=>Math.Clamp(x,-2,length+2),1=>x%length,2=>x%(2f*length),_=>Math.Clamp(x,-2,length+2)};
    // The pinned raster sampler assigns exact texel boundaries to the lower
    // texel. Keep that rule in both the scalar sampler and cached row path.
    internal static float Nearest(float value)=>MathF.Abs(value)<16_777_216?MathF.Ceiling(value)-1:MathF.Floor(MathF.BitDecrement(value));
    private static int Index(float value,int length,int mode)
    {
        if(mode==0)return(int)Math.Clamp(value,0,length-1);
        if(mode==3)return value<0||value>=length?-1:(int)value;
        int period=mode==2?checked(length*2):length;float v=value%period;if(v<0)v+=period;int i=(int)v;return mode==2&&i>=length?period-1-i:i;
    }
    private Vector4 Read(float x,float y)
    {
        int ix=Index(x,pixels.Width,tileX),iy=Index(y,pixels.Height,tileY);if(ix<0||iy<0)return default;
        return ReadRow(pixels.GetRowSpan(iy),ix);
    }
    private Vector4 ReadRow(ReadOnlySpan<byte> row,int ix)
    {
        if(pixels.Format==PixelFormat.Alpha8)return new(0,0,0,pixels.Alpha==AlphaFormat.Opaque?1:row[ix]/255f);
        var p=row.Slice(ix*4,4);float a=pixels.Alpha==AlphaFormat.Opaque?1:p[3]/255f;
        var c=new Vector4(p[pixels.Format==PixelFormat.Rgba8888?0:2]/255f,p[1]/255f,p[pixels.Format==PixelFormat.Rgba8888?2:0]/255f,a);
        if(!raw&&pixels.Alpha==AlphaFormat.Straight){c.X*=a;c.Y*=a;c.Z*=a;}return c;
    }
    private static Rgba32 Straight(Vector4 p)=>p.W<=0?default:new(ColorBlend.Byte(p.X/p.W),ColorBlend.Byte(p.Y/p.W),ColorBlend.Byte(p.Z/p.W),ColorBlend.Byte(p.W));
}

/// <summary>Optional shader contract avoids lossy unpremultiply/premultiply round trips.</summary>
public interface IPremultipliedColorShader : IColorShader
{
    Vector4 SamplePremultiplied(float x,float y);
}
internal interface IImageShader {bool TryGetImage(out ImageColorShader image,out Matrix3 inverse);}
internal interface IPremultipliedBatchShader {void SampleBatch(Span<Vector4> samples);}
internal sealed class PremultipliedTransformedShader(IPremultipliedColorShader shader,Matrix3 inverse) : IPremultipliedColorShader,IImageShader,IPremultipliedBatchShader
{
    internal IPremultipliedColorShader ForDevice(Matrix3 deviceToLocal)
    {var combined=inverse*deviceToLocal;return shader is PremultipliedTransformedShader nested?nested.ForDevice(combined):new DeviceShader(shader,combined);}
    private IColorShader? managed;
    public IColorShader ForDestination(bool taggedSrgb)
    {if(!taggedSrgb)return this;var resolved=shader.ForDestination(true);return ReferenceEquals(resolved,shader)?this:managed??=new PremultipliedTransformedShader((IPremultipliedColorShader)resolved,inverse);}
    public bool TryGetImage(out ImageColorShader image,out Matrix3 transform)
    {if(shader is IImageShader raster&&raster.TryGetImage(out image,out var child)){transform=child*inverse;return true;}image=null!;transform=default;return false;}
    public bool IsOpaque=>shader.IsOpaque;
    public Rgba32 Sample(float x,float y){var p=inverse.Map(x,y);return shader.Sample(p.X,p.Y);}
    public Vector4 SamplePremultiplied(float x,float y){var p=inverse.Map(x,y);return shader.SamplePremultiplied(p.X,p.Y);}
    void IPremultipliedBatchShader.SampleBatch(Span<Vector4> samples)=>SampleTransformedBatch(shader,inverse,samples);
    private static void SampleTransformedBatch(IPremultipliedColorShader source,Matrix3 matrix,Span<Vector4> samples)
    {
        for(int i=0;i<samples.Length;i++){var p=matrix.Map(samples[i].X,samples[i].Y);samples[i]=new(p.X,p.Y,0,0);}
        if(source is IPremultipliedBatchShader batch)batch.SampleBatch(samples);
        else for(int i=0;i<samples.Length;i++)samples[i]=source.SamplePremultiplied(samples[i].X,samples[i].Y);
    }
    private sealed class DeviceShader(IPremultipliedColorShader source,Matrix3 matrix):IPremultipliedColorShader,IImageShader,IPremultipliedBatchShader
    {
        public bool IsOpaque=>source.IsOpaque;
        public bool IsHorizontal=>source.IsHorizontal&&matrix.M01==0;
        public Vector4 SamplePremultiplied(float x,float y){var p=matrix.Map(x,y);return source.SamplePremultiplied(p.X,p.Y);}
        void IPremultipliedBatchShader.SampleBatch(Span<Vector4> samples)=>SampleTransformedBatch(source,matrix,samples);
        public Rgba32 Sample(float x,float y){var p=SamplePremultiplied(x,y);return p.W>0?new(ColorBlend.Byte(p.X/p.W),ColorBlend.Byte(p.Y/p.W),ColorBlend.Byte(p.Z/p.W),ColorBlend.Byte(p.W)):default;}
        public bool TryGetImage(out ImageColorShader image,out Matrix3 inverse)
        {if(source is IImageShader raster&&raster.TryGetImage(out image,out var child)){inverse=child*matrix;return true;}image=null!;inverse=default;return false;}
    }
}
