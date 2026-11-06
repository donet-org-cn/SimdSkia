using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool value){checks++;if(!value)throw new Exception("Float shader assertion "+checks);}
for(int tagged=0;tagged<2;tagged++)for(int scene=0;scene<9;scene++)for(int mode=0;mode<4;mode++)for(int transform=0;transform<2;transform++)
{
    using var cs=transform==0?SKColorSpace.CreateSrgb():SKColorSpace.CreateSrgbLinear();
    SKColorF[] colors=[new(.93f,.07f,.21f,.83f),new(.16f,.87f,.34f,.39f),new(.27f,.11f,.92f,.71f)];float[] offsets=[0,.37f,1];var tile=(SKShaderTileMode)mode;var matrix=transform==0?SKMatrix.Identity:new SKMatrix(1.1f,.1f,2,.2f,.9f,3,0,0,1);
    using var shader=scene switch
    {
        0=>SKShader.CreateLinearGradient(new(3,4),new(57,29),colors,cs,offsets,tile,matrix),
        1=>SKShader.CreateRadialGradient(new(29,23),24,colors,cs,offsets,tile,matrix),
        2=>SKShader.CreateSweepGradient(new(29,23),colors,cs,offsets,tile,30,300,matrix),
        3=>SKShader.CreateTwoPointConicalGradient(new(21,21),0,new(31,25),28,colors,cs,offsets,tile,matrix),
        4=>SKShader.CreateTwoPointConicalGradient(new(9,20),7,new(49,23),19,colors,cs,offsets,tile,matrix),
        5=>SKShader.CreateTwoPointConicalGradient(new(29,23),28,new(29,23),3,colors,cs,offsets,tile,matrix),
        6=>SKShader.CreateTwoPointConicalGradient(new(12,22),8,new(45,22),8,colors,cs,offsets,tile,matrix),
        7=>SKShader.CreateColor(new SKColorF(.12345f,.5689f,.8102f,.7345f),cs),
        _=>SKShader.CreateLinearGradient(new(0,0),new(64,0),[new SKColorF(0,0,0),new SKColorF(1,1,1)],cs,tile)
    };
    values[$"shader-{scene}-{mode}-{transform}"]=shader is not null;if(shader is null)continue;
    using var surface=SKSurface.Create(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul).WithColorSpace(tagged==0?null:SKColorSpace.CreateSrgb()))!;surface.Canvas.Clear(new SKColor(30,40,50));using(var paint=new SKPaint{Shader=shader}){shader.Dispose();cs.Dispose();surface.Canvas.DrawRect(new SKRect(0,0,64,48),paint);}
    using var image=surface.Snapshot();using var read=new SKBitmap(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));var bytes=read.GetPixelSpan().ToArray();cases.Add(new{kind="float-shader",scene=scene+tagged*9,transform=mode*2+transform,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"Captured {cases.Count} float shader images, {values.Count} metadata entries and {checks} lifetime checks");
