using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SimdSkia.Gpu;

public sealed class VulkanDeviceLostException(string message) : Exception(message);

/// <summary>Vulkan 1.0 compute rasterizer. Coverage and compositing execute on the device.</summary>
public sealed unsafe class VulkanCanvas : ICanvasBackend
{
    private readonly int ownerThread=Environment.CurrentManagedThreadId;
    private readonly PixelBuffer target;
    private readonly bool optimizedCoverage;
    private IntPtr instance,physical,device,queue,command;
    private ulong descriptorLayout,pipelineLayout,pipeline,descriptorPool,commandPool,fence,sampler;
    private Vk.MemoryProperties memory;
    private GpuBuffer? output,readback,empty,edgesArena,paramsArena;
    private bool outputInitialized;
    private Texture? emptyTexture;
    private readonly List<Queued> pending=[];
    private readonly Dictionary<PixelBuffer,ImageEntry> imageCache=[];
    private readonly List<Texture> retiredTextures=[];
    private readonly Dictionary<(PathGeometry Geometry,Matrix3 Transform),EdgeEntry> edgeCache=[];
    private bool disposed,faulted;
    private long useCounter;
    private long edgeBytes;
    private ulong[] descriptorSets=[];
    public string DeviceName {get;private set;}="";
    public bool IsSoftwareDevice {get;private set;}
    public bool IsFaulted=>faulted;
    public int CompletedBatches {get;private set;}
    public int CachedTextures=>imageCache.Count;
    public double LastSubmitMilliseconds {get;private set;}
    public double LastCompletionMilliseconds {get;private set;}
    private readonly record struct Queued(DrawingCommand Command,EdgeEntry? Path,Texture? Texture)
    {
        public float[] Edges=>Path?.Edges??[];
        public float[] Data=>Path?.Tiles?.Data??Edges;
    }
    private sealed class ImageEntry
    {
        public required Texture Texture;public required byte[] Snapshot;public long Used;
    }
    private sealed class EdgeEntry
    {
        public required PathGeometry Geometry;public required float[] Edges;public TiledEdges? Tiles;public long Used;public long Bytes=>(long)Edges.Length*4+(long)(Tiles?.Data.Length??0)*4+(long)Geometry.Segments.Length*32;
    }
    private sealed class Texture(IntPtr device,int width,int height) : IDisposable
    {
        public ulong Image,Memory,View;
        public GpuBuffer? Staging;
        public bool Uploaded;
        public int Width=>width;
        public int Height=>height;
        public int Size=>checked(width*height*4);
        public Vk.DescriptorImage Descriptor(ulong sampler)=>new(){Sampler=sampler,View=View,Layout=5};
        public void Dispose(){Staging?.Dispose();Staging=null;if(View!=0){Vk.vkDestroyImageView(device,View,null);View=0;}if(Image!=0){Vk.vkDestroyImage(device,Image,null);Image=0;}if(Memory!=0){Vk.vkFreeMemory(device,Memory,null);Memory=0;}}
    }

