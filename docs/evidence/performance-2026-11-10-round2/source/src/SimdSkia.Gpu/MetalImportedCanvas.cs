namespace SimdSkia.Gpu;
// Experimental correctness bridge. Metal performs drawing and both directions
// of device texture transfer; CPU shadow copies are explicit and included in
// future timings. No Mac hardware validation is available on this machine.
internal sealed class MetalImportedCanvas:IGpuCanvas
{
    private readonly MetalCanvas canvas;
    private readonly MetalImage image;
    private readonly PixelBuffer pixels;
    private bool disposed,abandoned,dirty;
    internal MetalImportedCanvas(PixelBuffer pixels,MetalImage image)
    {
        this.pixels=pixels.Share();this.image=image;
        try{using var initial=image.ReadPixels();for(int y=0;y<pixels.Height;y++)initial.GetRowSpan(y).CopyTo(pixels.GetRowSpan(y));canvas=new(image.Device,pixels);}catch{this.pixels.Dispose();image.Dispose();throw;}
    }
    public bool ContextAbandoned=>abandoned||canvas.ContextAbandoned;
    public bool ResourceDisposed=>disposed;
    public int MaximumImageSize=>canvas.MaximumImageSize;
    public (int Count,long Bytes,long Purgeable) ResourceUsage=>disposed||abandoned?default:canvas.ResourceUsage;
    public void Execute(in DrawingCommand command){if(abandoned)throw new InvalidOperationException("The imported Metal target is abandoned.");canvas.Execute(command);dirty=true;}
    public void Flush(){if(abandoned)throw new InvalidOperationException("The imported Metal target is abandoned.");canvas.Flush();if(dirty){image.Write(pixels);dirty=false;}}
    public void ConfigureResourceCache(long limit,bool pathMasks)=>canvas.ConfigureResourceCache(limit,pathMasks);
    public void PurgeCachedResources(long bytesToPurge=long.MaxValue,long minimumAgeMilliseconds=0,bool scratchOnly=false)=>canvas.PurgeCachedResources(bytesToPurge,minimumAgeMilliseconds,scratchOnly);
    public void Abandon(bool releaseResources){if(disposed||abandoned)return;if(releaseResources)Flush();canvas.Abandon(releaseResources);abandoned=true;}
    public void Dispose(){if(disposed)return;try{if(!abandoned&&!canvas.ContextAbandoned)Flush();}finally{canvas.Dispose();image.Dispose();pixels.Dispose();disposed=true;}}
}
