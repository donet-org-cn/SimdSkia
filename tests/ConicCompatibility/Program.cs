using SkiaSharp;
using System.Text.Json;
string output=args[0];Directory.CreateDirectory(output);var data=new SortedDictionary<string,object>();
float[] Point(SKPoint p)=>[p.X,p.Y];float[] Rect(SKRect r)=>[r.Left,r.Top,r.Right,r.Bottom];
void Capture(string key,SKPath p)=>data[key]=new{count=p.PointCount,verbs=p.VerbCount,points=p.Points.Select(Point).ToArray(),bounds=Rect(p.Bounds),tight=Rect(p.TightBounds),last=Point(p.LastPoint)};
foreach(float w in new[]{-1f,0,.1f,.70710677f,1,2,100,float.PositiveInfinity})
{
    using var p=new SKPath();p.ConicTo(new(50,-30),new(100,60),w);Capture("weight-"+w,p);
    p.Close();p.RConicTo(20,20,40,0,w);Capture("relative-"+w,p);
}
foreach(var dir in Enum.GetValues<SKPathDirection>())
{
    foreach(var r in new[]{new SKRect(10,20,110,90),new SKRect(20,20,20,20),new SKRect(100,90,10,20)})
    {using var p=new SKPath();p.AddOval(r,dir);Capture($"oval-{dir}-{r.Left}",p);}
    foreach(float radius in new[]{-1f,0,10}){using var p=new SKPath();p.AddCircle(40,50,radius,dir);Capture("circle-"+dir+radius,p);}
}
foreach(var angles in new[]{(0f,90f),(30f,150f),(35f,-180f),(0f,360f),(80f,400f),(90f,0f)})
foreach(bool force in new[]{false,true})
{
    using var p=new SKPath();p.MoveTo(3,7);p.ArcTo(new(10,20,110,90),angles.Item1,angles.Item2,force);Capture($"arc-{angles}-{force}",p);
    using var q=new SKPath();q.AddArc(new(10,20,110,90),angles.Item1,angles.Item2);Capture($"add-arc-{angles}",q);
}
foreach(float radius in new[]{-1f,0,5,50})
{using var p=new SKPath();p.MoveTo(10,20);p.ArcTo(new SKPoint(60,20),new SKPoint(80,90),radius);Capture("tangent-"+radius,p);}
var random=new Random(20261106);
for(int i=0;i<160;i++)
{
    float N()=>random.Next(-200,201)/4f;
    using var p=new SKPath();p.MoveTo(N(),N());p.ConicTo(N(),N(),N(),N(),.1f+(float)random.NextDouble()*5);Capture("random-"+i,p);
    var m=SKMatrix.CreateRotationDegrees(i*3.7f);p.Transform(in m);p.Offset(3.25f,-2.75f);Capture("transformed-"+i,p);
}
void Scene(string key,Action<SKCanvas,SKPaint> draw,bool aa=true,bool stroke=false,SKMatrix? transform=null)
{
    using var b=new SKBitmap(128,128,SKColorType.Bgra8888,SKAlphaType.Premul);using var c=new SKCanvas(b);c.Clear(new SKColor(30,40,50));
    using var p=new SKPaint{Color=new SKColor(30,145,230,192),IsAntialias=aa,Style=stroke?SKPaintStyle.Stroke:SKPaintStyle.Fill,StrokeWidth=4};
    if(transform is {} m)c.SetMatrix(m);draw(c,p);File.WriteAllBytes(Path.Combine(output,key+".bgra"),b.Bytes);
}
foreach(bool aa in new[]{false,true})foreach(bool stroke in new[]{false,true})
{
    Scene($"oval-{aa}-{stroke}",(c,p)=>c.DrawOval(new(12.25f,25.5f,112.75f,101.25f),p),aa,stroke);
    Scene($"oval-thin-{aa}-{stroke}",(c,p)=>c.DrawOval(64,64,54,9,p),aa,stroke);
    Scene($"circle-{aa}-{stroke}",(c,p)=>c.DrawCircle(63.5f,64.25f,47.5f,p),aa,stroke);
    Scene($"arc-{aa}-{stroke}",(c,p)=>c.DrawArc(new(12.25f,25.5f,112.75f,101.25f),30,150,true,p),aa,stroke);
}
Scene("oval-transformed",(c,p)=>c.DrawOval(new SKPoint(52,45),new SKSize(42,28),p),transform:SKMatrix.CreateRotationDegrees(23,64,64));
foreach(float w in new[]{.1f,.70710677f,1,2,100})
{
    Scene("conic-"+w,(c,p)=>{using var path=new SKPath();path.MoveTo(10,100);path.ConicTo(30,-70,115,80,w);path.LineTo(90,115);path.Close();c.DrawPath(path,p);});
}
foreach(var mode in new[]{SKBlendMode.Src,SKBlendMode.SrcOver,SKBlendMode.Clear})
Scene("draw-color-"+mode,(c,p)=>{c.Translate(30,10);c.ClipRect(new(3,4,65,90));c.DrawColor(new SKColor(20,110,220,160),mode);});
Scene("draw-paint-style",(c,p)=>{p.Style=SKPaintStyle.StrokeAndFill;p.StrokeCap=SKStrokeCap.Round;c.DrawPaint(p);});
Scene("draw-paint-shader",(c,p)=>{using var s=SKShader.CreateLinearGradient(new(0,0),new(100,20),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);p.Shader=s;c.Translate(20,5);c.ClipRect(new(10,10,95,90));c.DrawPaint(p);});
Scene("draw-paint-shader-identity",(c,p)=>{using var s=SKShader.CreateLinearGradient(new(0,0),new(100,100),[SKColors.Red,SKColors.Blue],SKShaderTileMode.Clamp);p.Color=SKColors.White;p.Shader=s;c.DrawPaint(p);});
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(output);
