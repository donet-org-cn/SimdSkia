using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SimdSkia.ControlValidation;

// The reference project compiles this same source. It deliberately bypasses
// control layout so mask coverage can be distinguished from shaping/layout.
internal static class TextProbe
{
    internal static void Clips(string output)
    {
        var factory=AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>();var cases=new List<object>();
        foreach(double edge in new[]{-1.5,-.5,0,.125,.375,.5,.625,.875,1,1.5,2.5})foreach(bool left in new[]{false,true})
        {
            using var image=factory.CreateRenderTargetBitmap(new(8,1),new(96,96));
            using(var c=image.CreateDrawingContext())
            {c.Clear(Colors.Transparent);c.PushClip(left?new Rect(edge,0,8-edge,1):new Rect(0,0,edge+4,1));c.DrawRectangle(Brushes.Red,null,new Rect(0,0,8,1));c.PopClip();}
            using var locked=((IReadableBitmapImpl)image).Lock();var bytes=new byte[32];Marshal.Copy(locked.Address,bytes,0,32);
            cases.Add(new{edge,left,alpha=Enumerable.Range(0,8).Select(i=>bytes[i*4+3]).ToArray()});
        }
        File.WriteAllText(Path.Combine(output,"clips.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));
    }
    internal static void Run(string output,FontFamily family)
    {
        var factory=AvaloniaLocator.Current.GetRequiredService<IPlatformRenderInterface>();
        var shaper=AvaloniaLocator.Current.GetRequiredService<ITextShaperImpl>();
        var face=new Typeface(family).GlyphTypeface;
        var cases=new List<object>();int row=0;
        using var image=factory.CreateRenderTargetBitmap(new(960,1008),new(96,96));
        using(var context=image.CreateDrawingContext())
        {
            context.Clear(Colors.White);
            context.PushTextOptions(new(){TextRenderingMode=TextRenderingMode.Antialias,TextHintingMode=TextHintingMode.None,BaselinePixelAlignment=BaselinePixelAlignment.Unaligned});
            foreach(double size in new[]{12d,18,27})foreach(double scale in new[]{1d,1.5})foreach(double phase in new[]{0d,.125,.375,.625})
            {
                int y=row++*42;
                using var shaped=shaper.ShapeText("AVATAR 中文界面 123.45".AsMemory(),new(face,size));
                using var run=factory.CreateGlyphRun(face,size,shaped,new(0,0));
                context.Transform=new Matrix(scale,0,0,scale,8+phase,y+34+phase);
                context.DrawGlyphRun(Brushes.Black,run);
                context.Transform=new Matrix(scale,0,0,scale,470+phase,y+34+phase);
                context.DrawGlyphRun(Brushes.MidnightBlue,run);
                cases.Add(new{size,scale,phase,top=y,height=42});
            }
            context.PopTextOptions();
        }
        using(var stream=File.Create(Path.Combine(output,"text.png")))image.Save(stream,PngBitmapEncoderOptions.Default);
        using var locked=((IReadableBitmapImpl)image).Lock();var rgba=new byte[960*1008*4];
        for(int y=0;y<1008;y++)Marshal.Copy(locked.Address+y*locked.RowBytes,rgba,y*960*4,960*4);
        if(locked.Format==PixelFormats.Bgra8888)for(int i=0;i<rgba.Length;i+=4)(rgba[i],rgba[i+2])=(rgba[i+2],rgba[i]);
        File.WriteAllBytes(Path.Combine(output,"text.rgba"),rgba);
        File.WriteAllText(Path.Combine(output,"text-cases.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Wrote {cases.Count} isolated text mask cases.");
    }
}
