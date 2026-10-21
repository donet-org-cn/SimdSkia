# SimdSkia 优化实测：2026-10-21

后续针对 Avalonia 的定位文字/缓存/图片行优化见 [专项报告](AVALONIA-OPTIMIZATION.md)，含 API 使用清单与冷启动；本文保留较早同日的通用 CPU/GPU/OCR 运行数据和源码快照。

本轮已合入生产模块，重新测量优化前、优化后及原版。保持 16×16 AA、颜色/gamma、CRC/Adler、预乘 Alpha、采样和像素舍入；生产仍无第三方 NuGet/Skia 原生依赖。GPU 使用系统 Vulkan。

硬件：Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz / GTX 1050 Ti；.NET 8.0.32，Windows。渲染对照固定 SkiaSharp 3.119.4，OCR 单独固定真实模块使用的 4.153.1。两者不混作一个版本的兼容承诺。

CPU、GPU 分别比较。优化前后每实现 3 个独立进程，每场景每进程 5 批、目标每批 70 ms、预热至少 100 ms；相同字体 SHA、输入、尺寸和环境开关，任务串行。耗时为 15 个批均值的中位数。GPU 每帧包含录制、提交、fence 完成、设备到主机传输及回读拷贝。

## 实际改动

- CPU 路径：按相邻扫描线复用交点排序，遇到杂乱边时用有界回退；简单路径直接输出连续覆盖区间，减少大面积中间 mask；复杂 AA 文字用紧凑覆盖率数组和 AVX2 合成。缓存仍有 64 项/16 MiB 上限。
- PNG：SSE2/SSSE3 行预测与 RGB 展开、AVX2 Adler 校验；32 位 DEFLATE 补位及短距回溯；IDAT 一次聚合到可复用缓冲。保留标量路径、全部校验和 gamma/透明度处理。
- 解码输出：全进程最多 4 块、32 MiB 的空闲像素池；共享 PixelBuffer/画布通过引用计数延长所有权，到最后一次 Dispose 才可复用。活动图像、原始 DEFLATE 临时池和 GPU 资源另计。默认公共位图仍初始化为零，只有完全写入的解码输出借用未初始化空间。
- Vulkan：按闭合轮廓和 Y 范围建立 8×8 瓦片边索引，索引有 16 MiB 上限，超限保留全扫描；AA 每条子扫描线只求交一次，用独立整数计数器覆盖全部 16 个 X 样本；候选边过多时保留原始采样核。
- Vulkan 资源：DEVICE_LOCAL 渲染目标加 HOST_CACHED 优先的 coherent staging，使用 transfer/compute/host 屏障、初次设备清零与 fence 后回读；减少逐命令临时分配，修正离屏 dispatch 负范围。同步遵循 [Khronos 示例](https://docs.vulkan.org/guide/latest/synchronization_examples.html)。

## 1024×768：帧耗时

“本轮提速”只比较自身前后；画质未通过者保留诊断数据，不据此宣布胜过原版。

### CPU

| 场景 | Simd 优化前 ms | Simd 优化后 ms | 本轮提速 | 原版 ms | 当前画质 |
| --- | ---: | ---: | ---: | ---: | --- |
| curve-changing | 2.114 | 0.597 | 3.54× | 0.204 | 诊断 max=63>32 |
| chinese-changing | 9.031 | 4.233 | 2.13× | 0.237 | 诊断 max=173>32 |
| clear | 0.073 | 0.077 | 0.95× | 0.071 | 通过 max=0 |
| rectangles | 0.247 | 0.254 | 0.97× | 0.207 | 通过 max=0 |
| blend | 3.581 | 3.253 | 1.10× | 9.374 | 诊断 max=2>1 |
| evenodd | 0.151 | 0.159 | 0.94× | 0.139 | 通过 max=0 |
| stroke | 0.097 | 0.101 | 0.95× | 0.076 | 通过 max=0 |
| rect-aa | 0.209 | 0.208 | 1.01× | 0.200 | 通过 max=1 |
| gradient | 0.150 | 0.150 | 1.00× | 2.071 | 通过 max=0 |
| image | 0.917 | 0.914 | 1.00× | 2.955 | 诊断 max=253>0 |
| curve-aa | 0.172 | 0.173 | 0.99× | 0.201 | 诊断 max=63>32 |
| chinese | 0.412 | 0.162 | 2.55× | 0.241 | 诊断 max=173>32 |
| png-encode | 4.665 | 3.707 | 1.26× | 28.152 | 通过 max=0；压缩策略不同 |
| png-decode | 14.428 | 3.863 | 3.74× | 3.455 | 通过 max=0 |

### CPU：CPU 与内存

CPU ms/帧为进程累计用户+内核处理器时间/帧数；单核满载通常接近帧耗时，百分比满载并不能反映固定帧率下的节省。WS/private 含整个进程、runtime、驱动和先前场景；不是纯引擎内存或显存。托管分配不含原版 native malloc。

| 场景 | CPU ms/帧 前→后 / 原版 | 托管 KiB/帧 前→后 / 原版 | WS MiB 前→后 / 原版 | private MiB 前→后 / 原版 |
| --- | --- | --- | --- | --- |
| curve-changing | 2.159→0.586 / 0.208 | 1024.75→180.33 / 0.00 | 76.8→54.5 / 43.2 | 55.1→32.2 / 18.7 |
| chinese-changing | 8.967→4.438 / 0.239 | 1107.35→183.43 / 0.62 | 64.5→64.4 / 42.9 | 43.2→42.6 / 17.2 |
| clear | 0.072→0.077 / 0.072 | 0.00→0.00 / 0.00 | 53.0→52.5 / 42.1 | 31.5→31.0 / 16.7 |
| rectangles | 0.246→0.253 / 0.205 | 0.00→0.00 / 0.00 | 62.8→64.9 / 42.3 | 41.3→43.6 / 16.6 |
| blend | 3.795→3.276 / 9.505 | 0.00→0.00 / 0.00 | 49.0→50.5 / 43.0 | 27.6→29.0 / 17.7 |
| rect-aa | 0.206→0.205 / 0.205 | 0.00→0.00 / 0.00 | 63.8→64.0 / 42.1 | 42.4→42.9 / 16.7 |
| chinese | 0.406→0.163 / 0.238 | 0.00→0.00 / 0.62 | 63.7→62.8 / 43.1 | 42.2→41.2 / 17.9 |
| png-decode | 14.583→3.877 / 3.276 | 3213.32→21.11 / 0.29 | 64.7→65.2 / 42.8 | 43.4→43.5 / 20.5 |

### GPU Vulkan 全回程

| 场景 | Simd 优化前 ms | Simd 优化后 ms | 本轮提速 | 原版 ms | 当前画质 |
| --- | ---: | ---: | ---: | ---: | --- |
| clear | 10.352 | 0.765 | 13.52× | 2.309 | 通过 max=0 |
| rectangles | 12.959 | 1.456 | 8.90× | 2.053 | 通过 max=0 |
| blend | 23.475 | 3.533 | 6.64× | 2.048 | 通过 max=0 |
| evenodd | 12.293 | 1.247 | 9.86× | 2.028 | 通过 max=0 |
| stroke | 10.658 | 1.082 | 9.85× | 2.050 | 通过 max=0 |
| rect-aa | 31.601 | 1.195 | 26.45× | 2.060 | 诊断 max=41>2 |
| gradient | 12.587 | 1.203 | 10.46× | 2.078 | 通过 max=1 |
| image | 12.670 | 1.188 | 10.67× | 2.052 | 诊断 max=253>0 |
| curve-aa | 38.905 | 1.550 | 25.11× | 2.024 | 诊断 max=44>32 |
| chinese | 979.261 | 4.138 | 236.67× | 2.049 | 诊断 max=173>32 |
| curve-changing | 43.079 | 1.760 | 24.48× | 2.050 | 诊断 max=44>32 |
| chinese-changing | 970.749 | 4.945 | 196.30× | 2.086 | 诊断 max=173>32 |

### GPU Vulkan 全回程：CPU 与内存

CPU ms/帧为进程累计用户+内核处理器时间/帧数；单核满载通常接近帧耗时，百分比满载并不能反映固定帧率下的节省。WS/private 含整个进程、runtime、驱动和先前场景；不是纯引擎内存或显存。托管分配不含原版 native malloc。

| 场景 | CPU ms/帧 前→后 / 原版 | 托管 KiB/帧 前→后 / 原版 | WS MiB 前→后 / 原版 | private MiB 前→后 / 原版 |
| --- | --- | --- | --- | --- |
| clear | 9.226→0.260 / 1.450 | 0.86→0.64 / 0.00 | 128.4→127.9 / 106.8 | 94.3→97.1 / 109.3 |
| rectangles | 10.069→0.449 / 1.467 | 24.87→0.64 / 0.00 | 123.2→126.3 / 106.7 | 88.8→91.9 / 109.4 |
| blend | 10.000→0.417 / 1.530 | 24.88→0.65 / 0.00 | 121.2→122.8 / 107.2 | 86.7→95.1 / 109.0 |
| rect-aa | 6.250→0.345 / 1.497 | 1.26→0.64 / 0.00 | 117.1→126.2 / 106.6 | 82.7→97.2 / 108.0 |
| chinese | 8.333→0.551 / 1.487 | 2.59→0.65 / 0.62 | 122.0→137.7 / 107.5 | 88.1→108.9 / 110.0 |
| curve-changing | 9.375→0.525 / 1.317 | 5.49→267.12 / 0.00 | 109.2→129.8 / 106.2 | 75.2→100.0 / 104.2 |
| chinese-changing | 11.458→1.116 / 1.594 | 129.56→532.06 / 0.62 | 125.7→151.4 / 107.1 | 92.1→120.7 / 109.6 |

## 256×256 GPU 中文

小画布仍比原版慢，不能把大画布的比例套用到所有尺寸。完整两尺寸的场景在 GPU 全表。

| 场景 | 优化前 ms | 优化后 ms | 原版 ms | 画质 |
| --- | ---: | ---: | ---: | --- |
| chinese | 264.301 | 2.229 | 0.228 | 既有误差，诊断数据 |
| chinese-changing | 264.490 | 3.071 | 0.209 | 既有误差，诊断数据 |

## 资源取舍

基础 CPU 的 clear/rectangles/evenodd/stroke 等没有改动热点，本次约 ±6% 变化不作为稳定提速。GPU 动态中文字虽然从 970.75 ms 降到 4.95 ms，但新瓦片索引使每帧托管分配从 129.6 KiB 增至 532.1 KiB，进程 WS 从 125.7 MiB 增至 151.4 MiB。该场景需要更多内存，不能据耗时宣布全面省内存。
像素复用减少重复分配，同时保留部分空闲数组；PNG 的工作集也未在所有尺寸下降。缓存预算约束空闲数组，不约束活动图像或整个进程。

## 真实 OCR 输入链路：SkiaSharp 4.153.1

只读链接 Qcxt.OCR.SkiaSharp 和真实 CPU OCR/模型，未修改目标仓库。每条件 3 个独立进程 × 5 批；500×500 是真实样本，1024/2048 仅为该样本放大的解码测试。RunBytes 的 Simd 路径仍是 PNG 适配，完整原版扩展仍缺 SKStream/SKPixmap 等 API；JPEG 仍未实现。

| 条件 | 原版 ms | Simd ms | Simd/原版 | CPU ms/张 原→Simd | WS MiB 原→Simd | 全线程托管 MiB/张 原→Simd |
| --- | ---: | ---: | ---: | --- | --- | --- |
| decode 500² | 2.194 | 2.879 | 1.31× | 2.27→2.92 | 36.3→40.3 | 0.002→0.134 |
| decode 1024² | 4.620 | 5.306 | 1.15× | 4.76→5.10 | 36.5→43.7 | 0.002→0.155 |
| decode 2048² | 10.941 | 10.040 | 0.92× | 10.94→10.18 | 36.7→63.7 | 0.002→0.155 |
| bitmap-ocr 500² | 56.953 | 58.635 | 1.03× | 478.12→446.88 | 104.2→104.2 | 1.644→1.667 |
| bytes-ocr 500² | 59.846 | 60.344 | 1.01× | 450.52→433.33 | 103.4→104.4 | 1.657→1.774 |

OCR 推理本身使用同一核心。raw-ocr 控制组为 58.35→60.58 ms，约 3.8% 波动；本样本整链差异约 1% 不能证明普遍持平或显著优劣。RunBitmap 只传递像素。OCR CPU ms 合计全部线程，可高于墙钟；不能把推理的 CPU 时间差异归功于绘图引擎。仍需关注透明图片的 premul/straight 输入语义差异。

## 验证与仍未解决的差距

- CPU/编解码回归：硬件 SIMD 6981 项、完整回退 6981 项通过，含逐字节比较、异常与生命周期检查。包含 213 个独立 zlib、471 个独立 PNG fixture、全部过滤器和 SIMD 边界。
- Vulkan：312 项检查通过，其中 309 组优化内核对原始全边/16×16 核逐字节一致、3 项索引边界检查；另通过纹理变更、队列线程、Dispose、批次、初始化和 stride 回归。
- 本轮优化前后固定 frame=0：CPU 84 帧、GPU 72 帧逐字节一致，覆盖 3 进程。动态位置另由随机/亚像素回归检查。
- 原版画质 corpus：108/120 通过；失败场景及差异图继续保留。行为探针 23/25 字符串完全一致，详见下述字体微差。
- 字体行为单独复核：默认 SimHei 25/25 完全一致；固定 Zen 字体 23/25 完全一致，Advance/Outline.Bounds 有约 0.000004 以内浮点字符串微差。保存的优化前程序集已复现同样微差，两字体均为优化前后 25/25 相同；未放宽测试阈值。
- 公共 API 仍为 403/3235 匹配，2832 缺失，0 个额外/不同签名；本轮不扩大版本或二进制兼容承诺。
- 曲线/字体仍有既有原版画质差异，部分图片/混合场景仍不通过严格门槛；压缩 PNG 编码、JPEG/WebP、复杂字体与应用完整集成仍有差距。Linux Vulkan 和其他 GPU/ARM 硬件未实测。

## 复现和证据

[CPU 全表](PERFORMANCE.md) · [GPU 全表](PERFORMANCE-GPU.md) · [优化前 CPU](../artifacts/performance/before-2026-10-21/REPORT.md) · [优化前 GPU](../artifacts/performance-gpu/before-2026-10-21/REPORT.md) · [对比 JSON](../artifacts/optimization-2026-10-21/comparison.json)

```powershell
powershell -NoProfile -File tools/verify.ps1 -Gpu
$env:DOTNET_EnableHWIntrinsic="0"
dotnet tests/Optimization/bin/Release/net8.0/Optimization.dll baseline/fonts/ZenOldMincho-Regular.ttf
Remove-Item Env:DOTNET_EnableHWIntrinsic
# 网络不可用时，已有基准包可用 NuGetAudit=false 环境变量离线构建；不改变产品依赖。
powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-new-run -Scenes clear,rectangles,blend,evenodd,stroke,rect-aa,gradient,image,curve-aa,chinese,png-encode,png-decode,curve-changing,chinese-changing -Samples 5 -SampleMilliseconds 70
powershell -NoProfile -File tools/run-gpu-benchmarks.ps1 -Output artifacts/performance-gpu/my-new-run -Samples 5 -SampleMilliseconds 70
powershell -NoProfile -File tools/run-ocr-benchmarks.ps1 -Output artifacts/ocr/my-new-run
python tools/report-ocr-benchmarks.py artifacts/ocr/my-new-run --data-only
python tools/report-optimization.py --cpu artifacts/performance/my-new-run --gpu artifacts/performance-gpu/my-new-run --ocr artifacts/ocr/my-new-run
```

测量前后均保存源文件 SHA，完成时检查未变化。原始 JSON/CSV、逐帧 ticks、像素、差异图、分配/GC/CPU/内存和日志保存在 artifacts。PNG 分段剖析工具为 tools/profile-codec.py；它只给源码副本计时，正式比较使用未插桩产品。
