using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);using var pixels=File.Create(Path.Combine(output,"pixels.bin"));var cases=new List<object>();var values=new Dictionary<string,object?>();
using var stamp=new SKPath();stamp.MoveTo(-2,-2);stamp.LineTo(3,0);stamp.LineTo(-2,2);stamp.Close();
SKPathEffect? Effect(string name)=>name switch
{
    "dash"=>SKPathEffect.CreateDash([8,4],3),"dash-negative"=>SKPathEffect.CreateDash([4,2,0,3],-2),"dash-zero"=>SKPathEffect.CreateDash([0,5],0),
    "corner"=>SKPathEffect.CreateCorner(5),"trim"=>SKPathEffect.CreateTrim(.18f,.73f),"trim-inverse"=>SKPathEffect.CreateTrim(.18f,.73f,SKTrimPathEffectMode.Inverted),
    "discrete"=>SKPathEffect.CreateDiscrete(6,2,123),"translate"=>SKPathEffect.Create1DPath(stamp,11,3,SKPath1DPathEffectStyle.Translate),"rotate"=>SKPathEffect.Create1DPath(stamp,11,3,SKPath1DPathEffectStyle.Rotate),"morph"=>SKPathEffect.Create1DPath(stamp,11,3,SKPath1DPathEffectStyle.Morph),
    "line-2d"=>SKPathEffect.Create2DLine(1.5f,SKMatrix.CreateScale(7,7)),"path-2d"=>SKPathEffect.Create2DPath(SKMatrix.CreateScale(10,8),stamp),
    "compose"=>Compose(),"sum"=>Sum(),_=>null
};
SKPathEffect Compose(){using var a=Effect("dash");using var b=Effect("corner");return SKPathEffect.CreateCompose(a!,b!);}
SKPathEffect Sum(){using var a=SKPathEffect.CreateDash([5,5],0);using var b=SKPathEffect.CreateDash([5,5],5);return SKPathEffect.CreateSum(a,b);}
string[] names=["none","dash","dash-negative","dash-zero","corner","trim","trim-inverse","discrete","translate","rotate","morph","line-2d","path-2d","compose","sum"];
for(int shape=0;shape<6;shape++)foreach(string name in names)foreach(var style in new[]{SKPaintStyle.Fill,SKPaintStyle.Stroke})foreach(bool aa in new[]{false,true})foreach(int transform in new[]{0,1})
{
    using var bitmap=new SKBitmap(new SKImageInfo(112,80,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(249,248,246));if(transform==1){canvas.Translate(5,3);canvas.Scale(.85f,.9f);}
    using var path=new SKPath();switch(shape)
    {
        case 0:path.MoveTo(9,18);path.LineTo(85,18);path.LineTo(76,62);break;
        case 1:path.AddRect(new SKRect(13,11,87,61));break;
        case 2:path.AddOval(new SKRect(13,11,87,61));break;
        case 3:path.MoveTo(8,55);path.CubicTo(18,-10,71,88,91,17);break;
        case 4:path.AddRect(new SKRect(13,11,48,61));path.AddCircle(73,37,19);break;
        case 5:path.MoveTo(13,25);path.LineTo(22,28);path.LineTo(30,65);path.LineTo(82,56);path.QuadTo(94,50,86,16);path.Close();break;
    }
    using var effect=Effect(name);using var paint=new SKPaint{Style=style,StrokeWidth=3,StrokeJoin=SKStrokeJoin.Round,IsAntialias=aa,Color=new SKColor(25,83,192,190),PathEffect=effect};
    canvas.DrawPath(path,paint);var data=bitmap.GetPixelSpan();int offset=(int)pixels.Position;pixels.Write(data);cases.Add(new{shape,name,style=(int)style,aa,transform,offset,length=data.Length});
    if(!aa&&transform==0)
    {
        using var fill=new SKPath();bool result=paint.GetFillPath(path,fill);values[$"fill-{shape}-{name}-{style}"]=new object[]{result,fill.IsEmpty};
        if(style==SKPaintStyle.Fill){using var iterator=fill.CreateRawIterator();var entries=new List<object>();var points=new SKPoint[4];for(SKPathVerb v;(v=iterator.Next(points))!=SKPathVerb.Done;){int n=v switch{SKPathVerb.Move=>1,SKPathVerb.Line=>2,SKPathVerb.Quad or SKPathVerb.Conic=>3,SKPathVerb.Cubic=>4,_=>0};entries.Add(new{verb=(int)v,points=points.Take(n).SelectMany(p=>new[]{p.X,p.Y}).ToArray(),weight=v==SKPathVerb.Conic?iterator.ConicWeight():0});}values[$"geometry-{shape}-{name}"]=entries;}
    }
}
using(var effect=Effect("corner"))using(var paint=new SKPaint{PathEffect=effect})using(var clone=paint.Clone())using(var path=new SKPath())
{
    path.AddRect(new SKRect(0,0,30,20));effect!.Dispose();paint.Dispose();using var rounded=clone.GetFillPath(path);values["retained-after-wrappers-disposed"]=rounded.VerbCount>5;clone.Reset();using var plain=clone.GetFillPath(path);values["reset-removes-effect"]=plain.VerbCount==path.VerbCount;
}
for(int i=0;i<7;i++)
{
    using var effect=i switch{0=>SKPathEffect.CreateDash([0,0],0),1=>SKPathEffect.CreateDash([-1,4],0),2=>SKPathEffect.CreateCorner(0),3=>SKPathEffect.CreateDiscrete(0,1),4=>SKPathEffect.CreateTrim(0,1),5=>SKPathEffect.CreateTrim(.8f,.2f,SKTrimPathEffectMode.Inverted),_=>SKPathEffect.Create1DPath(stamp,0,0,SKPath1DPathEffectStyle.Rotate)};values["invalid-"+i]=effect is null;
}
File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {cases.Count} path-effect images, {values.Count} metadata entries");
