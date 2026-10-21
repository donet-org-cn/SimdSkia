using SkiaSharp;

namespace SimdSkia.Benchmarks;

internal sealed class Workload : IDisposable
{
    public static readonly string[] Names=["clear","rectangles","blend","evenodd","stroke","rect-aa","gradient","image","curve-aa","chinese","png-encode","png-decode"];
    public static readonly string[] ChangingNames=["curve-changing","chinese-changing"];
    private readonly string name;
    private readonly int width,height;
    private readonly SKBitmap target,source;
    private readonly SKCanvas canvas;
    private readonly SKPaint paint;
    private readonly SKPath path;
    private readonly SKShader shader;
    private readonly SKTypeface? face;
    private readonly SKFont? font;
    private readonly byte[] encodedInput;
    private readonly SKRect[] rectangles;
    private readonly SKColor[] colors;
    public long EncodedBytes {get;private set;}
    public int OperationsPerFrame=>name is "rectangles" or "blend"?64:name.StartsWith("chinese",StringComparison.Ordinal)?4:1;
    public int AllowedPixelError=>name is "blend" or "gradient"?1:name=="rect-aa"?2:name is "curve-aa" or "chinese" or "curve-changing" or "chinese-changing"?32:0;
    public long InputEncodedBytes=>encodedInput.Length;
    public Workload(string name,int width,int height,string fixtureDirectory,string fontPath)
    {
        this.name=name;this.width=width;this.height=height;
        target=new(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Premul));canvas=new(target);paint=new();path=new();
        source=CreatePixels(name.StartsWith("png-",StringComparison.Ordinal)?width:32,name.StartsWith("png-",StringComparison.Ordinal)?height:32);
        shader=SKShader.CreateLinearGradient(new(0,0),new(width,0),[new(240,40,20),new(20,80,240)],SKShaderTileMode.Clamp);
        rectangles=new SKRect[64];colors=new SKColor[64];
        for(int i=0;i<64;i++)
        {
            int cellWidth=width/8,cellHeight=height/8;
            rectangles[i]=name=="blend"?SKRect.Create(i*73%(width-width/3),i*37%(height-height/3),width/3,height/3):SKRect.Create(i%8*cellWidth+2,i/8*cellHeight+2,Math.Max(1,cellWidth-4),Math.Max(1,cellHeight-4));
            colors[i]=new((byte)(i*31),(byte)(i*17),(byte)(200-i*2),name=="blend"?(byte)(48+i%144):(byte)255);
        }
        if(name=="evenodd")
        {path.FillType=SKPathFillType.EvenOdd;path.AddRect(new(8,8,width-8,height-8));path.AddRect(new(width/4,height/4,width*3/4,height*3/4));}
        else if(name is "curve-aa" or "curve-changing")
        {path.MoveTo(width*.05f,height*.5f);path.QuadTo(width*.5f,height*.02f,width*.95f,height*.5f);path.CubicTo(width*.9f,height*.95f,width*.1f,height*.95f,width*.05f,height*.5f);path.Close();}
        if(name is "chinese" or "chinese-changing")
        {face=SKTypeface.FromFile(fontPath);font=new(face,Math.Min(48,height/8f)){Hinting=SKFontHinting.None,Subpixel=true};}
        encodedInput=name=="png-decode"?File.ReadAllBytes(Path.Combine(fixtureDirectory,$"input-{width}x{height}.png")):[];
    }
    public void Render(int frame)
    {
        if(name=="clear")canvas.Clear(new SKColor((byte)(40+(frame&7)),120,200,128));
        else if(name=="png-encode")
        {using var encoded=source.Encode(SKEncodedImageFormat.Png,100);EncodedBytes=encoded.Size;if(EncodedBytes==0)throw new InvalidOperationException("Empty PNG output.");}
        else if(name=="png-decode")
        {using var decoded=Decode(encodedInput,width,height);if(decoded.Width!=width||decoded.Height!=height||decoded.ColorType!=SKColorType.Rgba8888||decoded.AlphaType!=SKAlphaType.Unpremul)throw new InvalidOperationException("Decode output layout mismatch.");}
        else
        {
            canvas.Clear(new SKColor(20,40,80));
            bool changing=ChangingNames.Contains(name);int saved=canvas.SaveCount;
            if(changing){saved=canvas.Save();canvas.Translate((frame%97)/16f,0);}
            switch(name)
            {
                case "rectangles":case "blend":for(int i=0;i<64;i++){paint.Color=colors[i];canvas.DrawRect(rectangles[i],paint);}break;
                case "evenodd":paint.Color=new(230,80,40);canvas.DrawPath(path,paint);break;
                case "stroke":paint.Color=new(240,100,50);paint.Style=SKPaintStyle.Stroke;paint.StrokeWidth=4;canvas.DrawRect(new SKRect(8,8,width-8,height-8),paint);break;
                case "rect-aa":paint.Color=new(240,60,30);paint.IsAntialias=true;canvas.DrawRect(new SKRect(8.25f,8.25f,width-8.25f,height-8.25f),paint);break;
                case "gradient":paint.Shader=shader;canvas.DrawRect(SKRect.Create(width,height),paint);break;
                case "image":canvas.DrawBitmap(source,new SKRect(8,8,width-8,height-8));break;
                case "curve-aa":case "curve-changing":paint.IsAntialias=true;paint.Color=new(240,60,30);canvas.DrawPath(path,paint);break;
                case "chinese":case "chinese-changing":paint.Color=SKColors.White;for(int i=0;i<4;i++)canvas.DrawText("SimdSkia 中文 ABC 0123",8,(i+1)*height/5f,font!,paint);break;
                default:throw new ArgumentException("Unknown workload: "+name);
            }
            if(changing)canvas.RestoreToCount(saved);
        }
        canvas.Flush(); // Both CPU implementations finish synchronously; submission-only GPU timing is absent.
    }
    public SKBitmap ValidationBitmap()
    {
        if(name=="png-decode")return Decode(encodedInput,width,height);
        if(name=="png-encode"){using var encoded=source.Encode(SKEncodedImageFormat.Png,100);EncodedBytes=encoded.Size;return Decode(encoded.ToArray(),width,height);}
        Render(0);return target.Copy();
    }
    private static SKBitmap Decode(byte[] data,int width,int height)
    {
#if ORIGINAL
        return SKBitmap.Decode(data,new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Unpremul))??throw new InvalidDataException("Reference decoder failed.");
#else
        return SKBitmap.Decode(data);
#endif
    }
    public static SKBitmap CreatePixels(int width,int height)
    {
        var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Unpremul));
        var pixels=bitmap.GetPixelSpan();for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {int i=y*bitmap.RowBytes+x*4;pixels[i]=(byte)(x*17+y*3);pixels[i+1]=(byte)(x*7+y*13);pixels[i+2]=(byte)((x/8+y/8)*23);pixels[i+3]=255;}
        return bitmap;
    }
    public void Dispose(){font?.Dispose();face?.Dispose();shader.Dispose();path.Dispose();paint.Dispose();canvas.Dispose();source.Dispose();target.Dispose();}
}
