using System.Runtime.InteropServices;
namespace SimdSkia.Gpu;
public sealed unsafe partial class VulkanCanvas
{
    private const int RectangleBatchSize=16,RectangleBatchUniformBytes=RectangleBatchSize*768;
    private const int OrderedRectangleUniformBytes=RectangleBatchUniformBytes+16;
    private ulong rectangleBatchPipeline;
    private ulong orderedRectanglePipeline;
    private readonly bool orderedRectanglesEnabled=!(AppContext.TryGetSwitch("SimdSkia.Vulkan.DisableOrderedRectangleBatches",out bool disabled)&&disabled);
    public long OrderedRectangleDispatches {get;private set;}
    private int lastRectangleBatchWidth,lastRectangleBatchHeight;
    public long RectangleBatchDispatches {get;private set;}
    public long TransferClears {get;private set;}
    private bool TransferClear(in DrawingCommand draw,ref int unbarrieredCount)
    {
        if(!optimizedCoverage||draw.Kind!=CommandKind.Clear||draw.Clip is not null||draw.CoverageClip is not null)return false;
        var color=draw.Paint.Color;uint red=PixelBuffer.Multiply(color.R,color.A),green=PixelBuffer.Multiply(color.G,color.A),blue=PixelBuffer.Multiply(color.B,color.A);
        uint packed=target.Format==PixelFormat.Bgra8888?blue|(green<<8)|(red<<16)|((uint)color.A<<24):red|(green<<8)|(blue<<16)|((uint)color.A<<24);
        var barrier=new Vk.BufferBarrier{Type=44,SourceAccess=32|64|2048,DestinationAccess=4096,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Buffer=output!.Handle,Size=(ulong)output.Size};Vk.vkCmdPipelineBarrier(command,2048|4096,4096,0,0,null,1,&barrier,0,null);Vk.vkCmdFillBuffer(command,output.Handle,0,(ulong)output.Size,packed);
        barrier.SourceAccess=4096;barrier.DestinationAccess=32|64;Vk.vkCmdPipelineBarrier(command,4096,2048,0,0,null,1,&barrier,0,null);unbarrieredCount=0;TransferClears++;return true;
    }
    private void EnsureRectangleBatchPipeline()
    {
        if(rectangleBatchPipeline!=0)return;
        rectangleBatchPipeline=CreateRectanglePipeline(VulkanShaderCode.RectangleBatch);
        if(orderedRectanglesEnabled)orderedRectanglePipeline=CreateRectanglePipeline(VulkanShaderCode.OrderedRectangles);
    }
    private ulong CreateRectanglePipeline(byte[] bytes)
    {
        ulong module=0;
        fixed(byte* code=bytes){var shader=new Vk.ShaderInfo{Type=16,CodeSize=(nuint)bytes.Length,Code=(uint*)code};Check(Vk.vkCreateShaderModule(device,&shader,null,&module));}
        try{byte* name=stackalloc byte[5]{(byte)'m',(byte)'a',(byte)'i',(byte)'n',0};uint kind=1;var entry=new Vk.SpecializationEntry{Size=4};var specialization=new Vk.SpecializationInfo{Count=1,Entries=&entry,Size=4,Data=&kind};var info=new Vk.ComputePipelineInfo{Type=29,Stage=new(){Type=18,Stage=32,Module=module,Name=name,Specialization=&specialization},Layout=pipelineLayout,BaseIndex=-1};ulong result=0;Check(Vk.vkCreateComputePipelines(device,0,1,&info,null,&result));PreparedPipelineCount++;return result;}
        finally{Vk.vkDestroyShaderModule(device,module,null);}
    }
    private int RectangleBatch(int start,ref int paramOffset,ref int setIndex,Span<RectF> unbarriered,ref int unbarrieredCount,ref ulong boundPipeline)
    {
        if(!rectangleBatchesEnabled)return 0;
        Span<uint> words=stackalloc uint[160];Span<RectF> bounds=stackalloc RectF[RectangleBatchSize];int count=0;uint maximumWidth=0,maximumHeight=0;bool ordered=false;RectF union=default;double area=0;
        for(int i=start;i<pending.Count&&count<RectangleBatchSize;i++)
        {
            var item=pending[i];var command=item.Command;
            if(command.Kind!=CommandKind.FillRectangle||command.CoverageClip is not null||!command.Transform.TryInvert(out _))break;
            Prepare(item,words);uint width=words[39],height=words[40];if(width==0||height==0)break;var candidate=new RectF(words[37],words[38],words[37]+width,words[38]+height);bool overlap=false;
            foreach(var previous in bounds[..count])if(Intersects(previous,candidate)){overlap=true;break;}
            if(overlap&&!orderedRectanglesEnabled)break;
            ordered|=overlap;area+=(double)width*height;
            union=count==0?candidate:new(Math.Min(union.Left,candidate.Left),Math.Min(union.Top,candidate.Top),Math.Max(union.Right,candidate.Right),Math.Max(union.Bottom,candidate.Bottom));
            bounds[count++]=candidate;maximumWidth=Math.Max(maximumWidth,width);maximumHeight=Math.Max(maximumHeight,height);
        }
        if(count<2)return 0;
        // Sparse unions would dispatch mostly empty pixels. Keep the ordinary
        // path when sequential work per pixel cannot amortize that area.
        if(ordered&&(double)(union.Right-union.Left)*(union.Bottom-union.Top)>area*2)return 0;
        EnsureRectangleBatchPipeline();int paramBase=(paramOffset+255)&~255,header=ordered?16:0;
        if(ordered){var info=MemoryMarshal.Cast<byte,uint>(paramsArena!.Bytes.Slice(paramBase,16));info[0]=(uint)union.Left;info[1]=(uint)union.Top;info[2]=(uint)count;info[3]=0;}
        for(int i=0;i<count;i++){Prepare(pending[start+i],words);words.CopyTo(MemoryMarshal.Cast<byte,uint>(paramsArena!.Bytes.Slice(paramBase+header+i*768,640)));}paramOffset=paramBase+header+count*768;
        bool dependent=false;foreach(var current in bounds[..count])foreach(var previous in unbarriered[..unbarrieredCount])if(Intersects(current,previous)){dependent=true;break;}
        if(dependent){var barrier=new Vk.BufferBarrier{Type=44,SourceAccess=64,DestinationAccess=32|64,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Buffer=output!.Handle,Size=(ulong)output.Size};Vk.vkCmdPipelineBarrier(command,2048,2048,0,0,null,1,&barrier,0,null);unbarrieredCount=0;DrawDependencyBarriers++;}
        foreach(var current in bounds[..count])unbarriered[unbarrieredCount++]=current;
        ulong set=descriptorSets[setIndex++];var buffers=stackalloc Vk.DescriptorBuffer[5];buffers[0]=output!.Descriptor;buffers[1]=empty!.Descriptor;buffers[2]=new(){Buffer=deviceParamsArena!.Handle,Offset=(ulong)paramBase,Range=(ulong)(ordered?OrderedRectangleUniformBytes:RectangleBatchUniformBytes)};buffers[4]=empty.Descriptor;
        var writes=stackalloc Vk.WriteDescriptor[5];for(uint i=0;i<3;i++)writes[i]=new(){Type=35,Set=set,Binding=i,Count=1,DescriptorType=i==2?6u:7u,Buffer=buffers+i};var image=emptyTexture!.Descriptor(sampler);writes[3]=new(){Type=35,Set=set,Binding=3,Count=1,DescriptorType=1,Image=&image};writes[4]=new(){Type=35,Set=set,Binding=4,Count=1,DescriptorType=7,Buffer=buffers+4};Vk.vkUpdateDescriptorSets(device,5,writes,0,null);Vk.vkCmdBindDescriptorSets(command,1,pipelineLayout,0,1,&set,0,null);
        ulong selected=ordered?orderedRectanglePipeline:rectangleBatchPipeline;
        if(boundPipeline!=selected){Vk.vkCmdBindPipeline(command,1,selected);boundPipeline=selected;}
        uint dispatchWidth=ordered?(uint)(union.Right-union.Left):maximumWidth,dispatchHeight=ordered?(uint)(union.Bottom-union.Top):maximumHeight;
        Vk.vkCmdDispatch(command,(dispatchWidth+7)/8,(dispatchHeight+7)/8,ordered?1u:(uint)count);lastRectangleBatchWidth=(int)dispatchWidth;lastRectangleBatchHeight=(int)dispatchHeight;DrawDispatches++;RectangleBatchDispatches++;if(ordered)OrderedRectangleDispatches++;return count;
    }
    private static bool Intersects(RectF a,RectF b)=>a.Left<b.Right&&b.Left<a.Right&&a.Top<b.Bottom&&b.Top<a.Bottom;
}
