using System.Diagnostics;
using SimdSkia.Cpu;
using SimdSkia.Gpu;
using SimdSkia.Gpu.Direct2D;

namespace SimdSkia.Avalonia;

internal sealed class WindowGpuCanvas : ICanvasBackend, ILayerBackendProvider, IFilterBackend
{
    private readonly PixelBuffer target;
    private readonly ICanvasBackend hardware;
    // Metal: the target is unified-memory pixels shared with the GPU. CPU
    // fallback draws into the same bytes after draining the device, so there
    // is never an upload or a readback.
    private readonly MetalDevice? metal;
    private readonly RasterCanvas software;
    private readonly SimdSkiaGpuBackend kind;
    private readonly bool fallback;
    private readonly bool nativeWindow;
    private readonly int owner = Environment.CurrentManagedThreadId;
    private bool cpuDirty, gpuDirty, gpuShadowStale, disposed;
    private long maskBuilds,maskHits,drawDispatches,drawBarriers;
    private long blockingWaits,completionPolls;
    private long nativeSubmitted,nativeCompleted,nativeOccluded,nativeStateChanges;
    private int nativePending,completedBatches;
    private double nativeWaitMs;
    private readonly Dictionary<(GlyphRunGeometry Run,Matrix3 Matrix,bool Aa,bool Snap),PathGeometry[]> glyphBatches=[];
    private long glyphBytes;
    private readonly SimdSkiaGpuTextMode textMode;
    internal sealed record AtlasEntry(PixelBuffer Pixels,int Left,int Top,GlyphRunGeometry Run,long RetainedBytes)
    {internal long Used;}
    // Text bitmaps, shared by a window canvas and its device layers so a layer
    // does not rasterize glyphs again every frame.
    internal sealed class GlyphAtlas(int entryLimit,long byteLimit)
    {
        internal readonly Dictionary<(int ContentHash,Matrix3 Matrix,Paint Paint,bool Preblend,bool Snap,float Tolerance,object? Coverage,int OriginX,int OriginY),AtlasEntry> Entries=[];
        internal long Bytes,Clock;
        internal readonly int EntryLimit=entryLimit;
        internal readonly long ByteLimit=byteLimit;
    }
    private readonly GlyphAtlas glyphs;
    private readonly bool ownsAtlas;
    internal int CachedGlyphAtlases=>glyphs.Entries.Count;
    internal long CachedGlyphAtlasBytes=>glyphs.Bytes;
    internal long BuiltGlyphAtlases{get;private set;}
    internal long GlyphAtlasHits{get;private set;}
    public bool IsFaulted=>hardware.IsFaulted;
    internal WindowGpuCanvas(PixelBuffer target, SimdSkiaGpuBackend kind, bool fallback,nint windowHandle=0,SimdSkiaGpuTextMode textMode=SimdSkiaGpuTextMode.ManagedGlyphAtlas,int atlasEntryLimit=256,long atlasByteLimit=8*1024*1024,MetalDevice? metal=null,GlyphAtlas? sharedAtlas=null)
    {
        if(atlasEntryLimit<=0||atlasByteLimit<=0)throw new ArgumentOutOfRangeException(nameof(atlasEntryLimit));
        if(target.Alpha==AlphaFormat.Straight)throw new NotSupportedException("Window GPU rendering requires premultiplied target pixels.");
        this.target = target.Share(); this.kind = kind; this.fallback = fallback;nativeWindow=windowHandle!=0;this.textMode=textMode;
        glyphs=sharedAtlas??new(atlasEntryLimit,atlasByteLimit);ownsAtlas=sharedAtlas is null;
        software = new RasterCanvas(this.target);
        ICanvasBackend? initializing=null;
        try
        {
            if (kind == SimdSkiaGpuBackend.Metal)
            {
                if (!OperatingSystem.IsMacOS()||metal is null) throw new PlatformNotSupportedException("Metal window drawing requires a macOS Metal device.");
                var canvas = new MetalCanvas(metal,this.target); initializing=hardware = canvas;
                if (!canvas.IsShared) throw new NotSupportedException("Metal window targets must be shared device pixels.");
                this.metal = metal;
                SimdSkiaGpuDiagnostics.SetDevice($"Metal: {metal.Name}");
            }
            else if (kind == SimdSkiaGpuBackend.Direct2D)
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                var canvas = new Direct2DCanvas(this.target,windowHandle); initializing=hardware = canvas;
                SimdSkiaGpuDiagnostics.SetDevice($"Direct2D: {canvas.DeviceName}");
            }
            else
            {
                var canvas = VulkanCanvas.CreateHardware(this.target);canvas.EnableProfiling=SimdSkiaGpuDiagnostics.CaptureVulkanProfiles;initializing=hardware = canvas;
                if (canvas.IsSoftwareDevice) { canvas.Dispose(); throw new NotSupportedException("A software Vulkan device cannot be reported as hardware acceleration."); }
                canvas.Upload();
                SimdSkiaGpuDiagnostics.SetDevice($"Vulkan: {canvas.DeviceName}");
            }
        }
        catch { try{initializing?.Dispose();}finally{software.Dispose();this.target.Dispose();}throw; }
    }
    private void Check()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // A host-serialized Metal device may be driven from the UI thread (resize)
        // and the render thread; other devices are bound to their creating thread.
        if (metal?.HostSerialized != true && owner != Environment.CurrentManagedThreadId) throw new InvalidOperationException("GPU framebuffer sessions are bound to their creating thread.");
        target.ThrowIfDisposed();
    }
    public void Execute(in DrawingCommand command)
    {
        Check();
        if(command.Kind==CommandKind.DrawImage&&command.Image is {} image&&command.ImageSource is {} source&&source.IsFinite&&!source.IsEmpty&&source.Left>=0&&source.Top>=0&&source.Right<=image.Width&&source.Bottom<=image.Height)
        {
            int left=(int)MathF.Floor(source.Left),top=(int)MathF.Floor(source.Top),right=(int)MathF.Ceiling(source.Right),bottom=(int)MathF.Ceiling(source.Bottom);
            // Device pixels (layers) are sampled in place; a CPU crop would read
            // them before the device finished drawing.
            if((long)(right-left)*(bottom-top)*2<(long)image.Width*image.Height&&metal?.IsShared(image)!=true)
            {
                image.ThrowIfDisposed();using var crop=PixelBuffer.Rent(right-left,bottom-top,image.Format,image.Alpha);
                int pixelBytes = image.BytesPerPixel;
                for(int y=0;y<crop.Height;y++)image.GetRowSpan(top+y).Slice(left*pixelBytes,crop.Width*pixelBytes).CopyTo(crop.GetRowSpan(y));
                Execute(command with{Image=crop,ImageSource=new(source.Left-left,source.Top-top,source.Right-left,source.Bottom-top),ImageContentVersion=0,ImageIsImmutable=true});return;
            }
        }
        if(textMode==SimdSkiaGpuTextMode.ManagedGlyphAtlas&&command.Kind==CommandKind.DrawGlyphRun&&command.TextPixelGeometry is not (1 or 2)&&command.Paint.Blender is null&&command.Paint.FloatingColor is null&&!command.Paint.Dither&&command.Paint.Shader is null&&command.Paint.ColorFilter is null&&command.Paint.ImageFilter is null&&command.Paint.MaskFilter is null&&command.Paint.Composite==CompositeMode.SourceOver&&DrawTextAtlas(command))return;
        string? unsupported = command.Paint.Shader is not null and not LinearGradient ? "brush shader" : null;
        if(command.Paint.Shader is {} shader&&shader.TryGetLinearGradient(out var linear)&&linear is not null)
            unsupported=linear.Stops.Length>(kind switch{SimdSkiaGpuBackend.Direct2D=>256,SimdSkiaGpuBackend.Metal=>16,_=>32})||!linear.HasUnitStops?"gradient stop limits":null;
        if (kind == SimdSkiaGpuBackend.Metal)
        {
            if (!command.Transform.IsAffine||command.ShaderTransform is {IsAffine:false}) unsupported = "perspective";
            else if (command.Kind == CommandKind.DrawImage && command.Sampling == ImageSampling.Mitchell) unsupported = "cubic image sampling";
        }
        if(kind==SimdSkiaGpuBackend.Direct2D&&command.Paint.Shader is {} gradientShader&&gradientShader.TryGetLinearGradient(out var description)&&description is not null&&description.SampleTransform!=Matrix3.Identity)unsupported="exact transformed gradient sampling";
        if(command.Kind==CommandKind.DrawGlyphRun&&command.Paint.Shader is not null)unsupported="transformed glyph brush";
        if (kind == SimdSkiaGpuBackend.Direct2D)
        {
            if (command.CoverageClip is not null && command.Paint.Composite!=CompositeMode.SourceOver) unsupported = "Source/Clear coverage clip";
            else if (command.Path?.FillRule is FillRule.InverseEvenOdd or FillRule.InverseWinding) unsupported = "inverse fill";
            else if (command.Kind == CommandKind.DrawImage && (command.Sampling == ImageSampling.Mitchell || command.NearestLowerBoundary)) unsupported = "exact image sampling";
            else if (command.Paint.Shader is not null && command.ShaderTransform is {} shaderTransform && shaderTransform != command.Transform) unsupported = "independent brush transform";
        }
        if(command.Paint.Composite is not (CompositeMode.SourceOver or CompositeMode.Source or CompositeMode.Clear))unsupported="blend mode";
        if(command.Paint.ColorFilter is not null)unsupported="color filter";
        if(command.Paint.ImageFilter is not null)unsupported="image filter";
        if(command.Paint.MaskFilter is not null)unsupported="mask filter";
        if(command.Paint.Blender is not null)unsupported="custom blender";
        if(command.Paint.FloatingColor is not null)unsupported="floating color";
        if(command.Paint.Dither)unsupported="dither";
        if(command.TextPixelGeometry is 1 or 2&&target.Format==PixelFormat.Bgra8888)unsupported="LCD glyph composition";
        if(command.Image?.Format==PixelFormat.Alpha8)unsupported="alpha-only image";
        if(command.FixedTriangleEdges)unsupported="fixed mesh coverage";
        if(command.LatticeRectangleEdges)unsupported="lattice edge rounding";
        if (unsupported is not null)
        {
            if (!fallback) throw new NotSupportedException($"{kind} cannot draw {unsupported}. Explicitly enable allowCpuFallback to preserve this feature with managed CPU rendering.");
            Flush(); software.Execute(command); cpuDirty = true;
            SimdSkiaGpuDiagnostics.CpuCommand(unsupported); return;
        }
        if (cpuDirty)
        {
            // Shared Metal pixels need no upload: the CPU finished writing them
            // before the device work that follows is committed.
            if (metal is not null) cpuDirty = false;
            else
            {
                if (hardware is VulkanCanvas vulkan) vulkan.Upload();
                else if (OperatingSystem.IsWindows() && hardware is Direct2DCanvas direct2D) direct2D.Upload();
                cpuDirty = false; SimdSkiaGpuDiagnostics.Uploaded();
            }
        }
        if (command.Kind == CommandKind.DrawGlyphRun)
        {
            var run=command.GlyphRun??throw new ArgumentException("Glyph outlines required.");
            var key=(run,command.Transform,command.Paint.Antialias,command.SnapGlyphBaseline);
            if(!glyphBatches.TryGetValue(key,out var paths))
            {
                paths=GlyphCommands.PackDisjoint(command,run);long bytes=paths.Sum(path=>128L+path.Segments.Length*32L);
                if(bytes<=8*1024*1024)
                {
                    if(glyphBatches.Count>=256||glyphBytes+bytes>8*1024*1024){glyphBatches.Clear();glyphBytes=0;}
                    glyphBatches.Add(key,paths);glyphBytes+=bytes;
                }
            }
            foreach(var path in paths)hardware.Execute(command with{Kind=CommandKind.FillPath,Path=path,GlyphRun=null,Transform=Matrix3.Identity,ShaderTransform=command.Transform});
        }
        else hardware.Execute(command);
        gpuDirty = true; SimdSkiaGpuDiagnostics.GpuCommand();
    }
    private bool DrawTextAtlas(in DrawingCommand command)
    {
        var run=command.GlyphRun??throw new ArgumentException("Glyph outlines required.");var matrix=command.Transform;
        // Metal: glyph positions are quantized to a quarter pixel, as Skia's
        // subpixel text does, so moving text reuses at most 16 bitmaps per run
        // instead of rasterizing a new one at every fractional offset.
        float tx=matrix.M02,ty=matrix.M12;if(metal is not null){tx=MathF.Round(tx*4)/4;ty=MathF.Round(ty*4)/4;}
        int ix=(int)MathF.Floor(tx),iy=(int)MathF.Floor(ty);var local=matrix with{M02=tx-ix,M12=ty-iy};
        var clip = command.CoverageClip;
        var plainKey=(run.ContentHash,local,command.Paint,command.TextMaskPreblend,command.SnapGlyphBaseline,command.CurveTolerance,Coverage:(object?)null,OriginX:0,OriginY:0);
        if (clip is not null && glyphs.Entries.TryGetValue(plainKey, out var plain) && run.ContentEquals(plain.Run) &&
            clip.IsOpaqueOver(ix + plain.Left, iy + plain.Top, plain.Pixels.Width, plain.Pixels.Height))
            clip = null;
        var key=plainKey;
        if (clip is not null) { key.Coverage=clip.CacheIdentity; key.OriginX=ix; key.OriginY=iy; }
        bool found=glyphs.Entries.TryGetValue(key,out var entry);
        if(found&&!run.ContentEquals(entry!.Run))
        {RemoveCollision(entry!);found=false;}
        if(!found)
        {
            var normalized=command with{Transform=local,Clip=null,CoverageClip=null};
            if(GlyphCommands.InkBounds(normalized,run) is not {} area)return true;
            int left=(int)MathF.Floor(area.Left)-1,top=(int)MathF.Floor(area.Top)-1,right=(int)MathF.Ceiling(area.Right)+1,bottom=(int)MathF.Ceiling(area.Bottom)+1;
            int width=right-left,height=bottom-top;long bytes=(long)width*height*4;
            if(width<=0||height<=0||width>32768||height>32768||bytes>glyphs.ByteLimit)return false;
            if (clip is not null && clip.IsOpaqueOver(ix + left,iy + top,width,height))
            {
                clip = null;
                key = plainKey;
                if (glyphs.Entries.TryGetValue(key,out var collision)) RemoveCollision(collision);
            }
            var bitmap=PixelBuffer.Rent(width,height);
            try
            {
                using var raster=new RasterCanvas(bitmap);
                CoverageClip? baked=null;
                if(clip is not null)
                {
                    // Clipping after flattening changes SourceOver at overlapping glyphs.
                    // Bake device coverage per glyph and key the cache by mask + origin.
                    byte[] glyphCoverage=new byte[checked(width*height)];
                    int x0 = Math.Max(ix + left,clip.Left), x1 = Math.Min(ix + right,clip.Left + clip.Width);
                    if (x0 < x1)
                        for (int y = Math.Max(iy + top,clip.Top); y < Math.Min(iy + bottom,clip.Top + clip.Height); y++)
                            clip.GetRow(y).Slice(x0-clip.Left,x1-x0).CopyTo(glyphCoverage.AsSpan((y-iy-top)*width+(x0-ix-left)));
                    baked=CoverageClip.BorrowImmutable(0,0,width,height,glyphCoverage);
                }
                raster.Execute(normalized with{Transform=local with{M02=local.M02-left,M12=local.M12-top},CoverageClip=baked});
                entry=new(bitmap,left,top,run,bytes);
                if(glyphs.Entries.Count>=glyphs.EntryLimit||glyphs.Bytes+bytes>glyphs.ByteLimit)
                {
                    // Queued Vulkan draws still borrow these buffers. Complete
                    // them before eviction; never free an in-flight atlas.
                    if(kind==SimdSkiaGpuBackend.Vulkan)Flush();
                    while(glyphs.Entries.Count>=glyphs.EntryLimit||glyphs.Bytes+bytes>glyphs.ByteLimit)
                    {
                        using var iterator=glyphs.Entries.GetEnumerator();
                        if(!iterator.MoveNext())throw new InvalidOperationException("The glyph cache accounting is inconsistent.");
                        var oldest=iterator.Current;
                        foreach(var candidate in glyphs.Entries)if(candidate.Value.Used<oldest.Value.Used)oldest=candidate;
                        glyphs.Entries.Remove(oldest.Key);glyphs.Bytes-=oldest.Value.RetainedBytes;oldest.Value.Pixels.Dispose();SimdSkiaGpuDiagnostics.AtlasEvicted();
                    }
                }
                entry.Used=++glyphs.Clock;
                glyphs.Entries.Add(key,entry);glyphs.Bytes+=bytes;BuiltGlyphAtlases++;SimdSkiaGpuDiagnostics.AtlasBuilt();
            }
            catch{bitmap.Dispose();throw;}
        }
        else {entry!.Used=++glyphs.Clock;GlyphAtlasHits++;SimdSkiaGpuDiagnostics.AtlasHit();}
        Execute(new DrawingCommand(CommandKind.DrawImage,new(ix+entry!.Left,iy+entry.Top,ix+entry.Left+entry.Pixels.Width,iy+entry.Top+entry.Pixels.Height),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver))
        {Image=entry.Pixels,ImageIsImmutable=true,ImageContentVersion=1,ImageSource=new(0,0,entry.Pixels.Width,entry.Pixels.Height),Sampling=ImageSampling.Nearest,Clip=command.Clip});
        return true;

        void RemoveCollision(AtlasEntry old)
        {
            if(kind==SimdSkiaGpuBackend.Vulkan)Flush();
            glyphs.Entries.Remove(key);glyphs.Bytes-=old.RetainedBytes;old.Pixels.Dispose();
        }
    }
    public void Flush()
    {
        // A Metal device is shared with layers: drain it even when this canvas
        // encoded nothing, since a layer may still be drawing into its pixels.
        Check(); if (!gpuDirty&&!gpuShadowStale&&metal is null) return;
        long start = Stopwatch.GetTimestamp(); hardware.Flush();
        if(hardware is VulkanCanvas vulkan)
        {
            SimdSkiaGpuDiagnostics.LastVulkanProfile=vulkan.LastProfile;
            SimdSkiaGpuDiagnostics.VulkanCounters(vulkan.BuiltPathMasks-maskBuilds,vulkan.PathMaskHits-maskHits,vulkan.PathMaskBytes,vulkan.DrawDispatches-drawDispatches,vulkan.DrawDependencyBarriers-drawBarriers);
            maskBuilds=vulkan.BuiltPathMasks;maskHits=vulkan.PathMaskHits;drawDispatches=vulkan.DrawDispatches;drawBarriers=vulkan.DrawDependencyBarriers;
        }
        gpuDirty = false;gpuShadowStale=false;
        if(OperatingSystem.IsWindows()&&hardware is Direct2DCanvas d2d)RecordNativeCompletion(d2d,Stopwatch.GetTimestamp()-start);
        else SimdSkiaGpuDiagnostics.Completed(Stopwatch.GetTimestamp() - start);
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void RecordNativeCompletion(Direct2DCanvas canvas,long elapsed)
    {
        SimdSkiaGpuDiagnostics.CompletionMode(canvas.BlockingCompletionWaits-blockingWaits,canvas.CompletionPolls-completionPolls);
        blockingWaits=canvas.BlockingCompletionWaits;completionPolls=canvas.CompletionPolls;
        SimdSkiaGpuDiagnostics.NativeProgress(canvas.SubmittedFrames-nativeSubmitted,canvas.CompletedFrames-nativeCompleted,canvas.PendingFrames-nativePending,canvas.CompletionWaitMilliseconds-nativeWaitMs,canvas.OccludedPresents-nativeOccluded);
        nativeSubmitted=canvas.SubmittedFrames;nativeCompleted=canvas.CompletedFrames;nativePending=canvas.PendingFrames;nativeWaitMs=canvas.CompletionWaitMilliseconds;
        nativeOccluded=canvas.OccludedPresents;
        SimdSkiaGpuDiagnostics.StateChanged(canvas.NativeStateChanges-nativeStateChanges);nativeStateChanges=canvas.NativeStateChanges;
        SimdSkiaGpuDiagnostics.Completed(elapsed,canvas.CompletedBatches-completedBatches);completedBatches=canvas.CompletedBatches;
    }
    internal void Present()
    {
        Check();if(!OperatingSystem.IsWindows()||hardware is not Direct2DCanvas canvas)throw new PlatformNotSupportedException();
        if(cpuDirty){canvas.Upload();cpuDirty=false;SimdSkiaGpuDiagnostics.Uploaded();}
        long start=Stopwatch.GetTimestamp();
        if(SimdSkiaDiagnostics.CapturePresentedFrames||SimdSkiaGpuDiagnostics.SynchronizeNativeFrames)canvas.Present(SimdSkiaDiagnostics.CapturePresentedFrames);
        else canvas.QueuePresent();
        gpuDirty=false;gpuShadowStale=!SimdSkiaDiagnostics.CapturePresentedFrames;
        if(canvas.UsesFlipPresentation)SimdSkiaGpuDiagnostics.FlipPresented();
        RecordNativeCompletion(canvas,Stopwatch.GetTimestamp()-start);SimdSkiaGpuDiagnostics.NativePresented();
    }
    /// <summary>Metal: encodes presentation of the shared target into the
    /// drawable and commits the frame without a CPU wait.</summary>
    internal void Present(nint drawable,bool flip)
    {
        Check();if(hardware is not MetalCanvas canvas)throw new PlatformNotSupportedException();
        long start=Stopwatch.GetTimestamp();
        canvas.Present(drawable,flip);cpuDirty=false;gpuDirty=false;
        SimdSkiaGpuDiagnostics.Completed(Stopwatch.GetTimestamp()-start);SimdSkiaGpuDiagnostics.NativePresented();
    }

    // Device layers: an opacity/layer/effect group drawn by another Metal canvas
    // on the same device and frame. Its pixels are composited in place.
    public bool TryCreateLayer(int width,int height,PixelFormat format,out PixelBuffer pixels,out ICanvasBackend canvas)
    {
        pixels=null!;canvas=null!;
        if(metal is null||disposed||IsFaulted||format!=PixelFormat.Rgba8888)return false;
        Check();var layer=metal.RentShared(width,height,true);
        try{canvas=new WindowGpuCanvas(layer,SimdSkiaGpuBackend.Metal,fallback,textMode:textMode,metal:metal,sharedAtlas:glyphs);pixels=layer;return true;}
        catch{layer.Dispose();throw;}
    }
    public PixelBuffer? TryFilter(PixelBuffer layer,RectF region,float sigmaX,float sigmaY,float offsetX,float offsetY,Rgba32 color,bool shadow)
    {
        if(metal is null||IsFaulted)return null;Check();
        var result=metal.Filter(layer,region,sigmaX,sigmaY,offsetX,offsetY,color,shadow);
        if(result is not null)SimdSkiaGpuDiagnostics.GpuCommand();
        return result;
    }
    public void SynchronizeForCpu(){if(metal is not null&&!IsFaulted)Flush();}

    public void Dispose()
    {
        if (disposed) return; Check();
        // Metal canvases never wait here: the window presents explicitly and a
        // layer's pixels are still read by the composite encoded after it.
        try { if(!nativeWindow&&metal is null&&!IsFaulted)Flush(); }
        finally
        {
            disposed=true;
            try{hardware.Dispose();}
            finally
            {
                if(nativePending!=0){SimdSkiaGpuDiagnostics.NativeProgress(0,0,-nativePending,0,0);nativePending=0;}
                software.Dispose();target.Dispose();glyphBatches.Clear();glyphBytes=0;
                if(ownsAtlas){foreach(var item in glyphs.Entries.Values)item.Pixels.Dispose();glyphs.Entries.Clear();glyphs.Bytes=0;}
            }
        }
    }
}
