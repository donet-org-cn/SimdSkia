namespace SkiaSharp;

/// <summary>Managed lifecycle. Native Skia handles are deliberately unsupported.</summary>
public abstract class SKNativeObject : IDisposable
{
    private bool disposed;
    protected SKNativeObject() { }
    public virtual IntPtr Handle
    {
        get { ThrowIfDisposed(); throw new NotSupportedException("SimdSkia objects do not have native Skia handles."); }
        protected set => throw new NotSupportedException("Native Skia handles cannot be installed in SimdSkia.");
    }
    protected bool IsDisposed => disposed;
    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    public void Dispose()
    {
        if (disposed) return;
        Dispose(true);
        disposed=true;
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) { }
}

public abstract class SKObject : SKNativeObject
{
    protected SKObject() { }
}
