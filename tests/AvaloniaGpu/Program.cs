using SimdSkia;
using SimdSkia.Avalonia;
using SimdSkia.Cpu;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This hardware integration suite currently requires Windows Direct2D and Vulkan devices.");
int checks = 0;
void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
foreach (var backend in Enum.GetValues<SimdSkiaGpuBackend>())
foreach (var format in Enum.GetValues<PixelFormat>())
{
    using var target = new PixelBuffer(48, 40, format, AlphaFormat.Premultiplied, 48 * 4 + 16);
    using var expected = new PixelBuffer(48, 40, format, AlphaFormat.Premultiplied);
    var options = new SimdSkiaOptions(); options.UseGpu(backend, allowCpuFallback: true, presentation: SimdSkiaGpuPresentation.FramebufferReadback);
    using var gpu = options.CanvasBackendFactory!(target); using var cpu = new RasterCanvas(expected);
    void Both(DrawingCommand command) { gpu.Execute(command); cpu.Execute(command); }
    Both(DrawingCommand.Clear(new(255, 255, 255, 255)));
    Both(DrawingCommand.FillRectangle(new(1, 2, 12, 18), Matrix3.Identity, new(new(255, 0, 0, 255), CompositeMode.SourceOver)));
    Both(DrawingCommand.FillRectangle(new(15, 1, 25, 18), Matrix3.Identity, new(new(255, 255, 255, 255), CompositeMode.SourceOver, false, new TestShader())));
    Both(DrawingCommand.FillRectangle(new(30, 3, 45, 18), Matrix3.Identity, new(new(0, 0, 255, 255), CompositeMode.SourceOver)));
    gpu.Flush();
    Check(target.ReadPixel(5, 5) == new Rgba32(255, 0, 0, 255), "GPU pixels survive a CPU fallback.");
    Check(target.ReadPixel(20, 5) == new Rgba32(0, 255, 0, 255), "CPU fallback pixels are uploaded before later GPU draws.");
    Check(target.ReadPixel(35, 5) == new Rgba32(0, 0, 255, 255), "GPU draws after a CPU fallback preserve order.");
    byte[] coverage=Enumerable.Range(0,64).Select(i=>(byte)((i%4)*85)).ToArray();
    Both(DrawingCommand.FillRectangle(new(0,0,48,40),Matrix3.Identity,new(new(255,255,0,160),CompositeMode.SourceOver)) with{CoverageClip=new CoverageClip(12,4,8,8,coverage)});
    gpu.Flush();
    var outline = PathGeometry.Rectangle(new(0, 0, 5, 5));
    var run = new GlyphRunGeometry([new(outline, new(3, 24)), new(outline, new(6, 24))], Matrix3.Identity);
    Both(new(CommandKind.DrawGlyphRun, default, Matrix3.Identity, new(new(0, 0, 0, 128), CompositeMode.SourceOver)) { GlyphRun = run });
    gpu.Flush();
    Check(Math.Abs(target.ReadPixel(7, 26).R - expected.ReadPixel(7, 26).R) <= 1, "Overlapping glyph outlines composite in input order.");
    for (int y = 0; y < target.Height; y++)
        for (int x = 0; x < target.Width; x++)
        {
            var a = target.ReadPixel(x, y); var b = expected.ReadPixel(x, y);
            Check(Math.Abs(a.R - b.R) <= 1 && Math.Abs(a.G - b.G) <= 1 && Math.Abs(a.B - b.B) <= 1 && Math.Abs(a.A - b.A) <= 1, "Independent scalar/GPU command-order pixel gate.");
        }
    gpu.Execute(DrawingCommand.FillRectangle(new(2, 32, 10, 38), Matrix3.Identity, new(new(255, 0, 255, 255), CompositeMode.SourceOver)));
    gpu.Flush();
    Check(target.ReadPixel(5, 35) == new Rgba32(255, 0, 255, 255) && target.ReadPixel(5, 5) == new Rgba32(255, 0, 0, 255), "Persistent GPU target retains the preceding completed frame.");
    // Fractional clipping must happen for each glyph before overlapping glyphs blend.
    foreach(byte opacity in new byte[]{128,255})foreach(byte coverageValue in new byte[]{64,128})
    {
        var fractionalClip=new CoverageClip(0,0,48,40,Enumerable.Range(0,48*40).Select(i=>(byte)(coverageValue+i%48%7*17)).ToArray());
        foreach(float origin in new[]{0f,1.25f,5.25f})
        {
            var clippedRun=new DrawingCommand(CommandKind.DrawGlyphRun,default,Matrix3.Identity with{M02=origin},new(new(0,0,0,opacity),CompositeMode.SourceOver)){GlyphRun=run,CoverageClip=fractionalClip};
            Both(DrawingCommand.Clear(new(255,255,255,255)));Both(clippedRun);gpu.Flush();
            int overlapX=7+(int)MathF.Floor(origin);
            Check(Math.Abs(target.ReadPixel(overlapX,26).R-expected.ReadPixel(overlapX,26).R)<=1,"Fractional clip preserves overlapping glyph alpha order and cached scroll/mask identity.");
        }
    }
    var strict = new SimdSkiaOptions(); strict.UseGpu(backend, presentation: SimdSkiaGpuPresentation.FramebufferReadback);
    using var strictTarget = PixelBuffer.Rent(8, 8); using var strictCanvas = strict.CanvasBackendFactory!(strictTarget);
    bool rejected = false;
    try { strictCanvas.Execute(DrawingCommand.FillRectangle(new(0, 0, 8, 8), Matrix3.Identity, new(new(255, 255, 255, 255), CompositeMode.SourceOver, Shader: new TestShader()))); }
    catch (NotSupportedException) { rejected = true; }
    Check(rejected, "Unsupported GPU features require an explicit fallback option.");
    Check(SimdSkiaGpuDiagnostics.GetStatistics().CpuFallbackCommands > 0, "Fallback is recorded in public diagnostics.");
    foreach(var extend in Enum.GetValues<GradientExtendMode>())foreach(bool transformed in new[]{false,true})
    {
        Both(DrawingCommand.Clear(new(255,255,255,255)));
        Both(DrawingCommand.FillRectangle(new(0,0,48,40),Matrix3.Identity,new(new(255,255,255,211),CompositeMode.SourceOver,Shader:new LinearFixture(extend,transformed))));gpu.Flush();
        for(int y=2;y<38;y+=4)for(int x=1;x<47;x+=3){var a=target.ReadPixel(x,y);var b=expected.ReadPixel(x,y);Check(Math.Abs(a.R-b.R)<=2&&Math.Abs(a.G-b.G)<=2&&Math.Abs(a.B-b.B)<=2&&Math.Abs(a.A-b.A)<=1,$"GPU multistop gradient, transform and spread: {backend} {extend} ({x},{y}) actual={a}, expected={b}.");}
    }
    using(var image=new PixelBuffer(128,96,PixelFormat.Rgba8888,AlphaFormat.Straight))
    {
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)image.WritePixel(x,y,new((byte)(x*7),(byte)(y*9),(byte)(x+y),180));
        foreach(var sampling in Enum.GetValues<ImageSampling>())
        {
            Both(DrawingCommand.Clear(new(255,255,255,255)));
            Both(new(CommandKind.DrawImage,new(3,4,42,35),Matrix3.Identity,new(new(255,255,255,211),CompositeMode.SourceOver)){Image=image,ImageSource=new(17.25f,23.5f,24.75f,30.25f),Sampling=sampling});gpu.Flush();
            for(int y=5;y<34;y+=4)for(int x=4;x<41;x+=4){var a=target.ReadPixel(x,y);var b=expected.ReadPixel(x,y);Check(Math.Abs(a.R-b.R)<=2&&Math.Abs(a.G-b.G)<=2&&Math.Abs(a.B-b.B)<=2&&Math.Abs(a.A-b.A)<=2,"Cropped GPU uploads preserve fractional source bounds and sampling.");}
        }
    }
    Console.WriteLine($"PASS {backend} {format}: pixel ordering, glyph overlap, persistence and strict rejection");
}
Console.WriteLine($"PASS {checks} GPU integration assertions");

