> 2026-10-21 最新交付：[测试报告](TEST-REPORT-2026-10-21.md)、[CPU/内存/滤镜性能](PERFORMANCE-FINAL-2026-10-21.md)、[Vulkan GPU 对照](PERFORMANCE-GPU-FINAL-2026-10-21.md)、[剩余缺口](COMPLETION-TRACKER.md)。完整 Avalonia 替换仍未通过验收。此前 [Avalonia 调用回放](AVALONIA-OPTIMIZATION.md) 与 [通用优化报告](OPTIMIZATION-2026-10-21.md) 保留为历史，下文仍是 2026-10-12 的测量。

# SimdSkia 性能与继续投入评估：2026-10-12

本次重新构建并实测当前代码。CPU：14 场景×2 尺寸×3 实现×3 进程×7 批，共 1764 批；GPU：12 场景×2 尺寸×2 实现×3 进程×7 批，共 1008 批。每批目标 100 ms，实际较慢帧允许超过目标。另做 HOST_CACHED 隔离实验 504 批，均串行运行；未用旧报告替代本轮测量。

硬件：Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz、Native Skia GPU dispatch (Vulkan, NVIDIA GeForce GTX 1050 Ti)；.NET 8.0.31；Microsoft Windows 10.0.26300。原版 SkiaSharp 3.119.4；同一固定字体 SHA、图像、绘图代码、质量门槛。CPU 与 GPU 各自比较，GPU 每帧包括命令录制、提交、等待和主机回读。

## 如何读这些数值

耗时是 21 个采样批均值的中位数，P95 在完整报告。CPU ms/帧是累计进程用户+内核处理器时间除以累计帧数，包括 GC/进程内驱动线程；满速 CPU% 是累计 CPU 时间/累计墙钟，100% 表示一个逻辑核持续忙。本机 12 逻辑核，机器总占用等于该数除以 12。它不是固定帧率应用的实际占用。[Microsoft Process.TotalProcessorTime](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.totalprocessortime?view=net-8.0)。

WS 是当前场景预热后的整个进程工作集，private 是整个进程私有提交，均包含运行时、驱动与此前场景；不能当成纯引擎缓冲区、峰值内存或显存。托管 B/帧是计时循环线程分配，不包含 native malloc、其他线程分配和 GPU 显存。完整 CSV 保留 private/WS/retained/peak/GC。

## CPU：1024×768

以下每个箭头为原版→当前 Simd SIMD。诊断场景不作超过原版的性能结论；画质门槛未放宽。

| 场景 | 上轮 Simd ms | 原版 ms | 当前 Simd ms | CPU ms/帧 原→Simd | WS MiB 原→Simd | private MiB 原→Simd | 托管 KiB/帧 原→Simd | 质量 |
| --- | ---: | ---: | ---: | --- | --- | --- | --- | --- |
| clear | 0.081 | 0.076 | 0.078 | 0.075→0.080 | 45.3→50.7 | 19.4→28.6 | 0.000→0.000 | 通过 max=0 |
| rectangles | 0.278 | 0.219 | 0.277 | 0.216→0.271 | 45.5→63.1 | 19.7→40.8 | 0.000→0.000 | 通过 max=0 |
| evenodd | 0.165 | 0.149 | 0.164 | 0.149→0.168 | 45.6→63.6 | 19.8→41.7 | 0.000→0.000 | 通过 max=0 |
| stroke | 0.106 | 0.081 | 0.105 | 0.082→0.104 | 45.0→63.1 | 19.2→40.8 | 0.000→0.000 | 通过 max=0 |
| rect-aa | 0.223 | 0.213 | 0.225 | 0.211→0.225 | 44.9→64.3 | 19.1→42.1 | 0.000→0.000 | 通过 max=1 |
| gradient | 0.166 | 2.230 | 0.159 | 2.281→0.163 | 43.8→63.9 | 19.1→41.8 | 0.000→0.000 | 通过 max=0 |
| blend | 3.539 | 10.322 | 3.519 | 10.491→3.660 | 45.2→48.4 | 19.3→26.4 | 0.000→0.001 | 诊断 max=2>1 |
| image | 0.959 | 3.158 | 0.964 | 3.270→0.963 | 46.1→64.7 | 20.4→42.4 | 0.102→0.000 | 诊断 max=253>0 |
| curve-aa | 0.178 | 0.226 | 0.183 | 0.225→0.185 | 40.1→43.8 | 14.2→21.8 | 0.000→0.000 | 诊断 max=63>32 |
| chinese | 0.427 | 0.259 | 0.439 | 0.256→0.440 | 45.4→63.5 | 19.7→41.2 | 0.625→0.000 | 诊断 max=173>32 |
| curve-changing | 2.245 | 0.226 | 2.278 | 0.229→2.251 | 43.8→77.8 | 19.0→55.5 | 0.000→1024.768 | 诊断 max=63>32 |
| chinese-changing | 9.555 | 0.271 | 9.590 | 0.270→9.835 | 45.9→63.1 | 19.8→41.0 | 0.625→1107.429 | 诊断 max=173>32 |
| png-encode | 4.550 | 29.469 | 4.868 | 30.235→5.234 | 45.5→43.5 | 23.0→23.6 | 0.312→9218.922 | 诊断：stored 编码压缩不同 |
| png-decode | 15.572 | 3.756 | 15.833 | 3.803→15.625 | 43.8→64.5 | 21.9→42.4 | 0.289→3213.337 | 通过 max=0 |

