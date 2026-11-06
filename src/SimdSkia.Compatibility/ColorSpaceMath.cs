// Transfer-function math translated from Skia m119 modules/skcms/skcms.cc.
// Copyright 2018 Google Inc. BSD-3-Clause; see licenses/Skia-BSD.txt.
namespace SkiaSharp;

public partial struct SKColorSpaceTransferFn
{
    public static readonly SKColorSpaceTransferFn Empty;
    public SKColorSpaceTransferFn(float g,float a,float b,float c,float d,float e,float f){fG=g;fA=a;fB=b;fC=c;fD=d;fE=e;fF=f;}
    public SKColorSpaceTransferFn(float[] values){ArgumentNullException.ThrowIfNull(values);if(values.Length!=7)throw new ArgumentException("Expected seven transfer parameters.",nameof(values));this=new(values[0],values[1],values[2],values[3],values[4],values[5],values[6]);}
    public readonly float[] Values=>[fG,fA,fB,fC,fD,fE,fF];
    public static SKColorSpaceTransferFn Srgb=>new(2.4f,.9478673f,.0521327f,.07739938f,.04045f,0,0);
    public static SKColorSpaceTransferFn TwoDotTwo=>new(2.2f,1,0,0,0,0,0);
    public static SKColorSpaceTransferFn Linear=>new(1,1,0,0,0,0,0);
    public static SKColorSpaceTransferFn Rec2020=>new(2.22222f,.909672f,.0903276f,.222222f,.0812429f,0,0);
    public static SKColorSpaceTransferFn Pq=>new(-2,-.8359375f,1,.012683313f,18.851562f,-18.6875f,6.277395f);
    public static SKColorSpaceTransferFn Hlg=>new(-3,2,2,5.5918164f,.28466892f,.5599107f,0);
    private readonly int Kind=>fG is -2 or -3 or -4?(int)-fG:float.IsFinite(fA+fB+fC+fD+fE+fF+fG)&&fA>=0&&fC>=0&&fD>=0&&fG>=0&&fA*fD+fB>=0?1:0;
    internal readonly bool IsValid=>Kind!=0;
    public readonly float Transform(float x)
    {
        float sign=x<0?-1:1;x*=sign;
        return Kind switch
        {
            1=>sign*(x<fD?fC*x+fF:Pow(fA*x+fB,fG)+fE),
            2=>sign*Pow(MathF.Max(fA+fB*Pow(x,fC),0)/(fD+fE*Pow(x,fC)),fF),
            3=>(fF+1)*sign*(x*fA<=1?Pow(x*fA,fB):Exp((x-fE)*fC)+fD),
            4=>InverseHlg(x,sign),_=>0
        };
    }
    private readonly float InverseHlg(float x,float sign){x/=fF+1;return sign*(x<=1?fA*Pow(x,fB):fC*Log(x-fD)+fE);}
    public readonly SKColorSpaceTransferFn Invert()
    {
        int kind=Kind;if(kind==0)return Empty;
        if(kind==2)return new(-2,-fA,fD,1/fF,fB,-fE,1/fC);
        if(kind is 3 or 4)return new(kind==3?-4:-3,1/fA,1/fB,1/fC,fD,fE,fF);
        float left=fC*fD+fF,right=Pow(fA*fD+fB,fG)+fE;
        if(MathF.Abs(left-right)>1/512f)return Empty;
        float k=Pow(fA,-fG);var inverse=new SKColorSpaceTransferFn(1/fG,k,-k*fE,left>0?1/fC:0,left,-fB/fA,left>0?-fF/fC:0);
        if(inverse.fA<0)return Empty;if(inverse.fA*inverse.fD+inverse.fB<0)inverse.fB=-inverse.fA*inverse.fD;
        if(inverse.Kind!=1)return Empty;float sample=Transform(1);if(!float.IsFinite(sample))return Empty;
        float sign=sample<0?-1:1;sample*=sign;
        if(sample<inverse.fD)inverse.fF=1-sign*inverse.fC*sample;else inverse.fE=1-sign*Pow(inverse.fA*sample+inverse.fB,inverse.fG);
        return inverse.Kind==1?inverse:Empty;
    }
    private static float Log2(float x)
    {
        int bits=BitConverter.SingleToInt32Bits(x);float e=bits*(1f/(1<<23)),m=BitConverter.Int32BitsToSingle((bits&0x007fffff)|0x3f000000);
        return e-124.225514990f-1.498030302f*m-1.725879990f/(.3520887068f+m);
    }
    private static float Exp2(float x)
    {
        if(x>128)return float.PositiveInfinity;float fraction=x-MathF.Floor(x);
        float bits=(1f*(1<<23))*(x+121.274057500f-1.490129070f*fraction+27.728023300f/(4.84252568f-fraction));
        return bits>=(float)int.MaxValue?float.PositiveInfinity:bits<0?0:BitConverter.Int32BitsToSingle((int)bits);
    }
    private static float Pow(float x,float y)=>x is 0 or 1?x:Exp2(Log2(x)*y);
    private static float Exp(float x)=>Exp2(1.4426950408889634074f*x);
    private static float Log(float x)=>.69314718f*Log2(x);
}