sealed class TestShader : IColorShader { public Rgba32 Sample(float x, float y) => new(0, 255, 0, 255); }

sealed class LinearFixture(GradientExtendMode extend,bool transformed):IColorShader
{
    private readonly Rgba32[] colors=[new(255,0,0,80),new(0,255,0,200),new(0,0,255,255)];
    private readonly Matrix3 sample=transformed?new(1,.2f,-.25f,.1f,1,0,0,0,1):Matrix3.Identity;
    public Rgba32 Sample(float x,float y)
    {
        float t=sample.Map(x,y).X/16;
        t=extend switch{GradientExtendMode.Clamp=>Math.Clamp(t,0,1),GradientExtendMode.Repeat=>t-MathF.Floor(t),_=>1-MathF.Abs((t-2*MathF.Floor(t/2))-1)};
        int i=t<.5f?0:1;float f=(t-i*.5f)*2;var a=colors[i];var b=colors[i+1];
        byte Mix(byte v,byte w)=>(byte)Math.Clamp((int)MathF.Round(v+(w-v)*f),0,255);return new(Mix(a.R,b.R),Mix(a.G,b.G),Mix(a.B,b.B),Mix(a.A,b.A));
    }
    public bool TryGetLinearGradient(out LinearGradientDescription? gradient){gradient=new(new(0,0),new(16,0),[new(0,colors[0]),new(.5f,colors[1]),new(1,colors[2])],extend,sample);return true;}
}
