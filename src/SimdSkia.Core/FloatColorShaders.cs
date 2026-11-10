using System.Numerics;
namespace SimdSkia;

public sealed class DestinationColorShader(IPremultipliedColorShader legacy,IPremultipliedColorShader srgb) : IPremultipliedColorShader,IPremultipliedBatchShader
{
    public IColorShader ForDestination(bool taggedSrgb)=>taggedSrgb?srgb:legacy;
    public bool IsHorizontal=>legacy.IsHorizontal;
    public bool IsOpaque=>legacy.IsOpaque;
    public Vector4 SamplePremultiplied(float x,float y)=>legacy.SamplePremultiplied(x,y);
    void IPremultipliedBatchShader.SampleBatch(Span<Vector4> samples)
    {if(legacy is IPremultipliedBatchShader batch)batch.SampleBatch(samples);else for(int i=0;i<samples.Length;i++)samples[i]=legacy.SamplePremultiplied(samples[i].X,samples[i].Y);}
    public Rgba32 Sample(float x,float y)=>legacy.Sample(x,y);
}

public sealed class FloatColorShader(Vector4 straight) : IPremultipliedColorShader
{
    private readonly Vector4 value=new(straight.X*straight.W,straight.Y*straight.W,straight.Z*straight.W,straight.W);
    public bool IsHorizontal=>true;
    public bool IsOpaque=>value.W>=1;
    public Vector4 SamplePremultiplied(float x,float y)=>value;
    public Rgba32 Sample(float x,float y)=>new(ColorBlend.Byte(straight.X),ColorBlend.Byte(straight.Y),ColorBlend.Byte(straight.Z),ColorBlend.Byte(straight.W));
}
public enum FloatGradientKind {Linear,Radial,Sweep,Conical}
public sealed class FloatGradientShader : IPremultipliedColorShader,IPremultipliedBatchShader
{
    private readonly FloatGradientKind kind;
    private readonly Vector2 first,second;
    private readonly float radius0,radius1,startAngle,endAngle;
    private readonly Vector4[] colors;
    private readonly float[] offsets;
    private readonly int tileMode;
    private readonly Matrix3 inverse;
    private readonly Vector2 direction;
    private readonly float lengthSquared;
    private readonly bool opaque,horizontal;
    public bool IsOpaque=>opaque;
    public bool IsHorizontal=>horizontal;
    public FloatGradientShader(FloatGradientKind kind,Vector2 first,Vector2 second,float radius0,float radius1,Vector4[] colors,float[] offsets,int tileMode,float startAngle,float endAngle,Matrix3 inverse)
    {
        this.kind=kind;this.first=first;this.second=second;this.radius0=radius0;this.radius1=radius1;this.colors=(Vector4[])colors.Clone();this.offsets=(float[])offsets.Clone();this.tileMode=tileMode;this.startAngle=startAngle;this.endAngle=endAngle;this.inverse=inverse;
        opaque=tileMode!=3&&kind!=FloatGradientKind.Conical&&colors.All(c=>c.W>=1);
        direction=second-first;lengthSquared=direction.LengthSquared();horizontal=kind==FloatGradientKind.Linear&&direction.X*inverse.M01+direction.Y*inverse.M11==0;
    }
    public Vector4 SamplePremultiplied(float x,float y)
    {
        var point=inverse.Map(x,y);var q=new Vector2(point.X,point.Y)-first;float t;
        switch(kind)
        {
            case FloatGradientKind.Linear:t=lengthSquared>0?Vector2.Dot(q,direction)/lengthSquared:1;break;
            case FloatGradientKind.Radial:t=radius0>0?q.Length()/radius0:1;break;
            case FloatGradientKind.Sweep:
                float angle=MathF.Atan2(q.Y,q.X)*(180/MathF.PI);if(angle<0)angle+=360;
                t=endAngle>startAngle?(angle-startAngle)/(endAngle-startAngle):1;break;
            case FloatGradientKind.Conical:
                var delta=second-first;double dr=(double)radius1-radius0;
                double a=Vector2.Dot(delta,delta)-dr*dr,b=-2*((double)q.X*delta.X+(double)q.Y*delta.Y+radius0*dr),c=Vector2.Dot(q,q)-(double)radius0*radius0;
                if(Math.Abs(a)<1e-12){if(Math.Abs(b)<1e-12)return default;t=(float)(-c/b);}
                else
                {
                    double determinant=b*b-4*a*c;if(determinant<0)return default;
                    double root=Math.Sqrt(determinant),t0=(-b-root)/(2*a),t1=(-b+root)/(2*a);
                    bool valid0=radius0+dr*t0>=0,valid1=radius0+dr*t1>=0;
                    if(!valid0&&!valid1)return default;
                    t=(float)(valid0&&valid1?(dr>=0?Math.Max(t0,t1):Math.Min(t0,t1)):valid0?t0:t1);
                }
                if(radius0+dr*t<0)return default;break;
            default:throw new InvalidOperationException("Unknown float gradient kind.");
        }
        return Interpolate(t);
    }
    void IPremultipliedBatchShader.SampleBatch(Span<Vector4> samples)
    {
        if(kind!=FloatGradientKind.Linear)
        {for(int i=0;i<samples.Length;i++)samples[i]=SamplePremultiplied(samples[i].X,samples[i].Y);return;}
        for(int i=0;i<samples.Length;i++)
        {
            var point=inverse.Map(samples[i].X,samples[i].Y);
            var q=new Vector2(point.X,point.Y)-first;
            samples[i]=Interpolate(lengthSquared>0?Vector2.Dot(q,direction)/lengthSquared:1);
        }
    }
    private Vector4 Interpolate(float t)
    {
        if(!float.IsFinite(t))return default;
        if(tileMode==3&&(t<0||t>1))return default;
        t=tileMode switch{1=>t-MathF.Floor(t),2=>1-MathF.Abs(t-2*MathF.Floor(t/2)-1),_=>Math.Clamp(t,0,1)};
        int lo=0,hi=offsets.Length;while(lo<hi){int mid=(lo+hi)/2;if(offsets[mid]<=t)lo=mid+1;else hi=mid;}
        Vector4 value;
        if(lo==0)value=colors[0];else if(lo>=colors.Length)value=colors[^1];else{int left=lo-1;float width=offsets[lo]-offsets[left];value=Vector4.Lerp(colors[left],colors[lo],width>0?(t-offsets[left])/width:1);}
        value=Vector4.Clamp(value,Vector4.Zero,Vector4.One);value.X*=value.W;value.Y*=value.W;value.Z*=value.W;return value;
    }
    public Rgba32 Sample(float x,float y)
    {var p=SamplePremultiplied(x,y);return p.W>0?new(ColorBlend.Byte(p.X/p.W),ColorBlend.Byte(p.Y/p.W),ColorBlend.Byte(p.Z/p.W),ColorBlend.Byte(p.W)):default;}
}
