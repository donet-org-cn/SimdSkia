using SimdSkia.Cpu;
namespace SimdSkia.Gpu;

// Device Gaussian blur and drop shadow on shared layer pixels. The blur uses
// the CPU filter's integer kernel and transparent extension, so its bytes are
// identical to ManagedFilters.Blur. The shadow colorization uses the same
// PixelBuffer.Multiply rounding; the final source-over is float (±1 LSB).
internal sealed unsafe partial class MetalDevice
{
    private readonly Dictionary<float,nint> weights=[];
    private nint Weights(float sigma)
    {
        if(weights.TryGetValue(sigma,out var buffer))return buffer;
        var kernel=ManagedFilters.Kernel(sigma);
        fixed(int* values=kernel)buffer=Upload(new ReadOnlySpan<byte>(values,kernel.Length*4));
        if(weights.Count>=64)ReleaseWeights();
        weights.Add(sigma,buffer);return buffer;
    }
    private void ReleaseWeights(){foreach(var value in weights.Values){nint buffer=value;MetalApi.Release(ref buffer);}weights.Clear();}

    /// <summary>Filters shared layer pixels into new shared pixels, or returns
    /// null when the layer is not device memory. <paramref name="region"/> is the
    /// drawn area of the layer; it is expanded by the Gaussian halo exactly as
    /// the CPU effect path does.</summary>
    internal PixelBuffer? Filter(PixelBuffer layer,RectF region,float sigmaX,float sigmaY,float offsetX,float offsetY,Rgba32 color,bool shadow)
    {
        Check();
        if(!TryGetShared(layer,out nint source))return null;
        if(!float.IsFinite(sigmaX)||!float.IsFinite(sigmaY)||sigmaX<0||sigmaY<0||sigmaX>256||sigmaY>256||!float.IsFinite(offsetX)||!float.IsFinite(offsetY)||!region.IsFinite)return null;
        int width=layer.Width,height=layer.Height;
        // FilterSpec.Apply: blur bounds are the drawn region plus ceil(3σ)+1.
        float haloX=MathF.Ceiling(sigmaX*3)+1,haloY=MathF.Ceiling(sigmaY*3)+1;
        int left=(int)Math.Clamp(MathF.Floor(region.Left-haloX),0,width),right=(int)Math.Clamp(MathF.Ceiling(region.Right+haloX),0,width);
        int top=(int)Math.Clamp(MathF.Floor(region.Top-haloY),0,height),bottom=(int)Math.Clamp(MathF.Ceiling(region.Bottom+haloY),0,height);
        nint xWeights=Weights(sigmaX),yWeights=Weights(sigmaY);
        int rx=(int)Math.Ceiling(sigmaX*3),ry=(int)Math.Ceiling(sigmaY*3);if(sigmaX==0)rx=0;if(sigmaY==0)ry=0;
        PixelBuffer? horizontal=RentShared(width,height,false),blurred=null,result=null;
        try
        {
            blurred=RentShared(width,height,false);
            TryGetShared(horizontal,out nint h);TryGetShared(blurred,out nint b);
            int* p=stackalloc int[12];
            p[0]=left;p[1]=top;p[2]=right;p[3]=bottom;p[4]=width;p[5]=height;p[6]=width;p[7]=0;p[8]=rx;p[9]=ry;p[10]=0;p[11]=0;
            Use(blurHorizontal);var encoder=Compute();
            MetalApi.SetBuffer(encoder,source,0);MetalApi.SetBuffer(encoder,h,1);MetalApi.SetBuffer(encoder,xWeights,2);MetalApi.SetBytes(encoder,p,48,3);MetalApi.Dispatch(encoder,(nuint)width,(nuint)height);
            Use(blurVertical);
            MetalApi.SetBuffer(encoder,h,0);MetalApi.SetBuffer(encoder,b,1);MetalApi.SetBuffer(encoder,yWeights,2);MetalApi.SetBytes(encoder,p,48,3);MetalApi.Dispatch(encoder,(nuint)width,(nuint)height);
            if(!shadow){result=blurred;blurred=null;return result;}
            // The horizontal pass is consumed: its buffer receives the shadow.
            result=horizontal;horizontal=null!;TryGetShared(result,out nint output);
            bool integral=offsetX==MathF.Truncate(offsetX)&&offsetY==MathF.Truncate(offsetY);
            int* s=stackalloc int[12];s[0]=width;s[1]=height;s[2]=width;s[3]=integral?1:0;
            float* offset=(float*)(s+4);offset[0]=offsetX;offset[1]=offsetY;offset[2]=0;offset[3]=0;
            uint* rgba=(uint*)(s+8);rgba[0]=color.R;rgba[1]=color.G;rgba[2]=color.B;rgba[3]=color.A;
            Use(dropShadow);
            MetalApi.SetBuffer(encoder,b,0);MetalApi.SetBuffer(encoder,source,1);MetalApi.SetBuffer(encoder,output,2);MetalApi.SetBytes(encoder,s,48,3);MetalApi.Dispatch(encoder,(nuint)width,(nuint)height);
            var done=result;result=null;return done;
        }
        finally{horizontal?.Dispose();blurred?.Dispose();result?.Dispose();}
    }
}
