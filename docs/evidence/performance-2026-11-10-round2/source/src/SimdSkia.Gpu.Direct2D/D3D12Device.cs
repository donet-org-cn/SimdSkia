using System.Runtime.InteropServices;
using System.Runtime.Versioning;
namespace SimdSkia.Gpu.Direct2D;

// OS D3D11On12 maps the existing C# Direct2D driver to the caller's actual
// Direct3D12 device and DIRECT queue. No additional renderer native binary.
[SupportedOSPlatform("windows")]
internal sealed unsafe partial class D3D12Device:IDisposable
{
    private nint device,queue,on12;
    private nint fence,eventHandle,immediate;
    private ulong signal;
    private readonly int thread=Environment.CurrentManagedThreadId;
    private bool disposed,abandoned;
    private readonly List<WeakReference<D3D12Image>> images=[];
    internal nint DrawingDevice {get;private set;}
    internal nint Immediate=>immediate;
    internal D3D12Device(nint device,nint queue,nint adapter=0)
    {
        if(device==0||queue==0)throw new ArgumentException("A live D3D12 device and DIRECT queue are required.");
        try
        {
            this.device=D2DNative.Query(device,new("189819f1-1db6-4b57-be54-1821339b85f7"));this.queue=D2DNative.Query(queue,new("0ec870a6-5d7e-4c22-8cfc-5baae07616ed"));
            // Reject a queue from a different device before passing it to the OS.
            Guid iid=new("189819f1-1db6-4b57-be54-1821339b85f7");nint queueDevice=0;
            D2DNative.Check(((delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)D2DNative.Slot(this.queue,7))(this.queue,&iid,&queueDevice));
            try{nint a=D2DNative.Query(this.device,new("00000000-0000-0000-C000-000000000046")),b=0;try{b=D2DNative.Query(queueDevice,new("00000000-0000-0000-C000-000000000046"));if(a!=b)throw new ArgumentException("The command queue belongs to another D3D12 device.");}finally{D2DNative.Release(ref a);D2DNative.Release(ref b);}}
            finally{D2DNative.Release(ref queueDevice);}
            nint result=0,immediate=0;uint level=0;nint input=this.queue;
            try{D2DNative.Check(D3D11On12CreateDevice(this.device,32,null,0,&input,1,0,&result,&immediate,&level));DrawingDevice=result;result=0;}
            finally{D2DNative.Release(ref immediate);D2DNative.Release(ref result);}
            on12=D2DNative.Query(DrawingDevice,new("85611e73-70a9-490e-9614-a9e302777904"));
            if(adapter!=0)ValidateAdapter(adapter);
            this.immediate=D2DNative.Immediate(DrawingDevice);Guid fenceId=new("0a753dcf-c4d8-4b91-adf6-be5a60d95a76");nint createdFence=0;D2DNative.Check(((delegate* unmanaged[Stdcall]<nint,ulong,uint,Guid*,nint*,int>)D2DNative.Slot(this.device,36))(this.device,0,0,&fenceId,&createdFence));fence=createdFence;eventHandle=CreateEventW(0,0,0,0);if(eventHandle==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        }
        catch{Dispose();throw;}
    }
    private void ValidateAdapter(nint supplied)
    {
        nint dxgi=0,actual=0,left=0,right=0;
        try
        {
            dxgi=D2DNative.Query(DrawingDevice,new("54ec77fa-1377-44e6-8c32-88fd5f44c84c"));
            D2DNative.Check(((delegate* unmanaged[Stdcall]<nint,nint*,int>)D2DNative.Slot(dxgi,7))(dxgi,&actual));
            var iid=new Guid("29038f61-3839-4626-91fd-086879011a05");left=D2DNative.Query(actual,iid);right=D2DNative.Query(supplied,iid);
            byte* a=stackalloc byte[312];byte* b=stackalloc byte[312];D2DNative.Check(((delegate* unmanaged[Stdcall]<nint,void*,int>)D2DNative.Slot(left,10))(left,a));D2DNative.Check(((delegate* unmanaged[Stdcall]<nint,void*,int>)D2DNative.Slot(right,10))(right,b));
            if(*(ulong*)(a+296)!=*(ulong*)(b+296))throw new ArgumentException("The adapter belongs to another D3D12 device.",nameof(supplied));
        }
        finally{D2DNative.Release(ref right);D2DNative.Release(ref left);D2DNative.Release(ref actual);D2DNative.Release(ref dxgi);}
    }
    internal void Check(){ObjectDisposedException.ThrowIf(disposed,this);if(thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("D3D12 interop requires its creating thread.");if(abandoned)throw new InvalidOperationException("The D3D12 device context is abandoned.");}
    internal nint Wrap(nint resource,uint state,uint bind)
    {
        Check();Guid deviceId=new("189819f1-1db6-4b57-be54-1821339b85f7");nint resourceDevice=0;D2DNative.Check(((delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)D2DNative.Slot(resource,7))(resource,&deviceId,&resourceDevice));
        try{nint a=D2DNative.Query(resourceDevice,new("00000000-0000-0000-C000-000000000046")),b=0;try{b=D2DNative.Query(device,new("00000000-0000-0000-C000-000000000046"));if(a!=b)throw new ArgumentException("The texture belongs to another D3D12 device.");}finally{D2DNative.Release(ref a);D2DNative.Release(ref b);}}
        finally{D2DNative.Release(ref resourceDevice);}
        uint* flags=stackalloc uint[4]{bind,0,0,0};Guid textureId=D2DNative.Texture2D;nint wrapped=0;D2DNative.Check(((delegate* unmanaged[Stdcall]<nint,nint,uint*,uint,uint,Guid*,nint*,int>)D2DNative.Slot(on12,3))(on12,resource,flags,state,state,&textureId,&wrapped));return wrapped;
    }
    internal void Acquire(nint texture){Check();nint value=texture;((delegate* unmanaged[Stdcall]<nint,nint*,uint,void>)D2DNative.Slot(on12,5))(on12,&value,1);}
    internal void ReleaseWrapped(nint texture){Check();nint value=texture;((delegate* unmanaged[Stdcall]<nint,nint*,uint,void>)D2DNative.Slot(on12,4))(on12,&value,1);D2DNative.Flush(immediate);Complete();}
    internal void Complete()
    {
        Check();ulong value=++signal;D2DNative.Check(((delegate* unmanaged[Stdcall]<nint,nint,ulong,int>)D2DNative.Slot(queue,14))(queue,fence,value));ulong completed=((delegate* unmanaged[Stdcall]<nint,ulong>)D2DNative.Slot(fence,8))(fence);if(completed==ulong.MaxValue)throw new InvalidOperationException("D3D12 device removed.");
        if(completed<value){D2DNative.Check(((delegate* unmanaged[Stdcall]<nint,ulong,nint,int>)D2DNative.Slot(fence,9))(fence,value,eventHandle));uint wait=WaitForSingleObject(eventHandle,10000);if(wait!=0)throw new TimeoutException($"D3D12 completion failed or timed out ({wait}).");}
    }
    internal void Register(D3D12Image image){if(images.Count>=256)images.RemoveAll(w=>!w.TryGetTarget(out var old)||old.IsDisposed);images.Add(new(image));}
    internal nint CreateTexture(int width,int height,uint format)
    {
        Check();uint* heap=stackalloc uint[5]{1,0,0,1,1};var description=new ResourceDescription{Dimension=3,Width=(ulong)width,Height=(uint)height,Depth=1,Mips=1,Format=format,SampleCount=1,Flags=1};Guid id=new("696442be-a72e-4059-bc79-5b5c98040fad");nint resource=0;D2DNative.Check(((delegate* unmanaged[Stdcall]<nint,uint*,uint,ResourceDescription*,uint,void*,Guid*,nint*,int>)D2DNative.Slot(device,27))(device,heap,0,&description,0,null,&id,&resource));return resource;
    }
    [StructLayout(LayoutKind.Sequential)]private struct ResourceDescription{internal uint Dimension;internal ulong Alignment,Width;internal uint Height;internal ushort Depth,Mips;internal uint Format,SampleCount,SampleQuality,Layout,Flags;}
    internal void Abandon(bool releaseResources){if(disposed||abandoned)return;Check();foreach(var weak in images)if(weak.TryGetTarget(out var image)&&!image.IsDisposed)image.ReleaseDeviceResource(releaseResources);images.Clear();if(!releaseResources)abandoned=true;}
    public void Dispose(){if(disposed)return;if(thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Dispose D3D12 on its creating thread.");if(!abandoned)Abandon(true);if(eventHandle!=0){CloseHandle(eventHandle);eventHandle=0;}D2DNative.Release(ref fence);D2DNative.Release(ref immediate);D2DNative.Release(ref on12);nint drawing=DrawingDevice;DrawingDevice=0;D2DNative.Release(ref drawing);D2DNative.Release(ref queue);D2DNative.Release(ref device);disposed=true;}
    [LibraryImport("d3d11.dll")]private static partial int D3D11On12CreateDevice(nint device,uint flags,uint* levels,uint count,nint* queues,uint queueCount,uint nodeMask,nint* result,nint* context,uint* level);
    [LibraryImport("kernel32.dll",SetLastError=true)]private static partial nint CreateEventW(nint security,int manual,int initial,nint name);
    [LibraryImport("kernel32.dll")]private static partial uint WaitForSingleObject(nint handle,uint milliseconds);
    [LibraryImport("kernel32.dll")]private static partial int CloseHandle(nint handle);
}
