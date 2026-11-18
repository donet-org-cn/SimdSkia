using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SimdSkia.Imaging;
using SimdSkia.Cpu;

namespace SimdSkia.Avalonia;

internal sealed class Bitmap:IRenderTargetBitmapImpl,IWriteableBitmapImpl,IDrawingContextLayerImpl
{
    private PixelBuffer? pixels;
    private long version;
    private int openLocks, usedAsCanvas;
    private readonly object canvasGate=new();
    private RasterCanvas? reusableCanvas;
    private PixelBuffer? canvasPixels;
    private bool canvasInUse;
    private Action<bool>? releaseCanvas;
    private Action? unlock;
    internal PixelBuffer Pixels=>pixels??throw new ObjectDisposedException(nameof(Bitmap));
    public Vector Dpi {get;}
    public PixelSize PixelSize {get;}
    public int Version=>unchecked((int)Volatile.Read(ref version));
    internal long ImageContentVersion => Volatile.Read(ref usedAsCanvas) != 0 ? 0 :
        Volatile.Read(ref openLocks) != 0 ? -1 : Volatile.Read(ref version);
    public global::Avalonia.Platform.PixelFormat? Format=>ConvertTo.Format(Pixels.Format);
    public global::Avalonia.Platform.AlphaFormat? AlphaFormat=>ConvertTo.Alpha(Pixels.Alpha);
    public bool CanBlit=>true;
    public bool IsCorrupted=>pixels is null;
    internal bool ScaleToDpi {get;init;}=true;
    internal Bitmap(PixelBuffer pixels,Vector dpi)
    {if(!double.IsFinite(dpi.X)||!double.IsFinite(dpi.Y)||dpi.X<=0||dpi.Y<=0)throw new ArgumentOutOfRangeException(nameof(dpi));this.pixels=pixels;Dpi=dpi;PixelSize=new(pixels.Width,pixels.Height);}
    public void Save(Stream stream,BitmapEncoderOptions options)
    {
        if(options is JpegBitmapEncoderOptions jpeg){stream.Write(JpegCodec.Encode(Pixels,jpeg.Quality));return;}
        if(options is not PngBitmapEncoderOptions png)throw new NotSupportedException("Managed encoding supports PNG and JPEG.");
        stream.Write(PngCodec.Encode(Pixels,png.CompressionLevel));
    }
    public ILockedFramebuffer Lock()
    {
        var frame = new Locked(Pixels, Dpi, unlock ??= Unlock);
        Interlocked.Increment(ref openLocks);
        return frame;
    }
    private void Unlock()
    {
        Interlocked.Increment(ref version);
        Interlocked.Decrement(ref openLocks);
    }
    public IDrawingContextImpl CreateDrawingContext()
    {
        lock(canvasGate)
        {
            var pixels=Pixels;
            // Drawing contexts may overlap or stay open between draws. Their
            // writes use ordinary exact-content cache validation.
            Volatile.Write(ref usedAsCanvas, 1);
            Interlocked.Increment(ref version);
            // Overlapping contexts keep independent rasterizers. The common
            // sequential case retains scanline scratch and immutable caches.
            if(canvasInUse)return new DrawingContext(pixels,Dpi,ScaleToDpi);
            canvasPixels??=pixels.Share();reusableCanvas??=new RasterCanvas(canvasPixels);canvasInUse=true;
            // Dispose keeps this shared view alive until ReleaseCanvas, including
            // when the owning bitmap is closed before the drawing context.
            try{return new DrawingContext(canvasPixels,Dpi,ScaleToDpi,releaseCanvas??=ReleaseCanvas,reusableCanvas,flushBackend:true,borrowPixels:true);}
            catch{canvasInUse=false;throw;}
        }
    }
    private void ReleaseCanvas(bool success)
    {lock(canvasGate){canvasInUse=false;if(pixels is null)DisposeCanvas();}}
    private void DisposeCanvas(){reusableCanvas?.Dispose();reusableCanvas=null;canvasPixels?.Dispose();canvasPixels=null;}
    public void Blit(IDrawingContextImpl context)=>((DrawingContext)context).BlitDevice(this);
    public void Dispose(){lock(canvasGate){Interlocked.Exchange(ref pixels,null)?.Dispose();if(!canvasInUse)DisposeCanvas();}}
    internal sealed class Locked:ILockedFramebuffer
    {
        private PixelBufferPin? pin;
        private Action? completed;
        public IntPtr Address=>pin?.Address??throw new ObjectDisposedException(nameof(Locked));
        public PixelSize Size {get;}
        public int RowBytes {get;}
        public Vector Dpi {get;}
        public global::Avalonia.Platform.PixelFormat Format {get;}
        public global::Avalonia.Platform.AlphaFormat AlphaFormat {get;}
        internal Locked(PixelBuffer pixels,Vector dpi,Action completed)
        {pin=pixels.Pin();Size=new(pixels.Width,pixels.Height);RowBytes=pixels.RowBytes;Dpi=dpi;Format=ConvertTo.Format(pixels.Format);AlphaFormat=ConvertTo.Alpha(pixels.Alpha);this.completed=completed;}
        public void Dispose(){Interlocked.Exchange(ref pin,null)?.Dispose();Interlocked.Exchange(ref completed,null)?.Invoke();}
    }
}
