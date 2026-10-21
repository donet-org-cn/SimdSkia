using System.Reflection;
using System.Runtime.InteropServices;

namespace SimdSkia.Gpu;

internal static unsafe class Vk
{
    private const string Library="SimdSkia.Vulkan.Loader";
    static Vk()
    {
        if(IntPtr.Size!=8||!BitConverter.IsLittleEndian)throw new PlatformNotSupportedException("The initial Vulkan ABI supports 64-bit little-endian Windows/Linux.");
        NativeLibrary.SetDllImportResolver(typeof(Vk).Assembly,Resolve);
        if(sizeof(InstanceInfo)!=64||sizeof(DeviceInfo)!=72||sizeof(SubmitInfo)!=72||sizeof(WriteDescriptor)!=64||sizeof(ComputePipelineInfo)!=96||sizeof(MemoryProperties)!=520||sizeof(ImageInfo)!=88||sizeof(ImageViewInfo)!=80||sizeof(ImageBarrier)!=72||sizeof(BufferImageCopy)!=56||sizeof(BufferCopy)!=24)
            throw new PlatformNotSupportedException("Vulkan structure ABI size mismatch.");
    }
    private static IntPtr Resolve(string name,Assembly assembly,DllImportSearchPath? search)
    {
        if(name!=Library)return IntPtr.Zero;
        string native=OperatingSystem.IsWindows()?"vulkan-1.dll":OperatingSystem.IsLinux()?"libvulkan.so.1":throw new PlatformNotSupportedException("Vulkan loader configured for Windows/Linux only.");
        return NativeLibrary.Load(native,assembly,search);
    }
    [StructLayout(LayoutKind.Sequential)]public struct InstanceInfo {public uint Type;public void* Next;public uint Flags;public void* Application;public uint LayerCount;public void* Layers;public uint ExtensionCount;public void* Extensions;}
    [StructLayout(LayoutKind.Sequential)]public struct QueueInfo {public uint Type;public void* Next;public uint Flags,Family,Count;public float* Priorities;}
    [StructLayout(LayoutKind.Sequential)]public struct DeviceInfo {public uint Type;public void* Next;public uint Flags,QueueCount;public QueueInfo* Queues;public uint LayerCount;public void* Layers;public uint ExtensionCount;public void* Extensions;public void* Features;}
    [StructLayout(LayoutKind.Sequential)]public struct QueueFamily {public uint Flags,Count,TimestampBits,X,Y,Z;}
    [StructLayout(LayoutKind.Sequential)]public struct MemoryProperties {public uint TypeCount;public fixed uint Types[64];public uint HeapCount;public fixed ulong Heaps[32];}
    [StructLayout(LayoutKind.Sequential)]public struct BufferInfo {public uint Type;public void* Next;public uint Flags;public ulong Size;public uint Usage,Sharing,FamilyCount;public uint* Families;}
    [StructLayout(LayoutKind.Sequential)]public struct MemoryRequirements {public ulong Size,Alignment;public uint TypeBits;}
    [StructLayout(LayoutKind.Sequential)]public struct AllocateInfo {public uint Type;public void* Next;public ulong Size;public uint MemoryType;}
    [StructLayout(LayoutKind.Sequential)]public struct ShaderInfo {public uint Type;public void* Next;public uint Flags;public nuint CodeSize;public uint* Code;}
    [StructLayout(LayoutKind.Sequential)]public struct ShaderStage {public uint Type;public void* Next;public uint Flags,Stage;public ulong Module;public byte* Name;public void* Specialization;}
    [StructLayout(LayoutKind.Sequential)]public struct PipelineLayoutInfo {public uint Type;public void* Next;public uint Flags,Count;public ulong* Layouts;public uint PushCount;public void* PushRanges;}
    [StructLayout(LayoutKind.Sequential)]public struct ComputePipelineInfo {public uint Type;public void* Next;public uint Flags;public ShaderStage Stage;public ulong Layout,Base;public int BaseIndex;}
    [StructLayout(LayoutKind.Sequential)]public struct LayoutBinding {public uint Binding,DescriptorType,Count,Stages;public void* Samplers;}
    [StructLayout(LayoutKind.Sequential)]public struct DescriptorLayoutInfo {public uint Type;public void* Next;public uint Flags,Count;public LayoutBinding* Bindings;}
    [StructLayout(LayoutKind.Sequential)]public struct PoolSize {public uint Type,Count;}
    [StructLayout(LayoutKind.Sequential)]public struct PoolInfo {public uint Type;public void* Next;public uint Flags,MaxSets,Count;public PoolSize* Sizes;}
    [StructLayout(LayoutKind.Sequential)]public struct SetInfo {public uint Type;public void* Next;public ulong Pool;public uint Count;public ulong* Layouts;}
    [StructLayout(LayoutKind.Sequential)]public struct DescriptorBuffer {public ulong Buffer,Offset,Range;}
    [StructLayout(LayoutKind.Sequential)]public struct WriteDescriptor {public uint Type;public void* Next;public ulong Set;public uint Binding,Element,Count,DescriptorType;public void* Image;public DescriptorBuffer* Buffer;public void* Texel;}
    [StructLayout(LayoutKind.Sequential)]public struct CommandPoolInfo {public uint Type;public void* Next;public uint Flags,Family;}
    [StructLayout(LayoutKind.Sequential)]public struct CommandAllocateInfo {public uint Type;public void* Next;public ulong Pool;public uint Level,Count;}
    [StructLayout(LayoutKind.Sequential)]public struct CommandBeginInfo {public uint Type;public void* Next;public uint Flags;public void* Inheritance;}
    [StructLayout(LayoutKind.Sequential)]public struct FenceInfo {public uint Type;public void* Next;public uint Flags;}
    [StructLayout(LayoutKind.Sequential)]public struct SubmitInfo {public uint Type;public void* Next;public uint WaitCount;public void* Waits;public void* Stages;public uint CommandCount;public IntPtr* Commands;public uint SignalCount;public void* Signals;}
    [StructLayout(LayoutKind.Sequential)]public struct BufferBarrier {public uint Type;public void* Next;public uint SourceAccess,DestinationAccess,SourceFamily,DestinationFamily;public ulong Buffer,Offset,Size;}
    [StructLayout(LayoutKind.Sequential)]public struct ImageInfo {public uint Type;public void* Next;public uint Flags,ImageType,Format,Width,Height,Depth,Mips,Layers,Samples,Tiling,Usage,Sharing,FamilyCount;public uint* Families;public uint InitialLayout;}
    [StructLayout(LayoutKind.Sequential)]public struct ImageViewInfo {public uint Type;public void* Next;public uint Flags;public ulong Image;public uint ViewType,Format,Red,Green,Blue,Alpha,Aspect,BaseMip,Mips,BaseLayer,Layers;}
    [StructLayout(LayoutKind.Sequential)]public struct SamplerInfo {public uint Type;public void* Next;public uint Flags,MagFilter,MinFilter,MipmapMode,AddressU,AddressV,AddressW;public float LodBias;public uint Anisotropy;public float MaxAnisotropy;public uint Compare,CompareOp;public float MinLod,MaxLod;public uint BorderColor,Unnormalized;}
    [StructLayout(LayoutKind.Sequential)]public struct DescriptorImage {public ulong Sampler,View;public uint Layout;}
    [StructLayout(LayoutKind.Sequential)]public struct ImageBarrier {public uint Type;public void* Next;public uint SourceAccess,DestinationAccess,OldLayout,NewLayout,SourceFamily,DestinationFamily;public ulong Image;public uint Aspect,BaseMip,Mips,BaseLayer,Layers;}
    [StructLayout(LayoutKind.Sequential)]public struct BufferImageCopy {public ulong Offset;public uint RowLength,ImageHeight,Aspect,Mip,BaseLayer,Layers;public int X,Y,Z;public uint Width,Height,Depth;}
    [DllImport(Library)]public static extern int vkCreateInstance(InstanceInfo* info,void* allocator,IntPtr* instance);
    [DllImport(Library)]public static extern void vkDestroyInstance(IntPtr instance,void* allocator);
    [DllImport(Library)]public static extern int vkEnumeratePhysicalDevices(IntPtr instance,uint* count,IntPtr* devices);
    [DllImport(Library)]public static extern void vkGetPhysicalDeviceProperties(IntPtr physical,byte* properties);
    [DllImport(Library)]public static extern void vkGetPhysicalDeviceQueueFamilyProperties(IntPtr physical,uint* count,QueueFamily* queues);
    [DllImport(Library)]public static extern void vkGetPhysicalDeviceMemoryProperties(IntPtr physical,MemoryProperties* properties);
    [DllImport(Library)]public static extern int vkCreateDevice(IntPtr physical,DeviceInfo* info,void* allocator,IntPtr* device);
    [DllImport(Library)]public static extern void vkDestroyDevice(IntPtr device,void* allocator);
    [DllImport(Library)]public static extern void vkGetDeviceQueue(IntPtr device,uint family,uint index,IntPtr* queue);
    [DllImport(Library)]public static extern int vkCreateBuffer(IntPtr device,BufferInfo* info,void* allocator,ulong* buffer);
    [DllImport(Library)]public static extern void vkDestroyBuffer(IntPtr device,ulong buffer,void* allocator);
    [DllImport(Library)]public static extern void vkGetBufferMemoryRequirements(IntPtr device,ulong buffer,MemoryRequirements* requirements);
    [DllImport(Library)]public static extern int vkAllocateMemory(IntPtr device,AllocateInfo* info,void* allocator,ulong* memory);
    [DllImport(Library)]public static extern void vkFreeMemory(IntPtr device,ulong memory,void* allocator);
    [DllImport(Library)]public static extern int vkBindBufferMemory(IntPtr device,ulong buffer,ulong memory,ulong offset);
    [DllImport(Library)]public static extern int vkMapMemory(IntPtr device,ulong memory,ulong offset,ulong size,uint flags,void** data);
    [DllImport(Library)]public static extern void vkUnmapMemory(IntPtr device,ulong memory);
    [DllImport(Library)]public static extern int vkCreateShaderModule(IntPtr device,ShaderInfo* info,void* allocator,ulong* module);
    [DllImport(Library)]public static extern void vkDestroyShaderModule(IntPtr device,ulong module,void* allocator);
    [DllImport(Library)]public static extern int vkCreateDescriptorSetLayout(IntPtr device,DescriptorLayoutInfo* info,void* allocator,ulong* layout);
    [DllImport(Library)]public static extern void vkDestroyDescriptorSetLayout(IntPtr device,ulong layout,void* allocator);
    [DllImport(Library)]public static extern int vkCreatePipelineLayout(IntPtr device,PipelineLayoutInfo* info,void* allocator,ulong* layout);
    [DllImport(Library)]public static extern void vkDestroyPipelineLayout(IntPtr device,ulong layout,void* allocator);
    [DllImport(Library)]public static extern int vkCreateComputePipelines(IntPtr device,ulong cache,uint count,ComputePipelineInfo* infos,void* allocator,ulong* pipeline);
    [DllImport(Library)]public static extern void vkDestroyPipeline(IntPtr device,ulong pipeline,void* allocator);
    [DllImport(Library)]public static extern int vkCreateDescriptorPool(IntPtr device,PoolInfo* info,void* allocator,ulong* pool);
    [DllImport(Library)]public static extern int vkResetDescriptorPool(IntPtr device,ulong pool,uint flags);
    [DllImport(Library)]public static extern void vkDestroyDescriptorPool(IntPtr device,ulong pool,void* allocator);
    [DllImport(Library)]public static extern int vkAllocateDescriptorSets(IntPtr device,SetInfo* info,ulong* sets);
    [DllImport(Library)]public static extern void vkUpdateDescriptorSets(IntPtr device,uint count,WriteDescriptor* writes,uint copies,void* copyInfo);
    [DllImport(Library)]public static extern int vkCreateCommandPool(IntPtr device,CommandPoolInfo* info,void* allocator,ulong* pool);
    [DllImport(Library)]public static extern void vkDestroyCommandPool(IntPtr device,ulong pool,void* allocator);
    [DllImport(Library)]public static extern int vkResetCommandPool(IntPtr device,ulong pool,uint flags);
    [DllImport(Library)]public static extern int vkAllocateCommandBuffers(IntPtr device,CommandAllocateInfo* info,IntPtr* buffer);
    [DllImport(Library)]public static extern int vkBeginCommandBuffer(IntPtr buffer,CommandBeginInfo* info);
    [DllImport(Library)]public static extern int vkEndCommandBuffer(IntPtr buffer);
    [DllImport(Library)]public static extern void vkCmdBindPipeline(IntPtr command,uint point,ulong pipeline);
    [DllImport(Library)]public static extern void vkCmdBindDescriptorSets(IntPtr command,uint point,ulong layout,uint first,uint count,ulong* sets,uint dynamicCount,uint* offsets);
    [StructLayout(LayoutKind.Sequential)]public struct BufferCopy {public ulong SourceOffset,DestinationOffset,Size;}
    [DllImport(Library)]public static extern void vkCmdFillBuffer(IntPtr command,ulong buffer,ulong offset,ulong size,uint value);
    [DllImport(Library)]public static extern void vkCmdCopyBuffer(IntPtr command,ulong source,ulong destination,uint count,BufferCopy* regions);
    [DllImport(Library)]public static extern void vkCmdDispatch(IntPtr command,uint x,uint y,uint z);
    [DllImport(Library)]public static extern void vkCmdPipelineBarrier(IntPtr command,uint source,uint destination,uint dependencies,uint memoryCount,void* memories,uint bufferCount,BufferBarrier* buffers,uint imageCount,void* images);
    [DllImport(Library)]public static extern int vkCreateFence(IntPtr device,FenceInfo* info,void* allocator,ulong* fence);
    [DllImport(Library)]public static extern void vkDestroyFence(IntPtr device,ulong fence,void* allocator);
    [DllImport(Library)]public static extern int vkResetFences(IntPtr device,uint count,ulong* fences);
    [DllImport(Library)]public static extern int vkWaitForFences(IntPtr device,uint count,ulong* fences,uint all,ulong timeout);
    [DllImport(Library)]public static extern int vkQueueSubmit(IntPtr queue,uint count,SubmitInfo* info,ulong fence);
    [DllImport(Library)]public static extern int vkDeviceWaitIdle(IntPtr device);
    [DllImport(Library)]public static extern int vkCreateImage(IntPtr device,ImageInfo* info,void* allocator,ulong* image);
    [DllImport(Library)]public static extern void vkDestroyImage(IntPtr device,ulong image,void* allocator);
    [DllImport(Library)]public static extern void vkGetImageMemoryRequirements(IntPtr device,ulong image,MemoryRequirements* requirements);
    [DllImport(Library)]public static extern int vkBindImageMemory(IntPtr device,ulong image,ulong memory,ulong offset);
    [DllImport(Library)]public static extern int vkCreateImageView(IntPtr device,ImageViewInfo* info,void* allocator,ulong* view);
    [DllImport(Library)]public static extern void vkDestroyImageView(IntPtr device,ulong view,void* allocator);
    [DllImport(Library)]public static extern int vkCreateSampler(IntPtr device,SamplerInfo* info,void* allocator,ulong* sampler);
    [DllImport(Library)]public static extern void vkDestroySampler(IntPtr device,ulong sampler,void* allocator);
    [DllImport(Library)]public static extern void vkCmdCopyBufferToImage(IntPtr command,ulong buffer,ulong image,uint layout,uint count,BufferImageCopy* regions);
}
