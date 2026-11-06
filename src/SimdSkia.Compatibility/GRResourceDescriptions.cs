using System.Runtime.InteropServices;

namespace SkiaSharp;

// Borrowed platform handles. These objects do not own a device, queue or image.
// In particular, their construction does not imply that a backend can import it.
public class GRContextOptions
{
    public bool AvoidStencilBuffers {get;set;}
    public int RuntimeProgramCacheSize {get;set;}=256;
    public int GlyphCacheTextureMaximumBytes {get;set;}=8*1024*1024;
    public bool AllowPathMaskCaching {get;set;}=true;
    public bool DoManualMipmapping {get;set;}
    public int BufferMapThreshold {get;set;}=-1;
}
public class GRD3DBackendContext : IDisposable
{
    public IntPtr Adapter {get;set;}
    public IntPtr Device {get;set;}
    public IntPtr Queue {get;set;}
    public bool ProtectedContext {get;set;}
    // No COM reference was acquired; ownership remains with the caller.
    protected virtual void Dispose(bool disposing) { }
    public void Dispose(){Dispose(true);GC.SuppressFinalize(this);}
}
public class GRD3DTextureResourceInfo : IDisposable
{
    public IntPtr Resource {get;set;}
    public uint ResourceState {get;set;}
    public uint Format {get;set;}
    public uint SampleCount {get;set;}
    public uint LevelCount {get;set;}
    public uint SampleQualityPattern {get;set;}
    public bool Protected {get;set;}
    protected virtual void Dispose(bool disposing) { }
    public void Dispose(){Dispose(true);GC.SuppressFinalize(this);}
    internal GRD3DTextureResourceInfo Copy()=>new(){Resource=Resource,ResourceState=ResourceState,Format=Format,SampleCount=SampleCount,LevelCount=LevelCount,SampleQualityPattern=SampleQualityPattern,Protected=Protected};
}
public class GRMtlBackendContext : IDisposable
{
    public IntPtr DeviceHandle {get;set;}
    public IntPtr QueueHandle {get;set;}
    protected virtual void Dispose(bool disposing) { }
    public void Dispose(){Dispose(true);GC.SuppressFinalize(this);}
}
[StructLayout(LayoutKind.Sequential)]
public struct GRMtlTextureInfo
{
    public GRMtlTextureInfo(IntPtr textureHandle)=>TextureHandle=textureHandle;
    public IntPtr TextureHandle {readonly get;set;}
    public readonly bool Equals(GRMtlTextureInfo obj)=>TextureHandle==obj.TextureHandle;
    public readonly override bool Equals(object? obj)=>obj is GRMtlTextureInfo other&&Equals(other);
    public readonly override int GetHashCode()=>HashCode.Combine(TextureHandle);
    public static bool operator ==(GRMtlTextureInfo left,GRMtlTextureInfo right)=>left.Equals(right);
    public static bool operator !=(GRMtlTextureInfo left,GRMtlTextureInfo right)=>!left.Equals(right);
}
public delegate IntPtr GRGlGetProcedureAddressDelegate(string name);
public delegate IntPtr GRVkGetProcedureAddressDelegate(string name,IntPtr instance,IntPtr device);
public delegate void SKImageTextureReleaseDelegate(object context);

public class GRVkBackendContext : IDisposable
{
    public IntPtr VkInstance {get;set;}
    public IntPtr VkPhysicalDevice {get;set;}
    public IntPtr VkDevice {get;set;}
    public IntPtr VkQueue {get;set;}
    public uint GraphicsQueueIndex {get;set;}
    public uint MaxAPIVersion {get;set;}
    public GRVkExtensions? Extensions {get;set;}
    public IntPtr VkPhysicalDeviceFeatures {get;set;}
    public IntPtr VkPhysicalDeviceFeatures2 {get;set;}
    public GRVkGetProcedureAddressDelegate? GetProcedureAddress {get;set;}
    public bool ProtectedContext {get;set;}
    // Managed references keep callbacks alive; there is no pinned/native proxy.
    protected virtual void Dispose(bool disposing){if(disposing)GetProcedureAddress=null;}
    public void Dispose(){Dispose(true);GC.SuppressFinalize(this);}
}

