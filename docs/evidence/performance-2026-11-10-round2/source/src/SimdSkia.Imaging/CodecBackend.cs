using System.Buffers;
using Sdcb.SimdCodecs.Abstractions;
using Sdcb.SimdCodecs.Bmp;
using Sdcb.SimdCodecs.Gif;
using Sdcb.SimdCodecs.Ico;
using Sdcb.SimdCodecs.Pnm;
using Sdcb.SimdCodecs.Qoi;
using Sdcb.SimdCodecs.Tiff;
using Facade=Sdcb.SimdCodecs.SimdCodecs;

namespace SimdSkia.Imaging;

/// <summary>File formats exposed by the imaging module; independent of the drawing backend.</summary>
public enum ImageFileFormat { Unknown,Png,Jpeg,Gif,Bmp,Ico,Webp,Tiff,Qoi,Netpbm,Jpeg2000,Jbig2 }

internal static class CodecBackend
{
    internal const int InputLimit=256*1024*1024;
    internal static void ValidateInput(ReadOnlySpan<byte> data)
    {if(data.Length>InputLimit)throw new NotSupportedException("Encoded image exceeds the 256 MiB budget.");}
    internal static void ValidateDimensions(int width,int height)
    {if(width<=0||height<=0||width>32768||height>32768||(long)width*height*4>InputLimit)throw new NotSupportedException("Decoded image exceeds the dimension/pixel budget.");}
    internal static ImageInfo ReadInfo(ReadOnlySpan<byte> data)
    {
        ValidateInput(data);
        if(!Facade.TryReadInfo(data,out var info))throw new InvalidDataException("Unsupported or invalid image header.");
        ValidateLayout(info);return info;
    }
    internal static void ValidateLayout(ImageInfo info)
    {
        ValidateDimensions(info.Width,info.Height);
        if(info.SampleFormat==SampleFormat.Float32)throw new NotSupportedException("Floating-point/HDR images require an explicit tone-mapping policy.");
        if(info.Channels is <1 or >4||(long)info.Width*info.Height*info.Channels*(info.SampleFormat==SampleFormat.UInt16?2:1)>InputLimit)
            throw new NotSupportedException("Decoded source samples exceed the budget.");
    }
    internal static PixelBuffer DecodeRaw(ReadOnlySpan<byte> data,int width,int height)
    {
        ValidateInput(data);ValidateDimensions(width,height);
        var output=PixelBuffer.RentRgba(width,height);
        try
        {
            if(!Facade.TryDecodeIntoRgba8(data,output.GetPixelSpan()))throw new InvalidDataException("Sdcb.SimdCodecs rejected the image payload.");
            return output;
        }
        catch{output.Dispose();throw;}
    }
    internal static PixelBuffer DecodeOther(ReadOnlySpan<byte> data)
    {
        var info=ReadInfo(data);
        // The fidelity API supplies ICC/transfer metadata; the display API alone
        // cannot turn a wide-gamut/HDR image into sRGB correctly.
        if(!Facade.TryDecode(data,out var image))throw new InvalidDataException("Invalid image payload.");
        ValidateDimensions(image.Width,image.Height);
        if(image.Width!=info.Width||image.Height!=info.Height)throw new InvalidDataException("Header and decoded dimensions differ.");
        if(image.SampleFormat==SampleFormat.Float32)throw new NotSupportedException("Floating-point/HDR images require an explicit tone-mapping policy.");
        var encoding=image.ColorEncoding;
        if(image.IccProfile.IsEmpty&&encoding!=ColorEncoding.AssumedSrgb)throw new NotSupportedException("Non-sRGB image signalling requires a supported colour transform.");
        var output=PixelBuffer.RentRgba(info.Width,info.Height);
        try
        {
            int sourceRow=checked(image.Width*image.Channels*(image.SampleFormat==SampleFormat.UInt16?2:1));
            if(image.Pixels.Length!=checked(sourceRow*image.Height))throw new InvalidDataException("Invalid decoded sample buffer length.");
            for(int y=0;y<image.Height;y++)PixelConverter.ConvertRow(image.Pixels.Slice(y*sourceRow,sourceRow),image.Channels,image.SampleFormat,output.GetRowSpan(y),ImagePixelFormat.Rgba32,image.Width);
            if(!image.IccProfile.IsEmpty)
            {var profile=new IccProfile(image.IccProfile);if(profile.InputChannels==4)throw new NotSupportedException("Raw CMYK samples are required for this ICC profile.");profile.Transform(output);}
            return output;
        }
        catch{output.Dispose();throw;}
    }
    internal static byte[] EncodeOther(PixelBuffer pixels,ImageFileFormat format)
    {
        if(format is not (ImageFileFormat.Gif or ImageFileFormat.Bmp or ImageFileFormat.Ico or ImageFileFormat.Qoi or ImageFileFormat.Netpbm or ImageFileFormat.Tiff))
            throw new NotSupportedException($"Encoding {format} is not available; decode-only formats are not silently changed to PNG.");
        using var source=new EncodingPixels(pixels,true);
        return format switch
        {
            ImageFileFormat.Gif=>GifEncoder.Encode(source.Span,pixels.Width,pixels.Height,4),
            ImageFileFormat.Bmp=>BmpEncoder.Encode(source.Span,pixels.Width,pixels.Height,ImagePixelFormat.Rgba32,new BmpEncodeOptions{Format=BmpEncodeFormat.Argb32}),
            ImageFileFormat.Ico=>IcoEncoder.Encode(source.Span,pixels.Width,pixels.Height),
            ImageFileFormat.Qoi=>QoiEncoder.Encode(source.Span,pixels.Width,pixels.Height,ImagePixelFormat.Rgba32),
            ImageFileFormat.Netpbm=>PnmEncoder.Encode(source.Span,pixels.Width,pixels.Height,ImagePixelFormat.Rgba32),
            ImageFileFormat.Tiff=>EncodeTiff(source.Span,pixels.Width,pixels.Height),
            _=>throw new NotSupportedException($"Encoding {format} is not available; decode-only formats are not silently changed to PNG.")
        };
    }
    private static byte[] EncodeTiff(ReadOnlySpan<byte> pixels,int width,int height)
    {
        using var stream=new MemoryStream();var writer=TiffWriter.Create(stream);
        try{writer.AddPageAsync(pixels.ToArray(),width,height,new TiffPageOptions{SamplesPerPixel=4,ExtraSamples=TiffExtraSamples.UnassociatedAlpha}).GetAwaiter().GetResult();writer.FlushAsync().GetAwaiter().GetResult();return stream.ToArray();}
        finally{writer.DisposeAsync().AsTask().GetAwaiter().GetResult();}
    }
}

