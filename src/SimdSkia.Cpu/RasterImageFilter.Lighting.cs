using System.Numerics;
namespace SimdSkia.Cpu;
public sealed partial class RasterImageFilter
{
    /// <summary>SVG alpha-height lighting: distant, point or spot light with diffuse or specular shading.</summary>
    public static RasterImageFilter Lighting(int kind,bool specular,Vector3 location,Vector3 target,float exponent,float cutoff,Rgba32 color,float surfaceScale,float coefficient,float shininess,RasterImageFilter? input=null,RectF? crop=null)
    {
        if((uint)kind>2||!float.IsFinite(location.X)||!float.IsFinite(location.Y)||!float.IsFinite(location.Z)||!float.IsFinite(target.X)||!float.IsFinite(target.Y)||!float.IsFinite(target.Z)||!float.IsFinite(exponent)||!float.IsFinite(cutoff)||!float.IsFinite(surfaceScale)||!float.IsFinite(coefficient)||!float.IsFinite(shininess)||exponent<0||coefficient<0||shininess<0)
            throw new ArgumentOutOfRangeException(nameof(coefficient));
        return new((source,m,clip)=>
        {
            using var image=Input(input,source,m,clip);var result=FilterFrame.Rent(crop is {} c?Map(c,m):clip);
            try
            {
                var xy=m.Map(location.X,location.Y);var aim=m.Map(target.X,target.Y);float zscale=(m.M00+m.M01+m.M10+m.M11)*.5f;
                var light=kind==0?new Vector3(m.M00*location.X+m.M01*location.Y,m.M10*location.X+m.M11*location.Y,location.Z*zscale):new(xy.X,xy.Y,location.Z*zscale);
                var spot=Vector3.Normalize(new Vector3(aim.X,aim.Y,target.Z*zscale)-light);float cos=MathF.Cos(Math.Clamp(cutoff,0,180)*MathF.PI/180f),heightScale=surfaceScale*zscale;
                float H(int x,int y)=>Read(image,x,y,0).W;
                if(result.Pixels is {} p)for(int y=0;y<p.Height;y++)
                {
                    var row=p.GetRowSpan(y);for(int x=0;x<p.Width;x++)
                    {
                        int px=x+result.Left,py=y+result.Top;
                        float gx=(H(px+1,py-1)-H(px-1,py-1)+2*(H(px+1,py)-H(px-1,py))+H(px+1,py+1)-H(px-1,py+1))*.25f;
                        float gy=(H(px-1,py+1)-H(px-1,py-1)+2*(H(px,py+1)-H(px,py-1))+H(px+1,py+1)-H(px+1,py-1))*.25f;
                        var normal=Vector3.Normalize(new(-gx*heightScale,-gy*heightScale,1));var l=kind==0?light:light-new Vector3(px+.5f,py+.5f,H(px,py)*heightScale);
                        float distance=l.Length();l=distance>0?l/distance:Vector3.Zero;float intensity=1;
                        if(kind==2){float cone=Vector3.Dot(-l,spot);intensity=cone<cos?0:MathF.Pow(MathF.Max(0,cone),exponent)*MathF.Min(1,(cone-cos)/.016f);}
                        float amount;
                        if(specular){var half=l+Vector3.UnitZ;half=half.LengthSquared()>0?Vector3.Normalize(half):Vector3.Zero;amount=coefficient*MathF.Pow(MathF.Max(0,Vector3.Dot(normal,half)),shininess)*intensity;}
                        else amount=coefficient*MathF.Max(0,Vector3.Dot(normal,l))*intensity;
                        var rgb=Vector3.Clamp(new Vector3(color.R,color.G,color.B)*(amount/255f),Vector3.Zero,Vector3.One);float alpha=specular?MathF.Max(rgb.X,MathF.Max(rgb.Y,rgb.Z)):1;
                        Write(row,x,new(rgb,alpha));
                    }
                }
                return result;
            }
            catch{result.Dispose();throw;}
        },input);
    }
}
