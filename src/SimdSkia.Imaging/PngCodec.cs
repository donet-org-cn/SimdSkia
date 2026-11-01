using System.Buffers.Binary;
using System.Text;
using Sdcb.SimdCodecs.Png;

namespace SimdSkia.Imaging;

/// <summary>Sdcb PNG codec with SimdSkia validation and explicit sRGB conversion.</summary>
public static class PngCodec
{
    private static ReadOnlySpan<byte> Signature=>[137,80,78,71,13,10,26,10];
    public static byte[] Encode(PixelBuffer pixels)=>Encode(pixels,System.IO.Compression.CompressionLevel.Optimal);
    public static byte[] Encode(PixelBuffer pixels,System.IO.Compression.CompressionLevel compression)
    {
        if(!Enum.IsDefined(compression))throw new ArgumentOutOfRangeException(nameof(compression));
        using var source=new EncodingPixels(pixels,true);
        return PngWriter.Encode(source.Span,pixels.Width,pixels.Height,new PngWriteOptions
        {CompressionLevel=compression,ParallelFragments=1,SrgbRenderingIntent=0});
    }
    public static PixelBuffer Decode(ReadOnlySpan<byte> png)
    {
        CodecBackend.ValidateInput(png);
        if(png.Length<8||!png[..8].SequenceEqual(Signature))throw new InvalidDataException("Only PNG decoding is implemented; unknown image signature.");
        int position=8,width=0,height=0,depth=0,type=-1,interlace=0;bool header=false,end=false,dataSeen=false,dataEnded=false,srgb=false;uint? gamma=null;
        IccProfile? profile=null;bool nonSrgbChromaticities=false;Span<byte> prefix=stackalloc byte[4];
        byte[]? palette=null,transparent=null;int firstIdat=0,idatCount=0,compressedLength=0;
        while(position<png.Length)
        {
            if(position>png.Length-12)throw new InvalidDataException("Truncated PNG chunk.");
            int length=checked((int)BinaryPrimitives.ReadUInt32BigEndian(png[position..]));if(length>png.Length-position-12)throw new InvalidDataException("PNG chunk exceeds file.");
            var nameBytes=png.Slice(position+4,4);string name=Encoding.ASCII.GetString(nameBytes);var data=png.Slice(position+8,length);
            if(Crc(png.Slice(position+4,length+4))!=BinaryPrimitives.ReadUInt32BigEndian(png[(position+8+length)..]))throw new InvalidDataException("PNG CRC mismatch: "+name);
            if(!header&&name!="IHDR")throw new InvalidDataException("IHDR must be first.");
            if(dataSeen&&name!="IDAT")dataEnded=true;
            switch(name)
            {
                case "IHDR":
                    if(header||length!=13)throw new InvalidDataException("Invalid IHDR.");header=true;
                    width=BinaryPrimitives.ReadInt32BigEndian(data);height=BinaryPrimitives.ReadInt32BigEndian(data[4..]);depth=data[8];type=data[9];
                    if(width<=0||height<=0||width>32768||height>32768)throw new NotSupportedException("PNG dimensions must be 1..32768.");
                    if((long)width*height*4>256*1024*1024)throw new NotSupportedException("Decoded PNG exceeds the 256 MiB pixel limit.");
                    if(type is not (0 or 2 or 3 or 4 or 6)||depth is not (1 or 2 or 4 or 8 or 16)||(type is 2 or 4 or 6&&depth is not (8 or 16))||(type==3&&depth==16))throw new NotSupportedException("Invalid PNG bit depth/color type combination.");
                    if(data[10]!=0||data[11]!=0)throw new InvalidDataException("Unknown PNG compression/filter method.");
                    interlace=data[12];if(interlace>1)throw new InvalidDataException("Invalid PNG interlace method.");break;
                case "PLTE":if(dataSeen||length==0||length%3!=0||length>768)throw new InvalidDataException("Invalid PLTE.");palette=data.ToArray();break;
                case "tRNS":if(dataSeen)throw new InvalidDataException("tRNS must precede IDAT.");transparent=data.ToArray();break;
                case "sRGB":if(dataSeen||length!=1||data[0]>3)throw new InvalidDataException("Invalid sRGB chunk.");srgb=true;break;
                case "gAMA":if(dataSeen||length!=4)throw new InvalidDataException("Invalid gAMA.");gamma=BinaryPrimitives.ReadUInt32BigEndian(data);if(gamma==0)throw new InvalidDataException("Zero PNG gamma.");break;
                case "cHRM":
                    uint[] expected=[31270,32900,64000,33000,30000,60000,15000,6000];
                    if(dataSeen||length!=32)throw new InvalidDataException("Invalid cHRM.");
                    for(int i=0;i<8;i++)if(BinaryPrimitives.ReadUInt32BigEndian(data[(i*4)..])!=expected[i])nonSrgbChromaticities=true;break;
                case "iCCP":
                    if(dataSeen||profile is not null||srgb)throw new InvalidDataException("Invalid/duplicate PNG color profile.");
                    int nul=data.IndexOf((byte)0);if(nul is <1 or >79||nul>data.Length-3||data[nul+1]!=0)throw new InvalidDataException("Invalid PNG ICC profile header.");
                    var zipped=data[(nul+2)..];ManagedZlib.Decode(zipped,prefix,true);
                    uint profileSize=BinaryPrimitives.ReadUInt32BigEndian(prefix);if(profileSize is <132 or >16*1024*1024)throw new InvalidDataException("PNG ICC size exceeds configured limits.");
                    profile=new IccProfile(ManagedZlib.Decode(zipped,(int)profileSize));break;
                case "cICP":
                    if(length!=4)throw new InvalidDataException("Invalid PNG cICP.");
                    if(data[0]!=1||data[1]!=13||data[2]!=0||data[3]!=1)throw new NotSupportedException("PNG HDR/wide-gamut cICP requires a supported colour transform.");break;
                case "acTL":case "fcTL":case "fdAT":throw new NotSupportedException("Animated PNG decoding is not implemented.");
                case "IDAT":
                    if(dataEnded)throw new InvalidDataException("IDAT chunks must be consecutive.");
                    if(idatCount++==0)firstIdat=position;
                    compressedLength=checked(compressedLength+length);dataSeen=true;break;
                case "IEND":if(length!=0||!dataSeen)throw new InvalidDataException("Invalid IEND.");end=true;break;
                default:if((nameBytes[0]&32)==0)throw new NotSupportedException("Unknown critical PNG chunk: "+name);break;
            }
            position+=length+12;if(end)break;
        }
        if(!end||position!=png.Length)throw new InvalidDataException("Missing IEND or trailing PNG payload.");
        if(profile is not null&&(srgb||profile.InputChannels!=(type is 0 or 4?1:3)))throw new InvalidDataException("PNG profile/color space conflict.");
        if(nonSrgbChromaticities&&profile is null&&!srgb)throw new NotSupportedException("Non-sRGB PNG chromaticities need an embedded ICC transform.");
        if(type==3&&(palette is null||palette.Length/3>(1<<depth)))throw new InvalidDataException("Missing/oversized palette.");
        if(transparent is not null&&((type==0&&transparent.Length!=2)||(type==2&&transparent.Length!=6)||(type==3&&(palette is null||transparent.Length>palette.Length/3))||type is 4 or 6))throw new InvalidDataException("Invalid transparency chunk.");

        PngValidation.ValidateRows(png,firstIdat,compressedLength,idatCount,width,height,depth,type,interlace,palette?.Length/3??0);
        var output=CodecBackend.DecodeRaw(png,width,height);
        try
        {
            if(!srgb&&profile is null&&gamma is {} exponent)
            {
                Span<byte> table=stackalloc byte[256];for(int i=0;i<256;i++)table[i]=ConvertGamma((byte)i,exponent);
                var bytes=output.GetPixelSpan();for(int i=0;i<bytes.Length;i+=4){bytes[i]=table[bytes[i]];bytes[i+1]=table[bytes[i+1]];bytes[i+2]=table[bytes[i+2]];}
            }
            profile?.Transform(output);return output;
        }
        catch{output.Dispose();throw;}
    }
    private static byte ConvertGamma(byte value,uint gamma)
    {double linear=Math.Pow(value/255.0,100000.0/gamma);double srgb=linear<=0.0031308?linear*12.92:1.055*Math.Pow(linear,1/2.4)-0.055;return (byte)Math.Clamp((int)Math.Round(srgb*255),0,255);}
    private static readonly uint[][] CrcTables=CreateCrcTables();
    private static uint[][] CreateCrcTables()
    {
        var tables=new uint[8][];for(int k=0;k<8;k++)tables[k]=new uint[256];
        for(uint i=0;i<256;i++)
        {
            uint value=i;for(int j=0;j<8;j++)value=(value>>1)^((value&1)!=0?0xedb88320u:0);tables[0][i]=value;
        }
        for(int k=1;k<8;k++)for(int i=0;i<256;i++){uint value=tables[k-1][i];tables[k][i]=tables[0][value&255]^(value>>8);}
        return tables;
    }
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        uint value=0xffffffff;var t=CrcTables;int i=0;
        for(;i<=data.Length-8;i+=8)
        {
            uint a=BinaryPrimitives.ReadUInt32LittleEndian(data[i..])^value,b=BinaryPrimitives.ReadUInt32LittleEndian(data[(i+4)..]);
            value=t[7][a&255]^t[6][(a>>8)&255]^t[5][(a>>16)&255]^t[4][a>>24]^t[3][b&255]^t[2][(b>>8)&255]^t[1][(b>>16)&255]^t[0][b>>24];
        }
        for(;i<data.Length;i++)value=t[0][(value^data[i])&255]^(value>>8);
        return value^0xffffffff;
    }
}
