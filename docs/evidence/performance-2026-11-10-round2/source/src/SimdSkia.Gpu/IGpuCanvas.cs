namespace SimdSkia.Gpu;
/// <summary>Engine device boundary shared by independently implemented GPU
/// drivers. Resource sizes are driver allocations or explicitly documented
/// driver accounting, never fabricated Skia handles or native Skia statistics.</summary>
internal interface IGpuCanvas:ICanvasBackend
{
    bool ContextAbandoned {get;}
    bool ResourceDisposed {get;}
    int MaximumImageSize {get;}
    (int Count,long Bytes,long Purgeable) ResourceUsage {get;}
    void ConfigureResourceCache(long limit,bool pathMasks);
    void PurgeCachedResources(long bytesToPurge=long.MaxValue,long minimumAgeMilliseconds=0,bool scratchOnly=false);
    void Abandon(bool releaseResources);
}
