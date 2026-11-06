namespace SimdSkia;

// A shared pixel view can outlive its original GPU image (for example inside a
// shader or a recorded picture). Keep the real resource and callback alive until
// the last such view is disposed. Independent pixel clones deliberately detach.
internal sealed class ResourceLease(IDisposable resource):IDisposable
{
    private int references=1;
    internal ResourceLease Retain(){Interlocked.Increment(ref references);return this;}
    public void Dispose(){if(Interlocked.Decrement(ref references)==0)resource.Dispose();}
}
