using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
namespace SimdSkia.Gpu;

// Objective-C calls target only macOS system frameworks. The renderer and all
// resource/lifetime code are C#; the GPU programs are Metal Shading Language.
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
    internal static nint Pipeline(nint device,nint function,out nint error)=>WithError(device,"newComputePipelineStateWithFunction:error:",function,out error);
    internal static nint WithError(nint value,string selector,nint argument,out nint error)
    {nint local=0;var result=((delegate* unmanaged[Cdecl]<nint,nint,nint,nint*,nint>)send)(value,S(selector),argument,&local);error=local;return result;}
    internal static nint Buffer(nint device,nuint length)=>((delegate* unmanaged[Cdecl]<nint,nint,nuint,nuint,nint>)send)(device,S("newBufferWithLength:options:"),length,0);
    internal static void SetBuffer(nint encoder,nint buffer,nuint index)=>((delegate* unmanaged[Cdecl]<nint,nint,nint,nuint,nuint,void>)send)(encoder,S("setBuffer:offset:atIndex:"),buffer,0,index);
    internal static void SetBytes(nint encoder,void* bytes,nuint length,nuint index)=>((delegate* unmanaged[Cdecl]<nint,nint,void*,nuint,nuint,void>)send)(encoder,S("setBytes:length:atIndex:"),bytes,length,index);
    internal static void SetFragmentBuffer(nint encoder,nint buffer,nuint index)=>((delegate* unmanaged[Cdecl]<nint,nint,nint,nuint,nuint,void>)send)(encoder,S("setFragmentBuffer:offset:atIndex:"),buffer,0,index);
    internal static void SetFragmentBytes(nint encoder,void* bytes,nuint length,nuint index)=>((delegate* unmanaged[Cdecl]<nint,nint,void*,nuint,nuint,void>)send)(encoder,S("setFragmentBytes:length:atIndex:"),bytes,length,index);
    internal static void Draw(nint encoder,nuint primitive,nuint start,nuint count)=>((delegate* unmanaged[Cdecl]<nint,nint,nuint,nuint,nuint,void>)send)(encoder,S("drawPrimitives:vertexStart:vertexCount:"),primitive,start,count);
    internal static void Fill(nint encoder,nint buffer,nuint offset,nuint length,byte value)=>((delegate* unmanaged[Cdecl]<nint,nint,nint,Range,byte,void>)send)(encoder,S("fillBuffer:range:value:"),buffer,new(offset,length),value);
    internal static void Dispatch(nint encoder,nuint width,nuint height)=>((delegate* unmanaged[Cdecl]<nint,nint,Size,Size,void>)send)(encoder,S("dispatchThreads:threadsPerThreadgroup:"),new(width,height,1),new(8,8,1));
    internal static void TextureToBuffer(nint encoder,nint texture,nint buffer,nuint width,nuint height,nuint rowBytes)
    {((delegate* unmanaged[Cdecl]<nint,nint,nint,nuint,nuint,Size,Size,nint,nuint,nuint,nuint,void>)send)(encoder,S("copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toBuffer:destinationOffset:destinationBytesPerRow:destinationBytesPerImage:"),texture,0,0,new(0,0,0),new(width,height,1),buffer,0,rowBytes,rowBytes*height);}
    internal static void BufferToTexture(nint encoder,nint buffer,nint texture,nuint width,nuint height,nuint rowBytes)
    {((delegate* unmanaged[Cdecl]<nint,nint,nint,nuint,nuint,nuint,Size,nint,nuint,nuint,Size,void>)send)(encoder,S("copyFromBuffer:sourceOffset:sourceBytesPerRow:sourceBytesPerImage:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:"),buffer,0,rowBytes,rowBytes*height,new(width,height,1),texture,0,0,new(0,0,0));}
    [StructLayout(LayoutKind.Sequential)]private readonly record struct Size(nuint Width,nuint Height,nuint Depth);
    [StructLayout(LayoutKind.Sequential)]private readonly record struct Range(nuint Location,nuint Length);
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

