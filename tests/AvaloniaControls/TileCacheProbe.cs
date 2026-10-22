using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SimdSkia.ControlValidation;

// Identical public Avalonia calls in managed/native workers. Deliberately update
// one source image: an old version cannot be used by a later brush operation.
internal static class TileCacheProbe
{
    internal static void Run(string output)
    {
        var factory=AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>();
        using var source=new WriteableBitmap(new(512,512),new(96,96),PixelFormats.Bgra8888,global::Avalonia.Platform.AlphaFormat.Premul);
        using var target=factory.CreateRenderTargetBitmap(new(320,240),new(96,96));
        var brush=new ImageBrush(source){TileMode=TileMode.Tile,Stretch=Stretch.Fill};
        var pixels=new byte[512*512*4];
        void Draw(int version)
        {
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)
            {int i=(y*512+x)*4;pixels[i]=(byte)(x+version);pixels[i+1]=(byte)y;pixels[i+2]=(byte)(255-version);pixels[i+3]=255;}
            using(var locked=source.Lock())for(int y=0;y<512;y++)Marshal.Copy(pixels,y*512*4,locked.Address+y*locked.RowBytes,512*4);
            using var context=target.CreateDrawingContext();context.Clear(Colors.Transparent);
            context.PushRenderOptions(new(){BitmapInterpolationMode=BitmapInterpolationMode.None});
            context.DrawRectangle(brush,null,new Rect(0,0,320,240));context.PopRenderOptions();
        }
        for(int i=0;i<8;i++)Draw(i);
        using var process=Process.GetCurrentProcess();
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        long allocated=GC.GetTotalAllocatedBytes(true);double cpu=process.TotalProcessorTime.TotalMilliseconds;
        var clock=Stopwatch.StartNew();const int frames=160;
        for(int i=0;i<frames;i++)Draw(i);
        clock.Stop();cpu=process.TotalProcessorTime.TotalMilliseconds-cpu;
        long allocation=GC.GetTotalAllocatedBytes(true)-allocated;
        long retained=GC.GetTotalMemory(true);process.Refresh();
        var row=new{scenario="mutable-image-brush",frames,width=320,height=240,sourceWidth=512,sourceHeight=512,
            millisecondsPerFrame=clock.Elapsed.TotalMilliseconds/frames,cpuMillisecondsPerFrame=cpu/frames,
            allocatedBytesPerFrame=allocation/(double)frames,workingSetMiB=process.WorkingSet64/1048576d,
            privateMiB=process.PrivateMemorySize64/1048576d,managedRetainedAfterGcMiB=retained/1048576d,
            nativeAot=!RuntimeFeature.IsDynamicCodeSupported,runtime=Environment.Version.ToString(),processors=Environment.ProcessorCount};
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"metrics.json"),JsonSerializer.Serialize(row,new JsonSerializerOptions{WriteIndented=true}));
        using(var stream=File.Create(Path.Combine(output,"scene.png")))target.Save(stream,PngBitmapEncoderOptions.Default);
        using var frame=((IReadableBitmapImpl)target).Lock();var rgba=new byte[320*240*4];
        for(int y=0;y<240;y++)Marshal.Copy(frame.Address+y*frame.RowBytes,rgba,y*320*4,320*4);
        if(frame.Format==PixelFormats.Bgra8888)for(int i=0;i<rgba.Length;i+=4)(rgba[i],rgba[i+2])=(rgba[i+2],rgba[i]);
        File.WriteAllBytes(Path.Combine(output,"scene.rgba"),rgba);
        Console.WriteLine($"PASS {frames} mutable image frames; {row.millisecondsPerFrame:F3}ms; retained {row.managedRetainedAfterGcMiB:F2}MiB");
    }
}
