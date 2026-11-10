using SimdSkia.Cpu;
using SimdSkia.Text;
namespace SkiaSharp;

/// <summary>Controls real shared managed caches. Byte counts estimate retained
/// payload, not process working set; font counts are outline cache owners,
/// not native Skia strike counts. Per-canvas caches and GPU caches are separate.</summary>
public static class SKGraphics
{
    private static readonly object gate=new();
    private static long fontLimit=2*1024*1024,resourceLimit=32*1024*1024,singleLimit;
    private static int fontCountLimit=2048;
    public static void Init(){_=GlyphCoverageCache.Statistics;_=TextCoverageCache.Statistics;_=ClipCoverageCache.Statistics;_=FontCacheRegistry.Statistics;}
    public static long GetFontCacheUsed(){var geometry=FontCacheRegistry.Statistics;var glyphs=GlyphCoverageCache.Statistics;return geometry.Bytes+glyphs.GlyphBytes+glyphs.RunBytes+TextCoverageCache.Statistics.Bytes;}
    public static long GetFontCacheLimit()=>Volatile.Read(ref fontLimit);
    public static int GetFontCacheCountUsed()=>FontCacheRegistry.Statistics.Count;
    public static int GetFontCacheCountLimit()=>Volatile.Read(ref fontCountLimit);
    private static void ConfigureFontCaches()
    {long geometry=fontLimit/16,glyphs=fontLimit/16,text=fontLimit/8,runs=fontLimit-geometry-glyphs-text;FontCacheRegistry.Configure(geometry,fontCountLimit);GlyphCoverageCache.Configure(glyphs,runs,fontCountLimit);TextCoverageCache.Configure(text,fontCountLimit);}
    public static long SetFontCacheLimit(long bytes){if(bytes<0)throw new ArgumentOutOfRangeException(nameof(bytes));lock(gate){long previous=fontLimit;fontLimit=bytes;ConfigureFontCaches();return previous;}}
    public static int SetFontCacheCountLimit(int count){if(count<0)throw new ArgumentOutOfRangeException(nameof(count));lock(gate){int previous=fontCountLimit;fontCountLimit=count;ConfigureFontCaches();return previous;}}
    public static void PurgeFontCache(){FontCacheRegistry.Purge();GlyphCoverageCache.Clear();TextCoverageCache.Clear();}
    public static void PurgeResourceCache()=>ClipCoverageCache.Clear();
    public static void PurgeAllCaches(){PurgeFontCache();PurgeResourceCache();}
    public static long GetResourceCacheTotalBytesUsed()=>ClipCoverageCache.Statistics.Bytes;
    public static long GetResourceCacheTotalByteLimit()=>Volatile.Read(ref resourceLimit);
    public static long SetResourceCacheTotalByteLimit(long bytes){if(bytes<0)throw new ArgumentOutOfRangeException(nameof(bytes));lock(gate){long previous=resourceLimit;resourceLimit=bytes;ClipCoverageCache.Configure(bytes,singleLimit);return previous;}}
    public static long GetResourceCacheSingleAllocationByteLimit()=>Volatile.Read(ref singleLimit);
    public static long SetResourceCacheSingleAllocationByteLimit(long bytes){if(bytes<0)throw new ArgumentOutOfRangeException(nameof(bytes));lock(gate){long previous=singleLimit;singleLimit=bytes;ClipCoverageCache.Configure(resourceLimit,bytes);return previous;}}
    public static void DumpMemoryStatistics(SKTraceMemoryDump dump)
    {
        ArgumentNullException.ThrowIfNull(dump);dump.ThrowIfDisposed();var geometry=FontCacheRegistry.Statistics;var glyphs=GlyphCoverageCache.Statistics;var text=TextCoverageCache.Statistics;var resources=ClipCoverageCache.Statistics;
        dump.Numeric("SimdSkia/CPU/font/outlines","size","bytes",(ulong)geometry.Bytes);dump.Numeric("SimdSkia/CPU/font/glyph_masks","size","bytes",(ulong)glyphs.GlyphBytes);dump.Numeric("SimdSkia/CPU/font/runs","size","bytes",(ulong)glyphs.RunBytes);dump.Numeric("SimdSkia/CPU/font/text_masks","size","bytes",(ulong)text.Bytes);dump.Numeric("SimdSkia/CPU/resource/clip_masks","size","bytes",(ulong)resources.Bytes);
        dump.Text("SimdSkia/CPU","accounting","Retained managed cache payload; excludes source fonts, live geometry, canvas-owned caches, object headers and GPU resources.");
    }
}
