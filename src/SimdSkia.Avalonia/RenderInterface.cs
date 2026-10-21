using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using SimdSkia.Cpu;
using SimdSkia.Imaging;
using AFormat=Avalonia.Platform.PixelFormat;
using AAlpha=Avalonia.Platform.AlphaFormat;

namespace SimdSkia.Avalonia;

internal sealed class RenderInterface(Func<PixelBuffer,ICanvasBackend>? backendFactory=null,Func<INativePlatformHandleSurface,IRenderTarget>? nativeFactory=null):IPlatformRenderInterface
{
    public bool SupportsIndividualRoundRects=>true;
    public bool SupportsRegions=>true;
    public AFormat DefaultPixelFormat=>PixelFormats.Bgra8888;
    public AAlpha DefaultAlphaFormat=>AAlpha.Premul;
    public bool IsSupportedBitmapPixelFormat(AFormat format)=>format==PixelFormats.Bgra8888||format==PixelFormats.Rgba8888;
    public IPlatformRenderInterfaceRegion CreateRegion()=>new Region();
    public IGeometryImpl CreateEllipseGeometry(Rect rect)=>Geometry.Ellipse(rect);
    public IGeometryImpl CreateRectangleGeometry(Rect rect)=>Geometry.Rectangle(rect);
    public IGeometryImpl CreateLineGeometry(Point a,Point b)=>new Geometry(new SimdSkia.PathGeometry([new(PathVerb.Move,ConvertTo.Point(a)),new(PathVerb.Line,ConvertTo.Point(b))]));
    public IStreamGeometryImpl CreateStreamGeometry()=>new StreamGeometry();
    public IGeometryImpl CreateGeometryGroup(global::Avalonia.Media.FillRule rule,IReadOnlyList<IGeometryImpl> children)
    {
        var fill=new List<PathSegment>();var stroke=new List<PathSegment>();foreach(Geometry child in children){child.Validate();child.Path.AppendTransformedTo(fill,Matrix3.Identity);child.StrokePath.AppendTransformedTo(stroke,Matrix3.Identity);}
        var f=rule==global::Avalonia.Media.FillRule.EvenOdd?SimdSkia.FillRule.EvenOdd:SimdSkia.FillRule.Winding;return new Geometry(new(fill,f),new(stroke,f));
    }
    public IGeometryImpl CreateCombinedGeometry(GeometryCombineMode mode,IGeometryImpl a,IGeometryImpl b)=>new Geometry(Geometry.Combine(((Geometry)a).Path,((Geometry)b).Path,mode));
    public IGeometryImpl BuildGlyphRunGeometry(global::Avalonia.Media.GlyphRun run)
    {using var glyph=new GlyphRun(run.GlyphTypeface,run.FontRenderingEmSize,run.GlyphInfos,run.BaselineOrigin);return new Geometry(glyph.Path.Transform(ConvertTo.Matrix(Matrix.CreateTranslation(run.BaselineOrigin.X,run.BaselineOrigin.Y))));}
    public IGlyphRunImpl CreateGlyphRun(GlyphTypeface typeface,double size,IReadOnlyList<GlyphInfo> infos,Point origin)=>new GlyphRun(typeface,size,infos,origin);
    public IRenderTargetBitmapImpl CreateRenderTargetBitmap(PixelSize size,Vector dpi)=>new Bitmap(new(size.Width,size.Height,PixelFormat.Bgra8888,AlphaFormat.Premultiplied),dpi);
    public IWriteableBitmapImpl CreateWriteableBitmap(PixelSize size,Vector dpi,AFormat format,AAlpha alpha)=>new Bitmap(new(size.Width,size.Height,ConvertTo.Format(format),ConvertTo.Alpha(alpha)),dpi);
    private static Bitmap Decode(Stream stream){using var memory=new MemoryStream();stream.CopyTo(memory);return new(ImageCodec.Decode(memory.GetBuffer().AsSpan(0,(int)memory.Length)),new(96,96));}
    public IBitmapImpl LoadBitmap(string path){using var stream=File.OpenRead(path);return Decode(stream);}
    public IBitmapImpl LoadBitmap(Stream stream)=>Decode(stream);
    public IWriteableBitmapImpl LoadWriteableBitmap(string path){using var stream=File.OpenRead(path);return Decode(stream);}
    public IWriteableBitmapImpl LoadWriteableBitmap(Stream stream)=>Decode(stream);
    private IWriteableBitmapImpl DecodeSized(Stream stream,int size,bool width,BitmapInterpolationMode mode)
    {
        if(size<=0)throw new ArgumentOutOfRangeException(nameof(size));using var original=Decode(stream);var s=original.PixelSize;
        return (IWriteableBitmapImpl)ResizeBitmap(original,width?new(size,Math.Max(1,(int)Math.Round((double)s.Height*size/s.Width))):new(Math.Max(1,(int)Math.Round((double)s.Width*size/s.Height)),size),mode);
    }
    public IWriteableBitmapImpl LoadWriteableBitmapToWidth(Stream stream,int width,BitmapInterpolationMode mode=BitmapInterpolationMode.HighQuality)=>DecodeSized(stream,width,true,mode);
    public IWriteableBitmapImpl LoadWriteableBitmapToHeight(Stream stream,int height,BitmapInterpolationMode mode=BitmapInterpolationMode.HighQuality)=>DecodeSized(stream,height,false,mode);
    public IBitmapImpl LoadBitmapToWidth(Stream stream,int width,BitmapInterpolationMode mode=BitmapInterpolationMode.HighQuality)=>DecodeSized(stream,width,true,mode);
    public IBitmapImpl LoadBitmapToHeight(Stream stream,int height,BitmapInterpolationMode mode=BitmapInterpolationMode.HighQuality)=>DecodeSized(stream,height,false,mode);
    public IBitmapImpl ResizeBitmap(IBitmapImpl source,PixelSize size,BitmapInterpolationMode mode=BitmapInterpolationMode.HighQuality)
    {
        var input=((Bitmap)source).Pixels;var pixels=new PixelBuffer(size.Width,size.Height,input.Format,input.Alpha);
        try
        {
            using var canvas=new RasterCanvas(pixels);canvas.Execute(new(CommandKind.DrawImage,new(0,0,size.Width,size.Height),Matrix3.Identity,new(new(255,255,255,255),CompositeMode.Source))
            {Image=input,ImageSource=new(0,0,input.Width,input.Height),Sampling=DrawingContext.Sampling(mode)});return new Bitmap(pixels,source.Dpi);
        }
        catch{pixels.Dispose();throw;}
    }
    public unsafe IBitmapImpl LoadBitmap(AFormat format,AAlpha alpha,IntPtr data,PixelSize size,Vector dpi,int stride)
    {
        if(data==IntPtr.Zero)throw new ArgumentNullException(nameof(data));var pixels=new PixelBuffer(size.Width,size.Height,ConvertTo.Format(format),ConvertTo.Alpha(alpha));
        try
        {if(stride<pixels.Width*4)throw new ArgumentOutOfRangeException(nameof(stride));for(int y=0;y<size.Height;y++)new ReadOnlySpan<byte>((byte*)data+checked((long)y*stride),pixels.Width*4).CopyTo(pixels.GetRowSpan(y));return new Bitmap(pixels,dpi);}
        catch{pixels.Dispose();throw;}
    }
    public IPlatformRenderInterfaceContext CreateBackendContext(IPlatformGraphicsContext? graphics)
    {if(graphics is not null)throw new NotSupportedException("Configure the OS host for framebuffer presentation; optional GPU drawing is selected by SimdSkia options.");return new RenderContext(backendFactory,nativeFactory);}
}

