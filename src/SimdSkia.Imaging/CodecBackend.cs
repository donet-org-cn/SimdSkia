using System.Buffers;
using System.IO.Compression;
using Sdcb.SimdCodecs.Abstractions;
using CodecPixelFormat=Sdcb.SimdCodecs.Abstractions.PixelFormat;
using Sdcb.SimdCodecs.Bmp;
using Sdcb.SimdCodecs.Gif;
using Sdcb.SimdCodecs.Ico;
using Sdcb.SimdCodecs.Pnm;
using Sdcb.SimdCodecs.Qoi;
using Sdcb.SimdCodecs.Tiff;
using Sdcb.SimdCodecs.Webp;
using Facade=Sdcb.SimdCodecs.ImageCodecs;

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
            if(!Facade.TryDecodeInto(data,output.GetPixelSpan(),output.RowBytes,CodecPixelFormat.Rgba32))throw new InvalidDataException("Sdcb.SimdCodecs rejected the image payload.");
            return output;
        }
        catch{output.Dispose();throw;}
    }
    internal static PixelBuffer DecodeOther(ReadOnlySpan<byte> data)
    {
        var info=ReadInfo(data);
        if (CanDecodeDirectly(data, info) || CanDecodeTiffDirectly(data, info))
            return DecodeRaw(data, info.Width, info.Height);

        // The fidelity API supplies ICC/transfer metadata; the display API alone
        // cannot turn a wide-gamut/HDR image into sRGB correctly.
        if(!Facade.TryDecode(data,out var decoded))throw new InvalidDataException("Invalid image payload.");
        using var image=decoded;
        ValidateDimensions(image.Width,image.Height);
        if(image.Width!=info.Width||image.Height!=info.Height)throw new InvalidDataException("Header and decoded dimensions differ.");
        if(image.SampleFormat==SampleFormat.Float32)throw new NotSupportedException("Floating-point/HDR images require an explicit tone-mapping policy.");
        var encoding=image.ColorEncoding;
        if(image.Metadata.Icc.IsEmpty&&encoding!=ColorEncoding.AssumedSrgb)throw new NotSupportedException("Non-sRGB image signalling requires a supported colour transform.");
        var output=PixelBuffer.RentRgba(info.Width,info.Height);
        try
        {
            int sourceRow=checked(image.Width*image.Channels*(image.SampleFormat==SampleFormat.UInt16?2:1));
            if(image.Pixels.Length!=checked(sourceRow*image.Height))throw new InvalidDataException("Invalid decoded sample buffer length.");
            for(int y=0;y<image.Height;y++)PixelConverter.ConvertRow(image.Pixels.Slice(y*sourceRow,sourceRow),image.Channels,image.SampleFormat,output.GetRowSpan(y),CodecPixelFormat.Rgba32,image.Width);
            if(!image.Metadata.Icc.IsEmpty)
            {var profile=new IccProfile(image.Metadata.Icc.Span);if(profile.InputChannels==4)throw new NotSupportedException("Raw CMYK samples are required for this ICC profile.");profile.Transform(output);}
            return output;
        }
        catch{output.Dispose();throw;}
    }
    internal static bool CanDecodeDirectly(ReadOnlySpan<byte> data, ImageInfo info)
    {
        if (info.ColorEncoding != ColorEncoding.AssumedSrgb)
            return false;

        // These built-in decoders can write straight RGBA without a source raster.
        // BMP and WebP may carry an ICC profile even when read-info reports sRGB.
        return info.Format switch
        {
            ImageFormat.Gif => GifImageDecoder.CanDecode(data),
            ImageFormat.Ico => IcoImageDecoder.CanDecode(data),
            ImageFormat.Cur => CurImageDecoder.CanDecode(data),
            ImageFormat.Qoi => QoiImageDecoder.CanDecode(data),
            ImageFormat.Pnm => PnmImageDecoder.CanDecode(data),
            ImageFormat.Bmp => BmpImageDecoder.CanDecode(data)
                && BmpImageDecoder.TryReadMetadata(data, out var bmp) && bmp.Icc.IsEmpty,
            ImageFormat.Webp => WebpImageDecoder.CanDecode(data)
                && WebpImageDecoder.TryReadMetadata(data, out var webp) && webp.Icc.IsEmpty,
            _ => false
        };
    }
    private static bool CanDecodeTiffDirectly(ReadOnlySpan<byte> data, ImageInfo info)
    {
        if (info.Format != ImageFormat.Tiff || info.SampleFormat == SampleFormat.Float32 ||
            info.ColorEncoding != ColorEncoding.AssumedSrgb || !TiffImageDecoder.CanDecode(data))
            return false;

        var layout = TiffReader.ReadLayout(data);
        int rows = Math.Min(layout.Height, layout.IsTiled ? layout.TileHeight : layout.RowsPerStrip);
        // The package rents a band per strip or tile row. Keep large single
        // strips on the fidelity path to avoid retaining a full-image band.
        long bandBytes = (long)rows * layout.Width * info.Channels * (info.SampleFormat == SampleFormat.UInt16 ? 2 : 1);
        return bandBytes <= 512 * 1024 && TiffImageDecoder.TryReadMetadata(data, out var metadata) && metadata.Icc.IsEmpty;
    }

    internal static byte[] EncodeOther(PixelBuffer pixels,ImageFileFormat format)
    {
        if(format is not (ImageFileFormat.Gif or ImageFileFormat.Bmp or ImageFileFormat.Ico or ImageFileFormat.Qoi or ImageFileFormat.Netpbm or ImageFileFormat.Tiff))
            throw new NotSupportedException($"Encoding {format} is not available; decode-only formats are not silently changed to PNG.");
        if (format == ImageFileFormat.Tiff)
            return EncodeTiff(pixels);
        using var source=new EncodingPixels(pixels,true);
        return format switch
        {
            ImageFileFormat.Gif=>GifEncoder.Encode(source.Span,pixels.Width,pixels.Height,4),
            ImageFileFormat.Bmp=>BmpEncoder.Encode(source.Span,pixels.Width,pixels.Height,CodecPixelFormat.Rgba32,new BmpEncodeOptions{Format=BmpEncodeFormat.Argb32}),
            ImageFileFormat.Ico=>IcoEncoder.Encode(source.Span,pixels.Width,pixels.Height),
            ImageFileFormat.Qoi=>QoiEncoder.Encode(source.Span,pixels.Width,pixels.Height,CodecPixelFormat.Rgba32),
            ImageFileFormat.Netpbm=>PnmEncoder.Encode(source.Span,pixels.Width,pixels.Height,CodecPixelFormat.Rgba32),
            _=>throw new NotSupportedException($"Encoding {format} is not available; decode-only formats are not silently changed to PNG.")
        };
    }
    private static byte[] EncodeTiff(PixelBuffer pixels)
    {
        EncodingPixels.Validate(pixels);
        int rowsPerStrip = Math.Min(pixels.Height, Math.Max(1, (256 * 1024) / (pixels.Width * 4)));
        using var source = new TiffStrips(pixels, rowsPerStrip);
        using var stream = new MemoryStream();
        var writer = TiffWriter.Create(stream);
        try
        {
            writer.AddPageAsync(source, new TiffPageOptions
            {
                SamplesPerPixel = 4,
                ExtraSamples = TiffExtraSamples.UnassociatedAlpha,
                RowsPerStrip = rowsPerStrip,
            }).GetAwaiter().GetResult();
            writer.FlushAsync().GetAwaiter().GetResult();
            return stream.ToArray();
        }
        finally { writer.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    private sealed class TiffStrips : IImageSource, IDisposable
    {
        private readonly PixelBuffer pixels;
        private readonly int rowsPerStrip, rowBytes;
        private readonly ReadOnlyMemory<byte> managed;
        private readonly byte[]? scratch;
        private readonly bool unpremultiply;
        private readonly MemoryStream compressed = new();

        internal TiffStrips(PixelBuffer pixels, int rowsPerStrip)
        {
            this.pixels = pixels;
            this.rowsPerStrip = rowsPerStrip;
            rowBytes = pixels.Width * 4;
            unpremultiply = pixels.Alpha == AlphaFormat.Premultiplied;
            if (pixels.Format == PixelFormat.Rgba8888 && pixels.RowBytes == rowBytes &&
                (!unpremultiply || PixelBuffer.HasOnlyOpaqueAlpha(pixels.GetPixelSpan())))
                managed = pixels.GetManagedMemory();
            if (managed.IsEmpty)
                scratch = ArrayPool<byte>.Shared.Rent(rowBytes * rowsPerStrip);
        }

        public int Width => pixels.Width;
        public int Height => pixels.Height;
        public int SegmentCount => (Height + rowsPerStrip - 1) / rowsPerStrip;
        public TiffCompression Compression => TiffCompression.Deflate;
        public TiffPhotometric Photometric => TiffPhotometric.Rgb;
        public bool IsPreCompressed => true;

        public ValueTask<ReadOnlyMemory<byte>> GetSegmentAsync(int index, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            int first = index * rowsPerStrip;
            int rows = Math.Min(rowsPerStrip, Height - first);
            int length = rows * rowBytes;
            ReadOnlySpan<byte> input;
            if (scratch is null)
                input = managed.Span.Slice(first * rowBytes, length);
            else
            {
                for (int y = 0; y < rows; y++)
                    EncodingPixels.CopyRow(pixels, first + y, scratch.AsSpan(y * rowBytes, rowBytes), unpremultiply);
                input = scratch.AsSpan(0, length);
            }

            // The writer awaits each segment write before requesting the next.
            // Reuse the compressed buffer for the whole page.
            compressed.Position = 0;
            compressed.SetLength(0);
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(input);
            return new(compressed.GetBuffer().AsMemory(0, (int)compressed.Length));
        }

        public void Dispose()
        {
            if (scratch is not null)
                ArrayPool<byte>.Shared.Return(scratch);
            compressed.Dispose();
        }
    }
}

/// <summary>Preserves padding, channel order and the PNG/JPEG alpha policies.</summary>
internal readonly ref struct EncodingPixels
{
    private readonly PixelStorage? storage;
    public ReadOnlySpan<byte> Span {get;}
    internal static void Validate(PixelBuffer pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        pixels.ThrowIfDisposed();
        if ((long)pixels.Width * pixels.Height * 4 > CodecBackend.InputLimit)
            throw new NotSupportedException("Encoding pixels exceed the 256 MiB budget.");
    }

    public EncodingPixels(PixelBuffer pixels,bool straightAlpha)
    {
        Validate(pixels);
        int rowBytes = pixels.Width * 4;
        bool rgba = pixels.Format == PixelFormat.Rgba8888;
        bool convertAlpha = straightAlpha && pixels.Alpha == AlphaFormat.Premultiplied;
        if (rgba && pixels.RowBytes == rowBytes)
        {
            var source = pixels.GetPixelSpan();
            if (!convertAlpha || PixelBuffer.HasOnlyOpaqueAlpha(source))
            {
                storage = null;
                Span = source;
                return;
            }
        }

        int length = rowBytes * pixels.Height;
        storage = PixelStorage.Rent(length, exactSize: false);
        var destination = storage.Bytes.AsSpan(0, length);
        try
        {
            for (int y = 0; y < pixels.Height; y++)
                CopyRow(pixels, y, destination.Slice(y * rowBytes, rowBytes), convertAlpha);
        }
        catch
        {
            storage.Release();
            throw;
        }
        Span = destination;
    }

    internal static void CopyRow(PixelBuffer pixels, int y, Span<byte> target, bool unpremultiply)
    {
        var row = pixels.GetRowSpan(y);
        if (pixels.Format == PixelFormat.Alpha8)
        {
            target.Clear();
            for (int x = 0; x < pixels.Width; x++)
                target[x * 4 + 3] = row[x];
            return;
        }
        row = row[..target.Length];
        bool rgba = pixels.Format == PixelFormat.Rgba8888;
        if (unpremultiply && !PixelBuffer.HasOnlyOpaqueAlpha(row))
            WriteStraightRow(row, target, rgba ? 0 : 2);
        else if (rgba)
            row.CopyTo(target);
        else
            PixelConverter.ConvertRow(row, CodecPixelFormat.Bgra32, target, CodecPixelFormat.Rgba32, pixels.Width);
    }

    private static void WriteStraightRow(ReadOnlySpan<byte> source, Span<byte> destination, int redIndex)
    {
        for (int offset = 0; offset < source.Length; offset += 4)
        {
            byte alpha = source[offset + 3];
            byte red = source[offset + redIndex];
            byte green = source[offset + 1];
            byte blue = source[offset + 2 - redIndex];
            if (alpha != 255)
            {
                red = PixelBuffer.Unmultiply(red, alpha);
                green = PixelBuffer.Unmultiply(green, alpha);
                blue = PixelBuffer.Unmultiply(blue, alpha);
            }
            destination[offset] = red;
            destination[offset + 1] = green;
            destination[offset + 2] = blue;
            destination[offset + 3] = alpha;
        }
    }
    public void Dispose() => storage?.Release();
}
