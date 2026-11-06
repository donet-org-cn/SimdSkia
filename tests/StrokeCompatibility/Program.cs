using SkiaSharp;
using System.Text.Json;
string output=args[0];Directory.CreateDirectory(output);var result=new SortedDictionary<string,object?>();
float[] R(SKRect r)=>[r.Left,r.Top,r.Right,r.Bottom];float[] P(SKPoint p)=>[p.X,p.Y];
void Query(string key,SKPath p)
{
    var rect=p.GetRect(out bool closed,out var direction);
    result[key]=new{p.IsLine,line=p.GetLine()?.Select(P).ToArray(),p.IsRect,rect=R(rect),closed=p.IsRect?closed:false,direction=p.IsRect?(int)direction:0,p.IsOval,oval=R(p.GetOvalBounds()),mask=(int)p.SegmentMasks};
}
using(var p=new SKPath())
{
    Query("empty",p);p.MoveTo(3,5);Query("move",p);p.LineTo(3,5);Query("zero-line",p);p.Close();Query("zero-closed",p);
    p.Reset();p.MoveTo(1,2);p.LineTo(9,8);Query("line",p);p.Close();Query("closed-line",p);
    p.Reset();p.AddOval(new(10,20,90,60));Query("oval",p);p.FillType=SKPathFillType.InverseEvenOdd;Query("inverse-oval",p);
    using var copy=new SKPath(p);Query("copied-oval",copy);copy.Offset(5,7);Query("offset-oval",copy);
    var m=SKMatrix.CreateScale(2,3);copy.Transform(in m);Query("scaled-oval",copy);m=SKMatrix.CreateSkew(.1f,0);copy.Transform(in m);Query("skewed-oval",copy);
    using var added=new SKPath();added.AddPath(p);Query("added-oval",added);
    p.LineTo(3,4);Query("mutated-oval",p);
    p.Reset();p.AddOval(new(10,20,10,20));Query("empty-oval",p);
}
for(int count=3;count<=5;count++)foreach(bool closed in new[]{false,true})foreach(var dir in Enum.GetValues<SKPathDirection>())
{
    SKPoint[] points=dir==SKPathDirection.Clockwise?[new(2,3),new(20,3),new(20,30),new(2,30),new(2,3)]:[new(2,3),new(2,30),new(20,30),new(20,3),new(2,3)];
    using var p=new SKPath();p.AddPoly(points[..count],closed);Query($"rect-{count}-{closed}-{dir}",p);
}
using(var p=new SKPath()){p.MoveTo(3,4);p.MoveTo(5,6);p.AddOval(new(10,20,90,60));Query("moves-then-oval",p);}
using(var p=new SKPath()){p.AddOval(new(10,20,90,60));using var q=new SKPath();q.AddPath(p,SKPathAddMode.Extend);Query("extend-oval",q);}
using(var p=new SKPath()){p.ConicTo(30,10,50,20,.7f);p.QuadTo(60,30,40,40);p.CubicTo(30,50,10,30,0,0);p.Close();Query("segment-kinds",p);}
int transformIndex=0;
foreach(var matrix in new[]{new SKMatrix(0,-1,0,1,0,0,0,0,1),SKMatrix.CreateScale(-2,3),SKMatrix.CreateRotationDegrees(37)})
{using var p=new SKPath();p.AddOval(new(2,3,20,30));p.Transform(in matrix);Query("oval-transform-"+transformIndex++,p);}
void Error(string key,Action action){try{action();result[key]="success";}catch(Exception e){result[key]=new{exception=e.GetType().Name,parameter=(e as ArgumentException)?.ParamName};}}
using(var p=new SKPath())using(var paint=new SKPaint())
{
    Error("null-src",()=>paint.GetFillPath(null!,p));Error("null-dst",()=>paint.GetFillPath(p,null!));
    foreach(float width in new[]{0,4})foreach(var style in new[]{SKPaintStyle.Fill,SKPaintStyle.Stroke})
    {
        p.Reset();p.MoveTo(10,20);p.LineTo(80,50);paint.Style=style;paint.StrokeWidth=width;
        using var q=new SKPath();result[$"fill-result-{style}-{width}"]=paint.GetFillPath(p,q);
        using var small=paint.GetFillPath(p,new SKRect(20,25,40,40),2);using var large=paint.GetFillPath(p,SKMatrix.CreateScale(2,3));
        result[$"fill-hint-{style}-{width}"]=new{small=small is null?null:R(small.TightBounds),large=large is null?null:R(large.TightBounds),fill=q.FillType.ToString(),hairline=R(q.TightBounds)};
    }
}
foreach(var cap in Enum.GetValues<SKStrokeCap>())foreach(var join in Enum.GetValues<SKStrokeJoin>())foreach(bool aa in new[]{false,true})
{
    using var p=new SKPath();p.MoveTo(13.25f,94.75f);p.LineTo(50.5f,18.25f);p.LineTo(100.75f,90.5f);
    void Draw(string key,bool outline)
    {
        using var b=new SKBitmap(128,128,SKColorType.Bgra8888,SKAlphaType.Premul);using var c=new SKCanvas(b);c.Clear(new SKColor(30,40,50));
        using var paint=new SKPaint{Color=new SKColor(20,150,220,160),IsAntialias=aa,Style=SKPaintStyle.Stroke,StrokeWidth=9,StrokeCap=cap,StrokeJoin=join};
        if(outline){using var q=paint.GetFillPath(p);paint.Style=SKPaintStyle.Fill;c.DrawPath(q,paint);}else c.DrawPath(p,paint);
        File.WriteAllBytes(Path.Combine(output,key+".bgra"),b.Bytes);
    }
    Draw($"stroke-{cap}-{join}-{aa}",false);Draw($"outline-{cap}-{join}-{aa}",true);
}
foreach(var cap in Enum.GetValues<SKStrokeCap>())foreach(bool close in new[]{false,true})
{
    using var p=new SKPath();p.MoveTo(50,50);p.LineTo(50,50);if(close)p.Close();using var paint=new SKPaint{Style=SKPaintStyle.Stroke,StrokeWidth=10,StrokeCap=cap};using var q=paint.GetFillPath(p);
    result[$"zero-stroke-{cap}-{close}"]=new{empty=q.IsEmpty,tight=R(q.TightBounds)};
}
foreach(var cap in Enum.GetValues<SKStrokeCap>())foreach(var join in Enum.GetValues<SKStrokeJoin>())
{
    using var b=new SKBitmap(128,128,SKColorType.Bgra8888,SKAlphaType.Premul);using var c=new SKCanvas(b);c.Clear(new SKColor(30,40,50));
    using var paint=new SKPaint{Color=new SKColor(20,150,220,160),Style=SKPaintStyle.Stroke,StrokeWidth=7,IsAntialias=true,StrokeCap=cap,StrokeJoin=join};
    c.DrawRect(new(20.25f,30.25f,100.75f,100.75f),paint);c.DrawLine(110.5f,15.5f,110.5f,15.5f,paint);
    File.WriteAllBytes(Path.Combine(output,$"closed-zero-{cap}-{join}.bgra"),b.Bytes);
}
foreach(float miter in new[]{0f,.5f,1,4})
{
    using var p=new SKPath();p.MoveTo(10,90);p.LineTo(50,10);p.LineTo(90,90);using var paint=new SKPaint{Style=SKPaintStyle.Stroke,StrokeWidth=6,StrokeMiter=miter};using var q=paint.GetFillPath(p);
    result["miter-"+miter]=new{paint.StrokeMiter,bounds=R(q.TightBounds)};
}
foreach(var style in Enum.GetValues<SKPaintStyle>())
{
    using var b=new SKBitmap(128,128,SKColorType.Bgra8888,SKAlphaType.Premul);using var c=new SKCanvas(b);c.Clear(SKColors.White);
    using var paint=new SKPaint{Style=style,StrokeCap=SKStrokeCap.Round,StrokeWidth=6,Color=SKColors.Red,IsAntialias=true};c.DrawLine(20.5f,40.5f,100.5f,80.5f,paint);
    File.WriteAllBytes(Path.Combine(output,"line-style-"+style+".bgra"),b.Bytes);
}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(output);
#if MANAGED
int exactChecks=0;var random=new Random(20261106);
for(int test=0;test<600;test++)
{
    int count=test%3==0?300:2+test%60;var points=new List<SimdSkia.PointF>();
    for(int i=0;i<count;i++){var v=new SimdSkia.PointF(random.Next(-256,257)/8f,random.Next(-256,257)/8f);points.Add(v);if(i%7==0)points.Add(v);}
    if(test%2==0)points.Add(points[0]);float width=.25f+test%13,miter=1+test%6;
    var contours=new[]{points.ToArray()};var expected=SimdSkia.StrokeReference.FrozenStrokeGeometry.Create(contours,width,miter);var actual=SimdSkia.StrokeGeometry.Create(contours,width,miter);
    if(!expected.Segments.SequenceEqual(actual.Segments))throw new Exception("Legacy stroke geometry changed at "+test);exactChecks++;
}
using(var p=new SKPath())using(var dst=new SKPath())using(var paint=new SKPaint{Style=SKPaintStyle.Stroke,StrokeWidth=5,StrokeCap=SKStrokeCap.Round})
{
    p.MoveTo(10,20);p.LineTo(80,50);paint.GetFillPath(p,dst);var expected=dst.TightBounds;dst.Reset();paint.GetFillPath(p,dst);
    if(dst.TightBounds!=expected)throw new Exception("Destination mutation leaked into paint cache");exactChecks++;
    paint.StrokeCap=SKStrokeCap.Square;paint.GetFillPath(p,dst);if(dst.TightBounds==expected)throw new Exception("Stroke cap cache key missing");exactChecks++;
    p.Offset(20,10);paint.GetFillPath(p,dst);if(dst.TightBounds.Left<=expected.Left)throw new Exception("Source mutation did not invalidate outline");exactChecks++;
    for(int i=0;i<50;i++){p.Offset(1,0);paint.GetFillPath(p,dst);}
    var field=typeof(SKPaint).GetField("strokeEntries",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
    var entries=(System.Collections.IDictionary)field.GetValue(paint)!;if(entries.Count>16)throw new Exception("Unbounded outline cache");exactChecks++;
    long bytes=(long)typeof(SKPaint).GetField("strokeBytes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(paint)!;
    if(bytes>1024*1024)throw new Exception("Outline byte budget exceeded");exactChecks++;
    paint.Reset();if(entries.Count!=0)throw new Exception("Reset retains outline cache entries");exactChecks++;
}
File.WriteAllText(Path.Combine(output,"managed-checks.json"),JsonSerializer.Serialize(new{exactChecks}));
#endif
