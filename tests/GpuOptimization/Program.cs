using SimdSkia;
using SimdSkia.Gpu;
using SimdSkia.Text;
using System.Text.Json;

const int width=128,height=96,stride=width*4+12;
using var expected=new PixelBuffer(width,height,PixelFormat.Rgba8888,AlphaFormat.Premultiplied,stride);
using var actual=new PixelBuffer(width,height,PixelFormat.Rgba8888,AlphaFormat.Premultiplied,stride);
using var reference=new VulkanCanvas(expected,false);
using var optimized=new VulkanCanvas(actual,true);
for(int y=0;y<height;y++){expected.GetRowSpan(y)[(width*4)..].Fill(0xa5);actual.GetRowSpan(y)[(width*4)..].Fill(0xa5);}
var random=new Random(20261021);int checks=0,comparisons=0;
float Next(float lo,float hi)=>lo+(float)random.NextDouble()*(hi-lo);
Rgba32 Color()=>new((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256));
void Compare(string name,params DrawingCommand[] commands)
{
    foreach(var command in commands){reference.Execute(command);optimized.Execute(command);}
    reference.Flush();optimized.Flush();
    if(!expected.GetPixelSpan().SequenceEqual(actual.GetPixelSpan()))
    {
        var a=expected.GetPixelSpan();var b=actual.GetPixelSpan();int i=0;while(i<a.Length&&a[i]==b[i])i++;
        throw new Exception($"{name}: byte {i}: reference {a[i]}, optimized {b[i]}");
    }
    for(int y=0;y<height;y++)foreach(byte value in actual.GetRowSpan(y)[(width*4)..])if(value!=0xa5)throw new Exception("Readback overwrote row padding.");
    checks++;comparisons++;
}
void Rect(List<PathSegment> path,RectF r,bool reverse=false)
{
    path.Add(new(PathVerb.Move,new(r.Left,r.Top)));
    path.Add(new(PathVerb.Line,reverse?new(r.Left,r.Bottom):new(r.Right,r.Top)));
    path.Add(new(PathVerb.Line,new(r.Right,r.Bottom)));
    path.Add(new(PathVerb.Line,reverse?new(r.Right,r.Top):new(r.Left,r.Bottom)));
    path.Add(new(PathVerb.Close,default));
}
// No implicit Clear is required for the first draw: GPU transfer initialization is observable.
Compare("initial device-local zero",DrawingCommand.FillRectangle(new(5,6,15,16),Matrix3.Identity,new(new(240,60,20,128),CompositeMode.SourceOver,true)));
for(int iteration=0;iteration<96;iteration++)
{
    var segments=new List<PathSegment>();
    for(int n=0;n<16;n++)
    {
        float x=Next(-40,140),y=Next(-30,110);
        Rect(segments,new(x,y,x+Next(1,45),y+Next(1,40)),n%3==0);
    }
    if(iteration%3==0)
    {
        segments.Add(new(PathVerb.Move,new(-15,48)));
        segments.Add(new(PathVerb.Cubic,new(50,-30),new(110,150),new(140,40)));
        segments.Add(new(PathVerb.Quadratic,new(30,120),new(-15,48)));
        segments.Add(new(PathVerb.Close,default));
    }
    var path=new PathGeometry(segments,(FillRule)(iteration%4));
    var matrix=(iteration%3) switch
    {
        0=>new Matrix3(-1.1f,0,120.03125f,0,.83f,.0625f,0,0,1),
        1=>new Matrix3(.9f,.2f,-10.0625f,-.13f,1.15f,8.03125f,0,0,1),
        _=>new Matrix3(1,0,(iteration%7)/16f,0,1,(iteration%9)/16f,0,0,1)
    };
    RectF? clip=iteration%2==0?new(7.03125f,5.0625f,119.96875f,91.9375f):null;
    var paint=new Paint(Color(),(CompositeMode)(iteration%3),iteration%5!=0,
        iteration%7==0?new LinearGradient(new(0,0),new(115,80),Color(),Color()):null);
    var command=new DrawingCommand(CommandKind.FillPath,default,matrix,paint){Path=path,Clip=clip};
    Compare("tile/ray/winding/AA "+iteration,DrawingCommand.Clear(Color()),command);
    Compare("cache/recolor "+iteration,command with {Paint=paint with {Color=Color(),Shader=null}});
}
// Strict AA ties, thin rectangles, negative scales and fractional clip boundaries.
for(int k=0;k<64;k++)
{
    var m=new Matrix3(k%2==0?1:-1,0,k%2==0?0:128,0,k%3==0?-1:1,k%3==0?96:0,0,0,1);
    var r=new RectF(8+k/32f,7.03125f,9+k/32f+(k%3)/32f,90.96875f);
    var c=DrawingCommand.FillRectangle(r,m,new(Color(),(CompositeMode)(k%3),true)) with {Clip=new(8.0625f,8.09375f,120.03125f,92.03125f)};
    Compare("separable rectangle "+k,DrawingCommand.Clear(Color()),c);
}
// More than 127 candidates must use the unrestricted winding kernel; negative windings too.
foreach(float outside in new[]{-1e9f,-200,200,1e9f})foreach(bool aa in new[]{false,true})
    Compare("offscreen dispatch bounds",DrawingCommand.Clear(Color()),DrawingCommand.FillRectangle(new(outside,outside,outside+8,outside+8),Matrix3.Identity,new(Color(),CompositeMode.SourceOver,aa)));
