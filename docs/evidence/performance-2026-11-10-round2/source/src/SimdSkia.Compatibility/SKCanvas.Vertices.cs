using SimdSkia;
namespace SkiaSharp;
public partial class SKCanvas
{
    public void DrawVertices(SKVertexMode vmode,SKPoint[] vertices,SKColor[] colors,SKPaint paint)=>DrawVertices(vmode,vertices,null!,colors,paint);
    public void DrawVertices(SKVertexMode vmode,SKPoint[] vertices,SKPoint[] texs,SKColor[] colors,SKPaint paint)=>DrawVertices(vmode,vertices,texs,colors,SKBlendMode.Modulate,null!,paint);
    public void DrawVertices(SKVertexMode vmode,SKPoint[] vertices,SKPoint[] texs,SKColor[] colors,ushort[] indices,SKPaint paint)=>DrawVertices(vmode,vertices,texs,colors,SKBlendMode.Modulate,indices,paint);
    public void DrawVertices(SKVertexMode vmode,SKPoint[] vertices,SKPoint[] texs,SKColor[] colors,SKBlendMode mode,ushort[] indices,SKPaint paint)
    {using var mesh=SKVertices.CreateCopy(vmode,vertices,texs,colors,indices);DrawVertices(mesh,mode,paint);}
    public void DrawVertices(SKVertices vertices,SKBlendMode mode,SKPaint paint)
    {
        Validate();ArgumentNullException.ThrowIfNull(vertices);ArgumentNullException.ThrowIfNull(paint);if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        var mesh=vertices.Mesh;var source=paint.ToCore(ignoreStyle:true);var material=paint.RetainedShader??(vertices.Colored?paint.MeshColorShader:null);byte opacity=paint.Color.Alpha;
        if(material is IContextualColorShader)throw new NotSupportedException("A per-triangle contextual shader is not implemented for mesh materials.");
        if(!vertices.Colored&&material is null){for(int i=0;i<mesh.TriangleCount;i++)Execute(new(CommandKind.FillPath,default,matrix,source with {Antialias=false}){Path=mesh.Geometry(i),FixedTriangleEdges=true},paint);return;}
        var shaders=mesh.Bind(material,SKPaint.GetComposite(mode));
        for(int i=0;i<mesh.TriangleCount;i++)
        {
            // A command carries the immutable engine shader directly. The
            // original paint remains its resource lease owner when recording;
            // immediate raster work needs no temporary SKShader or SKPaint.
            var drawing=source with {Antialias=false,Shader=shaders[i],Color=new(255,255,255,opacity),FilteredConstant=null};
            if(mesh.TryConstant(i,mode==SKBlendMode.Dst?null:material,out var color))
            {
                // Constant color filters need SKPaint's cached PMA path. Keep
                // the general shader route when such a filter is present.
                if(source.ColorFilter is null)drawing=drawing with {Shader=null,Color=color with {A=PixelBuffer.Multiply(color.A,opacity)}};
            }
            Execute(new(CommandKind.FillPath,default,matrix,drawing){Path=mesh.Geometry(i),ShaderTransform=matrix,FixedTriangleEdges=true},paint);
        }
    }
}
