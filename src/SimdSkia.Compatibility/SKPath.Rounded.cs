namespace SkiaSharp;
public partial class SKPath
{
    public bool IsRoundRect{get{ThrowIfDisposed();return roundRectTag is not null;}}
    public SKRoundRect GetRoundRect(){ThrowIfDisposed();return roundRectTag is null?null!:new(roundRectTag);}
    public void AddRoundRect(SKRoundRect rect,SKPathDirection direction,uint startIndex)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(rect);rect.ThrowIfDisposed();if(!Enum.IsDefined(direction))throw new ArgumentOutOfRangeException(nameof(direction));
        bool shape=segments.All(s=>s.Verb==SimdSkia.PathVerb.Move);var b=rect.Rect;int start=(int)(startIndex&7),step=direction==SKPathDirection.Clockwise?1:-1;
        if(rect.Type is SKRoundRectType.Empty or SKRoundRectType.Rect)
        {
            Span<SKPoint> corners=[new(b.Left,b.Top),new(b.Right,b.Top),new(b.Right,b.Bottom),new(b.Left,b.Bottom)];int at=(int)((startIndex+1)/2)&3;MoveTo(corners[at]);for(int i=1;i<4;i++)LineTo(corners[(at+step*i+4)&3]);Close();return;
        }
        if(rect.Type==SKRoundRectType.Oval){AddOvalCore(b,direction,((int)(startIndex/2)+3)&3);return;}
        var r0=rect.GetRadii(SKRoundRectCorner.UpperLeft);var r1=rect.GetRadii(SKRoundRectCorner.UpperRight);var r2=rect.GetRadii(SKRoundRectCorner.LowerRight);var r3=rect.GetRadii(SKRoundRectCorner.LowerLeft);
        Span<SKPoint> points=[new(b.Left+r0.X,b.Top),new(b.Right-r1.X,b.Top),new(b.Right,b.Top+r1.Y),new(b.Right,b.Bottom-r2.Y),new(b.Right-r2.X,b.Bottom),new(b.Left+r3.X,b.Bottom),new(b.Left,b.Bottom-r3.Y),new(b.Left,b.Top+r0.Y)];
        Span<SKPoint> controls=[new(b.Left,b.Top),new(b.Right,b.Top),new(b.Right,b.Bottom),new(b.Left,b.Bottom)];
        bool startsWithConic=(start&1)==(step>0?1:0);int count=startsWithConic?7:8;MoveTo(points[start]);
        for(int i=0;i<count;i++){int at=(start+step*i+8)&7,next=(at+step+8)&7;bool conic=(at&1)==(step>0?1:0);if(conic)ConicTo(controls[(at/2+(step>0?1:0))&3],points[next],.7071067811865476f);else LineTo(points[next]);}
        Close();if(shape)roundRectTag=new(rect);
    }
    [Obsolete("Use Transform(in SKMatrix) instead.",true)]
    public void Transform(SKMatrix matrix)=>Transform(in matrix);
    [Obsolete("Use Transform(in SKMatrix matrix, SKPath destination) instead.",true)]
    public void Transform(SKMatrix matrix,SKPath destination)=>Transform(in matrix,destination);
}
