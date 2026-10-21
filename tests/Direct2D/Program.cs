using SimdSkia;
using SimdSkia.Cpu;
using SimdSkia.Gpu.Direct2D;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Direct2D validation requires Windows hardware.");
int checks = 0;
void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
foreach (var format in Enum.GetValues<PixelFormat>())
{
    using var pixels = new PixelBuffer(64, 48, format, AlphaFormat.Premultiplied, 64 * 4 + 16);
    pixels.WritePixel(63, 47, new(20, 40, 80, 255));
    using var canvas = new Direct2DCanvas(pixels);
    canvas.Execute(DrawingCommand.FillRectangle(new(2, 3, 20, 30), Matrix3.Identity, new(new(255, 0, 0, 255), CompositeMode.SourceOver)));
    canvas.Flush();
    Check(canvas.CompletedBatches == 1, "GPU completion is observed after Flush.");
    Check(pixels.ReadPixel(4, 5) == new Rgba32(255, 0, 0, 255), "Rectangle pixels must come from the Direct2D GPU target.");
    Check(pixels.ReadPixel(63, 47) == new Rgba32(20, 40, 80, 255), "Initial target pixels survive GPU drawing.");
    Check(pixels.ReadPixel(0, 0) == default, "Transparent premultiplied background.");
    canvas.Execute(DrawingCommand.Clear(new(255, 255, 255, 255)));
    canvas.Execute(DrawingCommand.FillRectangle(new(0, 0, 64, 48), Matrix3.Identity, new(new(0, 0, 255, 128), CompositeMode.SourceOver)) with { Clip = new RectF(10, 10, 30, 30) });
    canvas.Flush();
    var blended = pixels.ReadPixel(20, 20);
    Check(Math.Abs(blended.R - 127) <= 1 && Math.Abs(blended.G - 127) <= 1 && blended.B == 255 && blended.A == 255, "SourceOver uses premultiplied alpha.");
    Check(pixels.ReadPixel(2, 2) == new Rgba32(255, 255, 255, 255), "Device rectangle clip bounds.");
    canvas.Execute(new(CommandKind.FillPath, default, Matrix3.Identity, new(new(0, 255, 0, 255), CompositeMode.SourceOver)) { Path = PathGeometry.Rectangle(new(35, 5, 60, 35)) });
    canvas.Flush();
    Check(pixels.ReadPixel(40, 15) == new Rgba32(0, 255, 0, 255), "Native geometry path.");
    using var image = new PixelBuffer(2, 2, PixelFormat.Rgba8888, AlphaFormat.Straight); image.WritePixel(0, 0, new(255, 0, 255, 255));
    canvas.Execute(new(CommandKind.DrawImage, new(0, 0, 2, 2), Matrix3.Identity, new(new(255, 255, 255, 255), CompositeMode.SourceOver)) { Image = image });
    canvas.Flush();
    Check(pixels.ReadPixel(0, 0) == new Rgba32(255, 0, 255, 255), "Managed decoded image upload and nearest sampling.");
    image.WritePixel(0, 0, new(0, 255, 255, 255));
    canvas.Execute(new(CommandKind.DrawImage, new(0, 0, 2, 2), Matrix3.Identity, new(new(255, 255, 255, 255), CompositeMode.SourceOver)) { Image = image }); canvas.Flush();
    Check(pixels.ReadPixel(0, 0) == new Rgba32(0, 255, 255, 255), "Image mutation must not reuse stale texture contents.");
    using(var expected=new PixelBuffer(64,48,format,AlphaFormat.Premultiplied))
    using(var scalar=new RasterCanvas(expected))
    {
        var gradient=new GradientFixture();var command=DrawingCommand.FillRectangle(new(0,0,64,48),Matrix3.Identity,new(new(255,255,255,211),CompositeMode.SourceOver,Shader:gradient));
        canvas.Execute(DrawingCommand.Clear(default));canvas.Execute(command);canvas.Flush();scalar.Execute(command);
        for(int x=1;x<63;x+=4)
        {
            var actual=pixels.ReadPixel(x,20);var want=expected.ReadPixel(x,20);
            Check(Math.Abs(actual.A-want.A)<=1&&Math.Abs(actual.R-want.R)<=3&&Math.Abs(actual.G-want.G)<=3&&Math.Abs(actual.B-want.B)<=3,"Multistop sRGB gradients preserve straight-color interpolation and premultiplied alpha.");
        }
    }
    var threadError = Task.Run(() => { if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException(); try { canvas.Flush(); return false; } catch (InvalidOperationException) { return true; } }).GetAwaiter().GetResult();
    Check(threadError, "Context thread ownership is enforced.");
    checks+=TextureCacheValidation.Run(canvas,pixels,()=>OperatingSystem.IsWindows()?canvas.UploadedTextures:throw new PlatformNotSupportedException(),()=>OperatingSystem.IsWindows()?canvas.TextureCacheHits:throw new PlatformNotSupportedException());
    canvas.Dispose(); canvas.Dispose();
    try { canvas.Flush(); throw new InvalidOperationException("Expected disposed error."); } catch (ObjectDisposedException) { checks++; }
    Console.WriteLine($"GPU={canvas.DeviceName}; format={format}; completed={canvas.CompletedBatches}");
}
Console.WriteLine($"PASS {checks} Direct2D hardware checks");

sealed class GradientFixture:IColorShader
{
    private readonly Rgba32[] colors=[new(255,0,0,80),new(0,255,0,200),new(0,0,255,255)];
    public Rgba32 Sample(float x,float y)
    {
        float t=Math.Clamp(x/64,0,1);int index=t<.5f?0:1;float f=(t-index*.5f)*2;var a=colors[index];var b=colors[index+1];
        byte Mix(byte v,byte w)=>(byte)Math.Clamp((int)MathF.Round(v+(w-v)*f),0,255);return new(Mix(a.R,b.R),Mix(a.G,b.G),Mix(a.B,b.B),Mix(a.A,b.A));
    }
    public bool TryGetLinearGradient(out LinearGradientDescription? gradient){gradient=new(new(0,0),new(64,0),[new(0,colors[0]),new(.5f,colors[1]),new(1,colors[2])]);return true;}
}