internal sealed class RenderContext(Func<PixelBuffer,ICanvasBackend>? backendFactory=null,Func<INativePlatformHandleSurface,IRenderTarget>? nativeFactory=null):IPlatformRenderInterfaceContext
{
    private bool disposed;
    public bool IsLost=>disposed;
    public IReadOnlyDictionary<Type,object> PublicFeatures {get;}=new Dictionary<Type,object>();
    public PixelSize? MaxOffscreenRenderTargetPixelSize=>new(16384,16384);
    public object? TryGetFeature(Type type)=>null;
    public IRenderTarget CreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        ObjectDisposedException.ThrowIf(disposed,this);var available=surfaces.ToArray();
        if(nativeFactory is not null)return nativeFactory(available.OfType<INativePlatformHandleSurface>().FirstOrDefault()??throw new NotSupportedException("Direct GPU presentation requires a native window surface."));
        var framebuffer=available.OfType<IFramebufferPlatformSurface>().FirstOrDefault()??throw new NotSupportedException("No software framebuffer surface was provided.");return new FramebufferTarget(framebuffer.CreateFramebufferRenderTarget(),backendFactory);
    }
    public IDrawingContextLayerImpl CreateOffscreenRenderTarget(PixelSize size,Vector scaling,bool antialias)
    {ObjectDisposedException.ThrowIf(disposed,this);return new Bitmap(PixelBuffer.Rent(size.Width,size.Height),scaling*96){ScaleToDpi=false};}
    public void Dispose()=>disposed=true;
}

