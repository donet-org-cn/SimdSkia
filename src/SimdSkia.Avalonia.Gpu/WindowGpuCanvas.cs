using System.Diagnostics;
using SimdSkia.Cpu;
using SimdSkia.Gpu;
using SimdSkia.Gpu.Direct2D;

namespace SimdSkia.Avalonia;

internal sealed class WindowGpuCanvas : ICanvasBackend
{
    private readonly PixelBuffer target;
    private readonly ICanvasBackend hardware;
    private readonly RasterCanvas software;
    private readonly SimdSkiaGpuBackend kind;
    private readonly bool fallback;
    private readonly int owner = Environment.CurrentManagedThreadId;
    private bool cpuDirty, gpuDirty, disposed;
    private long maskBuilds,maskHits,drawDispatches,drawBarriers;
    private readonly Dictionary<(GlyphRunGeometry Run,Matrix3 Matrix,bool Aa,bool Snap),PathGeometry[]> glyphBatches=[];
    private long glyphBytes;
    private readonly SimdSkiaGpuTextMode textMode;
    private readonly Dictionary<(int ContentHash,Matrix3 Matrix,Paint Paint,bool Preblend,bool Snap,float Tolerance,CoverageClip? Coverage,int OriginX,int OriginY),AtlasEntry> atlas=[];
    private sealed record AtlasEntry(PixelBuffer Pixels,int Left,int Top,GlyphRunGeometry Run,long RetainedBytes);
    private long atlasBytes;
    public bool IsFaulted=>hardware.IsFaulted;
    internal WindowGpuCanvas(PixelBuffer target, SimdSkiaGpuBackend kind, bool fallback,nint windowHandle=0,SimdSkiaGpuTextMode textMode=SimdSkiaGpuTextMode.ManagedGlyphAtlas)
    {
        if(target.Alpha==AlphaFormat.Straight)throw new NotSupportedException("Window GPU rendering requires premultiplied target pixels.");
        this.target = target.Share(); this.kind = kind; this.fallback = fallback;this.textMode=textMode;
        software = new RasterCanvas(this.target);
        try
        {
            if (kind == SimdSkiaGpuBackend.Direct2D)
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                var canvas = new Direct2DCanvas(this.target,windowHandle); hardware = canvas;
                SimdSkiaGpuDiagnostics.SetDevice($"Direct2D: {canvas.DeviceName}");
            }
            else
            {
                var canvas = new VulkanCanvas(this.target){EnableProfiling=SimdSkiaGpuDiagnostics.CaptureVulkanProfiles}; hardware = canvas;
                if (canvas.IsSoftwareDevice) { canvas.Dispose(); throw new NotSupportedException("A software Vulkan device cannot be reported as hardware acceleration."); }
                canvas.Upload();
                SimdSkiaGpuDiagnostics.SetDevice($"Vulkan: {canvas.DeviceName}");
            }
        }
        catch { software.Dispose(); this.target.Dispose(); throw; }
    }
    private void Check()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (owner != Environment.CurrentManagedThreadId) throw new InvalidOperationException("GPU framebuffer sessions are bound to their creating thread.");
        target.ThrowIfDisposed();
    }
    public void Execute(in DrawingCommand command)
    {
        Check();
        if(command.Kind==CommandKind.DrawImage&&command.Image is {} image&&command.ImageSource is {} source&&source.IsFinite&&!source.IsEmpty&&source.Left>=0&&source.Top>=0&&source.Right<=image.Width&&source.Bottom<=image.Height)
        {
            int left=(int)MathF.Floor(source.Left),top=(int)MathF.Floor(source.Top),right=(int)MathF.Ceiling(source.Right),bottom=(int)MathF.Ceiling(source.Bottom);
            if((long)(right-left)*(bottom-top)*2<(long)image.Width*image.Height)
            {
                image.ThrowIfDisposed();using var crop=PixelBuffer.Rent(right-left,bottom-top,image.Format,image.Alpha);
                for(int y=0;y<crop.Height;y++)image.GetRowSpan(top+y).Slice(left*4,crop.Width*4).CopyTo(crop.GetRowSpan(y));
                Execute(command with{Image=crop,ImageSource=new(source.Left-left,source.Top-top,source.Right-left,source.Bottom-top),ImageIsImmutable=true});return;
            }
        }
        if(textMode==SimdSkiaGpuTextMode.ManagedGlyphAtlas&&command.Kind==CommandKind.DrawGlyphRun&&command.Paint.Shader is null&&command.Paint.Composite==CompositeMode.SourceOver&&DrawTextAtlas(command))return;
        string? unsupported = command.Paint.Shader is not null and not LinearGradient ? "brush shader" : null;
        if(command.Paint.Shader is {} shader&&shader.TryGetLinearGradient(out var linear)&&linear is not null)
            unsupported=linear.Stops.Length>(kind==SimdSkiaGpuBackend.Direct2D?256:32)||linear.Stops.ToArray().Any(s=>s.Offset<0||s.Offset>1)?"gradient stop limits":null;
        if(kind==SimdSkiaGpuBackend.Direct2D&&command.Paint.Shader is {} gradientShader&&gradientShader.TryGetLinearGradient(out var description)&&description is not null&&description.SampleTransform!=Matrix3.Identity)unsupported="exact transformed gradient sampling";
        if(command.Kind==CommandKind.DrawGlyphRun&&command.Paint.Shader is not null)unsupported="transformed glyph brush";
        if (kind == SimdSkiaGpuBackend.Direct2D)
        {
            if (command.CoverageClip is not null && command.Paint.Composite!=CompositeMode.SourceOver) unsupported = "Source/Clear coverage clip";
            else if (command.Path?.FillRule is FillRule.InverseEvenOdd or FillRule.InverseWinding) unsupported = "inverse fill";
            else if (command.Kind == CommandKind.DrawImage && (command.Sampling == ImageSampling.Mitchell || command.NearestLowerBoundary)) unsupported = "exact image sampling";
            else if (command.Paint.Shader is not null && command.ShaderTransform is {} shaderTransform && shaderTransform != command.Transform) unsupported = "independent brush transform";
        }
        if (unsupported is not null)
        {
            if (!fallback) throw new NotSupportedException($"{kind} cannot draw {unsupported}. Explicitly enable allowCpuFallback to preserve this feature with managed CPU rendering.");
            Flush(); software.Execute(command); cpuDirty = true;
            SimdSkiaGpuDiagnostics.CpuCommand(unsupported); return;
        }
        if (cpuDirty)
        {
            if (hardware is VulkanCanvas vulkan) vulkan.Upload();
            else if (OperatingSystem.IsWindows() && hardware is Direct2DCanvas direct2D) direct2D.Upload();
            cpuDirty = false; SimdSkiaGpuDiagnostics.Uploaded();
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
        int ix=(int)MathF.Floor(matrix.M02),iy=(int)MathF.Floor(matrix.M12);var local=matrix with{M02=matrix.M02-ix,M12=matrix.M12-iy};
        var hash=new HashCode();hash.Add(run.FontTransform);hash.Add(run.Positioning);foreach(var glyph in run.Glyphs)hash.Add(glyph);
        var key=(hash.ToHashCode(),local,command.Paint,command.TextMaskPreblend,command.SnapGlyphBaseline,command.CurveTolerance,command.CoverageClip,command.CoverageClip is null?0:ix,command.CoverageClip is null?0:iy);
        bool found=atlas.TryGetValue(key,out var entry);
        if(found&&!ReferenceEquals(run,entry!.Run)&&(run.FontTransform!=entry.Run.FontTransform||run.Positioning!=entry.Run.Positioning||!run.Glyphs.SequenceEqual(entry.Run.Glyphs)))
        {atlas.Remove(key);atlasBytes-=entry.RetainedBytes;entry.Pixels.Dispose();found=false;}
        if(!found)
        {
            var normalized=command with{Transform=local,Clip=null,CoverageClip=null};RectF? bounds=null;
            foreach(var item in GlyphCommands.Expand(normalized,run))
            {
                if(item.Path!.Segments.IsEmpty)continue;var b=item.Path.GetTightBounds(item.Transform);
                bounds=bounds is {} old?new(Math.Min(old.Left,b.Left),Math.Min(old.Top,b.Top),Math.Max(old.Right,b.Right),Math.Max(old.Bottom,b.Bottom)):b;
            }
            if(bounds is not {} area)return true;
            int left=(int)MathF.Floor(area.Left)-1,top=(int)MathF.Floor(area.Top)-1,right=(int)MathF.Ceiling(area.Right)+1,bottom=(int)MathF.Ceiling(area.Bottom)+1;
            int width=right-left,height=bottom-top;long bytes=(long)width*height*4+(long)(command.CoverageClip?.Width??0)*(command.CoverageClip?.Height??0);
            if(width<=0||height<=0||width>32768||height>32768||bytes>8*1024*1024)return false;
            var bitmap=PixelBuffer.Rent(width,height);
            try
            {
                using var raster=new RasterCanvas(bitmap);
                CoverageClip? baked=null;
                if(command.CoverageClip is {} clip)
                {
                    // Clipping after flattening changes SourceOver at overlapping glyphs.
                    // Bake device coverage per glyph and key the cache by mask + origin.
                    byte[] coverage=new byte[checked(width*height)];
                    for(int y=0;y<height;y++)for(int x=0;x<width;x++)coverage[y*width+x]=clip.At(ix+left+x,iy+top+y);
                    baked=new CoverageClip(0,0,width,height,coverage);
                }
                raster.Execute(normalized with{Transform=local with{M02=local.M02-left,M12=local.M12-top},CoverageClip=baked});
                entry=new(bitmap,left,top,run,bytes);
                if(atlas.Count>=256||atlasBytes+bytes>8*1024*1024){foreach(var old in atlas.Values)old.Pixels.Dispose();atlas.Clear();atlasBytes=0;}
                atlas.Add(key,entry);atlasBytes+=bytes;SimdSkiaGpuDiagnostics.AtlasBuilt();
            }
            catch{bitmap.Dispose();throw;}
        }
        else SimdSkiaGpuDiagnostics.AtlasHit();
        Execute(new DrawingCommand(CommandKind.DrawImage,new(ix+entry!.Left,iy+entry.Top,ix+entry.Left+entry.Pixels.Width,iy+entry.Top+entry.Pixels.Height),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.SourceOver))
        {Image=entry.Pixels,ImageIsImmutable=true,ImageSource=new(0,0,entry.Pixels.Width,entry.Pixels.Height),Sampling=ImageSampling.Nearest,Clip=command.Clip});
        return true;
    }
    public void Flush()
    {
        Check(); if (!gpuDirty) return;
        long start = Stopwatch.GetTimestamp(); hardware.Flush();
        if(hardware is VulkanCanvas vulkan)
        {
            SimdSkiaGpuDiagnostics.LastVulkanProfile=vulkan.LastProfile;
            SimdSkiaGpuDiagnostics.VulkanCounters(vulkan.BuiltPathMasks-maskBuilds,vulkan.PathMaskHits-maskHits,vulkan.PathMaskBytes,vulkan.DrawDispatches-drawDispatches,vulkan.DrawDependencyBarriers-drawBarriers);
            maskBuilds=vulkan.BuiltPathMasks;maskHits=vulkan.PathMaskHits;drawDispatches=vulkan.DrawDispatches;drawBarriers=vulkan.DrawDependencyBarriers;
        }
        gpuDirty = false; SimdSkiaGpuDiagnostics.Completed(Stopwatch.GetTimestamp() - start);
    }
    internal void Present()
    {
        Check();if(!OperatingSystem.IsWindows()||hardware is not Direct2DCanvas canvas)throw new PlatformNotSupportedException();
        if(cpuDirty){canvas.Upload();cpuDirty=false;SimdSkiaGpuDiagnostics.Uploaded();}
        long start=Stopwatch.GetTimestamp();canvas.Present(SimdSkiaDiagnostics.CapturePresentedFrames);gpuDirty=false;
        SimdSkiaGpuDiagnostics.Completed(Stopwatch.GetTimestamp()-start);SimdSkiaGpuDiagnostics.NativePresented();
    }
    public void Dispose()
    {
        if (disposed) return; Check();
        try { if(!IsFaulted)Flush(); }
        finally { disposed = true;hardware.Dispose();software.Dispose();target.Dispose();glyphBatches.Clear();glyphBytes=0;foreach(var item in atlas.Values)item.Pixels.Dispose();atlas.Clear();atlasBytes=0; }
    }
}
