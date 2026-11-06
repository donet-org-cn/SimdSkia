namespace SimdSkia.Gpu;

internal readonly record struct VulkanImageDescription(ulong Image,ulong Memory,ulong AllocationSize,uint Format,uint Layout,uint Usage,uint Levels,uint QueueFamily);

// Transfer ownership is independent of the raster pipelines. Resources belong
// to the caller's real VkDevice/queue, and every transfer restores the declared
// image layout before signaling its completion fence.
internal sealed unsafe class VulkanTransfer:IDisposable
{
    internal BorrowedVulkanContext Handles {get;}
    private readonly int thread=Environment.CurrentManagedThreadId;
    private Vk.MemoryProperties memory;
    private ulong pool,fence,staging,stagingMemory,stagingAllocation;
    private nint command,mapped;
    private int stagingCapacity;
    private bool disposed,abandoned;
    private readonly List<WeakReference<VulkanImage>> images=[];
    internal VulkanTransfer(BorrowedVulkanContext handles)
    {
        if(handles.Instance==0||handles.PhysicalDevice==0||handles.Device==0||handles.Queue==0)throw new ArgumentException("A live borrowed Vulkan context is required.");Handles=handles;
        try
        {
            Vk.MemoryProperties properties=default;Vk.vkGetPhysicalDeviceMemoryProperties(handles.PhysicalDevice,&properties);memory=properties;if(memory.TypeCount>32)throw new InvalidOperationException("Invalid Vulkan memory type count.");
            uint families=0;Vk.vkGetPhysicalDeviceQueueFamilyProperties(handles.PhysicalDevice,&families,null);if(families>4096||handles.QueueFamily>=families)throw new ArgumentException("Invalid Vulkan transfer queue family.");
            var p=new Vk.CommandPoolInfo{Type=39,Flags=2,Family=handles.QueueFamily};ulong result=0;Result(Vk.vkCreateCommandPool(handles.Device,&p,null,&result));pool=result;
            var allocation=new Vk.CommandAllocateInfo{Type=40,Pool=pool,Count=1};nint buffer=0;Result(Vk.vkAllocateCommandBuffers(handles.Device,&allocation,&buffer));command=buffer;
            var fi=new Vk.FenceInfo{Type=8};Result(Vk.vkCreateFence(handles.Device,&fi,null,&result));fence=result;
        }
        catch{Dispose();throw;}
    }
    internal void Check(){ObjectDisposedException.ThrowIf(disposed,this);if(thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Vulkan image transfers require their creating thread and externally synchronized queue.");if(abandoned)throw new InvalidOperationException("The Vulkan transfer context is abandoned.");}
    private static void Result(int value){if(value==-4)throw new VulkanDeviceLostException("Vulkan image transfer lost its device.");if(value!=0)throw new InvalidOperationException($"Vulkan image transfer failed ({value}).");}
    private uint MemoryType(uint bits,uint required,uint preferred=0)
    {var properties=memory;uint fallback=uint.MaxValue;for(uint i=0;i<properties.TypeCount;i++)if((bits&(1u<<(int)i))!=0&&(properties.Types[i*2]&required)==required){fallback=i;if((properties.Types[i*2]&preferred)==preferred)return i;}return fallback!=uint.MaxValue?fallback:throw new NotSupportedException("No suitable Vulkan image transfer memory type.");}
    private void AllocateStaging(int bytes)
    {
        if(stagingCapacity>=bytes)return;ReleaseStaging();ulong buffer=0,allocation=0;void* pointer=null;
        try
        {
            var info=new Vk.BufferInfo{Type=12,Size=(ulong)bytes,Usage=3};Result(Vk.vkCreateBuffer(Handles.Device,&info,null,&buffer));Vk.MemoryRequirements requirements=default;Vk.vkGetBufferMemoryRequirements(Handles.Device,buffer,&requirements);
            var memoryInfo=new Vk.AllocateInfo{Type=5,Size=requirements.Size,MemoryType=MemoryType(requirements.TypeBits,6,8)};Result(Vk.vkAllocateMemory(Handles.Device,&memoryInfo,null,&allocation));Result(Vk.vkBindBufferMemory(Handles.Device,buffer,allocation,0));Result(Vk.vkMapMemory(Handles.Device,allocation,0,(ulong)bytes,0,&pointer));staging=buffer;stagingMemory=allocation;stagingAllocation=requirements.Size;mapped=(nint)pointer;stagingCapacity=bytes;
        }
        catch{if(pointer!=null)Vk.vkUnmapMemory(Handles.Device,allocation);if(buffer!=0)Vk.vkDestroyBuffer(Handles.Device,buffer,null);if(allocation!=0)Vk.vkFreeMemory(Handles.Device,allocation,null);throw;}
    }
    private void ReleaseStaging()
    {if(mapped!=0)Vk.vkUnmapMemory(Handles.Device,stagingMemory);if(staging!=0)Vk.vkDestroyBuffer(Handles.Device,staging,null);if(stagingMemory!=0)Vk.vkFreeMemory(Handles.Device,stagingMemory,null);mapped=0;staging=stagingMemory=stagingAllocation=0;stagingCapacity=0;}
    internal VulkanImage Import(VulkanImageDescription description,int width,int height,bool flip,AlphaFormat alpha,bool owned,Action? release=null)
    {
        Check();if(width<=0||height<=0||width>32768||height>32768||(long)width*height*4>128*1024*1024)throw new ArgumentOutOfRangeException(nameof(width));
        if(description.Image==0||description.Format is not (37 or 44)||description.Levels<1)throw new NotSupportedException("Only live single-layer RGBA/BGRA8888 Vulkan images can be imported.");
        if(description.QueueFamily!=uint.MaxValue&&description.QueueFamily!=Handles.QueueFamily)throw new NotSupportedException("Transfer the Vulkan image to this context's queue family before import.");
        if(owned&&description.Memory!=0&&description.AllocationSize==0)throw new NotSupportedException("A supplied adopted allocation must have a known dedicated size; leave Memory zero to adopt only VkImage.");
        var image=new VulkanImage(this,description,width,height,flip,alpha,owned,release);if(images.Count>=256)images.RemoveAll(w=>!w.TryGetTarget(out var old)||old.IsDisposed);images.Add(new(image));return image;
    }
    internal VulkanImage Create(PixelBuffer pixels,bool flip=false)
    {
        Check();int bytes=checked(pixels.Width*pixels.Height*4);if(pixels.Format==PixelFormat.Alpha8||pixels.Width>32768||pixels.Height>32768||bytes>128*1024*1024)throw new NotSupportedException("The Vulkan image exceeds the supported format/dimension/transfer budget.");uint format=pixels.Format==PixelFormat.Bgra8888?44u:37u;
        ulong image=0,allocation=0;
        try
        {
            var info=new Vk.ImageInfo{Type=14,ImageType=1,Format=format,Width=(uint)pixels.Width,Height=(uint)pixels.Height,Depth=1,Mips=1,Layers=1,Samples=1,Usage=23};Result(Vk.vkCreateImage(Handles.Device,&info,null,&image));Vk.MemoryRequirements requirements=default;Vk.vkGetImageMemoryRequirements(Handles.Device,image,&requirements);var allocate=new Vk.AllocateInfo{Type=5,Size=requirements.Size,MemoryType=MemoryType(requirements.TypeBits,0,1)};Result(Vk.vkAllocateMemory(Handles.Device,&allocate,null,&allocation));Result(Vk.vkBindImageMemory(Handles.Device,image,allocation,0));
            var result=Import(new(image,allocation,requirements.Size,format,0,23,1,Handles.QueueFamily),pixels.Width,pixels.Height,flip,pixels.Alpha,true);image=allocation=0;try{Write(result,pixels);return result;}catch{result.Dispose();throw;}
        }
        finally{if(image!=0)Vk.vkDestroyImage(Handles.Device,image,null);if(allocation!=0)Vk.vkFreeMemory(Handles.Device,allocation,null);}
    }
    internal PixelBuffer Read(VulkanImage image)
    {
        Check();if(image.Description.Layout==0)throw new InvalidOperationException("An undefined Vulkan image has no pixels to read.");int length=checked(image.Width*image.Height*4);AllocateStaging(length);Transfer(image,false);var result=PixelBuffer.Rent(image.Width,image.Height,image.Description.Format==44?PixelFormat.Bgra8888:PixelFormat.Rgba8888,image.Alpha);
        try{var bytes=new ReadOnlySpan<byte>((void*)mapped,length);for(int y=0;y<image.Height;y++)bytes.Slice((image.Flip?image.Height-1-y:y)*image.Width*4,image.Width*4).CopyTo(result.GetRowSpan(y));return result;}catch{result.Dispose();throw;}
    }
    internal void Write(VulkanImage image,PixelBuffer pixels)
    {
        Check();if(pixels.Width!=image.Width||pixels.Height!=image.Height||pixels.Format==PixelFormat.Alpha8)throw new ArgumentException("Vulkan transfer pixel dimensions/format do not match.");int length=checked(image.Width*image.Height*4);AllocateStaging(length);var target=new Span<byte>((void*)mapped,length);bool bgra=image.Description.Format==44;
        for(int y=0;y<image.Height;y++){var row=target.Slice((image.Flip?image.Height-1-y:y)*image.Width*4,image.Width*4);var input=pixels.GetRowSpan(y)[..(image.Width*4)];if((pixels.Format==PixelFormat.Bgra8888)==bgra)input.CopyTo(row);else SimdSkia.Cpu.PixelTransferKernel.SwapRedBlue(input,row,SimdSkia.Cpu.CpuOptions.EnableSimd);if(pixels.Alpha==AlphaFormat.Opaque)for(int x=0;x<image.Width;x++)row[x*4+3]=255;}
        Transfer(image,true);
    }
    private void Transfer(VulkanImage image,bool write)
    {
        var description=image.Description;if((description.Usage&(write?2u:1u))==0)throw new NotSupportedException("The Vulkan image was not created with the required transfer usage.");
        ulong currentFence=fence;Result(Vk.vkResetFences(Handles.Device,1,&currentFence));Result(Vk.vkResetCommandPool(Handles.Device,pool,0));var begin=new Vk.CommandBeginInfo{Type=42,Flags=1};Result(Vk.vkBeginCommandBuffer(command,&begin));
        uint layout=write?7u:6u,access=write?4096u:2048u;
        var imageBarrier=new Vk.ImageBarrier{Type=45,Image=description.Image,OldLayout=description.Layout,NewLayout=layout,SourceAccess=description.Layout==0?0u:0x18000,DestinationAccess=access,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Aspect=1,Mips=1,Layers=1};Vk.vkCmdPipelineBarrier(command,description.Layout==0?1u:0x10000,4096,0,0,null,0,null,1,&imageBarrier);
        var stagingBarrier=new Vk.BufferBarrier{Type=44,Buffer=staging,Size=(ulong)(image.Width*image.Height*4),SourceAccess=write?16384u:8192u,DestinationAccess=write?2048u:4096u,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue};Vk.vkCmdPipelineBarrier(command,16384,4096,0,0,null,1,&stagingBarrier,0,null);
        var region=new Vk.BufferImageCopy{Aspect=1,Layers=1,Width=(uint)image.Width,Height=(uint)image.Height,Depth=1};if(write)Vk.vkCmdCopyBufferToImage(command,staging,description.Image,layout,1,&region);else Vk.vkCmdCopyImageToBuffer(command,description.Image,layout,staging,1,&region);
        imageBarrier.SourceAccess=access;imageBarrier.DestinationAccess=0x18000;imageBarrier.OldLayout=layout;imageBarrier.NewLayout=description.Layout==0?5u:description.Layout;Vk.vkCmdPipelineBarrier(command,4096,0x10000,0,0,null,0,null,1,&imageBarrier);
        if(!write){stagingBarrier.SourceAccess=4096;stagingBarrier.DestinationAccess=8192;Vk.vkCmdPipelineBarrier(command,4096,16384,0,0,null,1,&stagingBarrier,0,null);}
        Result(Vk.vkEndCommandBuffer(command));nint recorded=command;var submit=new Vk.SubmitInfo{Type=4,CommandCount=1,Commands=&recorded};bool submitted=false,completed=false;
        try{Result(Vk.vkQueueSubmit(Handles.Queue,1,&submit,fence));submitted=true;Result(Vk.vkWaitForFences(Handles.Device,1,&currentFence,1,10_000_000_000));completed=true;image.SetLayout(imageBarrier.NewLayout);}
        finally{if(submitted&&!completed)Vk.vkDeviceWaitIdle(Handles.Device);}
    }
    internal void Release(VulkanImage image,bool destroy)
    {var description=image.Description;if(destroy&&image.Owned&&!abandoned){if(description.Image!=0)Vk.vkDestroyImage(Handles.Device,description.Image,null);if(description.Memory!=0)Vk.vkFreeMemory(Handles.Device,description.Memory,null);}image.Invalidate();}
    internal void Abandon(bool releaseResources)
    {if(disposed||abandoned)return;Check();foreach(var weak in images)if(weak.TryGetTarget(out var image)&&!image.IsDisposed&&image.HasResource)Release(image,releaseResources);images.Clear();if(releaseResources)ReleaseStaging();else{staging=stagingMemory=stagingAllocation=0;mapped=0;stagingCapacity=0;}abandoned=!releaseResources;}
    public void Dispose()
    {if(disposed)return;if(thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Dispose Vulkan transfers on their creating thread.");if(!abandoned){Abandon(true);if(fence!=0)Vk.vkDestroyFence(Handles.Device,fence,null);if(pool!=0)Vk.vkDestroyCommandPool(Handles.Device,pool,null);}pool=fence=0;disposed=true;}
}

internal sealed class VulkanImage:IGpuImage
{
    internal VulkanTransfer Device {get;}
    private VulkanImageDescription description;
    internal VulkanImageDescription Description {get{ObjectDisposedException.ThrowIf(disposed,this);if(description.Image==0)throw new InvalidOperationException("The Vulkan image's context was released or abandoned.");return description;}}
    internal int Width {get;}
    internal int Height {get;}
    internal bool Flip {get;}
    internal AlphaFormat Alpha {get;}
    internal bool Owned {get;}
    private bool disposed;
    internal bool IsDisposed=>disposed;
    internal bool HasResource=>description.Image!=0;
    private Action? release;
    internal VulkanImage(VulkanTransfer device,VulkanImageDescription description,int width,int height,bool flip,AlphaFormat alpha,bool owned,Action? release)
    {Device=device;this.description=description;Width=width;Height=height;Flip=flip;Alpha=alpha;Owned=owned;this.release=release;}
    internal void SetLayout(uint layout)=>description=description with{Layout=layout};
    internal void Invalidate()=>description=default;
    public PixelBuffer ReadPixels()=>Device.Read(this);
    public void Dispose(){if(disposed)return;if(description.Image!=0){Device.Check();Device.Release(this,true);}disposed=true;Interlocked.Exchange(ref release,null)?.Invoke();}
}
