using SimdSkia;
using SimdSkia.Cpu;
int checks=0,packed=0,original=0;
for(int n=0;n<30;n++)foreach(bool aa in new[]{false,true})
{
    using var expected=PixelBuffer.Rent(96,64);using var actual=PixelBuffer.Rent(96,64);
    using var scalar=new RasterCanvas(expected);using var batched=new RasterCanvas(actual);
    var outline=new PathGeometry([new(PathVerb.Move,new(0,0)),new(PathVerb.Quadratic,new(4,-3),new(7,0)),new(PathVerb.Line,new(7,8)),new(PathVerb.Cubic,new(5,10),new(2,5),new(0,8)),new(PathVerb.Close,default)]);
    var positions=Enumerable.Range(0,8).Select(i=>new PositionedGlyph(outline,new(4+i*(n%3==0?4.5f:9.1f),18+n%5)));
    var run=new GlyphRunGeometry(positions,new(.8f,0,0,0,1,0,0,0,1),n%2==0?GlyphPositioning.QuarterPixel:GlyphPositioning.Exact);
    var matrix=new Matrix3(1,n%4*.03f,n%7*.11f,0,1,n%6*.17f,0,0,1);
    var command=new DrawingCommand(CommandKind.DrawGlyphRun,default,matrix,new(new(100,30,200,130),CompositeMode.SourceOver,aa)){GlyphRun=run,SnapGlyphBaseline=n%2==0,CurveTolerance=.015625f};
    var individual=GlyphCommands.Expand(command,run);var groups=GlyphCommands.PackDisjoint(command,run);original+=individual.Length;packed+=groups.Length;
    foreach(var item in individual)scalar.Execute(item);
    foreach(var path in groups)batched.Execute(command with{Kind=CommandKind.FillPath,Path=path,GlyphRun=null,Transform=Matrix3.Identity});
    if(!expected.GetPixelSpan().SequenceEqual(actual.GetPixelSpan()))throw new InvalidOperationException($"Glyph batching changed pixels for transform {n}, AA={aa}.");
    checks++;
}
if(packed>=original)throw new InvalidOperationException("Disjoint text did not reduce GPU submissions.");
Console.WriteLine($"PASS {checks} exact glyph batch comparisons; {original} draws -> {packed}; overlaps preserve order.");
