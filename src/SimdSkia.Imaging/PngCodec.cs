using System.Buffers.Binary;
using System.Buffers;
using System.Text;

namespace SimdSkia.Imaging;

public static class PngCodec
{
    private static ReadOnlySpan<byte> Signature=>[137,80,78,71,13,10,26,10];
    public static byte[] Encode(PixelBuffer pixels)
        =>Encode(pixels,System.IO.Compression.CompressionLevel.Optimal);
    public static byte[] Encode(PixelBuffer pixels,System.IO.Compression.CompressionLevel compression)
    {
        if(!Enum.IsDefined(compression))throw new ArgumentOutOfRangeException(nameof(compression));
        pixels.ThrowIfDisposed();int row=checked(pixels.Width*4);var raw=new byte[checked((row+1)*pixels.Height)];
        int ri=pixels.Format==PixelFormat.Rgba8888?0:2;
        for(int y=0;y<pixels.Height;y++)
        {
            var source=pixels.GetRowSpan(y)[..row];var destination=raw.AsSpan(y*(row+1)+1,row);
            if(pixels.Format==PixelFormat.Rgba8888&&pixels.Alpha==AlphaFormat.Straight){source.CopyTo(destination);continue;}
            for(int x=0;x<row;x+=4)
            {
                byte r=source[x+ri],g=source[x+1],b=source[x+2-ri],a=source[x+3];
                if(pixels.Alpha==AlphaFormat.Premultiplied){r=PixelBuffer.Unmultiply(r,a);g=PixelBuffer.Unmultiply(g,a);b=PixelBuffer.Unmultiply(b,a);}
                destination[x]=r;destination[x+1]=g;destination[x+2]=b;destination[x+3]=pixels.Alpha==AlphaFormat.Opaque?(byte)255:a;
            }
        }
        var encoded=compression==System.IO.Compression.CompressionLevel.NoCompression?ManagedZlib.EncodeStored(raw):ManagedZlib.Encode(raw,compression==System.IO.Compression.CompressionLevel.Fastest?4:compression==System.IO.Compression.CompressionLevel.SmallestSize?256:32);var output=new byte[checked(encoded.Length+70)];Signature.CopyTo(output);
        Span<byte> header=stackalloc byte[13];header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header,pixels.Width);BinaryPrimitives.WriteInt32BigEndian(header[4..],pixels.Height);header[8]=8;header[9]=6;
        int offset=8;WriteChunk(output,ref offset,"IHDR"u8,header);WriteChunk(output,ref offset,"sRGB"u8,[0]);WriteChunk(output,ref offset,"IDAT"u8,encoded);WriteChunk(output,ref offset,"IEND"u8,[]);return output;
    }

    public static PixelBuffer Decode(ReadOnlySpan<byte> png)
    {
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
        int channels=type==2?3:type==4?2:type==6?4:1;int row=checked((width*channels*depth+7)/8),bpp=Math.Max(1,(channels*depth+7)/8);
        (int X,int Y,int Dx,int Dy)[] passes=interlace==0?[(0,0,1,1)]:[(0,0,8,8),(4,0,8,8),(0,4,4,8),(2,0,4,4),(0,2,2,4),(1,0,2,2),(0,1,1,2)];
        int rawLength=0;foreach(var pass in passes){int pw=(Math.Max(0,width-pass.X)+pass.Dx-1)/pass.Dx,ph=(Math.Max(0,height-pass.Y)+pass.Dy-1)/pass.Dy;if(pw!=0&&ph!=0)rawLength=checked(rawLength+(((pw*channels*depth+7)/8)+1)*ph);}
        if(rawLength>256*1024*1024)throw new NotSupportedException("Image inflation exceeds the configured 256 MiB limit.");
        byte[]? rented=null,compressedBytes=null;
        try
        {
        ReadOnlySpan<byte> compressed;
        if(idatCount==1)compressed=png.Slice(firstIdat+8,compressedLength);
        else
        {
            compressedBytes=ArrayPool<byte>.Shared.Rent(compressedLength);
            int chunk=firstIdat,at=0;
            for(int i=0;i<idatCount;i++)
            {int size=checked((int)BinaryPrimitives.ReadUInt32BigEndian(png[chunk..]));png.Slice(chunk+8,size).CopyTo(compressedBytes.AsSpan(at,size));at+=size;chunk+=size+12;}
            compressed=compressedBytes.AsSpan(0,compressedLength);
        }
        rented=ArrayPool<byte>.Shared.Rent(rawLength);
        var raw=rented.AsSpan(0,rawLength);
        ManagedZlib.Decode(compressed,raw);
        byte[]? gammaTable=null;
        if(!srgb&&profile is null&&gamma is {} exponent){gammaTable=new byte[256];for(int i=0;i<256;i++)gammaTable[i]=ConvertGamma((byte)i,exponent);}
        var output=PixelBuffer.RentRgba(width,height);
        try
        {
            int rawOffset=0;
            foreach(var pass in passes)
            {
              int pw=(Math.Max(0,width-pass.X)+pass.Dx-1)/pass.Dx,ph=(Math.Max(0,height-pass.Y)+pass.Dy-1)/pass.Dy;
              if(pw==0||ph==0)continue;int passRow=(pw*channels*depth+7)/8;
              for(int py=0;py<ph;py++)
              {
                int y=pass.Y+py*pass.Dy,offset=rawOffset;rawOffset+=passRow+1;int filter=raw[offset];if(filter>4)throw new InvalidDataException("Invalid PNG row filter.");
                var line=raw.Slice(offset+1,passRow);ReadOnlySpan<byte> above=py==0?ReadOnlySpan<byte>.Empty:raw.Slice(offset-passRow,passRow);
                PngFilters.Reconstruct(line,above,bpp,filter);
                var destination=output.GetRowSpan(y);
                if(type==6&&depth==8&&interlace==0&&gammaTable is null){line.CopyTo(destination);continue;}
                if(type==2&&depth==8&&interlace==0&&gammaTable is null&&transparent is null){PngFilters.ExpandRgb(line,destination);continue;}
                for(int x=0;x<pw;x++)
                {
                    int at=x*channels;byte r,g,b,a=255;
                    int sample=ReadSample(line,at,depth);
                    if(type==0){r=g=b=depth==16?Reduce(sample,depth):(byte)(sample*255/((1<<depth)-1));if(transparent is not null&&sample==BinaryPrimitives.ReadUInt16BigEndian(transparent))a=0;}
                    else if(type==3){int index=sample;if(index>=palette!.Length/3)throw new InvalidDataException("Palette index outside table.");r=palette[index*3];g=palette[index*3+1];b=palette[index*3+2];if(transparent is not null&&index<transparent.Length)a=transparent[index];}
                    else if(type==4){r=g=b=Reduce(sample,depth);a=Reduce(ReadSample(line,at+1,depth),depth);}
                    else{int green=ReadSample(line,at+1,depth),blue=ReadSample(line,at+2,depth);r=Reduce(sample,depth);g=Reduce(green,depth);b=Reduce(blue,depth);if(type==6)a=Reduce(ReadSample(line,at+3,depth),depth);else if(transparent is not null&&sample==BinaryPrimitives.ReadUInt16BigEndian(transparent)&&green==BinaryPrimitives.ReadUInt16BigEndian(transparent.AsSpan(2))&&blue==BinaryPrimitives.ReadUInt16BigEndian(transparent.AsSpan(4)))a=0;}
                    if(gammaTable is not null){r=gammaTable[r];g=gammaTable[g];b=gammaTable[b];}
                    int target=(pass.X+x*pass.Dx)*4;destination[target]=r;destination[target+1]=g;destination[target+2]=b;destination[target+3]=a;
                }
              }
            }
            profile?.Transform(output);
            return output;
        }
        catch{output.Dispose();throw;}
        }
        finally{if(rented is not null)ArrayPool<byte>.Shared.Return(rented);if(compressedBytes is not null)ArrayPool<byte>.Shared.Return(compressedBytes);}
    }
    private static int ReadSample(ReadOnlySpan<byte> line,int sample,int depth)=>depth==16?BinaryPrimitives.ReadUInt16BigEndian(line[(sample*2)..]):depth==8?line[sample]:(line[sample*depth/8]>>(8-depth-(sample*depth%8)))&((1<<depth)-1);
    private static byte Reduce(int sample,int depth)=>(byte)(depth==16?sample>>8:sample);
    private static byte ConvertGamma(byte value,uint gamma)
    {double linear=Math.Pow(value/255.0,100000.0/gamma);double srgb=linear<=0.0031308?linear*12.92:1.055*Math.Pow(linear,1/2.4)-0.055;return (byte)Math.Clamp((int)Math.Round(srgb*255),0,255);}
    private static void WriteChunk(Span<byte> output,ref int offset,ReadOnlySpan<byte> name,ReadOnlySpan<byte> data)
    {
        var chunk=output.Slice(offset,data.Length+12);BinaryPrimitives.WriteInt32BigEndian(chunk,data.Length);name.CopyTo(chunk[4..]);data.CopyTo(chunk[8..]);
        BinaryPrimitives.WriteUInt32BigEndian(chunk[(8+data.Length)..],Crc(chunk.Slice(4,data.Length+4)));offset+=data.Length+12;
    }
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
