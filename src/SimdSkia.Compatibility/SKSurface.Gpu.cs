using SimdSkia.Gpu;
namespace SkiaSharp;

public partial class SKCanvas
{
    public GRRecordingContext? Context {get{Validate();return OwnerSurface?.Context;}}
}
public partial class SKSurface
{
    private SKSurface(SKBitmap bitmap,GRContext context,IGpuCanvas backend)
    {this.bitmap=bitmap;gpuContext=context;glCanvas=backend as GlCanvas;vulkanCanvas=backend as VulkanCanvas;importedVulkanCanvas=backend as VulkanImportedCanvas;canvas=new(bitmap.Buffer,backend){OwnerSurface=this,ColorManagementEnabled=bitmap.ColorSpace is not null};}
    public GRRecordingContext? Context {get{ThrowIfDisposed();return gpuContext;}}
    public void Flush(bool submit,bool synchronous=false)
    {ThrowIfDisposed();if(gpuContext is null||submit)canvas.Flush();}
    private static SKSurface CreateGpu(GRRecordingContext context,bool budgeted,SKImageInfo info,int samples,GRSurfaceOrigin origin,SKSurfaceProperties? props,bool mips)
    {
        ArgumentNullException.ThrowIfNull(context);var direct=context.Direct;direct.ValidateContext();
        if(mips)throw new NotSupportedException("GPU mipmapped render targets have not been implemented.");
        if(samples is <0 or >1)throw new NotSupportedException("This GPU pipeline has a single-sample target with analytical/supersampled coverage; MSAA attachments are not implemented.");
        if(!Enum.IsDefined(origin))throw new ArgumentOutOfRangeException(nameof(origin));
        if(info.IsEmpty)return null!;
        if(info.ColorSpace is {} space&&!space.IsSrgb)throw new NotSupportedException("Non-sRGB GPU targets require a destination color pipeline.");
        if(info.AlphaType==SKAlphaType.Unpremul)throw new NotSupportedException("GPU drawing requires premultiplied/opaque storage.");
        var bitmap=new SKBitmap(info);IGpuCanvas? backend=null;
        try{backend=direct.CreateCanvas(bitmap.Buffer);return new SKSurface(bitmap,direct,backend).SetProperties(props);}
        catch{backend?.Dispose();bitmap.Dispose();throw;}
    }
    public static SKSurface Create(GRContext context,bool budgeted,SKImageInfo info)=>CreateGpu(context,budgeted,info,0,GRSurfaceOrigin.TopLeft,null,false);
    public static SKSurface Create(GRContext context,bool budgeted,SKImageInfo info,SKSurfaceProperties props)=>CreateGpu(context,budgeted,info,0,GRSurfaceOrigin.TopLeft,props,false);
    public static SKSurface Create(GRContext context,bool budgeted,SKImageInfo info,int sampleCount)=>CreateGpu(context,budgeted,info,sampleCount,GRSurfaceOrigin.TopLeft,null,false);
    public static SKSurface Create(GRContext context,bool budgeted,SKImageInfo info,int sampleCount,SKSurfaceProperties props)=>CreateGpu(context,budgeted,info,sampleCount,GRSurfaceOrigin.TopLeft,props,false);
    public static SKSurface Create(GRContext context,bool budgeted,SKImageInfo info,int sampleCount,GRSurfaceOrigin origin)=>CreateGpu(context,budgeted,info,sampleCount,origin,null,false);
    public static SKSurface Create(GRContext context,bool budgeted,SKImageInfo info,int sampleCount,GRSurfaceOrigin origin,SKSurfaceProperties props,bool shouldCreateWithMips)=>CreateGpu(context,budgeted,info,sampleCount,origin,props,shouldCreateWithMips);
    public static SKSurface Create(GRRecordingContext context,bool budgeted,SKImageInfo info)=>CreateGpu(context,budgeted,info,0,GRSurfaceOrigin.TopLeft,null,false);
    public static SKSurface Create(GRRecordingContext context,bool budgeted,SKImageInfo info,SKSurfaceProperties props)=>CreateGpu(context,budgeted,info,0,GRSurfaceOrigin.TopLeft,props,false);
    public static SKSurface Create(GRRecordingContext context,bool budgeted,SKImageInfo info,int sampleCount)=>CreateGpu(context,budgeted,info,sampleCount,GRSurfaceOrigin.TopLeft,null,false);
    public static SKSurface Create(GRRecordingContext context,bool budgeted,SKImageInfo info,int sampleCount,SKSurfaceProperties props)=>CreateGpu(context,budgeted,info,sampleCount,GRSurfaceOrigin.TopLeft,props,false);
    public static SKSurface Create(GRRecordingContext context,bool budgeted,SKImageInfo info,int sampleCount,GRSurfaceOrigin origin)=>CreateGpu(context,budgeted,info,sampleCount,origin,null,false);
    public static SKSurface Create(GRRecordingContext context,bool budgeted,SKImageInfo info,int sampleCount,GRSurfaceOrigin origin,SKSurfaceProperties props,bool shouldCreateWithMips)=>CreateGpu(context,budgeted,info,sampleCount,origin,props,shouldCreateWithMips);
}
