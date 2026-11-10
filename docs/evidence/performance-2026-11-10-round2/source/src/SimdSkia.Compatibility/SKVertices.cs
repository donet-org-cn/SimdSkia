using SimdSkia;
namespace SkiaSharp;
public class SKVertices : SKObject
{
    private TriangleMesh? mesh;
    private readonly bool colored;
    internal SKVertices(TriangleMesh mesh,bool colored){this.mesh=mesh;this.colored=colored;}
    internal TriangleMesh Mesh {get{ThrowIfDisposed();return mesh!;}}
    internal bool Colored=>colored;
    public static SKVertices CreateCopy(SKVertexMode vmode,SKPoint[] positions,SKColor[]? colors)=>CreateCopy(vmode,positions,null,colors,null);
    public static SKVertices CreateCopy(SKVertexMode vmode,SKPoint[] positions,SKPoint[]? texs,SKColor[]? colors)=>CreateCopy(vmode,positions,texs,colors,null);
    public static SKVertices CreateCopy(SKVertexMode vmode,SKPoint[] positions,SKPoint[]? texs,SKColor[]? colors,ushort[]? indices)
    {
        ArgumentNullException.ThrowIfNull(positions);if(!Enum.IsDefined(vmode))throw new ArgumentOutOfRangeException(nameof(vmode));
        if(texs is not null&&texs.Length!=positions.Length||colors is not null&&colors.Length!=positions.Length)throw new ArgumentException("Vertex attributes must have matching lengths.");
        if(positions.Length>1_000_000||(indices?.Length??0)>3_000_000)throw new NotSupportedException("Mesh exceeds one million vertices/triangles.");
        foreach(var p in positions)if(!float.IsFinite(p.X)||!float.IsFinite(p.Y))throw new ArgumentException("Vertex positions must be finite.",nameof(positions));
        if(texs is not null)foreach(var p in texs)if(!float.IsFinite(p.X)||!float.IsFinite(p.Y))throw new ArgumentException("Texture coordinates must be finite.",nameof(texs));
        int count=indices?.Length??positions.Length;if(indices is not null&&indices.Any(i=>i>=positions.Length))throw new ArgumentException("A mesh index is out of range.",nameof(indices));
        var triangles=new List<int>();int At(int i)=>indices is null?i:indices[i];
        if(vmode==SKVertexMode.Triangles)for(int i=0;i+2<count;i+=3){triangles.Add(At(i));triangles.Add(At(i+1));triangles.Add(At(i+2));}
        else for(int i=0;i+2<count;i++){triangles.Add(At(vmode==SKVertexMode.TriangleFan?0:i));triangles.Add(At(i+1));triangles.Add(At(i+2));}
        return new(new(positions.Select(p=>new PointF(p.X,p.Y)).ToArray(),texs?.Select(p=>new PointF(p.X,p.Y)).ToArray(),colors?.Select(c=>c.ToCore()).ToArray(),triangles.ToArray()),colors is not null);
    }
    protected override void Dispose(bool disposing)=>mesh=null;
}
