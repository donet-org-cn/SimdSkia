namespace SkiaSharp;

public class SKData : SKObject
{
    private byte[] bytes;
    internal SKData(byte[] bytes)=>this.bytes=bytes;
    public long Size {get{ThrowIfDisposed();return bytes.LongLength;}}
    public byte[] ToArray(){ThrowIfDisposed();return (byte[])bytes.Clone();}
    public ReadOnlySpan<byte> AsSpan(){ThrowIfDisposed();return bytes;}
    public void SaveTo(Stream target){ThrowIfDisposed();ArgumentNullException.ThrowIfNull(target);target.Write(bytes);}
    protected override void Dispose(bool disposing)=>bytes=[];
}
public enum SKEncodedImageFormat { Bmp=0,Gif=1,Ico=2,Jpeg=3,Png=4,Wbmp=5,Webp=6,Pkm=7,Ktx=8,Astc=9,Dng=10,Heif=11,Avif=12,Jpegxl=13 }
