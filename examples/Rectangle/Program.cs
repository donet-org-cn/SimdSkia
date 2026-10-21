using SkiaSharp;
using System.Text;

using var bitmap=new SKBitmap(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul));
using var canvas=new SKCanvas(bitmap);
canvas.Clear(SKColors.White);
using var paint=new SKPaint { Color=SKColors.Red };
canvas.DrawRect(SKRect.Create(8,10,24,16),paint);
for (int y=0;y<bitmap.Height;y++)
    for (int x=0;x<bitmap.Width;x++)
    {
        SKColor expected=x>=8 && x<32 && y>=10 && y<26 ? SKColors.Red : SKColors.White;
        if (bitmap.GetPixel(x,y)!=expected) throw new InvalidOperationException($"Pixel ({x},{y}) failed.");
    }
string path=Path.GetFullPath(args.Length>0 ? args[0] : "artifacts/rectangle.ppm");
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
using var output=File.Create(path);
output.Write(Encoding.ASCII.GetBytes($"P6\n{bitmap.Width} {bitmap.Height}\n255\n"));
for (int y=0;y<bitmap.Height;y++) for (int x=0;x<bitmap.Width;x++)
{
    var color=bitmap.GetPixel(x,y); output.WriteByte(color.Red); output.WriteByte(color.Green); output.WriteByte(color.Blue);
}
Console.WriteLine($"PASS: {bitmap.Width*bitmap.Height} pixels verified; PPM saved to {path}");
