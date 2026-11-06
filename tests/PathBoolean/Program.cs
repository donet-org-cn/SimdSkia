using SkiaSharp;
using System.Text.Json;
var output=args[0];Directory.CreateDirectory(output);var values=new Dictionary<string,object?>();var cases=new List<object>();using var pixels=File.Create(Path.Combine(output,"pixels.bin"));int checks=0;
void Check(bool value){checks++;if(!value)throw new Exception("Path Boolean assertion "+checks);}
SKPath Shape(int index)
{
 var path=new SKPath();switch(index){case 0:path.AddRect(new(8,7,61,49));break;case 1:path.AddCircle(42,34,27);break;case 2:path.AddRect(new(10,9,70,58));path.AddRect(new(24,20,56,45));path.FillType=SKPathFillType.EvenOdd;break;case 3:path.MoveTo(6,8);path.CubicTo(85,2,7,66,73,59);path.LineTo(9,58);path.Close();break;case 4:for(int i=0;i<5;i++){double t=i*4*Math.PI/5-Math.PI/2;var p=new SKPoint(44+30*(float)Math.Cos(t),34+28*(float)Math.Sin(t));if(i==0)path.MoveTo(p);else path.LineTo(p);}path.Close();break;default:break;}return path;
}
bool[] Contains(SKPath path){var result=new List<bool>();for(int y=0;y<75;y+=3)for(int x=0;x<95;x+=3)result.Add(path.Contains(x+.123f,y+.321f));return result.ToArray();}
for(int shape=0;shape<6;shape++)
{
 using var a=Shape(shape);values[$"shape-{shape}-convex"]=new object[]{a.IsConvex,a.IsConcave,a.Convexity.ToString()};values[$"shape-{shape}-contains"]=Contains(a);
 using var simple=a.Simplify();using var winding=a.ToWinding();values[$"shape-{shape}-simple"]=Contains(simple);values[$"shape-{shape}-winding"]=Contains(winding);
}
for(int shape=0;shape<4;shape++)for(int inverse=0;inverse<4;inverse++)for(int operation=0;operation<5;operation++)
{
 using var a=Shape(shape);using var b=Shape(shape==0?1:0);if((inverse&1)!=0)a.FillType=(SKPathFillType)((int)a.FillType^2);if((inverse&2)!=0)b.FillType=(SKPathFillType)((int)b.FillType^2);using var result=a.Op(b,(SKPathOp)operation)??throw new Exception("Path op failed.");values[$"op-{shape}-{inverse}-{operation}"]=Contains(result);
 using(var inPlace=new SKPath(a)){Check(inPlace.Op(b,(SKPathOp)operation,inPlace));Check(Contains(inPlace).SequenceEqual(Contains(result)));}
 using(var builder=new SKPath.OpBuilder()){builder.Add(a,SKPathOp.Union);builder.Add(b,(SKPathOp)operation);using var batch=new SKPath();Check(builder.Resolve(batch));values[$"batch-{shape}-{inverse}-{operation}"]=Contains(batch);Check(builder.Resolve(batch));Check(batch.IsEmpty);}
 for(int aa=0;aa<2;aa++)
 {
  using var surface=SKSurface.Create(new SKImageInfo(96,76,SKColorType.Rgba8888,SKAlphaType.Premul))!;surface.Canvas.Clear(SKColors.White);using(var paint=new SKPaint{Color=new SKColor(50,80,180,210),IsAntialias=aa!=0})surface.Canvas.DrawPath(result,paint);using var image=surface.Snapshot();using var read=new SKBitmap(new SKImageInfo(96,76,SKColorType.Rgba8888,SKAlphaType.Premul));Check(image.ReadPixels(read.Info,read.GetPixels(),read.RowBytes));var bytes=read.GetPixelSpan().ToArray();cases.Add(new{kind="path-boolean",scene=shape*4+inverse,transform=operation*2+aa,offset=pixels.Position,length=bytes.Length});pixels.Write(bytes);
 }
}
File.WriteAllText(Path.Combine(output,"values.json"),JsonSerializer.Serialize(values));File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases));File.WriteAllText(Path.Combine(output,"local.json"),JsonSerializer.Serialize(new{checks}));Console.WriteLine($"Captured {cases.Count} Boolean path images, {values.Count} topology entries and {checks} mutation/lifetime checks");
