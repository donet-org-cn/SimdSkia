using System.Numerics;
namespace SimdSkia;

/// <summary>Porter-Duff and W3C separable/nonseparable blend equations in
/// premultiplied sRGB. Coverage is applied after this operation, not to its inputs.</summary>
public static class ColorBlend
{
    // m119 lowp raster equations (Google, BSD-3). Difference/exclusion use
    // two rounded products, which differ by up to two bytes from ideal floats.
    public static Vector4 ComposeRaster(Vector4 source,Vector4 destination,CompositeMode mode)
    {
        if(mode is not (CompositeMode.Difference or CompositeMode.Exclusion))return ComposePremultiplied(source,destination,mode);
        int sa=Byte(source.W),da=Byte(destination.W);static int Approx(int x)=>(x+255)>>8;
        float Channel(float s,float d){int sv=Byte(s),dv=Byte(d);return Math.Clamp(sv+dv-2*Approx(mode==CompositeMode.Difference?Math.Min(sv*da,dv*sa):sv*dv),0,255)/255f;}
        return new(Channel(source.X,destination.X),Channel(source.Y,destination.Y),Channel(source.Z,destination.Z),(sa+Approx(da*(255-sa)))/255f);
    }
    public static Vector4 ComposePremultiplied(Vector4 source,Vector4 destination,CompositeMode mode)
    {
        float sa=source.W,da=destination.W;
        switch(mode)
        {
            case CompositeMode.Clear:return Vector4.Zero;
            case CompositeMode.Source:return source;
            case CompositeMode.Destination:return destination;
            case CompositeMode.SourceOver:return source+destination*(1-sa);
            case CompositeMode.DestinationOver:return destination+source*(1-da);
            case CompositeMode.SourceIn:return source*da;
            case CompositeMode.DestinationIn:return destination*sa;
            case CompositeMode.SourceOut:return source*(1-da);
            case CompositeMode.DestinationOut:return destination*(1-sa);
            case CompositeMode.SourceAtop:return source*da+destination*(1-sa);
            case CompositeMode.DestinationAtop:return destination*sa+source*(1-da);
            case CompositeMode.Xor:return source*(1-da)+destination*(1-sa);
            case CompositeMode.Plus:return Vector4.Min(source+destination,Vector4.One);
            case CompositeMode.Modulate:return source*destination;
        }
        var s=sa>0?new Vector3(source.X,source.Y,source.Z)/sa:Vector3.Zero;var d=da>0?new Vector3(destination.X,destination.Y,destination.Z)/da:Vector3.Zero;
        Vector3 blended;
        if(mode==CompositeMode.Hue)blended=SetLum(SetSat(s,Sat(d)),Lum(d));
        else if(mode==CompositeMode.Saturation)blended=SetLum(SetSat(d,Sat(s)),Lum(d));
        else if(mode==CompositeMode.Color)blended=SetLum(s,Lum(d));
        else if(mode==CompositeMode.Luminosity)blended=SetLum(d,Lum(s));
        else blended=new(Channel(s.X,d.X,mode),Channel(s.Y,d.Y,mode),Channel(s.Z,d.Z,mode));
        return source*(1-da)+destination*(1-sa)+new Vector4(blended*(sa*da),sa*da);
    }
    private static float Channel(float s,float d,CompositeMode mode)=>mode switch
    {
        CompositeMode.Screen=>s+d-s*d,
        CompositeMode.Overlay=>d<=.5f?2*s*d:1-2*(1-s)*(1-d),
        CompositeMode.Darken=>MathF.Min(s,d),CompositeMode.Lighten=>MathF.Max(s,d),
        CompositeMode.ColorDodge=>d==0?0:s>=1?1:MathF.Min(1,d/(1-s)),
        CompositeMode.ColorBurn=>d>=1?1:s<=0?0:1-MathF.Min(1,(1-d)/s),
        CompositeMode.HardLight=>s<=.5f?2*s*d:1-2*(1-s)*(1-d),
        CompositeMode.SoftLight=>s<=.5f?d-(1-2*s)*d*(1-d):d+(2*s-1)*((d<=.25f?((16*d-12)*d+4)*d:MathF.Sqrt(d))-d),
        CompositeMode.Difference=>MathF.Abs(d-s),CompositeMode.Exclusion=>s+d-2*s*d,CompositeMode.Multiply=>s*d,
        _=>throw new ArgumentOutOfRangeException(nameof(mode))
    };
    private static float Lum(Vector3 c)=>.3f*c.X+.59f*c.Y+.11f*c.Z;
    private static float Sat(Vector3 c)=>MathF.Max(c.X,MathF.Max(c.Y,c.Z))-MathF.Min(c.X,MathF.Min(c.Y,c.Z));
    private static Vector3 SetLum(Vector3 c,float luminance)
    {
        c+=new Vector3(luminance-Lum(c));float l=Lum(c),minimum=MathF.Min(c.X,MathF.Min(c.Y,c.Z)),maximum=MathF.Max(c.X,MathF.Max(c.Y,c.Z));
        if(minimum<0)c=new Vector3(l)+(c-new Vector3(l))*(l/(l-minimum));
        if(maximum>1)c=new Vector3(l)+(c-new Vector3(l))*((1-l)/(maximum-l));return c;
    }
    private static Vector3 SetSat(Vector3 c,float saturation)
    {
        int min=0,max=0;for(int i=1;i<3;i++){if(c[i]<c[min])min=i;if(c[i]>c[max])max=i;}
        if(min==max)return Vector3.Zero;int middle=3-min-max;float lo=c[min],hi=c[max];c[middle]=(c[middle]-lo)*saturation/(hi-lo);c[max]=saturation;c[min]=0;return c;
    }
    public static Rgba32 Compose(Rgba32 source,Rgba32 destination,CompositeMode mode)
    {
        var s=new Vector4(source.R,source.G,source.B,source.A)*(1f/255);var d=new Vector4(destination.R,destination.G,destination.B,destination.A)*(1f/255);s=new(s.X*s.W,s.Y*s.W,s.Z*s.W,s.W);d=new(d.X*d.W,d.Y*d.W,d.Z*d.W,d.W);var r=ComposePremultiplied(s,d,mode);
        return r.W>0?new(Byte(r.X/r.W),Byte(r.Y/r.W),Byte(r.Z/r.W),Byte(r.W)):default;
    }
    public static byte Byte(float value)=>(byte)Math.Clamp((int)MathF.Round(value*255),0,255);
}
