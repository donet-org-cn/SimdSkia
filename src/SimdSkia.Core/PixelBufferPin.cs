using System.Runtime.InteropServices;

namespace SimdSkia;

/// <summary>A pinned pixel lease retains storage independently of its original buffer.</summary>
public sealed class PixelBufferPin : IDisposable
{
    private PixelBuffer? owner;
    private GCHandle pin;
    private readonly IntPtr address;
    internal PixelBufferPin(PixelBuffer owner,byte[] data)
    {this.owner=owner.Share();try{pin=GCHandle.Alloc(data,GCHandleType.Pinned);}catch{this.owner.Dispose();this.owner=null;throw;}}
    internal PixelBufferPin(PixelBuffer owner,IntPtr address){this.owner=owner.Share();this.address=address;}
    public IntPtr Address=>owner is null?throw new ObjectDisposedException(nameof(PixelBufferPin)):address!=IntPtr.Zero?address:pin.AddrOfPinnedObject();
    public void Dispose(){var held=Interlocked.Exchange(ref owner,null);if(held is null)return;if(pin.IsAllocated)pin.Free();held.Dispose();GC.SuppressFinalize(this);}
    ~PixelBufferPin(){try{Dispose();}catch{/* User release callbacks cannot escape the finalizer thread. */}}
}