    public VulkanCanvas(PixelBuffer target):this(target,true) { }
    internal VulkanCanvas(PixelBuffer target,bool optimizedCoverage)
    {
        this.optimizedCoverage=optimizedCoverage;
        ArgumentNullException.ThrowIfNull(target);
        if(target.Format!=PixelFormat.Rgba8888||target.Alpha!=AlphaFormat.Premultiplied)throw new NotSupportedException("Vulkan offscreen targets currently require RGBA8888 premultiplied alpha.");
        if((long)target.Width*target.Height*4>128*1024*1024||target.Width>32768||target.Height>32768)throw new NotSupportedException("The initial Vulkan offscreen target is limited to 128 MiB and 32768 pixels per dimension.");
        this.target=target.Share();
        try{Initialize();}
        catch{DisposeCore();throw;}
    }
    private void Initialize()
    {
        var info=new Vk.InstanceInfo {Type=1};IntPtr created=default;Check(Vk.vkCreateInstance(&info,null,&created));instance=created;
        uint count=0;Check(Vk.vkEnumeratePhysicalDevices(instance,&count,null));
        if(count==0)throw new InvalidOperationException("No Vulkan device was found. Install a Vulkan-capable driver; no CPU rendering fallback is substituted.");
        var devices=new IntPtr[count];fixed(IntPtr* ptr=devices)Check(Vk.vkEnumeratePhysicalDevices(instance,&count,ptr));
        int score=-1;uint family=0;
        byte* properties=stackalloc byte[4096];
        foreach(IntPtr candidate in devices)
        {
            Vk.vkGetPhysicalDeviceProperties(candidate,properties);
            uint type=*(uint*)(properties+16);int rank=type==2?3:type==1?2:type==3?1:0;
            uint queueCount=0;Vk.vkGetPhysicalDeviceQueueFamilyProperties(candidate,&queueCount,null);var families=new Vk.QueueFamily[queueCount];
            fixed(Vk.QueueFamily* ptr=families)Vk.vkGetPhysicalDeviceQueueFamilyProperties(candidate,&queueCount,ptr);
            for(uint i=0;i<queueCount;i++)if((families[i].Flags&2)!=0&&families[i].Count>0&&rank>score)
            {
                score=rank;physical=candidate;family=i;IsSoftwareDevice=type==4;
                var name=new ReadOnlySpan<byte>(properties+20,256);int terminator=name.IndexOf((byte)0);DeviceName=Encoding.UTF8.GetString(terminator>=0?name[..terminator]:name);break;
            }
        }
        if(physical==IntPtr.Zero)throw new InvalidOperationException("No Vulkan compute queue is available.");
        float priority=1;var q=new Vk.QueueInfo {Type=2,Family=family,Count=1,Priorities=&priority};var d=new Vk.DeviceInfo {Type=3,QueueCount=1,Queues=&q};
        Check(Vk.vkCreateDevice(physical,&d,null,&created));device=created;Vk.vkGetDeviceQueue(device,family,0,&created);queue=created;
        Vk.MemoryProperties propertiesMemory=default;Vk.vkGetPhysicalDeviceMemoryProperties(physical,&propertiesMemory);memory=propertiesMemory;
        var bindings=stackalloc Vk.LayoutBinding[4];for(uint i=0;i<4;i++)bindings[i]=new(){Binding=i,DescriptorType=i==3?1u:7u,Count=1,Stages=32};
        var layoutInfo=new Vk.DescriptorLayoutInfo {Type=32,Count=4,Bindings=bindings};ulong handle=0;Check(Vk.vkCreateDescriptorSetLayout(device,&layoutInfo,null,&handle));descriptorLayout=handle;
        ulong descriptorHandle=descriptorLayout;var pLayout=new Vk.PipelineLayoutInfo {Type=30,Count=1,Layouts=&descriptorHandle};Check(Vk.vkCreatePipelineLayout(device,&pLayout,null,&handle));pipelineLayout=handle;
        using var stream=typeof(VulkanCanvas).Assembly.GetManifestResourceStream("SimdSkia.Gpu.Shaders.renderer.spv")??throw new InvalidOperationException("SPIR-V resource missing.");
        using var copy=new MemoryStream();stream.CopyTo(copy);byte[] code=copy.ToArray();if(code.Length%4!=0)throw new InvalidDataException("Invalid SPIR-V alignment.");
        ulong module=0;
        fixed(byte* bytes=code){var shaderInfo=new Vk.ShaderInfo {Type=16,CodeSize=(nuint)code.Length,Code=(uint*)bytes};Check(Vk.vkCreateShaderModule(device,&shaderInfo,null,&module));}
        try
        {
            byte* name=stackalloc byte[5]{(byte)'m',(byte)'a',(byte)'i',(byte)'n',0};
            var pipelineInfo=new Vk.ComputePipelineInfo {Type=29,Stage=new(){Type=18,Stage=32,Module=module,Name=name},Layout=pipelineLayout,BaseIndex=-1};
            Check(Vk.vkCreateComputePipelines(device,0,1,&pipelineInfo,null,&handle));pipeline=handle;
        }
        finally{Vk.vkDestroyShaderModule(device,module,null);}
        var samplerInfo=new Vk.SamplerInfo {Type=31,AddressU=2,AddressV=2,AddressW=2};Check(Vk.vkCreateSampler(device,&samplerInfo,null,&handle));sampler=handle;
        var commandInfo=new Vk.CommandPoolInfo {Type=39,Flags=2,Family=family};Check(Vk.vkCreateCommandPool(device,&commandInfo,null,&handle));commandPool=handle;
        var allocation=new Vk.CommandAllocateInfo {Type=40,Pool=commandPool,Count=1};Check(Vk.vkAllocateCommandBuffers(device,&allocation,&created));command=created;
        var fenceInfo=new Vk.FenceInfo {Type=8};Check(Vk.vkCreateFence(device,&fenceInfo,null,&handle));fence=handle;
        int targetBytes=checked(target.Width*target.Height*4);
        output=Allocate(targetBytes,usage:35,deviceLocal:true);readback=Allocate(targetBytes,usage:2,preferCached:true);
        empty=Allocate(16);empty.Bytes.Clear();emptyTexture=AllocateTexture(1,1,[0,0,0,0]);
        AllocateDescriptorSets(128);
    }
    // Flush is synchronous (fence waited before returning), so the GPU is idle here and
    // the old pool can be destroyed even though its sets were used by earlier submissions.
    private void AllocateDescriptorSets(int count)
    {
        if(descriptorPool!=0)Vk.vkDestroyDescriptorPool(device,descriptorPool,null);
        var poolSizes=stackalloc Vk.PoolSize[2];poolSizes[0]=new(){Type=7,Count=(uint)(count*3)};poolSizes[1]=new(){Type=1,Count=(uint)count};
        ulong handle=0;var poolInfo=new Vk.PoolInfo {Type=33,MaxSets=(uint)count,Count=2,Sizes=poolSizes};Check(Vk.vkCreateDescriptorPool(device,&poolInfo,null,&handle));descriptorPool=handle;
        var layouts=stackalloc ulong[count];for(int i=0;i<count;i++)layouts[i]=descriptorLayout;
        descriptorSets=new ulong[count];
        fixed(ulong* sets=descriptorSets){var setAllocation=new Vk.SetInfo {Type=34,Pool=descriptorPool,Count=(uint)count,Layouts=layouts};Check(Vk.vkAllocateDescriptorSets(device,&setAllocation,sets));}
    }
    private void Validate()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(Environment.CurrentManagedThreadId!=ownerThread)throw new InvalidOperationException("Vulkan surface operations must run on their creating thread (queue external synchronization).");
        if(faulted)throw new InvalidOperationException("Vulkan resources are invalid after a submission/device error. Dispose and recreate the surface.");target.ThrowIfDisposed();
    }
    public void Execute(in DrawingCommand command)
    {
        Validate();if(!Enum.IsDefined(command.Kind)||!Enum.IsDefined(command.Paint.Composite))throw new NotSupportedException("Unknown Vulkan drawing command/composite mode.");
        if(!command.Transform.IsFinite||!command.Transform.IsAffine)throw new NotSupportedException("Vulkan drawing requires a finite affine transform.");
        if(!command.Rectangle.IsFinite)throw new ArgumentException("Rectangle coordinates must be finite.");
        if(command.Clip is {} clip&&!clip.IsFinite)throw new ArgumentException("Clip coordinates must be finite.");
        if(command.Paint.Shader is {} shader)
        {float dx=shader.End.X-shader.Start.X,dy=shader.End.Y-shader.Start.Y;if(!float.IsFinite(dx*dx+dy*dy))throw new NotSupportedException("Gradient coordinates exceed Vulkan float32 precision.");}
        if(pending.Count>=128)Flush();EdgeEntry? edges=null;
        if(command.Kind==CommandKind.FillPath)
        {
            var geometry=command.Path??throw new ArgumentException("Path geometry missing.");
            edges=ResolveEdges(geometry,command.Transform);
        }
        Texture? texture=null;
        if(command.Kind==CommandKind.DrawImage)
        {
            var image=command.Image??throw new ArgumentException("Image pixels missing.");image.ThrowIfDisposed();
            if(!imageCache.TryGetValue(image,out var entry)||!MatchesSnapshot(image,entry.Snapshot))
            {
                // The stale texture may still be referenced by queued commands in this batch;
                // retire it instead of destroying it and free it once the GPU is idle after Flush.
                if(entry is not null){retiredTextures.Add(entry.Texture);imageCache.Remove(image);}
                var data=new byte[checked(image.Width*image.Height*4)];
                for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
                {var color=image.ReadPixel(x,y);int offset=(y*image.Width+x)*4;data[offset]=color.R;data[offset+1]=color.G;data[offset+2]=color.B;data[offset+3]=image.Alpha==AlphaFormat.Opaque?(byte)255:color.A;}
                if(data.Length>64*1024*1024)throw new NotSupportedException("A texture exceeds the configured 64 MiB image cache budget.");
                long bytes=imageCache.Values.Sum(e=>(long)e.Texture.Size);
                if(imageCache.Count>=32||bytes+data.Length>64*1024*1024)
                {
                    Flush();foreach(var victim in imageCache.OrderBy(e=>e.Value.Used).ToArray())
                    {if(imageCache.Count<32&&bytes+data.Length<=64*1024*1024)break;bytes-=victim.Value.Texture.Size;victim.Value.Texture.Dispose();imageCache.Remove(victim.Key);}
                }
                var snapshot=new byte[data.Length];for(int y=0;y<image.Height;y++)image.GetRowSpan(y).Slice(0,image.Width*4).CopyTo(snapshot.AsSpan(y*image.Width*4,image.Width*4));
                entry=new(){Texture=AllocateTexture(image.Width,image.Height,data),Snapshot=snapshot};imageCache.Add(image,entry);
            }
            entry.Used=++useCounter;texture=entry.Texture;
        }
        pending.Add(new(command with {Image=null},edges,texture));
    }
    private EdgeEntry ResolveEdges(PathGeometry geometry,Matrix3 transform)
    {
        if(edgeCache.TryGetValue((geometry,transform),out var cached)){cached.Used=++useCounter;return cached;}
        var contours=geometry.Flatten(transform);
        int count=0;foreach(var contour in contours)for(int i=0;i<contour.Length;i++)if(contour[i]!=contour[(i+1)%contour.Length])count++;
        var edges=new float[checked(count*4)];int at=0;
        foreach(var contour in contours)for(int i=0;i<contour.Length;i++)
        {var a=contour[i];var b=contour[(i+1)%contour.Length];if(a==b)continue;edges[at++]=a.X;edges[at++]=a.Y;edges[at++]=b.X;edges[at++]=b.Y;}
        var entry=new EdgeEntry{Geometry=geometry,Edges=edges,Tiles=optimizedCoverage?TiledEdges.Create(contours,edges,target.Width,target.Height):null,Used=++useCounter};
        edgeBytes+=entry.Bytes;
        edgeCache.Add((geometry,transform),entry);
        foreach(var victim in edgeCache.OrderBy(e=>e.Value.Used).ToArray())
        {if(edgeCache.Count<=64&&edgeBytes<=16*1024*1024)break;edgeBytes-=victim.Value.Bytes;edgeCache.Remove(victim.Key);}
        return entry;
    }
    private static bool MatchesSnapshot(PixelBuffer image,byte[] snapshot)
    {
        for(int y=0;y<image.Height;y++)
            if(!image.GetRowSpan(y).Slice(0,image.Width*4).SequenceEqual(snapshot.AsSpan(y*image.Width*4,image.Width*4)))return false;
        return true;
    }
    public void Flush()
    {
        Validate();if(pending.Count==0)return;
        bool submitted=false,completed=false;
        int edgeStride=256,paramStride=256;
        int edgeBytesNeeded=pending.Sum(p=>((p.Data.Length*4+255)&~255))+edgeStride,paramBytesNeeded=pending.Count*paramStride+paramStride;
        try
        {
            if(edgesArena is null||edgesArena.Size<edgeBytesNeeded)
            {var old=edgesArena;edgesArena=edgeBytesNeeded>edgeStride?Allocate(edgeBytesNeeded):null;old?.Dispose();}
            if(paramsArena is null||paramsArena.Size<paramBytesNeeded)
            {var old=paramsArena;paramsArena=Allocate(paramBytesNeeded);old?.Dispose();}
            if(descriptorSets is null||descriptorSets.Length<pending.Count)AllocateDescriptorSets(Math.Max(pending.Count,(descriptorSets?.Length??0)*2));
            ulong waitFence=fence;Check(Vk.vkResetFences(device,1,&waitFence));var begin=new Vk.CommandBeginInfo {Type=42,Flags=1};Check(Vk.vkBeginCommandBuffer(command,&begin));
            if(!outputInitialized)
            {
                Vk.vkCmdFillBuffer(command,output!.Handle,0,(ulong)output.Size,0);
                var initialize=new Vk.BufferBarrier {Type=44,SourceAccess=4096,DestinationAccess=32|64,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Buffer=output.Handle,Size=(ulong)output.Size};
                Vk.vkCmdPipelineBarrier(command,4096,2048,0,0,null,1,&initialize,0,null);
            }
            Vk.vkCmdBindPipeline(command,1,pipeline);
            foreach(var texture in imageCache.Values.Select(e=>e.Texture).Concat(retiredTextures).Prepend(emptyTexture!).Where(t=>!t.Uploaded))RecordTextureUpload(texture);
            var buffers=stackalloc Vk.DescriptorBuffer[4];var writes=stackalloc Vk.WriteDescriptor[4];int setIndex=0,edgeOffset=0,paramOffset=0;
            Span<uint> words=stackalloc uint[46];
            foreach(var item in pending)
            {
                var cmd=item.Command;
                if(cmd.Kind!=CommandKind.Clear&&!cmd.Transform.TryInvert(out _))continue;
                int edgeSize=item.Data.Length*4;var edges=empty!;int edgeBase=0;
                if(edgeSize>0)
                {
                    edgeBase=(edgeOffset+255)&~255;
                    MemoryMarshal.AsBytes(item.Data.AsSpan()).CopyTo(edgesArena!.Bytes.Slice(edgeBase,edgeSize));
                    edges=edgesArena;edgeOffset=edgeBase+edgeSize;
                }
                int paramBase=(paramOffset+255)&~255;var parameters=paramsArena!;int paramSize=46*4;Prepare(item,words);words.CopyTo(MemoryMarshal.Cast<byte,uint>(parameters.Bytes.Slice(paramBase,paramSize)));paramOffset=paramBase+paramSize;
                ulong set=descriptorSets![setIndex++];
                buffers[0]=output!.Descriptor;
                buffers[1]=new(){Buffer=edges.Handle,Offset=(ulong)(edges==empty?0:edgeBase),Range=(ulong)(edgeSize>0?edgeSize:edges.Size)};
                buffers[2]=new(){Buffer=parameters.Handle,Offset=(ulong)paramBase,Range=(ulong)paramSize};
                for(uint i=0;i<3;i++)writes[i]=new(){Type=35,Set=set,Binding=i,Count=1,DescriptorType=7,Buffer=buffers+i};
                var image=(item.Texture??emptyTexture!).Descriptor(sampler);writes[3]=new(){Type=35,Set=set,Binding=3,Count=1,DescriptorType=1,Image=&image};
                Vk.vkUpdateDescriptorSets(device,4,writes,0,null);Vk.vkCmdBindDescriptorSets(command,1,pipelineLayout,0,1,&set,0,null);
                uint bw=words[39],bh=words[40];
                if(bw>0&&bh>0)
                {
                    Vk.vkCmdDispatch(command,(bw+7)/8,(bh+7)/8,1);
                    uint originY=words[38],stride=(uint)(target.Width*4);var barrier=new Vk.BufferBarrier {Type=44,SourceAccess=64,DestinationAccess=32|64,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Buffer=output.Handle,Offset=originY*stride,Size=bh*stride};
                    Vk.vkCmdPipelineBarrier(command,2048,2048,0,0,null,1,&barrier,0,null);
                }
            }
            var transferBarrier=new Vk.BufferBarrier {Type=44,SourceAccess=64,DestinationAccess=2048,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Buffer=output!.Handle,Size=(ulong)output.Size};
            Vk.vkCmdPipelineBarrier(command,2048,4096,0,0,null,1,&transferBarrier,0,null);
            var copyRegion=new Vk.BufferCopy {Size=(ulong)output.Size};Vk.vkCmdCopyBuffer(command,output.Handle,readback!.Handle,1,&copyRegion);
            var hostBarrier=new Vk.BufferBarrier {Type=44,SourceAccess=4096,DestinationAccess=8192,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Buffer=readback.Handle,Size=(ulong)readback.Size};
            Vk.vkCmdPipelineBarrier(command,4096,16384,0,0,null,1,&hostBarrier,0,null);Check(Vk.vkEndCommandBuffer(command));
            IntPtr recorded=command;var submit=new Vk.SubmitInfo {Type=4,CommandCount=1,Commands=&recorded};var timer=Stopwatch.StartNew();
            Check(Vk.vkQueueSubmit(queue,1,&submit,fence));submitted=true;LastSubmitMilliseconds=timer.Elapsed.TotalMilliseconds;
            int result=Vk.vkWaitForFences(device,1,&waitFence,1,10_000_000_000);LastCompletionMilliseconds=timer.Elapsed.TotalMilliseconds;
            if(result==2)throw new TimeoutException("Vulkan completion fence timed out; the frame is not reported as completed.");Check(result);completed=true;
            outputInitialized=true;
            if(target.RowBytes==target.Width*4)readback.Bytes.CopyTo(target.GetPixelSpan());
            else for(int y=0;y<target.Height;y++)readback.Bytes.Slice(y*target.Width*4,target.Width*4).CopyTo(target.GetRowSpan(y));
            foreach(var texture in imageCache.Values.Select(e=>e.Texture).Prepend(emptyTexture!)){texture.Uploaded=true;texture.Staging?.Dispose();texture.Staging=null;}
            foreach(var retired in retiredTextures)retired.Dispose();retiredTextures.Clear();
            CompletedBatches++;pending.Clear();TrimTextures();
        }
        catch{faulted=true;throw;}
        finally
        {
            // A timed-out submission may still reference temporaries. Wait before freeing any memory.
            if(submitted&&!completed)Vk.vkDeviceWaitIdle(device);
        }
    }
    private void Prepare(Queued item,Span<uint> words)
    {
        var cmd=item.Command;words.Clear();var floats=MemoryMarshal.Cast<uint,float>(words);
        words[0]=(uint)target.Width;words[1]=(uint)target.Height;words[2]=(uint)cmd.Kind;words[3]=(uint)cmd.Paint.Composite;words[4]=Pack(cmd.Paint.Color);words[5]=cmd.Paint.Antialias?16u:1u;words[6]=(uint)(cmd.Path?.FillRule??FillRule.Winding);words[7]=(uint)(item.Edges.Length/4);
        var clip=cmd.Kind==CommandKind.Clear?new RectF(0,0,target.Width,target.Height):cmd.Clip??new(0,0,target.Width,target.Height);
        floats[8]=clip.Left;floats[9]=clip.Top;floats[10]=clip.Right;floats[11]=clip.Bottom;
        var rect=cmd.Rectangle;if(cmd.Kind==CommandKind.FillRectangle)rect=new(MathF.Min(rect.Left,rect.Right),MathF.Min(rect.Top,rect.Bottom),MathF.Max(rect.Left,rect.Right),MathF.Max(rect.Top,rect.Bottom));
        floats[12]=rect.Left;floats[13]=rect.Top;floats[14]=rect.Right;floats[15]=rect.Bottom;
        words[19]=(uint)(item.Texture?.Width??1);words[20]=(uint)(item.Texture?.Height??1);
        (cmd.Kind==CommandKind.FillPath?cmd.ShaderTransform??cmd.Transform:cmd.Transform).TryInvert(out var inverse);floats[22]=inverse.M00;floats[23]=inverse.M01;floats[24]=inverse.M02;floats[25]=inverse.M10;floats[26]=inverse.M11;floats[27]=inverse.M12;
        if(cmd.Paint.Shader is {} gradient){words[16]=1;words[17]=Pack(gradient.StartColor);words[18]=Pack(gradient.EndColor);floats[28]=gradient.Start.X;floats[29]=gradient.Start.Y;floats[30]=gradient.End.X;floats[31]=gradient.End.Y;}
        if(cmd.Kind==CommandKind.FillRectangle&&cmd.Transform.M01==0&&cmd.Transform.M10==0)
        {
            words[32]=1;var a=cmd.Transform.Map(rect.Left,rect.Top);var b=cmd.Transform.Map(rect.Right,rect.Bottom);
            floats[33]=MathF.Floor(MathF.Min(a.X,b.X)+.5f);floats[34]=MathF.Floor(MathF.Min(a.Y,b.Y)+.5f);floats[35]=MathF.Floor(MathF.Max(a.X,b.X)+.5f);floats[36]=MathF.Floor(MathF.Max(a.Y,b.Y)+.5f);
        }
        ComputeBounds(cmd,item.Edges,clip,rect,out var originX,out var originY,out var extentX,out var extentY);
        words[37]=(uint)originX;words[38]=(uint)originY;words[39]=(uint)extentX;words[40]=(uint)extentY;
        if(item.Path?.Tiles is {} tiles){words[41]=(uint)tiles.Columns;words[42]=(uint)tiles.Rows;words[43]=(uint)tiles.Left;words[44]=(uint)tiles.Top;}
        words[45]=optimizedCoverage?1u:0u;
    }
    private void ComputeBounds(in DrawingCommand cmd,float[] edges,RectF clip,RectF rect,out int originX,out int originY,out int extentX,out int extentY)
    {
        float minX,maxX,minY,maxY;
        if(cmd.Kind==CommandKind.Clear||(uint)(cmd.Path?.FillRule??FillRule.Winding)>=2){minX=0;minY=0;maxX=target.Width;maxY=target.Height;}
        else if(cmd.Kind==CommandKind.FillPath&&edges.Length>0)
        {
            minX=maxX=edges[0];minY=maxY=edges[1];
            for(int i=0;i<edges.Length;i+=2){float x=edges[i],y=edges[i+1];if(x<minX)minX=x;if(x>maxX)maxX=x;if(y<minY)minY=y;if(y>maxY)maxY=y;}
        }
        else
        {
            var a=cmd.Transform.Map(rect.Left,rect.Top);var b=cmd.Transform.Map(rect.Right,rect.Top);var c=cmd.Transform.Map(rect.Right,rect.Bottom);var d=cmd.Transform.Map(rect.Left,rect.Bottom);
            minX=MathF.Min(MathF.Min(a.X,b.X),MathF.Min(c.X,d.X));maxX=MathF.Max(MathF.Max(a.X,b.X),MathF.Max(c.X,d.X));
            minY=MathF.Min(MathF.Min(a.Y,b.Y),MathF.Min(c.Y,d.Y));maxY=MathF.Max(MathF.Max(a.Y,b.Y),MathF.Max(c.Y,d.Y));
        }
        if(minX<clip.Left)minX=clip.Left;if(maxX>clip.Right)maxX=clip.Right;if(minY<clip.Top)minY=clip.Top;if(maxY>clip.Bottom)maxY=clip.Bottom;
        // The shader may round bounds outward (fast-path floor(+0.5), AA sampling, clip edges); dispatch a
        // strict 1px superset on every side — the local-size guard in main() discards the excess pixels.
        originX=(int)Math.Clamp(MathF.Floor(minX)-1,0,target.Width);originY=(int)Math.Clamp(MathF.Floor(minY)-1,0,target.Height);
        int endX=(int)Math.Clamp(MathF.Ceiling(maxX)+1,0,target.Width),endY=(int)Math.Clamp(MathF.Ceiling(maxY)+1,0,target.Height);
        extentX=Math.Max(0,endX-originX);extentY=Math.Max(0,endY-originY);
    }
    private static uint Pack(Rgba32 c)=>(uint)(c.R|(c.G<<8)|(c.B<<16)|(c.A<<24));
    private void TrimTextures()
    {
        long bytes=imageCache.Values.Sum(e=>(long)e.Texture.Size);
        foreach(var entry in imageCache.OrderBy(e=>e.Value.Used).ToArray())
        {if(imageCache.Count<=32&&bytes<=64*1024*1024)break;bytes-=entry.Value.Texture.Size;entry.Value.Texture.Dispose();imageCache.Remove(entry.Key);}
    }
    private Texture AllocateTexture(int width,int height,ReadOnlySpan<byte> data)
    {
        var result=new Texture(device,width,height);
        try
        {
            result.Staging=Allocate(data.Length,1);data.CopyTo(result.Staging.Bytes);
            var info=new Vk.ImageInfo {Type=14,ImageType=1,Format=37,Width=(uint)width,Height=(uint)height,Depth=1,Mips=1,Layers=1,Samples=1,Usage=6};
            ulong handle=0;Check(Vk.vkCreateImage(device,&info,null,&handle));result.Image=handle;Vk.MemoryRequirements requirements=default;Vk.vkGetImageMemoryRequirements(device,handle,&requirements);
            uint type=uint.MaxValue;var properties=memory;
            for(uint i=0;i<properties.TypeCount;i++)if((requirements.TypeBits&(1u<<(int)i))!=0){if(type==uint.MaxValue)type=i;if((properties.Types[i*2]&1)!=0){type=i;break;}}
            if(type==uint.MaxValue)throw new NotSupportedException("No compatible Vulkan image memory.");
            var allocate=new Vk.AllocateInfo {Type=5,Size=requirements.Size,MemoryType=type};Check(Vk.vkAllocateMemory(device,&allocate,null,&handle));result.Memory=handle;Check(Vk.vkBindImageMemory(device,result.Image,handle,0));
            var view=new Vk.ImageViewInfo {Type=15,Image=result.Image,ViewType=1,Format=37,Aspect=1,Mips=1,Layers=1};Check(Vk.vkCreateImageView(device,&view,null,&handle));result.View=handle;return result;
        }
        catch{result.Dispose();throw;}
    }
    private void RecordTextureUpload(Texture texture)
    {
        var barrier=new Vk.ImageBarrier {Type=45,DestinationAccess=4096,OldLayout=0,NewLayout=7,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Image=texture.Image,Aspect=1,Mips=1,Layers=1};
        Vk.vkCmdPipelineBarrier(command,1,4096,0,0,null,0,null,1,&barrier);
        var region=new Vk.BufferImageCopy {Aspect=1,Layers=1,Width=(uint)texture.Width,Height=(uint)texture.Height,Depth=1};Vk.vkCmdCopyBufferToImage(command,texture.Staging!.Handle,texture.Image,7,1,&region);
        barrier.SourceAccess=4096;barrier.DestinationAccess=32;barrier.OldLayout=7;barrier.NewLayout=5;Vk.vkCmdPipelineBarrier(command,4096,2048,0,0,null,0,null,1,&barrier);
    }
    private GpuBuffer Allocate(int size,uint usage=32,bool preferCached=false,bool deviceLocal=false)
    {
        if(size<=0)throw new ArgumentOutOfRangeException(nameof(size));var result=new GpuBuffer(device,size);
        try
        {
            var info=new Vk.BufferInfo {Type=12,Size=(ulong)size,Usage=usage};ulong handle=0;Check(Vk.vkCreateBuffer(device,&info,null,&handle));result.Handle=handle;
            Vk.MemoryRequirements requirements=default;Vk.vkGetBufferMemoryRequirements(device,handle,&requirements);
            uint memoryType=uint.MaxValue;var properties=memory;
            for(uint i=0;i<properties.TypeCount;i++)if((requirements.TypeBits&(1u<<(int)i))!=0&&(properties.Types[i*2]&6)==6){memoryType=i;break;}
            if(preferCached)for(uint i=0;i<properties.TypeCount;i++)if((requirements.TypeBits&(1u<<(int)i))!=0&&(properties.Types[i*2]&14)==14){memoryType=i;break;}
            if(deviceLocal)for(uint i=0;i<properties.TypeCount;i++)if((requirements.TypeBits&(1u<<(int)i))!=0&&(properties.Types[i*2]&1)!=0){memoryType=i;break;}
            if(memoryType==uint.MaxValue)throw new NotSupportedException("No compatible Vulkan buffer memory type is available.");
            var allocation=new Vk.AllocateInfo {Type=5,Size=requirements.Size,MemoryType=memoryType};Check(Vk.vkAllocateMemory(device,&allocation,null,&handle));result.Memory=handle;
            Check(Vk.vkBindBufferMemory(device,result.Handle,handle,0));
            if(!deviceLocal){void* mapped=null;Check(Vk.vkMapMemory(device,handle,0,(ulong)size,0,&mapped));result.Pointer=mapped;}
            return result;
        }
        catch{result.Dispose();throw;}
    }
    private void Check(int result)
    {
        if(result==0)return;if(result==-4){faulted=true;throw new VulkanDeviceLostException("VK_ERROR_DEVICE_LOST: recreate Vulkan device resources.");}throw new InvalidOperationException($"Vulkan operation failed with VkResult {result}.");
    }
    private sealed class GpuBuffer(IntPtr device,int size) : IDisposable
    {
        public ulong Handle,Memory;
        public void* Pointer;
        public int Size=>size;
        public Span<byte> Bytes=>Pointer is null?throw new ObjectDisposedException(nameof(GpuBuffer)):new(Pointer,size);
        public Vk.DescriptorBuffer Descriptor=>new(){Buffer=Handle,Range=(ulong)size};
        public void Dispose(){if(Pointer is not null){Vk.vkUnmapMemory(device,Memory);Pointer=null;}if(Handle!=0){Vk.vkDestroyBuffer(device,Handle,null);Handle=0;}if(Memory!=0){Vk.vkFreeMemory(device,Memory,null);Memory=0;}}
    }
    public void Dispose()
    {
        if(disposed)return;if(Environment.CurrentManagedThreadId!=ownerThread)throw new InvalidOperationException("Dispose the Vulkan surface on its creating thread.");DisposeCore();GC.SuppressFinalize(this);
    }
    private void DisposeCore()
    {
        if(disposed)return;disposed=true;
        if(device!=IntPtr.Zero)
        {
            Vk.vkDeviceWaitIdle(device);pending.Clear();foreach(var entry in imageCache.Values)entry.Texture.Dispose();imageCache.Clear();foreach(var retired in retiredTextures)retired.Dispose();retiredTextures.Clear();edgeCache.Clear();edgeBytes=0;emptyTexture?.Dispose();output?.Dispose();readback?.Dispose();empty?.Dispose();edgesArena?.Dispose();paramsArena?.Dispose();
            if(fence!=0)Vk.vkDestroyFence(device,fence,null);if(commandPool!=0)Vk.vkDestroyCommandPool(device,commandPool,null);if(descriptorPool!=0)Vk.vkDestroyDescriptorPool(device,descriptorPool,null);
            if(sampler!=0)Vk.vkDestroySampler(device,sampler,null);
            if(pipeline!=0)Vk.vkDestroyPipeline(device,pipeline,null);if(pipelineLayout!=0)Vk.vkDestroyPipelineLayout(device,pipelineLayout,null);if(descriptorLayout!=0)Vk.vkDestroyDescriptorSetLayout(device,descriptorLayout,null);Vk.vkDestroyDevice(device,null);device=IntPtr.Zero;
        }
        if(instance!=IntPtr.Zero){Vk.vkDestroyInstance(instance,null);instance=IntPtr.Zero;}target.Dispose();
    }
    ~VulkanCanvas(){try{DisposeCore();}catch{ /* Finalizers cannot report a completed frame or throw. */ }}
}
