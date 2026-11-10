using SimdSkia;
using SimdSkia.Gpu;
namespace SkiaSharp;
public partial class SKImage
{
    internal static VulkanImageDescription VulkanDescription(GRVkImageInfo info,bool adopted)
    {
        if(info.Protected||info.SampleCount!=1||info.Format is not (37 or 44)||info.LevelCount<1||info.YcbcrConversionInfo!=default)throw new NotSupportedException("Only unprotected, single-sample RGBA/BGRA8888 Vulkan resources without YCbCr conversion are implemented.");
        if(adopted&&(info.Alloc.Offset!=0||info.Alloc.BackendMemory!=0))throw new NotSupportedException("Adopted Vulkan images require dedicated raw VkDeviceMemory; Skia/custom allocator blocks cannot be adopted.");
        return new(info.Image,info.Alloc.Memory,info.Alloc.Size,info.Format,info.ImageLayout,info.ImageUsageFlags,info.LevelCount,info.CurrentQueueFamily);
    }
    private static SKImage ImportVulkanTexture(GRRecordingContext context,GRBackendTexture texture,GRSurfaceOrigin origin,SKColorType colorType,SKAlphaType alpha,SKColorSpace? colorspace,bool adopted,SKImageTextureReleaseDelegate? releaseProc,object? releaseContext)
    {
        var info=texture.VulkanInfo;var description=VulkanDescription(info,adopted);if((info.Format==44)!=(colorType==SKColorType.Bgra8888))return null!;
        var image=context.Direct.VulkanTransfer.Import(description,texture.Width,texture.Height,origin==GRSurfaceOrigin.BottomLeft,alpha==SKAlphaType.Opaque?AlphaFormat.Opaque:alpha==SKAlphaType.Unpremul?AlphaFormat.Straight:AlphaFormat.Premultiplied,adopted,releaseProc is null?null:()=>releaseProc(releaseContext!));
        return FromGlSnapshot(new(texture.Width,texture.Height,colorType,alpha,colorspace),image);
    }
}
