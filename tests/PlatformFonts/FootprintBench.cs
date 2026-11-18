using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Media;
using SimdSkia;
using SimdSkia.Avalonia;
using SimdSkia.Cpu;
using SimdSkia.Text;
using FontManager = SimdSkia.Avalonia.FontManager;

internal static class FootprintBench
{
    internal static void Installed()
    {
        var installed=SimdSkiaSystemFonts.Discover();
        var manager=new FontManager(installed.Files,installed.DefaultFamily,supportedFacesOnly:true);
        Console.WriteLine($"font-files {installed.Files.Count}");
        foreach(bool mixed in new[]{false,true})
            Measure(mixed?"alternating-style":"same-style",30_000,i=>
            {
                var weight=mixed&&(i&1)==0?FontWeight.Bold:FontWeight.Normal;
                if(!manager.TryMatchCharacter((i&2)==0?'A':'中',FontStyle.Normal,weight,FontStretch.Normal,installed.DefaultFamily,null,out var face))
                    throw new InvalidOperationException("This workload requires Latin and Chinese installed fonts.");
                using(face)return (int)face.Weight+face.FamilyName.Length;
            });
    }

    internal static void Run(string fontPath)
    {
        var manager=new FontManager([fontPath]);
        var family=manager.GetDefaultFontFamilyName();
        Measure("family",100_000,i=>
        {
            if(!manager.TryCreateGlyphTypeface(family,FontStyle.Normal,FontWeight.Normal,FontStretch.Normal,out var face))throw new InvalidOperationException();
            using(face)return (int)face.Weight;
        });
        Measure("fallback",100_000,i=>
        {
            if(!manager.TryMatchCharacter((i&1)==0?'A':'中',FontStyle.Normal,FontWeight.Normal,FontStretch.Normal,
                (i&2)==0?family:null,null,out var face))throw new InvalidOperationException();
            using(face)return (int)face.Weight;
        });
        byte[] bytes=File.ReadAllBytes(fontPath);
        using(var registered=new MemoryStream(bytes))
        {
            if(!manager.TryCreateGlyphTypeface(registered,FontSimulations.None,out var face))throw new InvalidOperationException();
            face.Dispose();
        }
        byte[] different=new byte[bytes.Length+1];bytes.CopyTo(different,0);
        Measure("new-font-stream",12,i=>
        {
            using var input=new MemoryStream(different);
            if(!manager.TryCreateGlyphTypeface(input,FontSimulations.None,out var face))throw new InvalidOperationException();
            using(face)return face.FamilyName.Length;
        });
        var font=new TrueTypeFont(bytes);
        Measure("glyph-bounds",200_000,i=>(int)font.GetGlyphBounds((ushort)(i%font.GlyphCount)).Right);
        var api=new RenderInterface();
        var rect=api.CreateRectangleGeometry(new(0,0,151,79));
        var curve=api.CreateEllipseGeometry(new(0,0,151,79));
        var matrix=Matrix.CreateScale(1.5,1.5)*Matrix.CreateTranslation(12,7);
        Measure("transform",20_000,i=>(int)rect.WithTransform(matrix).Bounds.Right);
        Measure("hit-rectangle",30_000,i=>rect.FillContains(new Point(i%197,i%97))?1:0);
        Measure("hit-ellipse",12_000,i=>curve.FillContains(new Point(i%197,i%97))?1:0);
        using var pixels=new PixelBuffer(1024,400,PixelFormat.Bgra8888,AlphaFormat.Premultiplied);
        using var raster=new RasterCanvas(pixels);
        foreach(bool vertical in new[]{true,false})
        {
            var gradient=new LinearGradient(new(5,9),new(vertical?5:873,371),new(223,238,252,255),new(31,117,69,255));
            var draw=DrawingCommand.FillRectangle(new(0,0,1024,400),Matrix3.Identity,
                new(new(255,255,255,255),CompositeMode.Source,false,gradient));
            Measure(vertical?"vertical-gradient":"diagonal-gradient",240,i=>
            {
                raster.Execute(draw);return pixels.GetRowSpan(i%400)[(i%1024)*4];
            });
            Console.WriteLine($"pixels {Convert.ToHexString(SHA256.HashData(pixels.GetPixelSpan()))}");
        }
    }

    private static void Measure(string name,int count,Func<int,int> operation)
    {
        for(int i=0;i<Math.Min(count,8);i++)operation(i);
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        long before=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
        int checksum=0;
        for(int i=0;i<count;i++)checksum=unchecked(checksum*31+operation(i));
        double elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,$"{name} {count} {elapsed:F6} {allocated} {checksum}"));
    }
}
