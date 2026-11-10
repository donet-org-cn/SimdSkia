using System.Runtime.CompilerServices;
namespace SimdSkia.Text;

// Tracks actual font outline cache owners, not individual glyphs masquerading as
// native Skia strikes. Only misses update the registry; hot glyph hits keep their
// existing per-font fast path. All registry calls occur outside per-font locks.
internal static class FontCacheRegistry
{
    private sealed class Owner(TrueTypeFont font){internal readonly WeakReference<TrueTypeFont> Font=new(font);internal long Bytes,Used;internal bool Listed;}
    private static readonly object gate=new();
    private static readonly ConditionalWeakTable<TrueTypeFont,Owner> identities=new();
    private static readonly List<Owner> owners=[];
    private static long sequence;
    internal static long ByteLimit=128*1024;
    internal static int CountLimit=2048;
    internal static void Notify(TrueTypeFont font)
    {
        var usage=font.RawCacheUsage;var canonical=font.CanonicalCacheUsage;
        lock(gate)
        {
            var owner=identities.GetValue(font,static f=>new(f));if(!owner.Listed){owners.Add(owner);owner.Listed=true;}
            owner.Bytes=usage.Bytes+canonical.Bytes;owner.Used=++sequence;Trim();
        }
    }
    private static void Trim()
    {
        owners.RemoveAll(o=>!o.Font.TryGetTarget(out _));
        long bytes=owners.Sum(o=>o.Bytes);int count=owners.Count(o=>o.Bytes>0);
        while(bytes>Volatile.Read(ref ByteLimit)||count>Volatile.Read(ref CountLimit))
        {
            Owner? victim=null;foreach(var owner in owners)if(owner.Bytes>0&&(victim is null||owner.Used<victim.Used))victim=owner;
            if(victim is null)break;
            if(victim.Font.TryGetTarget(out var font))
            {
                if(count>Volatile.Read(ref CountLimit)){font.PurgeRawGlyphCache();font.PurgeCanonicalGlyphCache();}
                else font.TrimCachedGeometry(bytes-Volatile.Read(ref ByteLimit));
                long next=font.RawCacheUsage.Bytes+font.CanonicalCacheUsage.Bytes;bytes-=victim.Bytes-next;victim.Bytes=next;if(next==0)count--;
            }
            else{bytes-=victim.Bytes;victim.Bytes=0;count--;}
        }
    }
    internal static (int Count,long Bytes) Statistics
    {get{lock(gate){long bytes=0;int count=0;foreach(var owner in owners)if(owner.Font.TryGetTarget(out var font)){owner.Bytes=font.RawCacheUsage.Bytes+font.CanonicalCacheUsage.Bytes;if(owner.Bytes>0){count++;bytes+=owner.Bytes;}}return(count,bytes);}}}
    internal static void Configure(long bytes,int count){lock(gate){Volatile.Write(ref ByteLimit,Math.Max(0,bytes));Volatile.Write(ref CountLimit,Math.Max(0,count));Trim();}}
    internal static void Purge()
    {lock(gate){foreach(var owner in owners){if(owner.Font.TryGetTarget(out var font)){font.PurgeRawGlyphCache();font.PurgeCanonicalGlyphCache();}owner.Bytes=0;}}}
}
