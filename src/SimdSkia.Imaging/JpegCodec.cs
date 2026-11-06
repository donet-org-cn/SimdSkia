using System.Buffers.Binary;
using Sdcb.SimdCodecs.Jpeg;

namespace SimdSkia.Imaging;

/// <summary>JPEG pixels and encoding are provided by Sdcb.SimdCodecs.</summary>
public static class JpegCodec
{
    public static PixelBuffer Decode(ReadOnlySpan<byte> encoded)
    {
        CodecBackend.ValidateInput(encoded);
        var metadata=ReadMetadata(encoded);
        var info=JpegDecoder.ReadInfo(encoded);
        CodecBackend.ValidateDimensions(info.Width,info.Height);
        IccProfile? profile=metadata.Profile is null?null:new(metadata.Profile);
        if(profile is not null&&profile.InputChannels!=info.Components)throw new InvalidDataException("JPEG ICC component count differs from image.");
        // The package display API has already converted CMYK to RGB. It does not
        // expose original four-channel samples required by a CMYK ICC transform.
        if(profile?.InputChannels==4)throw new NotSupportedException("CMYK JPEG with ICC needs raw CMYK samples, which the package display API does not expose.");
        var output=CodecBackend.DecodeRaw(encoded,info.Width,info.Height);
        try{profile?.Transform(output);return output;}catch{output.Dispose();throw;}
    }
    public static byte[] Encode(PixelBuffer pixels,int quality=90)
    {
        ArgumentNullException.ThrowIfNull(pixels);if(quality is <0 or >100)throw new ArgumentOutOfRangeException(nameof(quality));
        using var source=new EncodingPixels(pixels,false);
        return JpegEncoder.Encode(source.Span,pixels.Width,pixels.Height,4,new JpegEncodeOptions
        {Quality=Math.Max(1,quality),Subsampling=JpegSubsampling.Chroma444,OptimizeHuffman=false,Progressive=false});
    }
    /// <summary>Explicit alpha and chroma policies for the compatibility encoder-options API.</summary>
    public static byte[] Encode(PixelBuffer pixels,int quality,bool subsample420,bool blendOnBlack)
    {
        ArgumentNullException.ThrowIfNull(pixels);pixels.ThrowIfDisposed();using var source=new EncodingPixels(pixels,false);
        byte[]? rented=null;ReadOnlySpan<byte> input=source.Span;
        try
        {
            if(blendOnBlack&&pixels.Alpha==AlphaFormat.Straight)
            {
                rented=System.Buffers.ArrayPool<byte>.Shared.Rent(input.Length);input.CopyTo(rented);var converted=rented.AsSpan(0,input.Length);
                for(int p=0;p<converted.Length;p+=4){byte a=converted[p+3];for(int c=0;c<3;c++)converted[p+c]=PixelBuffer.Multiply(converted[p+c],a);}input=converted;
            }
            return JpegEncoder.Encode(input,pixels.Width,pixels.Height,4,new JpegEncodeOptions{Quality=Math.Clamp(quality,1,100),Subsampling=subsample420?JpegSubsampling.Chroma420:JpegSubsampling.Chroma444,OptimizeHuffman=false,Progressive=false});
        }
        finally{if(rented is not null)System.Buffers.ArrayPool<byte>.Shared.Return(rented);}
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
