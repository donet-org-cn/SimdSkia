using System.Runtime.CompilerServices;

namespace SimdSkia;

internal readonly struct PathCacheKey : IEquatable<PathCacheKey>
{
    internal const int ContentLimit = 256;
    internal PathGeometry Geometry { get; }
    private readonly int hash;

    internal PathCacheKey(PathGeometry geometry)
    {
        Geometry = geometry;
        if (geometry.SegmentCount > ContentLimit)
        {
            hash = RuntimeHelpers.GetHashCode(geometry);
            return;
        }

        var content = new HashCode();
        content.Add(geometry.FillRule);
        foreach (var segment in geometry.Segments)
            content.Add(segment);
        hash = content.ToHashCode();
    }

    public bool Equals(PathCacheKey other) => hash == other.hash &&
        (ReferenceEquals(Geometry, other.Geometry) ||
         (Geometry is not null && other.Geometry is not null &&
          Geometry.SegmentCount <= ContentLimit && Geometry.SegmentCount == other.Geometry.SegmentCount && Geometry.FillRule == other.Geometry.FillRule &&
          Geometry.Segments.SequenceEqual(other.Geometry.Segments)));

    public override bool Equals(object? obj) => obj is PathCacheKey other && Equals(other);
    public override int GetHashCode() => hash;
}
