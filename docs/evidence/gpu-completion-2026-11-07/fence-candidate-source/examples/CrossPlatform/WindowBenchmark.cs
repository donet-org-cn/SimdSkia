using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SimdSkia.Avalonia;

internal static class WindowBenchmark
{
    internal static void Start(Window window,Button button,IClassicDesktopStyleApplicationLifetime desktop)
    {
        static double Duration(string name,double fallback)
        {
            string? text=Environment.GetEnvironmentVariable(name);if(text is null)return fallback;
            if(!double.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double value)||!double.IsFinite(value)||value<0||value>120)
                throw new ArgumentException($"{name} must be in [0, 120] seconds.");
            return value;
        }
        double warmup=Duration("SIMDSKIA_BENCH_WARMUP_SECONDS",2),seconds=Duration("SIMDSKIA_BENCH_MEASURE_SECONDS",3);
        var process=Process.GetCurrentProcess();var clock=Stopwatch.StartNew();
        bool measuring=false;long allocated=0,frames=0,cpuTicks=0;double started=0;long peakWorking=0,peakPrivate=0;int changes=0;
        SimdSkiaGpuStatistics? gpuStart=null;
        bool synchronous=SimdSkiaGpuDiagnostics.SynchronizeNativeFrames,draining=false;
        long nativeStart=0,completedStart=0,drainFrame=0,occludedStart=0,stateChangesStart=0;
        double drainStart=0,waitStart=0;
        long timelineStart=0,explicitFlushStart=0;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};
        timer.Tick+=(_,_)=>
        {
            try
            {
                button.Content=$"Frame / 帧 {changes++%10}";window.InvalidateVisual();
                if(!measuring&&clock.Elapsed.TotalSeconds>=warmup)
                {
                    process.Refresh();allocated=GC.GetTotalAllocatedBytes(true);cpuTicks=process.TotalProcessorTime.Ticks;frames=SimdSkiaDiagnostics.FramesPresented;
                    gpuStart=SimdSkiaGpuDiagnostics.GetStatistics();started=clock.Elapsed.TotalSeconds;SimdSkiaDiagnostics.BeginFrameTiming();measuring=true;
                    nativeStart=SimdSkiaGpuDiagnostics.NativeSubmittedFrames;completedStart=SimdSkiaGpuDiagnostics.NativeCompletedFrames;
                    waitStart=SimdSkiaGpuDiagnostics.NativeCompletionWaitMilliseconds;
                    occludedStart=SimdSkiaGpuDiagnostics.NativeOccludedPresents;
                    stateChangesStart=SimdSkiaGpuDiagnostics.NativeStateChanges;
                    timelineStart=SimdSkiaGpuDiagnostics.NativeTimelineSignals;explicitFlushStart=SimdSkiaGpuDiagnostics.NativeExplicitCompletionFlushes;
                }
                if(!measuring)return;
                process.Refresh();peakWorking=Math.Max(peakWorking,process.WorkingSet64);peakPrivate=Math.Max(peakPrivate,process.PrivateMemorySize64);
                double elapsed=clock.Elapsed.TotalSeconds-started;if(elapsed<seconds||(SimdSkiaDiagnostics.FramesPresented-frames<60&&elapsed<Math.Max(15,seconds)))return;
                if(SimdSkiaGpuDiagnostics.NativeSubmittedFrames>nativeStart)
                {
                    // One final synchronized frame drains every prior GPU frame.
                    // Include this tail in elapsed/process CPU and all frame samples.
                    if(!draining){draining=true;drainStart=clock.Elapsed.TotalSeconds;drainFrame=SimdSkiaGpuDiagnostics.NativeSubmittedFrames;SimdSkiaGpuDiagnostics.SynchronizeNativeFrames=true;return;}
                    if(SimdSkiaGpuDiagnostics.NativeSubmittedFrames<=drainFrame||SimdSkiaGpuDiagnostics.NativePendingFrames!=0)return;
                    if(SimdSkiaGpuDiagnostics.NativeCompletedFrames<SimdSkiaGpuDiagnostics.NativeSubmittedFrames)throw new InvalidOperationException("GPU work was not drained.");
                }
                timer.Stop();double[] times=SimdSkiaDiagnostics.EndFrameTiming();Array.Sort(times);
                if(times.Length==0)throw new InvalidOperationException("No completed real-window frames.");
                var gpu=SimdSkiaGpuDiagnostics.GetStatistics();long cpu=process.TotalProcessorTime.Ticks-cpuTicks;
                Directory.CreateDirectory(Program.Output);
                using var output=File.Create(Path.Combine(Program.Output,"benchmark.json"));using(var json=new Utf8JsonWriter(output,new(){Indented=true}))
                {
                    json.WriteStartObject();json.WriteString("backend",Program.Gpu?.ToString()??(Program.Cpu?"Cpu":"Auto"));json.WriteString("selectedRenderer",Program.SelectedBackend);json.WriteBoolean("nativeAot",!RuntimeFeature.IsDynamicCodeSupported);
                    json.WriteString("os",RuntimeInformation.OSDescription);json.WriteString("architecture",RuntimeInformation.ProcessArchitecture.ToString());
                    json.WriteString("scene",Program.Controls?"controls":"simple");json.WriteNumber("width",window.ClientSize.Width);json.WriteNumber("height",window.ClientSize.Height);json.WriteNumber("scaling",window.RenderScaling);
                    json.WriteString("presentation",SimdSkiaGpuDiagnostics.NativeSwapchainPresents>0?"native DXGI; GPU event completion; final queue drain":gpu.GpuCommands>0?"GPU fence and pixel readback; OS framebuffer":"CPU OS framebuffer");
                    json.WriteString("drawingTimingSemantics",SimdSkiaGpuDiagnostics.NativeSwapchainPresents>0&&!synchronous?"CPU recording/submission and bounded queue waits; not per-frame GPU latency":"CPU rendering or synchronized GPU completion");
                    json.WriteBoolean("visibleWindow",Environment.GetCommandLineArgs().Contains("--visible-benchmark"));
                    json.WriteNumber("nativeSubmittedFrames",SimdSkiaGpuDiagnostics.NativeSubmittedFrames-nativeStart);json.WriteNumber("nativeCompletedFrames",SimdSkiaGpuDiagnostics.NativeCompletedFrames-completedStart);
                    json.WriteNumber("nativePendingFrames",SimdSkiaGpuDiagnostics.NativePendingFrames);json.WriteNumber("nativeCompletionWaitMilliseconds",SimdSkiaGpuDiagnostics.NativeCompletionWaitMilliseconds-waitStart);
                    json.WriteNumber("nativeOccludedPresents",SimdSkiaGpuDiagnostics.NativeOccludedPresents-occludedStart);
                    json.WriteNumber("nativeStateChanges",SimdSkiaGpuDiagnostics.NativeStateChanges-stateChangesStart);
                    json.WriteNumber("nativeTimelineSignals",SimdSkiaGpuDiagnostics.NativeTimelineSignals-timelineStart);json.WriteNumber("nativeExplicitCompletionFlushes",SimdSkiaGpuDiagnostics.NativeExplicitCompletionFlushes-explicitFlushStart);
                    json.WriteNumber("finalDrainSeconds",draining?clock.Elapsed.TotalSeconds-drainStart:0);
                    json.WriteNumber("initializationFailures",SimdSkiaGpuDiagnostics.InitializationFailures);json.WriteString("lastInitializationFailure",SimdSkiaGpuDiagnostics.LastInitializationFailure);
                    json.WriteNumber("blockingCompletionWaits",SimdSkiaGpuDiagnostics.BlockingCompletionWaits);json.WriteNumber("completionPolls",SimdSkiaGpuDiagnostics.CompletionPolls);
                    json.WriteNumber("flipSwapchainPresents",SimdSkiaGpuDiagnostics.FlipSwapchainPresents);
                    json.WriteBoolean("diagnosticCaptureDuringMeasurement",false);json.WriteNumber("warmupSeconds",warmup);json.WriteNumber("measurementSeconds",elapsed);
                    json.WriteBoolean("gpuTimestampProfilingEnabled",SimdSkiaGpuDiagnostics.CaptureVulkanProfiles);
                    json.WriteBoolean("sampleCountSufficient",times.Length>=60);
                    json.WriteNumber("presentedFrames",SimdSkiaDiagnostics.FramesPresented-frames);json.WriteNumber("timedFrames",times.Length);json.WriteNumber("drawingMeanMs",times.Average());json.WriteNumber("drawingP50Ms",times[times.Length/2]);json.WriteNumber("drawingP95Ms",times[(int)Math.Floor((times.Length-1)*.95)]);
                    json.WriteNumber("cpuMilliseconds",cpu/(double)TimeSpan.TicksPerMillisecond);json.WriteNumber("cpuPercentOneCore",cpu/(double)TimeSpan.TicksPerSecond/elapsed*100);
                    json.WriteNumber("cpuPercentMachine",cpu/(double)TimeSpan.TicksPerSecond/elapsed/Environment.ProcessorCount*100);
                    json.WriteNumber("allocatedBytes",GC.GetTotalAllocatedBytes(true)-allocated);json.WriteNumber("peakWorkingSetBytes",peakWorking);json.WriteNumber("peakPrivateBytes",peakPrivate);
                    json.WriteNumber("gpuCommands",gpu.GpuCommands-gpuStart!.GpuCommands);json.WriteNumber("cpuFallbackCommands",gpu.CpuFallbackCommands-gpuStart.CpuFallbackCommands);
                    json.WriteNumber("gpuCompletedBatches",gpu.CompletedBatches-gpuStart.CompletedBatches);json.WriteNumber("gpuCompletionMilliseconds",gpu.CompletionMilliseconds-gpuStart.CompletionMilliseconds);
                    json.WriteString("gpuDevice",gpu.Device);json.WriteNumber("nativeSwapchainPresents",SimdSkiaGpuDiagnostics.NativeSwapchainPresents);
                    json.WriteNumber("managedGlyphAtlasBuilds",SimdSkiaGpuDiagnostics.ManagedGlyphAtlasBuilds);json.WriteNumber("managedGlyphAtlasHits",SimdSkiaGpuDiagnostics.ManagedGlyphAtlasHits);
                    json.WriteNumber("vulkanPathMaskBuildsTotal",SimdSkiaGpuDiagnostics.VulkanPathMaskBuilds);json.WriteNumber("vulkanPathMaskHitsTotal",SimdSkiaGpuDiagnostics.VulkanPathMaskHits);
                    json.WriteNumber("vulkanPathMaskPeakBytes",SimdSkiaGpuDiagnostics.VulkanPathMaskPeakBytes);json.WriteNumber("vulkanDrawDispatchesTotal",SimdSkiaGpuDiagnostics.VulkanDrawDispatches);json.WriteNumber("vulkanDrawDependencyBarriersTotal",SimdSkiaGpuDiagnostics.VulkanDrawDependencyBarriers);
                    json.WriteStartArray("frameMilliseconds");foreach(double t in times)json.WriteNumberValue(t);json.WriteEndArray();json.WriteEndObject();
                }
                Console.WriteLine($"PASS benchmark {Program.SelectedBackend}; completed frames={times.Length}; P50={times[times.Length/2]:F3}ms; workset={peakWorking/1048576d:F1}MiB");
                if(SimdSkiaGpuDiagnostics.LastVulkanProfile is {} profile)
                    File.WriteAllText(Path.Combine(Program.Output,"vulkan-profile.json"),JsonSerializer.Serialize(profile,VulkanProfileJsonContext.Default.VulkanProfile));
                process.Dispose();desktop.Shutdown();
            }
            catch(Exception error){timer.Stop();Console.Error.WriteLine(error);process.Dispose();desktop.Shutdown(1);}
        };
        timer.Start();
    }
}
