using SkiaSharp;
using System.Text.Json;
using var host=new D3DHost();
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool value){checks++;if(!value)throw new Exception("D3D12 context assertion "+checks);}
using var descriptor=new GRD3DBackendContext{Adapter=host.Adapter,Device=host.Device,Queue=host.Queue};using var context=GRContext.CreateDirect3D(descriptor);using var optionsContext=GRContext.CreateDirect3D(descriptor,new GRContextOptions());
if(context is null||optionsContext is null){File.WriteAllText(Path.Combine(output,"capability.json"),JsonSerializer.Serialize(new{device=host.Name,defaultFactory=context is not null,optionsFactory=optionsContext is not null,status="unavailable",note="Creation returned null on this package/device; no same-backend timing or passing rendering result is available."}));Console.WriteLine("D3D12 context unavailable; see capability.json.");return 3;}
Check(optionsContext.Backend==GRBackend.Direct3D);values["context-backend"]=context.Backend.ToString();values["initial-abandoned"]=context.IsAbandoned;
context.SetResourceCacheLimit(2*1024*1024);Check(context.GetResourceCacheLimit()==2*1024*1024);GRRecordingContext recording=context;Check(recording.GetMaxSurfaceSampleCount(SKColorType.Rgba8888)>=1);Check(context.MaxTextureSize>=1024);Check(context.MaxRenderTargetSize>=1024);

for(int scene=0;scene<5;scene++)for(int transform=0;transform<2;transform++)
{
 using var surface=SKSurface.Create(context,true,new SKImageInfo(96,72,SKColorType.Rgba8888,SKAlphaType.Premul),0,(GRSurfaceOrigin)transform)!;Check(surface.Context is not null);Check(surface.Canvas.Context is not null);using(var peek=surface.PeekPixels())Check(peek is null);
 var canvas=surface.Canvas;canvas.Clear(new SKColor(20,30,50));using var paint=new SKPaint{Color=new SKColor(200,70,90,180)};
 if(scene==0)canvas.DrawRect(new SKRect(8,7,64,49),paint);
 if(scene==1){canvas.ClipRect(new SKRect(12,13,70,54));canvas.DrawRect(new SKRect(4,5,80,66),paint);}
 if(scene==2){using var shader=SKShader.CreateLinearGradient(new(0,0),new(96,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);paint.Shader=shader;paint.Color=SKColors.White;canvas.DrawRect(new SKRect(6,7,84,60),paint);}
 if(scene==3){using var path=new SKPath();path.MoveTo(5,9);path.LineTo(64,9);path.LineTo(29,59);path.Close();canvas.DrawPath(path,paint);}
 if(scene==4){using var bitmap=new SKBitmap(16,12);bitmap.Erase(new SKColor(70,150,220));using var image=SKImage.FromBitmap(bitmap);canvas.DrawImage(image,new SKRect(8,9,72,57));}
 surface.Flush(false);context.Submit(true);using var read=new SKBitmap(new SKImageInfo(96,72,SKColorType.Rgba8888,SKAlphaType.Premul));Check(surface.ReadPixels(read.Info,read.GetPixels(),read.RowBytes,0,0));var bytes=read.GetPixelSpan().ToArray();cases.Add(new{kind="gpu-context",scene,transform,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);
 using var snapshot=surface.Snapshot();Check(snapshot.Width==96);using var props=surface.SurfaceProperties;Check(props is not null);
}
context.Flush(true,true);context.GetResourceCacheUsage(out var resources,out var before);Check(resources>=0&&before>=0);context.PurgeUnlockedResources(1,true);context.PurgeUnusedResources(100000);context.PurgeResources();context.GetResourceCacheUsage(out _,out var after);Check(after<=before);context.ResetContext();
using(var dump=new Dump()){context.DumpMemoryStatistics(dump);Check(dump.Values>0);}
context.AbandonContext(true);values["final-abandoned"]=context.IsAbandoned;
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks,deviceName=host.Name,resources,before,after}));Console.WriteLine($"Captured {cases.Count} D3D12 context images, {values.Count} metadata entries and {checks} real-device checks");
return 0;
sealed class Dump : SKTraceMemoryDump
{
 public int Values;
 public Dump():base(true,true) { }
 protected override void OnDumpNumericValue(string dumpName,string valueName,string units,ulong value)=>Values++;
}