// One device owns the programs, the frame command buffer and the resources
// shared by every canvas drawing into it. All canvases of one window frame
// (root and offscreen layers) encode into the same command buffer, so GPU
// ordering follows encoding order and no CPU wait is needed between them.
internal sealed unsafe partial class MetalDevice:IDisposable
{
    private readonly int thread=Environment.CurrentManagedThreadId;
    private nint device,queue,library,pipeline,blurHorizontal,blurVertical,dropShadow;
    private nint frame,encoder,currentPipeline,lastCommitted;
    private readonly Dictionary<nuint,nint> presentPipelines=[];
    private bool disposed;
    private readonly List<WeakReference<MetalImage>> images=[];
    internal nint Pipeline=>pipeline;
    internal nint NativeDevice=>device;
    internal MetalResources Resources {get;}=new();
    internal bool Abandoned;
    internal string Name {get;}
    /// <summary>The host serializes all use (Avalonia's compositor lock) but may
    /// render from different threads, e.g. a synchronous frame on the UI thread
    /// during a resize. Metal objects are thread-safe when not used concurrently.</summary>
    internal bool HostSerialized {get;}
    internal MetalDevice(nint device,nint queue,bool hostSerialized=false)
    {
        HostSerialized=hostSerialized;
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("Metal requires macOS.");
        if(device==0||queue==0)throw new ArgumentException("A live Metal device and command queue are required.");
        using var pool=new MetalApi.Pool(true);
        if(!MetalApi.Conforms(device,"MTLDevice")||!MetalApi.Conforms(queue,"MTLCommandQueue")||MetalApi.Call(queue,"device")!=device)throw new ArgumentException("The Metal queue must belong to the supplied Metal device.");
        this.device=MetalApi.Retain(device);this.queue=MetalApi.Retain(queue);nint source=0;
        try
        {
            Name=MetalApi.String(MetalApi.Call(device,"name"));
            long t0=System.Diagnostics.Stopwatch.GetTimestamp();
            source=MetalApi.Text(MetalKernel.Source);library=MetalApi.Library(device,source,out nint error);if(library==0)throw new InvalidOperationException("Metal program compilation failed: "+Error(error));
            long t1=System.Diagnostics.Stopwatch.GetTimestamp();
            pipeline=Compute("render");blurHorizontal=Compute("blurHorizontal");blurVertical=Compute("blurVertical");dropShadow=Compute("dropShadow");
            if(Trace)Console.Error.WriteLine($"[metal] device: library {System.Diagnostics.Stopwatch.GetElapsedTime(t0,t1).TotalMilliseconds:F1} ms, pipelines {System.Diagnostics.Stopwatch.GetElapsedTime(t1).TotalMilliseconds:F1} ms");
        }
        catch{Dispose();throw;}
        finally{MetalApi.Release(ref source);}
    }
    private nint Function(string entry)
    {
        nint name=MetalApi.Text(entry);
        try{nint function=MetalApi.Call(library,"newFunctionWithName:",name);return function!=0?function:throw new InvalidOperationException($"The Metal entry point {entry} is missing.");}
        finally{MetalApi.Release(ref name);}
    }
    private nint Compute(string entry)
    {
        nint function=Function(entry);
        try{nint state=MetalApi.Pipeline(device,function,out nint error);return state!=0?state:throw new InvalidOperationException($"Metal compute pipeline {entry} failed: "+Error(error));}
        finally{MetalApi.Release(ref function);}
    }
    internal static string Error(nint error)=>error==0?"no driver diagnostics":MetalApi.String(MetalApi.Call(error,"localizedDescription"));
    internal void Check(){ObjectDisposedException.ThrowIf(disposed,this);if(!HostSerialized&&thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Use Metal contexts on their creating thread.");if(Abandoned)throw new InvalidOperationException("The Metal context is abandoned.");}
    internal nint CreateBuffer(int length)
    {Check();if(length<=0||length>256*1024*1024)throw new ArgumentOutOfRangeException(nameof(length));nint buffer=MetalApi.Buffer(device,(nuint)length);if(buffer==0)throw new OutOfMemoryException("Metal shared buffer allocation failed.");return buffer;}
    internal nint Upload(ReadOnlySpan<byte> bytes)
    {nint buffer=CreateBuffer(bytes.Length);try{bytes.CopyTo(new Span<byte>((void*)MetalApi.Call(buffer,"contents"),bytes.Length));return buffer;}catch{MetalApi.Release(ref buffer);throw;}}

    // Standalone command buffers (texture transfers). The pending frame is
    // committed first so standalone work observes every earlier draw.
    internal nint Commands(){Check();Submit(false);using var pool=new MetalApi.Pool(true);nint buffer=MetalApi.Call(queue,"commandBuffer");return buffer!=0?MetalApi.Retain(buffer):throw new InvalidOperationException("Metal command buffer allocation failed.");}
    internal void Complete(nint commands)
    {Check();using var pool=new MetalApi.Pool(true);MetalApi.End(commands,"commit");MetalApi.End(commands,"waitUntilCompleted");if(MetalApi.Number(commands,"status")!=4)throw new InvalidOperationException("Metal work did not complete: "+Error(MetalApi.Call(commands,"error")));}

    /// <summary>The frame command buffer, created on first use.</summary>
    internal nint Frame()
    {
        Check();if(frame!=0)return frame;
        using var pool=new MetalApi.Pool(true);nint buffer=MetalApi.Call(queue,"commandBuffer");
        if(buffer==0)throw new InvalidOperationException("Metal command buffer allocation failed.");
        frame=MetalApi.Retain(buffer);return frame;
    }
    /// <summary>The open compute encoder of the frame. Dispatches in one serial
    /// compute encoder execute in order, so consecutive draws need no barrier.</summary>
    internal nint Compute()
    {
        if(encoder!=0)return encoder;
        nint commands=Frame();using var pool=new MetalApi.Pool(true);
        nint value=MetalApi.Call(commands,"computeCommandEncoder");if(value==0)throw new InvalidOperationException("Metal compute encoder allocation failed.");
        encoder=MetalApi.Retain(value);currentPipeline=0;return encoder;
    }
    internal void Use(nint state){if(currentPipeline==state)return;MetalApi.Call(Compute(),"setComputePipelineState:",state);currentPipeline=state;}
    internal void EndCompute(){if(encoder==0)return;MetalApi.End(encoder,"endEncoding");MetalApi.Release(ref encoder);currentPipeline=0;}
    /// <summary>Commits the frame. With <paramref name="wait"/>, blocks until all
    /// committed work completed so the CPU may read or write shared buffers.</summary>
    internal void Submit(bool wait)
    {
        Check();EndCompute();
        if(frame!=0)
        {
            using var pool=new MetalApi.Pool(true);
            MetalApi.End(frame,"commit");MetalApi.Release(ref lastCommitted);lastCommitted=frame;frame=0;
        }
        if(wait)Wait();
    }
    private void Wait()
    {
        if(lastCommitted==0)return;
        using var pool=new MetalApi.Pool(true);
        MetalApi.End(lastCommitted,"waitUntilCompleted");var status=MetalApi.Number(lastCommitted,"status");
        string failure=status==4?"":Error(MetalApi.Call(lastCommitted,"error"));MetalApi.Release(ref lastCommitted);
        if(status!=4){Abandoned=true;throw new InvalidOperationException("Metal work did not complete: "+failure);}
    }
    internal bool HasPendingWork=>frame!=0||lastCommitted!=0;

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
    internal void ReleaseImages(){foreach(var weak in images)if(weak.TryGetTarget(out var image)&&!image.IsDisposed)image.ReleaseDeviceResource();images.Clear();}

    // Copies a retained premultiplied RGBA buffer into a drawable. CAMetalLayer
    // drawables are framebuffer-only, so this is a render pass, not a blit.
    internal void Present(nint pixels,int width,int height,nint texture,bool flip)
    {
        Check();if(texture==0)throw new ArgumentException("The drawable texture is missing.");
        nuint format=MetalApi.Number(texture,"pixelFormat");nint state=PresentPipeline(format);
        EndCompute();nint commands=Frame();using var pool=new MetalApi.Pool(true);
        nint pass=MetalApi.Call(MetalApi.Class("MTLRenderPassDescriptor"),"renderPassDescriptor");
        nint color=MetalApi.Call(MetalApi.Call(pass,"colorAttachments"),"objectAtIndexedSubscript:",0);
        MetalApi.Call(color,"setTexture:",texture);MetalApi.SetNumber(color,"setLoadAction:",0);MetalApi.SetNumber(color,"setStoreAction:",1);
        nint render=MetalApi.Call(commands,"renderCommandEncoderWithDescriptor:",pass);if(render==0)throw new InvalidOperationException("Metal render encoder allocation failed.");
        uint* values=stackalloc uint[4];values[0]=(uint)width;values[1]=(uint)height;values[2]=flip?1u:0u;values[3]=format is 81 or 553 or 555?1u:0u;
        try{MetalApi.Call(render,"setRenderPipelineState:",state);MetalApi.SetFragmentBuffer(render,pixels,0);MetalApi.SetFragmentBytes(render,values,16,1);MetalApi.Draw(render,3,0,3);}
        finally{MetalApi.End(render,"endEncoding");}
        Maintain();
    }
    private nint PresentPipeline(nuint format)
    {
        if(presentPipelines.TryGetValue(format,out var cached))return cached;
        using var pool=new MetalApi.Pool(true);nint descriptor=MetalApi.Call(MetalApi.Class("MTLRenderPipelineDescriptor"),"new"),vertex=0,fragment=0;
        try
        {
            vertex=Function("presentVertex");fragment=Function("presentFragment");
            MetalApi.Call(descriptor,"setVertexFunction:",vertex);MetalApi.Call(descriptor,"setFragmentFunction:",fragment);
            MetalApi.SetNumber(MetalApi.Call(MetalApi.Call(descriptor,"colorAttachments"),"objectAtIndexedSubscript:",0),"setPixelFormat:",format);
            nint state=MetalApi.WithError(device,"newRenderPipelineStateWithDescriptor:error:",descriptor,out nint error);
            if(state==0)throw new NotSupportedException($"Metal presentation to pixel format {format} failed: "+Error(error));
            presentPipelines.Add(format,state);return state;
        }
        finally{MetalApi.Release(ref fragment);MetalApi.Release(ref vertex);MetalApi.Release(ref descriptor);}
    }

    public void Dispose()
    {
        if(disposed)return;if(!HostSerialized&&thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Dispose Metal on its creating thread.");
        // Shared buffers may still be read by committed work: drain it first.
        try{if(!Abandoned&&(frame!=0||lastCommitted!=0)){try{Submit(true);}catch(InvalidOperationException){/* A failed frame cannot be recovered during disposal. */}}}
        finally
        {
            MetalApi.Release(ref encoder);MetalApi.Release(ref frame);MetalApi.Release(ref lastCommitted);
            ReleaseImages();Resources.Clear();ReleaseShared();ReleaseWeights();
            foreach(var state in presentPipelines.Values){nint value=state;MetalApi.Release(ref value);}presentPipelines.Clear();
            MetalApi.Release(ref dropShadow);MetalApi.Release(ref blurVertical);MetalApi.Release(ref blurHorizontal);MetalApi.Release(ref pipeline);MetalApi.Release(ref library);MetalApi.Release(ref queue);MetalApi.Release(ref device);disposed=true;
        }
    }
}
