using SimdSkia;

namespace SkiaSharp;

/// <summary>Immutable positioned TrueType runs. Native Skia handles are unavailable.</summary>
public class SKTextBlob : SKObject
{
    internal readonly record struct Part(PathGeometry Geometry,bool Antialias);
    private Part[] parts;
    private readonly SKRect bounds;
    private readonly uint id;
    private static int nextId;
    internal SKTextBlob(Part[] parts,SKRect bounds){this.parts=parts;this.bounds=bounds;id=unchecked((uint)Interlocked.Increment(ref nextId));}
    internal ReadOnlySpan<Part> Parts {get{ThrowIfDisposed();return parts;}}
    public SKRect Bounds {get{ThrowIfDisposed();return bounds;}}
    public uint UniqueId {get{ThrowIfDisposed();return id;}}
    protected override void Dispose(bool disposing)=>parts=[];
}

public class SKRunBuffer
{
    private readonly ushort[] glyphs;
    internal bool Valid=true;
    internal SKRunBuffer(int count)=>glyphs=new ushort[count];
    internal void Validate()=>ObjectDisposedException.ThrowIf(!Valid,this);
    public int Size {get{Validate();return glyphs.Length;}}
    public Span<ushort> Glyphs {get{Validate();return glyphs;}}
    public void SetGlyphs(ReadOnlySpan<ushort> glyphs)
    {Validate();if(glyphs.Length>this.glyphs.Length)throw new ArgumentException("Glyph count exceeds the allocated run.",nameof(glyphs));glyphs.CopyTo(this.glyphs);}
}

public sealed class SKPositionedRunBuffer : SKRunBuffer
{
    private readonly SKPoint[] positions;
    internal readonly FontSnapshot Font;
    internal readonly SKRect? ExplicitBounds;
    internal SKPositionedRunBuffer(FontSnapshot font,int count,SKRect? bounds):base(count){Font=font;positions=new SKPoint[count];ExplicitBounds=bounds;}
    public Span<SKPoint> Positions {get{Validate();return positions;}}
    public void SetPositions(ReadOnlySpan<SKPoint> positions)
    {Validate();if(positions.Length>this.positions.Length)throw new ArgumentException("Position count exceeds the allocated run.",nameof(positions));positions.CopyTo(this.positions);}
}

public class SKTextBlobBuilder : SKObject
{
    private readonly List<SKPositionedRunBuffer> runs=[];
    public SKTextBlobBuilder(){ }
    public SKPositionedRunBuffer AllocatePositionedRun(SKFont font,int count,SKRect? bounds=null)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(font);if(count<0)throw new ArgumentOutOfRangeException(nameof(count));
        if(bounds is {} b&&!b.ToCore().IsFinite)throw new ArgumentException("Bounds must be finite.",nameof(bounds));
        var run=new SKPositionedRunBuffer(font.Capture(),count,bounds);runs.Add(run);return run;
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
                parts.Add(new(result.Geometry,run.Font.Antialias));
                var runBounds=run.ExplicitBounds??result.Bounds;
                if(!runBounds.ToCore().IsEmpty)bounds=Union(bounds,runBounds);
            }
            return parts.Count==0?null:new(parts.ToArray(),bounds??SKRect.Empty);
        }
        finally{ResetRuns();}
    }
    private static SKRect Union(SKRect? a,SKRect b)=>a is {} r?new(MathF.Min(r.Left,b.Left),MathF.Min(r.Top,b.Top),MathF.Max(r.Right,b.Right),MathF.Max(r.Bottom,b.Bottom)):b;
    private void ResetRuns(){foreach(var run in runs)run.Valid=false;runs.Clear();}
    protected override void Dispose(bool disposing)=>ResetRuns();
}
