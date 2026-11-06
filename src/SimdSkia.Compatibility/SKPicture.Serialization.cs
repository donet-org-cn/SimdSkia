namespace SkiaSharp;
public partial class SKPicture
{
    public SKData Serialize()=>new(SkpCodec.Write(Data));
    public void Serialize(Stream stream){ArgumentNullException.ThrowIfNull(stream);var bytes=SkpCodec.Write(Data);stream.Write(bytes);}
    public void Serialize(SKWStream stream){ArgumentNullException.ThrowIfNull(stream);stream.ThrowIfDisposed();var bytes=SkpCodec.Write(Data);if(!stream.Write(bytes,bytes.Length))throw new IOException("Failed to write the SKP document.");}
    public static SKPicture Deserialize(ReadOnlySpan<byte> data)
    {if(data.IsEmpty)return null!;if(data.Length>SkpCodec.MaximumBytes)throw new NotSupportedException("SKP input exceeds the 128 MiB document budget.");using var stream=new MemoryStream(data.ToArray(),false);return Deserialize(stream);}
    public static SKPicture Deserialize(SKData data){ArgumentNullException.ThrowIfNull(data);return Deserialize(data.AsSpan());}
    public static unsafe SKPicture Deserialize(IntPtr data,int length)
    {if(data==IntPtr.Zero)throw new ArgumentNullException(nameof(data));if(length<0)throw new ArgumentOutOfRangeException(nameof(length));return Deserialize(new ReadOnlySpan<byte>((void*)data,length));}
    public static SKPicture Deserialize(Stream stream)
    {ArgumentNullException.ThrowIfNull(stream);try{return SkpCodec.Read(stream);}catch(InvalidDataException){return null!;}catch(EndOfStreamException){return null!;}catch(OverflowException){return null!;}}
    public static SKPicture Deserialize(SKStream stream){ArgumentNullException.ThrowIfNull(stream);stream.ThrowIfDisposed();using var input=new SkpStream(stream);return Deserialize(input);}
    private sealed class SkpStream(SKStream source):Stream
    {
        public override bool CanRead=>true;public override bool CanWrite=>false;public override bool CanSeek=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(Span<byte> buffer)=>source.ReadSpan(buffer);public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
