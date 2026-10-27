using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using SimdSkia.ControlValidation;
#if MANAGED
using SimdSkia.Avalonia;
#endif

namespace SimdSkia.AotComparison;

internal static class Benchmark
{
    internal static async Task Run(Control scene, Window window)
    {
        var options = Program.Options;
        ControlScene.Prepare(scene);
        if (options.Scene == "controls" && (!scene.GetVisualDescendants().OfType<Button>().Any()
            || !scene.GetVisualDescendants().OfType<TextBox>().Any(t => t.GetVisualChildren().Any())))
            throw new InvalidOperationException("Real Fluent control templates were not instantiated.");
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)(1000 * options.Scale), (int)(720 * options.Scale)),
            new Vector(96 * options.Scale, 96 * options.Scale));
        var first = Stopwatch.StartNew(); bitmap.Render(scene); first.Stop();
        double readyMs = Program.Startup.Elapsed.TotalMilliseconds;
        Console.WriteLine("READY"); Console.Out.Flush();
        VerifyImageBrush();
        // Allow the compositor and the real Win32 software surface to present, outside the draw timing.
        await Task.Delay(150);
#if MANAGED
        if (SimdSkiaDiagnostics.FramesPresented == 0) throw new InvalidOperationException("The Win32 software window never presented a frame.");
#endif
        var transform = new TranslateTransform();
        if (options.Scene == "scroll") scene.RenderTransform = transform;
        int sequence = 0;
        void Render()
        {
            if (options.Scene == "scroll") transform.Y = -(sequence++ % 17);
            bitmap.Render(scene);
        }
        var warm = Stopwatch.StartNew();
        while (warm.Elapsed.TotalSeconds < options.Warmup) Render();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        // Preallocate timings, so the harness does not inflate per-frame allocations.
        var frameTicks = new long[Math.Max(1024, checked((int)Math.Ceiling(options.Seconds * 10_000)))];
        using var process = Process.GetCurrentProcess();
        process.Refresh(); long wsBefore = process.WorkingSet64, privateBefore = process.PrivateMemorySize64;
        long allocationBefore = GC.GetTotalAllocatedBytes(true);
        int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        double cpuBefore = process.TotalProcessorTime.TotalMilliseconds;
        Console.WriteLine("MEASURE_BEGIN"); Console.Out.Flush();
        var timer = Stopwatch.StartNew(); int frames = 0, missedDeadlines = 0;
        double interval = Stopwatch.Frequency / 60d; long origin = Stopwatch.GetTimestamp();
        while (timer.Elapsed.TotalSeconds < options.Seconds)
        {
            long started = Stopwatch.GetTimestamp(); Render();
            long elapsed = Stopwatch.GetTimestamp() - started;
            if (frames == frameTicks.Length) throw new InvalidOperationException("Increase the preallocated timing capacity.");
            frameTicks[frames++] = elapsed;
            if (options.Mode == "paced")
            {
                long target = origin + (long)(frames * interval);
                long now = Stopwatch.GetTimestamp();
                if (now > target) missedDeadlines++;
                else
                {
                    // Yield without spinning: CPU includes actual drawing, timer and desktop presentation work.
                    double waitMs = (target - now) * 1000d / Stopwatch.Frequency;
                    if (waitMs >= 1) await Task.Delay(TimeSpan.FromMilliseconds(waitMs));
                }
            }
        }
        timer.Stop();
        double cpuMs = process.TotalProcessorTime.TotalMilliseconds - cpuBefore;
        long allocated = GC.GetTotalAllocatedBytes(true) - allocationBefore;
        gc0 = GC.CollectionCount(0) - gc0; gc1 = GC.CollectionCount(1) - gc1; gc2 = GC.CollectionCount(2) - gc2;
        Console.WriteLine("MEASURE_END"); Console.Out.Flush();
        process.Refresh();
        long workingSet = process.WorkingSet64, privateBytes = process.PrivateMemorySize64, peak = process.PeakWorkingSet64;
        long usedHeap = GC.GetTotalMemory(false); long retainedHeap = GC.GetTotalMemory(true);
        Array.Sort(frameTicks, 0, frames);
        double Percentile(double p) => frameTicks[Math.Clamp((int)Math.Ceiling(p * frames) - 1, 0, frames - 1)] * 1000d / Stopwatch.Frequency;
        double drawMs = 0; for (int i = 0; i < frames; i++) drawMs += frameTicks[i] * 1000d / Stopwatch.Frequency;
        var loadedModules = process.Modules.Cast<ProcessModule>().Select(m => m.ModuleName).Order(StringComparer.OrdinalIgnoreCase).ToArray();
