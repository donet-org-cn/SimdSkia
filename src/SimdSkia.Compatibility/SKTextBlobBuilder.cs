using SimdSkia;
namespace SkiaSharp;
public partial class SKTextBlobBuilder : SKObject
{
    private readonly List<SKRunBuffer> runs=[];
    public SKTextBlobBuilder(){}
    private SKRawRunBuffer<T> Allocate<T>(SKFont font,int count,int kind,SKPoint origin,int textSize,bool hasText,SKRect? bounds)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(font);if(count<0||textSize<0)throw new ArgumentOutOfRangeException(nameof(count));
        if(count>1_000_000||textSize>64*1024*1024)throw new NotSupportedException("Text run exceeds the managed allocation budget.");
        if(!float.IsFinite(origin.X)||!float.IsFinite(origin.Y)||bounds is {} b&&!b.ToCore().IsFinite)throw new ArgumentException("Run coordinates must be finite.");
        var snapshot=font.Capture();SKRunBuffer run=(kind,hasText) switch
        {
            (0,true)=>new SKTextRunBuffer(snapshot,count,origin,textSize,bounds),
            (1,false)=>new SKHorizontalRunBuffer(snapshot,count,origin,0,bounds),
            (1,true)=>new SKHorizontalTextRunBuffer(snapshot,count,origin,textSize,bounds),
            (2,false)=>new SKPositionedRunBuffer(snapshot,count,origin,0,bounds),
            (2,true)=>new SKPositionedTextRunBuffer(snapshot,count,origin,textSize,bounds),
            (3,false)=>new SKRotationScaleRunBuffer(snapshot,count,origin,0,bounds),
            (3,true)=>new SKRotationScaleTextRunBuffer(snapshot,count,origin,textSize,bounds),
            _=>new(snapshot,count,kind,origin,0,false,bounds)
        };
        runs.Add(run);return new(run);
    }
    public SKTextBlob? Build()
    {
        ThrowIfDisposed();if(runs.Count==0)return null;
        try
        {
            var parts=new List<SKTextBlob.Part>();SKRect? bounds=null;
            foreach(var run in runs)
            {
                if(run.Size==0)continue;
                var result=PositionedGeometryCache.Get(run);
                parts.Add(new(result.Geometry,run.Font.Antialias){GlyphRun=result.GlyphRun,LcdText=run.Font.LcdText,IgnoreIntercepts=run.Kind==3,Text=run.HasText?new(run.TextArray.ToArray(),run.ClusterArray.ToArray()):null});var b=run.ExplicitBounds??result.Bounds;if(!b.ToCore().IsEmpty)bounds=Union(bounds,b);
            }
            return parts.Count==0?null:new(parts.ToArray(),bounds??SKRect.Empty);
        }
        finally{ResetRuns();}
    }
    internal static (PathGeometry Geometry,SKRect Bounds,GlyphRunGeometry GlyphRun) Geometry(SKRunBuffer run)
    {
        var segments=new List<PathSegment>();var glyphs=new PositionedGlyph[run.GlyphArray.Length];SKRect? bounds=null;float advance=run.Origin.X;var font=run.Font;
        for(int i=0;i<run.GlyphArray.Length;i++)
        {
            ushort glyph=run.GlyphArray[i];if(glyph>=font.Font.GlyphCount)throw new ArgumentOutOfRangeException("glyphs");
            Matrix3 position;
            if(run.Kind==3)position=((SKRotationScaleMatrix[])run.PositionArray)[i].ToMatrix().ToCore();
            else
            {
                SKPoint p=run.Kind switch{1=>new(((float[])run.PositionArray)[i],run.Origin.Y),2=>((SKPoint[])run.PositionArray)[i],_=>new(advance,run.Origin.Y)};
                position=new(1,0,p.X,0,1,p.Y,0,0,1);
            }
            if(!position.IsFinite)throw new ArgumentException("Glyph transforms must be finite.");
            font.Outline(glyph).AppendTransformedTo(segments,position*font.Transform);
            glyphs[i]=run.Kind==3?new(font.Outline(glyph).Transform((position with{M02=0,M12=0})*font.Transform),new(position.M02,position.M12)):new(font.Outline(glyph),new(position.M02,position.M12));
            // Default-positioned runs use tight glyph metrics; positioned runs
            // use conservative typeface bounds, as SkTextBlobBuilder m119 does.
            var b=run.Kind==0?font.Bounds(glyph):font.GlobalBounds();if(!b.ToCore().IsEmpty)
            {
                var a=position.Map(b.Left,b.Top);var c=position.Map(b.Right,b.Top);var d=position.Map(b.Left,b.Bottom);var e=position.Map(b.Right,b.Bottom);
                bounds=Union(bounds,new(MathF.Min(MathF.Min(a.X,c.X),MathF.Min(d.X,e.X)),MathF.Min(MathF.Min(a.Y,c.Y),MathF.Min(d.Y,e.Y)),MathF.Max(MathF.Max(a.X,c.X),MathF.Max(d.X,e.X)),MathF.Max(MathF.Max(a.Y,c.Y),MathF.Max(d.Y,e.Y))));
            }
            advance+=font.Font.Advance(glyph)*font.AdvanceScale;
        }
        return(new(segments),bounds??SKRect.Empty,new(glyphs,run.Kind==3?Matrix3.Identity:font.Transform,GlyphPositioning.QuarterPixel));
    }
    private static SKRect Union(SKRect? a,SKRect b)=>a is {} r?new(MathF.Min(r.Left,b.Left),MathF.Min(r.Top,b.Top),MathF.Max(r.Right,b.Right),MathF.Max(r.Bottom,b.Bottom)):b;
    private void ResetRuns(){foreach(var run in runs)run.Valid=false;runs.Clear();}
    protected override void Dispose(bool disposing)=>ResetRuns();
}