foreach(int count in new[]{31,63,127,129,260})foreach(var rule in Enum.GetValues<FillRule>())
{
    var segments=new List<PathSegment>();for(int i=0;i<count;i++)Rect(segments,new(7.03125f,8.03125f,120.96875f,91.96875f),i%3==0);
    Compare($"counter limit {count} {rule}",DrawingCommand.Clear(new(20,40,80,255)),
        new DrawingCommand(CommandKind.FillPath,default,Matrix3.Identity,new(new(230,170,40,157),CompositeMode.SourceOver,true)){Path=new(segments,rule)});
}
foreach(var rule in Enum.GetValues<FillRule>())
    Compare("empty "+rule,DrawingCommand.Clear(Color()),new(CommandKind.FillPath,default,Matrix3.Identity,new(Color(),CompositeMode.Source,true)){Path=new([],rule)});
string? fontPath=args.FirstOrDefault();
if(fontPath is not null)
{
    var font=new TrueTypeFont(File.ReadAllBytes(fontPath));var segments=new List<PathSegment>();float x=3,factor=24f/font.UnitsPerEm;
    foreach(var rune in "中文 ABC".EnumerateRunes())
    {
        ushort glyph=font.GetGlyph(rune.Value);
        segments.AddRange(font.Outline(glyph).Transform(new(factor,0,x,0,-factor,50,0,0,1)).Segments.ToArray());
        x+=font.Advance(glyph)*factor;
    }
    var path=new PathGeometry(segments);
    for(int i=0;i<20;i++)Compare("Chinese subpixel "+i,DrawingCommand.Clear(Color()),
        new(CommandKind.FillPath,default,new(1,0,i/16f,0,1,(i%5)/16f,0,0,1),new(Color(),CompositeMode.SourceOver,true)){Path=path});
}
// Exceed the bin budget before allocating its payload; retain the unrestricted scan.
foreach(float outside in new[]{-500f,500f})
{
    var offscreen=new List<PathSegment>();for(int i=0;i<20;i++)Rect(offscreen,new(outside+i*10,10,outside+i*10+8,70));
    var path=new PathGeometry(offscreen);var offContours=path.Flatten(Matrix3.Identity);var offEdges=new List<float>();
    foreach(var contour in offContours)for(int i=0;i<contour.Length;i++)
    {var aa=contour[i];var bb=contour[(i+1)%contour.Length];if(aa==bb)continue;offEdges.Add(aa.X);offEdges.Add(aa.Y);offEdges.Add(bb.X);offEdges.Add(bb.Y);}
    if(TiledEdges.Create(offContours,offEdges.ToArray(),width,height) is not null)throw new Exception("Offscreen contours populated a viewport tile.");
    checks++;
}
var huge=new List<PathSegment>();for(int i=0;i<12;i++)Rect(huge,new(0,0,4096,4096));
var contours=new PathGeometry(huge).Flatten(Matrix3.Identity);
var values=new List<float>();foreach(var contour in contours)for(int i=0;i<contour.Length;i++)
{var a=contour[i];var b=contour[(i+1)%contour.Length];if(a==b)continue;values.Add(a.X);values.Add(a.Y);values.Add(b.X);values.Add(b.Y);}
if(TiledEdges.Create(contours,values.ToArray(),4096,4096) is not null)throw new Exception("Expected bounded bin fallback.");checks++;
var unchanged=actual.CopyBytes();bool glyphRejected=false;
try{optimized.Execute(new(CommandKind.DrawGlyphRun,default,Matrix3.Identity,new(new(0,0,0,255),CompositeMode.SourceOver)){GlyphRun=new([new(PathGeometry.Rectangle(new(1,1,5,5)),default)],Matrix3.Identity)});}
catch(NotSupportedException){glyphRejected=true;}
optimized.Flush();
if(!glyphRejected||!actual.GetPixelSpan().SequenceEqual(unchanged))throw new Exception("Unsupported Vulkan glyph command was ignored or modified pixels.");checks++;
Directory.CreateDirectory("artifacts/optimization-2026-10-21");
File.WriteAllText("artifacts/optimization-2026-10-21/gpu-kernel-tests.json",JsonSerializer.Serialize(new{Checks=checks,Comparisons=comparisons,BinningChecks=checks-comparisons-1,UnsupportedGlyphChecks=1,Passed=true,Device=optimized.DeviceName,MaximumChannelError=0,Reference="Unrestricted GPU edge scan and 16x16 samples",Font=fontPath},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks} GPU checks ({comparisons} exact-byte comparisons, {checks-comparisons-1} binning limits, 1 unsupported glyph rejection; tiles, winding, AA, inverse fills, clipping, shaders, cache, stride, initialization, CJK).");
