using Sdcb.SimdCodecs.Abstractions;
using Facade=Sdcb.SimdCodecs.SimdCodecs;

namespace SimdSkia.Imaging;

public readonly record struct ImageFrameDescription(int Width,int Height,TimeSpan? Duration);

/// <summary>Owned frame/page reader. Returned pixel buffers have independent lifetimes.</summary>
public sealed class ImageFrames : IDisposable
{
    private IImageFrameReader? reader;
    private byte[]? encoded;
    private readonly ImageFileFormat format;
    public int FrameCount {get{Check();return reader!.FrameCount;}}
    /// <summary>Total plays including the first; zero is infinite; null denotes pages/stills.</summary>
    public int? LoopCount {get{Check();return reader!.LoopCount;}}
    internal ImageFrames(ReadOnlySpan<byte> data)
    {
        CodecBackend.ReadInfo(data);format=ImageCodec.DetectFormat(data);encoded=data.ToArray();
        if(!Facade.TryOpenFrames(encoded,out reader))throw new InvalidDataException("Image frames could not be opened.");
        if(reader.FrameCount<=0||reader.FrameCount>100000){reader.Dispose();reader=null;throw new NotSupportedException("Image frame/page count exceeds the budget.");}
    }
    public ImageFrameDescription GetFrameInfo(int index)
    {
        Check();if(!reader!.TryGetFrameInfo(index,out var info))throw new InvalidDataException("Invalid frame metadata.");
        CodecBackend.ValidateLayout(info.Info);
        return new(info.Info.Width,info.Info.Height,info.Duration);
    }
    public PixelBuffer DecodeFrame(int index)
    {
        var dimensions=GetFrameInfo(index);
        if(format is ImageFileFormat.Png or ImageFileFormat.Jpeg)return ImageCodec.Decode(encoded!);
        if(!reader!.TryDecodeFrame(index,out var image))throw new InvalidDataException("Frame decode failed.");
        if(image.Width!=dimensions.Width||image.Height!=dimensions.Height)throw new InvalidDataException("Frame dimensions differ.");
        if(image.SampleFormat==SampleFormat.Float32||image.IccProfile.IsEmpty&&image.ColorEncoding!=ColorEncoding.AssumedSrgb)
            throw new NotSupportedException("The frame needs an explicit HDR/colour conversion policy.");
        var output=PixelBuffer.RentRgba(image.Width,image.Height);
        try
        {
            int sourceRow=checked(image.Width*image.Channels*(image.SampleFormat==SampleFormat.UInt16?2:1));
            if(image.Pixels.Length!=checked(sourceRow*image.Height))throw new InvalidDataException("Invalid frame pixel buffer.");
            for(int y=0;y<image.Height;y++)PixelConverter.ConvertRow(image.Pixels.Slice(y*sourceRow,sourceRow),image.Channels,image.SampleFormat,output.GetRowSpan(y),ImagePixelFormat.Rgba32,image.Width);
            if(!image.IccProfile.IsEmpty)
            {var profile=new IccProfile(image.IccProfile);if(profile.InputChannels==4)throw new NotSupportedException("Raw CMYK samples are required for this ICC profile.");profile.Transform(output);}
            return output;
        }
        catch{output.Dispose();throw;}
    }
    private void Check()=>ObjectDisposedException.ThrowIf(reader is null,this);
    public void Dispose(){reader?.Dispose();reader=null;encoded=null;}
}
