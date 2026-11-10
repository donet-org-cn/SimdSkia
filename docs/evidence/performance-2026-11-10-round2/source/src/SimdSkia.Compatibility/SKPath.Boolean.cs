using SimdSkia;
namespace SkiaSharp;
public partial class SKPath
{
    public bool Contains(float x,float y){ThrowIfDisposed();return PathBoolean.Contains(ToCore(),x,y);}
    public bool IsConvex {get{ThrowIfDisposed();return PathBoolean.IsConvex(ToCore());}}
    public bool IsConcave=>!IsConvex;
    public SKPathConvexity Convexity=>IsConvex?SKPathConvexity.Convex:SKPathConvexity.Concave;
    public SKPath Op(SKPath other,SKPathOp op){var result=new SKPath();try{if(Op(other,op,result))return result;result.Dispose();return null!;}catch{result.Dispose();throw;}}
    public bool Op(SKPath other,SKPathOp op,SKPath result)
    {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(other);ArgumentNullException.ThrowIfNull(result);other.ThrowIfDisposed();result.ThrowIfDisposed();if(!Enum.IsDefined(op))throw new ArgumentOutOfRangeException(nameof(op));var geometry=PathBoolean.Combine(ToCore(),other.ToCore(),(int)op);result.AssignGeometry(geometry);return true;}
    private void AssignGeometry(PathGeometry geometry){segments.Clear();segments.AddRange(geometry.Segments);fillType=(SKPathFillType)geometry.FillRule;Invalidate();snapshot=geometry;}
    public SKPath Simplify(){var result=new SKPath();try{Simplify(result);return result;}catch{result.Dispose();throw;}}
    public bool Simplify(SKPath result){ThrowIfDisposed();ArgumentNullException.ThrowIfNull(result);result.ThrowIfDisposed();result.AssignGeometry(PathBoolean.Simplify(ToCore()));return true;}
    public SKPath ToWinding()=>Simplify();
    public bool ToWinding(SKPath result)=>Simplify(result);
    public class OpBuilder : SKObject
    {
        private readonly List<(PathGeometry Path,SKPathOp Op)> operands=[];
        public OpBuilder() { }
        public void Add(SKPath path,SKPathOp op){ThrowIfDisposed();ArgumentNullException.ThrowIfNull(path);if(!Enum.IsDefined(op))throw new ArgumentOutOfRangeException(nameof(op));if(operands.Count>=65536)throw new NotSupportedException("A path operation batch is limited to 65536 operands.");operands.Add((path.ToCore(),op));}
        public bool Resolve(SKPath result)
        {ThrowIfDisposed();ArgumentNullException.ThrowIfNull(result);result.ThrowIfDisposed();var geometry=new PathGeometry([]);foreach(var operand in operands)geometry=PathBoolean.Combine(geometry,operand.Path,(int)operand.Op);result.AssignGeometry(geometry);operands.Clear();return true;}
        protected override void Dispose(bool disposing)=>operands.Clear();
    }
}
