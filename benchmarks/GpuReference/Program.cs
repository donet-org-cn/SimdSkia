using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;
using SimdSkia.Benchmarks;

unsafe class Program
{
    const string Lib = "vulkan-1";
    [StructLayout(LayoutKind.Sequential)] struct InstanceInfo { public uint Type; public void* Next; public uint Flags; public void* Application; public uint LayerCount; public void* Layers; public uint ExtensionCount; public void* Extensions; }
    [StructLayout(LayoutKind.Sequential)] struct QueueInfo { public uint Type; public void* Next; public uint Flags, Family, Count; public float* Priorities; }
    [StructLayout(LayoutKind.Sequential)] struct DeviceInfo { public uint Type; public void* Next; public uint Flags, QueueCount; public QueueInfo* Queues; public uint LayerCount; public void* Layers; public uint ExtensionCount; public void* Extensions; public void* Features; }
    [StructLayout(LayoutKind.Sequential)] struct QueueFamily { public uint Flags, Count, TimestampBits, X, Y, Z; }

    [DllImport(Lib)] static extern int vkCreateInstance(InstanceInfo* info, void* allocator, IntPtr* instance);
    [DllImport(Lib)] static extern void vkDestroyInstance(IntPtr instance, void* allocator);
    [DllImport(Lib)] static extern int vkEnumeratePhysicalDevices(IntPtr instance, uint* count, IntPtr* devices);
    [DllImport(Lib)] static extern void vkGetPhysicalDeviceProperties(IntPtr physical, byte* properties);
    [DllImport(Lib)] static extern void vkGetPhysicalDeviceQueueFamilyProperties(IntPtr physical, uint* count, QueueFamily* families);
    [DllImport(Lib)] static extern int vkCreateDevice(IntPtr physical, DeviceInfo* info, void* allocator, IntPtr* device);
    [DllImport(Lib)] static extern void vkDestroyDevice(IntPtr device, void* allocator);
    [DllImport(Lib)] static extern void vkGetDeviceQueue(IntPtr device, uint family, uint index, IntPtr* queue);
    [DllImport(Lib, CharSet = CharSet.Ansi)] static extern IntPtr vkGetInstanceProcAddr(IntPtr instance, string name);
    [DllImport(Lib, CharSet = CharSet.Ansi)] static extern IntPtr vkGetDeviceProcAddr(IntPtr device, string name);

    static GRVkExtensions CreateExtensions()
    {
        var ctor = typeof(GRVkExtensions).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .First(c => c.GetParameters().Length == 0);
        return (GRVkExtensions)ctor.Invoke(null);
    }

