namespace SkiaSharp;
public readonly struct SKRawRunBuffer<T>
{
    private readonly SKRunBuffer? run;
    internal SKRawRunBuffer(SKRunBuffer run)=>this.run=run;
    internal SKRunBuffer Run=>run??throw new InvalidOperationException("The default run has no allocation.");
    public Span<ushort> Glyphs=>run is null?default:run.Glyphs;
    public Span<T> Positions {get{if(run is null)return default;run.Validate();return (T[])run.PositionArray;}}
    public Span<byte> Text {get{if(run is null)return default;run.Validate();return run.TextArray;}}
    public Span<uint> Clusters
    {
        get
        {
            if(run is null)return default;run.Validate();
            if(!run.HasText)throw new NotSupportedException("This non-text run has no cluster allocation; native null-pointer spans cannot be safely exposed.");
            return run.ClusterArray;
        }
    }
}
