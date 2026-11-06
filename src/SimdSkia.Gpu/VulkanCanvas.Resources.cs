using System.Diagnostics;
namespace SimdSkia.Gpu;

internal readonly record struct BorrowedVulkanContext(IntPtr Instance,IntPtr PhysicalDevice,IntPtr Device,IntPtr Queue,uint QueueFamily);
public sealed unsafe partial class VulkanCanvas:IGpuCanvas
{
    bool IGpuCanvas.ContextAbandoned=>ContextAbandoned;
    bool IGpuCanvas.ResourceDisposed=>ResourceDisposed;
    int IGpuCanvas.MaximumImageSize=>MaximumImageSize;
    (int Count,long Bytes,long Purgeable) IGpuCanvas.ResourceUsage=>ResourceUsage;
    void IGpuCanvas.ConfigureResourceCache(long limit,bool pathMasks)=>ConfigureResourceCache(limit,pathMasks);
    void IGpuCanvas.PurgeCachedResources(long bytesToPurge,long minimumAgeMilliseconds,bool scratchOnly)=>PurgeCachedResources(bytesToPurge,minimumAgeMilliseconds,scratchOnly);
    void IGpuCanvas.Abandon(bool releaseResources)=>Abandon(releaseResources);
    private long cacheLimit=96L*1024*1024;
    private bool cachePathMasks=true;
    internal BorrowedVulkanContext ContextHandles {get{Validate();return new(instance,physical,device,queue,queueFamily);}}
    internal bool ContextAbandoned=>abandoned||faulted;
    internal bool ResourceDisposed=>disposed;
    internal int MaximumImageSize {get;private set;}=32768;
    internal void ConfigureResourceCache(long limit,bool pathMasks)
    {Validate();cacheLimit=Math.Max(0,limit);cachePathMasks=pathMasks;if(!pathMasks)PurgeCachedResources(scratchOnly:true);TrimResourceCache();}
    internal (int Count,long Bytes,long Purgeable) ResourceUsage
    {
        get
        {
            Validate();int count=0;long bytes=0,purgeable=0;
            void Buffer(GpuBuffer? buffer){if(buffer?.Memory is >0){count++;bytes+=(long)buffer.AllocationSize;}}
            void TextureBytes(Texture? texture){if(texture?.Memory is >0){count++;bytes+=(long)texture.AllocationSize;Buffer(texture.Staging);}}
            Buffer(output);Buffer(readback);Buffer(empty);Buffer(edgesArena);Buffer(paramsArena);Buffer(deviceEdgesArena);Buffer(deviceParamsArena);Buffer(hostUpload);TextureBytes(emptyTexture);
            foreach(var item in imageCache.Values){TextureBytes(item.Texture);purgeable+=(long)item.Texture.AllocationSize;}
            foreach(var item in retiredTextures)TextureBytes(item);
            foreach(var item in maskCache.Values){Buffer(item.Buffer);purgeable+=(long)(item.Buffer?.AllocationSize??0);}
            return(count,bytes,purgeable);
        }
    }
    internal void PurgeCachedResources(long bytesToPurge=long.MaxValue,long minimumAgeMilliseconds=0,bool scratchOnly=false)
    {
        Validate();if(bytesToPurge<=0)return;Flush();long now=Stopwatch.GetTimestamp(),age=(long)(Math.Max(0,minimumAgeMilliseconds)*(double)Stopwatch.Frequency/1000),freed=0;
        // Completed cached masks are scratch data; immutable image textures are
        // reusable content. Neither can be destroyed while a batch references it.
        foreach(var pair in maskCache.OrderBy(p=>p.Value.Used).ToArray())
        {
            if(now-pair.Value.Touched<age)continue;var entry=pair.Value;freed+=(long)(entry.Buffer?.AllocationSize??0);entry.Buffer?.Dispose();maskBytes-=entry.Bytes;maskCache.Remove(pair.Key);if(freed>=bytesToPurge)return;
        }
        if(!scratchOnly)foreach(var pair in imageCache.OrderBy(p=>p.Value.Used).ToArray())
        {if(now-pair.Value.Touched<age)continue;freed+=(long)pair.Value.Texture.AllocationSize;pair.Value.Texture.Dispose();imageCache.Remove(pair.Key);if(freed>=bytesToPurge)return;}
    }
    private void TrimResourceCache()
    {
        if(pending.Count!=0)return;
        long imageBytes=0,maskGpuBytes=0;
        foreach(var entry in imageCache.Values)imageBytes+=(long)entry.Texture.AllocationSize;
        foreach(var entry in maskCache.Values)maskGpuBytes+=(long)(entry.Buffer?.AllocationSize??0);
        long excess=imageBytes+maskGpuBytes-cacheLimit;
        if(excess>0)PurgeCachedResources(excess);
    }
    internal void Abandon(bool releaseResources)
    {
        if(disposed||abandoned)return;if(Environment.CurrentManagedThreadId!=ownerThread)throw new InvalidOperationException("Release the Vulkan surface on its creating thread.");
        if(releaseResources){if(!faulted)Flush();Dispose();return;}
        // The device may already be lost. Do not call its driver during abandon;
        // the caller owns the borrowed VkDevice and must eventually destroy it.
        if(borrowedContext is null)throw new InvalidOperationException("Only externally owned Vulkan devices may be abandoned without releasing their resources.");
        pending.Clear();abandoned=true;faulted=true;
    }
}
