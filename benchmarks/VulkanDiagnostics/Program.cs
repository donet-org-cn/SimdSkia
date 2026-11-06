using SimdSkia;
using SimdSkia.Gpu;
using System.Text.Json;
using var hostPixels=new PixelBuffer(1,1,PixelFormat.Rgba8888,AlphaFormat.Premultiplied);using var host=new VulkanCanvas(hostPixels);var handles=host.ContextHandles;using var transfer=new VulkanTransfer(handles);using var initial=PixelBuffer.Rent(640,480,PixelFormat.Rgba8888);using var image=transfer.Create(initial);using var pixels=PixelBuffer.Rent(640,480,PixelFormat.Rgba8888);using var canvas=new VulkanCanvas(pixels,true,handles){EnableProfiling=true};canvas.AttachExternalImage(image);var profiles=new List<VulkanProfile>();
for(int frame=0;frame<12;frame++)
{
 canvas.Execute(DrawingCommand.Clear(new(20,30,50,255)));for(int n=0;n<24;n++){float x=n%6*100+4,y=n/6*110+4;canvas.Execute(DrawingCommand.FillRectangle(new(x,y,x+88,y+92),Matrix3.Identity,new(new(180,80,40,190),CompositeMode.SourceOver,true)));}canvas.Flush();if(frame>=6)profiles.Add(canvas.LastProfile!);
}
Directory.CreateDirectory(args[0]);File.WriteAllText(Path.Combine(args[0],"profiles.json"),JsonSerializer.Serialize(profiles,new JsonSerializerOptions{WriteIndented=true}));foreach(var p in profiles)Console.WriteLine($"init={p.InitializationTicks} draw={p.DrawingTicks} readback={p.ReadbackTicks} fence={p.FenceMilliseconds:F4}ms host={p.HostCopyMilliseconds:F4}ms; "+string.Join(", ",p.Dispatches.Select(d=>$"{d.Kind}x{d.CommandCount}={d.DeviceTicks}")));
