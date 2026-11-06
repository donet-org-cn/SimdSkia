using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new SortedDictionary<string,object?>();int id=0;
void Capture(SKCanvas canvas,string key)
{
    bool device=canvas.GetDeviceClipBounds(out var d),local=canvas.GetLocalClipBounds(out var l);values[key+"-device"]=new[]{d.Left,d.Top,d.Right,d.Bottom};values[key+"-local"]=new[]{l.Left,l.Top,l.Right,l.Bottom};values[key+"-flags"]=new[]{device,local,canvas.IsClipEmpty,canvas.IsClipRect};values[key+"-properties"]=new float[]{canvas.DeviceClipBounds.Left,canvas.LocalClipBounds.Left};
    foreach(var rect in new[]{new SKRect(0,0,1,1),new SKRect(10,10,15,15),new SKRect(-100,-100,-90,-90),new SKRect(48,36,50,40),new SKRect(0,0,0,2)})
    {values[key+"-reject-"+id++]=canvas.QuickReject(rect);using var path=new SKPath();path.AddRect(rect);values[key+"-path-reject-"+id++]=canvas.QuickReject(path);}
    var m=canvas.TotalMatrix44;values[key+"-matrix44"]=new[]{m.M00,m.M01,m.M02,m.M03,m.M10,m.M11,m.M12,m.M13,m.M20,m.M21,m.M22,m.M23,m.M30,m.M31,m.M32,m.M33};
}
using(var bitmap=new SKBitmap(48,36))using(var canvas=new SKCanvas(bitmap))
{
    values["bitmap-surface-null"]=canvas.Surface is null;
    foreach(int mode in new[]{0,1,2,3,4,5,6,7,8})foreach(bool aa in new[]{false,true})
    {
        int save=canvas.Save();if(mode==1)canvas.ClipRect(new(3.25f,2.7f,40.6f,31.1f),antialias:aa);
        if(mode==2){canvas.Translate(5,3);canvas.Scale(1.2f,.8f);canvas.ClipRect(new(3.25f,2.7f,40.6f,31.1f),antialias:aa);}
        if(mode==3){using var path=new SKPath();path.AddCircle(20,18,12);canvas.ClipPath(path,antialias:aa);}
        if(mode==4){canvas.ClipRect(new(3,2,41,32));canvas.ClipRect(new(12,9,28,23),SKClipOperation.Difference,aa);}
        if(mode==5)canvas.ClipRect(new(-100,-80,-50,-40),antialias:aa);
        if(mode==6){canvas.RotateDegrees(23);canvas.ClipRect(new(1,2,34,24),antialias:aa);}
        if(mode==7){var m=SKMatrix.CreateScale(0,1);canvas.SetMatrix(m);}
        if(mode==8){var m=new SKMatrix44(SKMatrix.CreateScale(1.4f,.7f));canvas.SetMatrix(in m);m=new SKMatrix44(SKMatrix.CreateTranslation(3,4));canvas.Concat(in m);}
        Capture(canvas,$"{mode}-{aa}");canvas.RestoreToCount(save);Capture(canvas,$"restored-{mode}-{aa}");
    }
}
using(var surface=SKSurface.Create(new SKImageInfo(48,36))??throw new Exception("surface")){values["surface-owner"]=ReferenceEquals(surface,surface.Canvas.Surface);Capture(surface.Canvas,"surface");}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {values.Count} query values");
