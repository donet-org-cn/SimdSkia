using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using JpegLibrary;

namespace SimdSkia.Imaging;

/// <summary>Managed 8-bit baseline/progressive JPEG. No native codec is loaded.</summary>
public static class JpegCodec
{
    public static PixelBuffer Decode(ReadOnlySpan<byte> encoded)
    {
        if(encoded.Length>256*1024*1024)throw new NotSupportedException("JPEG input exceeds 256 MiB.");
        var metadata=ReadMetadata(encoded);
        var decoder=new JpegDecoder();decoder.SetInput(encoded.ToArray());decoder.Identify();
        int width=decoder.Width,height=decoder.Height,channels=decoder.NumberOfComponents;
        if(decoder.StartOfFrame is not (JpegMarker.StartOfFrame0 or JpegMarker.StartOfFrame1 or JpegMarker.StartOfFrame2))throw new NotSupportedException("Only validated sequential/progressive DCT JPEG is supported; lossless/arithmetic variants require separate validation.");
        if(decoder.Precision!=8||channels is not (1 or 3 or 4))throw new NotSupportedException("JPEG requires 8-bit gray, RGB/YCbCr or CMYK/YCCK input.");
        if(channels==4&&decoder.StartOfFrame==JpegMarker.StartOfFrame2)throw new NotSupportedException("The pinned managed decoder cannot safely flush four-component progressive JPEG. Baseline CMYK is supported; progressive CMYK is explicitly rejected.");
        if(width<=0||height<=0||width>32768||height>32768||(long)width*height*4>256*1024*1024)throw new NotSupportedException("JPEG decoded size exceeds configured limits.");
        var samples=ArrayPool<byte>.Shared.Rent(checked(width*height*channels));
        try
        {
            samples.AsSpan(0,width*height*channels).Clear();
            decoder.SetOutputWriter(new Writer(width,height,channels,samples));decoder.Decode();
            var horizontal=new int[channels];var vertical=new int[channels];
            for(int c=0;c<channels;c++)
            {
                int h=decoder.GetHorizontalSampling(c),v=decoder.GetVerticalSampling(c);
                if(h<=0||v<=0||decoder.GetMaximumHorizontalSampling()%h!=0||decoder.GetMaximumVerticalSampling()%v!=0)throw new NotSupportedException("Fractional JPEG sampling ratios are unsupported.");
                horizontal[c]=decoder.GetMaximumHorizontalSampling()/h;vertical[c]=decoder.GetMaximumVerticalSampling()/v;
            }
            IccProfile? profile=metadata.Profile is null?null:new(metadata.Profile);
            if(profile is not null&&profile.InputChannels!=channels)throw new InvalidDataException("JPEG ICC component count differs from image.");
            var output=PixelBuffer.RentRgba(width,height);
            try
            {
                Span<double> input=stackalloc double[4];
                bool rgb=metadata.Transform==0||metadata.Rgb;
                for(int y=0;y<height;y++)
                {
                    var row=output.GetRowSpan(y);
                    for(int x=0;x<width;x++)
                    {
                        double a=Sample(0,x,y),b=channels>1?Sample(1,x,y):a,c=channels>1?Sample(2,x,y):a;
                        if((channels==3&&!rgb)||(channels==4&&metadata.Transform==2)){double cb=b-128,cr=c-128;b=a-.344136*cb-.714136*cr;c=a+1.772*cb;a+=1.402*cr;}
                        Rgba32 color;
                        if(channels==4)
                        {
                            double k=Sample(3,x,y);
                            if(metadata.Transform==2){input[0]=Math.Clamp(a,0,255)/255;input[1]=Math.Clamp(b,0,255)/255;input[2]=Math.Clamp(c,0,255)/255;input[3]=1-k/255;}
                            else{bool inverted=metadata.Transform==0;input[0]=inverted?1-a/255:a/255;input[1]=inverted?1-b/255:b/255;input[2]=inverted?1-c/255:c/255;input[3]=inverted?1-k/255:k/255;}
                            color=profile is not null?profile.ToSrgb(input):new(Byte(255*(1-input[0])*(1-input[3])),Byte(255*(1-input[1])*(1-input[3])),Byte(255*(1-input[2])*(1-input[3])),255);
                        }
                        else
                        {
                            input[0]=Math.Clamp(a,0,255)/255;input[1]=Math.Clamp(b,0,255)/255;input[2]=Math.Clamp(c,0,255)/255;
                            color=profile is not null?profile.ToSrgb(input[..channels]):new(Byte(a),Byte(b),Byte(c),255);
                        }
                        int p=x*4;row[p]=color.R;row[p+1]=color.G;row[p+2]=color.B;row[p+3]=255;
                    }
                }
                return output;
            }
            catch{output.Dispose();throw;}
            double Sample(int component,int x,int y)
            {
                int hx=horizontal[component],vy=vertical[component];
                if(hx==1&&vy==1)return samples[(y*width+x)*channels+component];
                double fx=(x+.5)/hx-.5,fy=(y+.5)/vy-.5;int ix=(int)Math.Floor(fx),iy=(int)Math.Floor(fy);double tx=fx-ix,ty=fy-iy;
                double At(int px,int py)=>samples[(Math.Clamp(py,0,(height+vy-1)/vy-1)*vy*width+Math.Clamp(px,0,(width+hx-1)/hx-1)*hx)*channels+component];
                return(At(ix,iy)*(1-tx)+At(ix+1,iy)*tx)*(1-ty)+(At(ix,iy+1)*(1-tx)+At(ix+1,iy+1)*tx)*ty;
            }
        }
        finally{ArrayPool<byte>.Shared.Return(samples);}
    }
    private static byte Byte(double value)=>(byte)Math.Clamp((int)Math.Floor(value+.5),0,255);
    public static byte[] Encode(PixelBuffer pixels,int quality=90)
    {
        pixels.ThrowIfDisposed();if(quality is <0 or >100)throw new ArgumentOutOfRangeException(nameof(quality));
        if(pixels.Width>65535||pixels.Height>65535)throw new NotSupportedException("JPEG dimensions exceed UInt16.");
        var encoder=new JpegEncoder();
        encoder.SetQuantizationTable(JpegStandardQuantizationTable.ScaleByQuality(JpegStandardQuantizationTable.GetLuminanceTable(JpegElementPrecision.Precision8Bit,0),Math.Max(1,quality)));
        encoder.SetQuantizationTable(JpegStandardQuantizationTable.ScaleByQuality(JpegStandardQuantizationTable.GetChrominanceTable(JpegElementPrecision.Precision8Bit,1),Math.Max(1,quality)));
        encoder.SetHuffmanTable(true,0,JpegStandardHuffmanEncodingTable.GetLuminanceDCTable());encoder.SetHuffmanTable(false,0,JpegStandardHuffmanEncodingTable.GetLuminanceACTable());
        encoder.SetHuffmanTable(true,1,JpegStandardHuffmanEncodingTable.GetChrominanceDCTable());encoder.SetHuffmanTable(false,1,JpegStandardHuffmanEncodingTable.GetChrominanceACTable());
        encoder.AddComponent(1,0,0,0,1,1);encoder.AddComponent(2,1,1,1,1,1);encoder.AddComponent(3,1,1,1,1,1);
        encoder.SetInputReader(new Reader(pixels));var output=new ArrayBufferWriter<byte>();encoder.SetOutput(output);encoder.Encode();return output.WrittenSpan.ToArray();
    }
    private sealed class Reader(PixelBuffer pixels):JpegBlockInputReader
    {
        public override int Width=>pixels.Width;
        public override int Height=>pixels.Height;
        public override void ReadBlock(ref short blockRef,int componentIndex,int x,int y)
        {
            var block=MemoryMarshal.CreateSpan(ref blockRef,64);
            for(int j=0;j<8;j++)for(int i=0;i<8;i++)
            {
                // Skia's default JPEG alpha policy consumes stored RGB: PMA
                // remains black-matted; straight and opaque storage ignore A.
                var p=pixels.GetRowSpan(Math.Min(y+j,Height-1)).Slice(Math.Min(x+i,Width-1)*4,4);int ri=pixels.Format==PixelFormat.Rgba8888?0:2;
                double r=p[ri],g=p[1],b=p[2-ri];
                block[j*8+i]=(short)Byte(componentIndex switch{0=>.299*r+.587*g+.114*b,1=>128-.168736*r-.331264*g+.5*b,_=>128+.5*r-.418688*g-.081312*b});
            }
        }
    }
    private sealed class Writer(int width,int height,int channels,byte[] samples):JpegBlockOutputWriter
    {
        public override void WriteBlock(ref short blockRef,int componentIndex,int x,int y)
        {
            if(componentIndex<0||componentIndex>=channels||x<0||y<0)throw new InvalidDataException("Invalid JPEG output block.");
            var block=MemoryMarshal.CreateReadOnlySpan(ref blockRef,64);
            for(int j=0;j<Math.Min(8,height-y);j++)for(int i=0;i<Math.Min(8,width-x);i++)samples[((y+j)*width+x+i)*channels+componentIndex]=(byte)Math.Clamp((int)block[j*8+i],0,255);
        }
    }
    private sealed record Metadata(int Transform,bool Rgb,byte[]? Profile);
    private static Metadata ReadMetadata(ReadOnlySpan<byte> data)
    {
        if(data.Length<4||data[0]!=255||data[1]!=216)throw new InvalidDataException("Invalid JPEG SOI.");
        int p=2,transform=-1,iccCount=0;bool rgb=false,end=false;var chunks=new SortedDictionary<int,byte[]>();
        while(p<data.Length)
        {
            if(data[p++]!=255)continue;while(p<data.Length&&data[p]==255)p++;if(p==data.Length)break;int marker=data[p++];
            if(marker==0||marker is >=208 and <=215)continue;
            if(marker==217){end=true;break;}if(marker==216)throw new InvalidDataException("Unexpected JPEG SOI.");
            if(p>data.Length-2)throw new InvalidDataException("Truncated JPEG marker.");int size=BinaryPrimitives.ReadUInt16BigEndian(data[p..]);
            if(size<2||size>data.Length-p)throw new InvalidDataException("Truncated JPEG segment.");var payload=data.Slice(p+2,size-2);p+=size;
            if(marker==238&&payload.Length>=12&&payload[..5].SequenceEqual("Adobe"u8)){transform=payload[11];if(transform>2)throw new NotSupportedException("Unknown Adobe JPEG color transform.");}
            if(marker is 192 or 193 or 194&&payload.Length>=15)rgb=payload[5]==3&&payload[6]=='R'&&payload[9]=='G'&&payload[12]=='B';
            if(marker==226&&payload.Length>=14&&payload[..12].SequenceEqual("ICC_PROFILE\0"u8))
            {
                int index=payload[12],count=payload[13];if(index<1||count<1||index>count||(iccCount!=0&&iccCount!=count)||!chunks.TryAdd(index,payload[14..].ToArray()))throw new InvalidDataException("Invalid JPEG ICC chunk sequence.");iccCount=count;
            }
        }
        if(!end)throw new InvalidDataException("JPEG is missing EOI.");
        byte[]? profile=null;
        if(iccCount!=0){if(chunks.Count!=iccCount)throw new InvalidDataException("Incomplete JPEG ICC profile.");int length=chunks.Values.Sum(x=>x.Length);if(length>16*1024*1024)throw new NotSupportedException("JPEG ICC exceeds 16 MiB.");profile=new byte[length];int at=0;foreach(var chunk in chunks.Values){chunk.CopyTo(profile,at);at+=chunk.Length;}}
        return new(transform,rgb,profile);
    }
}
