using System.Diagnostics;
using SimdSkia.Cpu;
namespace SimdSkia.Gpu;

// Device-wide upload caches. Root and layer canvases of a window share them,
// so glyph atlases and repeated coverage masks are uploaded once per device
// rather than once per offscreen layer. Entries are reused only on the
// device's creating thread; evicted buffers stay alive while in-flight work
// retains them.
internal sealed unsafe class MetalResources
{
    // Keyed by the stored bytes and their shape: integer-offset views of one
    // mask (a moving shape, or one drawn into a layer) share one upload.
    private readonly record struct MaskKey(object Storage,int Width,int Height,int RepeatStart,int ExtraRows);
    private readonly Dictionary<MaskKey,MaskEntry> masks=[];
    /// <summary>Path coverage shared by all canvases of the device.</summary>
    internal GpuCoverageCache Coverage {get;}=new();
    private readonly Dictionary<PixelBuffer,ImageEntry> images=new(ReferenceEqualityComparer.Instance);
    private readonly record struct MaskEntry(nint Buffer,int Bytes,long Touched,long Frame);
    private readonly record struct ImageEntry(nint Buffer,byte[]? Snapshot,int Bytes,long Touched);
    private long maskBytes,imageBytes;
    internal long CacheLimit {get;private set;}=32*1024*1024;
    internal bool CacheMasks {get;private set;}=true;
    internal int Count=>masks.Count+images.Count;
    internal long Bytes=>maskBytes+imageBytes;
    internal void Configure(long limit,bool pathMasks){CacheLimit=limit;CacheMasks=pathMasks;Coverage.Configure(limit,pathMasks);if(!pathMasks||Bytes>limit)Purge();}

    internal nint Mask(MetalDevice device,CoverageClip clip,out bool temporary)
    {
        temporary=false;var key=new MaskKey(clip.StorageIdentity,clip.Width,clip.Height,clip.RepeatStart,clip.ExtraRows);
        if(masks.TryGetValue(key,out var entry)){masks[key]=entry with{Touched=Stopwatch.GetTimestamp(),Frame=frame};return entry.Buffer;}
        // Rows expand straight into the shared buffer: no managed staging copy.
        int length=checked(clip.Width*clip.Height);nint buffer=device.CreateBuffer(length);
        try{var target=new Span<byte>((void*)MetalApi.Call(buffer,"contents"),length);for(int y=0;y<clip.Height;y++)clip.GetRow(clip.Top+y).CopyTo(target.Slice(y*clip.Width,clip.Width));}
        catch{MetalApi.Release(ref buffer);throw;}
        if(!CacheMasks||length>CacheLimit){temporary=true;return buffer;}
        if(masks.Count>=MaxMasks)EvictMasks(MaxMasks/4);
        if(Bytes+length>CacheLimit)Purge(Bytes+length-CacheLimit/2);
        masks.Add(key,new(buffer,length,Stopwatch.GetTimestamp(),frame));maskBytes+=length;return buffer;
    }

    internal nint Image(MetalDevice device,PixelBuffer source,bool immutable,out bool temporary)
    {
        temporary=false;
        if(images.TryGetValue(source,out var entry))
        {
            if(!immutable&&(entry.Snapshot is null||!source.GetPixelSpan().SequenceEqual(entry.Snapshot)))
            {
                // Earlier encoders retain their Metal buffer; only the cache
                // entry moves to the new upload.
                nint updated=Upload(device,source);nint previous=entry.Buffer;
                entry=entry with{Buffer=updated,Snapshot=source.CopyBytes()};
                MetalApi.Release(ref previous);
            }
            images[source]=entry with{Touched=Stopwatch.GetTimestamp()};
            return entry.Buffer;
        }
        int length=checked(source.Width*source.Height*4);nint buffer=Upload(device,source);
        if(length>CacheLimit){temporary=true;return buffer;}
        try
        {
            if(images.Count>=256)EvictImages(64);
            if(Bytes+length>CacheLimit)Purge(Bytes+length-CacheLimit/2);
            images.Add(source,new(buffer,immutable?null:source.CopyBytes(),length,Stopwatch.GetTimestamp()));
            imageBytes+=length;return buffer;
        }
        catch{MetalApi.Release(ref buffer);throw;}
    }
    internal static nint Upload(MetalDevice device,PixelBuffer source)
    {
        int length=checked(source.Width*source.Height*4);nint buffer=device.CreateBuffer(length);
        try{PixelTransferKernel.CopyToPremultiplied(source,new Span<byte>((void*)MetalApi.Call(buffer,"contents"),length),PixelFormat.Rgba8888);return buffer;}
        catch{MetalApi.Release(ref buffer);throw;}
    }

    internal void Purge(long bytesToPurge=long.MaxValue,long minimumAgeMilliseconds=0,bool scratchOnly=false)
    {
        if(bytesToPurge<=0)return;long freed=0;
        foreach(var item in masks.OrderBy(p=>p.Value.Touched).ToArray())
        {
            if(Stopwatch.GetElapsedTime(item.Value.Touched).TotalMilliseconds<minimumAgeMilliseconds)continue;
            masks.Remove(item.Key);maskBytes-=item.Value.Bytes;freed+=item.Value.Bytes;nint value=item.Value.Buffer;MetalApi.Release(ref value);if(freed>=bytesToPurge)return;
        }
        if(scratchOnly)return;
        foreach(var item in images.OrderBy(p=>p.Value.Touched).ToArray())
        {
            if(Stopwatch.GetElapsedTime(item.Value.Touched).TotalMilliseconds<minimumAgeMilliseconds)continue;
            images.Remove(item.Key);imageBytes-=item.Value.Bytes;freed+=item.Value.Bytes;nint value=item.Value.Buffer;MetalApi.Release(ref value);if(freed>=bytesToPurge)return;
        }
    }
    internal void Clear(){Purge();Coverage.Clear();}
    // Every Metal buffer occupies whole pages, so the entry count is bounded
    // as well as the byte count.
    private const int MaxMasks=256;
    private long frame;
    private void EvictMasks(int count)
    {
        foreach(var item in masks.OrderBy(p=>p.Value.Touched).Take(count).ToArray())
        {masks.Remove(item.Key);maskBytes-=item.Value.Bytes;nint value=item.Value.Buffer;MetalApi.Release(ref value);}
    }
    private void EvictImages(int count)
    {
        foreach(var item in images.OrderBy(p=>p.Value.Touched).Take(count).ToArray())
        {images.Remove(item.Key);imageBytes-=item.Value.Bytes;nint value=item.Value.Buffer;MetalApi.Release(ref value);}
    }
    /// <summary>Called once per presented frame: soft clips and moving shapes
    /// produce masks that are never drawn again, so masks unused for two frames
    /// are released.</summary>
    internal void EndFrame()
    {
        frame++;
        List<MaskKey>? stale=null;
        foreach(var (key,entry) in masks)if(frame-entry.Frame>2)(stale??=[]).Add(key);
        if(stale is null)return;
        foreach(var key in stale){var entry=masks[key];masks.Remove(key);maskBytes-=entry.Bytes;nint value=entry.Buffer;MetalApi.Release(ref value);}
    }
}
