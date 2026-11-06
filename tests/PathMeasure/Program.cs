using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new SortedDictionary<string,object?>();
object[] Raw(SKPath path){using var it=path.CreateRawIterator();var p=new SKPoint[4];var rows=new List<object>();for(SKPathVerb v;(v=it.Next(p))!=SKPathVerb.Done;){int count=v switch{SKPathVerb.Move=>1,SKPathVerb.Line=>2,SKPathVerb.Quad or SKPathVerb.Conic=>3,SKPathVerb.Cubic=>4,_=>0};rows.Add(new{v,p=p.Take(count).SelectMany(v=>new[]{v.X,v.Y}).ToArray(),w=v==SKPathVerb.Conic?it.ConicWeight():0});}return rows.ToArray();}
for(int scene=0;scene<9;scene++)foreach(bool closed in new[]{false,true})foreach(float resolution in new[]{1f,4f})
{
    using var path=new SKPath();path.MoveTo(2,4);
    switch(scene){case 1:path.LineTo(32,44);break;case 2:path.AddRect(new(2,4,42,64));break;case 3:path.QuadTo(20,70,90,30);break;case 4:path.CubicTo(40,70,80,-60,120,30);break;case 5:path.ConicTo(40,70,90,30,.70710677f);break;case 6:path.ConicTo(40,70,90,30,3);break;case 7:path.LineTo(40,50);path.MoveTo(70,80);path.LineTo(90,110);path.Close();break;case 8:path.LineTo(2,4);path.QuadTo(2,4,50,70);break;}
    using var measure=new SKPathMeasure(path,closed,resolution);string key=$"{scene}-{closed}-{resolution}";int contour=0;
    do{
        string prefix=key+"-"+contour++;values[prefix+"-length"]=measure.Length;values[prefix+"-closed"]=measure.IsClosed;
        foreach(float f in new[]{-.1f,0f,.1f,.25f,.5f,.8f,1f,1.1f}){float d=f*measure.Length;bool found=measure.GetPositionAndTangent(d,out var p,out var t);values[prefix+"-position-"+f]=new{found,p=new[]{p.X,p.Y},t=new[]{t.X,t.Y}};if(measure.Length>0){using var segment=measure.GetSegment(.1f*measure.Length,Math.Clamp(f,0,1)*measure.Length,true);values[prefix+"-segment-"+f]=segment is null?null:Raw(segment);}}
    }while(measure.NextContour());
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {values.Count} path measure cases");
