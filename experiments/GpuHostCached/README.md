# HOST_CACHED GPU experiment

This project is isolated from the production solution and reference graph. It compiles the current compatibility API and shader with one Vulkan allocation change: prefer HOST_CACHED when allocating the host-visible coherent render target. A driver without a compatible cached type falls back to the original selection.

The current production render target selects the first HOST_VISIBLE | HOST_COHERENT memory type. On the tested GTX 1050 Ti this is type 8 (flags 6). Type 9 (flags 14) also supports HOST_CACHED. The experiment preserves GPU coverage, sampling, compositing, fence synchronization, host readback, texture ownership and resource disposal.

Run the shared GPU regression suite:

```powershell
dotnet run --project experiments/GpuHostCached/Tests -c Release
```

First complete the current-product baseline, then run all 3 experimental workers using the same workload, font, sampling and dimensions:

```powershell
powershell -NoProfile -File tools/run-gpu-benchmarks.ps1 -Output artifacts/performance-gpu/my-review -SampleMilliseconds 100
powershell -NoProfile -File tools/run-gpu-host-cached-experiment.ps1 -Baseline artifacts/performance-gpu/my-review -Output artifacts/performance-gpu/my-cached-review
```

Only Windows NVIDIA hardware has been measured. This is an allocation experiment, not a replacement text rasterizer, a swapchain backend, or proof of cross-platform performance. See docs/REASSESSMENT.md for current, original and experimental measurements and quality gates.
