# 原版与 SimdSkia GPU(Vulkan)性能对比：2026-10-21

> 这是较早同日的数据。新增采样/裁剪后的最终同机对照见 [本轮 GPU 报告](PERFORMANCE-GPU-FINAL-2026-10-21.md)，CPU/内存与已知兼容失败见 [最终汇总](PERFORMANCE-FINAL-2026-10-21.md)。

实际执行：Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz；Microsoft Windows 10.0.26300；.NET 8.0.32 / X64，逻辑 CPU 12。原版包 SkiaSharp 3.119.4。
原版路径：Native Skia GPU dispatch (Vulkan, NVIDIA GeForce GTX 1050 Ti)；SimdSkia 路径：SimdSkia Vulkan compute (NVIDIA GeForce GTX 1050 Ti)。
每种实现 3 个独立进程；每项每进程 5 批，目标每批 70 ms，预热至少 100 ms 且 3 帧，再校准。进程顺序/场景顺序打乱，串行执行。TieredCompilation=0。
主要结论：以 1024×768 为例，以下只列通过画质门槛的场景。

- clear：原版 2.309 ms，SimdSkia 0.765 ms，为原版耗时的 0.33 倍。
- rectangles：原版 2.053 ms，SimdSkia 1.456 ms，为原版耗时的 0.71 倍。
- blend：原版 2.048 ms，SimdSkia 3.533 ms，为原版耗时的 1.73 倍。
- evenodd：原版 2.028 ms，SimdSkia 1.247 ms，为原版耗时的 0.62 倍。
- stroke：原版 2.050 ms，SimdSkia 1.082 ms，为原版耗时的 0.53 倍。
- gradient：原版 2.078 ms，SimdSkia 1.203 ms，为原版耗时的 0.58 倍。

这次只比较同机 Vulkan GPU round-trip（绘制全部命令 + 提交 + 等待 fence + 回读主机）。未与 CPU 光栅结果混用；CPU 结果见 PERFORMANCE.md。

原始数据：[JSON](../artifacts/performance-gpu/optimized-final-2026-10-21/summary.json)、[CSV](../artifacts/performance-gpu/optimized-final-2026-10-21/results.csv)、[配置](../artifacts/performance-gpu/optimized-final-2026-10-21/run.json)。工作进程数据、逐帧 ticks、像素和日志保留在同目录。

## 256×256：时间与质量

表内 ms 为多进程所有批均值的中位数；‘慢倍数’=SimdSkia / 原版，低于 1 表示更快。质量未过不给性能胜负结论。

| 场景 | 原版 ms | SimdSkia ms | 慢倍数 | SimdSkia P95 ms | 质量 |
| --- | ---: | ---: | ---: | ---: | --- |
| clear | 0.191 | 0.124 | 0.65× | 0.133 | 通过 max=0 |
| rectangles | 0.302 | 0.640 | 2.12× | 0.674 | 通过 max=0 |
| blend | 0.216 | 0.717 | 3.31× | 0.751 | 通过 max=0 |
| evenodd | 0.210 | 0.162 | 0.77× | 0.171 | 通过 max=0 |
| stroke | 0.184 | 0.159 | 0.87× | 0.179 | 通过 max=0 |
| rect-aa | 0.210 | 0.161 | — | 0.179 | 诊断：max 41>2 |
| gradient | 0.205 | 0.156 | 0.76× | 0.180 | 通过 max=1 |
| image | 0.192 | 0.154 | — | 0.166 | 诊断：max 249>0 |
| curve-aa | 0.221 | 0.276 | — | 0.403 | 诊断：max 45>32 |
| chinese | 0.228 | 2.229 | — | 2.282 | 诊断：max 173>32 |
| curve-changing | 0.210 | 0.385 | — | 0.434 | 诊断：max 45>32 |
| chinese-changing | 0.209 | 3.071 | — | 3.547 | 诊断：max 173>32 |

![GPU latency chart](../artifacts/performance-gpu/optimized-final-2026-10-21/latency-256x256.svg)

### 托管分配、进程内存、CPU 使用率

下表内存绝对值含 runtime、Vulkan 驱动映射与先前场景，不等于显存/渲染缓冲用量。CPU ms/帧为各批进程总处理器时间累计/总帧数；CPU% 按累计 CPU 时间/累计墙钟加权，单逻辑核归一（100% = 一核满载）。JSON/CSV 同时提供除以逻辑核数的机器占用。满速占用不等于固定帧率应用占用，Windows 处理器时间有计数粒度，短批内 0 不代表不消耗 CPU。

