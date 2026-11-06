namespace SkiaSharp;

// Shared by shader wrappers, composed shaders and retained paints. No per-draw
// reference-count traffic; callbacks run when the last explicit owner releases.
internal sealed class ShaderResources : IDisposable
{
    private Shared? shared;
    internal ShaderResources(params IDisposable[] owned)=>shared=new(owned);
    private ShaderResources(Shared shared){this.shared=shared;shared.Retain();}
    internal ShaderResources Retain()=>new(shared??throw new ObjectDisposedException(nameof(ShaderResources)));
    public void Dispose(){Interlocked.Exchange(ref shared,null)?.Release();GC.SuppressFinalize(this);}
    ~ShaderResources(){try{Interlocked.Exchange(ref shared,null)?.Release();}catch{}}
    private sealed class Shared(IDisposable[] owned)
    {
        private int references=1;
        internal void Retain()=>Interlocked.Increment(ref references);
        internal void Release()
        {
            if(Interlocked.Decrement(ref references)!=0)return;
            Exception? failure=null;
            foreach(var item in owned)try{item.Dispose();}catch(Exception e){failure??=e;}
            if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
