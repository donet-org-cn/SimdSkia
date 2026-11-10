using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
namespace SimdSkia.Gpu;

// Objective-C calls target only macOS system frameworks. The renderer and all
// resource/lifetime code are C#; the compute program is Metal Shading Language.
internal static unsafe partial class MetalApi
{
    static MetalApi() { }
    private const string Objc="/usr/lib/libobjc.A.dylib";
    private static readonly nint library=NativeLibrary.Load(Objc);
    private static readonly nint send=NativeLibrary.GetExport(library,"objc_msgSend");
    private static readonly ConcurrentDictionary<string,nint> selectors=new(StringComparer.Ordinal);
    internal static nint S(string name)=>selectors.GetOrAdd(name,static name=>sel_registerName(name));
    internal static nint Call(nint value,string selector)=>((delegate* unmanaged[Cdecl]<nint,nint,nint>)send)(value,S(selector));
    internal static nint Call(nint value,string selector,nint argument)=>((delegate* unmanaged[Cdecl]<nint,nint,nint,nint>)send)(value,S(selector),argument);
    internal static nint Class(string name)=>objc_getClass(name);
    internal static void SetNumber(nint value,string selector,nuint argument)=>((delegate* unmanaged[Cdecl]<nint,nint,nuint,void>)send)(value,S(selector),argument);
    internal static void End(nint value,string selector)=>((delegate* unmanaged[Cdecl]<nint,nint,void>)send)(value,S(selector));
    internal static nuint Number(nint value,string selector)=>((delegate* unmanaged[Cdecl]<nint,nint,nuint>)send)(value,S(selector));
    internal static bool Conforms(nint value,string protocol)=>((delegate* unmanaged[Cdecl]<nint,nint,nint,byte>)send)(value,S("conformsToProtocol:"),objc_getProtocol(protocol))!=0;
    internal static nint Retain(nint value)=>value==0?0:objc_retain(value);
    internal static void Release(ref nint value){nint old=value;value=0;if(old!=0)objc_release(old);}
    internal static string String(nint value)=>value==0?"":Marshal.PtrToStringUTF8(Call(value,"UTF8String"))??"";
    internal static nint Text(string text)
    {byte[] bytes=Encoding.UTF8.GetBytes(text+'\0');fixed(byte* data=bytes)return ((delegate* unmanaged[Cdecl]<nint,nint,byte*,nint>)send)(Call(objc_getClass("NSString"),"alloc"),S("initWithUTF8String:"),data);}
    internal static nint Library(nint device,nint source,out nint error)
    {nint local=0;var result=((delegate* unmanaged[Cdecl]<nint,nint,nint,nint,nint*,nint>)send)(device,S("newLibraryWithSource:options:error:"),source,0,&local);error=local;return result;}
    internal static nint Pipeline(nint device,nint function,out nint error)
    {nint local=0;var result=((delegate* unmanaged[Cdecl]<nint,nint,nint,nint*,nint>)send)(device,S("newComputePipelineStateWithFunction:error:"),function,&local);error=local;return result;}
    internal static nint Buffer(nint device,nuint length)=>((delegate* unmanaged[Cdecl]<nint,nint,nuint,nuint,nint>)send)(device,S("newBufferWithLength:options:"),length,0);
    internal static void SetBuffer(nint encoder,nint buffer,nuint index)=>((delegate* unmanaged[Cdecl]<nint,nint,nint,nuint,nuint,void>)send)(encoder,S("setBuffer:offset:atIndex:"),buffer,0,index);
    internal static void SetBytes(nint encoder,void* bytes,nuint length,nuint index)=>((delegate* unmanaged[Cdecl]<nint,nint,void*,nuint,nuint,void>)send)(encoder,S("setBytes:length:atIndex:"),bytes,length,index);
    internal static void Dispatch(nint encoder,nuint width,nuint height)=>((delegate* unmanaged[Cdecl]<nint,nint,Size,Size,void>)send)(encoder,S("dispatchThreads:threadsPerThreadgroup:"),new(width,height,1),new(8,8,1));
    internal static void TextureToBuffer(nint encoder,nint texture,nint buffer,nuint width,nuint height,nuint rowBytes)
    {((delegate* unmanaged[Cdecl]<nint,nint,nint,nuint,nuint,Size,Size,nint,nuint,nuint,nuint,void>)send)(encoder,S("copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toBuffer:destinationOffset:destinationBytesPerRow:destinationBytesPerImage:"),texture,0,0,new(0,0,0),new(width,height,1),buffer,0,rowBytes,rowBytes*height);}
    internal static void BufferToTexture(nint encoder,nint buffer,nint texture,nuint width,nuint height,nuint rowBytes)
    {((delegate* unmanaged[Cdecl]<nint,nint,nint,nuint,nuint,nuint,Size,nint,nuint,nuint,Size,void>)send)(encoder,S("copyFromBuffer:sourceOffset:sourceBytesPerRow:sourceBytesPerImage:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:"),buffer,0,rowBytes,rowBytes*height,new(width,height,1),texture,0,0,new(0,0,0));}
    [StructLayout(LayoutKind.Sequential)]private readonly record struct Size(nuint Width,nuint Height,nuint Depth);
    internal readonly struct Pool:IDisposable
    {private readonly nint value;internal Pool(bool create)=>value=create?objc_autoreleasePoolPush():0;public void Dispose(){if(value!=0)objc_autoreleasePoolPop(value);}}
    [LibraryImport(Objc,StringMarshalling=StringMarshalling.Utf8)]private static partial nint sel_registerName(string name);
    [LibraryImport(Objc,StringMarshalling=StringMarshalling.Utf8)]private static partial nint objc_getClass(string name);
    [LibraryImport(Objc,StringMarshalling=StringMarshalling.Utf8)]private static partial nint objc_getProtocol(string name);
    [LibraryImport(Objc)]private static partial nint objc_retain(nint value);
    [LibraryImport(Objc)]private static partial void objc_release(nint value);
    [LibraryImport(Objc)]private static partial nint objc_autoreleasePoolPush();
    [LibraryImport(Objc)]private static partial void objc_autoreleasePoolPop(nint value);
}

