namespace SimdSkia;

/// <summary>Owned, contiguous bytes. Rows may have padding. Access is single-threaded.</summary>
public sealed class PixelBuffer : IDisposable
{
    private byte[]? data;
    private PixelStorage? storage;
    private ExternalPixels? external;
    private ResourceLease? resource;
    public int Width { get; }
    public int Height { get; }
    public int RowBytes { get; }
    public PixelFormat Format { get; }
    public AlphaFormat Alpha { get; }
    public bool IsDisposed => data is null&&external is null;
    public int BytesPerPixel=>Format==PixelFormat.Alpha8?1:4;
    public int ByteCount => checked(RowBytes * (Height-1) + Width * BytesPerPixel);

    public PixelBuffer(int width, int height, PixelFormat format, AlphaFormat alpha, int rowBytes = 0)
        :this(width,height,format,alpha,rowBytes,false) { }

    // Only decoders that initialize every visible byte may rent uninitialized storage.
    internal static PixelBuffer RentRgba(int width,int height)=>new(width,height,PixelFormat.Rgba8888,AlphaFormat.Straight,0,true);
    /// <summary>Rents bounded reusable storage and initializes all bytes before exposing it.</summary>
    public static PixelBuffer Rent(int width,int height,PixelFormat format=PixelFormat.Bgra8888,AlphaFormat alpha=AlphaFormat.Premultiplied)
    {
        var buffer=new PixelBuffer(width,height,format,alpha,0,true);buffer.data!.AsSpan(0,checked(buffer.RowBytes*buffer.Height)).Clear();
        if(alpha==AlphaFormat.Opaque)for(int y=0;y<height;y++)for(int x=0;x<width;x++)buffer.data![y*buffer.RowBytes+x*buffer.BytesPerPixel+buffer.BytesPerPixel-1]=255;
        return buffer;
    }

    private PixelBuffer(int width,int height,PixelFormat format,AlphaFormat alpha,int rowBytes,bool pooled)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        if (!Enum.IsDefined(alpha)) throw new ArgumentOutOfRangeException(nameof(alpha));
        int minimum = checked(width * (format==PixelFormat.Alpha8?1:4));
        if (rowBytes == 0) rowBytes = minimum;
        if (rowBytes < minimum) throw new ArgumentOutOfRangeException(nameof(rowBytes));
        Width = width; Height = height; RowBytes = rowBytes; Format = format; Alpha = alpha;
        int length=checked(rowBytes*height);
        if(pooled){storage=PixelStorage.Rent(length);data=storage.Bytes;}
        else data = new byte[length];
        if (alpha == AlphaFormat.Opaque)
            for (int y=0; y<height; y++) for (int x=0; x<width; x++) data[y*rowBytes+x*BytesPerPixel+BytesPerPixel-1] = 255;
    }

    // External framebuffer/video memory is represented by a bounded Span only at
    // this boundary. Every view/pin retains one shared lease and releases it once.
    internal static PixelBuffer Wrap(int width,int height,PixelFormat format,AlphaFormat alpha,int stride,IntPtr address,Action? release=null)
        =>new(width,height,format,alpha,stride,address,release);
    private PixelBuffer(int width,int height,PixelFormat format,AlphaFormat alpha,int stride,IntPtr address,Action? release)
    {
        if(width<=0||height<=0||stride<checked(width*(format==PixelFormat.Alpha8?1:4))||address==IntPtr.Zero)throw new ArgumentOutOfRangeException(nameof(width));
        if(!Enum.IsDefined(format)||!Enum.IsDefined(alpha))throw new ArgumentOutOfRangeException(nameof(format));
        Width=width;Height=height;RowBytes=stride;Format=format;Alpha=alpha;_=ByteCount;external=new(address,release);
    }
    private sealed class ExternalPixels(IntPtr address,Action? release)
    {
        internal readonly IntPtr Address=address;private Action? release=release;private int references=1,freed;
        internal void Retain()=>Interlocked.Increment(ref references);
        internal void Release(){if(Interlocked.Decrement(ref references)==0){Free();GC.SuppressFinalize(this);}}
        private void Free(){if(Interlocked.Exchange(ref freed,1)==0)Interlocked.Exchange(ref release,null)?.Invoke();}
        ~ExternalPixels(){try{Free();}catch{/* A user callback cannot escape the finalizer thread. */}}
    }
    private PixelBuffer(PixelBuffer source)
    {
        source.ThrowIfDisposed();
        Width=source.Width; Height=source.Height; RowBytes=source.RowBytes; Format=source.Format; Alpha=source.Alpha;
        data=source.data;
        storage=source.storage;storage?.Retain();external=source.external;external?.Retain();resource=source.resource?.Retain();
    }
    // The decoder has already converted every byte in place. Relabel a retained
    // lease without allocating a second full image; never expose this as a cast API.
    internal PixelBuffer Reinterpret(PixelFormat format,AlphaFormat alpha)=>new(this,format,alpha);
    private PixelBuffer(PixelBuffer source,PixelFormat format,AlphaFormat alpha):this(source)
    {Format=format;Alpha=alpha;}
    /// <summary>Independent lifetime over the same managed storage, retained by the GC.</summary>
    public PixelBuffer Share() => new(this);
    internal void RetainResource(ResourceLease lease)
    {ThrowIfDisposed();if(resource is not null)throw new InvalidOperationException("A pixel view already retains its resource.");resource=lease.Retain();}

    /// <summary>Pins a retained storage lease until disposed; used only at platform framebuffer boundaries.</summary>
    public PixelBufferPin Pin() { ThrowIfDisposed(); return external is null?new(this,data!):new(this,external.Address); }

    public Span<byte> GetRowSpan(int y)
    {
        ThrowIfDisposed();
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
        return external is null?data.AsSpan(y*RowBytes,RowBytes):GetPixelSpan().Slice(y*RowBytes,y==Height-1?Width*BytesPerPixel:RowBytes);
    }

    public unsafe Span<byte> GetPixelSpan() { ThrowIfDisposed(); return external is null?data.AsSpan(0,ByteCount):new Span<byte>((void*)external.Address,ByteCount); }
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
        if(Format==PixelFormat.Alpha8)return new(0,0,0,GetRowSpan(y)[x]);
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
        if(Format==PixelFormat.Alpha8){GetRowSpan(y)[x]=color.A;return;}
        var p = GetRowSpan(y).Slice(x*4, 4);
        byte a = color.A;
        byte r=color.R, g=color.G, b=color.B;
        if (Alpha != AlphaFormat.Straight) { r=Multiply(r,a); g=Multiply(g,a); b=Multiply(b,a); }
        p[Format == PixelFormat.Rgba8888 ? 0 : 2]=r; p[1]=g; p[Format == PixelFormat.Rgba8888 ? 2 : 0]=b; p[3]=a;
    }

    public static byte Multiply(byte value, byte alpha) => (byte)((value * alpha + 127) / 255);
    public static byte Unmultiply(byte value, byte alpha) => alpha == 0 ? (byte)0 : (byte)Math.Min(255, (value * 255 + alpha / 2) / alpha);
    public void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
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
        Interlocked.Exchange(ref external,null)?.Release();
        Interlocked.Exchange(ref resource,null)?.Dispose();
    }
}
