using System.Buffers.Binary;
using System.Numerics;
namespace SkiaSharp;

/// <summary>Immutable RGB matrix/parametric-transfer color spaces. No native color-management dependency.</summary>
public class SKColorSpace : SKObject
{
    internal sealed record Description(SKColorSpaceTransferFn Transfer,SKColorSpaceXyz Matrix)
    {
        internal bool Srgb=>Transfer==SKColorSpaceTransferFn.Srgb&&Matrix==SKColorSpaceXyz.Srgb;
    }
    private static readonly SKColorSpace srgb=new(new(SKColorSpaceTransferFn.Srgb,SKColorSpaceXyz.Srgb),true);
    private static readonly SKColorSpace linear=new(new(SKColorSpaceTransferFn.Linear,SKColorSpaceXyz.Srgb),true);
    private static readonly float[] srgbToLinear=Enumerable.Range(0,256).Select(v=>{double x=v/255d;return(float)(x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4));}).ToArray();
    internal static Vector4 DecodeColor(SKColor color)=>new(srgbToLinear[color.Red],srgbToLinear[color.Green],srgbToLinear[color.Blue],color.Alpha*(1f/255));
    private readonly bool immortal;
    private Description? data;
    private SKColorSpace(Description data,bool immortal=false){this.data=data;this.immortal=immortal;}
    protected override bool IgnoreDispose=>immortal;
    internal Description Value {get{ThrowIfDisposed();return data!;}}
    internal SKColorSpace Retain()=>immortal?this:new(Value);
    internal static SKImageInfo RetainInfo(SKImageInfo info)=>info.ColorSpace is null?info:info.WithColorSpace(info.ColorSpace.Retain());
    public static SKColorSpace CreateSrgb()=>srgb;
    public static SKColorSpace CreateSrgbLinear()=>linear;
    public bool IsSrgb=>Value.Srgb;
    public bool GammaIsCloseToSrgb=>Value.Transfer==SKColorSpaceTransferFn.Srgb;
    public bool GammaIsLinear=>Value.Transfer==SKColorSpaceTransferFn.Linear;
    public bool IsNumericalTransferFunction=>Value.Transfer.G>=0;
    public static bool Equal(SKColorSpace left,SKColorSpace right)
    {ArgumentNullException.ThrowIfNull(left);ArgumentNullException.ThrowIfNull(right);return left.Value==right.Value;}
    public static SKColorSpace CreateRgb(SKColorSpaceTransferFn transferFn,SKColorSpaceXyz toXyzD50)
    {
        if(!ValidTransfer(transferFn))return null!;
        if(toXyzD50.Values.Any(v=>!float.IsFinite(v)))return null!;
        if(transferFn==SKColorSpaceTransferFn.Srgb&&toXyzD50==SKColorSpaceXyz.Srgb)return srgb;
        if(transferFn==SKColorSpaceTransferFn.Linear&&toXyzD50==SKColorSpaceXyz.Srgb)return linear;
        return new(new(transferFn,toXyzD50));
    }
    internal static bool ValidTransfer(SKColorSpaceTransferFn tf)=>tf.IsValid;
    public SKColorSpaceTransferFn GetNumericalTransferFunction()=>GetNumericalTransferFunction(out var fn)?fn:default;
    public bool GetNumericalTransferFunction(out SKColorSpaceTransferFn fn){var value=Value;fn=value.Transfer.G>=0?value.Transfer:default;return value.Transfer.G>=0;}
    public SKColorSpaceXyz ToColorSpaceXyz()=>Value.Matrix;
    public bool ToColorSpaceXyz(out SKColorSpaceXyz toXyzD50){toXyzD50=Value.Matrix;return true;}
    public SKColorSpace ToLinearGamma()=>CreateRgb(SKColorSpaceTransferFn.Linear,Value.Matrix);
    public SKColorSpace ToSrgbGamma()=>CreateRgb(SKColorSpaceTransferFn.Srgb,Value.Matrix);
    public SKColorSpaceIccProfile ToProfile()=>new(Value);
    public static SKColorSpace CreateIcc(SKColorSpaceIccProfile profile)
    {ArgumentNullException.ThrowIfNull(profile);var d=profile.Description;return d is null||d.Matrix.Invert()==default?null!:CreateRgb(d.Transfer,d.Matrix);}
    public static SKColorSpace CreateIcc(ReadOnlySpan<byte> input){using var profile=SKColorSpaceIccProfile.Create(input);return profile is null?null!:CreateIcc(profile);}
    public static SKColorSpace CreateIcc(byte[] input)=>CreateIcc(input.AsSpan());
    public static SKColorSpace CreateIcc(byte[] input,long length)
    {ArgumentNullException.ThrowIfNull(input);if(length<0||length>input.LongLength)throw new ArgumentOutOfRangeException(nameof(length));return CreateIcc(input.AsSpan(0,(int)length));}
    public static SKColorSpace CreateIcc(SKData input){ArgumentNullException.ThrowIfNull(input);return CreateIcc(input.AsSpan());}
    public static unsafe SKColorSpace CreateIcc(IntPtr input,long length)
    {using var profile=SKColorSpaceIccProfile.Create(input,length);return profile is null?null!:CreateIcc(profile);}
    protected override void Dispose(bool disposing)=>data=null;

    // Untagged source pixels follow Skia's legacy destination-space convention.
    internal static bool NeedsConversion(SKColorSpace? source,SKColorSpace? destination)=>source is not null&&source.Value!=(destination??srgb).Value;
    internal readonly struct Converter
    {
        private readonly SKColorSpaceXyz matrix;
        private readonly SKColorSpaceTransferFn decode,encode;
        internal Converter(SKColorSpace? source,SKColorSpace? destination)
        {var s=(source??srgb).Value;var d=(destination??srgb).Value;var inverse=d.Matrix.Invert();if(inverse==default)throw new NotSupportedException("Cannot convert to a singular color-space matrix.");matrix=SKColorSpaceXyz.Concat(inverse,s.Matrix);decode=s.Transfer;encode=d.Transfer.Invert();}
        internal Vector4 Convert(Vector4 straight)
        {
            float r=decode==SKColorSpaceTransferFn.Linear?straight.X:decode.Transform(straight.X),g=decode==SKColorSpaceTransferFn.Linear?straight.Y:decode.Transform(straight.Y),b=decode==SKColorSpaceTransferFn.Linear?straight.Z:decode.Transform(straight.Z);
            float x=matrix[0,0]*r+matrix[1,0]*g+matrix[2,0]*b,y=matrix[0,1]*r+matrix[1,1]*g+matrix[2,1]*b,z=matrix[0,2]*r+matrix[1,2]*g+matrix[2,2]*b;
            return encode==SKColorSpaceTransferFn.Linear?new(x,y,z,straight.W):new(encode.Transform(x),encode.Transform(y),encode.Transform(z),straight.W);
        }
    }
}

