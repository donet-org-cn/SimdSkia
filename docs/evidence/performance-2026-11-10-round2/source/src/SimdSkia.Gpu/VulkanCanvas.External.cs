namespace SimdSkia.Gpu;
public sealed unsafe partial class VulkanCanvas
{
    private VulkanImage? externalImage;
    private Vk.BufferImageCopy[]? externalRows;
    internal void AttachExternalImage(VulkanImage image)
    {
        Validate();var description=image.Description;
        if(image.Device.Handles.Device!=device||image.Width!=target.Width||image.Height!=target.Height||(description.Usage&3)!=3||(description.Format==44)!=(target.Format==PixelFormat.Bgra8888))throw new ArgumentException("The external VkImage does not match this canvas.");
        externalImage=image;outputInitialized=false;
    }
    private void RecordExternalCopy(bool write)
    {
        var image=externalImage!;image.Device.Check();var description=image.Description;
        uint layout=write?7u:6u,access=write?4096u:2048u;
        var barrier=new Vk.ImageBarrier{Type=45,Image=description.Image,OldLayout=description.Layout,NewLayout=layout,SourceAccess=0x18000,DestinationAccess=access,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Aspect=1,Mips=1,Layers=1};Vk.vkCmdPipelineBarrier(command,0x10000,4096,0,0,null,0,null,1,&barrier);
        void Copy(uint count,Vk.BufferImageCopy* regions){if(write)Vk.vkCmdCopyBufferToImage(command,output!.Handle,description.Image,layout,count,regions);else Vk.vkCmdCopyImageToBuffer(command,description.Image,layout,output!.Handle,count,regions);}
        if(!image.Flip){var region=new Vk.BufferImageCopy{Aspect=1,Layers=1,Width=(uint)target.Width,Height=(uint)target.Height,Depth=1};Copy(1,&region);}
        else
        {
            if(externalRows is null){externalRows=new Vk.BufferImageCopy[target.Height];for(int y=0;y<externalRows.Length;y++)externalRows[y]=new(){Offset=(ulong)y*(uint)target.Width*4,Aspect=1,Layers=1,Y=target.Height-1-y,Width=(uint)target.Width,Height=1,Depth=1};}fixed(Vk.BufferImageCopy* regions=externalRows)Copy((uint)externalRows.Length,regions);
        }
        barrier.SourceAccess=access;barrier.DestinationAccess=0x18000;barrier.OldLayout=layout;barrier.NewLayout=description.Layout;Vk.vkCmdPipelineBarrier(command,4096,0x10000,0,0,null,0,null,1,&barrier);
    }
    private void ReadInitialExternal(bool updateTarget)
    {
        ulong waitFence=fence;Check(Vk.vkResetFences(device,1,&waitFence));var begin=new Vk.CommandBeginInfo{Type=42,Flags=1};Check(Vk.vkBeginCommandBuffer(command,&begin));RecordExternalCopy(false);
        var visible=new Vk.BufferBarrier{Type=44,SourceAccess=4096,DestinationAccess=2048|32|64,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Buffer=output!.Handle,Size=(ulong)output.Size};Vk.vkCmdPipelineBarrier(command,4096,4096|2048,0,0,null,1,&visible,0,null);
        var copy=new Vk.BufferCopy{Size=(ulong)output.Size};Vk.vkCmdCopyBuffer(command,output.Handle,readback!.Handle,1,&copy);var hostBarrier=new Vk.BufferBarrier{Type=44,SourceAccess=4096,DestinationAccess=8192,SourceFamily=uint.MaxValue,DestinationFamily=uint.MaxValue,Buffer=readback.Handle,Size=(ulong)readback.Size};Vk.vkCmdPipelineBarrier(command,4096,16384,0,0,null,1,&hostBarrier,0,null);Check(Vk.vkEndCommandBuffer(command));
        nint recorded=command;var submit=new Vk.SubmitInfo{Type=4,CommandCount=1,Commands=&recorded};bool submitted=false,completed=false;var watch=System.Diagnostics.Stopwatch.StartNew();
        try{Check(Vk.vkQueueSubmit(queue,1,&submit,fence));submitted=true;Check(Vk.vkWaitForFences(device,1,&waitFence,1,10_000_000_000));completed=true;shadowStale=true;if(updateTarget)CopyReadbackToTarget();outputInitialized=true;LastCompletionMilliseconds=watch.Elapsed.TotalMilliseconds;}
        catch{faulted=true;throw;}
        finally{if(submitted&&!completed)Vk.vkDeviceWaitIdle(device);}
    }
    private void CopyReadbackToTarget()
    {if(target.RowBytes==target.Width*4)readback!.Bytes.CopyTo(target.GetPixelSpan());else for(int y=0;y<target.Height;y++)readback!.Bytes.Slice(y*target.Width*4,target.Width*4).CopyTo(target.GetRowSpan(y));shadowStale=false;}
    internal void ReadTo(Span<byte> destination,int rowBytes)
    {
        Validate();if(rowBytes<target.Width*4||destination.Length<(long)rowBytes*(target.Height-1)+target.Width*4)throw new ArgumentException("The GPU readback destination is too small.");
        if(!outputInitialized&&pending.Count==0&&externalImage is null){for(int y=0;y<target.Height;y++)target.GetRowSpan(y)[..(target.Width*4)].CopyTo(destination.Slice(y*rowBytes,target.Width*4));return;}
        Flush(false);if(rowBytes==target.Width*4)readback!.Bytes.CopyTo(destination);else for(int y=0;y<target.Height;y++)readback!.Bytes.Slice(y*target.Width*4,target.Width*4).CopyTo(destination.Slice(y*rowBytes,target.Width*4));
    }
}
