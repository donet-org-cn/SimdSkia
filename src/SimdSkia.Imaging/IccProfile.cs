using System.Buffers.Binary;
using System.Text;

namespace SimdSkia.Imaging;

/// <summary>Managed ICC matrix/TRC and lut8/lut16 input-to-PCS transforms, with D50 to sRGB adaptation.</summary>
public sealed class IccProfile
{
    private readonly byte[] data;
    private readonly Dictionary<string,(int Offset,int Length)> tags=[];
    private readonly double[][]? curves;
    private readonly double[]? matrix;
    private readonly Lut? lut;
    private readonly bool lab;
    public int InputChannels {get;}
    private int U16(int p)=>BinaryPrimitives.ReadUInt16BigEndian(Slice(p,2));
    private int U32(int p){uint v=BinaryPrimitives.ReadUInt32BigEndian(Slice(p,4));if(v>int.MaxValue)throw new InvalidDataException("ICC offset/length exceeds Int32.");return(int)v;}
    private double Fixed(int p)=>BinaryPrimitives.ReadInt32BigEndian(Slice(p,4))/65536d;
    private ReadOnlySpan<byte> Slice(int p,int n)
    {if(p<0||n<0||p>data.Length-n)throw new InvalidDataException("ICC table exceeds its profile.");return data.AsSpan(p,n);}
    private string Tag(int p)=>Encoding.ASCII.GetString(Slice(p,4));
    private (int Offset,int Length) Require(string tag)=>tags.TryGetValue(tag,out var value)?value:throw new NotSupportedException("ICC is missing "+tag+".");
    public IccProfile(ReadOnlySpan<byte> profile)
    {
        if(profile.Length<132||profile.Length>16*1024*1024)throw new InvalidDataException("ICC size must be 132 bytes..16 MiB.");data=profile.ToArray();
        if(U32(0)!=data.Length||Tag(36)!="acsp")throw new InvalidDataException("Invalid ICC header or declared size.");
        InputChannels=Tag(16) switch{"RGB "=>3,"GRAY"=>1,"CMYK"=>4,_=>throw new NotSupportedException("Unsupported ICC input color space.")};
        lab=Tag(20)=="Lab ";if(!lab&&Tag(20)!="XYZ ")throw new NotSupportedException("ICC PCS must be XYZ or Lab.");
        int count=U32(128);if(count>(data.Length-132)/12)throw new InvalidDataException("Truncated ICC tag directory.");
        for(int i=0;i<count;i++){int p=132+i*12,offset=U32(p+4),length=U32(p+8);Slice(offset,length);if(length<8)throw new InvalidDataException("Truncated ICC tag.");if(!tags.TryAdd(Tag(p),(offset,length)))throw new InvalidDataException("Duplicate ICC tag.");}
        string inputTag=U32(64)==1&&tags.ContainsKey("A2B1")?"A2B1":"A2B0";
        if(tags.TryGetValue(inputTag,out var transform)){lut=new(this,transform.Offset,transform.Length);if(lut.Inputs!=InputChannels)throw new InvalidDataException("ICC LUT channel mismatch.");return;}
        if(InputChannels==4)throw new NotSupportedException("CMYK ICC profile requires an A2B lut8/lut16 transform.");
        if(lab)throw new NotSupportedException("Matrix ICC profile requires XYZ PCS.");
        curves=InputChannels==1?[Curve(Require("kTRC"))]:[Curve(Require("rTRC")),Curve(Require("gTRC")),Curve(Require("bTRC"))];
        if(InputChannels==3)
        {
            matrix=new double[9];string[] names=["rXYZ","gXYZ","bXYZ"];
            for(int c=0;c<3;c++){var entry=Require(names[c]);if(entry.Length<20||Tag(entry.Offset)!="XYZ ")throw new InvalidDataException("Invalid ICC colorant.");for(int r=0;r<3;r++)matrix[r*3+c]=Fixed(entry.Offset+8+r*4);}
        }
    }
    private double[] Curve((int Offset,int Length) entry)
    {
        if(entry.Length<12)throw new InvalidDataException("Truncated ICC curve header.");
        int p=entry.Offset;string type=Tag(p);var result=new double[4097];
        if(type=="curv")
        {
            int n=U32(p+8);if(n>(entry.Length-12)/2)throw new InvalidDataException("Truncated ICC curve.");
            for(int i=0;i<result.Length;i++){double x=i/4096d;if(n==0)result[i]=x;else if(n==1)result[i]=Math.Pow(x,U16(p+12)/256d);else{double at=x*(n-1);int a=(int)at,b=Math.Min(n-1,a+1);result[i]=(U16(p+12+a*2)*(b-at)+U16(p+12+b*2)*(at-a))/65535d;if(a==b)result[i]=U16(p+12+a*2)/65535d;}}
        }
        else if(type=="para")
        {
            int kind=U16(p+8),n=kind switch{0=>1,1=>3,2=>4,3=>5,4=>7,_=>throw new NotSupportedException("Unknown ICC parametric curve.")};
            if(entry.Length<12+n*4)throw new InvalidDataException("Truncated ICC parametric curve.");var a=new double[n];for(int i=0;i<n;i++)a[i]=Fixed(p+12+i*4);
            for(int i=0;i<result.Length;i++)
            {double x=i/4096d;result[i]=kind switch{0=>Math.Pow(x,a[0]),1=>x>=-a[2]/a[1]?Math.Pow(Math.Max(0,a[1]*x+a[2]),a[0]):0,2=>x>=-a[2]/a[1]?Math.Pow(Math.Max(0,a[1]*x+a[2]),a[0])+a[3]:a[3],3=>x>=a[4]?Math.Pow(Math.Max(0,a[1]*x+a[2]),a[0]):a[3]*x,_=>x>=a[4]?Math.Pow(Math.Max(0,a[1]*x+a[2]),a[0])+a[5]:a[3]*x+a[6]};}
        }
        else throw new NotSupportedException("ICC curve type "+type+" is not implemented.");
        if(result.Any(x=>!double.IsFinite(x)))throw new InvalidDataException("Nonfinite ICC curve.");return result;
    }
    private static double Lookup(double[] table,double x)
    {double at=Math.Clamp(x,0,1)*(table.Length-1);int a=(int)at,b=Math.Min(table.Length-1,a+1);return table[a]+(table[b]-table[a])*(at-a);}
    public Rgba32 ToSrgb(ReadOnlySpan<double> input,byte alpha=255)
    {
        if(input.Length!=InputChannels)throw new ArgumentException("ICC input channel count differs.");
        double x,y,z;
        if(lut is not null)
        {
            Span<double> pcs=stackalloc double[3];lut.Transform(input,pcs);
            if(lab)
            {
                double l=pcs[0]*100,a=pcs[1]*255-128,b=pcs[2]*255-128,fy=(l+16)/116;
                static double Inv(double v)=>v>6/29d?v*v*v:3*(6/29d)*(6/29d)*(v-4/29d);
                x=.96422*Inv(fy+a/500);y=Inv(fy);z=.82521*Inv(fy-b/200);
            }
            else{x=pcs[0]*lut.XyzScale;y=pcs[1]*lut.XyzScale;z=pcs[2]*lut.XyzScale;}
        }
        else if(InputChannels==1){y=Lookup(curves![0],input[0]);x=.96422*y;z=.82521*y;}
        else
        {double r=Lookup(curves![0],input[0]),g=Lookup(curves[1],input[1]),b=Lookup(curves[2],input[2]);x=matrix![0]*r+matrix[1]*g+matrix[2]*b;y=matrix[3]*r+matrix[4]*g+matrix[5]*b;z=matrix[6]*r+matrix[7]*g+matrix[8]*b;}
        double xx=.9555766*x-.0230393*y+.0631636*z,yy=-.0282895*x+1.0099416*y+.0210077*z,zz=.0122982*x-.0204830*y+1.3299098*z;
        static byte Encode(double c){c=Math.Clamp(c,0,1);double v=c<=.0031308?12.92*c:1.055*Math.Pow(c,1/2.4)-.055;return(byte)Math.Clamp(Math.Floor(v*255+.5),0,255);}
        return new(Encode(3.2404542*xx-1.5371385*yy-.4985314*zz),Encode(-.969266*xx+1.8760108*yy+.041556*zz),Encode(.0556434*xx-.2040259*yy+1.0572252*zz),alpha);
    }
    public void Transform(PixelBuffer image)
    {
        if(InputChannels is not (1 or 3))throw new ArgumentException("An RGBA image cannot carry four-channel CMYK input.");
        Span<double> input=stackalloc double[3];
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
        {var p=image.ReadPixel(x,y);input[0]=p.R/255d;if(InputChannels==3){input[1]=p.G/255d;input[2]=p.B/255d;}image.WritePixel(x,y,ToSrgb(input[..InputChannels],p.A));}
    }
    private sealed class Lut
    {
        internal int Inputs {get;}
        internal double XyzScale {get;}
        private readonly int grid;
        private readonly double[][] inputs,outputs;
        private readonly double[] clut,matrix;
        internal Lut(IccProfile p,int offset,int length)
        {
            string type=p.Tag(offset);bool sixteen=type=="mft2";
            if(!sixteen&&type!="mft1")throw new NotSupportedException("ICC multi-stage/other A2B transforms are not implemented.");
            if(length<(sixteen?52:48))throw new InvalidDataException("Truncated ICC LUT header.");
            Inputs=p.data[offset+8];int count=p.data[offset+9];grid=p.data[offset+10];
            if(Inputs is <1 or >4||count!=3||grid is <2 or >65)throw new NotSupportedException("Unsupported ICC LUT dimensions.");
            matrix=new double[9];for(int i=0;i<9;i++)matrix[i]=p.Fixed(offset+12+i*4);
            // All supported input spaces are device RGB/GRAY/CMYK. ICC uses
            // this matrix only for XYZ input; require the prescribed identity.
            for(int i=0;i<9;i++)if(matrix[i]!=(i%4==0?1d:0d))throw new NotSupportedException("Non-identity ICC LUT matrix for non-XYZ input.");
            int ni=sixteen?p.U16(offset+48):256,no=sixteen?p.U16(offset+50):256;if(ni<2||no<2)throw new InvalidDataException("Invalid ICC LUT table length.");
            long elements=1;for(int i=0;i<Inputs;i++)elements*=grid;
            if(elements*3>8*1024*1024)throw new NotSupportedException("ICC LUT exceeds its configured element limit.");
            int cursor=offset+(sixteen?52:48),end=offset+length;
            double Read(){if(cursor>end-(sixteen?2:1))throw new InvalidDataException("Truncated ICC LUT.");double v=sixteen?p.U16(cursor)/65535d:p.data[cursor]/255d;cursor+=sixteen?2:1;return v;}
            inputs=new double[Inputs][];for(int c=0;c<Inputs;c++){inputs[c]=new double[ni];for(int i=0;i<ni;i++)inputs[c][i]=Read();}
            clut=new double[elements*3];for(int i=0;i<clut.Length;i++)clut[i]=Read();
            outputs=new double[3][];for(int c=0;c<3;c++){outputs[c]=new double[no];for(int i=0;i<no;i++)outputs[c][i]=Read();}
            XyzScale=sixteen?65535/32768d:255/128d;
        }
        internal void Transform(ReadOnlySpan<double> input,Span<double> result)
        {
            Span<double> values=stackalloc double[4],fraction=stackalloc double[4];Span<int> low=stackalloc int[4];
            for(int c=0;c<Inputs;c++)values[c]=Lookup(inputs[c],input[c]);
            for(int c=0;c<Inputs;c++){double at=Math.Clamp(values[c],0,1)*(grid-1);low[c]=Math.Min(grid-2,(int)at);fraction[c]=at-low[c];}
            result.Clear();
            for(int corner=0;corner<(1<<Inputs);corner++)
            {double weight=1;int index=0;for(int c=0;c<Inputs;c++){bool upper=(corner&(1<<c))!=0;weight*=upper?fraction[c]:1-fraction[c];index=index*grid+low[c]+(upper?1:0);}for(int c=0;c<3;c++)result[c]+=clut[index*3+c]*weight;}
            for(int c=0;c<3;c++)result[c]=Lookup(outputs[c],result[c]);
        }
    }
}
