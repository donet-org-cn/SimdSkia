using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new SortedDictionary<string,object?>();var random=new Random(639541);
int[] Rect(SKRectI r)=>[r.Left,r.Top,r.Right,r.Bottom];
object Snapshot(SKRegion region)
{
    var bits=new byte[512];for(int y=0;y<64;y++)for(int x=0;x<64;x++)if(region.Contains(x-24,y-24))bits[(y*64+x)>>3]|=(byte)(1<<(x&7));
    var rectangles=new List<int[]>();using(var it=region.CreateRectIterator())while(it.Next(out var rect))rectangles.Add(Rect(rect));
    var clips=new List<int[]>();using(var it=region.CreateClipIterator(new(-5,-6,15,18)))while(it.Next(out var rect))clips.Add(Rect(rect));
    var spans=new List<int[]>();foreach(int y in new[]{-15,-5,0,5,15,30})using(var it=region.CreateSpanIterator(y,-10,20))while(it.Next(out int l,out int r))spans.Add([y,l,r]);
    using var boundary=region.GetBoundaryPath();using var restored=boundary is null?new SKRegion():new SKRegion(boundary);var roundtrip=new byte[512];for(int y=0;y<64;y++)for(int x=0;x<64;x++)if(restored.Contains(x-24,y-24))roundtrip[(y*64+x)>>3]|=(byte)(1<<(x&7));if(!bits.SequenceEqual(roundtrip))throw new Exception("Boundary path changes the region.");
    var query=new SKRectI(-5,-6,15,18);return new{bounds=Rect(region.Bounds),region.IsEmpty,region.IsRect,region.IsComplex,bits,rectangles,clips,spans,contains=region.Contains(query),quickContains=region.QuickContains(query),quickReject=region.QuickReject(query),intersects=region.Intersects(query)};
}
for(int sample=0;sample<70;sample++)
{
    using var a=new SKRegion();using var b=new SKRegion();var aa=new SKRectI[sample%6];var bb=new SKRectI[sample%4];SKRectI Make(){int x=random.Next(-20,20),y=random.Next(-20,20);return new(x,y,x+random.Next(1,30),y+random.Next(1,30));}for(int i=0;i<aa.Length;i++)aa[i]=Make();for(int i=0;i<bb.Length;i++)bb[i]=Make();a.SetRects(aa);b.SetRects(bb);
    values[$"{sample}-a"]=Snapshot(a);values[$"{sample}-contains"]=a.Contains(b);values[$"{sample}-intersects"]=a.Intersects(b);values[$"{sample}-reject"]=a.QuickReject(b);
    foreach(var op in Enum.GetValues<SKRegionOperation>()){using var c=new SKRegion(a);values[$"{sample}-{op}-result"]=c.Op(b,op);values[$"{sample}-{op}"]=Snapshot(c);}
    a.Translate(7,-9);values[$"{sample}-translate"]=Snapshot(a);
}
string[] paths=["M-4 -3H14V18H-4Z","M-4.5 -3.5H14.5V18.5H-4.5Z","M-10 -10L20 17L-8 27Z","M-10 -10Q50 20 2 25Z","M2 4A15 12 0 1 1 2 28Z","M-10 -10H30V30H-10ZM0 0H20V20H0Z"];
for(int i=0;i<paths.Length;i++)foreach(var fill in Enum.GetValues<SKPathFillType>()){using var path=SKPath.ParseSvgPathData(paths[i]);path.FillType=fill;using var clip=new SKRegion(new SKRectI(-20,-20,40,40));using var region=new SKRegion();values[$"path-{i}-{fill}-result"]=region.SetPath(path,clip);values[$"path-{i}-{fill}"]=Snapshot(region);}
using(var region=new SKRegion(new SKRectI(2,3,24,26)))
{
    region.Op(new SKRectI(8,10,16,20),SKRegionOperation.Difference);
    foreach(bool difference in new[]{false,true})
    {using var bitmap=new SKBitmap(new SKImageInfo(32,32,SKColorType.Rgba8888,SKAlphaType.Premul));using var canvas=new SKCanvas(bitmap);using var paint=new SKPaint{Color=SKColors.Red};canvas.Clear(SKColors.White);if(difference){canvas.Translate(100,200);canvas.ClipRegion(region,SKClipOperation.Difference);canvas.ResetMatrix();canvas.DrawRect(new SKRect(0,0,32,32),paint);}else canvas.DrawRegion(region,paint);values["region-draw-"+difference]=bitmap.GetPixelSpan().ToArray();}
}
foreach(var rect in new[]{new SKRectI(5,6,5,9),new SKRectI(7,9,5,11),new SKRectI(int.MinValue,0,int.MaxValue,5)}){using var region=new SKRegion(rect);values["degenerate-"+rect.ToString()]=new{region.IsEmpty,region.IsRect,bounds=Rect(region.Bounds)};}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Captured {values.Count} region cases");
