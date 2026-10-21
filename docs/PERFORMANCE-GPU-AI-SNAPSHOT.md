# 原版与 SimdSkia GPU(Vulkan)性能对比：2026-10-12

实际执行：Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz；Microsoft Windows 10.0.26300；.NET 8.0.31 / X64，逻辑 CPU 12。原版包 SkiaSharp 3.119.4。
原版路径：Native Skia GPU dispatch (Vulkan, NVIDIA GeForce GTX 1050 Ti)；SimdSkia 路径：SimdSkia Vulkan compute (NVIDIA GeForce GTX 1050 Ti)。
每种实现 3 个独立进程；每项每进程 7 批，目标每批 60 ms，预热至少 100 ms 且 3 帧，再校准。进程顺序/场景顺序打乱，串行执行。TieredCompilation=0。
主要结论：以 1024×768 为例，以下只列通过画质门槛的场景。

- clear：原版 2.569 ms，SimdSkia 10.824 ms，为原版耗时的 4.21 倍。
- rectangles：原版 2.668 ms，SimdSkia 13.155 ms，为原版耗时的 4.93 倍。
- blend：原版 2.271 ms，SimdSkia 21.263 ms，为原版耗时的 9.36 倍。
- evenodd：原版 2.248 ms，SimdSkia 12.436 ms，为原版耗时的 5.53 倍。
- stroke：原版 2.634 ms，SimdSkia 11.269 ms，为原版耗时的 4.28 倍。
- gradient：原版 2.337 ms，SimdSkia 12.490 ms，为原版耗时的 5.35 倍。

这次只比较同机 Vulkan GPU round-trip（绘制全部命令 + 提交 + 等待 fence + 回读主机）。未与 CPU 光栅结果混用；CPU 结果见 PERFORMANCE.md。

原始数据：[JSON](../results/gpu-opt5/summary.json)、[CSV](../results/gpu-opt5/results.csv)、[配置](../results/gpu-opt5/run.json)。工作进程数据、逐帧 ticks、像素和日志保留在同目录。

## 256×256：时间与质量

表内 ms 为多进程所有批均值的中位数；‘慢倍数’=SimdSkia / 原版，低于 1 表示更快。质量未过不给性能胜负结论。

| 场景 | 原版 ms | SimdSkia ms | 慢倍数 | SimdSkia P95 ms | 质量 |
| --- | ---: | ---: | ---: | ---: | --- |
| clear | 0.222 | 0.753 | 3.39× | 0.944 | 通过 max=0 |
| rectangles | 0.253 | 1.211 | 4.78× | 1.419 | 通过 max=0 |
| blend | 0.233 | 1.789 | 7.66× | 1.994 | 通过 max=0 |
| evenodd | 0.214 | 0.809 | 3.77× | 0.994 | 通过 max=0 |
| stroke | 0.217 | 0.778 | 3.58× | 0.980 | 通过 max=0 |
| rect-aa | 0.235 | 2.584 | — | 2.802 | 诊断：max 41>2 |
| gradient | 0.219 | 0.830 | 3.78× | 1.046 | 通过 max=1 |
| image | 0.278 | 0.824 | — | 1.015 | 诊断：max 249>0 |
| curve-aa | 0.246 | 5.092 | — | 5.749 | 诊断：max 45>32 |
| chinese | 0.218 | 266.707 | — | 266.995 | 诊断：max 173>32 |
| curve-changing | 0.201 | 5.102 | — | 5.615 | 诊断：max 45>32 |
| chinese-changing | 0.315 | 266.802 | — | 267.186 | 诊断：max 173>32 |

![GPU latency chart](../results/gpu-opt5/latency-256x256.svg)

### 托管分配、进程内存、CPU 使用率

下表内存绝对值含 runtime、Vulkan 驱动映射与先前场景，不等于显存/渲染缓冲用量。CPU% 为采样批内进程总处理器时间/墙钟，单逻辑核归一（100% = 单逻辑核满载）。

| 场景 | 原 B/op | Simd B/op | 原 CPU% | Simd CPU% | 原 private MiB | Simd private MiB | 原 working set MiB | Simd working set MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| clear | 1 | 878 | 75.0 | 97.3 | 102.9 | 87.2 | 91.0 | 85.6 |
| rectangles | 2 | 25459 | 77.4 | 52.9 | 103.0 | 85.1 | 91.1 | 84.8 |
| blend | 2 | 25462 | 80.4 | 50.7 | 102.5 | 78.5 | 90.7 | 78.0 |
| evenodd | 1 | 1286 | 74.4 | 78.6 | 102.9 | 90.3 | 91.0 | 88.9 |
| stroke | 1 | 1286 | 55.2 | 78.2 | 102.8 | 85.2 | 90.8 | 83.6 |
| rect-aa | 2 | 1276 | 53.5 | 0.0 | 103.2 | 89.2 | 91.1 | 88.1 |
| gradient | 2 | 1263 | 58.1 | 77.4 | 102.9 | 84.1 | 90.7 | 83.2 |
| image | 106 | 1646 | 52.9 | 77.0 | 103.4 | 85.5 | 91.5 | 85.0 |
| curve-aa | 2 | 1319 | 52.7 | 0.0 | 103.2 | 88.1 | 91.1 | 86.8 |
| chinese | 642 | 3005 | 57.8 | 0.0 | 104.0 | 100.5 | 91.5 | 99.9 |
| curve-changing | 1 | 10389 | 55.4 | 0.0 | 101.7 | 84.9 | 90.8 | 84.2 |
| chinese-changing | 642 | 116531 | 57.2 | 0.0 | 105.6 | 92.8 | 91.6 | 91.2 |

