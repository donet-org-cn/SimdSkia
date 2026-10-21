# 原版与 SimdSkia 性能对比：2026-10-12

实际执行：Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz；Microsoft Windows 10.0.26300；.NET 8.0.31 / X64，逻辑 CPU 12。原版包 SkiaSharp 3.119.4。
每种实现 3 个独立进程；每项每进程 7 批，目标每批 60 ms，预热至少 100 ms 且 3 帧，再校准。进程顺序/场景顺序打乱，串行执行。TieredCompilation=0，ReadyToRun=0，ServerGC=False。

主要结论：以 1024×768 为例，以下只列通过画质门槛的场景。表中倍数大于 1 表示 SimdSkia 更慢；未据清屏接近原版推断全引擎性能。


这次只比较同机 CPU 光栅和 PNG。未把 SimdSkia Vulkan 提交时间与原版 CPU 时间混用。标量/SIMD 切换控制显式清屏、连续像素填充及预乘 Alpha 合成；运行时 JIT 优化仍开启，抗锯齿质量不变。

原始数据：[JSON](../artifacts/performance/changing-2026-10-12/summary.json)、[CSV](../artifacts/performance/changing-2026-10-12/results.csv)、[配置](../artifacts/performance/changing-2026-10-12/run.json)。工作进程数据、逐帧 ticks、像素和日志保留在同目录。

## 1024×768：时间与质量

表内 ms 为多进程所有批均值的中位数；‘慢倍数’=Simd SIMD / 原版，低于 1 表示更快。质量未过或编码压缩策略不同，不给性能胜负结论。

| 场景 | 原版 ms | 标量 ms | SIMD ms | 慢倍数 | SIMD P95 ms | SIMD 托管 B/op | 质量/可比性 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| curve-changing | 0.219 | 2.432 | 2.245 | — | 2.564 | 1049352 | 诊断：max 63>32 |
| chinese-changing | 0.263 | 9.817 | 9.555 | — | 10.644 | 1134007 | 诊断：max 173>32 |

![CPU latency chart](../artifacts/performance/changing-2026-10-12/latency-1024x768.svg)

### 托管分配、进程内存和输出大小

下表 原/Simd 均为原版与 SIMD 进程；内存绝对值含 runtime/native cache，不等于渲染缓冲用量。

| 场景 | 原 B/op | Simd B/op | 原 private MiB | Simd private MiB | 原 working set MiB | Simd working set MiB | 原编码字节 | Simd 编码字节 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| curve-changing | 0 | 1049352 | 18.4 | 41.1 | 42.8 | 62.7 | 0 | 0 |
| chinese-changing | 640 | 1134007 | 18.9 | 60.3 | 44.4 | 82.8 | 0 | 0 |

完整 op/s、GC 次数、托管 retained、进程历史峰值、IQR 和 CV 在 CSV/JSON。


## 解读边界

- 每个‘op’是一整帧工作负载；rectangles/blend 各含 64 个矩形，chinese 含 4 行，其余 1 项。除 clear 和 codec 外，每帧含同样的清屏。对象/字体/图像准备、JSON、像素核查不在计时内；绘制产生的分配及实际 GC 在计时内。固定场景允许正常复用缓存；额外 curve-changing/chinese-changing 场景每帧移动 1/16 像素、97 个位置循环，超过 64 项缓存容量，强制覆盖率/文字缓存持续未命中。变化场景像素 gate 检查 frame=0；变化位置另外由冻结渲染器随机测试检查。
- CPU RGBA8888/Premul、连续 stride、同尺寸/画笔/命令/字体。PNG 解码共享原版生成的相同压缩输入，双方输出 RGBA8888/Unpremul。
- PNG 编码像素虽相同，原版压缩与 SimdSkia stored 块不同，必须同时看输出字节，不能据时间宣布压缩效率优势。
- AA/字体必须检查本轮相同尺寸的像素 gate；失败的时间保留为诊断数据。未提高容差，也未降低画质。
- B/op 是线程托管分配，不含 native malloc。private/working set 是整个进程，包含 runtime、native/font caches 和先前场景，不能当纯渲染缓冲区内存公平排名；peak working set 是进程历史峰值。完整分配、GC 和内存字段在 CSV/JSON。
- P95 是实际单帧延迟，包含 GC/调度抖动；耗时列是批均值中位数，两者定义不同。原始 ticks 也包含两次计时读取的微小共同开销。
- 样本不控制 OS power plan/Turbo/其他用户进程，不给全引擎或所有硬件的优势承诺。

