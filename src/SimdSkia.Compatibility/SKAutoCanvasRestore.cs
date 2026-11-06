namespace SkiaSharp;

/// <summary>Restores the captured save count once, including when a scope throws.</summary>
public class SKAutoCanvasRestore : IDisposable
{
    private SKCanvas? canvas;
    private readonly int saveCount;
    public SKAutoCanvasRestore(SKCanvas canvas):this(canvas,true) { }
    public SKAutoCanvasRestore(SKCanvas canvas,bool doSave)
    {
        this.canvas=canvas;
        if(canvas is not null){saveCount=canvas.SaveCount;if(doSave)canvas.Save();}
    }
    public void Restore()
    {
        var target=canvas;canvas=null;
        if(target is null)return;
        // The native helper also permits the canvas to have been disposed first.
        try{target.RestoreToCount(saveCount);}catch(ObjectDisposedException) { }
    }
    public void Dispose()=>Restore();
}