public partial struct SKColorSpaceXyz
{
    public static readonly SKColorSpaceXyz Empty;
    public static readonly SKColorSpaceXyz Identity=new(1,0,0,0,1,0,0,0,1);
    public SKColorSpaceXyz(float value):this(value,value,value,value,value,value,value,value,value) { }
    public SKColorSpaceXyz(float[] values){ArgumentNullException.ThrowIfNull(values);if(values.Length!=9)throw new ArgumentException("Expected nine matrix values.",nameof(values));this=new(values[0],values[1],values[2],values[3],values[4],values[5],values[6],values[7],values[8]);}
    public SKColorSpaceXyz(float m00,float m01,float m02,float m10,float m11,float m12,float m20,float m21,float m22)
    {fM00=m00;fM01=m01;fM02=m02;fM10=m10;fM11=m11;fM12=m12;fM20=m20;fM21=m21;fM22=m22;}
    public float[] Values {readonly get=>[fM00,fM01,fM02,fM10,fM11,fM12,fM20,fM21,fM22];set{ArgumentNullException.ThrowIfNull(value);if(value.Length!=9)throw new ArgumentException("Expected nine matrix values.",nameof(value));this=new(value);}}
    public readonly float this[int x,int y] {get{if((uint)x>=3)throw new ArgumentOutOfRangeException(nameof(x));if((uint)y>=3)throw new ArgumentOutOfRangeException(nameof(y));return (x+y*3) switch{0=>fM00,1=>fM01,2=>fM02,3=>fM10,4=>fM11,5=>fM12,6=>fM20,7=>fM21,_=>fM22};}}
    public static SKColorSpaceXyz Srgb=>new(.43606567f,.3851471f,.1430664f,.2224884f,.71687317f,.06060791f,.013916016f,.097076416f,.71409607f);
    public static SKColorSpaceXyz AdobeRgb=>new(.6097412f,.20527649f,.14918518f,.31111145f,.6256714f,.06321716f,.019470215f,.06086731f,.7445679f);
    public static SKColorSpaceXyz DisplayP3=>new(.515102f,.291965f,.157153f,.241182f,.692236f,.0665819f,-.00104941f,.0418818f,.784378f);
    public static SKColorSpaceXyz Rec2020=>new(.673459f,.165661f,.1251f,.279033f,.675338f,.0456288f,-.00193139f,.0299794f,.797162f);
    public static SKColorSpaceXyz Xyz=>Identity;
    public readonly SKColorSpaceXyz Invert()
    {
        double a=fM00,b=fM01,c=fM02,d=fM10,e=fM11,f=fM12,g=fM20,h=fM21,i=fM22;
        double det=a*(e*i-f*h)-b*(d*i-f*g)+c*(d*h-e*g);if(det==0||!double.IsFinite(det))return Empty;double r=1/det;
        return new((float)((e*i-f*h)*r),(float)((c*h-b*i)*r),(float)((b*f-c*e)*r),(float)((f*g-d*i)*r),(float)((a*i-c*g)*r),(float)((c*d-a*f)*r),(float)((d*h-e*g)*r),(float)((b*g-a*h)*r),(float)((a*e-b*d)*r));
    }
    public static SKColorSpaceXyz Concat(SKColorSpaceXyz a,SKColorSpaceXyz b)
    {
        float At(int row,int column)=>a[0,row]*b[column,0]+a[1,row]*b[column,1]+a[2,row]*b[column,2];
        return new(At(0,0),At(0,1),At(0,2),At(1,0),At(1,1),At(1,2),At(2,0),At(2,1),At(2,2));
    }
}

