namespace SimdSkia.Cpu;

internal static class ClipCoverageCache
{
    internal readonly record struct Key(PathGeometry Path,Matrix3 Matrix,int Width,int Height,bool Antialias,float Tolerance);
    private static readonly object gate=new();
    private static readonly Dictionary<Key,LinkedListNode<(Key Key,CoverageClip Clip,long Bytes)>> entries=[];
    private static readonly LinkedList<(Key Key,CoverageClip Clip,long Bytes)> order=[];
    private static long bytes;
    internal static bool TryGet(Key key,out CoverageClip clip)
    {lock(gate){if(entries.TryGetValue(key,out var node)){order.Remove(node);order.AddLast(node);clip=node.Value.Clip;return true;}clip=null!;return false;}}
    internal static CoverageClip Add(Key key,CoverageClip clip)
    {
        long length=(long)clip.Width*clip.Height+(long)key.Path.Segments.Length*32;
        if(length>8*1024*1024)return clip;
        lock(gate)
        {
            if(entries.TryGetValue(key,out var existing))return existing.Value.Clip;
            while(entries.Count>=64||bytes+length>8*1024*1024){var first=order.First!;bytes-=first.Value.Bytes;entries.Remove(first.Value.Key);order.RemoveFirst();}
            entries.Add(key,order.AddLast((key,clip,length)));bytes+=length;return clip;
        }
    }
}
