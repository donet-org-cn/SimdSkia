using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SimdSkia.Imaging;

namespace SimdSkia.Avalonia;

internal sealed class Bitmap:IRenderTargetBitmapImpl,IWriteableBitmapImpl,IDrawingContextLayerImpl
{
    private PixelBuffer? pixels;
    private int version;
    internal PixelBuffer Pixels=>pixels??throw new ObjectDisposedException(nameof(Bitmap));
    public Vector Dpi {get;}
    public PixelSize PixelSize {get;}
    public int Version=>Volatile.Read(ref version);
    public global::Avalonia.Platform.PixelFormat? Format=>ConvertTo.Format(Pixels.Format);
    public global::Avalonia.Platform.AlphaFormat? AlphaFormat=>ConvertTo.Alpha(Pixels.Alpha);
    public bool CanBlit=>true;
    public bool IsCorrupted=>pixels is null;
    internal bool ScaleToDpi {get;init;}=true;
    internal Bitmap(PixelBuffer pixels,Vector dpi)
    {if(!double.IsFinite(dpi.X)||!double.IsFinite(dpi.Y)||dpi.X<=0||dpi.Y<=0)throw new ArgumentOutOfRangeException(nameof(dpi));this.pixels=pixels;Dpi=dpi;PixelSize=new(pixels.Width,pixels.Height);}
    public void Save(Stream stream,BitmapEncoderOptions options)
    {
        if(options is not PngBitmapEncoderOptions png)throw new NotSupportedException("The managed backend currently encodes PNG only.");
        if(png.CompressionLevel!=System.IO.Compression.CompressionLevel.NoCompression&&png.CompressionLevel!=System.IO.Compression.CompressionLevel.Optimal)
            throw new NotSupportedException("The managed PNG writer currently supports stored DEFLATE; custom compression levels are not implemented.");
        stream.Write(PngCodec.Encode(Pixels));
    }
    public ILockedFramebuffer Lock()=>new Locked(Pixels,Dpi,()=>Interlocked.Increment(ref version));
    public IDrawingContextImpl CreateDrawingContext()
    {Interlocked.Increment(ref version);return new DrawingContext(Pixels,Dpi,ScaleToDpi);}
    public void Blit(IDrawingContextImpl context)=>((DrawingContext)context).BlitDevice(this);
    public void Dispose()=>Interlocked.Exchange(ref pixels,null)?.Dispose();
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
