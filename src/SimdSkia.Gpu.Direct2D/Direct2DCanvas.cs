using System.Diagnostics;
using System.Runtime.Versioning;
namespace SimdSkia.Gpu.Direct2D;

/// <summary>Hardware Direct2D canvas. Flush reads completed pixels; Present uses a native DXGI swapchain.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class Direct2DCanvas : ICanvasBackend
{
    private readonly int owner=Environment.CurrentManagedThreadId;
    private readonly PixelBuffer pixels;
    private readonly SimdSkia.Cpu.GlyphMaskCommands glyphMasks=new();
    private nint device,immediate,texture,staging,factory,drawingDevice,context,target,swapChain,completion;
    private bool drawing,disposed,faulted;
    private readonly Dictionary<(PathGeometry Geometry,float Tolerance),nint> paths=[];
    private readonly Dictionary<Rgba32,nint> solids=[];
    private readonly Dictionary<(IColorShader Shader,byte Opacity),nint> gradients=[];
    public const int MaximumCachedGradientBrushes=64;
    public int CachedGradientBrushes=>gradients.Count;
    private readonly Dictionary<CoverageClip,(nint Bitmap,nint Brush,long Bytes)> clips=[];
    private long clipBytes;
    private sealed class CachedImage(nint bitmap,byte[] snapshot,int hash,long used){internal nint Bitmap=bitmap;internal byte[] Snapshot=snapshot;internal int Hash=hash;internal long Used=used;}
    private readonly Dictionary<PixelBuffer,CachedImage> images=[];
    private long imageBytes;
    private long imageClock;
    private long resourceLimit=64*1024*1024,lastActivity;
    private bool cachePathMasks=true;
    private readonly List<nint> transient=[];
    public string DeviceName{get;private set;}="";
    public int CompletedBatches{get;private set;}
    public long UploadedTextures{get;private set;}
    public long TextureCacheHits{get;private set;}
    public double LastCompletionMilliseconds{get;private set;}
    public bool IsFaulted=>faulted;
    public Direct2DCanvas(PixelBuffer pixels,nint windowHandle=0):this(pixels,windowHandle,0) { }
    internal static Direct2DCanvas FromBorrowedDevice(PixelBuffer pixels,nint device)=>new(pixels,0,device);
    private Direct2DCanvas(PixelBuffer pixels,nint windowHandle,nint borrowedDevice)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Direct2D requires Windows.");
        D2DNative.ValidateAbi();ArgumentNullException.ThrowIfNull(pixels);pixels.ThrowIfDisposed();
        if(pixels.Format==PixelFormat.Alpha8||pixels.Alpha==AlphaFormat.Straight)throw new NotSupportedException("Direct2D targets require premultiplied alpha.");
        if(pixels.Width>16384||pixels.Height>16384||pixels.ByteCount>128*1024*1024)throw new NotSupportedException("Direct2D target exceeds 16384 pixels / 128 MiB.");
        this.pixels=pixels.Share();nint dxgi=0,adapter=0,surface=0,dxgiFactory=0;
        try
        {
            device=borrowedDevice==0?D2DNative.CreateDevice():D2DNative.Retain(borrowedDevice);immediate=D2DNative.Immediate(device);dxgi=D2DNative.Query(device,D2DNative.DxgiDevice);adapter=D2DNative.Adapter(dxgi);DeviceName=D2DNative.AdapterName(adapter);
            factory=D2DNative.CreateFactory();drawingDevice=D2DNative.DrawingDevice(factory,dxgi);context=D2DNative.DrawingContext(drawingDevice);
            if(windowHandle==0)texture=D2DNative.Texture(device,pixels.Width,pixels.Height,false);
            else{dxgiFactory=D2DNative.Parent(adapter,D2DNative.DxgiFactory2);swapChain=D2DNative.Swapchain(dxgiFactory,device,windowHandle,pixels.Width,pixels.Height);texture=D2DNative.Backbuffer(swapChain);completion=D2DNative.QueryEvent(device);}
            staging=D2DNative.Texture(device,pixels.Width,pixels.Height,true);surface=D2DNative.Query(texture,D2DNative.DxgiSurface);target=D2DNative.Target(context,surface);D2DNative.SetTarget(context,target);Upload();
        }
        catch{DisposeResources();throw;}
        finally{D2DNative.Release(ref surface);D2DNative.Release(ref dxgiFactory);D2DNative.Release(ref adapter);D2DNative.Release(ref dxgi);}
    }
    private void Check(){ObjectDisposedException.ThrowIf(disposed,this);if(Environment.CurrentManagedThreadId!=owner)throw new InvalidOperationException("Direct2D operations require their creating thread.");if(faulted)throw new InvalidOperationException("Direct2D resources are invalid after a GPU error; recreate the canvas.");pixels.ThrowIfDisposed();}
    public void Upload(){Check();if(drawing)throw new InvalidOperationException("Flush pending draws before uploading CPU pixels.");using var converted=pixels.Format==PixelFormat.Bgra8888?null:ConvertBgra(pixels);var source=converted??pixels;using var pin=source.Pin();D2DNative.Upload(immediate,texture,pin.Address,source.RowBytes);}
    private static PixelBuffer ConvertBgra(PixelBuffer source)
    {
        var result=PixelBuffer.Rent(source.Width,source.Height);
        for(int y=0;y<source.Height;y++)
        {
            var src=source.GetRowSpan(y);var dst=result.GetRowSpan(y);
            for(int x=0;x<source.Width;x++){int p=x*4;byte a=source.Alpha==AlphaFormat.Opaque?(byte)255:src[p+3],r=src[p+(source.Format==PixelFormat.Rgba8888?0:2)],g=src[p+1],b=src[p+(source.Format==PixelFormat.Rgba8888?2:0)];if(source.Alpha==AlphaFormat.Straight){r=PixelBuffer.Multiply(r,a);g=PixelBuffer.Multiply(g,a);b=PixelBuffer.Multiply(b,a);}dst[p]=b;dst[p+1]=g;dst[p+2]=r;dst[p+3]=a;}
        }
        return result;
    }
    public void Execute(in DrawingCommand command)
    {
        if(command.TextPixelGeometry is 1 or 2&&pixels.Format==PixelFormat.Bgra8888)throw new NotSupportedException("LCD glyph composition on this GPU pipeline requires explicit raster fallback.");
        if(command.Image?.Format==PixelFormat.Alpha8)throw new NotSupportedException("Direct2D alpha-only image tinting is not implemented.");
        if(command.FixedTriangleEdges)throw new NotSupportedException("Fixed mesh coverage currently requires the CPU backend.");
        if(command.LatticeRectangleEdges)throw new NotSupportedException("Lattice cell edge rounding currently requires the CPU backend.");
        if(command.Paint.MaskFilter is not null){Check();throw new NotSupportedException("Mask filters require explicit CPU fallback on this Direct2D pipeline.");}
        if(command.Paint.ImageFilter is not null){Check();throw new NotSupportedException("Image filters require explicit CPU fallback on this Direct2D pipeline.");}
        Check();lastActivity=Stopwatch.GetTimestamp();if(command.Paint.Blender is not null||command.Paint.FloatingColor is not null||command.Paint.Dither)throw new NotSupportedException("Custom blender/float-paint/dither stages require explicit raster fallback on this Direct2D pipeline.");if(command.Paint.ColorFilter is not null)throw new NotSupportedException("Color filters require explicit CPU fallback on this GPU pipeline.");if(command.Paint.Composite is not (CompositeMode.SourceOver or CompositeMode.Source or CompositeMode.Clear))throw new NotSupportedException("This Direct2D pipeline requires explicit fallback for the requested blend mode.");if(!Enum.IsDefined(command.Kind)||!Enum.IsDefined(command.Paint.Composite)||!Enum.IsDefined(command.Sampling))throw new NotSupportedException("Unknown Direct2D command/blend/sampling.");
        if(!command.Transform.IsFinite||!command.Transform.IsAffine||!command.Rectangle.IsFinite||command.Clip is {IsFinite:false})throw new ArgumentException("Finite affine coordinates required.");
        if(!float.IsFinite(command.CurveTolerance)||command.CurveTolerance<=0)throw new ArgumentOutOfRangeException(nameof(command));
        LinearGradientDescription? gradient=null;
        if(command.Paint.Shader is not null and not LinearGradient&&!command.Paint.Shader.TryGetLinearGradient(out gradient))throw new NotSupportedException("Direct2D accepts solid and linear gradients.");
        if(gradient is not null&&gradient.SampleTransform!=Matrix3.Identity)throw new NotSupportedException("Exact transformed gradient sampling requires explicit CPU fallback; native Direct2D gradient filtering differs from the scalar contract.");
        if(command.CoverageClip is not null&&command.Paint.Composite!=CompositeMode.SourceOver)throw new NotSupportedException("Coverage clipping with Source/Clear requires explicit CPU fallback.");
        if(command.CoverageClip is {Width:0} or {Height:0})return;
        if(command.Kind==CommandKind.DrawImage&&(command.Sampling==ImageSampling.Mitchell||command.NearestLowerBoundary&&!SafeNearestCoordinates(command)))throw new NotSupportedException("Exact Mitchell/lower-boundary sampling requires explicit CPU fallback.");
        if(command.Paint.Shader is not null&&command.ShaderTransform is {} shaderTransform&&shaderTransform!=command.Transform)throw new NotSupportedException("Independent brush transforms require explicit CPU fallback.");
        if(command.Kind==CommandKind.DrawGlyphRun)
        {
            if(command.Paint.Composite!=CompositeMode.SourceOver)throw new NotSupportedException("Direct2D glyph-mask Source/Clear require explicit CPU fallback.");
            foreach(var glyph in glyphMasks.Resolve(command))
                Execute(command with{Kind=CommandKind.FillRectangle,Rectangle=glyph.Bounds,Transform=Matrix3.Identity,ShaderTransform=command.ShaderTransform??command.Transform,GlyphRun=null,CoverageClip=glyph,Paint=command.Paint with{Antialias=false}});
            return;
        }
        if(command.Kind==CommandKind.FillPath&&command.Path is null)throw new ArgumentException("Path required.");
        if(command.Path?.FillRule is FillRule.InverseEvenOdd or FillRule.InverseWinding)throw new NotSupportedException("Inverse fill requires explicit CPU fallback.");
        if(command.Kind==CommandKind.DrawImage){var image=command.Image??throw new ArgumentException("Image required.");image.ThrowIfDisposed();var src=command.ImageSource??new RectF(0,0,image.Width,image.Height);if(!src.IsFinite||src.IsEmpty||src.Left<0||src.Top<0||src.Right>image.Width||src.Bottom>image.Height)throw new ArgumentOutOfRangeException(nameof(command.ImageSource));}
        if(!drawing){D2DNative.Begin(context);drawing=true;}D2DNative.Transform(context,Matrix3.Identity);bool clipped=command.Clip.HasValue;if(clipped)D2DNative.PushClip(context,command.Clip!.Value);
        // PushLayer captures compositing state. A preceding Clear/Src leaves
        // primitive COPY active, which would erase the parent's transparent mask pixels.
        D2DNative.Blend(context,command.Paint.Composite);
        bool coverage=command.CoverageClip is not null;
        if(coverage){var mask=command.CoverageClip!;D2DNative.PushCoverage(context,mask.Bounds,CoverageBrush(mask));}
        try
        {
            D2DNative.Blend(context,command.Paint.Composite);D2DNative.Antialias(context,command.Paint.Antialias);D2DNative.Transform(context,command.Transform);
            if(command.Kind==CommandKind.Clear){D2DNative.Clear(context,command.Paint.Color);return;}
            if(command.Kind==CommandKind.DrawImage){nint bitmap=CachedBitmap(command.Image!,command.ImageIsImmutable);D2DNative.DrawBitmap(context,bitmap,command.Rectangle,command.ImageSource??new(0,0,command.Image!.Width,command.Image.Height),command.Paint.Color.A,command.Sampling);return;}
            nint brush=command.Paint.Shader is {} shader&&command.Paint.Composite!=CompositeMode.Clear?Gradient(shader,gradient,command.Paint.Color.A):Solid(command.Paint.Composite==CompositeMode.Clear?default:command.Paint.Color);
            if(command.Kind==CommandKind.FillRectangle)D2DNative.FillRect(context,command.Rectangle,brush);else D2DNative.FillPath(context,GetPath(command.Path!,command.Transform,command.CurveTolerance),brush);
        }
        catch{faulted=true;throw;}
        finally{if(coverage)D2DNative.PopLayer(context);if(clipped){D2DNative.Transform(context,Matrix3.Identity);D2DNative.PopClip(context);}}
    }
    private nint Solid(Rgba32 color)
    {if(solids.TryGetValue(color,out nint brush))return brush;brush=D2DNative.Solid(context,color);if(solids.Count>=256){foreach(nint value in solids.Values){nint old=value;D2DNative.Release(ref old);}solids.Clear();}solids.Add(color,brush);return brush;}
    private nint Gradient(IColorShader shader,LinearGradientDescription? description,byte opacity)
    {
        var key=(shader,opacity);if(gradients.TryGetValue(key,out nint brush))return brush;
        brush=description is null?D2DNative.Gradient(context,(LinearGradient)shader,opacity):D2DNative.Gradient(context,description,opacity);
        try
        {
            if(gradients.Count>=MaximumCachedGradientBrushes)
            {foreach(nint value in gradients.Values){nint old=value;D2DNative.Release(ref old);}gradients.Clear();}
            gradients.Add(key,brush);return brush;
        }
        catch{D2DNative.Release(ref brush);throw;}
    }
    private nint CoverageBrush(CoverageClip mask)
    {
        if(clips.TryGetValue(mask,out var found))return found.Brush;
        long bytes=checked((long)mask.Width*mask.Height*4);if(bytes>64*1024*1024)throw new NotSupportedException("A Direct2D coverage texture exceeds 64 MiB.");
        using var pixels=PixelBuffer.Rent(mask.Width,mask.Height);
        for(int y=0;y<mask.Height;y++){var row=pixels.GetRowSpan(y);var values=mask.GetRow(mask.Top+y);for(int x=0;x<mask.Width;x++)row.Slice(x*4,4).Fill(values[x]);}
        nint bitmap=CreateBitmap(pixels),brush=0;
        try
        {
            brush=D2DNative.OpacityBrush(context,bitmap,mask.Left,mask.Top);
            if(!cachePathMasks||bytes>resourceLimit){transient.Add(bitmap);transient.Add(brush);return brush;}
            if(imageBytes+clipBytes+bytes>resourceLimit)PurgeCachePayloads();
            if(clips.Count>=128||clipBytes+bytes>64*1024*1024){foreach(var old in clips.Values){nint b=old.Brush,t=old.Bitmap;D2DNative.Release(ref b);D2DNative.Release(ref t);}clips.Clear();clipBytes=0;}
            clips.Add(mask,(bitmap,brush,bytes));clipBytes+=bytes;return brush;
        }
        catch{D2DNative.Release(ref brush);D2DNative.Release(ref bitmap);throw;}
    }
    private nint CreateBitmap(PixelBuffer source){using var converted=source.Format==PixelFormat.Bgra8888&&source.Alpha==AlphaFormat.Premultiplied?null:ConvertBgra(source);var input=converted??source;using var pin=input.Pin();return D2DNative.Bitmap(context,source.Width,source.Height,pin.Address,input.RowBytes);}
    private nint CachedBitmap(PixelBuffer source,bool immutable)
    {
        if(source.ByteCount>resourceLimit){nint one=CreateBitmap(source);transient.Add(one);UploadedTextures++;return one;}
        if(images.TryGetValue(source,out var found)&&(immutable||source.GetPixelSpan().SequenceEqual(found.Snapshot))){found.Used=++imageClock;TextureCacheHits++;return found.Bitmap;}
        if(source.ByteCount>64*1024*1024)throw new NotSupportedException("A Direct2D source texture exceeds the 64 MiB cache budget.");
        // Different wrappers (ROI crops and CPU layers) often contain the same
        // pixels. Hash only narrows the search; exact bytes/metadata decide reuse.
        var hash=new HashCode();hash.AddBytes(source.GetPixelSpan());int fingerprint=hash.ToHashCode();
        PixelBuffer? duplicate=null;
        foreach(var item in images)
            if(item.Value.Hash==fingerprint&&item.Key.Width==source.Width&&item.Key.Height==source.Height&&item.Key.RowBytes==source.RowBytes&&item.Key.Format==source.Format&&item.Key.Alpha==source.Alpha&&source.GetPixelSpan().SequenceEqual(item.Value.Snapshot))
            {duplicate=item.Key;found=item.Value;break;}
        if(duplicate is not null)
        {
            if(images.Remove(source,out var stale)){nint old=stale.Bitmap;D2DNative.Release(ref old);imageBytes-=stale.Snapshot.Length;}
            images.Remove(duplicate);images.Add(source,found!);found!.Used=++imageClock;TextureCacheHits++;return found.Bitmap;
        }
        var snapshot=source.CopyBytes();nint bitmap=CreateBitmap(source);
        if(imageBytes+clipBytes+snapshot.Length>resourceLimit)PurgeCachePayloads();
        if(images.Remove(source,out var previous)){nint old=previous.Bitmap;D2DNative.Release(ref old);imageBytes-=previous.Snapshot.Length;}
        while(images.Count>0&&(images.Count>=256||imageBytes+snapshot.Length>64*1024*1024)){var victim=images.MinBy(p=>p.Value.Used);nint old=victim.Value.Bitmap;D2DNative.Release(ref old);imageBytes-=victim.Value.Snapshot.Length;images.Remove(victim.Key);}
        images.Add(source,new(bitmap,snapshot,fingerprint,++imageClock));imageBytes+=snapshot.Length;UploadedTextures++;return bitmap;
    }
    private nint GetPath(PathGeometry path,Matrix3 transform,float requestedTolerance)
    {
        float tolerance=0;
        if(path.HasConics)
        {
            double scale=Math.Sqrt((double)transform.M00*transform.M00+(double)transform.M01*transform.M01+(double)transform.M10*transform.M10+(double)transform.M11*transform.M11);
            tolerance=(float)(requestedTolerance/Math.Max(1,scale));
            if(tolerance==0)throw new NotSupportedException("Conic transform exceeds the subdivision precision budget.");
        }
        var key=(path,tolerance);
        if(paths.TryGetValue(key,out nint result))return result;result=D2DNative.Path(factory);nint sink=0;
        try
        {
            sink=D2DNative.Open(result);D2DNative.FillRule(sink,path.FillRule==FillRule.Winding);bool open=false;
            if(path.HasConics)
            {
                foreach(var contour in path.Flatten(Matrix3.Identity,tolerance))
                {
                    if(contour.Length==0)continue;D2DNative.BeginFigure(sink,contour[0]);
                    for(int i=1;i<contour.Length;i++)D2DNative.Line(sink,contour[i]);
                    D2DNative.EndFigure(sink,contour.Length>1&&contour[0]==contour[^1]);
                }
            }
            else foreach(var s in path.Segments)
            {
                if(s.Verb==PathVerb.Move){if(open)D2DNative.EndFigure(sink,false);D2DNative.BeginFigure(sink,s.A);open=true;}
                else if(s.Verb==PathVerb.Close){if(open)D2DNative.EndFigure(sink,true);open=false;}
                else{if(!open){D2DNative.BeginFigure(sink,default);open=true;}if(s.Verb==PathVerb.Line)D2DNative.Line(sink,s.A);else if(s.Verb is PathVerb.Quadratic or PathVerb.Cubic)D2DNative.Bezier(sink,s);else throw new NotSupportedException("Unknown path verb.");}
            }
            if(open)D2DNative.EndFigure(sink,false);D2DNative.Close(sink);if(paths.Count>=1024){foreach(var value in paths.Values){nint old=value;D2DNative.Release(ref old);}paths.Clear();}paths.Add(key,result);return result;
        }
        catch{D2DNative.Release(ref result);throw;}
        finally{D2DNative.Release(ref sink);}
    }
    public void Flush()=>Finish(true,false);
    public void Present(bool capturePixels=false){if(swapChain==0)throw new InvalidOperationException("No native swapchain.");Finish(capturePixels,true);}
    private unsafe void Finish(bool readback,bool present)
    {
        Check();if(!drawing&&!present)return;long start=Stopwatch.GetTimestamp();
        try
        {
            if(drawing){D2DNative.End(context);drawing=false;}
            if(readback){D2DNative.Copy(immediate,staging,texture);var mapped=D2DNative.Map(immediate,staging);try{for(int y=0;y<pixels.Height;y++){var src=new ReadOnlySpan<byte>((byte*)mapped.Data+(long)y*mapped.RowPitch,pixels.Width*4);var dest=pixels.GetRowSpan(y);if(pixels.Format==PixelFormat.Bgra8888)src.CopyTo(dest);else for(int x=0;x<pixels.Width;x++){int p=x*4;dest[p]=src[p+2];dest[p+1]=src[p+1];dest[p+2]=src[p];dest[p+3]=src[p+3];}}}finally{D2DNative.Unmap(immediate,staging);}}
            if(present){D2DNative.Present(swapChain);D2DNative.EndEvent(immediate,completion);D2DNative.Flush(immediate);while(true){int result=D2DNative.EventReady(immediate,completion);D2DNative.Check(result);if(result==0)break;if(Stopwatch.GetElapsedTime(start).TotalSeconds>10)throw new TimeoutException("Direct2D completion timed out.");Thread.Yield();}D2DNative.SetTarget(context,target);}
            CompletedBatches++;LastCompletionMilliseconds=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            foreach(nint item in transient){nint value=item;D2DNative.Release(ref value);}transient.Clear();
        }
        catch{faulted=true;throw;}
    }
    private void DisposeResources()
    {
        glyphMasks.Clear();
        foreach(nint item in transient){nint value=item;D2DNative.Release(ref value);}transient.Clear();
        foreach(var value in paths.Values){nint p=value;D2DNative.Release(ref p);}paths.Clear();
        foreach(var value in solids.Values){nint p=value;D2DNative.Release(ref p);}solids.Clear();
        foreach(var value in gradients.Values){nint p=value;D2DNative.Release(ref p);}gradients.Clear();
        foreach(var value in clips.Values){nint p=value.Brush,b=value.Bitmap;D2DNative.Release(ref p);D2DNative.Release(ref b);}clips.Clear();clipBytes=0;
        foreach(var value in images.Values){nint p=value.Bitmap;D2DNative.Release(ref p);}images.Clear();imageBytes=0;
        D2DNative.Release(ref context);D2DNative.Release(ref target);D2DNative.Release(ref drawingDevice);D2DNative.Release(ref factory);D2DNative.Release(ref completion);D2DNative.Release(ref staging);D2DNative.Release(ref texture);D2DNative.Release(ref swapChain);if(immediate!=0){D2DNative.ClearState(immediate);D2DNative.Flush(immediate);}D2DNative.Release(ref immediate);D2DNative.Release(ref device);pixels.Dispose();
    }
    public void Dispose(){if(disposed)return;if(owner!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Dispose Direct2D on its creating thread.");try{if(drawing&&!faulted)Flush();}finally{disposed=true;DisposeResources();}}
}
