using SkiaSharp;

Directory.CreateDirectory("artifacts");
using var source=new SKBitmap(new SKImageInfo(240,140,SKColorType.Rgba8888,SKAlphaType.Unpremul));
using var pixels=source.PeekPixels();
var span=pixels.GetPixelSpan();
for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)
{int i=y*pixels.RowBytes+x*4;span[i]=(byte)x;span[i+1]=(byte)(y*255/source.Height);span[i+2]=150;span[i+3]=255;}
using var subset=pixels.ExtractSubset(new(20,20,220,120));
using var resized=new SKBitmap(new SKImageInfo(600,300,SKColorType.Bgra8888,SKAlphaType.Premul));
using var destination=resized.PeekPixels();
if(!subset.ScalePixels(destination,new SKSamplingOptions(SKCubicResampler.Mitchell)))throw new Exception("Scale failed.");
using var surface=SKSurface.Create(new SKImageInfo(640,340))!;
using(var restore=new SKAutoCanvasRestore(surface.Canvas))
{surface.Canvas.Clear(SKColors.White);using var image=SKImage.FromBitmap(resized);surface.Canvas.DrawImage(image,20,20);}
using var snapshot=surface.PeekPixels();using var data=snapshot.Encode(SKEncodedImageFormat.Png,100);
using var output=File.Create("artifacts/pixels.png");data.SaveTo(output);
if(snapshot.GetPixelColor(320,170).Alpha!=255)throw new Exception("Unexpected alpha.");
Console.WriteLine("Wrote artifacts/pixels.png: borrowed subset -> resize -> surface -> PNG.");
