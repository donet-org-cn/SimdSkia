namespace SimdSkia.Gpu;

// Real MTLTexture ownership. Private GPU textures are transferred by a blit
// encoder into aligned shared buffers and waited before any CPU pixel access.
// Runtime/ABI/quality validation requires a physical Mac Metal device.
internal sealed unsafe class MetalImage:IGpuImage
{
    internal MetalDevice Device {get;}
    internal int Width {get;}
    internal int Height {get;}
    internal AlphaFormat Alpha {get;}
    internal PixelFormat Format {get;}
    private readonly bool flip;
    private readonly int rowBytes;
    private nint texture,staging;
    private bool disposed;
    internal bool IsDisposed=>disposed;
    private Action? release;
    internal MetalImage(MetalDevice device,nint texture,int width,int height,bool bgra,bool flip,AlphaFormat alpha,bool adopted,Action? release=null)
    {
        Device=device;Width=width;Height=height;Format=bgra?PixelFormat.Bgra8888:PixelFormat.Rgba8888;this.flip=flip;Alpha=alpha;this.release=release;rowBytes=checked((width*4+255)&~255);
        device.ImportTexture(texture,width,height);if(MetalApi.Number(texture,"pixelFormat")!=(bgra?80u:70u))throw new NotSupportedException("Metal texture must use the declared RGBA/BGRA8Unorm format; sRGB/extended/color-managed formats require a separate pipeline.");this.texture=adopted?texture:MetalApi.Retain(texture);device.Register(this);
    }
    private void Check(){ObjectDisposedException.ThrowIf(disposed,this);Device.Check();if(texture==0)throw new InvalidOperationException("The Metal texture's context was released.");}
    private void Transfer(bool write)
    {
        Check();staging=staging==0?Device.CreateBuffer(checked(rowBytes*Height)):staging;using var pool=new MetalApi.Pool(true);nint commands=Device.Commands();
        try{nint encoder=MetalApi.Call(commands,"blitCommandEncoder");if(encoder==0)throw new InvalidOperationException("Metal blit encoder allocation failed.");try{if(write)MetalApi.BufferToTexture(encoder,staging,texture,(nuint)Width,(nuint)Height,(nuint)rowBytes);else MetalApi.TextureToBuffer(encoder,texture,staging,(nuint)Width,(nuint)Height,(nuint)rowBytes);}finally{MetalApi.End(encoder,"endEncoding");}Device.Complete(commands);}
        finally{MetalApi.Release(ref commands);}
    }
    public PixelBuffer ReadPixels()
    {
        Transfer(false);var result=PixelBuffer.Rent(Width,Height,Format,Alpha);
        try{var source=new ReadOnlySpan<byte>((void*)MetalApi.Call(staging,"contents"),checked(rowBytes*Height));for(int y=0;y<Height;y++)source.Slice((flip?Height-1-y:y)*rowBytes,Width*4).CopyTo(result.GetRowSpan(y));return result;}catch{result.Dispose();throw;}
    }
    internal void Write(PixelBuffer pixels)
    {
        Check();if(pixels.Width!=Width||pixels.Height!=Height||pixels.Format==PixelFormat.Alpha8)throw new ArgumentException("Metal texture pixel dimensions/format differ.");staging=staging==0?Device.CreateBuffer(checked(rowBytes*Height)):staging;var target=new Span<byte>((void*)MetalApi.Call(staging,"contents"),checked(rowBytes*Height));
        for(int y=0;y<Height;y++){var row=target.Slice((flip?Height-1-y:y)*rowBytes,Width*4);var input=pixels.GetRowSpan(y)[..(Width*4)];if(pixels.Format==Format)input.CopyTo(row);else SimdSkia.Cpu.PixelTransferKernel.SwapRedBlue(input,row,SimdSkia.Cpu.CpuOptions.EnableSimd);if(pixels.Alpha==AlphaFormat.Opaque)for(int x=0;x<Width;x++)row[x*4+3]=255;}Transfer(true);
    }
    internal void ReleaseDeviceResource(){MetalApi.Release(ref staging);MetalApi.Release(ref texture);}
    public void Dispose(){if(disposed)return;if(texture!=0)Device.Check();try{ReleaseDeviceResource();}finally{disposed=true;Interlocked.Exchange(ref release,null)?.Invoke();}}
}
