using System.Buffers;

namespace SimdSkia.Gpu;

/// <summary>
/// Conservative 8x8 edge bins, not coverage masks. Every closed contour that can contain
/// a tile contributes its edges crossing that tile's Y band, including edges to its left.
/// The shader still evaluates winding and all 16x16 AA samples on the GPU.
/// </summary>
internal sealed record TiledEdges(float[] Data, int Left, int Top, int Columns, int Rows)
{
    private readonly record struct Range(int Edge, int Left, int Top, int Right, int Bottom);
    private const int MaximumFloats = 4 * 1024 * 1024; // 16 MiB, including edges and bin headers.

    public static TiledEdges? Create(PointF[][] contours, float[] edges, int width, int height)
    {
        if (edges.Length < 24 * 4) return null;
        // Extreme coordinates retain the full scan rather than relying on a pixel halo
        // smaller than their float32 rounding error.
        foreach(float value in edges)if(MathF.Abs(value)>65536)return null;
        var ranges = ArrayPool<Range>.Shared.Rent(edges.Length/4);int rangeCount=0;
        try
        {
        int originX = (width + 7) / 8, originY = (height + 7) / 8, endX = 0, endY = 0, edge = 0;
        foreach (var contour in contours)
        {
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            foreach (var point in contour)
            {minX = MathF.Min(minX, point.X);minY = MathF.Min(minY, point.Y);maxX = MathF.Max(maxX, point.X);maxY = MathF.Max(maxY, point.Y);}
            int left = Tile(minX - 1, width), right = EndTile(maxX + 1, width);
            int top = Tile(minY - 1, height), bottom = EndTile(maxY + 1, height);
            for (int i = 0; i < contour.Length; i++)
            {
                var a = contour[i];var b = contour[(i + 1) % contour.Length];
                if (a == b) continue;
                int firstY = Math.Max(top, Tile(MathF.Min(a.Y, b.Y) - 1, height));
                int lastY = Math.Min(bottom, EndTile(MathF.Max(a.Y, b.Y) + 1, height));
                // A leftward winding ray cannot see edges entirely to its right.
                // Retain a pixel halo for float32 crossing/boundary tests.
                int firstX=Math.Max(left,Tile(MathF.Min(a.X,b.X)-1,width));
                if (firstX < right && firstY < lastY)
                {
                    ranges[rangeCount++]=new(edge, firstX, firstY, right, lastY);
                    originX = Math.Min(originX, firstX);endX = Math.Max(endX, right);
                    originY = Math.Min(originY, firstY);endY = Math.Max(endY, lastY);
                }
                edge++;
            }
        }
        int columns = Math.Max(0, endX - originX), rows = Math.Max(0, endY - originY);
        long headers = (long)columns * rows;
        if (headers == 0 || headers * 4 + edges.Length > MaximumFloats) return null;
        long references = 0;
        foreach (var range in ranges.AsSpan(0,rangeCount))
        {
            references += (long)(range.Right - range.Left) * (range.Bottom - range.Top);
            if (references + headers * 4 + edges.Length + 3 > MaximumFloats) return null; // Original edge scan remains correct.
        }
        var counts = ArrayPool<int>.Shared.Rent((int)headers);
        try
        {
        counts.AsSpan(0,(int)headers).Clear();
        foreach (var range in ranges.AsSpan(0,rangeCount))
            for (int y = range.Top; y < range.Bottom; y++)
                for (int x = range.Left; x < range.Right; x++)
                    if(!ConstantWinding(edges,range.Edge,x,y))counts[(y - originY) * columns + x - originX]++;
        // Store each edge once. Bin lists contain packed 32-bit edge indices, saving
        // four times the index payload versus duplicating vec4 endpoints per tile.
        int actualReferences=0;for(int i=0;i<(int)headers;i++)actualReferences=checked(actualReferences+counts[i]);
        var data = new float[checked(edges.Length + (int)headers * 4 + ((actualReferences + 3) & ~3))];
        edges.CopyTo(data, 0);
        int next = edges.Length + (int)headers * 4;
        for (int i = 0; i < (int)headers; i++)
        {
            data[edges.Length + i * 4] = next;data[edges.Length + i * 4 + 1] = counts[i];
            int count = counts[i];counts[i] = next;next += count;
        }
        foreach (var range in ranges.AsSpan(0,rangeCount))
            for (int y = range.Top; y < range.Bottom; y++)
                for (int x = range.Left; x < range.Right; x++)
                {
                    int bin=(y-originY)*columns+x-originX;
                    if(ConstantWinding(edges,range.Edge,x,y))data[edges.Length+bin*4+2]+=edges[range.Edge*4+3]>edges[range.Edge*4+1]?1:-1;
                    else{int offset = counts[bin]++;data[offset] = range.Edge;}
                }
        return new(data, originX, originY, columns, rows);
        }
        finally{ArrayPool<int>.Shared.Return(counts);}
        }
        finally{ArrayPool<Range>.Shared.Return(ranges);}
    }
    // Geometry binning only: these edges have the same crossing contribution for
    // every point in the entire tile. Coverage and AA still execute on the GPU.
    private static bool ConstantWinding(float[] edges,int index,int x,int y)
    {
        int at=index*4;
        return MathF.Max(edges[at],edges[at+2])<x*8-1&&
            MathF.Min(edges[at+1],edges[at+3])<=y*8-1&&MathF.Max(edges[at+1],edges[at+3])>=y*8+9;
    }
    private static int Tile(float value, int limit) => (int)Math.Clamp(MathF.Floor(value / 8), 0, (limit + 7) / 8);
    private static int EndTile(float value, int limit) => (int)Math.Clamp(MathF.Floor(value / 8) + 1, 0, (limit + 7) / 8);
}