CPU 核心文件与上轮测量 SHA 一致，本轮速度变化主要是执行环境波动；26 个已有 scene-size 的 current/previous canonical 帧字节一致。没有将 GPU 改动解释成 CPU 算法提速。旧数据未采 CPU ms/帧，所以不伪造历史 CPU 使用率；旧/new 内存均可在对应原始 JSON 核查。

## GPU：1024×768

当前列为现有生产实现，实验列是独立项目，只改变渲染目标分配优先 HOST_CACHED，仍有相同 fence 和回读。其他 shader/字体/AA/混合算法不变；原版一列来自本轮原版 Vulkan。原版使用 graphics queue/GRContext，自研使用 compute；不是纯 GPU timestamp 时间。

| 场景 | 上次 AI 报告 Simd ms | 本轮原版 ms | 当前 Simd ms | 隔离实验 ms | 实验相对当前加速 | 实验/原版耗时 | 质量 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| clear | 10.824 | 3.488 | 11.076 | 1.230 | 9.00× | 0.35× | 通过 max=0 |
| rectangles | 13.155 | 3.463 | 12.919 | 3.187 | 4.05× | 0.92× | 通过 max=0 |
| blend | 21.263 | 2.366 | 21.886 | 12.383 | 1.77× | 5.23× | 通过 max=0 |
| evenodd | 12.436 | 2.682 | 12.382 | 2.707 | 4.57× | 1.01× | 通过 max=0 |
| stroke | 11.269 | 2.712 | 11.129 | 1.511 | 7.37× | 0.56× | 通过 max=0 |
| gradient | 12.490 | 2.982 | 12.277 | 3.122 | 3.93× | 1.05× | 通过 max=1 |
| image | 12.827 | 2.652 | 12.790 | 2.909 | 4.40× | — | 诊断 max=253>0 |
| rect-aa | 32.479 | 2.439 | 32.561 | 23.560 | 1.38× | — | 诊断 max=41>2 |
| curve-aa | 44.426 | 2.273 | 43.092 | 33.091 | 1.30× | — | 诊断 max=44>32 |
| chinese | 1087.597 | 2.505 | 899.290 | 951.100 | 0.95× | — | 诊断 max=173>32 |
| curve-changing | 46.820 | 2.336 | 40.730 | 34.426 | 1.18× | — | 诊断 max=44>32 |
| chinese-changing | 1065.476 | 2.556 | 877.127 | 909.506 | 0.96× | — | 诊断 max=173>32 |

GPU 资源对照：

| 场景 | CPU ms/帧 原/当前/实验 | WS MiB 原/当前/实验 | private MiB 原/当前/实验 | 托管 KiB/帧 原/当前/实验 |
| --- | --- | --- | --- | --- |
| clear | 1.786/10.119/0.338 | 108.2/122.8/121.6 | 110.0/88.2/86.9 | 0.000/0.858/0.852 |
| rectangles | 1.652/10.536/0.493 | 107.7/123.1/122.1 | 109.6/88.5/87.2 | 0.000/24.860/24.854 |
| blend | 1.881/8.929/0.507 | 107.2/116.6/114.5 | 103.1/82.7/80.4 | 0.000/24.864/24.861 |
| curve-aa | 1.599/10.324/0.223 | 107.4/121.6/119.8 | 107.6/87.3/85.1 | 0.000/1.275/1.270 |
| chinese | 1.734/11.905/0.744 | 108.1/122.4/122.3 | 109.8/87.7/87.6 | 0.625/2.546/2.546 |
| chinese-changing | 1.961/12.649/5.952 | 108.8/128.0/125.2 | 110.7/93.5/90.7 | 0.625/132.162/132.162 |

隔离实验的全部 24 个 scene-size、3 进程（72 个保存帧）与当前 GPU 保存帧逐字节一致，原版质量 gate 状态保持不变。实验回归检查矩形、路径/AA、渐变、描边、中英文、纹理版本、缓存淘汰、128 命令批次、线程和 Dispose，通过。生产 GPU 中另修复 descriptor offset 后 range 超出实际缓冲区的 Vulkan 合法性问题；此修复已纳入“当前”代码与哈希。

## 瓶颈与还能优化的地方

