using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using SimdSkia;
using SimdSkia.Cpu;
using SimdSkia.Gpu.Direct2D;

// Unpaced, fully drained render throughput complements the timer-limited
// Avalonia window. CPU raster has no OS blit; GPU includes native presentation.
[SupportedOSPlatform("windows")]
internal static class NativeThroughput
{
    internal static void Run(string mode,int rectangleCount,string output)
    {
        if(mode is not ("cpu" or "queued" or "synchronous")||rectangleCount is not (1 or 64 or 512))throw new ArgumentException("mode: cpu/queued/synchronous; rectangles: 1/64/512.");
        using var pixels=new PixelBuffer(1000,720,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
        var commands=new DrawingCommand[rectangleCount+1];commands[0]=DrawingCommand.Clear(new(20,30,40,255));
        for(int i=0;i<rectangleCount;i++)
        {
            int x=(i*73)%900,y=(i*53)%640;
            commands[i+1]=DrawingCommand.FillRectangle(new(x,y,x+100,y+80),Matrix3.Identity,new(new((byte)(i*17),(byte)(i*31),(byte)(i*47),180),CompositeMode.SourceOver,true));
        }
        nint hwnd=0;
        try
        {
            if(mode!="cpu"){hwnd=NativeWindow.CreateWindowEx(0,"STATIC","SimdSkia unpaced throughput",0,0,0,1000,720,0,0,0,0);if(hwnd==0)throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());}
            using ICanvasBackend canvas=mode=="cpu"?new RasterCanvas(pixels):new Direct2DCanvas(pixels,hwnd);
            void Frame(){foreach(var command in commands)canvas.Execute(command);if(canvas is Direct2DCanvas gpu){if(mode=="queued")gpu.QueuePresent();else gpu.Present();}else canvas.Flush();}
            // Fixed duration warmup avoids a different JIT/driver warmup budget
            // for the faster backend. Every measured batch includes its drain.
            var warmup=Stopwatch.StartNew();do{Frame();}while(warmup.Elapsed.TotalSeconds<3);
            if(canvas is Direct2DCanvas ready)ready.WaitForIdle();
            int frames=0;using var process=Process.GetCurrentProcess();
            long allocated=GC.GetTotalAllocatedBytes(true),cpu=process.TotalProcessorTime.Ticks;
            long start=Stopwatch.GetTimestamp();
            do{for(int frame=0;frame<32;frame++)Frame();frames+=32;}while(Stopwatch.GetElapsedTime(start).TotalSeconds<2);
            if(canvas is Direct2DCanvas drain)drain.WaitForIdle();
            double elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            long processor=process.TotalProcessorTime.Ticks-cpu,bytes=GC.GetTotalAllocatedBytes(true)-allocated;
            var direct=canvas as Direct2DCanvas;
            if(direct is not null&&(direct.PendingFrames!=0||direct.CompletedFrames!=direct.SubmittedFrames))throw new InvalidOperationException("GPU batch did not finish.");
            canvas.Flush();
            using var reference=new PixelBuffer(1000,720,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);using var scalar=new RasterCanvas(reference);
            foreach(var command in commands)scalar.Execute(command);scalar.Flush();
            int maxError=0;long totalError=0;
            for(int y=0;y<720;y++)for(int x=0;x<4000;x++){int error=Math.Abs(reference.GetRowSpan(y)[x]-pixels.GetRowSpan(y)[x]);maxError=Math.Max(maxError,error);totalError+=error;}
            // Native and managed SourceOver can round by a byte. This small
            // scene gate does not replace the existing full-page quality gate.
            if(maxError>2)throw new InvalidOperationException($"Rectangle throughput pixel gate failed: max={maxError}.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            using var stream=File.Create(output);using var json=new Utf8JsonWriter(stream,new(){Indented=true});
            json.WriteStartObject();json.WriteString("mode",mode);json.WriteString("scope","CPU raster only versus GPU render + native present; hidden HWND; excludes readback from timing; complete GPU drain included");
            json.WriteNumber("rectangles",rectangleCount);json.WriteNumber("frames",frames);json.WriteNumber("completedBatchMilliseconds",elapsed);json.WriteNumber("millisecondsPerCompletedFrame",elapsed/frames);
            json.WriteNumber("cpuMilliseconds",processor/(double)TimeSpan.TicksPerMillisecond);json.WriteNumber("cpuMillisecondsPerFrame",processor/(double)TimeSpan.TicksPerMillisecond/frames);json.WriteNumber("allocatedBytesPerFrame",bytes/(double)frames);
            json.WriteString("device",direct?.DeviceName??"managed CPU");json.WriteNumber("pendingFrames",direct?.PendingFrames??0);json.WriteNumber("completionPolls",direct?.CompletionPolls??0);json.WriteNumber("maxPixelError",maxError);json.WriteNumber("meanPixelError",totalError/(1000d*720*4));json.WriteEndObject();
            Console.WriteLine($"PASS {mode}, {rectangleCount} rectangles: {elapsed/frames:F4} ms/completed frame, CPU={processor/(double)TimeSpan.TicksPerMillisecond/frames:F4} ms/frame, max pixel error={maxError}");
        }
        finally{if(hwnd!=0)NativeWindow.DestroyWindow(hwnd);}
    }
}