#if MANAGED
        if (loadedModules.Any(n => n.Contains("skia", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || n.Contains("harfbuzz", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A forbidden native drawing/font module was loaded.");
#else
        if (!loadedModules.Any(n => n.Equals("libSkiaSharp.dll", StringComparison.OrdinalIgnoreCase))
            || !loadedModules.Any(n => n.Equals("libHarfBuzzSharp.dll", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The isolated original renderer/font native modules were not loaded.");
#endif
        using (var stream = File.Create(Path.Combine(options.Output!, "metrics.json")))
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("engine", Program.Engine); json.WriteString("runtime", Environment.Version.ToString());
            json.WriteBoolean("nativeAot", !RuntimeFeature.IsDynamicCodeSupported); json.WriteBoolean("imageBrushProbePassed", true); json.WriteString("os", RuntimeInformation.OSDescription);
            json.WriteBoolean("avx2", System.Runtime.Intrinsics.X86.Avx2.IsSupported);
            json.WriteNumber("vectorBits", System.Numerics.Vector<byte>.Count * 8);
            json.WriteString("scene", options.Scene); json.WriteString("mode", options.Mode);
#if MANAGED
            json.WriteString("opacityMode", "full group (SimdSkia backend)");
#else
            json.WriteString("opacityMode", options.FullOpacity ? "full group" : "default per draw");
#endif
            json.WriteNumber("scale", options.Scale); json.WriteNumber("width", bitmap.PixelSize.Width); json.WriteNumber("height", bitmap.PixelSize.Height);
            json.WriteNumber("processors", Environment.ProcessorCount); json.WriteNumber("frames", frames);
            json.WriteNumber("wallMs", timer.Elapsed.TotalMilliseconds); json.WriteNumber("drawMsPerFrame", drawMs / frames);
            json.WriteNumber("wallMsPerFrame", timer.Elapsed.TotalMilliseconds / frames); json.WriteNumber("framesPerSecond", frames / timer.Elapsed.TotalSeconds);
            json.WriteNumber("p50Ms", Percentile(.5)); json.WriteNumber("p95Ms", Percentile(.95)); json.WriteNumber("p99Ms", Percentile(.99));
            json.WriteNumber("cpuMs", cpuMs); json.WriteNumber("cpuMsPerFrame", cpuMs / frames);
            json.WriteNumber("cpuPercentOneCore", 100 * cpuMs / timer.Elapsed.TotalMilliseconds);
            json.WriteNumber("cpuPercentMachine", 100 * cpuMs / timer.Elapsed.TotalMilliseconds / Environment.ProcessorCount);
            json.WriteNumber("allocatedBytesPerFrame", (double)allocated / frames);
            json.WriteNumber("workingSetMiB", workingSet / 1048576d); json.WriteNumber("privateMiB", privateBytes / 1048576d);
            json.WriteNumber("peakWorkingSetMiB", peak / 1048576d); json.WriteNumber("workingSetBeforeMiB", wsBefore / 1048576d);
            json.WriteNumber("privateBeforeMiB", privateBefore / 1048576d); json.WriteNumber("managedHeapMiB", usedHeap / 1048576d);
            json.WriteNumber("managedRetainedMiB", retainedHeap / 1048576d);
            json.WriteNumber("gc0", gc0); json.WriteNumber("gc1", gc1); json.WriteNumber("gc2", gc2);
            json.WriteNumber("firstDrawMs", first.Elapsed.TotalMilliseconds); json.WriteNumber("mainToFirstOffscreenFrameMs", readyMs);
            json.WriteNumber("missedDeadlines", missedDeadlines);
            json.WriteString("fontSha256", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Program.Font))));
            json.WriteStartArray("loadedModules"); foreach (string name in loadedModules) json.WriteStringValue(name); json.WriteEndArray();
            json.WriteEndObject();
        }
        // Capture a fixed pose outside all CPU/frame/memory measurements for reproducible quality checks.
        if (options.Scene == "scroll") transform.Y = -8;
        bitmap.Render(scene);
        using (var stream = File.Create(Path.Combine(options.Output!, "scene.png"))) bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        byte[] pixels = new byte[checked(bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4)];
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { bitmap.CopyPixels(new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), pinned.AddrOfPinnedObject(), pixels.Length, bitmap.PixelSize.Width * 4); }
        finally { pinned.Free(); }
        if (bitmap.Format == PixelFormats.Bgra8888) for (int i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        File.WriteAllBytes(Path.Combine(options.Output!, "scene.rgba"), pixels);
#if MANAGED
        if(Environment.GetEnvironmentVariable("SIMD_SKIA_PROFILE")=="1")
        {
            using var profile=File.Create(Path.Combine(options.Output!,"drawing-profile.json"));
            using var writer=new Utf8JsonWriter(profile,new(){Indented=true});
            writer.WriteStartObject();
            foreach(var entry in SimdSkiaDiagnostics.GetDrawingTimings())
            {
                writer.WriteStartObject(entry.Key);writer.WriteNumber("calls",entry.Value.Calls);
                writer.WriteNumber("milliseconds",entry.Value.Milliseconds);writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
#endif
        Console.WriteLine($"DONE {Program.Engine} {options.Scene} {options.Mode}: {frames} frames");
    }

    private static void VerifyImageBrush()
    {
        using var source = ControlScene.ImageFixture();
        var control = new Border { Width = 32, Height = 16, Background = new ImageBrush(source) { Stretch = Stretch.Fill } };
        RenderOptions.SetBitmapInterpolationMode(control, BitmapInterpolationMode.None);
        ControlScene.Prepare(control, 32, 16);
        using var target = new RenderTargetBitmap(new PixelSize(32, 16), new Vector(96, 96));
        target.Render(control);
        byte[] bytes = new byte[32 * 16 * 4]; var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { target.CopyPixels(new PixelRect(0, 0, 32, 16), pinned.AddrOfPinnedObject(), bytes.Length, 32 * 4); }
        finally { pinned.Free(); }
        int p = (8 * 32 + 10) * 4;
        int red = target.Format == PixelFormats.Bgra8888 ? 2 : 0, blue = 2 - red;
        if (bytes[p + red] != 70 || bytes[p + 1] != 120 || bytes[p + blue] != 80 || bytes[p + 3] != 255)
            throw new InvalidOperationException("Native AOT ImageBrush reflection/bitmap round trip produced incorrect pixels.");
    }
}
