using SkiaSharp;
int checks=0;void Check(bool ok){if(!ok)throw new Exception("Stroke AOT check "+checks);checks++;}
Check(!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
using var path=new SKPath();path.AddOval(new(10,20,80,60));Check(path.IsOval&&path.SegmentMasks==SKPathSegmentMask.Conic);
using var copy=new SKPath(path);copy.Offset(3,4);Check(copy.IsOval&&copy.GetOvalBounds()==new SKRect(13,24,83,64));
path.Reset();path.AddRect(new(1,2,10,20));Check(path.IsRect&&path.GetRect(out bool closed,out var direction)==new SKRect(1,2,10,20)&&closed&&direction==SKPathDirection.Clockwise);
path.Reset();path.MoveTo(10,20);path.LineTo(80,50);Check(path.IsLine&&path.GetLine().Length==2);
using var dst=new SKPath();using var paint=new SKPaint{Style=SKPaintStyle.Stroke,StrokeWidth=7,IsAntialias=true,StrokeCap=SKStrokeCap.Round,StrokeJoin=SKStrokeJoin.Round,Color=SKColors.Red};
Check(paint.GetFillPath(path,dst));Check(dst.TightBounds.Left<10);
using var b=new SKBitmap(96,96);using var c=new SKCanvas(b);c.Clear(SKColors.White);c.DrawPath(path,paint);Check(b.GetPixel(45,35).Green<100);
paint.StrokeWidth=0;Check(!paint.GetFillPath(path,dst));Check(paint.GetFillPath(path) is null);
paint.Reset();Check(paint.GetFillPath(path,dst));
Console.WriteLine("PASS "+checks+" actual Native AOT stroke/query checks");
