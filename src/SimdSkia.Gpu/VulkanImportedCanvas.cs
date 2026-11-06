namespace SimdSkia.Gpu;

// External images are copied to/from the compute output buffer on the device.
// The CPU bridge remains an explicit test oracle; default drawing never uploads
// the rendered CPU shadow back to VkImage. Public readback still completes.
internal sealed class VulkanImportedCanvas:IGpuCanvas
{
    private readonly PixelBuffer pixels;
    private readonly VulkanCanvas drawing;
    private readonly VulkanImage image;
    private bool dirty,disposed,abandoned;
    private readonly bool reference=AppContext.TryGetSwitch("SimdSkia.Vulkan.ExternalCpuBridge",out bool enabled)&&enabled;
    internal VulkanImportedCanvas(PixelBuffer pixels,VulkanImage image)
    {
        this.pixels=pixels.Share();this.image=image;
        try{drawing=new(pixels,true,image.Device.Handles);if(reference){using var initial=image.ReadPixels();for(int y=0;y<pixels.Height;y++)initial.GetRowSpan(y).CopyTo(pixels.GetRowSpan(y));drawing.Upload();}else drawing.AttachExternalImage(image);}
        catch{this.pixels.Dispose();image.Dispose();throw;}
    }
    public bool ContextAbandoned=>abandoned||drawing.ContextAbandoned;
    public bool ResourceDisposed=>disposed;
    public int MaximumImageSize=>drawing.MaximumImageSize;
    public (int Count,long Bytes,long Purgeable) ResourceUsage=>disposed||abandoned?default:drawing.ResourceUsage;
    public void Execute(in DrawingCommand command){if(abandoned)throw new InvalidOperationException("The imported Vulkan surface is abandoned.");drawing.Execute(command);dirty=true;}
    public void Flush(){if(abandoned)throw new InvalidOperationException("The imported Vulkan surface is abandoned.");drawing.Flush();if(reference&&dirty)image.Device.Write(image,pixels);dirty=false;}
    internal bool ReadTo(Span<byte> destination,int rowBytes){if(reference)return false;if(abandoned)throw new InvalidOperationException("The imported Vulkan surface is abandoned.");drawing.ReadTo(destination,rowBytes);dirty=false;return true;}
    public void ConfigureResourceCache(long limit,bool pathMasks)=>drawing.ConfigureResourceCache(limit,pathMasks);
    public void PurgeCachedResources(long bytesToPurge=long.MaxValue,long minimumAgeMilliseconds=0,bool scratchOnly=false)=>drawing.PurgeCachedResources(bytesToPurge,minimumAgeMilliseconds,scratchOnly);
    public void Abandon(bool releaseResources){if(disposed||abandoned)return;if(releaseResources)Flush();drawing.Abandon(releaseResources);abandoned=true;}
    public void Dispose(){if(disposed)return;try{if(!abandoned&&!drawing.IsFaulted)Flush();}finally{drawing.Dispose();image.Dispose();pixels.Dispose();disposed=true;}}
}
