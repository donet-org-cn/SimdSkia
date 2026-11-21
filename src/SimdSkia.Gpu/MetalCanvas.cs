using System.Numerics;
using SimdSkia.Cpu;
namespace SimdSkia.Gpu;

// Coverage is generated in C#. The Metal compute pipeline performs paint and
// image sampling and composition into a GPU buffer. Two target modes exist:
// * copied (GRContext surfaces): pixels are uploaded once, Flush waits for the
//   device and copies the bytes back into the caller's buffer;
// * shared (window and layer targets): the pixels ARE the shared MTLBuffer.
//   Nothing is copied; Flush only drains the device so the CPU may touch them.
// All canvases of a device encode into the device's frame command buffer.
internal sealed unsafe class MetalCanvas:IGpuCanvas
{
    private readonly MetalDevice device;
    private readonly int owner=Environment.CurrentManagedThreadId;
    private readonly PixelBuffer pixels;
    private readonly bool shared;
    private readonly GlyphMaskCommands glyphMasks=new();
    private readonly Dictionary<RectF,PathGeometry> rectangles=[];
    private nint output,dummy;
    private bool disposed,abandoned,faulted,dirty;
    internal MetalDevice Device=>device;
    internal long Dispatches {get;private set;}
    internal MetalCanvas(MetalDevice device,PixelBuffer pixels)
    {
        device.Check();if(pixels.Format==PixelFormat.Alpha8||pixels.Alpha==AlphaFormat.Straight)throw new NotSupportedException("Metal targets require RGBA/BGRA8888 premultiplied or opaque pixels.");
        if(pixels.Width>16384||pixels.Height>16384||pixels.ByteCount>128*1024*1024)throw new NotSupportedException("Metal targets exceed the dimension/memory budget.");
        this.device=device;this.pixels=pixels.Share();
        try
        {
            if(device.TryGetShared(pixels,out nint buffer)){shared=true;output=MetalApi.Retain(buffer);}
            else output=MetalResources.Upload(device,pixels);
            dummy=device.Upload([255,255,255,255]);
        }
        catch{Dispose();throw;}
    }
    /// <summary>True when the canvas draws directly into shared pixels.</summary>
    internal bool IsShared=>shared;
    internal nint Output=>output;
    public bool ContextAbandoned=>abandoned||faulted||device.Abandoned;
    public bool ResourceDisposed=>disposed;
    public int MaximumImageSize=>16384;
    public (int Count,long Bytes,long Purgeable) ResourceUsage=>disposed||abandoned?default:(device.Resources.Count+2,(long)pixels.Width*pixels.Height*4+4+device.Resources.Bytes,device.Resources.Bytes);
    private void Check(){ObjectDisposedException.ThrowIf(disposed,this);if(!device.HostSerialized&&owner!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Use Metal surfaces on their creating thread.");device.Check();if(abandoned||faulted)throw new InvalidOperationException("The Metal surface is abandoned or its command buffer failed.");}
    public void Execute(in DrawingCommand command)
    {
        Check();var paint=command.Paint;
        if(!command.Transform.IsFinite||!command.Transform.IsAffine)throw new NotSupportedException("Metal drawing requires finite affine transforms.");
        if(paint.Blender is not null||paint.ColorFilter is not null||paint.ImageFilter is not null||paint.MaskFilter is not null||paint.Dither)throw new NotSupportedException("The Metal pipeline requires explicit CPU fallback for custom blending, filters and dithering.");
        if(paint.Composite is not (CompositeMode.SourceOver or CompositeMode.Source or CompositeMode.Clear))throw new NotSupportedException("The Metal pipeline implements SourceOver, Source and Clear.");
        if(command.TextPixelGeometry is 1 or 2&&pixels.Format==PixelFormat.Bgra8888)throw new NotSupportedException("Metal LCD composition requires an additional pipeline.");
        if(command.Kind==CommandKind.Clear){Draw(command,null,default,RectMode.None);return;}
        if(command.Kind==CommandKind.DrawGlyphRun){foreach(var mask in glyphMasks.Resolve(command))Draw(command,mask,default,RectMode.None);return;}
        // Axis-aligned rectangles and image quads: coverage is computed in the
        // kernel, so no CPU mask is rasterized or uploaded.
        if(command.Kind is CommandKind.FillRectangle or CommandKind.DrawImage&&command.Transform.M01==0&&command.Transform.M10==0&&!command.AliasedRectangleEdges&&!command.LatticeRectangleEdges)
        {
            var r=command.Rectangle;var a=command.Transform.Map(r.Left,r.Top);var b=command.Transform.Map(r.Right,r.Bottom);
            var device=new RectF(MathF.Min(a.X,b.X),MathF.Min(a.Y,b.Y),MathF.Max(a.X,b.X),MathF.Max(a.Y,b.Y));
            if(!device.IsFinite)throw new NotSupportedException("Metal drawing requires finite rectangles.");
            var mode=!paint.Antialias?RectMode.Aliased:command.Kind==CommandKind.DrawImage?RectMode.MinAxis:RectMode.Area;
            Draw(command,null,device,mode);return;
        }
        // Rounded rectangles and solid rectangle strokes: signed-distance coverage.
        if(command.Kind==CommandKind.FillPath&&command.Shape is {} shape&&TryRounded(command,shape,out var rounded)){Draw(command,null,rounded.Outer,RectMode.Rounded,rounded);return;}
        PathGeometry Rectangle(RectF rect){if(!rectangles.TryGetValue(rect,out var geometry)){if(rectangles.Count>=64)rectangles.Clear();rectangles.Add(rect,geometry=PathGeometry.Rectangle(rect));}return geometry;}
        var path=command.Kind switch{CommandKind.FillRectangle or CommandKind.DrawImage=>Rectangle(command.Rectangle),CommandKind.FillPath=>command.Path??throw new ArgumentException("The Metal path is missing."),_=>throw new NotSupportedException("Unknown Metal command.")};
        Draw(command,device.Resources.Coverage.Get(path,command,pixels.Width,pixels.Height),default,RectMode.None);
    }
    private enum RectMode:uint {None=0,Area=1,MinAxis=2,Aliased=3,Rounded=4}
    // Maps the local rounded shape to device space. Only translation and a
    // uniform positive or negative scale keep corners circular; very thin
    // shapes keep the rasterized path, where distance coverage is inexact.
    private static bool TryRounded(in DrawingCommand command,AnalyticShape shape,out AnalyticShape device)
    {
        device=default;var m=command.Transform;
        if(m.M01!=0||m.M10!=0||command.Path?.FillRule is FillRule.InverseWinding or FillRule.InverseEvenOdd)return false;
        float sx=MathF.Abs(m.M00),sy=MathF.Abs(m.M11);
        if(!(sx>0)||!(sy>0)||MathF.Abs(sx-sy)>1e-4f*MathF.Max(sx,sy))return false;
        RectF Map(RectF r){var a=m.Map(r.Left,r.Top);var b=m.Map(r.Right,r.Bottom);return new(MathF.Min(a.X,b.X),MathF.Min(a.Y,b.Y),MathF.Max(a.X,b.X),MathF.Max(a.Y,b.Y));}
        // Mirroring swaps corners: keep radii attached to device corners.
        System.Numerics.Vector4 Radii(System.Numerics.Vector4 r)
        {
            if(m.M00<0)r=new(r.Y,r.X,r.W,r.Z);
            if(m.M11<0)r=new(r.W,r.Z,r.Y,r.X);
            return r*sx;
        }
        var outer=Map(shape.Outer);if(!outer.IsFinite||outer.Right-outer.Left<1||outer.Bottom-outer.Top<1)return false;
        var inner=shape.Ring?Map(shape.Inner):default;if(shape.Ring&&!inner.IsFinite)return false;
        device=new(outer,Radii(shape.OuterRadii),inner,shape.Ring?Radii(shape.InnerRadii):default,shape.Ring);
        return float.IsFinite(device.OuterRadii.X+device.OuterRadii.Y+device.OuterRadii.Z+device.OuterRadii.W);
    }
    // Hard clips are integer device rectangles: pixel centers inside.
    private static int Edge(float value)=>(int)MathF.Ceiling(value-.5f);
    private void Draw(in DrawingCommand command,CoverageClip? coverage,RectF rectangle,RectMode mode,AnalyticShape rounded=default)
    {
        bool clear=command.Kind==CommandKind.Clear;var paint=command.Paint;
        int left=0,top=0,right=pixels.Width,bottom=pixels.Height;
        int clipLeft=0,clipTop=0,clipRight=pixels.Width,clipBottom=pixels.Height;
        if(!clear)
        {
            // An inherited soft clip is a second mask only when the shape itself
            // has one; for analytic rectangles it is the only mask.
            if(command.CoverageClip is {} inherited)coverage=coverage is null?inherited:CoverageClip.Intersect(coverage,inherited);
            if(command.Clip is {} rect)
            {
                if(!rect.IsFinite)return;
                clipLeft=Math.Clamp(Edge(rect.Left),0,pixels.Width);clipTop=Math.Clamp(Edge(rect.Top),0,pixels.Height);
                clipRight=Math.Clamp(Edge(rect.Right),clipLeft,pixels.Width);clipBottom=Math.Clamp(Edge(rect.Bottom),clipTop,pixels.Height);
            }
            left=clipLeft;top=clipTop;right=clipRight;bottom=clipBottom;
            if(coverage is not null){left=Math.Max(left,coverage.Left);top=Math.Max(top,coverage.Top);right=Math.Min(right,coverage.Left+coverage.Width);bottom=Math.Min(bottom,coverage.Top+coverage.Height);}
            if(mode!=RectMode.None)
            {
                left=Math.Max(left,(int)Math.Clamp(MathF.Floor(rectangle.Left),int.MinValue/2,int.MaxValue/2));top=Math.Max(top,(int)Math.Clamp(MathF.Floor(rectangle.Top),int.MinValue/2,int.MaxValue/2));
                right=Math.Min(right,(int)Math.Clamp(MathF.Ceiling(rectangle.Right),int.MinValue/2,int.MaxValue/2));bottom=Math.Min(bottom,(int)Math.Clamp(MathF.Ceiling(rectangle.Bottom),int.MinValue/2,int.MaxValue/2));
            }
            else if(coverage is null)return;
            if(right<=left||bottom<=top||coverage is {Width:0}or{Height:0})return;
        }
        LinearGradientDescription? gradient=null;
        bool tiled=ComputeTilePlacement.TryResolve(command,out var tileImage,out var tile);
        var compute=ComputeGradient.CanDraw(command)?((IComputeGradientShader)paint.Shader!).ComputeGradient:null;
        if(!tiled&&compute is null)
        {
            if(paint.Shader is LinearGradient simple)gradient=new(simple.Start,simple.End,[new(0,simple.StartColor),new(1,simple.EndColor)]);
            else if(paint.Shader is not null&&!paint.Shader.TryGetLinearGradient(out gradient))throw new NotSupportedException("This Metal brush requires CPU fallback.");
        }
        if(!(command.ShaderTransform??command.Transform).TryInvert(out var inverse))return;
        if(gradient is not null)inverse=gradient.SampleTransform*inverse;
        else if(compute is not null)inverse=compute.SampleTransform*inverse;
        var source=tileImage??command.Image;RectF imageSource=default;
        if(command.Kind==CommandKind.DrawImage)
        {
            if(source is null||source.Format==PixelFormat.Alpha8||paint.Shader is not null||command.Sampling==ImageSampling.Mitchell)throw new NotSupportedException("Metal A8/image-brush/cubic sampling requires an additional pipeline.");
            imageSource=command.ImageSource??new(0,0,source.Width,source.Height);if(!imageSource.IsFinite||imageSource.IsEmpty||imageSource.Left<0||imageSource.Top<0||imageSource.Right>source.Width||imageSource.Bottom>source.Height)throw new ArgumentOutOfRangeException(nameof(command.ImageSource));
        }
        bool temporaryMask=false,temporaryImage=false;nint mask=0,image=0,stopStorage=0;
        // Build and retain each input before purging the other cache: the encoder
        // has not yet acquired references to them at this point.
        try
        {
            mask=MetalApi.Retain(coverage is null?dummy:device.Resources.Mask(device,coverage,out temporaryMask));
            if(source is null)image=MetalApi.Retain(dummy);
            // Shared pixels (layers, filter results) are sampled in place.
            else if(device.TryGetShared(source,out nint sharedImage))image=MetalApi.Retain(sharedImage);
            else image=MetalApi.Retain(device.Resources.Image(device,source,tiled||command.ImageIsImmutable,out temporaryImage));
            uint* words=stackalloc uint[72];new Span<uint>(words,72).Clear();
            words[0]=(uint)pixels.Width;words[1]=(uint)pixels.Height;words[2]=(uint)left;words[3]=(uint)top;
            words[4]=(uint)(right-left);words[5]=(uint)(bottom-top);
            words[6]=clear?3u:tiled?6u:source is not null?2u:gradient is not null?1u:compute is null?0u:compute.Kind==ComputeGradientKind.Radial?4u:5u;
            words[7]=paint.Composite==CompositeMode.SourceOver?0u:paint.Composite==CompositeMode.Source?1u:2u;
            var color=paint.FloatingColor??new Vector4(paint.Color.R/255f,paint.Color.G/255f,paint.Color.B/255f,paint.Color.A/255f);float* values=(float*)words;values[8]=color.X;values[9]=color.Y;values[10]=color.Z;values[11]=color.W;
            values[12]=inverse.M00;values[13]=inverse.M01;values[14]=inverse.M02;values[16]=inverse.M10;values[17]=inverse.M11;values[18]=inverse.M12;
            if(source is not null)
            {
                words[28]=(uint)source.Width;words[29]=(uint)source.Height;
                if(tiled){words[30]=(uint)tile.Mode;values[68]=tile.X;values[69]=tile.Y;}
                else
                {
                    values[20]=imageSource.Left;values[21]=imageSource.Top;values[22]=imageSource.Right-imageSource.Left;values[23]=imageSource.Bottom-imageSource.Top;
                    values[24]=command.Rectangle.Left;values[25]=command.Rectangle.Top;values[26]=command.Rectangle.Right-command.Rectangle.Left;values[27]=command.Rectangle.Bottom-command.Rectangle.Top;
                    words[30]=command.Sampling==ImageSampling.Nearest?0u:1u;words[31]=command.NearestLowerBoundary?1u:0u;
                }
            }
            var stops=gradient?.Stops??compute?.Stops??default;
            if(gradient is not null)
            {
                values[32]=gradient.Start.X;values[33]=gradient.Start.Y;values[34]=gradient.End.X;values[35]=gradient.End.Y;
                words[37]=(uint)gradient.Extend;
            }
            else if(compute is not null)
            {
                values[32]=compute.Center.X;values[33]=compute.Center.Y;values[34]=compute.Origin.X;values[35]=compute.Origin.Y;
                values[68]=compute.Radius.X;values[69]=compute.Radius.Y;values[70]=compute.AngleTurns;
                words[37]=(uint)compute.Extend;
            }
            words[36]=(uint)stops.Length;
            float* inlineStops=stackalloc float[128];int stopBytes=Math.Max(16,checked(stops.Length*32));
            float* stopValues=inlineStops;
            if(stops.Length>16)
            {
                stopStorage=device.CreateBuffer(stopBytes);
                stopValues=(float*)MetalApi.Call(stopStorage,"contents");
            }
            var stopSpan=new Span<float>(stopValues,stopBytes/4);
            stopSpan.Clear();
            int index=0;
            foreach(var stop in stops.Span)
            {
                stopSpan[index]=stop.Color.R/255f;stopSpan[index+1]=stop.Color.G/255f;
                stopSpan[index+2]=stop.Color.B/255f;stopSpan[index+3]=stop.Color.A/255f;
                stopSpan[index+4]=stop.Offset;index+=8;
            }
            values[40]=rectangle.Left;values[41]=rectangle.Top;values[42]=rectangle.Right;values[43]=rectangle.Bottom;
            int* ints=(int*)words;ints[44]=clipLeft;ints[45]=clipTop;ints[46]=clipRight;ints[47]=clipBottom;
            if(coverage is not null){ints[48]=coverage.Left;ints[49]=coverage.Top;ints[50]=coverage.Width;ints[51]=coverage.Height;words[53]=1;}
            words[52]=(uint)mode;
            if(mode==RectMode.Rounded)
            {
                words[54]=rounded.Ring?1u:0u;words[55]=paint.Antialias?1u:0u;
                values[56]=rounded.OuterRadii.X;values[57]=rounded.OuterRadii.Y;values[58]=rounded.OuterRadii.Z;values[59]=rounded.OuterRadii.W;
                values[60]=rounded.Inner.Left;values[61]=rounded.Inner.Top;values[62]=rounded.Inner.Right;values[63]=rounded.Inner.Bottom;
                values[64]=rounded.InnerRadii.X;values[65]=rounded.InnerRadii.Y;values[66]=rounded.InnerRadii.Z;values[67]=rounded.InnerRadii.W;
            }
            device.Use(device.Pipeline);nint encoder=device.Compute();
            MetalApi.SetBuffer(encoder,output,0);MetalApi.SetBuffer(encoder,mask,1);MetalApi.SetBuffer(encoder,image,2);MetalApi.SetBytes(encoder,words,288,3);
            if(stopStorage!=0)MetalApi.SetBuffer(encoder,stopStorage,4);else MetalApi.SetBytes(encoder,inlineStops,(nuint)stopBytes,4);
            MetalApi.Dispatch(encoder,words[4],words[5]);
            dirty=true;Dispatches++;
        }
        catch(Exception error){faulted=true;if(Environment.GetEnvironmentVariable("SIMDSKIA_METAL_TRACE")=="1")Console.Error.WriteLine("[metal] canvas fault: "+error);throw;}
        finally
        {
            // A temporary upload starts with +1 and gets the local +1 above.
            if(temporaryMask){nint value=mask;MetalApi.Release(ref value);}if(temporaryImage){nint value=image;MetalApi.Release(ref value);}MetalApi.Release(ref mask);MetalApi.Release(ref image);
            MetalApi.Release(ref stopStorage);
        }
    }
    /// <summary>Drains the device. Copied targets then receive the GPU bytes.</summary>
    public void Flush()
    {
        Check();if(!dirty&&!device.HasPendingWork)return;
        try
        {
            device.Submit(true);
            if(!shared&&dirty)
            {
                var bytes=new ReadOnlySpan<byte>((void*)MetalApi.Call(output,"contents"),checked(pixels.Width*pixels.Height*4));
                for(int y=0;y<pixels.Height;y++){var input=bytes.Slice(y*pixels.Width*4,pixels.Width*4);var row=pixels.GetRowSpan(y)[..(pixels.Width*4)];if(pixels.Format==PixelFormat.Rgba8888)input.CopyTo(row);else PixelTransferKernel.SwapRedBlue(input,row,CpuOptions.EnableSimd);}
            }
            dirty=false;
        }
        catch(Exception error){faulted=true;if(Environment.GetEnvironmentVariable("SIMDSKIA_METAL_TRACE")=="1")Console.Error.WriteLine("[metal] canvas fault: "+error);throw;}
    }
    /// <summary>Encodes presentation of the shared target into a drawable and
    /// commits without waiting. The platform presents the drawable afterwards
    /// on the same queue.</summary>
    internal void Present(nint texture,bool flip)
    {
        Check();if(!shared)throw new InvalidOperationException("Only shared Metal targets are presented.");
        try{device.Present(output,pixels.Width,pixels.Height,texture,flip);device.Submit(false);dirty=false;}
        catch(Exception error){faulted=true;if(Environment.GetEnvironmentVariable("SIMDSKIA_METAL_TRACE")=="1")Console.Error.WriteLine("[metal] canvas fault: "+error);throw;}
    }
    public void ConfigureResourceCache(long limit,bool pathMasks){Check();device.Resources.Configure(limit,pathMasks);}
    public void PurgeCachedResources(long bytesToPurge=long.MaxValue,long minimumAgeMilliseconds=0,bool scratchOnly=false)
    {Check();if(bytesToPurge<=0)return;if(minimumAgeMilliseconds==0)device.Resources.Coverage.Clear();device.Resources.Purge(bytesToPurge,minimumAgeMilliseconds,scratchOnly);}
    private void Release(){MetalApi.Release(ref output);MetalApi.Release(ref dummy);glyphMasks.Clear();rectangles.Clear();}
    public void Abandon(bool releaseResources){if(disposed||abandoned)return;if(!device.HostSerialized&&owner!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Release Metal surfaces on their creating thread.");if(releaseResources&&!faulted&&!device.Abandoned)Flush();Release();abandoned=true;}
    // A shared canvas never waits on disposal: its pixels stay valid and later
    // readers synchronize through the device.
    public void Dispose(){if(disposed)return;if(!device.HostSerialized&&owner!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Dispose Metal surfaces on their creating thread.");try{if(!shared&&!abandoned&&!faulted&&!device.Abandoned)Flush();}finally{Release();disposed=true;pixels.Dispose();}}
}