    static int Main(string[] args)
    {
        string engine = Get(args, "--engine", "original");
        if (engine != "original") throw new ArgumentException("GPU reference worker needs --engine original.");
        var info = new InstanceInfo { Type = 1 }; IntPtr created = default;
        Check(vkCreateInstance(&info, null, &created));
        IntPtr instance = created;
        try
        {
            uint count = 0; Check(vkEnumeratePhysicalDevices(instance, &count, null));
            var devices = new IntPtr[count];
            fixed (IntPtr* p = devices) Check(vkEnumeratePhysicalDevices(instance, &count, p));
            IntPtr physical = IntPtr.Zero; uint family = 0; string deviceName = ""; int score = -1; bool software = false;
            byte* properties = stackalloc byte[4096];
            foreach (IntPtr candidate in devices)
            {
                vkGetPhysicalDeviceProperties(candidate, properties);
                uint type = *(uint*)(properties + 16); int rank = type == 2 ? 3 : type == 1 ? 2 : type == 3 ? 1 : 0;
                uint queueCount = 0; vkGetPhysicalDeviceQueueFamilyProperties(candidate, &queueCount, null);
                var families = new QueueFamily[queueCount];
                fixed (QueueFamily* fp = families) vkGetPhysicalDeviceQueueFamilyProperties(candidate, &queueCount, fp);
                for (uint i = 0; i < queueCount; i++)
                    if ((families[i].Flags & 1) != 0 && families[i].Count > 0 && rank > score)
                    {
                        score = rank; physical = candidate; family = i; software = type == 4;
                        var name = new ReadOnlySpan<byte>(properties + 20, 256); int terminator = name.IndexOf((byte)0);
                        deviceName = Encoding.UTF8.GetString(terminator >= 0 ? name[..terminator] : name);
                    }
            }
            if (physical == IntPtr.Zero) throw new InvalidOperationException("No Vulkan graphics queue.");
            float priority = 1; var queueInfo = new QueueInfo { Type = 2, Family = family, Count = 1, Priorities = &priority };
            var deviceInfo = new DeviceInfo { Type = 3, QueueCount = 1, Queues = &queueInfo };
            Check(vkCreateDevice(physical, &deviceInfo, null, &created));
            IntPtr device = created;
            try
            {
                vkGetDeviceQueue(device, family, 0, &created); IntPtr queue = created;
                Console.WriteLine($"GPU: {deviceName} (software={software}, family={family})");

                var extensions = CreateExtensions();
                extensions.Initialize(GetProc, instance, physical);
                var backend = new GRVkBackendContext
                {
                    VkInstance = instance, VkPhysicalDevice = physical, VkDevice = device, VkQueue = queue,
                    GraphicsQueueIndex = family, MaxAPIVersion = 0, GetProcedureAddress = GetProc, Extensions = extensions,
                };
                using var gr = GRContext.CreateVulkan(backend) ?? throw new InvalidOperationException("GRContext.CreateVulkan failed.");
                return GpuWorker.Run(args, (width, height) => new SkiaSurfaceTarget(gr, width, height),
                    $"Native Skia GPU dispatch (Vulkan, {deviceName})");
            }
            finally { vkDestroyDevice(device, null); }
        }
        finally { vkDestroyInstance(instance, null); }
    }

    static string Get(string[] args, string key, string fallback)
    {
        int index = Array.IndexOf(args, key);
        return index < 0 ? fallback : index + 1 < args.Length ? args[index + 1] : throw new ArgumentException("Missing value: " + key);
    }

    static IntPtr GetProc(string name, IntPtr instance, IntPtr device)
    {
        if (device != IntPtr.Zero)
        {
            IntPtr address = vkGetDeviceProcAddr(device, name);
            if (address != IntPtr.Zero) return address;
        }
        return vkGetInstanceProcAddr(instance, name);
    }

    static void Check(int result) { if (result != 0) throw new InvalidOperationException($"Vulkan call failed: {result}"); }
}

/// <summary>Adapts an SkiaSharp Vulkan surface to the shared benchmark target contract: Present = flush + submit + fence + readback.</summary>
file sealed class SkiaSurfaceTarget : IGpuTarget
{
    private readonly GRContext context;
    private readonly SKSurface surface;
    private readonly SKImageInfo info;
    private readonly byte[] pixels;
    private readonly GCHandle handle;

    public SkiaSurfaceTarget(GRContext context, int width, int height)
    {
        this.context = context;
        info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        surface = SKSurface.Create(context, true, info) ?? throw new InvalidOperationException("SKSurface.Create returned null.");
        pixels = new byte[checked(width * height * 4)];
        handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
    }

    public SKCanvas Canvas => surface.Canvas;

    public void Present()
    {
        surface.Flush();
        context.Flush();
        if (!surface.ReadPixels(info, handle.AddrOfPinnedObject(), info.Width * 4, 0, 0))
            throw new InvalidOperationException("ReadPixels failed.");
    }

    public SKBitmap Capture()
    {
        Present();
        var copy = new SKBitmap(info);
        Marshal.Copy(pixels, 0, copy.GetPixels(), pixels.Length);
        return copy;
    }

    public void Dispose()
    {
        handle.Free();
        surface.Dispose();
    }
}
