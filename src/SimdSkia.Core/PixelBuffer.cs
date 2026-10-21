namespace SimdSkia;

/// <summary>Owned, contiguous bytes. Rows may have padding. Access is single-threaded.</summary>
public sealed class PixelBuffer : IDisposable
{
    private byte[]? data;
    private PixelStorage? storage;
    public int Width { get; }
    public int Height { get; }
    public int RowBytes { get; }
    public PixelFormat Format { get; }
    public AlphaFormat Alpha { get; }
    public bool IsDisposed => data is null;
    public int ByteCount => checked(RowBytes * (Height-1) + Width * 4);

    public PixelBuffer(int width, int height, PixelFormat format, AlphaFormat alpha, int rowBytes = 0)
        :this(width,height,format,alpha,rowBytes,false) { }

    // Only decoders that initialize every visible byte may rent uninitialized storage.
    internal static PixelBuffer RentRgba(int width,int height)=>new(width,height,PixelFormat.Rgba8888,AlphaFormat.Straight,0,true);
    /// <summary>Rents bounded reusable storage and initializes all bytes before exposing it.</summary>
    public static PixelBuffer Rent(int width,int height,PixelFormat format=PixelFormat.Bgra8888,AlphaFormat alpha=AlphaFormat.Premultiplied)
    {
        var buffer=new PixelBuffer(width,height,format,alpha,0,true);buffer.data!.AsSpan(0,checked(buffer.RowBytes*buffer.Height)).Clear();
        if(alpha==AlphaFormat.Opaque)for(int y=0;y<height;y++)for(int x=0;x<width;x++)buffer.data![y*buffer.RowBytes+x*4+3]=255;
        return buffer;
    }

    private PixelBuffer(int width,int height,PixelFormat format,AlphaFormat alpha,int rowBytes,bool pooled)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        if (!Enum.IsDefined(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));
        int minimum = checked(width * 4);
        if (rowBytes == 0) rowBytes = minimum;
        if (rowBytes < minimum) throw new ArgumentOutOfRangeException(nameof(rowBytes));
        Width = width; Height = height; RowBytes = rowBytes; Format = format; Alpha = alpha;
        int length=checked(rowBytes*height);
        if(pooled){storage=PixelStorage.Rent(length);data=storage.Bytes;}
        else data = new byte[length];
        if (alpha == AlphaFormat.Opaque)
            for (int y=0; y<height; y++) for (int x=0; x<width; x++) data[y*rowBytes+x*4+3] = 255;
    }

    private PixelBuffer(PixelBuffer source)
    {
        source.ThrowIfDisposed();
        Width=source.Width; Height=source.Height; RowBytes=source.RowBytes; Format=source.Format; Alpha=source.Alpha;
        data=source.data;
        storage=source.storage;storage?.Retain();
    }
    /// <summary>Independent lifetime over the same managed storage, retained by the GC.</summary>
    public PixelBuffer Share() => new(this);

    /// <summary>Pins a retained storage lease until disposed; used only at platform framebuffer boundaries.</summary>
    public PixelBufferPin Pin() { ThrowIfDisposed(); return new(this,data!); }

    public Span<byte> GetRowSpan(int y)
    {
        ThrowIfDisposed();
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
        return data.AsSpan(y * RowBytes, RowBytes);
    }

    public Span<byte> GetPixelSpan() { ThrowIfDisposed(); return data.AsSpan(0,ByteCount); }
    public byte[] CopyBytes() => GetPixelSpan().ToArray();
    public PixelBuffer Clone()
    {
        ThrowIfDisposed();
        var copy=new PixelBuffer(Width,Height,Format,Alpha,RowBytes);
        GetPixelSpan().CopyTo(copy.GetPixelSpan());
        return copy;
    }

    public Rgba32 ReadPixel(int x, int y)
    {
        ValidatePixel(x, y);
        var p = GetRowSpan(y).Slice(x*4, 4);
        byte r = p[Format == PixelFormat.Rgba8888 ? 0 : 2], g = p[1], b = p[Format == PixelFormat.Rgba8888 ? 2 : 0];
        // Opaque is metadata; Skia's Src/Clear may still write a nonopaque physical alpha.
        byte a = p[3];
        if (Alpha == AlphaFormat.Premultiplied) { r=Unmultiply(r,a); g=Unmultiply(g,a); b=Unmultiply(b,a); }
        return new(r,g,b,a);
    }

    public void WritePixel(int x, int y, Rgba32 color)
    {
        ValidatePixel(x, y);
        var p = GetRowSpan(y).Slice(x*4, 4);
        byte a = color.A;
        byte r=color.R, g=color.G, b=color.B;
        if (Alpha != AlphaFormat.Straight) { r=Multiply(r,a); g=Multiply(g,a); b=Multiply(b,a); }
        p[Format == PixelFormat.Rgba8888 ? 0 : 2]=r; p[1]=g; p[Format == PixelFormat.Rgba8888 ? 2 : 0]=b; p[3]=a;
    }

    public static byte Multiply(byte value, byte alpha) => (byte)((value * alpha + 127) / 255);
    public static byte Unmultiply(byte value, byte alpha) => alpha == 0 ? (byte)0 : (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);
    public void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(data is null, this);
    private void ValidatePixel(int x, int y)
    {
        ThrowIfDisposed();
        if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
    }
    public void Dispose()
    {
        data=null;
        Interlocked.Exchange(ref storage,null)?.Release();
    }
}
