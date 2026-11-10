using SimdSkia;

namespace SkiaSharp;

// A display list of immutable engine commands, clip operations and layer scopes.
// No destination-sized pixel allocation is made while recording. SKP binary
// serialization is intentionally not exposed until its wire format is supported.
internal sealed class PictureCommands : IDisposable
{
    private static int nextId;
    internal readonly uint Id=unchecked((uint)Interlocked.Increment(ref nextId));
    internal enum Kind {Draw,Clear,Paint,Save,Restore,Clip,Layer,Annotation}
    internal sealed record Operation(Kind Kind,Matrix3 Matrix,DrawingCommand Command=default,PathGeometry? Path=null,SKClipOperation ClipOperation=default,bool Antialias=false,SKCanvasSaveLayerRec Layer=default,CanvasAnnotation? Annotation=null);
    internal readonly SKRect Cull;
    internal readonly List<Operation> Items=[];
    private readonly Dictionary<Paint,SKPaint> paints=[];
    private readonly List<IDisposable> owned=[];
    private readonly HashSet<PathGeometry> geometries=[];
    private long bytes=128;
    private int references=1;
    private bool sealedList;
    internal PictureCommands(SKRect cull)=>Cull=cull;
    internal int Bytes=>(int)Math.Min(int.MaxValue,bytes);
    internal int DrawingCount {get;private set;}
    internal PictureCommands Retain(){if(Volatile.Read(ref references)<=0)throw new ObjectDisposedException(nameof(SKPicture));Interlocked.Increment(ref references);return this;}
    internal void Add(Operation operation,SKPaint? paint=null)
    {
        if(sealedList)throw new ObjectDisposedException(nameof(SKPictureRecorder));
        if(Items.Count>=1_000_000)throw new NotSupportedException("Picture recording is limited to one million operations.");
        if(paint is not null&&!paints.ContainsKey(operation.Command.Paint)){var held=paint.Clone();paints.Add(operation.Command.Paint,held);owned.Add(held);bytes+=256;}
        if(operation.Command.Image is {} image)
        {
            // DrawBitmap snapshots writable storage at the call; SKImage is
            // already immutable. A retained lease owns external release hooks.
            var held=operation.Command.ImageIsImmutable?image.Share():image.Clone();owned.Add(held);bytes+=held.ByteCount;
            operation=operation with {Command=operation.Command with {Image=held,ImageIsImmutable=true}};
        }
        if(operation.Path is {} clipPath&&geometries.Add(clipPath))bytes+=(long)clipPath.Segments.Length*32;
        if(operation.Command.Path is {} path&&geometries.Add(path))bytes+=(long)path.Segments.Length*32;
        Items.Add(operation);bytes+=320;
        if(operation.Kind is Kind.Draw or Kind.Clear or Kind.Paint)DrawingCount++;
    }
    internal void Layer(Matrix3 matrix,SKCanvasSaveLayerRec rec)
    {
        SKPaint? paint=rec.Paint?.Clone();var backdrop=rec.Backdrop?.RetainForPicture();
        if(paint is not null)owned.Add(paint);if(backdrop is not null)owned.Add(backdrop);
        Add(new(Kind.Layer,matrix,Layer:rec with {Paint=paint,Backdrop=backdrop}));
    }
    internal void Seal()=>sealedList=true;
    internal void Own(IDisposable resource)=>owned.Add(resource);
    public void Dispose()
    {
        if(Interlocked.Decrement(ref references)!=0)return;
        Exception? failure=null;foreach(var item in owned)try{item.Dispose();}catch(Exception e){failure??=e;}
        owned.Clear();paints.Clear();geometries.Clear();Items.Clear();if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

public partial class SKPicture : SKObject
{
    private PictureCommands? commands;
    internal SKPicture(PictureCommands commands)=>this.commands=commands;
    private PictureCommands Data {get{ThrowIfDisposed();return commands!;}}
    internal SKPicture Share()=>new(Data.Retain());
    public uint UniqueId=>Data.Id;
    public SKRect CullRect=>Data.Cull;
    public int ApproximateBytesUsed=>Data.Bytes;
    public int ApproximateOperationCount=>GetApproximateOperationCount(false);
    public int GetApproximateOperationCount(bool includeNested)=>Data.Items.Count;
    public void Playback(SKCanvas canvas){ArgumentNullException.ThrowIfNull(canvas);canvas.Playback(Data);}
    public SKShader ToShader()=>ToShader(SKShaderTileMode.Clamp,SKShaderTileMode.Clamp);
    public SKShader ToShader(SKShaderTileMode tmx,SKShaderTileMode tmy)=>ToShader(tmx,tmy,SKFilterMode.Nearest);
    public SKShader ToShader(SKShaderTileMode tmx,SKShaderTileMode tmy,SKFilterMode filterMode)=>ToShader(tmx,tmy,filterMode,SKMatrix.Identity,CullRect);
    public SKShader ToShader(SKShaderTileMode tmx,SKShaderTileMode tmy,SKRect tile)=>ToShader(tmx,tmy,SKFilterMode.Nearest,tile);
    public SKShader ToShader(SKShaderTileMode tmx,SKShaderTileMode tmy,SKFilterMode filterMode,SKRect tile)=>ToShader(tmx,tmy,filterMode,SKMatrix.Identity,tile);
    public SKShader ToShader(SKShaderTileMode tmx,SKShaderTileMode tmy,SKMatrix localMatrix,SKRect tile)=>ToShader(tmx,tmy,SKFilterMode.Nearest,localMatrix,tile);
    public SKShader ToShader(SKShaderTileMode tmx,SKShaderTileMode tmy,SKFilterMode filterMode,SKMatrix localMatrix,SKRect tile)
    {
        _=Data;if(tile.IsEmpty||!tile.ToCore().IsFinite)return null!;if(!Enum.IsDefined(tmx)||!Enum.IsDefined(tmy)||!Enum.IsDefined(filterMode))throw new ArgumentOutOfRangeException(nameof(filterMode));
        int width=checked((int)MathF.Ceiling(tile.Width)),height=checked((int)MathF.Ceiling(tile.Height));
        if((long)width*height>16_777_216)throw new NotSupportedException("Picture shader raster tiles are limited to 16 million pixels.");
        using var bitmap=new SKBitmap(width,height);using(var canvas=new SKCanvas(bitmap)){canvas.Scale(width/tile.Width,height/tile.Height);canvas.Translate(-tile.Left,-tile.Top);Playback(canvas);}
        bitmap.SetImmutable();using var image=SKImage.FromBitmap(bitmap);
        var tileMatrix=localMatrix.ToCore()*new Matrix3(tile.Width/width,0,tile.Left,0,tile.Height/height,tile.Top,0,0,1);
        return image.ToShader(tmx,tmy,new SKSamplingOptions(filterMode),SKMatrix.FromCore(tileMatrix));
    }
    protected override void Dispose(bool disposing)=>Interlocked.Exchange(ref commands,null)?.Dispose();
}

public class SKPictureRecorder : SKObject
{
    private SKCanvas? canvas;
    private PictureCommands? recording;
    public SKPictureRecorder() { }
    public SKCanvas BeginRecording(SKRect cullRect)=>BeginRecording(cullRect,false);
    public SKCanvas BeginRecording(SKRect cullRect,bool useRTree)
    {
        ThrowIfDisposed();if(!cullRect.ToCore().IsFinite)throw new ArgumentException("Cull rectangle must be finite.",nameof(cullRect));
        if(useRTree)throw new NotSupportedException("An R-tree picture index has not been implemented; use linear recording.");
        Abort();recording=new(cullRect);return canvas=new SKCanvas(recording);
    }
    public SKCanvas RecordingCanvas {get{ThrowIfDisposed();return canvas!;}}
    public SKPicture EndRecording()
    {
        ThrowIfDisposed();if(recording is null)return null!;
        canvas!.RestoreToCount(1);var result=recording;canvas.Dispose();canvas=null;recording=null;result.Seal();return new(result);
    }
    public SKDrawable EndRecordingAsDrawable(){using var picture=EndRecording();return picture is null?null!:new PictureDrawable(picture.Share());}
    private void Abort(){try{canvas?.Dispose();}finally{canvas=null;recording?.Dispose();recording=null;}}
    protected override void Dispose(bool disposing)=>Abort();
}

public class SKDrawable : SKObject
{
    private static int nextGeneration;
    private uint generation=unchecked((uint)Interlocked.Increment(ref nextGeneration));
    protected SKDrawable() { }
    protected SKDrawable(bool owns) { }
    public uint GenerationId {get{ThrowIfDisposed();return generation;}}
    public SKRect Bounds {get{ThrowIfDisposed();return OnGetBounds();}}
    public int ApproximateBytesUsed {get{ThrowIfDisposed();return OnGetApproximateBytesUsed();}}
    public void Draw(SKCanvas canvas,in SKMatrix matrix){ThrowIfDisposed();ArgumentNullException.ThrowIfNull(canvas);int count=canvas.Save();try{canvas.Concat(in matrix);OnDraw(canvas);}finally{canvas.RestoreToCount(count);}}
    public void Draw(SKCanvas canvas,float x,float y){var m=SKMatrix.CreateTranslation(x,y);Draw(canvas,in m);}
    public SKPicture Snapshot(){ThrowIfDisposed();return OnSnapshot();}
    public void NotifyDrawingChanged(){ThrowIfDisposed();generation=unchecked((uint)Interlocked.Increment(ref nextGeneration));}
    protected internal virtual void OnDraw(SKCanvas canvas) { }
    protected internal virtual SKRect OnGetBounds()=>SKRect.Empty;
    protected internal virtual int OnGetApproximateBytesUsed()=>0;
    protected internal virtual SKPicture OnSnapshot(){using var recorder=new SKPictureRecorder();Draw(recorder.BeginRecording(Bounds),0,0);return recorder.EndRecording();}
}
internal sealed class PictureDrawable(SKPicture picture) : SKDrawable
{
    protected internal override void OnDraw(SKCanvas canvas)=>picture.Playback(canvas);
    protected internal override SKRect OnGetBounds()=>picture.CullRect;
    protected internal override int OnGetApproximateBytesUsed()=>picture.ApproximateBytesUsed;
    protected internal override SKPicture OnSnapshot()=>picture.Share();
    protected override void Dispose(bool disposing)=>picture.Dispose();
}
