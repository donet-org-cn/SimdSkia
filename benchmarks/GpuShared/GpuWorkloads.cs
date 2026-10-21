using SkiaSharp;

namespace SimdSkia.Benchmarks;

/// <summary>Abstract GPU render target: one frame = full draw list + submission + fence wait + host readback.</summary>
public interface IGpuTarget : IDisposable
{
    SKCanvas Canvas {get;}
    /// <summary>Submits pending GPU work, waits for completion and copies pixels back to the host.</summary>
    void Present();
    /// <summary>Returns a CPU snapshot of the current target pixels (must Present first).</summary>
    SKBitmap Capture();
}

internal sealed class GpuWorkload : IDisposable
{
    public static readonly string[] Names=["clear","rectangles","blend","evenodd","stroke","rect-aa","gradient","image","curve-aa","chinese"];
    public static readonly string[] ChangingNames=["curve-changing","chinese-changing"];
    private readonly string name;
    private readonly int width,height;
    private readonly IGpuTarget target;
    private readonly SKPaint paint;
    private readonly SKPath path;
    private readonly SKShader shader;
    private readonly SKTypeface? face;
    private readonly SKFont? font;
    private readonly SKBitmap source;
    private readonly SKRect[] rectangles;
    private readonly SKColor[] colors;
    public int OperationsPerFrame=>name is "rectangles" or "blend"?64:name.StartsWith("chinese",StringComparison.Ordinal)?4:1;
    public int AllowedPixelError=>name is "blend" or "gradient"?1:name=="rect-aa"?2:name is "curve-aa" or "chinese" or "curve-changing" or "chinese-changing"?32:0;
    public GpuWorkload(string name,int width,int height,string fontPath,Func<int,int,IGpuTarget> factory)
    {
        this.name=name;this.width=width;this.height=height;
        target=factory(width,height);paint=new();path=new();
        source=CreatePixels(32,32);
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
    }
    public void Render(int frame)
    {
        var canvas=target.Canvas;
        if(name=="clear")canvas.Clear(new SKColor((byte)(40+(frame&7)),120,200,128));
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
        // No Flush here: the worker times Render + Present as one full GPU round-trip frame.
    }
    public void Present()=>target.Present();
    public SKBitmap ValidationBitmap(){Render(0);target.Present();return target.Capture();}
    public static SKBitmap CreatePixels(int width,int height)
    {
        var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Unpremul));
        var pixels=bitmap.GetPixelSpan();for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {int i=y*bitmap.RowBytes+x*4;pixels[i]=(byte)(x*17+y*3);pixels[i+1]=(byte)(x*7+y*13);pixels[i+2]=(byte)((x/8+y/8)*23);pixels[i+3]=255;}
        return bitmap;
    }
    public void Dispose(){font?.Dispose();face?.Dispose();shader.Dispose();path.Dispose();paint.Dispose();source.Dispose();target.Dispose();}
}
