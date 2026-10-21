using SimdSkia;
using SimdSkia.Cpu;
using SkiaSharp;

bool gpu=args.Contains("--gpu");string? fontPath=args.FirstOrDefault(a=>a.EndsWith(".ttf",StringComparison.OrdinalIgnoreCase))??Environment.GetEnvironmentVariable("SIMDSKIA_CJK_FONT");
if(fontPath is null&&OperatingSystem.IsWindows())fontPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"simhei.ttf");
if(fontPath is null||!File.Exists(fontPath))throw new FileNotFoundException("Supply a Chinese glyf TrueType font path or SIMDSKIA_CJK_FONT. No substitute glyph data is used.");
Directory.CreateDirectory("artifacts");using var face=SKTypeface.FromFile(fontPath);using var font=new SKFont(face,24);
using var bitmap=new SKBitmap(new SKImageInfo(320,200,SKColorType.Rgba8888,SKAlphaType.Premul));using var cpu=new SKCanvas(bitmap);using var surface=gpu?VulkanSurface.Create(320,200):null;
var canvas=surface?.Canvas??cpu;canvas.Clear(SKColors.White);
using var paint=new SKPaint {IsAntialias=true};using var gradient=SKShader.CreateLinearGradient(new(0,0),new(320,0),[new SKColor(30,70,190),new SKColor(20,180,160)],SKShaderTileMode.Clamp);
paint.Shader=gradient;canvas.DrawRect(new SKRect(0,0,320,52),paint);paint.Shader=null;paint.Color=SKColors.White;canvas.DrawText("SimdSkia 中文",12,36,font,paint);
using var path=new SKPath {FillType=SKPathFillType.EvenOdd};path.MoveTo(12,112);path.QuadTo(92,48,172,112);path.CubicTo(160,190,24,184,12,112);path.Close();path.AddCircle(92,126,20);
paint.Color=new SKColor(40,120,200,180);canvas.DrawPath(path,paint);
paint.Color=new SKColor(220,80,50);paint.Style=SKPaintStyle.Stroke;paint.StrokeWidth=3;canvas.DrawCircle(230,108,30,paint);paint.Style=SKPaintStyle.Fill;
using var icon=new SKBitmap(new SKImageInfo(12,12,SKColorType.Rgba8888,SKAlphaType.Premul));icon.Erase(new SKColor(220,170,30));icon.SetPixel(0,0,SKColors.Blue);
using var encodedIcon=icon.Encode(SKEncodedImageFormat.Png,100);using var decodedIcon=SKBitmap.Decode(encodedIcon.ToArray());
paint.Color=SKColors.White;int saved=canvas.Save();canvas.ClipRect(new SKRect(212,144,272,184));canvas.DrawBitmap(decodedIcon,new SKRect(204,136,284,192),paint);canvas.RestoreToCount(saved);
string output=$"artifacts/features-{(gpu?"vulkan":"cpu")}.png";
if(surface is not null){using var image=surface.Snapshot();using var data=image.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(output);data.SaveTo(file);Console.WriteLine($"Vulkan={surface.DeviceName}, software={surface.IsSoftwareDevice}, completedBatches={surface.CompletedBatches}");}
else{using var data=bitmap.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(output);data.SaveTo(file);Console.WriteLine("CPU clear="+CpuOptions.ClearImplementation);}
Console.WriteLine("Saved "+Path.GetFullPath(output));
