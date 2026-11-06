using SkiaSharp;
using System.Runtime.InteropServices;
int checks=0;void Check(bool ok){if(!ok)throw new Exception("Conic AOT check "+checks);checks++;}
Check(!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
Check(Marshal.SizeOf<SimdSkia.PathSegment>()==28);
using var p=new SKPath();p.AddCircle(32,32,20);Check(p.PointCount==9&&p.VerbCount==6);
Check(p.Bounds==new SKRect(12,12,52,52));
var m=SKMatrix.CreateScale(2,3);p.Transform(in m);Check(p.TightBounds==new SKRect(24,36,104,156));
p.Reset();p.ConicTo(20,-40,50,20,.70710677f);Check(p.PointCount==3&&p.TightBounds.Top<0);
using var b=new SKBitmap(96,96);using var c=new SKCanvas(b);using var paint=new SKPaint{Color=SKColors.Red,IsAntialias=true};
c.Clear(SKColors.White);c.DrawOval(48,48,30,20,paint);Check(b.GetPixel(48,48)==SKColors.Red);Check(b.GetPixel(3,3)==SKColors.White);
c.Clear(SKColors.White);c.DrawArc(new(10,10,80,80),0,90,true,paint);Check(b.GetPixel(55,55)==SKColors.Red);
p.Reset();p.MoveTo(10,20);p.ArcTo(new SKPoint(60,20),new SKPoint(80,90),5);Check(p.PointCount==4);
Console.WriteLine("PASS "+checks+" actual Native AOT conic checks");
