using System.Buffers;

namespace SimdSkia;

/// <summary>Temporary device-space points and contour boundaries for rasterization.</summary>
internal sealed class FlattenedPath : IDisposable
{
    private static readonly ArrayPool<PointF> PointPool = ArrayPool<PointF>.Create(131_072, 2);
    private static readonly ArrayPool<int> EndPool = ArrayPool<int>.Create(8_192, 2);
    private PointF[] points = [];
    private int[] ends = [];
    private int count;
    internal int ContourCount { get; private set; }
    private int ContourStart => ContourCount == 0 ? 0 : ends[ContourCount-1];
    internal int CurrentCount => count-ContourStart;
    internal PointF Last => points[count-1];

    internal void Add(PointF point)
    {
        if (count == points.Length) Grow(ref points, count, PointPool);
        points[count++] = point;
    }

    internal void FinishContour()
    {
        if (CurrentCount == 0) return;
        if (ContourCount == ends.Length) Grow(ref ends, ContourCount, EndPool);
        ends[ContourCount++] = count;
    }

    internal ReadOnlySpan<PointF> Contour(int index)
    {
        int start = index == 0 ? 0 : ends[index-1];
        return points.AsSpan(start, ends[index]-start);
    }

    private static void Grow<T>(ref T[] buffer, int used, ArrayPool<T> pool)
    {
        var next = pool.Rent(Math.Max(16, checked(used*2)));
        buffer.AsSpan(0, used).CopyTo(next);
        if (buffer.Length != 0) pool.Return(buffer);
        buffer = next;
    }

    public void Dispose()
    {
        if (points.Length != 0) PointPool.Return(points);
        if (ends.Length != 0) EndPool.Return(ends);
        points = [];
        ends = [];
        count = ContourCount = 0;
    }
}
