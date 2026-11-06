using SkiaSharp;
using SimdSkia.Pdf;
string output=args.Length>0?args[0]:"artifacts/examples/compatibility99";Directory.CreateDirectory(output);
using var recorder=new SKPictureRecorder();var canvas=recorder.BeginRecording(new(0,0,640,360));canvas.Clear(new SKColor(20,25,40));
using var background=SKRuntimeEffect.BuildShader("uniform float2 size;half4 main(float2 p){float2 uv=p/size;return half4(.06+uv.x*.08,.08+uv.y*.12,.17+uv.x*.1,1);}");background.Uniforms["size"]=new SKPoint(640,360);using var shader=background.Build();using var paint=new SKPaint{Shader=shader};canvas.DrawPaint(paint);
using(var noise=SKShader.CreatePerlinNoiseFractalNoise(.035f,.04f,3,11)){paint.Shader=noise;paint.Color=SKColors.White.WithAlpha(100);canvas.DrawRect(340,75,265,200,paint);}
paint.Shader=null;paint.Color=SKColors.White;
SKPoint[] patch=[new(35,90),new(100,45),new(180,130),new(280,85),new(320,140),new(245,215),new(285,275),new(190,310),new(110,235),new(40,280),new(10,220),new(75,145)];canvas.DrawPatch(patch,[new(240,70,130),new(70,200,250),new(90,90,240),new(240,180,70)],null,paint);
string fontPath=args.Length>1?args[1]:"baseline/fonts/ZenOldMincho-Regular.ttf";using var typeface=SKTypeface.FromFile(fontPath);using var font=new SKFont(typeface,25);canvas.DrawText("SimdSkia · 中文与曲面",26,40,font,paint);using var link=canvas.DrawUrlAnnotation(new(24,310,610,350),"https://github.com/donet-org-cn/SimdSkia");using var picture=recorder.EndRecording();
using(var bitmap=new SKBitmap(640,360)){using(var raster=new SKCanvas(bitmap))raster.DrawPicture(picture);using var png=bitmap.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(Path.Combine(output,"scene.png"));png.SaveTo(file);}
using(var document=SKDocument.CreatePdf(Path.Combine(output,"scene.pdf"))){document.BeginPage(640,360).DrawPicture(picture);document.EndPage();document.Close();}
using(var pdf=PdfReader.Open(Path.Combine(output,"scene.pdf")))using(var page=pdf.RenderPage(1)){using var png=page.Bitmap.Encode(SKEncodedImageFormat.Png,100);using var file=File.Create(Path.Combine(output,"pdf-page.png"));png.SaveTo(file);}
Console.WriteLine($"Wrote PNG, annotated PDF and rendered PDF page to {Path.GetFullPath(output)}");

// Keep a vector-only picture for the supported native SKP subset. The richer
// runtime effect above is deliberately not flattened into a fake SKP bitmap.
using var vectorRecorder=new SKPictureRecorder();
DrawVector(vectorRecorder.BeginRecording(new(0,0,240,140)));
using var vector=vectorRecorder.EndRecording();
using(var data=vector.Serialize())File.WriteAllBytes(Path.Combine(output,"vector.skp"),data.ToArray());
using var stream=File.OpenRead(Path.Combine(output,"vector.skp"));
using var restored=SKPicture.Deserialize(stream)??throw new Exception("SKP roundtrip failed.");
using(var image=SKImage.FromPicture(restored,new SKSizeI(240,140)))
using(var png=image.Encode(SKEncodedImageFormat.Png,100))File.WriteAllBytes(Path.Combine(output,"lazy-picture.png"),png.ToArray());
using(var svg=File.Create(Path.Combine(output,"vector.svg")))
using(var svgCanvas=SKSvgCanvas.Create(new(0,0,240,140),svg))DrawVector(svgCanvas);
using(var xps=SKDocument.CreateXps(Path.Combine(output,"vector.xps")))
{DrawVector(xps.BeginPage(240,140));xps.EndPage();xps.Close();}
using(var rgba=new SKBitmap(new SKImageInfo(240,140,SKColorType.Rgba8888,SKAlphaType.Premul)))
{
 using(var raster=new SKCanvas(rgba))using(var floatPaint=new SKPaint{ColorF=new(.17f,.57f,.91f,.65f),IsDither=true})raster.DrawRect(10,10,220,120,floatPaint);
 using var alpha=new SKBitmap();
 if(!rgba.ExtractAlpha(alpha)||alpha.ColorType!=SKColorType.Alpha8||alpha.GetPixel(40,40).Alpha is <160 or >170)throw new Exception("Real Alpha8 extraction failed.");
 using var pixmap=rgba.PeekPixels();using var webp=pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless,75));
 File.WriteAllBytes(Path.Combine(output,"float-color.webp"),webp.ToArray());
 Console.WriteLine($"Alpha8: {alpha.ByteCount} bytes (RGBA: {rgba.ByteCount}); center alpha {alpha.GetPixel(40,40).Alpha}");
}
Console.WriteLine("Also wrote real vector SVG/XPS/SKP, lazy-picture PNG and lossless WebP.");

static void DrawVector(SKCanvas canvas)
{
 canvas.Clear(SKColors.White);using var p=new SKPaint{Color=new(35,100,210,180),IsAntialias=true};
 canvas.DrawRoundRect(new SKRect(12,12,228,128),18,18,p);p.Color=new(230,70,100,210);
 using var path=new SKPath();path.MoveTo(25,110);path.CubicTo(40,20,170,15,210,95);path.LineTo(25,110);path.Close();canvas.DrawPath(path,p);
}
