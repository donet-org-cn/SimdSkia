using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;

namespace SkiaSharp;

public abstract class SKStream:SKObject
{
    internal Stream source;
    private readonly bool owns;
    private bool end;
    internal SKStream(Stream source,bool owns){this.source=source;this.owns=owns;}
    public bool IsAtEnd {get{ThrowIfDisposed();return this is SKAbstractManagedStream m?m.OnIsAtEnd():source.CanSeek?source.Position>=source.Length:end;}}
    public bool HasPosition {get{ThrowIfDisposed();return this is SKAbstractManagedStream m?m.OnHasPosition():source.CanSeek;}}
    public bool HasLength {get{ThrowIfDisposed();return this is SKAbstractManagedStream m?m.OnHasLength():source.CanSeek;}}
    public int Position {get{ThrowIfDisposed();return this is SKAbstractManagedStream m?(int)m.OnGetPosition():source.CanSeek?checked((int)source.Position):0;}set=>Seek(value);}
    public int Length {get{ThrowIfDisposed();return this is SKAbstractManagedStream m?(int)m.OnGetLength():source.CanSeek?checked((int)source.Length):0;}}
    internal unsafe int ReadSpan(Span<byte> buffer)
    {
        ThrowIfDisposed();if(this is SKAbstractManagedStream managed){fixed(byte* p=buffer){int n=checked((int)managed.OnRead((IntPtr)p,(IntPtr)buffer.Length));if((uint)n>(uint)buffer.Length)throw new IOException("Invalid managed stream read count.");return n;}}
        int read=source.Read(buffer);if(read==0&&!buffer.IsEmpty)end=true;return read;
    }
    public int Read(byte[] buffer,int size){ArgumentNullException.ThrowIfNull(buffer);if((uint)size>(uint)buffer.Length)throw new ArgumentOutOfRangeException(nameof(size));return ReadSpan(buffer.AsSpan(0,size));}
    public unsafe int Read(IntPtr buffer,int size)
    {
        ThrowIfDisposed();if(size<0)throw new ArgumentOutOfRangeException(nameof(size));if(this is SKAbstractManagedStream m)return checked((int)m.OnRead(buffer,(IntPtr)size));if(buffer==IntPtr.Zero)return Skip(size);
        return ReadSpan(new Span<byte>((void*)buffer,size));
    }
    public unsafe int Peek(IntPtr buffer,int size)
    {
        ThrowIfDisposed();if(size<0)throw new ArgumentOutOfRangeException(nameof(size));if(this is SKAbstractManagedStream m)return checked((int)m.OnPeek(buffer,(IntPtr)size));if(!source.CanSeek)return 0;
        long old=source.Position;try{return Read(buffer,size);}finally{source.Position=old;end=false;}
    }
    public int Skip(int size)
    {
        ThrowIfDisposed();if(size<0)throw new ArgumentOutOfRangeException(nameof(size));if(this is SKAbstractManagedStream m)return checked((int)m.OnRead(IntPtr.Zero,(IntPtr)size));
        if(source.CanSeek){long n=Math.Min(size,Math.Max(0,source.Length-source.Position));source.Position+=n;return (int)n;}
        Span<byte> scratch=stackalloc byte[1024];int total=0;while(total<size){int n=ReadSpan(scratch[..Math.Min(size-total,scratch.Length)]);total+=n;if(n==0)break;}return total;
    }
    public bool Rewind(){ThrowIfDisposed();return this is SKAbstractManagedStream m?m.OnRewind():Seek(0);}
    public bool Seek(int position)
    {
        ThrowIfDisposed();if(this is SKAbstractManagedStream m)return m.OnSeek((IntPtr)position);if(!source.CanSeek)return false;source.Position=source is SKData.DataViewStream?(position<0?source.Length:Math.Min(position,source.Length)):position;end=false;return true;
    }
    public bool Move(int offset){ThrowIfDisposed();if(this is SKAbstractManagedStream m)return m.OnMove(offset);if(!source.CanSeek)return false;long next=checked(source.Position+offset);source.Position=source is SKData.DataViewStream?(next<0?source.Length:Math.Min(next,source.Length)):next;end=false;return true;}
    public bool Move(long offset)=>Move(unchecked((int)offset));
    public IntPtr GetMemoryBase(){ThrowIfDisposed();return source is SKData.DataViewStream view?view.Address:IntPtr.Zero;}
    public byte ReadByte()=>ReadByte(out var value)?value:default;
    public bool ReadByte(out byte buffer){Span<byte> value=stackalloc byte[1];bool ok=ReadSpan(value)==1;buffer=ok?value[0]:default;return ok;}
    public sbyte ReadSByte()=>ReadSByte(out var value)?value:default;
    public bool ReadSByte(out sbyte buffer){bool ok=ReadByte(out byte value);buffer=unchecked((sbyte)value);return ok;}
    public bool ReadBool()=>ReadBool(out var value)&&value;
    public bool ReadBool(out bool buffer){bool ok=ReadByte(out byte value);buffer=value>0;return ok;}
    public short ReadInt16()=>ReadInt16(out var value)?value:default;
    public bool ReadInt16(out short buffer){Span<byte> value=stackalloc byte[2];bool ok=ReadSpan(value)==2;buffer=ok?BinaryPrimitives.ReadInt16LittleEndian(value):default;return ok;}
    public ushort ReadUInt16()=>ReadUInt16(out var value)?value:default;
    public bool ReadUInt16(out ushort buffer){bool ok=ReadInt16(out short value);buffer=unchecked((ushort)value);return ok;}
    public int ReadInt32()=>ReadInt32(out var value)?value:default;
    public bool ReadInt32(out int buffer){Span<byte> value=stackalloc byte[4];bool ok=ReadSpan(value)==4;buffer=ok?BinaryPrimitives.ReadInt32LittleEndian(value):default;return ok;}
    public uint ReadUInt32()=>ReadUInt32(out var value)?value:default;
    public bool ReadUInt32(out uint buffer){bool ok=ReadInt32(out int value);buffer=unchecked((uint)value);return ok;}
    protected override void Dispose(bool disposing){if(owns)source.Dispose();}
}
public abstract class SKStreamRewindable:SKStream {internal SKStreamRewindable(Stream stream,bool owns):base(stream,owns) { }}
public abstract class SKStreamSeekable:SKStreamRewindable {internal SKStreamSeekable(Stream stream,bool owns):base(stream,owns) { }}
public abstract class SKStreamAsset:SKStreamSeekable {internal SKStreamAsset(Stream stream,bool owns):base(stream,owns) { }}
public abstract class SKStreamMemory:SKStreamAsset {internal SKStreamMemory(Stream stream,bool owns):base(stream,owns) { }}
public abstract class SKAbstractManagedStream:SKStreamAsset
{
    protected SKAbstractManagedStream():this(true) { }
    protected SKAbstractManagedStream(bool owns):base(Stream.Null,false) { }
    protected internal abstract IntPtr OnRead(IntPtr buffer,IntPtr size);
    protected internal abstract IntPtr OnPeek(IntPtr buffer,IntPtr size);
    protected internal abstract bool OnIsAtEnd();
    protected internal abstract bool OnHasPosition();
    protected internal abstract bool OnHasLength();
    protected internal abstract bool OnRewind();
    protected internal abstract IntPtr OnGetPosition();
    protected internal abstract IntPtr OnGetLength();
    protected internal abstract bool OnSeek(IntPtr position);
    protected internal abstract bool OnMove(int offset);
    protected internal abstract IntPtr OnCreateNew();
    protected internal virtual IntPtr OnDuplicate()=>OnCreateNew();
    protected internal virtual IntPtr OnFork()=>throw new NotSupportedException("Forking a native Skia stream handle is not supported.");
}
public class SKManagedStream:SKAbstractManagedStream
{
    public SKManagedStream(Stream managedStream):this(managedStream,false) { }
    private readonly Stream managedSource;
    private readonly bool disposeSource;
    private bool atEnd;
    public SKManagedStream(Stream managedStream,bool disposeManagedStream):base(){managedSource=managedStream??throw new ArgumentNullException(nameof(managedStream));disposeSource=disposeManagedStream;}
    protected internal override unsafe IntPtr OnRead(IntPtr buffer,IntPtr size)
    {
        int count=checked((int)size);if(count<0)throw new ArgumentOutOfRangeException(nameof(size));if(count==0)return IntPtr.Zero;int n;
        if(buffer==IntPtr.Zero){byte[] scratch=ArrayPool<byte>.Shared.Rent(count);try{n=managedSource.Read(scratch.AsSpan(0,count));}finally{ArrayPool<byte>.Shared.Return(scratch);}}
        else n=managedSource.Read(new Span<byte>((void*)buffer,count));
        if(!managedSource.CanSeek&&n<=count)atEnd=true;return (IntPtr)n;
    }
    protected internal override IntPtr OnPeek(IntPtr buffer,IntPtr size){if(!managedSource.CanSeek)return IntPtr.Zero;long position=managedSource.Position;try{return OnRead(buffer,size);}finally{managedSource.Position=position;}}
    protected internal override bool OnIsAtEnd()=>managedSource.CanSeek?managedSource.Position>=managedSource.Length:atEnd;
    protected internal override bool OnHasPosition()=>managedSource.CanSeek;
    protected internal override bool OnHasLength()=>managedSource.CanSeek;
    protected internal override bool OnRewind(){if(!managedSource.CanSeek)return false;managedSource.Position=0;return true;}
    protected internal override IntPtr OnGetPosition()=>managedSource.CanSeek?(IntPtr)managedSource.Position:IntPtr.Zero;
    protected internal override IntPtr OnGetLength()=>managedSource.CanSeek?(IntPtr)managedSource.Length:IntPtr.Zero;
    protected internal override bool OnSeek(IntPtr position){if(!managedSource.CanSeek)return false;managedSource.Position=(long)position;return true;}
    protected internal override bool OnMove(int offset){if(!managedSource.CanSeek)return false;managedSource.Position+=offset;return true;}
    protected internal override IntPtr OnCreateNew()=>IntPtr.Zero;
    protected override void Dispose(bool disposing){if(disposeSource)managedSource.Dispose();base.Dispose(disposing);}
    public int CopyTo(SKWStream destination)
    {
        ThrowIfDisposed();ArgumentNullException.ThrowIfNull(destination);byte[] bytes=ArrayPool<byte>.Shared.Rent(81920);int total=0;
        try{int n;while((n=ReadSpan(bytes))>0){if(!destination.WriteSpan(bytes.AsSpan(0,n)))throw new IOException("Destination rejected stream bytes.");total=checked(total+n);}destination.Flush();return total;}finally{ArrayPool<byte>.Shared.Return(bytes);}
    }
    public SKStreamAsset ToMemoryStream(){using var writer=new SKDynamicMemoryWStream();CopyTo(writer);return writer.DetachAsStream();}
}
public class SKMemoryStream:SKStreamMemory
{
    private static Stream FromData(SKData data){ArgumentNullException.ThrowIfNull(data);return data.AsStream();}
    private static Stream FromBytes(byte[] data){using var bytes=SKData.CreateCopy(data);return bytes.AsStream();}
    public SKMemoryStream():this([]) { }
    public SKMemoryStream(byte[] data):base(FromBytes(data),true) { }
    public SKMemoryStream(SKData data):base(FromData(data),true) { }
    public SKMemoryStream(ulong length):base(Create(length),true) { }
    private static Stream Create(ulong length){using var bytes=SKData.Create(length);return bytes.AsStream();}
    public void SetMemory(byte[] data){ThrowIfDisposed();var next=FromBytes(data);source.Dispose();source=next;}
}
public class SKFileStream:SKStreamAsset
{
    private readonly bool valid;
    private static Stream Open(string path)
    {ArgumentNullException.ThrowIfNull(path);try{return File.OpenRead(path);}catch(IOException){return Stream.Null;}catch(UnauthorizedAccessException){return Stream.Null;}}
    public SKFileStream(string path):base(Open(path),true){valid=!ReferenceEquals(source,Stream.Null);}
    public bool IsValid {get{ThrowIfDisposed();return valid;}}
    public static bool IsPathSupported(string path)=>true;
    public static SKStreamAsset OpenStream(string path){var stream=new SKFileStream(path);if(stream.IsValid)return stream;stream.Dispose();return null!;}
}

