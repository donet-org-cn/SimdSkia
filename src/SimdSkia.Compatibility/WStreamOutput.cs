namespace SkiaSharp;
// Write-only adapter over all SKWStream implementations, including callbacks.
// Disposing the adapter does not dispose the caller's SKWStream.
internal sealed class WStreamOutput(SKWStream output) : Stream
{
    private bool closed;
    private long position;
    public override bool CanRead=>false;
    public override bool CanSeek=>false;
    public override bool CanWrite=>!closed;
    public override long Length=>throw new NotSupportedException("This is a write-only stream.");
    public override long Position {get=>position;set=>throw new NotSupportedException("This stream cannot seek.");}
    public override void Flush(){ObjectDisposedException.ThrowIf(closed,this);output.Flush();}
    public override void Write(byte[] buffer,int offset,int count)=>Write(buffer.AsSpan(offset,count));
    public override void Write(ReadOnlySpan<byte> buffer){ObjectDisposedException.ThrowIf(closed,this);if(!output.WriteSpan(buffer))throw new IOException("The SKWStream rejected document output.");position=checked(position+buffer.Length);}
    public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException("This is a write-only stream.");
    public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException("This stream cannot seek.");
    public override void SetLength(long value)=>throw new NotSupportedException("This stream cannot set its length.");
    protected override void Dispose(bool disposing){closed=true;base.Dispose(disposing);}
}
