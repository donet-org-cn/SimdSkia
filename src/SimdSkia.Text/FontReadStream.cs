namespace SimdSkia.Text;

// Read-only, seekable stream retains its source even after the typeface is
// disposed. No unmanaged memory is exposed and no extra whole-font copy needed.
internal sealed class FontReadStream(FontSource source):Stream
{
    private FontSource? source=source;
    private long position;
    private FontSource Source=>source??throw new ObjectDisposedException(nameof(FontReadStream));
    public override bool CanRead=>source is not null;
    public override bool CanSeek=>source is not null;
    public override bool CanWrite=>false;
    public override long Length=>Source.Length;
    public override long Position {get{_ = Source;return position;}set{_ = Source;ArgumentOutOfRangeException.ThrowIfNegative(value);position=value;}}
    public override int Read(byte[] buffer,int offset,int count)
    {ArgumentNullException.ThrowIfNull(buffer);return Read(buffer.AsSpan(offset,count));}
    public override int Read(Span<byte> buffer)
    {
        var file=Source;int count=(int)Math.Min(buffer.Length,Math.Max(0,file.Length-position));
        if(count==0)return 0;
        file.CopyTo((int)position,buffer[..count]);position+=count;return count;
    }
    public override long Seek(long offset,SeekOrigin origin)
    {
        _ = Source;
        long result=checked(origin switch{SeekOrigin.Begin=>offset,SeekOrigin.Current=>position+offset,SeekOrigin.End=>Length+offset,_=>throw new ArgumentException("Unknown seek origin.",nameof(origin))});
        if(result<0)throw new IOException("Cannot seek before beginning.");return Position=result;
    }
    public override void Flush(){_ = Source;}
    public override void SetLength(long value)=>throw new NotSupportedException();
    public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    protected override void Dispose(bool disposing){source=null;base.Dispose(disposing);}
}
