using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new SortedDictionary<string,object?>();
int Count(SKPathVerb verb)=>verb switch{SKPathVerb.Move=>1,SKPathVerb.Line=>2,SKPathVerb.Quad or SKPathVerb.Conic=>3,SKPathVerb.Cubic=>4,_=>0};
object[] Raw(SKPath path)
{
    using var iterator=path.CreateRawIterator();var rows=new List<object>();var points=new SKPoint[4];
    for(int n=0;n<1000;n++){var peek=iterator.Peek();var verb=iterator.Next(points);if(verb==SKPathVerb.Done)break;rows.Add(new{verb,peek,p=points.Take(Count(verb)).SelectMany(p=>new[]{p.X,p.Y}).ToArray(),w=verb==SKPathVerb.Conic?iterator.ConicWeight():0});}return rows.ToArray();
}
object[] Regular(SKPath path,bool forceClose)
{
    using var iterator=path.CreateIterator(forceClose);var rows=new List<object>();var points=new SKPoint[4];
    for(int n=0;n<1000;n++){var verb=iterator.Next(points);if(verb==SKPathVerb.Done)break;rows.Add(new{verb,p=points.Take(Count(verb)).SelectMany(p=>new[]{p.X,p.Y}).ToArray(),w=verb==SKPathVerb.Conic?iterator.ConicWeight():0,line=verb==SKPathVerb.Line&&iterator.IsCloseLine(),closed=iterator.IsCloseContour()});}return rows.ToArray();
}
for(int i=0;i<10;i++)
{
    using var path=new SKPath();
    switch(i)
    {
        case 1:path.MoveTo(1,2);break;
        case 2:path.MoveTo(1,2);path.Close();break;
        case 3:path.MoveTo(1,2);path.LineTo(1,2);path.Close();break;
        case 4:path.AddRect(new(1,2,20,30));break;
        case 5:path.MoveTo(1,2);path.LineTo(20,30);path.MoveTo(50,60);path.LineTo(60,70);break;
        case 6:path.MoveTo(1,2);path.ConicTo(10,50,60,20,.7f);path.Close();break;
        case 7:path.MoveTo(1,2);path.QuadTo(10,50,60,20);path.CubicTo(20,40,70,80,90,100);break;
        case 8:path.MoveTo(3,4);path.LineTo(10,4);path.LineTo(10,4);path.LineTo(30,10);path.Close();break;
        case 9:path.MoveTo(1,2);path.LineTo(1,2);break;
    }
    values[$"raw-{i}"]=Raw(path);values[$"regular-{i}"]=Regular(path,false);values[$"closed-{i}"]=Regular(path,true);
    using var reverse=new SKPath();reverse.AddPathReverse(path);values[$"reverse-{i}"]=Raw(reverse);
}
for(int pow=0;pow<=5;pow++)foreach(float w in new[]{0f,.25f,.70710677f,1f,3f})
{var p=SKPath.ConvertConicToQuads(new(1,2),new(40,80),new(90,20),w,pow);values[$"conic-{pow}-{w}"]=p.SelectMany(v=>new[]{v.X,v.Y}).ToArray();}
string[] svg=["","M1 2L10 20Z","m5 5h10v20h-10z","M1 2q10 30 40 20t60 10c2 3 4 5 6 7s3 4 5 6","M2 4 A30 40 15 0 1 70 80","M2 4a30 40 15 1 0 70 80","M1e1,2e1L-.5.4z","M1 2 A0 0 0 0 1 30 40","M1 2 A10 10 0 0 1 1 2","M1 2Q3","M1 2X5 6"];
for(int i=0;i<svg.Length;i++)
{
    using var path=SKPath.ParseSvgPathData(svg[i]);values[$"svg-valid-{i}"]=path is not null;if(path is null)continue;
    values[$"svg-raw-{i}"]=Raw(path);values[$"svg-bounds-{i}"]=new[]{path.TightBounds.Left,path.TightBounds.Top,path.TightBounds.Right,path.TightBounds.Bottom};
    using var roundtrip=SKPath.ParseSvgPathData(path.ToSvgPathData());if(roundtrip is null)throw new Exception("SVG serialization failed to parse");
}
for(int shape=0;shape<5;shape++)foreach(var direction in Enum.GetValues<SKPathDirection>())for(uint index=0;index<8;index++)
{
    using var rounded=new SKRoundRect(new SKRect(2,4,62,84),7,11);if(shape==1)rounded.SetNinePatch(rounded.Rect,3,7,9,13);if(shape==2)rounded.SetOval(rounded.Rect);if(shape==3)rounded.SetRect(rounded.Rect);if(shape==4)rounded.SetEmpty();
    using var path=new SKPath();path.AddRoundRect(rounded,direction,index);string key=$"round-{shape}-{direction}-{index}";values[key]=Raw(path);values[key+"-type"]=new[]{path.IsRoundRect,path.IsRect,path.IsOval};using var clone=new SKPath(path);using var retrieved=clone.GetRoundRect();values[key+"-retrieved"]=retrieved is null?null:retrieved.Radii.SelectMany(p=>new[]{p.X,p.Y}).ToArray();
    var matrix=SKMatrix.CreateScaleTranslation(2,3,5,7);path.Transform(in matrix);using var transformed=path.GetRoundRect();values[key+"-transform"]=transformed is null?null:transformed.Radii.SelectMany(p=>new[]{p.X,p.Y}).ToArray();
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {values.Count} path behavior cases");
