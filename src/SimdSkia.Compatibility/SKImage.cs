using SimdSkia;

namespace SkiaSharp;

/// <summary>Immutable managed pixel snapshot. Bitmap conversions allocate independent storage.</summary>
public partial class SKImage : SKObject
{
    private readonly PixelBuffer? pixels;
    private readonly ImageGenerator? generated;
    private readonly SimdSkia.Gpu.IGpuImage? gpuImage;
    private SKData? encoded;
    private static int nextId;
    private readonly uint id=unchecked((uint)Interlocked.Increment(ref nextId));
    private readonly SKImageInfo info;
    internal PixelBuffer Buffer {get{ThrowIfDisposed();return pixels??generated!.Value;}}
    internal SKImage(SKImageInfo info,PixelBuffer pixels) { this.info=SKColorSpace.RetainInfo(info); this.pixels=pixels; }
    private SKImage(SKImageInfo info,PixelBuffer pixels,uint id):this(info,pixels)=>this.id=id;
    private SKImage(SKImageInfo info,Func<PixelBuffer> generate,IDisposable resources)
    {this.info=SKColorSpace.RetainInfo(info);generated=new(generate,resources);}
    private SKImage(SKImageInfo info,ImageGenerator generator,uint id)
    {this.info=SKColorSpace.RetainInfo(info);generated=generator.Retain();this.id=id;}
    public int Width { get { ThrowIfDisposed(); return info.Width; } }
    public int Height { get { ThrowIfDisposed(); return info.Height; } }
    public SKColorType ColorType { get { ThrowIfDisposed(); return info.ColorType; } }
    public SKAlphaType AlphaType { get { ThrowIfDisposed(); return info.AlphaType; } }
    public SKColorSpace? ColorSpace { get { ThrowIfDisposed(); return info.ColorSpace; } }
    public static SKImage FromBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return new(bitmap.Info,bitmap.IsImmutable?bitmap.Buffer.Share():bitmap.Buffer.Clone());
    }
    internal SKBitmap CopyBitmap() { ThrowIfDisposed(); return new(info,Buffer.Clone()); }
    public SKData Encode(SKEncodedImageFormat format,int quality)
    {ThrowIfDisposed();if(quality is <0 or >100)throw new ArgumentOutOfRangeException(nameof(quality));if(info.ColorSpace is {} space&&!space.IsSrgb)throw new NotSupportedException("Tagged image encoding requires ICC serialization; convert to sRGB first.");return new(SimdSkia.Imaging.ImageCodec.Encode(Buffer,CodecFormats.ToEngine(format),quality));}
    protected override void Dispose(bool disposing) {try{pixels?.Dispose();generated?.Dispose();}finally{encoded?.Dispose();encoded=null;}}
    // Lazy image wrappers share both the one-time pixel generation and its
    // command/resource lease. Disposing one wrapper cannot invalidate another.
    private sealed class ImageGenerator(Func<PixelBuffer> generate,IDisposable resources):IDisposable
    {
        private readonly Lazy<PixelBuffer> value=new(generate,LazyThreadSafetyMode.ExecutionAndPublication);
        private int references=1;
        internal PixelBuffer Value=>value.Value;
        internal ImageGenerator Retain(){Interlocked.Increment(ref references);return this;}
        public void Dispose(){if(Interlocked.Decrement(ref references)!=0)return;try{if(value.IsValueCreated)value.Value.Dispose();}finally{resources.Dispose();}}
    }
}
