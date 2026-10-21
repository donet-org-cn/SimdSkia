using SkiaSharp;

namespace SimdSkia.Benchmarks;

// Replays the SKTextBlobBuilder -> SKCanvas.DrawText pattern from Avalonia.Skia
// 12.1.4 GlyphRunImpl / DrawingContextImpl. This does not run Avalonia layout,
// its compositor, HarfBuzz, hit testing, platform windows, or a GPU swapchain.
internal sealed class Workload : IDisposable
{
    public static readonly string[] Names=["ui-labels","ui-scroll","ui-dpi-scroll","ui-text-rebuild","ui-opaque-images"];
    public static readonly string[] ChangingNames=[];
    private readonly string name;
    private readonly int width,height;
    private readonly SKBitmap target,icon;
    private readonly SKTypeface face;
    private readonly SKFont font;
    private readonly SKPaint paint=new(){Color=new SKColor(230,235,245),IsAntialias=true};
    private readonly SKTextBlob[] labels;
    private readonly ushort[][] glyphs;
    private readonly SKPoint[][] positions;
    public long EncodedBytes=>0;
    public long InputEncodedBytes=>0;
    public int OperationsPerFrame=>name=="ui-opaque-images"?48:24;
    public int AllowedPixelError=>name=="ui-opaque-images"?0:32;
    public Workload(string name,int width,int height,string fixtureDirectory,string fontPath)
    {
        if(!Names.Contains(name))throw new ArgumentException(name);
        this.name=name;this.width=width;this.height=height;
        target=new(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Premul));
        icon=CreatePixels(64,48);face=SKTypeface.FromFile(fontPath);font=new(face,16){Hinting=SKFontHinting.None,Subpixel=true,Edging=SKFontEdging.Antialias,BaselineSnap=false,LinearMetrics=true,Embolden=false,ForceAutoHinting=false};
        labels=new SKTextBlob[24];glyphs=new ushort[24][];positions=new SKPoint[24][];
        // One common set of reference glyph advances is used by both engines.
        var fixture=System.Text.Json.JsonSerializer.Deserialize<LabelFixture[]>(File.ReadAllText(Path.Combine(fixtureDirectory,"labels.json")))!;
        for(int i=0;i<labels.Length;i++)
        {
            var item=fixture[i];glyphs[i]=item.Glyphs;positions[i]=item.X.Select(x=>new SKPoint(x,0)).ToArray();
            labels[i]=Build(i);
        }
    }
    public void Render(int frame)
    {
        // FramebufferRenderTarget can retain its surface while disposing canvas
        // wrappers at the end of a drawing session. Exercise fresh wrappers.
        using var canvas=new SKCanvas(target);canvas.Clear(new SKColor(24,28,38));
        float dpi=name=="ui-dpi-scroll"?1.5f:1f;canvas.Scale(dpi);
        canvas.ClipRect(new SKRect(12,12,width/dpi-12,height/dpi-12));
        float scroll=name is "ui-scroll" or "ui-dpi-scroll"?(frame%256)*.25f:0;
        for(int row=0;row<(name=="ui-opaque-images"?48:24);row++)
        {
            if(name=="ui-opaque-images")
            {canvas.DrawBitmap(icon,16+(row%8)*70,16+(row/8)*54);continue;}
            float y=40+row*23-scroll;
            // Cull outside rows before drawing, as Avalonia's retained scene does.
            if(y<4||y-18>height/dpi-12)continue;
            int saved=canvas.Save();canvas.Translate(20,y);
            if(name=="ui-text-rebuild"){using var blob=Build(row);canvas.DrawText(blob,0,0,paint);}
            else canvas.DrawText(labels[row],0,0,paint);
            canvas.RestoreToCount(saved);
        }
        canvas.Flush();
    }
    private SKTextBlob Build(int row)
    {
        using var builder=new SKTextBlobBuilder();var run=builder.AllocatePositionedRun(font,glyphs[row].Length);
        run.SetGlyphs(glyphs[row]);run.SetPositions(positions[row]);return builder.Build()??throw new InvalidOperationException("Empty label.");
    }
    public SKBitmap ValidationBitmap(){Render(37);return target.Copy();}
    public static SKBitmap CreatePixels(int width,int height)
    {
        var bitmap=new SKBitmap(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Opaque));
        var bytes=bitmap.GetPixelSpan();for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {int p=y*bitmap.RowBytes+x*4;bytes[p]=(byte)(x*3);bytes[p+1]=(byte)(y*5);bytes[p+2]=(byte)(x+y);bytes[p+3]=255;}
        return bitmap;
    }
    public static void MakeLabels(string directory,string fontPath)
    {
#if ORIGINAL
        using var face=SKTypeface.FromFile(fontPath);using var font=new SKFont(face,16){Hinting=SKFontHinting.None,Subpixel=true,Edging=SKFontEdging.Antialias,BaselineSnap=false,LinearMetrics=true,Embolden=false,ForceAutoHinting=false};
        var labels=new List<LabelFixture>();
        for(int row=0;row<24;row++)
        {
            string text=$"中文列表 Item {row:00} 012345";var ids=font.GetGlyphs(text.EnumerateRunes().Select(r=>r.Value).ToArray());
            var widths=new float[ids.Length];font.GetGlyphWidths(ids,widths,Span<SKRect>.Empty);
            var x=new float[ids.Length];for(int i=1;i<x.Length;i++)x[i]=x[i-1]+widths[i-1];labels.Add(new(text,ids,x));
        }
        Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"labels.json"),System.Text.Json.JsonSerializer.Serialize(labels));
#else
        throw new NotSupportedException("Only the original process generates common glyph fixtures.");
#endif
    }
    public void Dispose(){foreach(var label in labels)label.Dispose();font.Dispose();face.Dispose();paint.Dispose();icon.Dispose();target.Dispose();}
}
internal sealed record LabelFixture(string Text,ushort[] Glyphs,float[] X);