完整 op/s、GC 次数、托管 retained、进程历史峰值、IQR 和 CV 在 CSV/JSON。


## 1024×768：时间与质量

表内 ms 为多进程所有批均值的中位数；‘慢倍数’=SimdSkia / 原版，低于 1 表示更快。质量未过不给性能胜负结论。

| 场景 | 原版 ms | SimdSkia ms | 慢倍数 | SimdSkia P95 ms | 质量 |
| --- | ---: | ---: | ---: | ---: | --- |
| clear | 2.569 | 10.824 | 4.21× | 11.325 | 通过 max=0 |
| rectangles | 2.668 | 13.155 | 4.93× | 13.940 | 通过 max=0 |
| blend | 2.271 | 21.263 | 9.36× | 23.328 | 通过 max=0 |
| evenodd | 2.248 | 12.436 | 5.53× | 13.602 | 通过 max=0 |
| stroke | 2.634 | 11.269 | 4.28× | 11.768 | 通过 max=0 |
| rect-aa | 2.310 | 32.479 | — | 33.030 | 诊断：max 41>2 |
| gradient | 2.337 | 12.490 | 5.35× | 13.657 | 通过 max=1 |
| image | 2.319 | 12.827 | — | 14.150 | 诊断：max 253>0 |
| curve-aa | 2.339 | 44.426 | — | 48.431 | 诊断：max 44>32 |
| chinese | 2.321 | 1087.597 | — | 1162.200 | 诊断：max 173>32 |
| curve-changing | 2.212 | 46.820 | — | 52.606 | 诊断：max 44>32 |
| chinese-changing | 2.609 | 1065.476 | — | 1090.144 | 诊断：max 173>32 |

![GPU latency chart](../results/gpu-opt5/latency-1024x768.svg)

### 托管分配、进程内存、CPU 使用率

下表内存绝对值含 runtime、Vulkan 驱动映射与先前场景，不等于显存/渲染缓冲用量。CPU% 为采样批内进程总处理器时间/墙钟，单逻辑核归一（100% = 单逻辑核满载）。

| 场景 | 原 B/op | Simd B/op | 原 CPU% | Simd CPU% | 原 private MiB | Simd private MiB | 原 working set MiB | Simd working set MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| clear | 15 | 950 | 53.4 | 95.5 | 105.4 | 95.5 | 93.7 | 94.7 |
| rectangles | 17 | 25541 | 78.8 | 72.7 | 105.7 | 93.7 | 93.5 | 93.0 |
| blend | 16 | 25603 | 75.3 | 50.9 | 105.5 | 96.0 | 93.8 | 94.6 |
| evenodd | 15 | 1373 | 79.8 | 75.9 | 105.4 | 91.5 | 93.7 | 91.1 |
| stroke | 16 | 1363 | 76.5 | 92.7 | 105.4 | 87.6 | 93.7 | 86.2 |
| rect-aa | 16 | 1489 | 79.1 | 24.1 | 105.0 | 89.1 | 93.0 | 88.5 |
| gradient | 16 | 1349 | 75.0 | 76.2 | 106.3 | 88.2 | 93.8 | 87.5 |
| image | 120 | 1733 | 73.7 | 77.5 | 105.8 | 90.6 | 93.7 | 89.7 |
| curve-aa | 15 | 1513 | 80.5 | 33.0 | 105.7 | 91.0 | 93.5 | 89.3 |
| chinese | 655 | 3005 | 77.7 | 1.4 | 108.2 | 94.9 | 94.1 | 94.3 |
| curve-changing | 14 | 6203 | 76.1 | 30.4 | 100.2 | 86.3 | 93.0 | 83.8 |
| chinese-changing | 656 | 135731 | 77.1 | 1.5 | 108.3 | 95.8 | 94.1 | 94.6 |

完整 op/s、GC 次数、托管 retained、进程历史峰值、IQR 和 CV 在 CSV/JSON。


## 解读边界

- 每个‘op’是一整帧 GPU round-trip：绘制全部命令 + flush 提交 + fence 等待 + 主机回读拷贝。rectangles/blend 每帧 65 条命令（1 clear+64 draw），chinese 含 4 行文字，其余 1 项。几何/字体/纹理准备不在计时内；每帧的命令录制、提交、等待与回读在计时内。
- 双方目标像素格式 RGBA8888/Premul，同一 GPU 上各自独立初始化 Vulkan（原版用 graphics queue + GRContext 光栅后端；SimdSkia 用 compute queue + 自研 compute shader 管线）。
- 固定场景允许正常复用纹理/缓存（SimdSkia 纹理缓存 ≤32 个/64MiB LRU）；curve-changing/chinese-changing 场景每帧移动 1/16 像素、97 个位置循环，强制覆盖率/文字缓存持续未命中。变化场景像素 gate 检查 frame=0。
- 质量门槛以原版 GPU 输出为基准逐通道比较；AA/字体必须检查本轮相同尺寸的像素 gate；失败的时间保留为诊断数据。未提高容差，也未降低画质。
- B/op 是线程托管分配，不含 native malloc 与显存。private/working set 是整个进程，包含 runtime、Vulkan 驱动映射、native/字体缓存和先前场景，不能当显存或纯渲染缓冲内存公平排名；peak working set 是进程历史峰值。
- P95 是实际单帧延迟，包含 GC/调度/驱动抖动；耗时列是批均值中位数，两者定义不同。
- 帧时间由 CPU 计时（Stopwatch）测得，包含每帧回读同步；驱动内部排队可能引入噪声。样本不控制 OS power plan/Turbo/其他用户进程，不给所有硬件的优势承诺。