public partial struct SKColorSpacePrimaries
{
    public static readonly SKColorSpacePrimaries Empty;
    public SKColorSpacePrimaries(float[] values){ArgumentNullException.ThrowIfNull(values);if(values.Length!=8)throw new ArgumentException("Expected eight primary coordinates.",nameof(values));this=new(values[0],values[1],values[2],values[3],values[4],values[5],values[6],values[7]);}
    public SKColorSpacePrimaries(float rx,float ry,float gx,float gy,float bx,float by,float wx,float wy){fRX=rx;fRY=ry;fGX=gx;fGY=gy;fBX=bx;fBY=by;fWX=wx;fWY=wy;}
    public readonly float[] Values=>[fRX,fRY,fGX,fGY,fBX,fBY,fWX,fWY];
    public readonly SKColorSpaceXyz ToColorSpaceXyz()=>ToColorSpaceXyz(out var xyz)?xyz:SKColorSpaceXyz.Empty;
    public readonly bool ToColorSpaceXyz(out SKColorSpaceXyz toXyzD50)
    {
        toXyzD50=default;if(fRY==0||fGY==0||fBY==0||fWY==0)return false;
        var p=new SKColorSpaceXyz(fRX/fRY,fGX/fGY,fBX/fBY,1,1,1,(1-fRX-fRY)/fRY,(1-fGX-fGY)/fGY,(1-fBX-fBY)/fBY);
        var inverse=p.Invert();if(inverse==default)return false;float wx=fWX/fWY,wz=(1-fWX-fWY)/fWY;
        float r=inverse[0,0]*wx+inverse[1,0]+inverse[2,0]*wz,g=inverse[0,1]*wx+inverse[1,1]+inverse[2,1]*wz,b=inverse[0,2]*wx+inverse[1,2]+inverse[2,2]*wz;
        p=SKColorSpaceXyz.Concat(p,new(r,0,0,0,g,0,0,0,b));
        var bradford=new SKColorSpaceXyz(.8951f,.2664f,-.1614f,-.7502f,1.7135f,.0367f,.0389f,-.0685f,1.0296f);
        float X(int row,float x,float z)=>bradford[0,row]*x+bradford[1,row]+bradford[2,row]*z;
        float cr=X(0,.96422f,.82521f)/X(0,wx,wz),cg=X(1,.96422f,.82521f)/X(1,wx,wz),cb=X(2,.96422f,.82521f)/X(2,wx,wz);
        toXyzD50=SKColorSpaceXyz.Concat(SKColorSpaceXyz.Concat(SKColorSpaceXyz.Concat(bradford.Invert(),new(cr,0,0,0,cg,0,0,0,cb)),bradford),p);
        for(int row=0;row<3;row++)for(int column=0;column<3;column++)if(!float.IsFinite(toXyzD50[column,row]))return false;return true;
    }
}
