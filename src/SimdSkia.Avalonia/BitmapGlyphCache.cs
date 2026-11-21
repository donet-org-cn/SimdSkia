using SimdSkia.Imaging;
using SimdSkia.Text;
using System.Runtime.CompilerServices;

namespace SimdSkia.Avalonia;

internal static class BitmapGlyphCache
{
    private const int MaxBytes = 8 * 1024 * 1024;
    private const int MaxEntries = 128;
    private static readonly ConditionalWeakTable<TrueTypeFont, object> identities = new();
    internal readonly record struct Key(object Font, int Offset, int Length);
    internal sealed class Entry(Key key, PixelBuffer image)
    {
        internal readonly Key Key = key;
        internal readonly PixelBuffer Image = image;
        private int references = 1;
        internal void Retain() => Interlocked.Increment(ref references);
        internal void Release() { if (Interlocked.Decrement(ref references) == 0) Image.Dispose(); }
    }

    internal readonly struct Lease : IDisposable
    {
        private readonly Entry entry;
        internal Lease(Entry entry) { this.entry = entry; entry.Retain(); }
        internal PixelBuffer Image => entry.Image;
        public void Dispose() => entry.Release();
    }
    private static readonly LinkedList<Entry> order = new();
    private static readonly Dictionary<Key, LinkedListNode<Entry>> cache = new();
    private static int bytes;

    internal static Lease Acquire(TrueTypeFont font, in GlyphBitmap bitmap)
    {
        var key = new Key(identities.GetValue(font, static _ => new object()), bitmap.Offset, bitmap.Length);
        lock (cache)
        {
            if (cache.TryGetValue(key, out var hit)) return Use(hit);
        }
        var image = Decode(font, bitmap);
        lock (cache)
        {
            if (cache.TryGetValue(key, out var hit))
            {
                image.Dispose();
                return Use(hit);
            }
            while (cache.Count == MaxEntries || bytes + image.StorageCapacity > MaxBytes)
            {
                var oldest = order.First!;
                order.RemoveFirst();
                cache.Remove(oldest.Value.Key);
                bytes -= oldest.Value.Image.StorageCapacity;
                oldest.Value.Release();
            }
            var entry = order.AddLast(new Entry(key, image));
            cache.Add(key, entry);
            bytes += image.StorageCapacity;
            return new(entry.Value);
        }
    }

    private static Lease Use(LinkedListNode<Entry> entry)
    {
        order.Remove(entry);
        order.AddLast(entry);
        return new(entry.Value);
    }

    private static PixelBuffer Decode(TrueTypeFont font, in GlyphBitmap bitmap)
    {
        var data = font.ReadBitmapGlyph(bitmap).Span;
        if (bitmap.Format == GlyphBitmapFormat.Png) return PngCodec.Decode(data);
        if (bitmap.Format == GlyphBitmapFormat.ApplePng) return PngCodec.DecodeAppleGlyph(data, bitmap.Width, bitmap.Height);
        var image = PixelBuffer.RentForOverwrite(bitmap.Width, bitmap.Height, PixelFormat.Bgra8888);
        data.CopyTo(image.GetPixelSpan());
        return image;
    }
}
