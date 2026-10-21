using SkiaSharp;
using System.Text.Json;
using System.Globalization;

namespace Scenes;

internal static class Behavior
{
    public static void Record(string directory)
    {
        var values=new SortedDictionary<string,string>(StringComparer.Ordinal);
        values["SKColors.Transparent"] = SKColors.Transparent.ToString();
        values["Rect.ZeroWidth.IsEmpty"] = new SKRect(1,2,1,4).IsEmpty.ToString();
        values["Rect.Inverted.IsEmpty"] = new SKRect(4,4,1,1).IsEmpty.ToString();
        values["Rect.RightEdge.Contains"] = new SKRect(0,0,2,2).Contains(2,1).ToString();
        var nan=SKMatrix.Identity; nan.ScaleX=float.NaN;
        values["Matrix.NaN.Equals"] = nan.Equals(nan).ToString();
        values["ImageInfo.Empty.IsEmpty"] = SKImageInfo.Empty.IsEmpty.ToString();
        values["Default.PlatformColorType"] = SKImageInfo.PlatformColorType.ToString();
        using var bitmap=new SKBitmap(2,2); using var canvas=new SKCanvas(bitmap); using var paint=new SKPaint();
        values["Default.Paint.Color"] = paint.Color.ToString();
        values["Default.Paint.IsAntialias"] = paint.IsAntialias.ToString();
        values["Default.Paint.Style"] = paint.Style.ToString();
        values["Default.Paint.BlendMode"] = paint.BlendMode.ToString();
        values["Canvas.Save.Return"] = canvas.Save().ToString();
        values["Canvas.Save.Count"] = canvas.SaveCount.ToString();
        canvas.RestoreToCount(0); values["Canvas.RestoreToZero.Count"] = canvas.SaveCount.ToString();
        values["DrawRect.NullPaint.Exception"] = ExceptionName(()=>canvas.DrawRect(SKRect.Create(1,1),null!));
        values["SetPixel.OutOfRange.Exception"] = ExceptionName(()=>bitmap.SetPixel(-1,0,SKColors.Red));
        values["Bitmap.NegativeSize.Exception"] = ExceptionName(()=>{ using var invalid=new SKBitmap(-1,2); });
        var disposableCanvas=new SKCanvas(bitmap); disposableCanvas.Dispose();
        values["Canvas.Disposed.Save.Exception"] = ExceptionName(()=>disposableCanvas.Save());
        using(var snapshotSurface=SKSurface.Create(new SKImageInfo(2,2))!)
        {
            snapshotSurface.Canvas.Clear(SKColors.Red); using var image=snapshotSurface.Snapshot(); snapshotSurface.Canvas.Clear(SKColors.Blue);
            using var captured=SKBitmap.FromImage(image); values["Surface.Snapshot.Pixel"] = captured.GetPixel(0,0).ToString();
        }
        var surface=SKSurface.Create(new SKImageInfo(2,2))!; var ownedCanvas=surface.Canvas; surface.Dispose();
        values["Surface.Disposed.Canvas.Save.Exception"] = ExceptionName(()=>ownedCanvas.Save());
        var ownedBitmap=new SKBitmap(2,2); using var retainingCanvas=new SKCanvas(ownedBitmap); ownedBitmap.Dispose();
        values["Bitmap.Disposed.Canvas.Save.Exception"] = ExceptionName(()=>retainingCanvas.Save());
        string fontPath=Environment.GetEnvironmentVariable("SIMDSKIA_CJK_FONT")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"simhei.ttf");
        using(var face=SKTypeface.FromFile(fontPath))using(var font=new SKFont(face,18){Hinting=SKFontHinting.None,Subpixel=true})
        {
            values["Font.Chinese.Glyph"] = font.GetGlyph('中').ToString(CultureInfo.InvariantCulture);
            values["Font.Latin.Glyph"] = font.GetGlyph('A').ToString(CultureInfo.InvariantCulture);
            values["Font.ChineseLatin.Advance"] = font.MeasureText("中文 A").ToString(CultureInfo.InvariantCulture);
            using var outline=font.GetGlyphPath(font.GetGlyph('中'));var bounds=outline.Bounds;
            values["Font.Chinese.Outline.Bounds"] = string.Join(",",new[]{bounds.Left,bounds.Top,bounds.Right,bounds.Bottom}.Select(v=>v.ToString(CultureInfo.InvariantCulture)));
        }
        File.WriteAllText(Path.Combine(directory,"behavior.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions { WriteIndented=true }));
    }
    private static string ExceptionName(Action action)
    {
        try { action(); return "none"; }
        catch(Exception e) { return e.GetType().FullName!; }
    }
}
