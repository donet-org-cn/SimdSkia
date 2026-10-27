using System.Text.Json;
using SimdSkia;
using SimdSkia.Cpu;
using SimdSkia.Text;
using SkiaSharp;

// Isolated reference diagnostic. Native Skia is never linked by the product.
// Rasterize both extracted outlines through the identical managed rasterizer
// to separate TrueType geometry discrepancies from platform text-mask rules.
string fontPath=args.Length>0?args[0]:"baseline/fonts/ZCOOLXiaoWei-Regular.ttf";
string output=args.Length>1?args[1]:"artifacts/font-outlines";
Directory.CreateDirectory(output);
var managed=new TrueTypeFont(File.ReadAllBytes(fontPath));
using var native=SKTypeface.FromFile(fontPath);
var results=new List<object>();
foreach(float size in new[]{12f,18f,27f,40.5f})
foreach(char character in "AVATAR中文界面123.45".Distinct())
{
    using var font=new SKFont(native,size){Hinting=SKFontHinting.None,Subpixel=true,Edging=SKFontEdging.Antialias,LinearMetrics=true,BaselineSnap=false};
    ushort glyph=font.GetGlyph(character);
    using var path=font.GetGlyphPath(glyph);
    var segments=new List<PathSegment>();
    using var iterator=path.CreateRawIterator();var points=new SKPoint[4];
    PointF P(int i)=>new(points[i].X,points[i].Y);
    while(true)
    {
        var verb=iterator.Next(points);if(verb==SKPathVerb.Done)break;
        segments.Add(verb switch
        {
            SKPathVerb.Move=>new(PathVerb.Move,P(0)),
            SKPathVerb.Line=>new(PathVerb.Line,P(1)),
            SKPathVerb.Quad=>new(PathVerb.Quadratic,P(1),P(2)),
            SKPathVerb.Cubic=>new(PathVerb.Cubic,P(1),P(2),P(3)),
            SKPathVerb.Close=>new(PathVerb.Close,default),
            _=>throw new NotSupportedException($"Unexpected native glyph verb {verb}")
        });
    }
    var reference=new PathGeometry(segments);
    float scale=size/managed.UnitsPerEm;
    var own=managed.Outline(managed.GetGlyph(character)).Transform(new(scale,0,0,0,-scale,0,0,0,1));
    var translation=new Matrix3(1,0,8.25f,0,1,64.375f,0,0,1);
    byte[] a=PathRasterizer.Coverage(own.Flatten(translation,.001f),FillRule.Winding,96,96,true);
    byte[] b=PathRasterizer.Coverage(reference.Flatten(translation,.001f),FillRule.Winding,96,96,true);
    int maximum=0,different=0;long squares=0;
    for(int i=0;i<a.Length;i++){int d=a[i]-b[i];maximum=Math.Max(maximum,Math.Abs(d));squares+=(long)d*d;if(d!=0)different++;}
    results.Add(new{size,character,glyph,managedGlyph=managed.GetGlyph(character),maximum,rmse=Math.Sqrt((double)squares/a.Length),different,
        nativeBounds=reference.GetTightBounds(Matrix3.Identity),managedBounds=own.GetTightBounds(Matrix3.Identity)});
    if(size==27)
    {
        File.WriteAllText(Path.Combine(output,$"outline-{(int)character}.json"),JsonSerializer.Serialize(new{native=segments,managed=own.Segments.ToArray()},new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(output,$"glyph-{glyph}.json"),JsonSerializer.Serialize(reference.Transform(new(1/scale,0,0,0,-1/scale,0,0,0,1)).Segments.ToArray()));
    }
}
File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Wrote {results.Count} isolated native/managed outline comparisons to {output}.");
