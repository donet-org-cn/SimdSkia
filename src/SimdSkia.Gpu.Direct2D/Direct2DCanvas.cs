using System.Diagnostics;
using System.Runtime.Versioning;
namespace SimdSkia.Gpu.Direct2D;

/// <summary>Hardware Direct2D canvas. Flush reads completed pixels; Present uses a native DXGI swapchain.</summary>
[SupportedOSPlatform("windows")]
public sealed class Direct2DCanvas : ICanvasBackend
{
    private readonly int owner=Environment.CurrentManagedThreadId;
    private readonly PixelBuffer pixels;
    private nint device,immediate,texture,staging,factory,drawingDevice,context,target,swapChain,completion;
    private bool drawing,disposed,faulted;
    private readonly Dictionary<PathGeometry,nint> paths=[];
    private readonly Dictionary<Rgba32,nint> solids=[];
    private readonly Dictionary<CoverageClip,(nint Bitmap,nint Brush,long Bytes)> clips=[];
    private long clipBytes;
    private sealed class CachedImage(nint bitmap,byte[] snapshot,long used){internal nint Bitmap=bitmap;internal byte[] Snapshot=snapshot;internal long Used=used;}
    private readonly Dictionary<PixelBuffer,CachedImage> images=[];
    private long imageBytes;
    private long imageClock;
    public string DeviceName{get;private set;}="";
    public int CompletedBatches{get;private set;}
    public double LastCompletionMilliseconds{get;private set;}
    public bool IsFaulted=>faulted;
    public Direct2DCanvas(PixelBuffer pixels,nint windowHandle=0)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Direct2D requires Windows.");
        D2DNative.ValidateAbi();ArgumentNullException.ThrowIfNull(pixels);pixels.ThrowIfDisposed();
        if(pixels.Alpha==AlphaFormat.Straight)throw new NotSupportedException("Direct2D targets require premultiplied alpha.");
        if(pixels.Width>16384||pixels.Height>16384||pixels.ByteCount>128*1024*1024)throw new NotSupportedException("Direct2D target exceeds 16384 pixels / 128 MiB.");
        this.pixels=pixels.Share();nint dxgi=0,adapter=0,surface=0,dxgiFactory=0;
        try
        {
            device=D2DNative.CreateDevice();immediate=D2DNative.Immediate(device);dxgi=D2DNative.Query(device,D2DNative.DxgiDevice);adapter=D2DNative.Adapter(dxgi);DeviceName=D2DNative.AdapterName(adapter);
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
            for(int x=0;x<source.Width;x++){int p=x*4;byte a=src[p+3],r=src[p+(source.Format==PixelFormat.Rgba8888?0:2)],g=src[p+1],b=src[p+(source.Format==PixelFormat.Rgba8888?2:0)];if(source.Alpha==AlphaFormat.Straight){r=PixelBuffer.Multiply(r,a);g=PixelBuffer.Multiply(g,a);b=PixelBuffer.Multiply(b,a);}dst[p]=b;dst[p+1]=g;dst[p+2]=r;dst[p+3]=a;}
        }
        return result;
    }
    public void Execute(in DrawingCommand command)
    {
        Check();if(!Enum.IsDefined(command.Kind)||!Enum.IsDefined(command.Paint.Composite)||!Enum.IsDefined(command.Sampling))throw new NotSupportedException("Unknown Direct2D command/blend/sampling.");
        if(!command.Transform.IsFinite||!command.Transform.IsAffine||!command.Rectangle.IsFinite||command.Clip is {IsFinite:false})throw new ArgumentException("Finite affine coordinates required.");
        if(!float.IsFinite(command.CurveTolerance)||command.CurveTolerance<=0)throw new ArgumentOutOfRangeException(nameof(command));
        LinearGradientDescription? gradient=null;
        if(command.Paint.Shader is not null and not LinearGradient&&!command.Paint.Shader.TryGetLinearGradient(out gradient))throw new NotSupportedException("Direct2D accepts solid and linear gradients.");
        if(gradient is not null&&gradient.SampleTransform!=Matrix3.Identity)throw new NotSupportedException("Exact transformed gradient sampling requires explicit CPU fallback; native Direct2D gradient filtering differs from the scalar contract.");
        if(command.CoverageClip is not null&&command.Paint.Composite!=CompositeMode.SourceOver)throw new NotSupportedException("Coverage clipping with Source/Clear requires explicit CPU fallback.");
        if(command.CoverageClip is {Width:0} or {Height:0})return;
        if(command.Kind==CommandKind.DrawImage&&(command.Sampling==ImageSampling.Mitchell||command.NearestLowerBoundary))throw new NotSupportedException("Exact Mitchell/lower-boundary sampling requires explicit CPU fallback.");
        if(command.Paint.Shader is not null&&command.ShaderTransform is {} shaderTransform&&shaderTransform!=command.Transform)throw new NotSupportedException("Independent brush transforms require explicit CPU fallback.");
        if(command.Kind==CommandKind.DrawGlyphRun){foreach(var item in GlyphCommands.Expand(command,command.GlyphRun??throw new ArgumentException("Glyph outlines required.")))Execute(item);return;}
        if(command.Kind==CommandKind.FillPath&&command.Path is null)throw new ArgumentException("Path required.");
        if(command.Path?.FillRule is FillRule.InverseEvenOdd or FillRule.InverseWinding)throw new NotSupportedException("Inverse fill requires explicit CPU fallback.");
        if(command.Kind==CommandKind.DrawImage){var image=command.Image??throw new ArgumentException("Image required.");image.ThrowIfDisposed();var src=command.ImageSource??new RectF(0,0,image.Width,image.Height);if(!src.IsFinite||src.IsEmpty||src.Left<0||src.Top<0||src.Right>image.Width||src.Bottom>image.Height)throw new ArgumentOutOfRangeException(nameof(command.ImageSource));}
        if(!drawing){D2DNative.Begin(context);drawing=true;}D2DNative.Transform(context,Matrix3.Identity);bool clipped=command.Clip.HasValue;if(clipped)D2DNative.PushClip(context,command.Clip!.Value);
        bool coverage=command.CoverageClip is not null;
        if(coverage){var mask=command.CoverageClip!;D2DNative.PushCoverage(context,mask.Bounds,CoverageBrush(mask));}
        try
        {
            D2DNative.Blend(context,command.Paint.Composite);D2DNative.Antialias(context,command.Paint.Antialias);D2DNative.Transform(context,command.Transform);
            if(command.Kind==CommandKind.Clear){D2DNative.Clear(context,command.Paint.Color);return;}
            if(command.Kind==CommandKind.DrawImage){nint bitmap=CachedBitmap(command.Image!,command.ImageIsImmutable);D2DNative.DrawBitmap(context,bitmap,command.Rectangle,command.ImageSource??new(0,0,command.Image!.Width,command.Image.Height),command.Paint.Color.A,command.Sampling);return;}
            bool temporary=command.Paint.Shader is not null&&command.Paint.Composite!=CompositeMode.Clear;
            nint brush=temporary?(gradient is not null?D2DNative.Gradient(context,gradient,command.Paint.Color.A):D2DNative.Gradient(context,(LinearGradient)command.Paint.Shader!,command.Paint.Color.A)):Solid(command.Paint.Composite==CompositeMode.Clear?default:command.Paint.Color);
            try{if(command.Kind==CommandKind.FillRectangle)D2DNative.FillRect(context,command.Rectangle,brush);else D2DNative.FillPath(context,GetPath(command.Path!),brush);}finally{if(temporary)D2DNative.Release(ref brush);}
        }
        catch{faulted=true;throw;}
        finally{if(coverage)D2DNative.PopLayer(context);if(clipped){D2DNative.Transform(context,Matrix3.Identity);D2DNative.PopClip(context);}}
    }
    private nint Solid(Rgba32 color)
    {if(solids.TryGetValue(color,out nint brush))return brush;brush=D2DNative.Solid(context,color);if(solids.Count>=256){foreach(nint value in solids.Values){nint old=value;D2DNative.Release(ref old);}solids.Clear();}solids.Add(color,brush);return brush;}
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
            if(clips.Count>=128||clipBytes+bytes>64*1024*1024){foreach(var old in clips.Values){nint b=old.Brush,t=old.Bitmap;D2DNative.Release(ref b);D2DNative.Release(ref t);}clips.Clear();clipBytes=0;}
            clips.Add(mask,(bitmap,brush,bytes));clipBytes+=bytes;return brush;
        }
        catch{D2DNative.Release(ref brush);D2DNative.Release(ref bitmap);throw;}
    }
    private nint CreateBitmap(PixelBuffer source){using var converted=source.Format==PixelFormat.Bgra8888&&source.Alpha==AlphaFormat.Premultiplied?null:ConvertBgra(source);var input=converted??source;using var pin=input.Pin();return D2DNative.Bitmap(context,source.Width,source.Height,pin.Address,input.RowBytes);}
    private nint CachedBitmap(PixelBuffer source,bool immutable)
    {
        if(images.TryGetValue(source,out var found)&&(immutable||source.GetPixelSpan().SequenceEqual(found.Snapshot))){found.Used=++imageClock;return found.Bitmap;}
        if(source.ByteCount>64*1024*1024)throw new NotSupportedException("A Direct2D source texture exceeds the 64 MiB cache budget.");
        var snapshot=source.CopyBytes();nint bitmap=CreateBitmap(source);
        if(images.Remove(source,out var previous)){nint old=previous.Bitmap;D2DNative.Release(ref old);imageBytes-=previous.Snapshot.Length;}
        while(images.Count>0&&(images.Count>=256||imageBytes+snapshot.Length>64*1024*1024)){var victim=images.MinBy(p=>p.Value.Used);nint old=victim.Value.Bitmap;D2DNative.Release(ref old);imageBytes-=victim.Value.Snapshot.Length;images.Remove(victim.Key);}
        images.Add(source,new(bitmap,snapshot,++imageClock));imageBytes+=snapshot.Length;return bitmap;
    }
    private nint GetPath(PathGeometry path)
    {
        if(paths.TryGetValue(path,out nint result))return result;result=D2DNative.Path(factory);nint sink=0;
        try
        {
            sink=D2DNative.Open(result);D2DNative.FillRule(sink,path.FillRule==FillRule.Winding);bool open=false;
            foreach(var s in path.Segments)
            {
                if(s.Verb==PathVerb.Move){if(open)D2DNative.EndFigure(sink,false);D2DNative.BeginFigure(sink,s.A);open=true;}
                else if(s.Verb==PathVerb.Close){if(open)D2DNative.EndFigure(sink,true);open=false;}
                else{if(!open){D2DNative.BeginFigure(sink,default);open=true;}if(s.Verb==PathVerb.Line)D2DNative.Line(sink,s.A);else if(s.Verb is PathVerb.Quadratic or PathVerb.Cubic)D2DNative.Bezier(sink,s);else throw new NotSupportedException("Unknown path verb.");}
            }
            if(open)D2DNative.EndFigure(sink,false);D2DNative.Close(sink);if(paths.Count>=1024){foreach(var value in paths.Values){nint old=value;D2DNative.Release(ref old);}paths.Clear();}paths.Add(path,result);return result;
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
        }
        catch{faulted=true;throw;}
    }
    private void DisposeResources()
    {
        foreach(var value in paths.Values){nint p=value;D2DNative.Release(ref p);}paths.Clear();
        foreach(var value in solids.Values){nint p=value;D2DNative.Release(ref p);}solids.Clear();
        foreach(var value in clips.Values){nint p=value.Brush,b=value.Bitmap;D2DNative.Release(ref p);D2DNative.Release(ref b);}clips.Clear();clipBytes=0;
        foreach(var value in images.Values){nint p=value.Bitmap;D2DNative.Release(ref p);}images.Clear();imageBytes=0;
        D2DNative.Release(ref context);D2DNative.Release(ref target);D2DNative.Release(ref drawingDevice);D2DNative.Release(ref factory);D2DNative.Release(ref completion);D2DNative.Release(ref staging);D2DNative.Release(ref texture);D2DNative.Release(ref swapChain);if(immediate!=0){D2DNative.ClearState(immediate);D2DNative.Flush(immediate);}D2DNative.Release(ref immediate);D2DNative.Release(ref device);pixels.Dispose();
    }
    public void Dispose(){if(disposed)return;if(owner!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Dispose Direct2D on its creating thread.");try{if(drawing&&!faulted)Flush();}finally{disposed=true;DisposeResources();}}
}
