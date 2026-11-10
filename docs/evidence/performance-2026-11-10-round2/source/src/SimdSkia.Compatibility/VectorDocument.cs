using System.Globalization;
using System.Text;
using SimdSkia;
namespace SkiaSharp;

/// <summary>Replays immutable commands into vector document writers. It does
/// not rasterize a picture to hide commands that a format cannot represent.</summary>
internal abstract class VectorDocument(SKRect bounds)
{
    protected readonly SKRect Bounds=bounds;
    protected abstract void Save();
    protected abstract void Restore();
    protected abstract void Clip(PathGeometry path,Matrix3 matrix,SKClipOperation operation);
    protected abstract void Layer(SKCanvasSaveLayerRec layer,Matrix3 matrix);
    protected abstract void Path(PathGeometry path,Matrix3 matrix,Paint paint,Matrix3 shaderMatrix);
    protected abstract void Image(DrawingCommand command);
    protected abstract void Annotation(CanvasAnnotation annotation,Matrix3 matrix);
    internal void Replay(PictureCommands commands)
    {
        bool painted=false;
        foreach(var op in commands.Items)
        {
            switch(op.Kind)
            {
                case PictureCommands.Kind.Save:Save();break;
                case PictureCommands.Kind.Restore:Restore();break;
                case PictureCommands.Kind.Clip:Clip(op.Path!,op.Matrix,op.ClipOperation);break;
                case PictureCommands.Kind.Layer:Layer(op.Layer,op.Matrix);break;
                case PictureCommands.Kind.Annotation:Annotation(op.Annotation!,op.Matrix);break;
                case PictureCommands.Kind.Clear:
                    if(op.Command.Paint.Color.A==0&&!painted)break;
                    if(op.Command.Paint.Color.A!=255)throw new NotSupportedException("A transparent Source clear over existing vectors requires an erasure mask not implemented by this document writer.");
                    Path(PathGeometry.Rectangle(Bounds.ToCore()),Matrix3.Identity,new(op.Command.Paint.Color,CompositeMode.SourceOver),Matrix3.Identity);painted=true;break;
                case PictureCommands.Kind.Paint:Path(PathGeometry.Rectangle(Bounds.ToCore()),Matrix3.Identity,op.Command.Paint,op.Matrix);painted=true;break;
                case PictureCommands.Kind.Draw:
                    var c=op.Command;
                    if(c.Kind==CommandKind.DrawImage){Image(c);painted=true;break;}
                    var geometry=c.Kind switch
                    {CommandKind.FillRectangle=>PathGeometry.Rectangle(c.Rectangle),CommandKind.FillPath=>c.Path!,CommandKind.DrawGlyphRun=>GlyphPath(c.GlyphRun!),_=>throw new NotSupportedException($"Vector document command {c.Kind} is not implemented.")};
                    Path(geometry,c.Transform,c.Paint,c.ShaderTransform??c.Transform);painted=true;break;
                default:throw new InvalidOperationException("Unknown recorded document operation.");
            }
        }
    }
    private static PathGeometry GlyphPath(GlyphRunGeometry run)
    {var segments=new List<PathSegment>();foreach(var glyph in run.Glyphs)glyph.Outline.AppendTransformedTo(segments,new Matrix3(1,0,glyph.Position.X,0,1,glyph.Position.Y,0,0,1)*run.FontTransform);return new(segments);}
    protected static string Number(float value)
    {if(!float.IsFinite(value))throw new NotSupportedException("Document coordinates must be finite.");return value.ToString("R",CultureInfo.InvariantCulture);}
    protected static float Opacity(Paint paint)=>paint.FloatingColor?.W??paint.Color.A/255f;
    protected static string MatrixValues(Matrix3 m)=>string.Join(" ",new[]{m.M00,m.M10,m.M01,m.M11,m.M02,m.M12}.Select(Number));
    protected static string PathData(PathGeometry geometry,Matrix3 transform)
    {
        var text=new StringBuilder();PointF current=default,start=default;int emitted=0;
        void Point(PointF p){text.Append(Number(p.X)).Append(' ').Append(Number(p.Y)).Append(' ');}
        void Cubic(PointF a,PointF b,PointF c){if(++emitted>1_000_000)throw new NotSupportedException("Vector path exceeds one million emitted segments.");text.Append("C ");Point(a);Point(b);Point(c);}
        foreach(var s in geometry.Segments)
        {
            switch(s.Verb)
            {
                case PathVerb.Move:text.Append("M ");Point(s.A);current=start=s.A;break;
                case PathVerb.Line:text.Append("L ");Point(s.A);current=s.A;break;
                case PathVerb.Quadratic:text.Append("Q ");Point(s.A);Point(s.B);current=s.B;break;
                case PathVerb.Cubic:Cubic(s.A,s.B,s.C);current=s.C;break;
                case PathVerb.Close:text.Append("Z ");current=start;break;
                case PathVerb.Conic:
                    if(s.ConicWeight==0){text.Append("L ");Point(s.B);current=s.B;break;}
                    if(s.ConicWeight==1){text.Append("Q ");Point(s.A);Point(s.B);current=s.B;break;}
                    var p0=current;var p1=s.A;var p2=s.B;double w=s.ConicWeight;
                    (PointF Point,PointF Derivative) Evaluate(double t)
                    {double u=1-t,nx=p0.X*u*u+2*w*p1.X*u*t+p2.X*t*t,ny=p0.Y*u*u+2*w*p1.Y*u*t+p2.Y*t*t,d=u*u+2*w*u*t+t*t,dx=2*(p0.X*(t-1)+w*p1.X*(1-2*t)+p2.X*t),dy=2*(p0.Y*(t-1)+w*p1.Y*(1-2*t)+p2.Y*t),dd=2*((t-1)+w*(1-2*t)+t);return(new((float)(nx/d),(float)(ny/d)),new((float)((dx*d-nx*dd)/(d*d)),(float)((dy*d-ny*dd)/(d*d))));}
                    void Emit(double a,double b,int depth)
                    {
                        var left=Evaluate(a);var right=Evaluate(b);float step=(float)((b-a)/3);var ca=left.Point+left.Derivative*step;var cb=right.Point-right.Derivative*step;double error=0;
                        foreach(float t in new[]{.25f,.5f,.75f}){float u=1-t;var fitted=left.Point*(u*u*u)+ca*(3*u*u*t)+cb*(3*u*t*t)+right.Point*(t*t*t);var exact=Evaluate(a+(b-a)*t).Point;var delta=fitted-exact;float dx=transform.M00*delta.X+transform.M01*delta.Y,dy=transform.M10*delta.X+transform.M11*delta.Y;error=Math.Max(error,(double)dx*dx+(double)dy*dy);}
                        if(error<=1d/(1024*1024)){Cubic(ca,cb,right.Point);return;}if(depth==12)throw new NotSupportedException("A conic exceeds the vector export precision/subdivision budget.");double mid=(a+b)*.5;Emit(a,mid,depth+1);Emit(mid,b,depth+1);
                    }
                    Emit(0,1,0);current=s.B;break;
                default:throw new NotSupportedException("Unknown path verb.");
            }
        }
        return text.ToString();
    }
    protected static LinearGradientDescription? Gradient(IColorShader? shader)
    {
        if(shader is LinearGradient g)return new(g.Start,g.End,[new(0,g.StartColor),new(1,g.EndColor)]);
        if(shader is not null&&shader.TryGetLinearGradient(out var linear))return linear;
        return null;
    }
    protected static void ValidatePaint(Paint paint)
    {
        if(paint.Blender is not null||paint.Dither)throw new NotSupportedException("This vector writer has not implemented custom blending/dither serialization.");
        if(paint.ColorFilter is not null||paint.ImageFilter is not null||paint.MaskFilter is not null)throw new NotSupportedException("This vector writer has not implemented paint/filter graph serialization.");
        if(paint.Composite!=CompositeMode.SourceOver)throw new NotSupportedException("This vector writer supports SourceOver composition; other blend modes require format-specific serialization.");
    }
}