public unsafe class GRVkExtensions : SKObject
{
    private Dictionary<string,uint> versions=new(StringComparer.Ordinal);
    private GRVkExtensions() { }
    public static GRVkExtensions Create(GRVkGetProcedureAddressDelegate getProc,IntPtr vkInstance,IntPtr vkPhysicalDevice,string[] instanceExtensions,string[] deviceExtensions)
    {var result=new GRVkExtensions();result.Initialize(getProc,vkInstance,vkPhysicalDevice,instanceExtensions,deviceExtensions);return result;}
    public void Initialize(GRVkGetProcedureAddressDelegate getProc,IntPtr vkInstance,IntPtr vkPhysicalDevice)=>Initialize(getProc,vkInstance,vkPhysicalDevice,[],[]);
    public void Initialize(GRVkGetProcedureAddressDelegate getProc,IntPtr vkInstance,IntPtr vkPhysicalDevice,string[] instanceExtensions,string[] deviceExtensions)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(getProc);
        var next=new Dictionary<string,uint>(StringComparer.Ordinal);
        Enumerate(false,instanceExtensions??[]);Enumerate(true,deviceExtensions??[]);versions=next;
        void Enumerate(bool device,string[] enabled)
        {
            if(enabled.Length==0)return;
            var address=getProc(device?"vkEnumerateDeviceExtensionProperties":"vkEnumerateInstanceExtensionProperties",vkInstance,IntPtr.Zero);
            if(address==IntPtr.Zero)throw new InvalidOperationException("The Vulkan extension enumeration entry point is unavailable.");
            uint count=0;int result;
            var inst=(delegate* unmanaged[Stdcall]<byte*,uint*,ExtensionProperty*,int>)address;
            var dev=(delegate* unmanaged[Stdcall]<IntPtr,byte*,uint*,ExtensionProperty*,int>)address;
            result=device?dev(vkPhysicalDevice,null,&count,null):inst(null,&count,null);
            if(result<0||count>65536)throw new InvalidOperationException($"Vulkan extension enumeration failed ({result}, {count}).");
            var properties=new ExtensionProperty[count];
            for(int attempt=0;;attempt++)
            {
                fixed(ExtensionProperty* pointer=properties)result=device?dev(vkPhysicalDevice,null,&count,pointer):inst(null,&count,pointer);
                if(result!=5)break; // VK_INCOMPLETE: the driver's list changed.
                if(attempt>=3||count>=65536)throw new InvalidOperationException("Vulkan extension enumeration did not stabilize.");
                count=Math.Min(65536,Math.Max(count+16,(uint)properties.Length*2));properties=new ExtensionProperty[count];
            }
            if(result<0||count>properties.Length)throw new InvalidOperationException($"Vulkan extension enumeration failed ({result}).");
            var requested=new HashSet<string>(enabled,StringComparer.Ordinal);
            for(int i=0;i<count;i++)fixed(byte* name=properties[i].Name)
            {int length=0;while(length<256&&name[length]!=0)length++;if(length==256)throw new InvalidOperationException("Invalid Vulkan extension name.");string key=System.Text.Encoding.UTF8.GetString(new ReadOnlySpan<byte>(name,length));if(requested.Contains(key))next[key]=Math.Max(next.GetValueOrDefault(key),properties[i].Version);}
        }
    }
    // SkiaSharp 3.119.4 exposes this query as void (an upstream API defect).
    // Keep its signature; the result is available internally to backend importers.
    public void HasExtension(string extension,int minVersion){_ = Supports(extension,minVersion);}
    internal bool Supports(string extension,int minVersion){ThrowIfDisposed();ArgumentNullException.ThrowIfNull(extension);return versions.TryGetValue(extension,out uint v)&&v>=minVersion;}
    protected override void Dispose(bool disposing)=>versions.Clear();
    [StructLayout(LayoutKind.Sequential)]private struct ExtensionProperty{internal fixed byte Name[256];internal uint Version;}
}