| 场景 | 原 B/op | Simd B/op | 原 CPU ms/帧 | Simd CPU ms/帧 | 原 CPU% 单核 | Simd CPU% 单核 | 原 private MiB | Simd private MiB | 原 working set MiB | Simd working set MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| clear | 0 | 656 | 0.119 | 0.062 | 63.6 | 49.9 | 106.2 | 92.1 | 104.5 | 123.9 |
| rectangles | 0 | 657 | 0.176 | 0.151 | 65.6 | 23.8 | 106.6 | 87.5 | 103.4 | 120.2 |
| blend | 0 | 657 | 0.167 | 0.101 | 69.7 | 14.1 | 107.2 | 87.3 | 104.1 | 117.8 |
| evenodd | 0 | 656 | 0.121 | 0.077 | 60.7 | 47.4 | 106.6 | 90.4 | 104.1 | 121.1 |
| stroke | 0 | 656 | 0.143 | 0.085 | 74.7 | 52.6 | 105.7 | 94.9 | 103.4 | 125.8 |
| rect-aa | 0 | 656 | 0.121 | 0.064 | 60.8 | 39.6 | 106.5 | 94.6 | 104.2 | 126.1 |
| gradient | 0 | 656 | 0.149 | 0.070 | 69.7 | 43.5 | 105.2 | 87.5 | 103.8 | 120.5 |
| image | 104 | 1040 | 0.147 | 0.069 | 73.8 | 44.7 | 106.2 | 90.0 | 104.5 | 122.6 |
| curve-aa | 0 | 656 | 0.124 | 0.068 | 59.7 | 23.1 | 106.6 | 93.6 | 104.5 | 125.8 |
| chinese | 640 | 660 | 0.156 | 0.098 | 67.9 | 4.4 | 107.1 | 100.7 | 104.8 | 131.0 |
| curve-changing | 0 | 40900 | 0.117 | 0.085 | 57.4 | 22.8 | 106.7 | 90.5 | 104.4 | 120.2 |
| chinese-changing | 640 | 373085 | 0.153 | 1.128 | 68.2 | 36.9 | 107.3 | 110.1 | 104.2 | 142.7 |

完整 op/s、GC 次数、托管 retained、进程历史峰值、IQR 和 CV 在 CSV/JSON。


## 1024×768：时间与质量

表内 ms 为多进程所有批均值的中位数；‘慢倍数’=SimdSkia / 原版，低于 1 表示更快。质量未过不给性能胜负结论。

| 场景 | 原版 ms | SimdSkia ms | 慢倍数 | SimdSkia P95 ms | 质量 |
| --- | ---: | ---: | ---: | ---: | --- |
| clear | 2.309 | 0.765 | 0.33× | 0.813 | 通过 max=0 |
| rectangles | 2.053 | 1.456 | 0.71× | 1.526 | 通过 max=0 |
| blend | 2.048 | 3.533 | 1.73× | 3.584 | 通过 max=0 |
| evenodd | 2.028 | 1.247 | 0.62× | 1.319 | 通过 max=0 |
| stroke | 2.050 | 1.082 | 0.53× | 1.150 | 通过 max=0 |
| rect-aa | 2.060 | 1.195 | — | 1.256 | 诊断：max 41>2 |
| gradient | 2.078 | 1.203 | 0.58× | 1.266 | 通过 max=1 |
| image | 2.052 | 1.188 | — | 1.244 | 诊断：max 253>0 |
| curve-aa | 2.024 | 1.550 | — | 1.609 | 诊断：max 44>32 |
| chinese | 2.049 | 4.138 | — | 4.226 | 诊断：max 173>32 |
| curve-changing | 2.050 | 1.760 | — | 1.855 | 诊断：max 44>32 |
| chinese-changing | 2.086 | 4.945 | — | 5.547 | 诊断：max 173>32 |

![GPU latency chart](../artifacts/performance-gpu/optimized-final-2026-10-21/latency-1024x768.svg)

### 托管分配、进程内存、CPU 使用率