/// <summary>Owned immutable ICC bytes, or an in-memory profile produced by ToProfile. Buffer is pinned only on demand.</summary>
public class SKColorSpaceIccProfile : SKObject
{
    private SKData? storage;
    private SKColorSpace.Description? description;
    public SKColorSpaceIccProfile(){}
    internal SKColorSpaceIccProfile(SKColorSpace.Description description)=>this.description=description;
    internal SKColorSpace.Description? Description {get{ThrowIfDisposed();return description;}}
    public long Size {get{ThrowIfDisposed();return storage?.Size??0;}}
    public IntPtr Buffer {get{ThrowIfDisposed();return storage?.Data??IntPtr.Zero;}}
    public bool ToColorSpaceXyz(out SKColorSpaceXyz toXyzD50){toXyzD50=Description?.Matrix??default;return description is not null;}
    public SKColorSpaceXyz ToColorSpaceXyz()=>ToColorSpaceXyz(out var m)?m:default;
    public static SKColorSpaceIccProfile Create(byte[] data)=>Create(data.AsSpan());
    public static SKColorSpaceIccProfile Create(SKData data){ArgumentNullException.ThrowIfNull(data);return Create(data.AsSpan());}
    public static unsafe SKColorSpaceIccProfile Create(IntPtr data,long length)
    {if(data==IntPtr.Zero)throw new ArgumentNullException(nameof(data));if(length<=0)return null!;if(length>16*1024*1024)throw new NotSupportedException("ICC profiles are limited to 16 MiB.");return Create(new ReadOnlySpan<byte>((void*)data,(int)length));}
    public static SKColorSpaceIccProfile Create(ReadOnlySpan<byte> data)
    {
        if(data.IsEmpty)return null!;
        if(data.Length>16*1024*1024)throw new NotSupportedException("ICC profiles are limited to 16 MiB.");
        try
        {
            var parsed=Parse(data);if(parsed is null)return null!;
            return new(parsed){storage=SKData.CreateCopy(data)};
        }
        catch(InvalidDataException){return null!;}
    }
    private static SKColorSpace.Description? Parse(ReadOnlySpan<byte> input)
    {
        // Copy once into owned, bounded storage so parsing never retains a caller's pointer.
        byte[] bytes=input.ToArray();ReadOnlySpan<byte> At(int p,int n)
        {if(p<0||n<0||p>bytes.Length-n)throw new InvalidDataException("ICC tag exceeds the input.");return bytes.AsSpan(p,n);}
        int U32(int p){uint n=BinaryPrimitives.ReadUInt32BigEndian(At(p,4));if(n>int.MaxValue)throw new InvalidDataException("ICC length overflow.");return(int)n;}
        float Fixed(int p)=>BinaryPrimitives.ReadInt32BigEndian(At(p,4))*(1f/65536);
        if(bytes.Length<132||U32(0)>bytes.Length||!At(36,4).SequenceEqual("acsp"u8))return null;
        if(!At(16,4).SequenceEqual("RGB "u8)||!At(20,4).SequenceEqual("XYZ "u8))throw new NotSupportedException("Color-space creation currently requires RGB/XYZ matrix ICC profiles; the image decoder separately supports additional ICC transforms.");
        int count=U32(128);if(count>(bytes.Length-132)/12)return null;
        var tags=new Dictionary<string,(int Offset,int Size)>();
        for(int i=0;i<count;i++){int p=132+i*12,offset=U32(p+4),size=U32(p+8);_=At(offset,size);if(size<8)return null;tags.TryAdd(System.Text.Encoding.ASCII.GetString(At(p,4)),(offset,size));}
        (int Offset,int Size) Tag(string name)=>tags.TryGetValue(name,out var entry)?entry:throw new InvalidDataException("ICC is missing "+name);
        float[] matrix=new float[9];string[] channels=["r","g","b"];SKColorSpaceTransferFn transfer=default;
        for(int c=0;c<3;c++)
        {
            var xyz=Tag(channels[c]+"XYZ");if(xyz.Size<20||!At(xyz.Offset,4).SequenceEqual("XYZ "u8))return null;
            for(int r=0;r<3;r++)matrix[r*3+c]=Fixed(xyz.Offset+8+r*4);
            var trc=Tag(channels[c]+"TRC");int p=trc.Offset;SKColorSpaceTransferFn fn;
            if(trc.Size<12)return null;
            if(At(p,4).SequenceEqual("curv"u8))
            {
                int n=U32(p+8);if(n>(trc.Size-12)/2)return null;
                fn=n switch{0=>SKColorSpaceTransferFn.Linear,1=>new(BinaryPrimitives.ReadUInt16BigEndian(At(p+12,2))*(1f/256),1,0,0,0,0,0),_=>throw new NotSupportedException("ICC sampled TRCs require a numerical fit before they can become an RGB color space.")};
            }
            else if(At(p,4).SequenceEqual("para"u8))
            {
                int kind=BinaryPrimitives.ReadUInt16BigEndian(At(p+8,2)),n=kind switch{0=>1,1=>3,2=>4,3=>5,4=>7,_=>0};if(n==0||trc.Size<12+n*4)return null;
                float g=Fixed(p+12),a=n>=3?Fixed(p+16):1,b=n>=3?Fixed(p+20):0,c0=kind==3?Fixed(p+24):kind==4?Fixed(p+36):0;
                float d=kind>=3?Fixed(p+28):kind>0?-b/a:0,e=kind is 2 or 4?Fixed(p+24):0,f=kind==2?e:kind==4?Fixed(p+36):0;
                // ICC type 4 stores [g,a,b,c,d,e,f].
                if(kind==4){c0=Fixed(p+24);e=Fixed(p+32);f=Fixed(p+36);}
                fn=new(g,a,b,c0,d,e,f);
            }
            else throw new NotSupportedException("Unsupported ICC transfer curve.");
            if(!SKColorSpace.ValidTransfer(fn))return null;
            if(c==0)transfer=fn;else if(transfer!=fn)throw new NotSupportedException("Independent channel TRCs are not yet supported by RGB color-space creation.");
        }
        var m=new SKColorSpaceXyz(matrix);if(m.Invert()==default)return null;
        return new(transfer,m);
    }
    protected override void Dispose(bool disposing){storage?.Dispose();storage=null;description=null;}
}
