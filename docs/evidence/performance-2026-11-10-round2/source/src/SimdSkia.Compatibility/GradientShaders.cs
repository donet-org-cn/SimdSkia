namespace SimdSkia;

internal enum GradientKind { Linear,Radial,Sweep }
internal sealed class SolidColorShader(Rgba32 color):IColorShader
{
    internal Rgba32 Color=>color;
    public bool IsHorizontal=>true;
    public bool IsOpaque=>color.A==255;
    public Rgba32 Sample(float x,float y)=>color;
}

/// <summary>Immutable stops and precomputed geometry; sampling never allocates.</summary>
internal sealed class MultiStopGradient:IColorShader
{
    private readonly GradientKind kind;
    private readonly PointF start,end;
    private readonly LinearColorStop[] stops;
    private readonly int mode;
    private readonly double dx,dy,inverseLengthSquared,inverseRadius,startAngle,inverseAngle;
    private readonly bool degenerate;
    private readonly Rgba32 average;
    private LinearGradientDescription? description;
    public bool IsHorizontal=>kind==GradientKind.Linear&&start.Y==end.Y;
    public bool IsOpaque{get;}
    internal MultiStopGradient(GradientKind kind,PointF start,PointF end,float radius,LinearColorStop[] stops,int mode,float startAngle,float endAngle)
    {
        this.kind=kind;this.start=start;this.end=end;this.stops=stops;this.mode=mode;
        dx=(double)end.X-start.X;dy=(double)end.Y-start.Y;double length=dx*dx+dy*dy;
        inverseLengthSquared=length==0?0:1/length;inverseRadius=radius==0?double.PositiveInfinity:1d/radius;
        degenerate=kind==GradientKind.Linear&&length==0||kind==GradientKind.Radial&&radius==0||kind==GradientKind.Sweep&&startAngle==endAngle;
        double r=0,g=0,b=0,a=0;
        void Add(Rgba32 c,double weight){r+=c.R*weight;g+=c.G*weight;b+=c.B*weight;a+=c.A*weight;}
        Add(stops[0].Color,stops[0].Offset);Add(stops[^1].Color,1-stops[^1].Offset);
        for(int i=1;i<stops.Length;i++){double weight=(stops[i].Offset-stops[i-1].Offset)/2d;Add(stops[i-1].Color,weight);Add(stops[i].Color,weight);}
        average=new((byte)Math.Round(r),(byte)Math.Round(g),(byte)Math.Round(b),(byte)Math.Round(a));
        this.startAngle=startAngle/360d;inverseAngle=360d/(endAngle-startAngle);
        IsOpaque=mode!=3&&stops.All(s=>s.Color.A==255);
    }
    public bool TryGetLinearGradient(out LinearGradientDescription? gradient)
    {
        gradient=null;if(kind!=GradientKind.Linear||mode==3||degenerate)return false;
        gradient=description??=new(start,end,stops,(GradientExtendMode)mode);return true;
    }
    public Rgba32 Sample(float x,float y)
    {
        if(degenerate)
        {
            if(kind==GradientKind.Sweep&&mode==0&&startAngle>0&&UnitAngle(x-start.X,y-start.Y)<startAngle)return stops[0].Color;
            return mode switch{0=>stops[^1].Color,3=>default,_=>average};
        }
        double px=(double)x-start.X,py=(double)y-start.Y;
        double t=kind switch
        {
            GradientKind.Linear=>inverseLengthSquared==0?1:(px*dx+py*dy)*inverseLengthSquared,
            GradientKind.Radial=>Math.Sqrt(px*px+py*py)*inverseRadius,
            _=>(UnitAngle((float)px,(float)py)-startAngle)*inverseAngle
        };
        if(mode==3&&(t<0||t>1))return default;
        t=mode switch{1=>t-Math.Floor(t),2=>1-Math.Abs(t-2*Math.Floor(t/2)-1),_=>Math.Clamp(t,0,1)};
        if(!double.IsFinite(t))return stops[^1].Color;
        if(t<stops[0].Offset)return stops[0].Color;
        // Upper bound chooses the last repeated offset at a hard edge.
        int lo=0,hi=stops.Length;
        while(lo<hi){int mid=(lo+hi)/2;if(t<stops[mid].Offset)hi=mid;else lo=mid+1;}
        if(lo==0)return stops[0].Color;if(lo==stops.Length)return stops[^1].Color;
        var a=stops[lo-1];var b=stops[lo];double f=(t-a.Offset)/(b.Offset-a.Offset);
        byte Mix(byte u,byte v)=>(byte)Math.Clamp((int)Math.Round(u+(v-u)*f),0,255);
        return new(Mix(a.Color.R,b.Color.R),Mix(a.Color.G,b.Color.G),Mix(a.Color.B,b.Color.B),Mix(a.Color.A,b.Color.A));
    }
    // Scalar C# translation of SkRasterPipeline's xy_to_unit_angle polynomial.
    // Copyright 2018 Google Inc. BSD-3-Clause; see licenses/Skia-BSD.txt.
    // Using the baseline's float approximation also preserves which side of
    // a repeat/decal discontinuity contains pixels exactly on a diagonal.
    private static float UnitAngle(float x,float y)
    {
        float ax=MathF.Abs(x),ay=MathF.Abs(y),slope=MathF.Min(ax,ay)/MathF.Max(ax,ay),s=slope*slope;
        float phi=slope*(.15912117063999176025390625f+s*(-.05185396969318389892578125f+s*(.02476101927459239959716796875f+s*(-.0070547382347285747528076171875f))));
        if(ax<ay)phi=.25f-phi;if(x<0)phi=.5f-phi;if(y<0)phi=1-phi;
        return float.IsNaN(phi)?0:phi;
    }
}
internal sealed class TransformedColorShader(IColorShader inner,Matrix3 inverse):IColorShader
{
    private LinearGradientDescription? description;
    public bool IsHorizontal=>inner.IsHorizontal&&inverse.IsAffine&&inverse.M01==0;
    public bool IsOpaque=>inner.IsOpaque;
    public Rgba32 Sample(float x,float y)
    {
        var p=inverse.Map(x,y);
        if(!float.IsFinite(p.X)||!float.IsFinite(p.Y))throw new NotSupportedException("Sampling on a projective shader horizon is not implemented.");
        return inner.Sample(p.X,p.Y);
    }
    public bool TryGetLinearGradient(out LinearGradientDescription? gradient)
    {
        gradient=null;if(!inverse.IsAffine)return false;
        if(description is not null){gradient=description;return true;}
        LinearGradientDescription? source;
        if(inner is LinearGradient linear)source=new(linear.Start,linear.End,[new(0,linear.StartColor),new(1,linear.EndColor)]);
        else if(!inner.TryGetLinearGradient(out source)||source is null)return false;
        gradient=description=new(source.Start,source.End,source.Stops.ToArray(),source.Extend,source.SampleTransform*inverse);return true;
    }
}
