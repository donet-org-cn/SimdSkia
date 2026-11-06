using SkiaSharp;
int checks=0;
void Check(bool value){if(!value)throw new Exception("Native AOT geometry check failed: "+checks);checks++;}
Check(!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
using var path=new SKPath();path.LineTo(10,20);Check(path.PointCount==2);path.Close();path.RLineTo(3,7);Check(path.LastPoint==new SKPoint(3,7));
using var copy=new SKPath(path);var matrix=SKMatrix.CreateScale(2,3,1,1);path.Transform(in matrix,copy);Check(path.LastPoint!=copy.LastPoint);Check(copy.GetBounds(out var bounds)&&bounds==copy.Bounds);Check(copy.GetTightBounds(out _));
Span<SKPoint> points=[new(2,3),new(4,5)];matrix.MapPoints(points,points);Check(points[0]==matrix.MapPoint(2,3));
using var paint=new SKPaint{Color=SKColors.Red,StrokeWidth=3};using var clone=paint.Clone();paint.Reset();Check(clone.Color==SKColors.Red&&paint.Color==SKColors.Black);
using var bitmap=new SKBitmap(64,64);using var canvas=new SKCanvas(bitmap);
for(int i=0;i<100;i++){canvas.Clear(SKColors.Transparent);canvas.DrawLine(4,8,48,8,clone);canvas.DrawCircle(32,32,8,clone);}
Check(bitmap.GetPixel(20,8)==SKColors.Red);Check(bitmap.GetPixel(32,32)==SKColors.Red);
using var face1=SKTypeface.FromFile("baseline/fonts/ZCOOLXiaoWei-Regular.ttf");
using var face2=SKTypeface.FromFile("baseline/fonts/ZenOldMincho-Regular.ttf");
using var font=new SKFont(face1,24);float before=font.MeasureText("Hello");font.Typeface=face2;
Check(font.MeasureText("Hello")!=before);face2.Dispose();Check(font.Typeface.FamilyName=="Zen Old Mincho");
font.Typeface=null!;Check(font.MeasureText("Hello")>0);
Check(SKColor.Parse("#6495ed")==SKColors.CornflowerBlue);
Console.WriteLine("PASS "+checks+" actual Native AOT geometry checks");