/// <summary>Preserves padding, channel order and the PNG/JPEG alpha policies.</summary>
internal readonly ref struct EncodingPixels
{
    private readonly byte[]? rented;
    public ReadOnlySpan<byte> Span {get;}
    public EncodingPixels(PixelBuffer pixels,bool straightAlpha)
    {
        ArgumentNullException.ThrowIfNull(pixels);pixels.ThrowIfDisposed();
        if((long)pixels.Width*pixels.Height*4>CodecBackend.InputLimit)throw new NotSupportedException("Encoding pixels exceed the 256 MiB budget.");
        bool convertAlpha=straightAlpha&&pixels.Alpha==AlphaFormat.Premultiplied;
        if(pixels.Format==PixelFormat.Rgba8888&&pixels.RowBytes==pixels.Width*4&&!convertAlpha){rented=null;Span=pixels.GetPixelSpan();return;}
        rented=ArrayPool<byte>.Shared.Rent(checked(pixels.Width*pixels.Height*4));var destination=rented.AsSpan(0,pixels.Width*pixels.Height*4);
        if(pixels.Format==PixelFormat.Alpha8){for(int y=0;y<pixels.Height;y++)for(int x=0;x<pixels.Width;x++){int at=(y*pixels.Width+x)*4;destination[at]=destination[at+1]=destination[at+2]=0;destination[at+3]=pixels.GetRowSpan(y)[x];}Span=destination;return;}
        int ri=pixels.Format==PixelFormat.Rgba8888?0:2;
        for(int y=0;y<pixels.Height;y++)
        {
            var row=pixels.GetRowSpan(y);var target=destination.Slice(y*pixels.Width*4,pixels.Width*4);
            for(int x=0;x<pixels.Width;x++)
            {
                int at=x*4;byte a=row[at+3],r=row[at+ri],g=row[at+1],b=row[at+2-ri];
                if(convertAlpha){r=PixelBuffer.Unmultiply(r,a);g=PixelBuffer.Unmultiply(g,a);b=PixelBuffer.Unmultiply(b,a);}
                target[at]=r;target[at+1]=g;target[at+2]=b;target[at+3]=a;
            }
        }
        Span=destination;
    }
    public void Dispose(){if(rented is not null)ArrayPool<byte>.Shared.Return(rented);}
}