internal sealed unsafe class MetalDevice:IDisposable
{
    private readonly int thread=Environment.CurrentManagedThreadId;
    private nint device,queue,pipeline;
    private bool disposed;
    private readonly List<WeakReference<MetalImage>> images=[];
    internal nint Pipeline=>pipeline;
    internal bool Abandoned;
    internal MetalDevice(nint device,nint queue)
    {
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("Metal requires macOS.");
        if(device==0||queue==0)throw new ArgumentException("A live Metal device and command queue are required.");
        using var pool=new MetalApi.Pool(true);
        if(!MetalApi.Conforms(device,"MTLDevice")||!MetalApi.Conforms(queue,"MTLCommandQueue")||MetalApi.Call(queue,"device")!=device)throw new ArgumentException("The Metal queue must belong to the supplied Metal device.");
        this.device=MetalApi.Retain(device);this.queue=MetalApi.Retain(queue);nint source=0,library=0,name=0,function=0;
        try
        {
            source=MetalApi.Text(MetalKernel.Source);library=MetalApi.Library(device,source,out nint error);if(library==0)throw new InvalidOperationException("Metal program compilation failed: "+Error(error));
            name=MetalApi.Text("render");function=MetalApi.Call(library,"newFunctionWithName:",name);if(function==0)throw new InvalidOperationException("The Metal render entry point is missing.");
            pipeline=MetalApi.Pipeline(device,function,out error);if(pipeline==0)throw new InvalidOperationException("Metal compute pipeline creation failed: "+Error(error));
        }
        catch{Dispose();throw;}
        finally{MetalApi.Release(ref function);MetalApi.Release(ref name);MetalApi.Release(ref library);MetalApi.Release(ref source);}
    }
    internal static string Error(nint error)=>error==0?"no driver diagnostics":MetalApi.String(MetalApi.Call(error,"localizedDescription"));
    internal void Check(){ObjectDisposedException.ThrowIf(disposed,this);if(thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Use Metal contexts on their creating thread.");if(Abandoned)throw new InvalidOperationException("The Metal context is abandoned.");}
    internal nint CreateBuffer(int length)
    {Check();if(length<=0||length>128*1024*1024)throw new ArgumentOutOfRangeException(nameof(length));nint buffer=MetalApi.Buffer(device,(nuint)length);if(buffer==0)throw new OutOfMemoryException("Metal shared buffer allocation failed.");return buffer;}
    internal nint Upload(ReadOnlySpan<byte> bytes)
    {nint buffer=CreateBuffer(bytes.Length);try{bytes.CopyTo(new Span<byte>((void*)MetalApi.Call(buffer,"contents"),bytes.Length));return buffer;}catch{MetalApi.Release(ref buffer);throw;}}
    internal nint Commands(){Check();using var pool=new MetalApi.Pool(true);nint buffer=MetalApi.Call(queue,"commandBuffer");return buffer!=0?MetalApi.Retain(buffer):throw new InvalidOperationException("Metal command buffer allocation failed.");}
    internal void Register(MetalImage image){if(images.Count>=256)images.RemoveAll(w=>!w.TryGetTarget(out var old)||old.IsDisposed);images.Add(new(image));}
    internal nint ImportTexture(nint texture,int width,int height)
    {
        Check();if(texture==0||!MetalApi.Conforms(texture,"MTLTexture")||MetalApi.Call(texture,"device")!=device)throw new ArgumentException("The Metal texture must belong to this device.");
        if(MetalApi.Number(texture,"width")!=(nuint)width||MetalApi.Number(texture,"height")!=(nuint)height||MetalApi.Number(texture,"textureType")!=2||MetalApi.Number(texture,"sampleCount")!=1||MetalApi.Number(texture,"arrayLength")!=1||MetalApi.Number(texture,"mipmapLevelCount")!=1)throw new NotSupportedException("Metal imports require matching, single-level, single-sample 2D texture storage.");return texture;
    }
    internal nint CreateTexture(int width,int height,bool bgra)
    {
        Check();if(width<=0||height<=0||width>16384||height>16384||(long)width*height*4>128*1024*1024)throw new ArgumentOutOfRangeException(nameof(width));using var pool=new MetalApi.Pool(true);nint descriptor=MetalApi.Call(MetalApi.Class("MTLTextureDescriptor"),"new");
        try{MetalApi.SetNumber(descriptor,"setTextureType:",2);MetalApi.SetNumber(descriptor,"setPixelFormat:",bgra?80u:70u);MetalApi.SetNumber(descriptor,"setWidth:",(nuint)width);MetalApi.SetNumber(descriptor,"setHeight:",(nuint)height);MetalApi.SetNumber(descriptor,"setDepth:",1);MetalApi.SetNumber(descriptor,"setMipmapLevelCount:",1);MetalApi.SetNumber(descriptor,"setSampleCount:",1);MetalApi.SetNumber(descriptor,"setArrayLength:",1);MetalApi.SetNumber(descriptor,"setStorageMode:",2);MetalApi.SetNumber(descriptor,"setUsage:",7);return MetalApi.Call(device,"newTextureWithDescriptor:",descriptor) is var texture&&texture!=0?texture:throw new OutOfMemoryException("Metal texture allocation failed.");}
        finally{MetalApi.Release(ref descriptor);}
    }
    internal void Complete(nint commands)
    {Check();using var pool=new MetalApi.Pool(true);MetalApi.End(commands,"commit");MetalApi.End(commands,"waitUntilCompleted");if(MetalApi.Number(commands,"status")!=4)throw new InvalidOperationException("Metal work did not complete: "+Error(MetalApi.Call(commands,"error")));}
    internal void ReleaseImages(){foreach(var weak in images)if(weak.TryGetTarget(out var image)&&!image.IsDisposed)image.ReleaseDeviceResource();images.Clear();}
    public void Dispose(){if(disposed)return;if(thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Dispose Metal on its creating thread.");ReleaseImages();MetalApi.Release(ref pipeline);MetalApi.Release(ref queue);MetalApi.Release(ref device);disposed=true;}
}
