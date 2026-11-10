using SimdSkia;
using SimdSkia.Gpu;
namespace SkiaSharp;

public class GRRecordingContext : SKObject
{
    internal GRRecordingContext() { }
    internal virtual GRContext Direct=>throw new InvalidOperationException("This recording context has no direct device.");
    public virtual GRBackend Backend=>Direct.Backend;
    public virtual bool IsAbandoned=>Direct.IsAbandoned;
    public int MaxRenderTargetSize {get{Direct.ValidateContext();return Direct.DeviceMaximumSize;}}
    public int MaxTextureSize {get{Direct.ValidateContext();return Direct.DeviceMaximumSize;}}
    public int GetMaxSurfaceSampleCount(SKColorType colorType){Direct.ValidateContext();return colorType is SKColorType.Rgba8888 or SKColorType.Bgra8888?1:0;}
}
public partial class GRContext : GRRecordingContext
{
    private readonly int thread=Environment.CurrentManagedThreadId;
    private readonly Func<PixelBuffer,IGpuCanvas> createCanvas;
    private readonly IDisposable? deviceOwner;
    private readonly GRBackend backend;
    private readonly List<WeakReference<IGpuCanvas>> surfaces=[];
    private readonly IGpuCanvas probe;
    private readonly bool pathMasks;
    private long limit=96L*1024*1024;
    private bool abandoned;
    internal int DeviceMaximumSize=>probe.MaximumImageSize;
    internal VulkanTransfer VulkanTransfer {get{ValidateContext();return deviceOwner as VulkanTransfer??throw new InvalidOperationException("This context is not Vulkan.");}}
    private GRContext(GRBackend backend,Func<PixelBuffer,IGpuCanvas> createCanvas,IDisposable? deviceOwner,GRContextOptions? options)
    {
        this.backend=backend;this.createCanvas=createCanvas;this.deviceOwner=deviceOwner;pathMasks=options?.AllowPathMaskCaching??true;
        using var pixels=new PixelBuffer(1,1,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);probe=createCanvas(pixels);try{probe.ConfigureResourceCache(limit,pathMasks);Register(probe);}catch{probe.Dispose();throw;}
    }
    internal override GRContext Direct=>this;
    internal void ValidateContext()
    {
        ThrowIfDisposed();if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException($"Use this {backend} context on its creating thread; device access requires external synchronization.");
        if(abandoned||probe.ContextAbandoned)throw new InvalidOperationException($"The {backend} context has been abandoned or lost.");
    }
    public override GRBackend Backend {get{ThrowIfDisposed();return backend;}}
    public override bool IsAbandoned {get{ThrowIfDisposed();return abandoned||probe.ContextAbandoned;}}
    public static GRContext CreateVulkan(GRVkBackendContext backendContext)=>CreateVulkan(backendContext,null!);
    public static GRContext CreateVulkan(GRVkBackendContext backendContext,GRContextOptions? options)
    {
        ArgumentNullException.ThrowIfNull(backendContext);
        if(backendContext.ProtectedContext)throw new NotSupportedException("Protected Vulkan memory and queues are not supported.");
        if(options is not null&&(options.DoManualMipmapping||options.RuntimeProgramCacheSize!=256||options.GlyphCacheTextureMaximumBytes!=8*1024*1024||options.BufferMapThreshold!=-1))throw new NotSupportedException("Custom program/glyph cache, mipmapping and map-threshold options are not yet supported by this Vulkan backend.");
        var handles=new BorrowedVulkanContext(backendContext.VkInstance,backendContext.VkPhysicalDevice,backendContext.VkDevice,backendContext.VkQueue,backendContext.GraphicsQueueIndex);var transfer=new VulkanTransfer(handles);
        try{return new(GRBackend.Vulkan,pixels=>new VulkanCanvas(pixels,true,handles),transfer,options);}catch{transfer.Dispose();throw;}
    }
    private IEnumerable<IGpuCanvas> Live()
    {for(int i=surfaces.Count-1;i>=0;i--){if(surfaces[i].TryGetTarget(out var value)&&!value.ResourceDisposed)yield return value;else surfaces.RemoveAt(i);}}
    internal void Register(IGpuCanvas canvas)=>surfaces.Add(new(canvas));
    internal IGpuCanvas CreateCanvas(PixelBuffer pixels)
    {ValidateContext();var result=createCanvas(pixels);try{result.ConfigureResourceCache(limit,pathMasks);Register(result);return result;}catch{result.Dispose();throw;}}
    public void AbandonContext(bool releaseResources=false)
    {
        if(IsAbandoned)return;ValidateContext();
        // Invalidate borrowed GL objects before releasing in-flight image leases
        // when the native context may no longer be current.
        if(!releaseResources&&deviceOwner is GlDevice lostGl){ValidateContext();lostGl.AbandonImages(false);lostGl.Abandoned=true;}
        foreach(var canvas in Live())canvas.Abandon(releaseResources);if(deviceOwner is GlDevice gl){gl.AbandonImages(releaseResources);if(!releaseResources)gl.Abandoned=true;}if(deviceOwner is VulkanTransfer transfer)transfer.Abandon(releaseResources);if(OperatingSystem.IsWindows()&&deviceOwner is SimdSkia.Gpu.Direct2D.D3D12Device d3d)d3d.Abandon(releaseResources);if(deviceOwner is MetalDevice metal){metal.ReleaseImages();metal.Abandoned=true;}abandoned=true;
    }
    public long GetResourceCacheLimit(){ValidateContext();return limit;}
    public void SetResourceCacheLimit(long maxResourceBytes)
    {ValidateContext();if(maxResourceBytes<0)throw new ArgumentOutOfRangeException(nameof(maxResourceBytes));limit=maxResourceBytes;foreach(var canvas in Live())canvas.ConfigureResourceCache(limit,pathMasks);EnforceLimit();}
    public void GetResourceCacheUsage(out int maxResources,out long maxResourceBytes)
    {ValidateContext();maxResources=0;maxResourceBytes=0;foreach(var canvas in Live()){var usage=canvas.ResourceUsage;maxResources+=usage.Count;maxResourceBytes+=usage.Bytes;}}
    private void EnforceLimit()
    {long bytes=Live().Sum(c=>c.ResourceUsage.Purgeable);foreach(var canvas in Live()){if(bytes<=limit)break;long old=canvas.ResourceUsage.Purgeable;canvas.PurgeCachedResources(bytes-limit);bytes-=old-canvas.ResourceUsage.Purgeable;}}
    public void ResetContext(GRGlBackendState state)=>ResetContext((uint)state);
    public void ResetContext(GRBackendState state=GRBackendState.All)=>ResetContext((uint)state);
    public void ResetContext(uint state)
    {ValidateContext();foreach(var canvas in Live())canvas.Flush();/* Every compute dispatch binds its entire pipeline/descriptors; no implicit GL state is cached. */}
    public void Flush()=>Flush(true);
    public void Flush(bool submit,bool synchronous=false)
    {ValidateContext();if(submit){foreach(var canvas in Live())canvas.Flush();EnforceLimit();}}
    public void Submit(bool synchronous=false)=>Flush(true,synchronous);
    public new int GetMaxSurfaceSampleCount(SKColorType colorType)=>base.GetMaxSurfaceSampleCount(colorType);
    public void PurgeResources(){ValidateContext();foreach(var canvas in Live())canvas.PurgeCachedResources();}
    public void PurgeUnusedResources(long milliseconds){ValidateContext();if(milliseconds<0)throw new ArgumentOutOfRangeException(nameof(milliseconds));foreach(var canvas in Live())canvas.PurgeCachedResources(minimumAgeMilliseconds:milliseconds);}
    public void PurgeUnlockedResources(bool scratchResourcesOnly){ValidateContext();foreach(var canvas in Live())canvas.PurgeCachedResources(scratchOnly:scratchResourcesOnly);}
    public void PurgeUnlockedResources(long bytesToPurge,bool preferScratchResources)
    {
        ValidateContext();if(bytesToPurge<0)throw new ArgumentOutOfRangeException(nameof(bytesToPurge));long remaining=bytesToPurge;
        if(preferScratchResources)foreach(var canvas in Live()){long old=canvas.ResourceUsage.Purgeable;canvas.PurgeCachedResources(remaining,scratchOnly:true);remaining-=old-canvas.ResourceUsage.Purgeable;}
        foreach(var canvas in Live()){if(remaining<=0)break;long old=canvas.ResourceUsage.Purgeable;canvas.PurgeCachedResources(remaining);remaining-=old-canvas.ResourceUsage.Purgeable;}
    }
    public void DumpMemoryStatistics(SKTraceMemoryDump dump)
    {ValidateContext();ArgumentNullException.ThrowIfNull(dump);dump.ThrowIfDisposed();int i=0;foreach(var canvas in Live()){var usage=canvas.ResourceUsage;dump.Numeric($"SimdSkia/{backend}/{i}","size","bytes",(ulong)usage.Bytes);dump.Numeric($"SimdSkia/{backend}/{i}","purgeable_size","bytes",(ulong)usage.Purgeable);dump.Text($"SimdSkia/{backend}/{i++}","backend",backend.ToString());}}
    protected override void Dispose(bool disposing)
    {if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException($"Dispose the {backend} context on its creating thread.");if(!abandoned)foreach(var canvas in Live())canvas.Abandon(true);try{probe.Dispose();}finally{deviceOwner?.Dispose();surfaces.Clear();abandoned=true;}}
}
public class SKTraceMemoryDump : SKObject
{
    protected SKTraceMemoryDump(bool detailedDump,bool dumpWrappedObjects) {Detailed=detailedDump;Wrapped=dumpWrappedObjects;}
    internal bool Detailed {get;}
    internal bool Wrapped {get;}
    internal void Numeric(string dump,string name,string units,ulong value)=>OnDumpNumericValue(dump,name,units,value);
    internal void Text(string dump,string name,string value)=>OnDumpStringValue(dump,name,value);
    protected internal virtual void OnDumpNumericValue(string dumpName,string valueName,string units,ulong value) { }
    protected internal virtual void OnDumpStringValue(string dumpName,string valueName,string value) { }
}
