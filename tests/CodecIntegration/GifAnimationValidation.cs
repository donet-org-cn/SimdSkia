using SimdSkia;
using SimdSkia.Imaging;
using SkiaSharp;

internal static class GifAnimationValidation
{
    public static void Run(string output, Action<bool, string> check)
    {
        foreach (int plays in new[] { 0, 1, 3, 65536 })
        {
            using var destination = new ForwardOnlyStream();
            using (var writer = new GifAnimationWriter(destination, 3, 2, plays))
            using (var bitmap = new SKBitmap(new SKImageInfo(3, 2, SKColorType.Bgra8888, SKAlphaType.Premul), 20))
            {
                bitmap.Erase(SKColors.Transparent);
                bitmap.SetPixel(0, 0, SKColors.Red);
                writer.AddFrame(bitmap, TimeSpan.FromMilliseconds(100));
                int firstLength = destination.Bytes.Length;
                check(firstLength >= 13, "AddFrame writes to a forward-only stream before completion");

                bitmap.Erase(SKColors.Transparent);
                bitmap.SetPixel(1, 0, new SKColor(0, 255, 0, 128));
                bitmap.SetPixel(1, 1, new SKColor(0, 255, 0, 127));
                writer.AddFrame(bitmap, TimeSpan.FromMilliseconds(84), GifFrameDisposal.Keep);
                check(destination.Bytes.Length > firstLength, "The next frame is also written immediately");

                bitmap.Erase(SKColors.Transparent);
                bitmap.SetPixel(2, 0, SKColors.Blue);
                writer.AddFrame(bitmap, TimeSpan.FromMilliseconds(45), GifFrameDisposal.RestorePrevious);

                bitmap.Erase(SKColors.Transparent);
                bitmap.SetPixel(0, 1, SKColors.White);
                writer.AddFrame(bitmap, TimeSpan.FromMilliseconds(125));
                if (plays == 1) writer.Complete();
            }

            byte[] bytes = destination.Bytes;
            if (plays == 3) File.WriteAllBytes(Path.Combine(output, "written-animation.gif"), bytes);
            using var frames = ImageCodec.OpenFrames(bytes);
            check(frames.FrameCount == 4 && frames.LoopCount == plays, "GIF frame and total play counts");
            using var gif = new GifImage(bytes);
            check(gif.Frames.Select(f => f.Disposal).SequenceEqual(new[] { 2, 1, 3, 2 }), "GIF disposal metadata");
            check(gif.Frames.All(f => f.Palette.Length >= 6 && f.Compressed.Length > 0),
                "Public GIF reader retains palette and compressed frame data");
            int[] delays = [100, 80, 50, 130];
            for (int i = 0; i < delays.Length; i++)
                check(frames.GetFrameInfo(i).Duration?.TotalMilliseconds == delays[i], "GIF delay in hundredths");
            using var first = frames.DecodeFrame(0);
            using var second = frames.DecodeFrame(1);
            using var third = frames.DecodeFrame(2);
            using var fourth = frames.DecodeFrame(3);
            check(first.ReadPixel(0, 0).R == 255 && first.ReadPixel(0, 0).A == 255, "Reusing bitmap preserves earlier frame");
            check(second.ReadPixel(0, 0).A == 0 && second.ReadPixel(1, 0).G == 255 &&
                second.ReadPixel(1, 0).A == 255 && second.ReadPixel(1, 1).A == 0, "GIF palette, stride and alpha threshold");
            check(third.ReadPixel(1, 0).G == 255 && third.ReadPixel(2, 0).B == 255, "Keep preserves pixels behind the next frame");
            check(fourth.ReadPixel(1, 0).G == 255 && fourth.ReadPixel(2, 0).A == 0 &&
                fourth.ReadPixel(0, 1).A == 255, "RestorePrevious restores the prior canvas");
            using var retained = gif.DecodeFrame(0);
            gif.Dispose();
            using var separate = ImageCodec.Decode(bytes);
            separate.WritePixel(0, 0, new Rgba32(0, 0, 255, 255));
            check(retained.ReadPixel(0, 0) == new Rgba32(255, 0, 0, 255),
                "Returned GIF pixels survive reader disposal and other decodes into pooled storage");
            check(destination.CanWrite, "Writer leaves destination open");
        }
    }

    private sealed class ForwardOnlyStream : Stream
    {
        private readonly MemoryStream data = new();
        public byte[] Bytes => data.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => data.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) => data.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => data.Write(buffer);
        public override void Flush() => data.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) data.Dispose(); base.Dispose(disposing); }
    }
}
