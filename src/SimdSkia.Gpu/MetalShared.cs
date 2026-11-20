namespace SimdSkia.Gpu;

// Unified-memory pixel buffers. A shared-storage MTLBuffer is wrapped as a
// PixelBuffer without copying: the GPU draws into it and the managed CPU
// rasterizer can fall back onto the same bytes after the frame was drained.
// Buffers are recycled; reuse is safe without a CPU wait because every user
// encodes into the same in-order queue and a recycled buffer is cleared by a
// GPU fill encoded after all earlier readers.
internal sealed unsafe partial class MetalDevice
{
    private const long SharedPoolLimit=64L*1024*1024;
    private readonly object sharedGate=new();
    private readonly List<(nint Buffer,int Capacity,long Returned)> freeShared=[];
    private readonly Dictionary<nint,(nint Buffer,int Capacity)> liveShared=[];
    private long pooledBytes,liveBytes;
    internal long PooledSharedBytes{get{lock(sharedGate)return pooledBytes;}}
    internal int LiveSharedBuffers{get{lock(sharedGate)return liveShared.Count;}}

    /// <summary>Rents premultiplied RGBA pixels backed by a shared MTLBuffer.
    /// The row stride is exactly <c>width*4</c>, as the kernels address it.</summary>
    internal PixelBuffer RentShared(int width,int height,bool clear)
    {
        Check();if(width<=0||height<=0||width>16384||height>16384)throw new ArgumentOutOfRangeException(nameof(width));
        int stride=checked(width*4),length=checked(stride*height);nint buffer=0;int capacity=0;bool recycled=false;
        lock(sharedGate)
        {
            int best=-1;
            for(int i=0;i<freeShared.Count;i++)if(freeShared[i].Capacity>=length&&(best<0||freeShared[i].Capacity<freeShared[best].Capacity))best=i;
            // Do not pin a large allocation for a small layer.
            if(best>=0&&freeShared[best].Capacity<=Math.Max(length*2L,length+(1<<20))){(buffer,capacity,_)=freeShared[best];freeShared.RemoveAt(best);pooledBytes-=capacity;recycled=true;}
        }
        if(buffer==0){capacity=checked((length+65535)&~65535);buffer=CreateBuffer(capacity);}
        nint contents=MetalApi.Call(buffer,"contents");
        lock(sharedGate){liveShared[contents]=(buffer,capacity);liveBytes+=capacity;}
        // New shared allocations are zero-filled pages. Recycled ones are
        // cleared on the GPU, ordered after the previous owner's readers.
        if(clear&&recycled)Clear(buffer,length);
        return PixelBuffer.Wrap(width,height,PixelFormat.Rgba8888,AlphaFormat.Premultiplied,stride,contents,()=>ReturnShared(contents));
    }
    private void Clear(nint buffer,int length)
    {
        EndCompute();using var pool=new MetalApi.Pool(true);
        nint blit=MetalApi.Call(Frame(),"blitCommandEncoder");if(blit==0)throw new InvalidOperationException("Metal blit encoder allocation failed.");
        try{MetalApi.Fill(blit,buffer,0,(nuint)length,0);}finally{MetalApi.End(blit,"endEncoding");}
    }
    // May run on the finalizer thread when a wrapper was leaked.
    private void ReturnShared(nint contents)
    {
        nint release=0;
        lock(sharedGate)
        {
            if(!liveShared.Remove(contents,out var entry))return;
            liveBytes-=entry.Capacity;
            if(disposed||pooledBytes+entry.Capacity>SharedPoolLimit)release=entry.Buffer;
            else{freeShared.Add((entry.Buffer,entry.Capacity,System.Diagnostics.Stopwatch.GetTimestamp()));pooledBytes+=entry.Capacity;}
        }
        // Command buffers retain bound resources, so in-flight work keeps it alive.
        MetalApi.Release(ref release);
    }
    /// <summary>Finds the MTLBuffer behind shared pixels (any view of them).</summary>
    internal bool TryGetShared(PixelBuffer pixels,out nint buffer)
    {
        buffer=0;
        if(pixels.IsDisposed||pixels.Format!=PixelFormat.Rgba8888||!pixels.GetManagedMemory().IsEmpty)return false;
        using var pin=pixels.Pin();
        lock(sharedGate)
        {
            if(!liveShared.TryGetValue(pin.Address,out var entry)||pixels.RowBytes!=pixels.Width*4||(long)pixels.RowBytes*pixels.Height>entry.Capacity)return false;
            buffer=entry.Buffer;return true;
        }
    }
    internal bool IsShared(PixelBuffer pixels)=>TryGetShared(pixels,out _);
    /// <summary>Drops recycled buffers, e.g. after a resize or memory warning.</summary>
    /// <summary>Releases recycled buffers idle for at least the given time (all by default).</summary>
    internal void TrimShared(long idleMilliseconds=0)
    {
        List<nint> released=[];
        lock(sharedGate)
        {
            for(int i=freeShared.Count-1;i>=0;i--)
            {
                if(idleMilliseconds>0&&System.Diagnostics.Stopwatch.GetElapsedTime(freeShared[i].Returned).TotalMilliseconds<idleMilliseconds)continue;
                released.Add(freeShared[i].Buffer);pooledBytes-=freeShared[i].Capacity;freeShared.RemoveAt(i);
            }
        }
        foreach(var entry in released){nint value=entry;MetalApi.Release(ref value);}
    }
    private long presents;
    private Timer? idleTrim;
    // Nothing presents while the UI is idle, so a timer, not the frame loop,
    // returns the pooled layer buffers of a finished animation.
    private void ScheduleIdleTrim()
    {
        idleTrim??=new Timer(static state=>((MetalDevice)state!).TrimShared(),this,Timeout.Infinite,Timeout.Infinite);
        idleTrim.Change(1000,Timeout.Infinite);
    }
    // Bounded steady state: an animation reuses its layers frame to frame, so
    // anything idle for a second (pool) or half a second (per-frame masks) is
    // garbage. Glyph and image uploads live longer.
    private void Maintain()
    {
        Resources.EndFrame();ScheduleIdleTrim();
        if(++presents%30!=0)return;
        TrimShared(1000);
        Resources.Purge(long.MaxValue,10000);
        if(Trace)lock(sharedGate)Console.Error.WriteLine($"[metal] memory live={liveBytes>>20}MB/{liveShared.Count} pooled={pooledBytes>>20}MB/{freeShared.Count} cache={Resources.Bytes>>20}MB/{Resources.Count} coverage={Resources.Coverage.Bytes>>20}MB/{Resources.Coverage.Count}");
    }
    internal static readonly bool Trace=Environment.GetEnvironmentVariable("SIMDSKIA_METAL_TRACE")=="1";
    private void ReleaseShared()
    {
        idleTrim?.Dispose();idleTrim=null;
        TrimShared();
        // Live wrappers release their buffer when they are disposed.
    }
}
