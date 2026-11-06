using SkiaSharp;
int checks=0;void Check(bool ok){if(!ok)throw new Exception("Pixel AOT check "+checks);checks++;}
Check(!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
using var source=new SKBitmap(new SKImageInfo(16,8,SKColorType.Bgra8888,SKAlphaType.Premul),80);source.Erase(SKColors.CornflowerBlue);
using var map=source.PeekPixels();Check(map.RowBytes==80&&map.GetPixelColor(5,2)==SKColors.CornflowerBlue);
using var subset=map.ExtractSubset(new(2,1,12,7));Check(subset.Width==10&&subset.Height==6);
using var copy=source.Copy(SKColorType.Rgba8888);Check(copy.GetPixel(5,2)==SKColors.CornflowerBlue);
using var resized=copy.Resize(new SKSizeI(31,17),new SKSamplingOptions(SKFilterMode.Linear));Check(resized.GetPixel(15,8)==SKColors.CornflowerBlue);
using var view=resized.PeekPixels();Check(view.GetPixelSpan<uint>().Length==31*17);
using var image=SKImage.FromBitmap(resized);Check(image.ReadPixels(view,0,0,SKImageCachingHint.Disallow));
var color=SKColorF.FromHsv(200,50,70,.5f);Check(((SKColor)color).Alpha==128);
using var canvas=new SKCanvas(copy);using(var restore=new SKAutoCanvasRestore(canvas)){canvas.Save();}Check(canvas.SaveCount==1);
source.Reset();Check(map.GetPixelColor(5,2)==SKColors.CornflowerBlue);
Console.WriteLine("PASS "+checks+" actual Native AOT pixel/color checks");