下表内存绝对值含 runtime、Vulkan 驱动映射与先前场景，不等于显存/渲染缓冲用量。CPU ms/帧为各批进程总处理器时间累计/总帧数；CPU% 按累计 CPU 时间/累计墙钟加权，单逻辑核归一（100% = 一核满载）。JSON/CSV 同时提供除以逻辑核数的机器占用。满速占用不等于固定帧率应用占用，Windows 处理器时间有计数粒度，短批内 0 不代表不消耗 CPU。

| 场景 | 原 B/op | Simd B/op | 原 CPU ms/帧 | Simd CPU ms/帧 | 原 CPU% 单核 | Simd CPU% 单核 | 原 private MiB | Simd private MiB | 原 working set MiB | Simd working set MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| clear | 0 | 657 | 1.450 | 0.260 | 65.5 | 34.0 | 109.3 | 97.1 | 106.8 | 127.9 |
| rectangles | 0 | 658 | 1.467 | 0.449 | 67.1 | 30.8 | 109.4 | 91.9 | 106.7 | 126.3 |
| blend | 0 | 661 | 1.530 | 0.417 | 70.8 | 11.8 | 109.0 | 95.1 | 107.2 | 122.8 |
| evenodd | 0 | 658 | 1.531 | 0.356 | 72.4 | 28.5 | 109.4 | 94.0 | 106.6 | 126.9 |
| stroke | 0 | 658 | 1.596 | 0.220 | 75.1 | 20.3 | 109.2 | 93.7 | 106.9 | 123.8 |
| rect-aa | 0 | 658 | 1.497 | 0.345 | 68.5 | 29.0 | 108.0 | 97.2 | 106.6 | 126.2 |
| gradient | 0 | 658 | 1.353 | 0.246 | 62.4 | 20.5 | 109.3 | 97.2 | 106.8 | 126.2 |
| image | 104 | 1042 | 1.400 | 0.330 | 65.2 | 27.9 | 109.2 | 93.9 | 106.8 | 124.8 |
| curve-aa | 0 | 658 | 1.484 | 0.391 | 69.6 | 25.3 | 107.5 | 101.3 | 106.7 | 133.7 |
| chinese | 640 | 664 | 1.487 | 0.551 | 72.6 | 13.4 | 110.0 | 108.9 | 107.5 | 137.7 |
| curve-changing | 0 | 273534 | 1.317 | 0.525 | 62.7 | 29.8 | 104.2 | 100.0 | 106.2 | 129.8 |
| chinese-changing | 640 | 544831 | 1.594 | 1.116 | 74.3 | 22.6 | 109.6 | 120.7 | 107.1 | 151.4 |

完整 op/s、GC 次数、托管 retained、进程历史峰值、IQR 和 CV 在 CSV/JSON。


## 解读边界

- 每个‘op’是一整帧 GPU round-trip：绘制全部命令 + flush 提交 + fence 等待 + 主机回读拷贝。rectangles/blend 每帧 65 条命令（1 clear+64 draw），chinese 含 4 行文字，其余 1 项。几何/字体/纹理准备不在计时内；每帧的命令录制、提交、等待与回读在计时内。
- 双方目标像素格式 RGBA8888/Premul，同一 GPU 上各自独立初始化 Vulkan（原版用 graphics queue + GRContext 光栅后端；SimdSkia 用 compute queue + 自研 compute shader 管线）。
- 固定场景允许正常复用纹理/缓存（SimdSkia 纹理缓存 ≤32 个/64MiB LRU）；curve-changing/chinese-changing 场景每帧移动 1/16 像素、97 个位置循环，强制覆盖率/文字缓存持续未命中。变化场景像素 gate 检查 frame=0。
- 质量门槛以原版 GPU 输出为基准逐通道比较；AA/字体必须检查本轮相同尺寸的像素 gate；失败的时间保留为诊断数据。未提高容差，也未降低画质。
- B/op 是线程托管分配，不含 native malloc 与显存。private/working set 是整个进程，包含 runtime、Vulkan 驱动映射、native/字体缓存和先前场景，不能当显存或纯渲染缓冲内存公平排名；peak working set 是进程历史峰值。
- P95 是实际单帧延迟，包含 GC/调度/驱动抖动；耗时列是批均值中位数，两者定义不同。
- 帧时间由 CPU 计时（Stopwatch）测得，包含每帧回读同步；驱动内部排队可能引入噪声。样本不控制 OS power plan/Turbo/其他用户进程，不给所有硬件的优势承诺。