internal sealed class FramebufferTarget(IFramebufferRenderTarget surface,Func<PixelBuffer,ICanvasBackend>? backendFactory=null):IRenderTarget
{
    private IFramebufferRenderTarget? surface=surface;
    private PixelBuffer? buffer;
    private ICanvasBackend? backend;
    private bool retained,active;
    public RenderTargetProperties Properties=>new(){RetainsPreviousFrameContents=true,IsSuitableForDirectRendering=true};
    public PlatformRenderTargetState PlatformRenderTargetState=>backend?.IsFaulted==true?new(){IsCorrupted=true}:surface?.State??PlatformRenderTargetState.Disposed;
    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo scene,out RenderTargetDrawingContextProperties properties)
    {
        ObjectDisposedException.ThrowIf(surface is null,this);if(active)throw new InvalidOperationException("A framebuffer drawing context is already active.");
        long started=SimdSkiaDiagnostics.FrameStarted();
        var locked=surface.Lock(scene,out _);
        try
        {
            var format=ConvertTo.Format(locked.Format);var alpha=ConvertTo.Alpha(locked.AlphaFormat);
            if(buffer is null||buffer.Width!=locked.Size.Width||buffer.Height!=locked.Size.Height||buffer.Format!=format||buffer.Alpha!=alpha)
            {backend?.Dispose();backend=null;buffer?.Dispose();buffer=new(locked.Size.Width,locked.Size.Height,format,alpha);retained=false;}
            if(backend is null&&backendFactory is not null)backend=backendFactory(buffer)??throw new InvalidOperationException("CanvasBackendFactory returned null.");
            properties=new(){PreviousFrameIsRetained=retained};active=true;var pixels=buffer;
            return new DrawingContext(pixels,locked.Dpi,false,success=>
            {
                bool copied=false;
                try{if(success){CopyTo(pixels,locked);retained=true;copied=true;}else retained=false;}
                finally
                {
                    active=false;try{locked.Dispose();}catch{retained=false;throw;}
                    if(copied){SimdSkiaDiagnostics.Presented(pixels);SimdSkiaDiagnostics.FrameCompleted(started);}
                }
            },backend);
        }
        catch{locked.Dispose();throw;}
    }
    private static unsafe void CopyTo(PixelBuffer pixels,ILockedFramebuffer framebuffer)
    {
        if(framebuffer.RowBytes<checked(pixels.Width*4)||framebuffer.Address==IntPtr.Zero)throw new InvalidOperationException("Invalid platform framebuffer.");
        for(int y=0;y<pixels.Height;y++)pixels.GetRowSpan(y)[..(pixels.Width*4)].CopyTo(new Span<byte>((byte*)framebuffer.Address+checked((long)y*framebuffer.RowBytes),pixels.Width*4));
    }
    public void Dispose(){if(active)throw new InvalidOperationException("Dispose the active framebuffer context first.");Interlocked.Exchange(ref backend,null)?.Dispose();Interlocked.Exchange(ref surface,null)?.Dispose();Interlocked.Exchange(ref buffer,null)?.Dispose();}
}
