# CPU benchmark workers

Managed and Reference compile exactly the same Workloads/Worker source, referencing different assemblies in isolated processes. Reference defines ORIGINAL only to select a forced RGBA8888/Unpremul decode overload and prevent it being mislabeled as SimdSkia. Original Skia exists only in this reference test project.

Each case is set up outside timing, warmed, calibrated to a target sample duration, then repeatedly measured. Per-frame Stopwatch ticks, managed allocations and GC collections are recorded. No per-sample GC is forced during the timed loop; GC is forced before measuring and before retained-memory capture. Memory snapshots are whole-process values, including native memory, runtime and caches. Managed allocation counters do not count native malloc.

PNG encoding compares pixel output but uses different compression strategies; output size is recorded and this case is not eligible for an equal-compression speed claim. PNG decoding consumes the same original-generated compressed fixture and requests the same RGBA8888/Unpremul output. All raster cases use CPU, RGBA8888/Premul, tightly packed rows and the same font/data/commands. Font/curve quality gates can fail; their timings must remain diagnostic.

Explicit scalar/SIMD mode controls SimdSkia's optimized kernels; it does not disable all CLR/BCL intrinsics. Separate process-wide instruction-disable correctness tests are also required.

`benchmarks/Sampling` compares the CPU linear sampler against isolated SkiaSharp 3.119.4 calls using the same 512×384 source pixels, source Alpha metadata, clear and output sizes. `tools/run-core-sampling-comparison.py` rotates three processes per frozen-before/current/original/scalar variant, captures CPU time, allocations, whole-process memory and raw pixels, and retains failed original quality gates. Build and freeze the before worker before changing the engine; this tool deliberately refuses to overwrite previous results. The native reference is used only by the `original` worker process. This sampling microbenchmark is not a GPU or whole-Avalonia throughput measurement.
