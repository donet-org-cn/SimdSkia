using System.Diagnostics;

namespace SimdSkia.Avalonia;

/// <summary>Opt-in timings for investigating real UI rendering. Disabled unless SIMD_SKIA_PROFILE=1.</summary>
public static class SimdSkiaDiagnostics
{
    private static readonly bool enabled=Environment.GetEnvironmentVariable("SIMD_SKIA_PROFILE")=="1";
    private static readonly object gate=new();
    private static readonly Dictionary<string,(long Calls,long Ticks)> totals=[];
    private static long frames;
    public static long FramesPresented=>Interlocked.Read(ref frames);
    internal static void Presented()=>Interlocked.Increment(ref frames);
    internal static Measurement Measure(string name)=>new(enabled?name:null);
    internal readonly struct Measurement(string? name):IDisposable
    {
        private readonly long start=name is null?0:Stopwatch.GetTimestamp();
        public void Dispose(){if(name is null)return;long elapsed=Stopwatch.GetTimestamp()-start;lock(gate){totals.TryGetValue(name,out var old);totals[name]=(old.Calls+1,old.Ticks+elapsed);}}
    }
    public static IReadOnlyDictionary<string,(long Calls,double Milliseconds)> GetDrawingTimings()
    {lock(gate)return totals.ToDictionary(p=>p.Key,p=>(p.Value.Calls,p.Value.Ticks*1000d/Stopwatch.Frequency));}
}
