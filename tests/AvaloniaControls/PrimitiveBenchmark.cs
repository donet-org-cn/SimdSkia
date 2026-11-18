using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;

namespace SimdSkia.ControlValidation;

internal static class PrimitiveBenchmark
{
    internal static void Run(string output,int frames)
    {
        var factory=AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>();
        using var bitmap=factory.CreateRenderTargetBitmap(new(512,320),new(96,96));
        var pen=new Pen(Brushes.SteelBlue,1.75){LineCap=PenLineCap.Round};
        var shadows=new BoxShadows(new BoxShadow{OffsetX=3.5,OffsetY=2.25,Blur=4,Spread=1,Color=Color.FromArgb(151,40,58,80)});
        const int maskWidth=507,maskHeight=301;
        var values=new byte[maskWidth*maskHeight];
        Array.Fill(values,byte.MaxValue);
        var solid=new CoverageClip(-2,-3,maskWidth,maskHeight,values);
        for(int i=0;i<values.Length;i++)values[i]=(byte)(i*71+17);
        var first=new CoverageClip(-2,-3,maskWidth,maskHeight,values);
        var second=new CoverageClip(4,5,maskWidth-17,maskHeight-13,values.AsSpan(0,(maskWidth-17)*(maskHeight-13)));
        CoverageClip? intersection=null;
        var rows=new List<object>();
        using var process=Process.GetCurrentProcess();

        void Measure(string name,Action frame,Func<string> capture,int count)
        {
            for(int i=0;i<8;i++)frame();
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            int gen0=GC.CollectionCount(0),gen1=GC.CollectionCount(1),gen2=GC.CollectionCount(2);
            long allocated=GC.GetTotalAllocatedBytes(true);
            var cpu=process.TotalProcessorTime;
            var timer=Stopwatch.StartNew();
            for(int i=0;i<count;i++)frame();
            timer.Stop();cpu=process.TotalProcessorTime-cpu;
            allocated=GC.GetTotalAllocatedBytes(true)-allocated;
            int[] collections=[GC.CollectionCount(0)-gen0,GC.CollectionCount(1)-gen1,GC.CollectionCount(2)-gen2];
            process.Refresh();
            rows.Add(new {name,count,msPerOperation=timer.Elapsed.TotalMilliseconds/count,cpuMsPerOperation=cpu.TotalMilliseconds/count,
                bytesPerOperation=(double)allocated/count,collections,workingSetMiB=process.WorkingSet64/1048576d,
                privateMiB=process.PrivateMemorySize64/1048576d,retainedMiB=GC.GetTotalMemory(true)/1048576d,hash=capture()});
        }
        string BitmapHash()
        {
            using var locked=((IReadableBitmapImpl)bitmap).Lock();
            var bytes=new byte[locked.RowBytes*locked.Size.Height];
            Marshal.Copy(locked.Address,bytes,0,bytes.Length);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        string ClipHash()
        {
            var clip=intersection!;
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            for(int y=clip.Top;y<clip.Top+clip.Height;y++)hash.AppendData(clip.GetRow(y));
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        Measure("stable-lines-and-borders",()=>
        {
            using var draw=bitmap.CreateDrawingContext();draw.Clear(Colors.White);
            for(int i=0;i<18;i++)
            {
                draw.DrawLine(pen,new(10.25,7.5+i*15),new(480.5-i*5,14.75+i*15));
                draw.DrawRectangle(null,pen,new Rect(12.25+i*23,19.5+i*5,38.75,43));
            }
        },BitmapHash,frames);
        Measure("rectangular-box-shadow",()=>
        {
            using var draw=bitmap.CreateDrawingContext();draw.Clear(Colors.Transparent);
            draw.DrawRectangle(Brushes.White,null,new Rect(23.75,14.25,440,268),shadows);
        },BitmapHash,Math.Max(12,frames/8));
        Measure("soft-clip-product",()=>intersection=CoverageClip.Intersect(first,second),ClipHash,frames);
        int frameIndex=0;
        Measure("changing-soft-clip",()=>
        {
            values[0]=(byte)++frameIndex;
            var changed=new CoverageClip(4,5,second.Width,second.Height,values.AsSpan(0,second.Width*second.Height));
            intersection=CoverageClip.Intersect(first,changed);
        },ClipHash,frames);
        Measure("opaque-parent-clip",()=>intersection=CoverageClip.Intersect(solid,second),ClipHash,frames);
        const int sourceWidth=640,sourceHeight=360,sourceStride=sourceWidth*4+7;
        var source=new byte[sourceStride*sourceHeight];
        new Random(871).NextBytes(source);
        foreach(var (width,height) in new[]{(620,344),(320,180)})
        {
            int stride=width*4+3;
            var destination=new byte[stride*height];
            Measure($"scale-linear-{width}x{height}",()=>
                Cpu.PixelTransferKernel.ScaleLinear32(source,sourceWidth,sourceHeight,sourceStride,destination,width,height,stride,false,false),
                ()=>Convert.ToHexString(SHA256.HashData(destination)),Math.Max(12,frames/2));
        }
        File.WriteAllText(Path.Combine(output,"primitives.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    }
}