实际 GPU 剖析显示清屏 Record 约 0.001 ms、fence 完成（含 submit）约 1 ms，整个 Flush 约 10.6 ms。渲染目标选中 memory type 8，flags=6（HOST_VISIBLE|HOST_COHERENT、非 HOST_CACHED/DEVICE_LOCAL）；同设备还有 type 9，flags=14，支持 HOST_CACHED。隔离实验使用后者，证明 CPU 回读拷贝部分存在可兑现优化空间。这里的阶段差值不是纯 GPU timestamp，不将所有余量归因于一个硬件事件。

该分配仍是主机内存渲染目标。下一步可用 DEVICE_LOCAL 渲染目标 + 缓存的 staging readback，需正确 transfer barriers 与生命周期；是否更快要再实测，未承诺速度倍数。离散 GPU 的主机/设备内存传输及 staging 设计见 [Khronos Memory Allocation](https://docs.vulkan.org/guide/latest/memory_allocation.html)。

更严重的是 GPU 文字：四行共有约 7156 条扁平边。现 shader 对像素扫描整条路径边，边界像素再执行 16×16 次全路径测试；字体布局/命令录制很快，主要等待落在 GPU。HOST_CACHED 解决回读成本，无法根治边扫描复杂度。值得验证按扫描带/瓦片分配相关边、GPU 字形覆盖率图集与纹理批量文字；不能靠关闭 AA 或直接增大缓存冒充解决。

GPU AA 矩形还对内部像素执行完整 16×16 采样；轴对齐矩形可以用与原网格一致的分离边缘覆盖计算，CPU 已有数学实现可作为参考。这也是值得独立验证的优化，尚未在本轮实现或计入实验收益。

CPU 动态路径约每帧分配 1 MiB mask/spans，动态中英文约 1.08 MiB；97 个移动位置超出 64 项缓存。可流式输出 spans、按行复用覆盖率缓冲、按 glyph/亚像素相位缓存，避免整串重建。需要覆盖旋转/裁剪/变更和资源上限的验证，不是只增加缓存容量。PNG 解码仍分配输出图像并有 Paeth/DEFLATE 开销；可继续优化，但它不是当前 GPU 文字瓶颈。

## 继续还是停止

如果目标是近期给真实 Avalonia 应用提供完整的 SkiaSharp 替代，我建议暂停生产替换投入。当前只有 403/3235 基线公共签名匹配，2832 缺失；复杂字体/色彩/native handle 和二进制加载仍有阻碍，且曲线、文字和某些图片/混合质量尚未达标。继续逐个微调性能不能补齐这些结构性问题。

如果纯 C# 是核心价值、应用仅需受控的 CPU 离屏绘制子集，基础 CPU 性能已经足以保留这条路线；继续应集中在真实目标应用与动态文字/内存分配。GPU 简单场景已有实验证明可优化，不建议仅因当前清屏回读慢就放弃整个项目；但复杂文字现有算法需要专项重设计，建议先用有限试验验证瓦片/字形图集，再决定是否扩展 GPU。

以上是根据当前实现与实测作出的工程判断，不代表完整应用性能。未测实际窗口 swapchain、完整 Avalonia 帧、Linux GPU、显存常驻用量、能耗或其他硬件；private/WS 不能拿来宣称更省显存。

## 验证与复现

当前 Release 构建、36 基础测试、14 功能测试、5267 项冻结/fixture 检查、Windows Vulkan 回归通过。GLSL 重新编译所得 SPIR-V SHA 与产品嵌入文件一致。原版 120 场景复测仍为 108 通过、12 曲线/文字失败，25 个行为探针一致；原版压缩 PNG fixture 解码测试通过。性能场景仍保留失败画质 gate，见具体表。

[CPU 完整报告](PERFORMANCE.md) · [GPU 完整报告](PERFORMANCE-GPU.md) · [上轮 CPU](OPTIMIZATION.md) · [其他 AI 的 GPU 报告快照](PERFORMANCE-GPU-AI-SNAPSHOT.md)

```powershell
powershell -NoProfile -File tools/verify.ps1 -Gpu
powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-review -Scenes clear,rectangles,blend,evenodd,stroke,rect-aa,gradient,image,curve-aa,chinese,png-encode,png-decode,curve-changing,chinese-changing -SampleMilliseconds 100
powershell -NoProfile -File tools/run-gpu-benchmarks.ps1 -Output artifacts/performance-gpu/my-review -SampleMilliseconds 100
powershell -NoProfile -File tools/run-gpu-host-cached-experiment.ps1 -Baseline artifacts/performance-gpu/my-review -Output artifacts/performance-gpu/my-cached-review
python tools/report-reassessment.py --cpu artifacts/performance/my-review --gpu artifacts/performance-gpu/my-review --experiment artifacts/performance-gpu/my-cached-review
```

数据与日志分别在 artifacts/performance/reassessment-2026-10-12、artifacts/performance-gpu/reassessment-2026-10-12、artifacts/performance-gpu/host-cached-experiment-2026-10-12；GPU 阶段/内存类型诊断在 artifacts/reassessment/gpu-diagnostics.json。未覆盖原有运行证据；实验不在生产引用图或解决方案中。