public abstract class SKWStream:SKObject
{
    internal Stream? target;
    private readonly bool owns;
    internal int written;
    internal SKWStream(Stream? target,bool owns){this.target=target;this.owns=owns;}
    public virtual int BytesWritten {get{ThrowIfDisposed();return this is SKAbstractManagedWStream m?checked((int)m.OnBytesWritten()):written;}}
    internal unsafe bool WriteSpan(ReadOnlySpan<byte> bytes)
    {ThrowIfDisposed();if(this is SKAbstractManagedWStream m){fixed(byte* p=bytes)return m.OnWrite((IntPtr)p,(IntPtr)bytes.Length);}if(target is null)return false;try{target.Write(bytes);written=checked(written+bytes.Length);return true;}catch(IOException){return false;}}
    public virtual bool Write(byte[] buffer,int size)
    {ArgumentNullException.ThrowIfNull(buffer);if((uint)size>(uint)buffer.Length)throw new ArgumentOutOfRangeException(nameof(size));return WriteSpan(buffer.AsSpan(0,size));}
    public virtual void Flush(){ThrowIfDisposed();if(this is SKAbstractManagedWStream m)m.OnFlush();else target?.Flush();}
    public bool NewLine()=>WriteSpan("\n"u8);
    public bool Write8(byte value){Span<byte> bytes=stackalloc byte[1];bytes[0]=value;return WriteSpan(bytes);}
    public bool Write16(ushort value){Span<byte> bytes=stackalloc byte[2];BinaryPrimitives.WriteUInt16LittleEndian(bytes,value);return WriteSpan(bytes);}
    public bool Write32(uint value){Span<byte> bytes=stackalloc byte[4];BinaryPrimitives.WriteUInt32LittleEndian(bytes,value);return WriteSpan(bytes);}
    public bool WriteBool(bool value)=>Write8(value?(byte)1:(byte)0);
    public bool WriteScalar(float value)=>Write32(BitConverter.SingleToUInt32Bits(value));
    public unsafe bool WriteText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);var pointer=Marshal.StringToCoTaskMemAnsi(value);
        try{byte* start=(byte*)pointer;int n=0;while(start[n]!=0)n++;return WriteSpan(new ReadOnlySpan<byte>(start,n));}finally{Marshal.FreeCoTaskMem(pointer);}
    }
    public bool WriteDecimalAsTest(int value)=>WriteText(value.ToString(CultureInfo.InvariantCulture));
    public bool WriteBigDecimalAsText(long value,int digits)=>WriteText(unchecked((ulong)value).ToString("D"+Math.Clamp(digits,0,99),CultureInfo.InvariantCulture));
    public bool WriteHexAsText(uint value,int digits)=>WriteText(value.ToString("X"+Math.Clamp(digits,0,8),CultureInfo.InvariantCulture));
    public bool WriteScalarAsText(float value)=>WriteText(value.ToString("G8",CultureInfo.InvariantCulture).Replace('E','e'));
    public static int GetSizeOfPackedUInt32(uint value)=>value<=253?1:value<=65535?3:5;
    public bool WritePackedUInt32(uint value)
    {
        Span<byte> bytes=stackalloc byte[5];int n=GetSizeOfPackedUInt32(value);bytes[0]=n==1?(byte)value:n==3?(byte)254:(byte)255;
        if(n==3)BinaryPrimitives.WriteUInt16LittleEndian(bytes[1..],(ushort)value);else if(n==5)BinaryPrimitives.WriteUInt32LittleEndian(bytes[1..],value);return WriteSpan(bytes[..n]);
    }
    public bool WriteStream(SKStream input,int length)
    {
        ArgumentNullException.ThrowIfNull(input);if(length<0)throw new ArgumentOutOfRangeException(nameof(length));byte[] buffer=ArrayPool<byte>.Shared.Rent(Math.Min(81920,Math.Max(1,length)));
        try{while(length>0){int n=input.ReadSpan(buffer.AsSpan(0,Math.Min(length,buffer.Length)));if(n==0||!WriteSpan(buffer.AsSpan(0,n)))return false;length-=n;}return true;}finally{ArrayPool<byte>.Shared.Return(buffer);}
    }
    protected override void Dispose(bool disposing){if(owns)target?.Dispose();target=null;}
}
public abstract class SKAbstractManagedWStream:SKWStream
{
    protected SKAbstractManagedWStream():this(true) { }
    protected SKAbstractManagedWStream(bool owns):base(null,false) { }
    protected internal abstract bool OnWrite(IntPtr buffer,IntPtr size);
    protected internal abstract void OnFlush();
    protected internal abstract IntPtr OnBytesWritten();
}
public class SKManagedWStream:SKAbstractManagedWStream
{
    public SKManagedWStream(Stream managedStream):this(managedStream,false) { }
    private readonly Stream managedSource;
    private readonly bool disposeSource;
    public SKManagedWStream(Stream managedStream,bool disposeManagedStream):base(){managedSource=managedStream??throw new ArgumentNullException(nameof(managedStream));disposeSource=disposeManagedStream;}
    protected internal override unsafe bool OnWrite(IntPtr buffer,IntPtr size){int count=checked((int)size);if(count<0)throw new ArgumentOutOfRangeException(nameof(size));managedSource.Write(new ReadOnlySpan<byte>((void*)buffer,count));return true;}
    protected internal override void OnFlush()=>managedSource.Flush();
    protected internal override IntPtr OnBytesWritten()=>(IntPtr)managedSource.Position;
    protected override void Dispose(bool disposing){if(disposeSource)managedSource.Dispose();base.Dispose(disposing);}
}
public class SKFileWStream:SKWStream
{
    private static Stream? Open(string path){ArgumentNullException.ThrowIfNull(path);try{return File.Create(path);}catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}}
    public SKFileWStream(string path):base(Open(path),true) { }
    public bool IsValid {get{ThrowIfDisposed();return target is not null;}}
    public static bool IsPathSupported(string path)=>true;
    public static SKWStream OpenStream(string path){var stream=new SKFileWStream(path);if(stream.IsValid)return stream;stream.Dispose();return null!;}
}
public class SKDynamicMemoryWStream:SKWStream
{
    public SKDynamicMemoryWStream():base(new MemoryStream(),true) { }
    private MemoryStream Buffer {get{ThrowIfDisposed();return (MemoryStream)target!;}}
    private ReadOnlySpan<byte> Content=>Buffer.GetBuffer().AsSpan(0,written);
    public SKData CopyToData()=>new(Content.ToArray());
    public SKData DetachAsData(){var previous=Buffer;var result=SKData.Owned(previous.GetBuffer(),0,written);target=new MemoryStream();written=0;previous.Dispose();return result;}
    public SKStreamAsset DetachAsStream(){using var data=DetachAsData();return new SKMemoryStream(data);}
    public unsafe void CopyTo(IntPtr data){if(data==IntPtr.Zero&&written>0)throw new ArgumentNullException(nameof(data));Content.CopyTo(new Span<byte>((void*)data,written));}
    public void CopyTo(Span<byte> data){if(data.Length<written)throw new Exception($"Not enough space to copy. Expected at least {written}, but received {data.Length}.");Content.CopyTo(data);}
    public bool CopyTo(SKWStream dst){ArgumentNullException.ThrowIfNull(dst);return dst.WriteSpan(Content);}
    public bool CopyTo(Stream dst){ArgumentNullException.ThrowIfNull(dst);ThrowIfDisposed();try{dst.Write(Content);return true;}catch(IOException){return false;}}
}
