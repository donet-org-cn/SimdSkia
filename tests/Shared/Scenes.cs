using SkiaSharp;
using System.Text.Json;

namespace Scenes;

internal static class Suite
{
    public static void Render(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var format in new[] { SKColorType.Rgba8888,SKColorType.Bgra8888 })
        foreach (var alpha in new[] { SKAlphaType.Premul,SKAlphaType.Opaque,SKAlphaType.Unpremul })
        foreach (string scene in new[] { "rectangle","blend","transform","fractional","source","clear","transparent-clear","clear-then-blend","mirror","rotation","inverted","zero-area","path-aa","rect-aa","stroke","evenodd","gradient","clip","image","chinese" })
        {
            string name=$"{scene}-{format}-{alpha}";
            using var bitmap=new SKBitmap(new SKImageInfo(64,48,format,alpha),64*4+16);
            using var canvas=new SKCanvas(bitmap);
            using var paint=new SKPaint { Color=SKColors.Red,IsAntialias=false };
            canvas.Clear(new SKColor(20,40,80,255));
            switch (scene)
            {
                case "rectangle": canvas.DrawRect(SKRect.Create(8,10,24,16),paint); break;
                case "blend":
                    paint.Color=new SKColor(200,100,50,128); canvas.DrawRect(SKRect.Create(4,6,28,24),paint);
                    paint.Color=new SKColor(40,200,180,96); canvas.DrawRect(SKRect.Create(18,12,36,24),paint); break;
                case "transform":
                    int count=canvas.Save(); canvas.Translate(12,6); canvas.Scale(2,1.5f);
                    canvas.DrawRect(SKRect.Create(0,0,10,12),paint); canvas.RestoreToCount(count);
                    paint.Color=SKColors.White; canvas.DrawRect(SKRect.Create(0,0,3,3),paint); break;
                case "fractional": canvas.DrawRect(SKRect.Create(8.25f,10.75f,23.25f,16.75f),paint); break;
                case "source": paint.Color=new SKColor(200,100,50,128); paint.BlendMode=SKBlendMode.Src; canvas.DrawRect(SKRect.Create(8,10,24,16),paint); break;
                case "clear": paint.BlendMode=SKBlendMode.Clear; canvas.DrawRect(SKRect.Create(8,10,24,16),paint); break;
                case "transparent-clear": canvas.Clear(new SKColor(200,100,50,128)); break;
                case "clear-then-blend":
                    canvas.Clear(); paint.Color=new SKColor(200,100,50,128); canvas.DrawRect(SKRect.Create(8,10,24,16),paint); break;
                case "mirror": canvas.Translate(40,0); canvas.Scale(-1,1); canvas.DrawRect(SKRect.Create(2,3,10,12),paint); break;
                case "rotation": canvas.Translate(40,4); canvas.RotateDegrees(90); canvas.DrawRect(SKRect.Create(0,0,12,8),paint); break;
                case "inverted": canvas.DrawRect(new SKRect(32,26,8,10),paint); break;
                case "zero-area": canvas.DrawRect(new SKRect(16,10,16,26),paint); break;
                case "path-aa":
                    paint.IsAntialias=true;using(var path=new SKPath()){path.MoveTo(3,24);path.QuadTo(32,1,60,24);path.CubicTo(55,44,8,44,3,24);path.Close();canvas.DrawPath(path,paint);}break;
                case "rect-aa":paint.IsAntialias=true;canvas.DrawRect(new SKRect(8.25f,10.25f,31.75f,26.75f),paint);break;
                case "stroke":paint.Style=SKPaintStyle.Stroke;paint.StrokeWidth=4;canvas.DrawRect(SKRect.Create(8,10,24,16),paint);break;
                case "evenodd":using(var path=new SKPath()){path.FillType=SKPathFillType.EvenOdd;path.AddRect(SKRect.Create(4,4,56,40));path.AddRect(SKRect.Create(16,12,32,24));canvas.DrawPath(path,paint);}break;
                case "gradient":using(var shader=SKShader.CreateLinearGradient(new(0,0),new(64,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp)){paint.Shader=shader;canvas.DrawRect(SKRect.Create(64,48),paint);}break;
                case "clip":int saved=canvas.Save();canvas.ClipRect(new SKRect(12.25f,12.25f,32.75f,32.75f));canvas.DrawRect(SKRect.Create(64,48),paint);canvas.RestoreToCount(saved);break;
                case "image":using(var source=new SKBitmap(2,2)){source.Erase(SKColors.Blue);source.SetPixel(0,0,SKColors.Red);canvas.DrawBitmap(source,new SKRect(8,10,32,26));}break;
                case "chinese":
                    string fontPath=Environment.GetEnvironmentVariable("SIMDSKIA_CJK_FONT")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"simhei.ttf");
                    using(var face=SKTypeface.FromFile(fontPath))using(var font=new SKFont(face,18){Hinting=SKFontHinting.None,Subpixel=true})canvas.DrawText("中文 A",2,32,font,paint);break;
            }
            // Canonical RGBA, independent of storage channel order and row padding.
            var rgba=new byte[64*48*4];
            for (int y=0;y<48;y++) for (int x=0;x<64;x++)
            {
                var c=bitmap.GetPixel(x,y); int i=(y*64+x)*4;
                rgba[i]=c.Red; rgba[i+1]=c.Green; rgba[i+2]=c.Blue; rgba[i+3]=c.Alpha;
            }
            File.WriteAllBytes(Path.Combine(directory,name+".rgba"),rgba);
            using(var encoded=bitmap.Encode(SKEncodedImageFormat.Png,100))using(var stream=File.Create(Path.Combine(directory,name+".png")))encoded.SaveTo(stream);
            File.WriteAllText(Path.Combine(directory,name+".json"),JsonSerializer.Serialize(new { Width=64,Height=48,bitmap.RowBytes,bitmap.ByteCount,Format=format.ToString(),Alpha=alpha.ToString() }));
        }
        Console.WriteLine($"Rendered 120 CPU scenes to {Path.GetFullPath(directory)}");
    }
}
