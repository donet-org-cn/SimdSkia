using System.Diagnostics;

namespace SimdSkia.Avalonia;

/// <summary>Opt-in timings for investigating real UI rendering. Disabled unless SIMD_SKIA_PROFILE=1.</summary>
public static class SimdSkiaDiagnostics
{
    private static readonly bool enabled=Environment.GetEnvironmentVariable("SIMD_SKIA_PROFILE")=="1";
    private static readonly object gate=new();
    private static readonly Dictionary<string,(long Calls,long Ticks)> totals=[];
    private static long frames;
    private static long softwareLayerCommands;
    private static PixelBuffer? capturedFrame;
    private static bool capture;
    private static bool timing;
    private static readonly List<double> frameTimes=[];
    public static void BeginFrameTiming(){lock(gate){frameTimes.Clear();timing=true;}}
    public static double[] EndFrameTiming(){lock(gate){timing=false;return frameTimes.ToArray();}}
    internal static long FrameStarted()=>Volatile.Read(ref timing)?Stopwatch.GetTimestamp():0;
    internal static void FrameCompleted(long start){if(start==0)return;double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;lock(gate){if(timing){if(frameTimes.Count>=1000000)throw new InvalidOperationException("Frame timing capture exceeds one million samples.");frameTimes.Add(ms);}}}
    /// <summary>Opt-in diagnostic copies. Disabled by default and excluded from performance measurements.</summary>
    public static bool CapturePresentedFrames
    {
        get {lock(gate)return capture;}
        set {lock(gate){capture=value;if(!value){capturedFrame?.Dispose();capturedFrame=null;}}}
    }
    public static PixelBuffer? GetLastPresentedFrame(){lock(gate)return capturedFrame?.Clone();}
    public static long FramesPresented=>Interlocked.Read(ref frames);
    public static long SoftwareLayerCommands=>Interlocked.Read(ref softwareLayerCommands);
    internal static void SoftwareLayerCommand()=>Interlocked.Increment(ref softwareLayerCommands);
    internal static void Presented(PixelBuffer pixels)
    {Interlocked.Increment(ref frames);if(!Volatile.Read(ref capture))return;lock(gate){if(capture){capturedFrame?.Dispose();capturedFrame=pixels.Clone();}}}
    internal static Measurement Measure(string name)=>new(enabled?name:null);
    internal readonly struct Measurement(string? name):IDisposable
    {
        private readonly long start=name is null?0:Stopwatch.GetTimestamp();
        public void Dispose(){if(name is null)return;long elapsed=Stopwatch.GetTimestamp()-start;lock(gate){totals.TryGetValue(name,out var old);totals[name]=(old.Calls+1,old.Ticks+elapsed);}}
    }
    public static IReadOnlyDictionary<string,(long Calls,double Milliseconds)> GetDrawingTimings()
    {lock(gate)return totals.ToDictionary(p=>p.Key,p=>(p.Value.Calls,p.Value.Ticks*1000d/Stopwatch.Frequency));}
}
