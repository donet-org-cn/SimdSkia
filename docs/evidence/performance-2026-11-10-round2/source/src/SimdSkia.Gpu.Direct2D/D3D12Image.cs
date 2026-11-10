using System.Runtime.Versioning;
using SimdSkia.Gpu;
namespace SimdSkia.Gpu.Direct2D;

[SupportedOSPlatform("windows")]
internal sealed unsafe class D3D12Image:IGpuImage
{
    internal D3D12Device Device {get;}
    internal int Width {get;}
    internal int Height {get;}
    internal uint Format {get;}
    internal AlphaFormat Alpha {get;}
    internal bool Flip {get;}
    internal nint Wrapped {get{ObjectDisposedException.ThrowIf(disposed,this);Device.Check();return wrapped!=0?wrapped:throw new InvalidOperationException("The D3D12 texture's context was released.");}}
    private nint resource,wrapped,staging;
    private bool acquired,disposed;
    internal bool IsDisposed=>disposed;
    internal nint ResourceHandle {get{_=Wrapped;return resource;}}
    private Action? release;
    internal D3D12Image(D3D12Device device,nint resource,uint state,uint format,int width,int height,bool flip,AlphaFormat alpha,bool adopted,bool renderTarget,Action? release=null)
    {
        Device=device;Width=width;Height=height;Format=format;Flip=flip;Alpha=alpha;this.release=release;
        if(resource==0||format is not (28 or 87)||width<=0||height<=0||width>16384||height>16384||(long)width*height*4>128*1024*1024)throw new NotSupportedException("D3D12 images require a live single-sample RGBA/BGRA8888 2D resource within the memory budget.");
        try{wrapped=device.Wrap(resource,state,renderTarget?0x28u:8u);D2DNative.TextureDescription description=default;((delegate* unmanaged[Stdcall]<nint,D2DNative.TextureDescription*,void>)D2DNative.Slot(wrapped,10))(wrapped,&description);if(description.Width!=(uint)width||description.Height!=(uint)height||description.Format!=format||description.SampleCount!=1||description.Array!=1||description.Mips!=1)throw new ArgumentException("The actual D3D12 texture does not match its descriptor.");this.resource=adopted?resource:D2DNative.Retain(resource);device.Register(this);}
        catch{D2DNative.Release(ref wrapped);throw;}
    }
    internal void Acquire(){_=Wrapped;if(!acquired){Device.Acquire(wrapped);acquired=true;}}
    internal void Release(){if(acquired){Device.ReleaseWrapped(wrapped);acquired=false;}}
    public PixelBuffer ReadPixels()
    {
        Acquire();var result=PixelBuffer.Rent(Width,Height,Format==87?PixelFormat.Bgra8888:PixelFormat.Rgba8888,Alpha);
        try
        {
            staging=staging==0?D2DNative.Texture(Device.DrawingDevice,Width,Height,true,Format):staging;D2DNative.Copy(Device.Immediate,staging,wrapped);var map=D2DNative.Map(Device.Immediate,staging);
            try{for(int y=0;y<Height;y++)new ReadOnlySpan<byte>((byte*)map.Data+(long)(Flip?Height-1-y:y)*map.RowPitch,Width*4).CopyTo(result.GetRowSpan(y));}finally{D2DNative.Unmap(Device.Immediate,staging);}Release();return result;
        }
        catch{result.Dispose();throw;}
    }
    internal void Write(PixelBuffer pixels)
    {
        if(pixels.Width!=Width||pixels.Height!=Height||pixels.Format==PixelFormat.Alpha8)throw new ArgumentException("D3D12 texture pixel dimensions/format differ.");Acquire();using var canonical=PixelBuffer.Rent(Width,Height,Format==87?PixelFormat.Bgra8888:PixelFormat.Rgba8888,pixels.Alpha);
        for(int y=0;y<Height;y++){var from=pixels.GetRowSpan(y)[..(Width*4)];var target=canonical.GetRowSpan(Flip?Height-1-y:y);if(pixels.Format==canonical.Format)from.CopyTo(target);else SimdSkia.Cpu.PixelTransferKernel.SwapRedBlue(from,target,SimdSkia.Cpu.CpuOptions.EnableSimd);}using var pin=canonical.Pin();D2DNative.Upload(Device.Immediate,wrapped,pin.Address,canonical.RowBytes);Release();
    }
    internal void ReleaseDeviceResource(bool complete){if(complete)Release();else acquired=false;D2DNative.Release(ref staging);D2DNative.Release(ref wrapped);D2DNative.Release(ref resource);}
    public void Dispose(){if(disposed)return;if(wrapped!=0)Device.Check();try{ReleaseDeviceResource(true);}finally{disposed=true;Interlocked.Exchange(ref release,null)?.Invoke();}}
}
