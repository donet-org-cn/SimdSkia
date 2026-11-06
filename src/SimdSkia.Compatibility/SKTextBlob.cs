using SimdSkia;

namespace SkiaSharp;

/// <summary>Immutable managed TrueType glyph runs. Native Skia handles are unavailable.</summary>
public partial class SKTextBlob : SKObject
{
    internal sealed record TextMetadata(byte[] Utf8,uint[] Clusters);
    internal readonly record struct Part(PathGeometry Geometry,bool Antialias)
    {
        internal TextMetadata? Text {get;init;}
        internal GlyphRunGeometry? GlyphRun {get;init;}
        internal bool LcdText {get;init;}
        internal bool IgnoreIntercepts {get;init;}
    }
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
