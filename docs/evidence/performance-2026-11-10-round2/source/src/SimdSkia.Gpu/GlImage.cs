using System.Runtime.InteropServices;
namespace SimdSkia.Gpu;

// This is a real GL texture lease, never a stand-in for SKObject.Handle.
internal sealed unsafe class GlImage:IGpuImage
{
    internal GlDevice Device {get;}
    internal int Width {get;}
    internal int Height {get;}
    internal bool Flip {get;}
    internal AlphaFormat Alpha {get;}
    internal bool Mipmapped {get;}
    internal ResourceLease? Lease {get;set;}
    private uint texture;
    private readonly bool owned;
    private Action? release;
    private bool disposed;
    internal bool IsDisposed=>disposed;
    internal uint Texture {get{ObjectDisposedException.ThrowIf(disposed,this);Device.Check();if(texture==0)throw new InvalidOperationException("The texture's context was released or abandoned.");return texture;}}
    internal GlImage(GlDevice device,uint texture,int width,int height,bool flip,AlphaFormat alpha,bool owned,bool mipmapped,Action? release=null)
    {Device=device;this.texture=texture;Width=width;Height=height;Flip=flip;Alpha=alpha;this.owned=owned;Mipmapped=mipmapped;this.release=release;device.Register(this);}
    internal static GlImage Import(GlDevice device,uint texture,int width,int height,bool flip,AlphaFormat alpha,bool owned,bool mipmapped,Action? release)
    {
        device.Check();if(width<=0||height<=0||width>device.MaximumSize||height>device.MaximumSize)throw new ArgumentOutOfRangeException(nameof(width));
        if(texture==0||((delegate* unmanaged[Stdcall]<uint,byte>)device.P("glIsTexture"))(texture)==0)throw new ArgumentException("The supplied texture does not exist in the current GL share group.",nameof(texture));
        // Framebuffer completeness validates that this is an actual renderable
        // 2D image. Never infer validity from a nonzero object name alone.
        uint framebuffer=0;((delegate* unmanaged[Stdcall]<int,uint*,void>)device.P("glGenFramebuffers"))(1,&framebuffer);
        try{((delegate* unmanaged[Stdcall]<uint,uint,void>)device.P("glBindFramebuffer"))(0x8D40,framebuffer);((delegate* unmanaged[Stdcall]<uint,uint,uint,uint,int,void>)device.P("glFramebufferTexture2D"))(0x8D40,0x8CE0,0x0DE1,texture,0);if(((delegate* unmanaged[Stdcall]<uint,uint>)device.P("glCheckFramebufferStatus"))(0x8D40)!=0x8CD5)throw new NotSupportedException("The imported GL texture is not a renderable RGBA image.");device.CheckError();}
        finally{((delegate* unmanaged[Stdcall]<int,uint*,void>)device.P("glDeleteFramebuffers"))(1,&framebuffer);}
        return new(device,texture,width,height,flip,alpha,owned,mipmapped,release);
    }
    internal PixelBuffer ReadPixels()
    {
        uint value=Texture,framebuffer=0;var result=PixelBuffer.Rent(Width,Height,PixelFormat.Rgba8888,Alpha);((delegate* unmanaged[Stdcall]<int,uint*,void>)Device.P("glGenFramebuffers"))(1,&framebuffer);
        try
        {
            ((delegate* unmanaged[Stdcall]<uint,uint,void>)Device.P("glBindFramebuffer"))(0x8D40,framebuffer);((delegate* unmanaged[Stdcall]<uint,uint,uint,uint,int,void>)Device.P("glFramebufferTexture2D"))(0x8D40,0x8CE0,0x0DE1,value,0);
            using(var pin=result.Pin()){Device.PixelStorage(true);((delegate* unmanaged[Stdcall]<int,int,int,int,uint,uint,nint,void>)Device.P("glReadPixels"))(0,0,Width,Height,0x1908,0x1401,pin.Address);}Device.CheckError();
            if(Flip){using var row=PixelBuffer.Rent(Width,1);for(int y=0;y<Height/2;y++){var a=result.GetRowSpan(y);var b=result.GetRowSpan(Height-1-y);a.CopyTo(row.GetRowSpan(0));b.CopyTo(a);row.GetRowSpan(0).CopyTo(b);}}
            if(Alpha==AlphaFormat.Opaque)for(int y=0;y<Height;y++){var row=result.GetRowSpan(y);for(int x=0;x<Width;x++)row[x*4+3]=255;}
            return result;
        }
        catch{result.Dispose();throw;}
        finally{((delegate* unmanaged[Stdcall]<int,uint*,void>)Device.P("glDeleteFramebuffers"))(1,&framebuffer);}
    }
    PixelBuffer IGpuImage.ReadPixels()=>ReadPixels();
    internal void ReleaseDeviceResource(bool destroy)
    {uint value=texture;texture=0;if(owned&&destroy&&value!=0)Device.DeleteTexture(ref value);}
    public void Dispose()
    {if(disposed)return;if(texture!=0)Device.Check();try{ReleaseDeviceResource(true);}finally{disposed=true;Interlocked.Exchange(ref release,null)?.Invoke();}}
}
