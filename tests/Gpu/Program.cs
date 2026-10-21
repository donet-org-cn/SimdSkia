using SimdSkia;
using SkiaSharp;
using System.Text.Json;

Directory.CreateDirectory("artifacts");
using var surface=VulkanSurface.Create(64,48);Console.WriteLine($"Vulkan device: {surface.DeviceName}; software={surface.IsSoftwareDevice}");
using var bitmap=new SKBitmap(new SKImageInfo(64,48,SKColorType.Rgba8888,SKAlphaType.Premul));using var cpu=new SKCanvas(bitmap);
using var paint=new SKPaint {Color=SKColors.Red};
foreach(var canvas in new[]{cpu,surface.Canvas}){canvas.Clear(SKColors.White);canvas.DrawRect(SKRect.Create(8,10,24,16),paint);}
using(var image=surface.Snapshot())using(var actual=SKBitmap.FromImage(image))Compare(bitmap,actual,0,"rectangle");
using var path=new SKPath();path.MoveTo(3,24);path.QuadTo(32,1,60,24);path.CubicTo(55,44,8,44,3,24);path.Close();
paint.IsAntialias=true;paint.Color=new SKColor(20,120,220,160);
foreach(var canvas in new[]{cpu,surface.Canvas}){canvas.Clear(SKColors.White);canvas.DrawPath(path,paint);}
using(var image=surface.Snapshot())using(var actual=SKBitmap.FromImage(image))Compare(bitmap,actual,2,"path-AA");
using var source=new SKBitmap(2,2);source.Erase(SKColors.Blue);paint.IsAntialias=false;paint.Color=SKColors.White;
foreach(var canvas in new[]{cpu,surface.Canvas}){canvas.Clear(SKColors.White);int saved=canvas.Save();canvas.ClipRect(SKRect.Create(12,12,20,20));canvas.DrawBitmap(source,SKRect.Create(8,8,36,28),paint);canvas.RestoreToCount(saved);}
source.Dispose();
using(var image=surface.Snapshot())using(var actual=SKBitmap.FromImage(image))Compare(bitmap,actual,0,"texture-cache-clip-source-disposed");
using(var shader=SKShader.CreateLinearGradient(new(0,0),new(64,0),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp))
{
    paint.Shader=shader;paint.Color=SKColors.White.WithAlpha(128);foreach(var canvas in new[]{cpu,surface.Canvas}){canvas.Clear(SKColors.White);canvas.DrawRect(SKRect.Create(64,48),paint);}
    using(var image=surface.Snapshot())using(var actual=SKBitmap.FromImage(image))Compare(bitmap,actual,0,"gradient");paint.Shader=null;
}
paint.Color=SKColors.Red;paint.Style=SKPaintStyle.Stroke;paint.StrokeWidth=4;paint.IsAntialias=true;
foreach(var canvas in new[]{cpu,surface.Canvas}){canvas.Clear(SKColors.White);canvas.DrawRect(SKRect.Create(8,10,24,16),paint);}
using(var image=surface.Snapshot())using(var actual=SKBitmap.FromImage(image))Compare(bitmap,actual,0,"miter-stroke-AA");
paint.Style=SKPaintStyle.Fill;paint.IsAntialias=false;
using(var changing=new SKBitmap(2,2))
{
    changing.Erase(SKColors.Red);surface.Canvas.Clear(SKColors.White);surface.Canvas.DrawBitmap(changing,SKRect.Create(0,0,16,16));
    changing.Erase(SKColors.Blue);surface.Canvas.DrawBitmap(changing,SKRect.Create(16,0,16,16));
    using var image=surface.Snapshot();using var actual=SKBitmap.FromImage(image);
    if(actual.GetPixel(4,4)!=SKColors.Red||actual.GetPixel(20,4)!=SKColors.Blue)throw new Exception("Texture cache did not capture the two source versions.");Console.WriteLine("PASS texture content invalidation and queued snapshots");
}
bool chineseVerified=false;string fontPath=Environment.GetEnvironmentVariable("SIMDSKIA_CJK_FONT")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"simhei.ttf");
if(File.Exists(fontPath))
{
    using var face=SKTypeface.FromFile(fontPath);using var font=new SKFont(face,18);paint.Color=SKColors.Black;
    foreach(var canvas in new[]{cpu,surface.Canvas}){canvas.Clear(SKColors.White);canvas.DrawText("中文 A",2,32,font,paint);}
    using var image=surface.Snapshot();using var actual=SKBitmap.FromImage(image);Compare(bitmap,actual,2,"Chinese/Latin glyph paths");chineseVerified=true;
}
paint.Color=SKColors.Red;
foreach(var canvas in new[]{cpu,surface.Canvas}){canvas.Clear(SKColors.White);for(int i=0;i<130;i++)canvas.DrawRect(SKRect.Create(i%60,i%44,4,4),paint);}
using(var image=surface.Snapshot())using(var actual=SKBitmap.FromImage(image))Compare(bitmap,actual,0,"automatic submission across 128-command batch boundary");
for(int i=0;i<SimdSkia.Gpu.VulkanCanvas.MaximumCachedTextures+4;i++){using var tile=new SKBitmap(1,1);tile.Erase(new SKColor((byte)i,12,20));surface.Canvas.DrawBitmap(tile,i%60,(i/60)%40);}
surface.Flush();if(surface.CachedTextures>SimdSkia.Gpu.VulkanCanvas.MaximumCachedTextures)throw new Exception("Texture cache exceeded its post-fence eviction limit.");Console.WriteLine("PASS completed-frame texture eviction");
bool threadRejected=false;var thread=new Thread(()=>{try{surface.Flush();}catch(InvalidOperationException){threadRejected=true;}});thread.Start();thread.Join();if(!threadRejected)throw new Exception("Cross-thread queue access was not rejected.");
using(var image=surface.Snapshot())using(var data=image.Encode(SKEncodedImageFormat.Png,100))using(var file=File.Create("artifacts/vulkan.png"))data.SaveTo(file);
var report=new {Backend="Vulkan compute (device coverage and composition)",surface.DeviceName,surface.IsSoftwareDevice,surface.CompletedBatches,surface.CachedTextures,surface.LastSubmitMilliseconds,surface.LastCompletionMilliseconds,ThreadConstraintVerified=threadRejected,ChineseVerified=chineseVerified,TextureVkImageVerified=true};
File.WriteAllText("artifacts/vulkan.json",JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));
surface.Dispose();try{surface.Flush();throw new Exception("Disposed surface accepted Flush.");}catch(ObjectDisposedException){ }
static void Compare(SKBitmap expected,SKBitmap actual,int tolerance,string name)
{
    int max=0;for(int y=0;y<expected.Height;y++)for(int x=0;x<expected.Width;x++)
    {var a=expected.GetPixel(x,y);var b=actual.GetPixel(x,y);max=Math.Max(max,new[]{Math.Abs(a.Red-b.Red),Math.Abs(a.Green-b.Green),Math.Abs(a.Blue-b.Blue),Math.Abs(a.Alpha-b.Alpha)}.Max());}
    if(max>tolerance)throw new Exception($"{name}: max error {max}, tolerance {tolerance}.");Console.WriteLine($"PASS {name}: maximum channel error {max}");
}
