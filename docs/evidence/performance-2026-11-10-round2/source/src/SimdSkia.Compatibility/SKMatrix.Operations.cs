namespace SkiaSharp;

public partial struct SKMatrix
{
    public SKMatrix(float[] values){ArgumentNullException.ThrowIfNull(values);if(values.Length!=9)throw new ArgumentException("The matrix needs nine values.",nameof(values));this=new(values[0],values[1],values[2],values[3],values[4],values[5],values[6],values[7],values[8]);}
    public readonly void GetValues(float[] values){ArgumentNullException.ThrowIfNull(values);if(values.Length!=9)throw new ArgumentException("The matrix needs nine values.",nameof(values));values[0]=ScaleX;values[1]=SkewX;values[2]=TransX;values[3]=SkewY;values[4]=ScaleY;values[5]=TransY;values[6]=Persp0;values[7]=Persp1;values[8]=Persp2;}
    public readonly SKMatrix Invert()=>TryInvert(out var inverse)?inverse:Empty;
    public static SKMatrix CreateScaleTranslation(float sx,float sy,float tx,float ty)=>new(sx,0,tx,0,sy,ty,0,0,1);

    public readonly bool IsInvertible=>ToCore().TryInvert(out _);
    public float[] Values
    {
        readonly get=>[ScaleX,SkewX,TransX,SkewY,ScaleY,TransY,Persp0,Persp1,Persp2];
        set{ArgumentNullException.ThrowIfNull(value,nameof(Values));if(value.Length!=9)throw new ArgumentException("Matrix needs nine values.",nameof(Values));this=new(value[0],value[1],value[2],value[3],value[4],value[5],value[6],value[7],value[8]);}
    }
    public static SKMatrix CreateScale(float x,float y,float pivotX,float pivotY)=>new(x,0,pivotX*(1-x),0,y,pivotY*(1-y),0,0,1);
    public static SKMatrix CreateSkew(float x,float y)=>new(1,x,0,y,1,0,0,0,1);
    public static SKMatrix CreateRotation(float radians,float pivotX,float pivotY)
    {
        var m=CreateRotation(radians);m.TransX=pivotX-m.ScaleX*pivotX-m.SkewX*pivotY;m.TransY=pivotY-m.SkewY*pivotX-m.ScaleY*pivotY;return m;
    }
    public static SKMatrix CreateRotationDegrees(float degrees,float pivotX,float pivotY)=>CreateRotation(degrees*MathF.PI/180,pivotX,pivotY);
    public static SKMatrix Concat(SKMatrix first,SKMatrix second)=>FromCore(first.ToCore()*second.ToCore());
    public static void Concat(ref SKMatrix target,SKMatrix first,SKMatrix second)=>target=Concat(first,second);
    public readonly SKMatrix PreConcat(SKMatrix matrix)=>Concat(this,matrix);
    public readonly SKMatrix PostConcat(SKMatrix matrix)=>Concat(matrix,this);
    public readonly SKPoint MapVector(SKPoint vector)=>MapVector(vector.X,vector.Y);
    public readonly SKPoint MapVector(float x,float y)
    {
        if(Persp0==0&&Persp1==0&&Persp2==1)return new(ScaleX*x+SkewX*y,SkewY*x+ScaleY*y);
        var p=MapPoint(x,y);var origin=MapPoint(0,0);return new(p.X-origin.X,p.Y-origin.Y);
    }
    public readonly SKPoint[] MapPoints(SKPoint[] points){ArgumentNullException.ThrowIfNull(points);var result=new SKPoint[points.Length];MapPoints(result.AsSpan(),points);return result;}
    public readonly void MapPoints(SKPoint[] result,SKPoint[] points){ArgumentNullException.ThrowIfNull(result);ArgumentNullException.ThrowIfNull(points);MapPoints(result.AsSpan(),points);}
    public readonly void MapPoints(Span<SKPoint> result,ReadOnlySpan<SKPoint> points)=>Map(result,points,false);
    public readonly SKPoint[] MapVectors(SKPoint[] vectors){ArgumentNullException.ThrowIfNull(vectors);var result=new SKPoint[vectors.Length];MapVectors(result.AsSpan(),vectors);return result;}
    public readonly void MapVectors(SKPoint[] result,SKPoint[] vectors){ArgumentNullException.ThrowIfNull(result);ArgumentNullException.ThrowIfNull(vectors);MapVectors(result.AsSpan(),vectors);}
    public readonly void MapVectors(Span<SKPoint> result,ReadOnlySpan<SKPoint> vectors)=>Map(result,vectors,true);
    private readonly void Map(Span<SKPoint> result,ReadOnlySpan<SKPoint> points,bool vectors)
    {
        if(result.Length!=points.Length)throw new ArgumentException("Result and input lengths must match.");
        // Reverse traversal protects shifted, overlapping spans as well as exact in-place use.
        if(points.Overlaps(result,out int offset)&&offset>0){for(int i=points.Length-1;i>=0;i--)result[i]=vectors?MapVector(points[i]):MapPoint(points[i]);}
        else for(int i=0;i<points.Length;i++)result[i]=vectors?MapVector(points[i]):MapPoint(points[i]);
    }
    public readonly SKRect MapRect(SKRect source)
    {
        var a=MapPoint(source.Left,source.Top);var b=MapPoint(source.Right,source.Top);var c=MapPoint(source.Right,source.Bottom);var d=MapPoint(source.Left,source.Bottom);
        return new(MathF.Min(MathF.Min(a.X,b.X),MathF.Min(c.X,d.X)),MathF.Min(MathF.Min(a.Y,b.Y),MathF.Min(c.Y,d.Y)),MathF.Max(MathF.Max(a.X,b.X),MathF.Max(c.X,d.X)),MathF.Max(MathF.Max(a.Y,b.Y),MathF.Max(c.Y,d.Y)));
    }
    public readonly float MapRadius(float radius)
    {
        var x=MapVector(radius,0);var y=MapVector(0,radius);return MathF.Sqrt(MathF.Sqrt(x.X*x.X+x.Y*x.Y)*MathF.Sqrt(y.X*y.X+y.Y*y.Y));
    }
}
