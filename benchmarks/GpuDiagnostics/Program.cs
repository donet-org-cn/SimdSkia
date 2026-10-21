using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;
using SimdSkia;
using SimdSkia.Benchmarks;

if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("This diagnostic uses the Windows Vulkan loader; production supports Windows/Linux.");
string font=args.FirstOrDefault()??"baseline/fonts/ZenOldMincho-Regular.ttf";
string outputPath=args.Length>1?args[1]:"artifacts/optimization-2026-10-21/gpu-diagnostics.json";
var rows=new List<object>();
foreach(string scene in new[]{"clear","rectangles","curve-aa","chinese","chinese-changing"})
{
    Target? adapter=null;
    using var workload=new GpuWorkload(scene,1024,768,font,(w,h)=>adapter=new Target(VulkanSurface.Create(w,h)));
    for(int i=0;i<3;i++){workload.Render(i);workload.Present();}
    var surface=adapter!.Surface;var backend=typeof(VulkanSurface).GetField("backend",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(surface)!;
    var type=backend.GetType();
    IntPtr physical=(IntPtr)type.GetField("physical",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(backend)!;
    IntPtr device=(IntPtr)type.GetField("device",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(backend)!;
    var output=type.GetField("output",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(backend)!;
    ulong handle=(ulong)output.GetType().GetField("Handle")!.GetValue(output)!;
    Native.vkGetBufferMemoryRequirements(device,handle,out var requirements);
    var readback=type.GetField("readback",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(backend)!;
    ulong readbackHandle=(ulong)readback.GetType().GetField("Handle")!.GetValue(readback)!;
    Native.vkGetBufferMemoryRequirements(device,readbackHandle,out var readbackRequirements);
    IntPtr memory=Marshal.AllocHGlobal(520);
    var types=new List<object>();int selected=-1,deviceLocal=-1,readbackSelected=-1,cached=-1;uint selectedFlags=0,deviceFlags=0,readbackFlags=0,cachedFlags=0;
    try
    {
        Native.vkGetPhysicalDeviceMemoryProperties(physical,memory);
        int count=Marshal.ReadInt32(memory);
        for(int i=0;i<count;i++)
        {
            uint flags=(uint)Marshal.ReadInt32(memory,4+i*8),heap=(uint)Marshal.ReadInt32(memory,8+i*8);
            bool compatible=(requirements.TypeBits&(1u<<i))!=0;
            bool readbackCompatible=(readbackRequirements.TypeBits&(1u<<i))!=0;
            types.Add(new {Index=i,Flags=flags,Heap=heap,Compatible=compatible,ReadbackCompatible=readbackCompatible,DeviceLocal=(flags&1)!=0,HostVisible=(flags&2)!=0,HostCoherent=(flags&4)!=0,HostCached=(flags&8)!=0});
            if(selected<0&&compatible&&(flags&6)==6){selected=i;selectedFlags=flags;}
            if(deviceLocal<0&&compatible&&(flags&1)!=0){deviceLocal=i;deviceFlags=flags;}
            if(readbackSelected<0&&readbackCompatible&&(flags&6)==6){readbackSelected=i;readbackFlags=flags;}
            if(cached<0&&readbackCompatible&&(flags&14)==14){cached=i;cachedFlags=flags;}
        }
        if(deviceLocal>=0){selected=deviceLocal;selectedFlags=deviceFlags;}
        if(cached>=0){readbackSelected=cached;readbackFlags=cachedFlags;}
    }
    finally{Marshal.FreeHGlobal(memory);}
    int frames=scene.StartsWith("chinese",StringComparison.Ordinal)?3:scene=="curve-aa"?10:40;
    var record=new List<double>();var flush=new List<double>();var submit=new List<double>();var complete=new List<double>();var edges=new List<int>();
    for(int i=0;i<frames;i++)
    {
        long begin=Stopwatch.GetTimestamp();workload.Render(i+20);record.Add(Stopwatch.GetElapsedTime(begin).TotalMilliseconds);
        var pending=(System.Collections.IEnumerable)type.GetField("pending",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(backend)!;
        int edgeCount=0;foreach(var item in pending)edgeCount+=((float[])item.GetType().GetProperty("Edges")!.GetValue(item)!).Length/4;edges.Add(edgeCount);
        begin=Stopwatch.GetTimestamp();workload.Present();flush.Add(Stopwatch.GetElapsedTime(begin).TotalMilliseconds);
        submit.Add(surface.LastSubmitMilliseconds);complete.Add(surface.LastCompletionMilliseconds);
    }
    double Median(List<double> values)=>values.Order().ElementAt(values.Count/2);
    var row=new {Scene=scene,surface.DeviceName,Frames=frames,RecordMs=Median(record),FlushMs=Median(flush),SubmitMs=Median(submit),FenceCompletionIncludingSubmitMs=Median(complete),EdgeCount=edges.Max(),RenderTargetMemoryBytes=requirements.Size,MemoryTypesInferredFromCurrentAllocationPolicy=true,RenderTargetMemoryType=selected,RenderTargetMemoryFlags=selectedFlags,RenderTargetDeviceLocal=(selectedFlags&1)!=0,ReadbackMemoryBytes=readbackRequirements.Size,ReadbackMemoryType=readbackSelected,ReadbackMemoryFlags=readbackFlags,ReadbackHostCached=(readbackFlags&8)!=0,Types=types};
    rows.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);File.WriteAllText(outputPath,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
file sealed class Target(VulkanSurface surface):IGpuTarget
{
    public VulkanSurface Surface=>surface;
    public SKCanvas Canvas=>surface.Canvas;
    public void Present()=>surface.Flush();
    public SKBitmap Capture()=>surface.CopyBitmap();
    public void Dispose()=>surface.Dispose();
}
file static class Native
{
    [StructLayout(LayoutKind.Sequential)]public struct Requirements{public ulong Size,Alignment;public uint TypeBits;}
    [DllImport("vulkan-1")]public static extern void vkGetPhysicalDeviceMemoryProperties(IntPtr physical,IntPtr properties);
    [DllImport("vulkan-1")]public static extern void vkGetBufferMemoryRequirements(IntPtr device,ulong buffer,out Requirements requirements);
}
