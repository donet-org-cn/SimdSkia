namespace SimdSkia.Gpu.Direct2D;
public sealed partial class Direct2DCanvas
{
    // Texture payload accounting; excludes opaque driver bookkeeping and native
    // path/brush allocation sizes, which Direct2D does not expose here.
    internal (int Count,long Bytes,long Purgeable) ResourceUsage
    {
        get
        {
            int buffers=(texture!=0?1:0)+(staging!=0?1:0)+(UsesFlipPresentation?2:0);
            return(buffers+images.Count+clips.Count*2+paths.Count+solids.Count+gradients.Count,(long)pixels.Width*pixels.Height*4*buffers+imageBytes+clipBytes,imageBytes+clipBytes);
        }
    }
    internal void ConfigureCache(long bytes,bool pathMasks)
    {Check();resourceLimit=bytes;cachePathMasks=pathMasks;if(imageBytes+clipBytes>bytes||!pathMasks)PurgeCachePayloads();}
    private void PurgeCachePayloads(bool scratchOnly=false)
    {
        foreach(var value in clips.Values){nint brush=value.Brush,bitmap=value.Bitmap;D2DNative.Release(ref brush);D2DNative.Release(ref bitmap);}clips.Clear();clipBytes=0;
        if(scratchOnly)return;
        foreach(var value in images.Values){nint bitmap=value.Bitmap;D2DNative.Release(ref bitmap);}images.Clear();imageBytes=0;
    }
    internal void PurgeCaches(long bytes,long age,bool scratchOnly)
    {
        Check();if(bytes<=0||System.Diagnostics.Stopwatch.GetElapsedTime(lastActivity).TotalMilliseconds<age)return;
        // Finish the queued batch before dropping resources referenced by it.
        Flush();PurgeCachePayloads(scratchOnly);
        if(!scratchOnly){foreach(nint item in paths.Values){nint value=item;D2DNative.Release(ref value);}paths.Clear();foreach(nint item in solids.Values){nint value=item;D2DNative.Release(ref value);}solids.Clear();foreach(nint item in gradients.Values){nint value=item;D2DNative.Release(ref value);}gradients.Clear();}
    }
    internal void DropLostDeviceResources(){if(disposed)return;if(owner!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Release Direct2D on its creating thread.");faulted=true;disposed=true;DisposeResources();}
}
