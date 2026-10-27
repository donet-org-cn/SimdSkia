using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

const int width=1920,height=1080,sourceStride=width*4+64;
byte[] source=new byte[sourceStride*height];for(int i=0;i<source.Length;i++)source[i]=(byte)(i*17);
for(int y=0;y<height;y++)for(int x=0;x<width;x++)source[y*sourceStride+x*4+3]=255;
using var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Premul));using var process=Process.GetCurrentProcess();
unsafe
{
    fixed(byte* input=source)
    {
        void Frame(byte* data)
        {
            byte* target=(byte*)bitmap.GetPixels();for(int y=0;y<height;y++)Buffer.MemoryCopy(data+y*sourceStride,target+y*bitmap.RowBytes,width*4,width*4);
        }
        for(int i=0;i<200;i++)Frame(input);GC.Collect();GC.WaitForPendingFinalizers();
        long allocated=GC.GetAllocatedBytesForCurrentThread();var cpu=process.TotalProcessorTime;var clock=Stopwatch.StartNew();int frames=0;
        while(clock.Elapsed.TotalSeconds<3){Frame(input);frames++;}
        clock.Stop();cpu=process.TotalProcessorTime-cpu;long allocation=GC.GetAllocatedBytesForCurrentThread()-allocated;
        for(int y=0;y<height;y++)if(!bitmap.GetPixelSpan().Slice(y*bitmap.RowBytes,width*4).SequenceEqual(source.AsSpan(y*sourceStride,width*4)))throw new Exception("Copied pixels differ.");
        process.Refresh();var result=new{implementation=typeof(SKBitmap).Assembly.GetName().Name,width,height,frames,msPerFrame=clock.Elapsed.TotalMilliseconds/frames,cpuMsPerFrame=cpu.TotalMilliseconds/frames,cpuPercent=cpu.TotalSeconds/clock.Elapsed.TotalSeconds/Environment.ProcessorCount*100,allocatedBytesPerFrame=(double)allocation/frames,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,bytesCopiedPerFrame=width*height*4,pixelsEqual=true};
        string json=JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true});Console.WriteLine(json);if(args.Length>0)File.WriteAllText(args[0],json);
    }
}
