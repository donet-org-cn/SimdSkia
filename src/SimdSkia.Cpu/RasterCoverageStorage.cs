using System.Buffers;

namespace SimdSkia.Cpu;

// Only canvas-local masks enter these pools. A draw and the canvas cache
// each hold a reference, so a nested draw can evict a mask still in use.
internal sealed class RasterCoverageStorage : IDisposable
{
    private const int LargeBytes = 80 * 1024;
    private const int MaximumCoverage = 2 * 1024 * 1024;
    private const int MaximumLargeCoverage = 8 * 1024 * 1024;
    private const int MaximumRuns = 64 * 1024;
    private static readonly ArrayPool<byte> CoveragePool = ArrayPool<byte>.Create(MaximumCoverage, 4);
    private static readonly ArrayPool<CoverageRun> RunPool = ArrayPool<CoverageRun>.Create(MaximumRuns, 2);
    // A 4K A8 mask can exceed the regular pool. Reuse one between sequential
    // frames, while allowing a full GC to reclaim it after a rare large draw.
    private static readonly WeakReference<byte[]> largeCoverage = new(null!);
    private int references = 1;
    private byte[]? coverage;
    private CoverageRun[]? runs;

    internal bool HasCoverage => coverage is not null;
    internal bool HasBuffers => coverage is not null || runs is not null;
    internal byte[]? RentCoverage(int length)
    {
        if(length is <LargeBytes or >MaximumLargeCoverage)return null;
        if(length<=MaximumCoverage)return coverage=CoveragePool.Rent(length);
        lock(largeCoverage)
        {
            if(largeCoverage.TryGetTarget(out var retained)&&retained.Length>=length)
            {
                largeCoverage.SetTarget(null!);
                return coverage=retained;
            }
        }
        return coverage=GC.AllocateUninitializedArray<byte>(length);
    }
    internal CoverageRun[]? RentRuns(int count) => count is >=LargeBytes/16 and <=MaximumRuns
        ? runs=RunPool.Rent(count) : null;

    internal void ReturnCoverage()
    {
        if (coverage is null) return;
        if(coverage.Length<=MaximumCoverage)CoveragePool.Return(coverage);
        else lock(largeCoverage)
        {
            if(!largeCoverage.TryGetTarget(out var retained)||retained.Length<coverage.Length)
                largeCoverage.SetTarget(coverage);
        }
        coverage=null;
    }

    internal void Retain()=>references++;

    public void Dispose()
    {
        if (--references!=0) return;
        ReturnCoverage();
        if (runs is not null) RunPool.Return(runs);
        runs=null;
    }
}
