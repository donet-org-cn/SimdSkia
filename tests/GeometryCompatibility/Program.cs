using SkiaSharp;
using System.Text.Json;
string output=args[0];Directory.CreateDirectory(output);var data=new SortedDictionary<string,object>();
float[] P(SKPoint p)=>[p.X,p.Y];float[] R(SKRect r)=>[r.Left,r.Top,r.Right,r.Bottom];
void Capture(string key,SKPath p)
{
    bool b=p.GetBounds(out var bounds),t=p.GetTightBounds(out var tight);
    var shortPoints=new SKPoint[2];int n=p.GetPoints(shortPoints,2);
    data[key]=new{empty=p.IsEmpty,bounds=R(bounds),boundsOk=b,tight=R(tight),tightOk=t,last=P(p.LastPoint),verbs=p.VerbCount,count=p.PointCount,points=p.Points.Select(P).ToArray(),shortCount=n,shortPoints=shortPoints.Select(P).ToArray()};
}
using(var p=new SKPath())
{
    Capture("empty",p);p.Close();Capture("close-empty",p);p.LineTo(10,20);Capture("implicit-move",p);
    p.Close();p.RLineTo(3,7);Capture("relative-after-close",p);p.Rewind();p.MoveTo(3,4);Capture("move",p);p.MoveTo(6,7);Capture("replaced-move",p);
    p.QuadTo(30,80,40,5);p.CubicTo(1,-20,80,70,3,4);p.Close();Capture("curve",p);
    foreach(int i in new[]{-1,0,9})Error("point-"+i,()=>data["point-value-"+i]=P(p.GetPoint(i)));
    using var copy=new SKPath(p);var m=SKMatrix.CreateScale(2,3,8,9);p.Transform(in m,copy);Capture("transform-copy",copy);Capture("transform-source",p);
    copy.Offset(new SKPoint(5,-7));Capture("offset",copy);p.RMoveTo(2,4);p.RQuadTo(1,2,3,4);p.RCubicTo(new(1,2),new(3,4),new(5,6));Capture("relative-curves",p);
}
void Error(string key,Action action){try{action();data[key]="success";}catch(Exception e){data[key]=e.GetType().Name;if(e is ArgumentException arg)data[key+"-parameter"]=arg.ParamName!;}}
using(var p=new SKPath())
{
    p.AddRect(new(1,2,20,30));Error("points-small",()=>p.GetPoints(new SKPoint[2],2));Error("points-negative",()=>p.GetPoints(-1));data["points-large"]=p.GetPoints(10).Select(P).ToArray();
    Error("null-destination",()=>{var m=SKMatrix.CreateTranslation(1,2);p.Transform(in m,null!);});
}
for(int mode=0;mode<2;mode++)
{
    using var p=new SKPath();p.MoveTo(2,3);p.LineTo(8,9);p.Close();
    using var q=new SKPath();q.MoveTo(20,30);q.QuadTo(40,80,60,10);q.Close();
    var tr=SKMatrix.CreateTranslation(7,-3);p.AddPath(q,in tr,(SKPathAddMode)mode);Capture("add-path-"+mode,p);
    p.AddPath(p,2,3);Capture("add-self-"+mode,p);
}
for(uint start=0;start<4;start++)for(int dir=0;dir<2;dir++)
{
    using var p=new SKPath();p.AddRect(new(2,3,20,30),(SKPathDirection)dir,start);Capture($"rect-{start}-{dir}",p);
}
using(var p=new SKPath()){p.AddPoly(new SKPoint[]{new(3,4),new(5,9),new(10,-3)},false);Capture("poly-open",p);}
using(var paint=new SKPaint{Color=SKColors.Red,StrokeWidth=7,IsAntialias=true,Style=SKPaintStyle.Stroke})
{
    using var clone=paint.Clone();paint.Reset();data["paint-reset"]=new{Color=new[]{paint.Color.Red,paint.Color.Green,paint.Color.Blue,paint.Color.Alpha},paint.StrokeWidth,paint.StrokeMiter,paint.IsStroke,paint.IsAntialias};
    data["paint-clone"]=new{Color=new[]{clone.Color.Red,clone.Color.Green,clone.Color.Blue,clone.Color.Alpha},clone.StrokeWidth,clone.StrokeMiter,clone.IsStroke,clone.IsAntialias};
}
using(var f1=SKTypeface.FromFile("baseline/fonts/ZCOOLXiaoWei-Regular.ttf"))
using(var f2=SKTypeface.FromFile("baseline/fonts/ZenOldMincho-Regular.ttf"))
using(var font=new SKFont(f1,24){Hinting=SKFontHinting.None,LinearMetrics=true,Subpixel=true,BaselineSnap=false})
{
    float before=font.MeasureText("Hello中文");font.Typeface=f2;float after=font.MeasureText("Hello中文");
    data["font-switch"]=new{before,after,family=font.Typeface.FamilyName};
    f2.Dispose();Error("retained-typeface",()=>data["retained-family"]=font.Typeface.FamilyName);Error("null-typeface",()=>font.Typeface=null!);
}
var a=SKMatrix.CreateScale(2,3);var bmat=SKMatrix.CreateTranslation(5,7);data["concat"]=SKMatrix.Concat(a,bmat).Values;data["pre"]=a.PreConcat(bmat).Values;data["post"]=a.PostConcat(bmat).Values;
var matrices=new[]{SKMatrix.Identity,SKMatrix.CreateSkew(.3f,-.2f),SKMatrix.CreateScale(-2,3,5,9),SKMatrix.CreateRotation(.7f,4,6),SKMatrix.CreateRotationDegrees(90,3,8),new SKMatrix(2,.5f,8,.25f,3,-2,.001f,.002f,1)};
for(int i=0;i<matrices.Length;i++)
{
    var m=matrices[i];SKPoint[] points=[new(2,3),new(-4,5),new(9,-7)];var mapped=m.MapPoints(points);var vectors=m.MapVectors(points);
    data["matrix-"+i]=new{values=m.Values,rect=R(m.MapRect(new(-1,-2,5,7))),points=mapped.Select(P).ToArray(),vectors=vectors.Select(P).ToArray(),radius=m.MapRadius(5),invertible=m.IsInvertible};
    m.MapPoints(points.AsSpan(),points);data["in-place-"+i]=points.Select(P).ToArray();
}
Error("matrix-length",()=>SKMatrix.Identity.MapPoints(new SKPoint[1],new SKPoint[2]));
Error("matrix-values-null",()=>{var m=SKMatrix.Identity;m.Values=null!;});
Error("matrix-values-length",()=>{var m=SKMatrix.Identity;m.Values=new float[8];});
var random=new Random(20261106);
for(int i=0;i<200;i++)
{
    float F()=>random.Next(-256,257)/16f;
    var m=new SKMatrix(F(),F(),F(),F(),F(),F(),0,0,1);
    SKPoint[] ps=[new(F(),F()),new(F(),F()),new(F(),F())];
    data["random-matrix-"+i]=new{points=m.MapPoints(ps).Select(P).ToArray(),vectors=m.MapVectors(ps).Select(P).ToArray(),rect=R(m.MapRect(new(-5,-3,7,9))),radius=m.MapRadius(3)};
}
for(int style=0;style<3;style++){using var p=new SKPaint{Style=(SKPaintStyle)style};data["is-stroke-"+style]=p.IsStroke;}
using(var p=new SKPath())
{
    p.MoveTo(1,2);p.QuadTo(4,20,10,2);var bounds=p.TightBounds;p.LineTo(40,-30);Capture("invalidate",p);
    Error("rect-start-range",()=>p.AddRect(new(0,0,3,4),SKPathDirection.Clockwise,4));
}
void Scene(string name,Action<SKCanvas,SKPaint> draw)
{
    using var b=new SKBitmap(128,128,SKColorType.Bgra8888,SKAlphaType.Premul);using var c=new SKCanvas(b);c.Clear(SKColors.Transparent);using var p=new SKPaint{Color=new SKColor(48,96,192,180),IsAntialias=true};draw(c,p);if(!b.Bytes.Any(v=>v!=0))throw new Exception("Scene rendered blank: "+name);File.WriteAllBytes(Path.Combine(output,name+".bgra"),b.Bytes);
}
Scene("transformed-fill",(c,p)=>{p.IsAntialias=false;c.Scale(2,2,20,20);c.RotateDegrees(90,32,32);c.Skew(.25f,0);c.DrawRect(new SKRect(10,12,30,36),p);});
Scene("path-fill",(c,p)=>{p.IsAntialias=false;using var q=new SKPath();q.AddPoly(new SKPoint[]{new(10,10),new(35,10),new(35,35),new(10,35)});q.Offset(4,7);var m=SKMatrix.CreateScale(2,2);q.Transform(in m);c.DrawPath(q,p);});
Scene("layer-bounds-hint",(c,p)=>{using var lp=new SKPaint{Color=new SKColor(255,255,255,128)};c.SaveLayer(new SKRect(20,20,30,30),lp);c.DrawRect(new SKRect(2,2,15,15),p);c.DrawRect(new SKRect(22,22,26,26),p);c.Restore();});
Scene("clone-gradient",(c,p)=>{using var sh=SKShader.CreateLinearGradient(new SKPoint(0,0),new SKPoint(128,0),new[]{SKColors.Red,SKColors.Blue},null,SKShaderTileMode.Clamp);p.Shader=sh;sh.Dispose();using var q=p.Clone();p.Reset();c.DrawRect(new SKRect(0,0,128,128),q);});
#if MANAGED
void Check(bool value,string name){if(!value)throw new Exception(name);}
{
    var contours=new SimdSkia.PointF[12][];
    for(int i=0;i<contours.Length;i++){float x=i*64;contours[i]=[new(x,0),new(x+16,0),new(x+16,128),new(x,128),new(x,0)];}
    var mask=SimdSkia.Cpu.PathRasterizer.Rasterize(contours,SimdSkia.FillRule.Winding,1024,128,true,compactRuns:true);
    var dense=SimdSkia.Cpu.PathRasterizer.Coverage(contours,SimdSkia.FillRule.Winding,1024,128,true);
    var restored=new byte[dense.Length];foreach(var run in mask.Runs)restored.AsSpan(run.Y*1024+run.X,run.Length).Fill(run.Coverage);
    Check(restored.AsSpan().SequenceEqual(dense),"Compact coverage must reconstruct every byte exactly");
    Check(mask.Data.Length==0&&mask.Runs.Length*16<mask.Width*mask.Height,"Do not retain duplicate coverage grids");
    File.WriteAllText(Path.Combine(output,"mask-storage.json"),JsonSerializer.Serialize(new{denseGridBytes=mask.Width*mask.Height,retainedBytes=mask.Data.Length+mask.Runs.Length*16,exact=true}));
}
using(var p=new SKPath())
{
    p.AddRect(new(0,0,10,10));var old=p.Points;var oldBounds=p.TightBounds;
    var bad=new SKMatrix(float.NaN,0,0,0,1,0,0,0,1);bool threw=false;try{p.Transform(in bad);}catch(ArgumentException){threw=true;}Check(threw&&p.Points.SequenceEqual(old)&&p.TightBounds==oldBounds,"Atomic invalid transform");
    p.Dispose();bool disposed=false;try{_ = p.PointCount;}catch(ObjectDisposedException){disposed=true;}Check(disposed,"Disposed point query");
}
using(var b=new SKBitmap(64,64))using(var c=new SKCanvas(b))using(var p=new SKPaint{StrokeWidth=2,Color=SKColors.Red})
{
    c.DrawLine(4,8,48,8,p);p.Color=SKColors.Blue;c.DrawLine(4,8,48,8,p);Check(b.GetPixel(20,8)==SKColors.Blue,"Line cache must read paint changes");
    for(int i=0;i<90;i++)c.DrawLine(i,0,i,20,p);
    c.ResetMatrix();c.Translate(0,10);c.DrawLine(4,8,48,8,p);Check(b.GetPixel(20,18)==SKColors.Blue,"Stroke cache must include transform");
}
#endif
var named=typeof(SKColors).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Where(f=>f.FieldType==typeof(SKColor)).ToDictionary(f=>f.Name,f=>(uint)(SKColor)f.GetValue(null)!);
File.WriteAllText(Path.Combine(output,"named-colors.json"),JsonSerializer.Serialize(named));
foreach(string? hex in new string?[]{null,"","red","#f00","#abcd","ff0000","#ff0000","#80ff0000","FFFFFFFF"," 112233 ","#11223344","123456789","#gg0000"})
{
    bool ok=SKColor.TryParse(hex!,out var color);data["parse-"+(hex??"null")]=new{ok,color=(uint)color};Error("parse-error-"+(hex??"null"),()=>SKColor.Parse(hex!));
}
using(var circle=new SKPath()){circle.AddCircle(20,30,12);File.WriteAllText(Path.Combine(output,"circle-representation.json"),JsonSerializer.Serialize(new{points=circle.PointCount,verbs=circle.VerbCount}));}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("Recorded "+data.Count+" geometry/behavior cases");
