using System.Runtime.InteropServices;

namespace SkiaSharp;

public delegate void SKDataReleaseDelegate(IntPtr address,object context);

/// <summary>Shared byte ownership. Subsets and streams retain storage without copying.</summary>
public class SKData : SKObject
{
    private DataStorage? storage;
    private readonly int offset,length;
    private static readonly SKData empty=new EmptyData();
    internal SKData(byte[] bytes):this(new DataStorage(bytes),0,bytes.Length,false) { }
    private SKData(DataStorage storage,int offset,int length,bool retain)
    {this.storage=storage;this.offset=offset;this.length=length;if(retain)storage.Retain();}
    internal static SKData Owned(byte[] bytes,int offset,int length)=>new(new DataStorage(bytes),offset,length,false);
    private sealed class EmptyData:SKData
    {internal EmptyData():base([]) { } protected override bool IgnoreDispose=>true;}
    public static SKData Empty=>empty;
    public bool IsEmpty=>Size==0;
    public long Size {get{ThrowIfDisposed();return length;}}
    public IntPtr Data {get{ThrowIfDisposed();return length==0?IntPtr.Zero:storage!.Address+offset;}}
    public Span<byte> Span {get{ThrowIfDisposed();return storage!.Bytes.Slice(offset,length);}}
    public ReadOnlySpan<byte> AsSpan()=>Span;
    public byte[] ToArray()=>Span.ToArray();
    public static SKData CreateCopy(byte[] bytes){ArgumentNullException.ThrowIfNull(bytes);return CreateCopy(bytes.AsSpan());}
    public static SKData CreateCopy(ReadOnlySpan<byte> bytes)=>new(bytes.ToArray());
    public static SKData CreateCopy(byte[] bytes,ulong length)
    {ArgumentNullException.ThrowIfNull(bytes);if(length>(ulong)bytes.Length)throw new ArgumentOutOfRangeException(nameof(length));return new(bytes.AsSpan(0,(int)length).ToArray());}
    public static SKData CreateCopy(IntPtr bytes,int length)=>CreateCopy(bytes,CheckedLength(length,nameof(length)));
    public static SKData CreateCopy(IntPtr bytes,long length)=>CreateCopy(bytes,CheckedLength(length,nameof(length)));
    public static unsafe SKData CreateCopy(IntPtr bytes,ulong length)
    {int count=CheckedLength(length,nameof(length));if(bytes==IntPtr.Zero&&count!=0)throw new ArgumentNullException(nameof(bytes));return new(new ReadOnlySpan<byte>((void*)bytes,count).ToArray());}
    public static SKData Create(int size)=>Create(CheckedLength(size,nameof(size)));
    public static SKData Create(long size)=>Create(CheckedLength(size,nameof(size)));
    public static SKData Create(ulong size)=>new(GC.AllocateUninitializedArray<byte>(CheckedLength(size,nameof(size))));
    public static SKData Create(IntPtr address,int length)=>Create(address,length,null!,null!);
    public static SKData Create(IntPtr address,int length,SKDataReleaseDelegate releaseProc)=>Create(address,length,releaseProc,null!);
    public static SKData Create(IntPtr address,int length,SKDataReleaseDelegate releaseProc,object context)
    {if(length<0)throw new ArgumentOutOfRangeException(nameof(length));if(address==IntPtr.Zero&&length!=0)throw new ArgumentNullException(nameof(address));return new(new DataStorage(address,length,releaseProc,context),0,length,false);}
    public static SKData Create(string filename){ArgumentNullException.ThrowIfNull(filename);try{return new(File.ReadAllBytes(filename));}catch(IOException){return null!;}catch(UnauthorizedAccessException){return null!;}}
    public static SKData Create(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);if(stream.CanSeek)return Create(stream,stream.Length-stream.Position);
        using var copy=new MemoryStream();stream.CopyTo(copy);return Owned(copy.GetBuffer(),0,checked((int)copy.Length));
    }
    public static SKData Create(Stream stream,int length)=>Create(stream,CheckedLength(length,nameof(length)));
    public static SKData Create(Stream stream,long length)=>Create(stream,CheckedLength(length,nameof(length)));
    public static SKData Create(Stream stream,ulong length)
    {
        ArgumentNullException.ThrowIfNull(stream);int count=CheckedLength(length,nameof(length));var bytes=GC.AllocateUninitializedArray<byte>(count);int read=0;
        while(read<count){int n=stream.Read(bytes.AsSpan(read));if(n==0)return null!;read+=n;}return new(bytes);
    }
    public static SKData Create(SKStream stream){ArgumentNullException.ThrowIfNull(stream);return Create(stream,stream.Length);}
    public static SKData Create(SKStream stream,int length)=>Create(stream,CheckedLength(length,nameof(length)));
    public static SKData Create(SKStream stream,long length)=>Create(stream,CheckedLength(length,nameof(length)));
    public static SKData Create(SKStream stream,ulong length)
    {ArgumentNullException.ThrowIfNull(stream);int count=CheckedLength(length,nameof(length));var bytes=GC.AllocateUninitializedArray<byte>(count);return stream.ReadSpan(bytes)==count?new(bytes):null!;}
    private static ulong CheckedLength(long value,string parameter){if(value<0||value>Array.MaxLength)throw new ArgumentOutOfRangeException(parameter);return (ulong)value;}
    private static int CheckedLength(ulong value,string parameter){if(value>(ulong)Array.MaxLength)throw new ArgumentOutOfRangeException(parameter);return (int)value;}
    public SKData Subset(ulong offset,ulong length)
    {ThrowIfDisposed();if(offset>(ulong)this.length)return null!;length=Math.Min(length,(ulong)this.length-offset);return new(storage!,checked(this.offset+(int)offset),(int)length,true);}
    public Stream AsStream()=>AsStream(false);
    public Stream AsStream(bool streamDisposesData){ThrowIfDisposed();return new DataViewStream(this,streamDisposesData);}
    public void SaveTo(Stream target){ThrowIfDisposed();ArgumentNullException.ThrowIfNull(target);target.Write(Span);}
    protected override void Dispose(bool disposing){var previous=storage;storage=null;previous?.Release();}

    private sealed class DataStorage
    {
        private byte[]? buffer;
        private readonly IntPtr external;
        private readonly int count;
        private readonly SKDataReleaseDelegate? release;
        private readonly object? context;
        private GCHandle pin;
        private int references=1;
        private int freed;
        internal DataStorage(byte[] buffer){this.buffer=buffer;count=buffer.Length;GC.SuppressFinalize(this);}
        internal DataStorage(IntPtr address,int length,SKDataReleaseDelegate? release,object? context){external=address;count=length;this.release=release;this.context=context;if(release is null)GC.SuppressFinalize(this);}
        internal unsafe Span<byte> Bytes=>buffer is not null?buffer:new Span<byte>((void*)external,count);
        internal IntPtr Address {get{if(buffer is null)return external;lock(this){if(!pin.IsAllocated){pin=GCHandle.Alloc(buffer,GCHandleType.Pinned);GC.ReRegisterForFinalize(this);}return pin.AddrOfPinnedObject();}}}
        internal void Retain()=>Interlocked.Increment(ref references);
        internal void Release()
        {if(Interlocked.Decrement(ref references)!=0)return;GC.SuppressFinalize(this);Free();}
        private void Free(){if(Interlocked.Exchange(ref freed,1)!=0)return;lock(this){if(pin.IsAllocated)pin.Free();buffer=null;}release?.Invoke(external,context!);}
        ~DataStorage(){try{Free();}catch{/* User callbacks cannot propagate from the finalizer thread. */}}
    }
    internal sealed class DataViewStream:Stream
    {
        private DataStorage? store;
        private readonly SKData? owner;
        private readonly int start,count;
        private long position;
        internal DataViewStream(SKData data,bool disposeOwner){store=data.storage!;store.Retain();start=data.offset;count=data.length;owner=disposeOwner?data:null;}
        internal IntPtr Address {get{Check();return count==0?IntPtr.Zero:store!.Address+start;}}
        private void Check()=>ObjectDisposedException.ThrowIf(store is null,this);
        public override bool CanRead=>store is not null;
        public override bool CanSeek=>store is not null;
        public override bool CanWrite=>store is not null;
        public override long Length {get{Check();return count;}}
        public override long Position {get{Check();return position;}set{Check();if(value<0)throw new ArgumentOutOfRangeException(nameof(value));position=value;}}
        public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override int Read(Span<byte> buffer){Check();int n=(int)Math.Min(buffer.Length,Math.Max(0,count-position));store!.Bytes.Slice(start+(int)Math.Min(position,count),n).CopyTo(buffer);position+=n;return n;}
        public override void Write(byte[] buffer,int offset,int count)=>Write(buffer.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> buffer){Check();if(position>count-buffer.Length)throw new NotSupportedException("A data stream cannot grow.");buffer.CopyTo(store!.Bytes.Slice(start+(int)position,buffer.Length));position+=buffer.Length;}
        public override long Seek(long offset,SeekOrigin origin){Check();long next=checked((origin switch{SeekOrigin.Begin=>0,SeekOrigin.Current=>position,SeekOrigin.End=>count,_=>throw new ArgumentOutOfRangeException(nameof(origin))})+offset);if(next<0)throw new IOException("Negative stream position.");return Position=next;}
        public override void SetLength(long value)=>throw new NotSupportedException("A data stream has fixed length.");
        public override void Flush()=>Check();
        protected override void Dispose(bool disposing){var data=store;store=null;data?.Release();owner?.Dispose();base.Dispose(disposing);}
    }
}
public enum SKEncodedImageFormat { Bmp=0,Gif=1,Ico=2,Jpeg=3,Png=4,Wbmp=5,Webp=6,Pkm=7,Ktx=8,Astc=9,Dng=10,Heif=11,Avif=12,Jpegxl=13 }
