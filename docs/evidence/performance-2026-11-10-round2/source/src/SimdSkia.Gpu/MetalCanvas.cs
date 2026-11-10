using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using SimdSkia.Cpu;
namespace SimdSkia.Gpu;

// Coverage is generated in C#. The Metal compute pipeline performs actual
// paint/image sampling and composition into a shared GPU buffer. Flush commits,
// waits for completion, checks the command-buffer status and reads its bytes.
// This backend is experimental until the macOS hardware suite has run.
internal sealed unsafe class MetalCanvas:IGpuCanvas
{
    private readonly MetalDevice device;
    private readonly int owner=Environment.CurrentManagedThreadId;
    private readonly PixelBuffer pixels;
    private readonly GlyphMaskCommands glyphMasks=new();
    private readonly Dictionary<RectF,PathGeometry> rectangles=[];
    private readonly GpuCoverageCache coverageCache=new();
    private readonly Dictionary<CoverageClip,MaskEntry> masks=[];
    private readonly Dictionary<PixelBuffer,ImageEntry> images=[];
    private sealed record MaskEntry(nint Buffer,int Bytes,long Touched);
    private sealed record ImageEntry(nint Buffer,byte[] Snapshot,int Bytes,long Touched);
    private nint output,dummy,commands;
    private bool disposed,abandoned,faulted,cacheMasks=true;
    private long cacheLimit=96*1024*1024,maskBytes,imageBytes;
    internal MetalCanvas(MetalDevice device,PixelBuffer pixels)
    {
        device.Check();if(pixels.Format==PixelFormat.Alpha8||pixels.Alpha==AlphaFormat.Straight)throw new NotSupportedException("Metal targets require RGBA/BGRA8888 premultiplied or opaque pixels.");
        if(pixels.Width>16384||pixels.Height>16384||pixels.ByteCount>128*1024*1024)throw new NotSupportedException("Metal targets exceed the dimension/memory budget.");
        this.device=device;this.pixels=pixels.Share();
        try{output=Upload(pixels);dummy=device.Upload([255,255,255,255]);}
        catch{Dispose();throw;}
    }
    public bool ContextAbandoned=>abandoned||faulted||device.Abandoned;
    public bool ResourceDisposed=>disposed;
    public int MaximumImageSize=>16384;
    public (int Count,long Bytes,long Purgeable) ResourceUsage=>disposed||abandoned?default:(masks.Count+images.Count+2,(long)pixels.Width*pixels.Height*4+4+maskBytes+imageBytes,maskBytes+imageBytes);
    private void Check(){ObjectDisposedException.ThrowIf(disposed,this);if(owner!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Use Metal surfaces on their creating thread.");device.Check();if(abandoned||faulted)throw new InvalidOperationException("The Metal surface is abandoned or its command buffer failed.");}
    public void Execute(in DrawingCommand command)
    {
        Check();var paint=command.Paint;
        if(!command.Transform.IsFinite||!command.Transform.IsAffine)throw new NotSupportedException("Metal drawing requires finite affine transforms.");
        if(paint.Blender is not null||paint.ColorFilter is not null||paint.ImageFilter is not null||paint.MaskFilter is not null||paint.Dither)throw new NotSupportedException("The Metal pipeline requires explicit CPU fallback for custom blending, filters and dithering.");
        if(paint.Composite is not (CompositeMode.SourceOver or CompositeMode.Source or CompositeMode.Clear))throw new NotSupportedException("The Metal pipeline implements SourceOver, Source and Clear.");
        if(command.TextPixelGeometry is 1 or 2&&pixels.Format==PixelFormat.Bgra8888)throw new NotSupportedException("Metal LCD composition requires an additional pipeline.");
        if(command.Kind==CommandKind.Clear){Draw(command,null);return;}
        if(command.Kind==CommandKind.DrawGlyphRun){foreach(var mask in glyphMasks.Resolve(command))Draw(command,mask);return;}
        PathGeometry Rectangle(RectF rect){if(!rectangles.TryGetValue(rect,out var geometry)){if(rectangles.Count>=64)rectangles.Clear();rectangles.Add(rect,geometry=PathGeometry.Rectangle(rect));}return geometry;}
        var path=command.Kind switch{CommandKind.FillRectangle or CommandKind.DrawImage=>Rectangle(command.Rectangle),CommandKind.FillPath=>command.Path??throw new ArgumentException("The Metal path is missing."),_=>throw new NotSupportedException("Unknown Metal command.")};
        Draw(command,coverageCache.Get(path,command,pixels.Width,pixels.Height));
    }
    private nint Upload(PixelBuffer source)
    {
        using var canonical=PixelBuffer.Rent(source.Width,source.Height,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);
        for(int y=0;y<source.Height;y++)
        {
            var row=canonical.GetRowSpan(y);var input=source.GetRowSpan(y)[..(source.Width*source.BytesPerPixel)];
            if(source.Format!=PixelFormat.Alpha8&&source.Alpha!=AlphaFormat.Straight)
            {if(source.Format==PixelFormat.Rgba8888)input.CopyTo(row);else PixelTransferKernel.SwapRedBlue(input,row,CpuOptions.EnableSimd);if(source.Alpha==AlphaFormat.Opaque)for(int x=0;x<source.Width;x++)row[x*4+3]=255;}
            else for(int x=0;x<source.Width;x++)canonical.WritePixel(x,y,source.ReadPixel(x,y));
        }
        return device.Upload(canonical.GetPixelSpan());
    }
    private nint Mask(CoverageClip clip,out bool temporary)
    {
        temporary=false;if(masks.TryGetValue(clip,out var entry)){masks[clip]=entry with{Touched=Stopwatch.GetTimestamp()};return entry.Buffer;}
        int length=checked(clip.Width*clip.Height);byte[] bytes=System.Buffers.ArrayPool<byte>.Shared.Rent(length);nint buffer=0;
        try{for(int y=0;y<clip.Height;y++)clip.GetRow(clip.Top+y).CopyTo(bytes.AsSpan(y*clip.Width,clip.Width));buffer=device.Upload(bytes.AsSpan(0,length));}
        finally{System.Buffers.ArrayPool<byte>.Shared.Return(bytes);}
        if(!cacheMasks||length>cacheLimit){temporary=true;return buffer;}
        if(masks.Count>=64||maskBytes+imageBytes+length>cacheLimit)PurgeCachedResources();masks.Add(clip,new(buffer,length,Stopwatch.GetTimestamp()));maskBytes+=length;return buffer;
    }
    private nint Image(PixelBuffer source,bool immutable,out bool temporary)
    {
        temporary=false;if(images.TryGetValue(source,out var entry)&&(immutable||source.GetPixelSpan().SequenceEqual(entry.Snapshot))){images[source]=entry with{Touched=Stopwatch.GetTimestamp()};return entry.Buffer;}
        int length=checked(source.Width*source.Height*4);nint buffer=Upload(source);
        if(images.Remove(source,out var old)){nint value=old.Buffer;MetalApi.Release(ref value);imageBytes-=old.Bytes;}
        if(length>cacheLimit){temporary=true;return buffer;}
        if(images.Count>=64||maskBytes+imageBytes+length>cacheLimit)PurgeCachedResources();
        try{images.Add(source,new(buffer,source.CopyBytes(),length,Stopwatch.GetTimestamp()));imageBytes+=length;return buffer;}
        catch{MetalApi.Release(ref buffer);throw;}
    }
    private void Draw(in DrawingCommand command,CoverageClip? coverage)
    {
        bool clear=command.Kind==CommandKind.Clear;var paint=command.Paint;
        if(!clear)
        {
            if(command.CoverageClip is {} inherited)coverage=CoverageClip.Intersect(coverage!,inherited);
            if(command.Clip is {} rect){var clip=RasterCanvas.CreateClipForBounds(PathGeometry.Rectangle(rect),Matrix3.Identity,pixels.Width,pixels.Height,false);coverage=CoverageClip.Intersect(coverage!,clip);}
            if(coverage!.Width==0||coverage.Height==0)return;
        }
        LinearGradientDescription? gradient=null;
        if(paint.Shader is LinearGradient simple)gradient=new(simple.Start,simple.End,[new(0,simple.StartColor),new(1,simple.EndColor)]);
        else if(paint.Shader is not null&&!paint.Shader.TryGetLinearGradient(out gradient))throw new NotSupportedException("The Metal pipeline accepts solid and linear-gradient brushes.");
        if(gradient is not null&&gradient.Stops.Length>16)throw new NotSupportedException("The Metal gradient pipeline accepts at most 16 stops.");
        if(!(command.ShaderTransform??command.Transform).TryInvert(out var inverse))return;if(gradient is not null)inverse=gradient.SampleTransform*inverse;
        var source=command.Image;RectF imageSource=default;
        if(command.Kind==CommandKind.DrawImage)
        {
            if(source is null||source.Format==PixelFormat.Alpha8||paint.Shader is not null||command.Sampling==ImageSampling.Mitchell)throw new NotSupportedException("Metal A8/image-brush/cubic sampling requires an additional pipeline.");
            imageSource=command.ImageSource??new(0,0,source.Width,source.Height);if(!imageSource.IsFinite||imageSource.IsEmpty||imageSource.Left<0||imageSource.Top<0||imageSource.Right>source.Width||imageSource.Bottom>source.Height)throw new ArgumentOutOfRangeException(nameof(command.ImageSource));
        }
        bool temporaryMask=false,temporaryImage=false;nint mask=0,image=0;
        // Build and retain each input before purging the other cache: the encoder
        // has not yet acquired references to them at this point.
        try
        {
            mask=MetalApi.Retain(clear?dummy:Mask(coverage!,out temporaryMask));
            image=MetalApi.Retain(source is null?dummy:Image(source,command.ImageIsImmutable,out temporaryImage));
            uint* words=stackalloc uint[40];new Span<uint>(words,40).Clear();
            words[0]=(uint)pixels.Width;words[1]=(uint)pixels.Height;words[2]=(uint)(coverage?.Left??0);words[3]=(uint)(coverage?.Top??0);
            words[4]=(uint)(coverage?.Width??pixels.Width);words[5]=(uint)(coverage?.Height??pixels.Height);words[6]=clear?3u:source is not null?2u:gradient is not null?1u:0u;words[7]=paint.Composite==CompositeMode.SourceOver?0u:paint.Composite==CompositeMode.Source?1u:2u;
            var color=paint.FloatingColor??new Vector4(paint.Color.R/255f,paint.Color.G/255f,paint.Color.B/255f,paint.Color.A/255f);float* values=(float*)words;values[8]=color.X;values[9]=color.Y;values[10]=color.Z;values[11]=color.W;
            values[12]=inverse.M00;values[13]=inverse.M01;values[14]=inverse.M02;values[16]=inverse.M10;values[17]=inverse.M11;values[18]=inverse.M12;
            if(source is not null){values[20]=imageSource.Left;values[21]=imageSource.Top;values[22]=imageSource.Right-imageSource.Left;values[23]=imageSource.Bottom-imageSource.Top;values[24]=command.Rectangle.Left;values[25]=command.Rectangle.Top;values[26]=command.Rectangle.Right-command.Rectangle.Left;values[27]=command.Rectangle.Bottom-command.Rectangle.Top;words[28]=(uint)source.Width;words[29]=(uint)source.Height;words[30]=command.Sampling==ImageSampling.Nearest?0u:1u;words[31]=command.NearestLowerBoundary?1u:0u;}
            float* stops=stackalloc float[128];new Span<float>(stops,128).Clear();
            if(gradient is not null){values[32]=gradient.Start.X;values[33]=gradient.Start.Y;values[34]=gradient.End.X;values[35]=gradient.End.Y;words[36]=(uint)gradient.Stops.Length;words[37]=(uint)gradient.Extend;int index=0;foreach(var stop in gradient.Stops.Span){stops[index*8]=stop.Color.R/255f;stops[index*8+1]=stop.Color.G/255f;stops[index*8+2]=stop.Color.B/255f;stops[index*8+3]=stop.Color.A/255f;stops[index*8+4]=stop.Offset;index++;}}
            commands=commands==0?device.Commands():commands;using var pool=new MetalApi.Pool(true);nint encoder=MetalApi.Call(commands,"computeCommandEncoder");if(encoder==0)throw new InvalidOperationException("Metal compute encoder allocation failed.");
            try{MetalApi.Call(encoder,"setComputePipelineState:",device.Pipeline);MetalApi.SetBuffer(encoder,output,0);MetalApi.SetBuffer(encoder,mask,1);MetalApi.SetBuffer(encoder,image,2);MetalApi.SetBytes(encoder,words,160,3);MetalApi.SetBytes(encoder,stops,512,4);MetalApi.Dispatch(encoder,words[4],words[5]);}
            finally{MetalApi.End(encoder,"endEncoding");}
        }
        catch{faulted=true;throw;}
        finally
        {
            // A temporary upload starts with +1 and gets the local +1 above.
            if(temporaryMask){nint value=mask;MetalApi.Release(ref value);}if(temporaryImage){nint value=image;MetalApi.Release(ref value);}MetalApi.Release(ref mask);MetalApi.Release(ref image);
        }
    }
    public void Flush()
    {
        Check();if(commands==0)return;
        try{device.Complete(commands);var bytes=new ReadOnlySpan<byte>((void*)MetalApi.Call(output,"contents"),checked(pixels.Width*pixels.Height*4));for(int y=0;y<pixels.Height;y++){var input=bytes.Slice(y*pixels.Width*4,pixels.Width*4);var row=pixels.GetRowSpan(y)[..(pixels.Width*4)];if(pixels.Format==PixelFormat.Rgba8888)input.CopyTo(row);else PixelTransferKernel.SwapRedBlue(input,row,CpuOptions.EnableSimd);}}
        catch{faulted=true;throw;}
        finally{MetalApi.Release(ref commands);}
    }
    public void ConfigureResourceCache(long limit,bool pathMasks){Check();cacheLimit=limit;cacheMasks=pathMasks;coverageCache.Configure(limit,pathMasks);if(!pathMasks||maskBytes+imageBytes>limit)PurgeCachedResources();}
    public void PurgeCachedResources(long bytesToPurge=long.MaxValue,long minimumAgeMilliseconds=0,bool scratchOnly=false)
    {
        Check();if(bytesToPurge<=0)return;if(minimumAgeMilliseconds==0)coverageCache.Clear();long freed=0;
        foreach(var item in masks.OrderBy(p=>p.Value.Touched).ToArray()){if(Stopwatch.GetElapsedTime(item.Value.Touched).TotalMilliseconds<minimumAgeMilliseconds)continue;masks.Remove(item.Key);maskBytes-=item.Value.Bytes;freed+=item.Value.Bytes;nint value=item.Value.Buffer;MetalApi.Release(ref value);if(freed>=bytesToPurge)return;}
        if(!scratchOnly)foreach(var item in images.OrderBy(p=>p.Value.Touched).ToArray()){if(Stopwatch.GetElapsedTime(item.Value.Touched).TotalMilliseconds<minimumAgeMilliseconds)continue;images.Remove(item.Key);imageBytes-=item.Value.Bytes;freed+=item.Value.Bytes;nint value=item.Value.Buffer;MetalApi.Release(ref value);if(freed>=bytesToPurge)return;}
    }
    private void Release()
    {MetalApi.Release(ref commands);foreach(var mask in masks.Values){nint value=mask.Buffer;MetalApi.Release(ref value);}masks.Clear();foreach(var image in images.Values){nint value=image.Buffer;MetalApi.Release(ref value);}images.Clear();MetalApi.Release(ref output);MetalApi.Release(ref dummy);glyphMasks.Clear();rectangles.Clear();coverageCache.Clear();maskBytes=imageBytes=0;}
    public void Abandon(bool releaseResources){if(disposed||abandoned)return;if(owner!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Release Metal surfaces on their creating thread.");if(releaseResources&&!faulted&&!device.Abandoned)Flush();Release();abandoned=true;}
    public void Dispose(){if(disposed)return;if(owner!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Dispose Metal surfaces on their creating thread.");try{if(!abandoned&&!faulted&&!device.Abandoned)Flush();}finally{Release();disposed=true;pixels.Dispose();}}
}
