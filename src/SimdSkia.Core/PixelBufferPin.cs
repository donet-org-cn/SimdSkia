using System.Runtime.InteropServices;

namespace SimdSkia;

/// <summary>A pinned pixel lease retains storage independently of its original buffer.</summary>
public sealed class PixelBufferPin : IDisposable
{
    private PixelBuffer? owner;
    private GCHandle pin;
    internal PixelBufferPin(PixelBuffer owner,byte[] data)
    {this.owner=owner.Share();try{pin=GCHandle.Alloc(data,GCHandleType.Pinned);}catch{this.owner.Dispose();this.owner=null;throw;}}
    public IntPtr Address=>owner is null?throw new ObjectDisposedException(nameof(PixelBufferPin)):pin.AddrOfPinnedObject();
    public void Dispose(){var held=Interlocked.Exchange(ref owner,null);if(held is null)return;pin.Free();held.Dispose();GC.SuppressFinalize(this);}
    ~PixelBufferPin()=>Dispose();
}
