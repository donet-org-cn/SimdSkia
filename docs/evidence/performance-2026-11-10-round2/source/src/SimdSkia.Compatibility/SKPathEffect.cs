// Path effects translated from the fixed Skia m119 algorithms (BSD-3-Clause).
// See docs/licenses/Skia-BSD.txt. Geometry and distance evaluation remain managed.
using SimdSkia;
namespace SkiaSharp;

public class SKPathEffect : SKObject
{
    internal readonly record struct Result(PathGeometry Geometry,float Width,SKPaintStyle Style)
    {internal bool Applied {get;init;}=true;}
    internal abstract record Node
    {
        internal abstract Result Apply(Result source,int depth=0);
        protected static void Budget(int count,int depth){if(count>100000||depth>64)throw new NotSupportedException("Path-effect expansion exceeds 100,000 stamps/segments or 64 graph levels.");}
    }
    private Node? node;
    private SKPathEffect(Node node)=>this.node=node;
    internal Node ToCore(){ThrowIfDisposed();return node!;}
    protected override void Dispose(bool disposing)=>node=null;
    public static SKPathEffect CreateCompose(SKPathEffect outer,SKPathEffect inner)
    {ArgumentNullException.ThrowIfNull(outer);ArgumentNullException.ThrowIfNull(inner);return new( new Compose(outer.ToCore(),inner.ToCore(),false));}
    public static SKPathEffect CreateSum(SKPathEffect first,SKPathEffect second)
    {ArgumentNullException.ThrowIfNull(first);ArgumentNullException.ThrowIfNull(second);return new(new Compose(first.ToCore(),second.ToCore(),true));}
    public static SKPathEffect CreateDash(float[] intervals,float phase)
    {
        ArgumentNullException.ThrowIfNull(intervals);if(intervals.Length%2!=0)throw new ArgumentException("The intervals must have an even number of entries.",nameof(intervals));
        if(intervals.Length<2||!float.IsFinite(phase)||intervals.Any(v=>!float.IsFinite(v)||v<0))return null!;
        float sum=0;foreach(float v in intervals)sum+=v;if(!(sum>0)||!float.IsFinite(sum))return null!;
        phase%=sum;if(phase<0)phase+=sum;return new(new Dash((float[])intervals.Clone(),phase,sum));
    }
    public static SKPathEffect CreateCorner(float radius)=>radius>0&&float.IsFinite(radius)?new(new Corner(radius)):null!;
    public static SKPathEffect CreateDiscrete(float segLength,float deviation,uint seedAssist=0)=>segLength>1f/4096&&float.IsFinite(segLength)&&float.IsFinite(deviation)?new(new Discrete(segLength,deviation,seedAssist)):null!;
    public static SKPathEffect CreateTrim(float start,float stop)=>CreateTrim(start,stop,SKTrimPathEffectMode.Normal);
    public static SKPathEffect CreateTrim(float start,float stop,SKTrimPathEffectMode mode)
    {
        if(!float.IsFinite(start)||!float.IsFinite(stop)||!Enum.IsDefined(mode))return null!;
        if(start<=0&&stop>=1&&mode==SKTrimPathEffectMode.Normal)return null!;
        start=Math.Clamp(start,0,1);stop=Math.Clamp(stop,0,1);return start>=stop&&mode==SKTrimPathEffectMode.Inverted?null!:new(new Trim(start,stop,mode));
    }
    public static SKPathEffect Create1DPath(SKPath path,float advance,float phase,SKPath1DPathEffectStyle style)
    {
        ArgumentNullException.ThrowIfNull(path);var geometry=path.ToCore();if(geometry.Segments.Length==0||!(advance>0)||!float.IsFinite(advance)||!float.IsFinite(phase)||!Enum.IsDefined(style))return null!;
        float offset=phase<0?-phase%advance:advance-phase%advance;if(offset>=advance)offset=0;return new(new Stamp1D(geometry,advance,offset,style));
    }
    public static SKPathEffect Create2DLine(float width,SKMatrix matrix)=>width>=0&&float.IsFinite(width)?new(new Lattice(matrix.ToCore(),width,null)):null!;
    public static SKPathEffect Create2DPath(SKMatrix matrix,SKPath path)
    {ArgumentNullException.ThrowIfNull(path);return new(new Lattice(matrix.ToCore(),0,path.ToCore()));}
    private sealed record Compose(Node Outer,Node Inner,bool Sum):Node
    {
        internal override Result Apply(Result source,int depth=0)
        {
            Budget(0,depth);
            if(!Sum){var first=Inner.Apply(source,depth+1);var result=Outer.Apply(first.Applied?first:source,depth+1);return result.Applied?result:source with{Applied=false};}
            var outer=Outer.Apply(source,depth+1);var inner=Inner.Apply(source with{Width=outer.Width,Style=outer.Style},depth+1);
            if(!outer.Applied)return inner.Applied?inner:source with{Applied=false};if(!inner.Applied)return outer;
            if(inner.Width!=outer.Width||inner.Style!=outer.Style){inner=Bake(inner);outer=Bake(outer);}
            return inner with {Geometry=Join(inner.Geometry,outer.Geometry)};
        }
    }
    private static Result Bake(Result value)
    {
        if(value.Style==SKPaintStyle.Fill)return value;
        if(value.Width==0)throw new NotSupportedException("Summing different hairline effect styles requires a device transform.");
        var outline=StrokeGeometry.Create(value.Geometry.Flatten(Matrix3.Identity),value.Width,4,StrokeCap.Butt,StrokeJoin.Miter);
        return new(outline,0,SKPaintStyle.Fill);
    }
    private static PathGeometry Join(PathGeometry a,PathGeometry b)
    {var segments=new List<PathSegment>(a.Segments.Length+b.Segments.Length);segments.AddRange(a.Segments.ToArray());segments.AddRange(b.Segments.ToArray());return new(segments,a.FillRule);}
    private sealed record Trim(float Start,float Stop,SKTrimPathEffectMode Mode):Node
    {
        internal override Result Apply(Result source,int depth=0)
        {
            Budget(0,depth);using var src=new SKPath(source.Geometry);using var dst=new SKPath();using var measure=new SKPathMeasure(src);float length=0;int contours=0;
            do{length+=measure.Length;contours++;}while(measure.NextContour());
            void Add(float start,float stop,bool move=true)
            {
                if(start>=stop)return;measure.SetPath(src);float offset=0;
                do{float end=offset+measure.Length;if(start<end)measure.GetSegment(start-offset,stop-offset,dst,move);if(stop<=end)break;offset=end;}while(measure.NextContour());
            }
            if(Mode==SKTrimPathEffectMode.Normal){if(Start<Stop)Add(length*Start,length*Stop);}
            else{Add(length*Stop,length);Add(0,length*Start,!(contours==1&&source.Geometry.Segments.Length>0&&source.Geometry.Segments[^1].Verb==PathVerb.Close&&Stop<1));}
            return source with {Geometry=dst.ToCore()};
        }
    }
    private sealed record Dash(float[] Intervals,float Phase,float Sum):Node
    {
        internal override Result Apply(Result source,int depth=0)
        {
            Budget(0,depth);if(source.Style==SKPaintStyle.Fill)return source with{Applied=false};
            using var src=new SKPath(source.Geometry);using var dst=new SKPath();using var measure=new SKPathMeasure(src);
            int count=0;
            do
            {
                float length=measure.Length;if(length<=0)continue;
                Budget((int)Math.Min(100001,(double)length/Sum*Intervals.Length),depth);
                int index=0;float phase=Phase;
                while(index<Intervals.Length-1&&phase>=Intervals[index]){phase-=Intervals[index];index++;}
                float remaining=Intervals[index]-phase,position=0;float firstEnd=-1;bool lastConnected=false;
                while(position<length)
                {
                    Budget(++count,depth);float next=MathF.Min(length,position+remaining);
                    if((index&1)==0&&next>position)
                    {
                        if(position==0&&measure.IsClosed)firstEnd=next;
                        else{measure.GetSegment(position,next,dst,true);lastConnected=next==length;}
                    }
                    position=next;index=(index+1)%Intervals.Length;remaining=Intervals[index];
                }
                if(firstEnd>0){measure.GetSegment(0,firstEnd,dst,!lastConnected);if(firstEnd==length)dst.Close();}
            }while(measure.NextContour());
            return source with {Geometry=dst.ToCore()};
        }
    }
    private sealed record Corner(float Radius):Node
    {
        internal override Result Apply(Result source,int depth=0)
        {
            Budget(0,depth);using var src=new SKPath(source.Geometry);using var dst=new SKPath();using var iterator=src.CreateIterator(false);Span<SKPoint> p=stackalloc SKPoint[4];
            SKPathVerb previous=SKPathVerb.Done;bool valid=true;SKPoint move=default,last=default,firstStep=default,step=default;
            while(true)
            {
                var verb=iterator.Next(p);
                switch(verb)
                {
                    case SKPathVerb.Move:
                        if(previous==SKPathVerb.Line)dst.LineTo(last);
                        if(iterator.IsCloseContour()){move=p[0];valid=false;}else{dst.MoveTo(p[0]);valid=true;}
                        break;
                    case SKPathVerb.Line:
                        float dx=p[1].X-p[0].X,dy=p[1].Y-p[0].Y,distance=MathF.Sqrt(dx*dx+dy*dy);bool line=distance>Radius*2;float scale=line?Radius/distance:.5f;step=new(dx*scale,dy*scale);
                        if(!valid){dst.MoveTo(move+step);valid=true;}else dst.QuadTo(p[0],p[0]+step);
                        if(line)dst.LineTo(p[1]-step);last=p[1];valid=true;break;
                    case SKPathVerb.Quad:
                    case SKPathVerb.Conic:
                    case SKPathVerb.Cubic:
                        if(!valid){dst.MoveTo(p[0]);valid=true;}
                        if(verb==SKPathVerb.Quad)dst.QuadTo(p[1],p[2]);else if(verb==SKPathVerb.Conic)dst.ConicTo(p[1],p[2],iterator.ConicWeight());else dst.CubicTo(p[1],p[2],p[3]);
                        last=p[verb==SKPathVerb.Cubic?3:2];firstStep=default;break;
                    case SKPathVerb.Close:
                        if(firstStep!=default)dst.QuadTo(last,last+firstStep);dst.Close();valid=false;break;
                    case SKPathVerb.Done:
                        if(valid)dst.LineTo(last);return source with {Geometry=dst.ToCore()};
                }
                if(previous==SKPathVerb.Move)firstStep=step;previous=verb;
            }
        }
    }
    private sealed record Discrete(float Segment,float Deviation,uint Seed):Node
    {
        internal override Result Apply(Result source,int depth=0)
        {
            Budget(0,depth);bool fill=source.Style==SKPaintStyle.Fill;using var src=new SKPath(source.Geometry);using var dst=new SKPath();using var measure=new SKPathMeasure(src,fill);
            uint seed=Seed^(uint)MathF.Floor(measure.Length+.5f);seed^=(seed<<16)|(seed>>16);
            SKPoint Perturb(SKPoint p,SKPoint tangent)
            {seed=unchecked(seed*1664525+1013904223);float amount=((int)seed>>15)*(1f/65536)*Deviation;return new(p.X+tangent.Y*amount,p.Y-tangent.X*amount);}
            do
            {
                float length=measure.Length;if(Segment*(fill?3:2)>length){measure.GetSegment(0,length,dst,true);continue;}
                int n=Math.Min(100000,(int)MathF.Floor(length/Segment+.5f));float delta=length/n,distance=0;
                if(measure.IsClosed){n--;distance=delta*.5f;}
                if(measure.GetPositionAndTangent(distance,out var p,out var tangent))dst.MoveTo(Perturb(p,tangent));
                while(--n>=0){distance+=delta;if(measure.GetPositionAndTangent(distance,out p,out tangent))dst.LineTo(Perturb(p,tangent));}
                if(measure.IsClosed)dst.Close();
            }while(measure.NextContour());
            return source with {Geometry=dst.ToCore()};
        }
    }
    private sealed record Stamp1D(PathGeometry Stamp,float Advance,float Offset,SKPath1DPathEffectStyle Style):Node
    {
        internal override Result Apply(Result source,int depth=0)
        {
            Budget(0,depth);using var src=new SKPath(source.Geometry);using var dst=new SKPath();using var measure=new SKPathMeasure(src);int stamps=0;
            do
            {
                Budget((int)Math.Min(100001,(double)measure.Length/Advance),depth);
                for(float distance=Offset;distance<measure.Length;)
                {
                    Budget(++stamps,depth);
                    if((long)stamps*Stamp.Segments.Length>1000000)throw new NotSupportedException("Path-effect stamp geometry exceeds one million segments.");
                    if(Style!=SKPath1DPathEffectStyle.Morph)
                    {
                        if(measure.GetPositionAndTangent(distance,out var p,out var t)){var m=Style==SKPath1DPathEffectStyle.Translate?SKMatrix.CreateTranslation(p.X,p.Y):new SKMatrix(t.X,-t.Y,p.X,t.Y,t.X,p.Y,0,0,1);dst.AppendGeometry(Stamp,m.ToCore());}
                    }
                    else Morph(dst,Stamp,measure,distance);
                    float next=distance+Advance;if(next<=distance)throw new NotSupportedException("Stamp advance is below floating-point path resolution.");distance=next;
                }
            }while(measure.NextContour());
            return new(dst.ToCore(),0,SKPaintStyle.Fill);
        }
        private static void Morph(SKPath dst,PathGeometry stamp,SKPathMeasure measure,float distance)
        {
            PointF current=default,start=default;
            SKPoint Map(PointF p){measure.GetPositionAndTangent(distance+p.X,out var position,out var tangent);return new(position.X-p.Y*tangent.Y,position.Y+p.Y*tangent.X);}
            foreach(var s in stamp.Segments)
            {
                switch(s.Verb)
                {
                    case PathVerb.Move:dst.MoveTo(Map(s.A));start=current=s.A;break;
                    case PathVerb.Line:dst.QuadTo(Map((current+s.A)*.5f),Map(s.A));current=s.A;break;
                    case PathVerb.Quadratic:dst.QuadTo(Map(s.A),Map(s.B));current=s.B;break;
                    case PathVerb.Conic:dst.ConicTo(Map(s.A),Map(s.B),s.ConicWeight);current=s.B;break;
                    case PathVerb.Cubic:dst.CubicTo(Map(s.A),Map(s.B),Map(s.C));current=s.C;break;
                    case PathVerb.Close:if(current!=start)dst.QuadTo(Map((current+start)*.5f),Map(start));dst.Close();current=start;break;
                }
            }
        }
    }
    private sealed record Lattice(Matrix3 Matrix,float Width,PathGeometry? Stamp):Node
    {
        internal override Result Apply(Result source,int depth=0)
        {
            Budget(0,depth);if(!Matrix.IsFinite||!Matrix.IsAffine||!Matrix.TryInvert(out var inverse))return source with{Applied=false};
            using var src=new SKPath(source.Geometry.Transform(inverse));using var dst=new SKPath();var b=src.Bounds;var bounds=new SKRectI((int)MathF.Floor(b.Left+.5f),(int)MathF.Floor(b.Top+.5f),(int)MathF.Floor(b.Right+.5f),(int)MathF.Floor(b.Bottom+.5f));
            if((double)bounds.Width*bounds.Height>10000000)throw new NotSupportedException("Path-effect lattice exceeds ten million candidate cells.");
            using var clip=new SKRegion(bounds);using var region=new SKRegion();region.SetPath(src,clip);using var iterator=region.CreateRectIterator();int count=0;
            while(iterator.Next(out var r))
            {
                for(int y=r.Top;y<r.Bottom;y++)
                {
                    if(Stamp is null){if(r.Width<=1)continue;Budget(++count,depth);var a=Matrix.Map(r.Left+.5f,y+.5f);var z=Matrix.Map(r.Right+.5f,y+.5f);dst.MoveTo(a.X,a.Y);dst.LineTo(z.X,z.Y);}
                    else for(int x=r.Left;x<r.Right;x++){Budget(++count,depth);if((long)count*Stamp.Segments.Length>1000000)throw new NotSupportedException("Lattice stamp geometry exceeds one million segments.");var p=Matrix.Map(x+.5f,y+.5f);dst.AppendGeometry(Stamp,SKMatrix.CreateTranslation(p.X,p.Y).ToCore());}
                }
            }
            return new(dst.ToCore(),Stamp is null?Width:source.Width,Stamp is null?SKPaintStyle.Stroke:source.Style);
        }
    }
}
