# Vulkan 检查与优化：2026-10-22

真实 Avalonia 控件窗口，Vulkan P50 从 **22.250 ms 降至 2.316 ms**，自身加速 **9.61×**。同场景 CPU **2.067 ms**、Direct2D **1.552 ms**；Vulkan 仍比 CPU 多耗时约 **12%**。CPU 占用与 Private 没有随帧时间同比下降，完整兼容性仍未通过。

本轮是仓库源码优化，已发布的 preview.4 产物不包含这些改动。优化前仓库提交为 `3b32f46527cded46a1fc196178dcd9ec1ebe0f78`；优化后以证据中的源文件、SPIR-V 和运行 DLL 的 SHA-256 标识。

## 真实窗口：CPU、GPU、内存

i7-8700K / 12 逻辑处理器、GTX 1050 Ti、Windows 10.0.26300 x64。共享控件场景、固定 ZCOOL 字体、1000×720、**100% DPI**，.NET 10 JIT；每种后端 3 个独立进程，轮转顺序，预热 2 秒，正式测量至少 3 秒且 60 帧。以下为三个进程指标的中位数。所有后端均为 SimdSkia；原版 SkiaSharp 的独立 GPU 对照在后文。

| 后端 | P50 ms | P95 ms | 机器 CPU% | CPU ms/帧 | 峰值工作集 MiB | 峰值 Private MiB | 托管 KiB/帧 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| CPU 优化前 | 2.073 | 2.890 | 3.47 | 12.63 | 112.1 | 57.0 | 27.40 |
| Direct2D 优化前 | 1.654 | 2.185 | 4.45 | 16.26 | 137.1 | 108.6 | 22.61 |
| Vulkan 优化前 | 22.250 | 23.032 | 4.49 | 14.62 | 156.7 | 137.4 | 23.15 |
| CPU 优化后 | 2.067 | 2.768 | 3.45 | 12.38 | 111.9 | 56.8 | 27.66 |
| Direct2D 优化后 | 1.552 | 2.016 | 4.26 | 15.78 | 137.3 | 107.6 | 21.61 |
| Vulkan 优化后 | 2.316 | 2.704 | 3.51 | 12.05 | 155.1 | 138.7 | 24.10 |

窗口放在屏幕外，计时覆盖绘制和呈现服务，**不是显示器 FPS**。Vulkan 等待 fence、回读完整帧，再交给 OS framebuffer；Direct2D 等待 GPU event 后原生 DXGI 呈现。三轮 GPU 都为 0 条不支持命令的 CPU 回退；文字图集首次生成及分组透明度离屏层仍由 C# CPU 处理，后续由 GPU 合成。

机器 CPU% 包含整个进程的 UI、渲染和运行时线程，100% 表示 12 核逻辑容量全满；单核归一数字和每轮数据在证据中。CPU ms/帧用进程总 CPU 时间除以计时帧数。窗口工作集/Private 在计时区间采样，可能漏掉瞬时尖峰；它们不是显存。分配统计全部托管线程，包含 UI 布局。CPU 和 Direct2D 的绘图算法未改，独立重复测量仍存在数个百分点漂移。

最终 Vulkan 机器 CPU 占用 4.49%→3.51%，Private 137.4→138.7 MiB、分配 23.15→24.10 KiB/帧。CPU 占用在这一轮下降，但此前一次优化后复测为 4.67%（P50 2.295 ms），不能据此认定稳定的 CPU 节省；内存没有明显下降。这两轮共 18 个优化后进程均保存在证据中，前一轮之后仅补齐 Opaque 接受条件与诊断开关的批次一致性。

## 为什么慢，以及实际改动

第一次时间戳诊断中，186×51 像素、348 条边的描边占 GPU 绘制 ticks 的 43.3%；GPU 回读占绘制加回读 ticks 的约 1.4%。该诊断版本已经包含独立的图片 AA 优化，不能当作未修改基线。只读时钟快照显示 GPU 处于 P0，频率由驱动按负载调整。

- 高边数路径：原 8 位绕数内核在 128 条候选边时回到逐像素 256 次完整扫描。增加 16 位打包绕数，超过安全范围仍使用不受限扫描；8 位/16 位均保留原来 16×16 AA 样本和整数混合。32764/32768 边、正反绕向和填充规则加入对照。
- 保守边分块：排除射线右侧的边，合并整块区域恒定的绕数贡献；GPU 工作组识别远离边界的整块内部/外部区域。极端坐标保留通用路径。C# 执行几何分块，像素覆盖率仍由 GPU 计算。
- GPU 覆盖率缓存：以不可变路径、完整变换、细分容差、矩形 clip 和 AA 为 key，第二次相同使用才生成；颜色、渐变、复合模式和额外 coverage clip 每次重新合成。动态路径持续直接绘制，绘制顺序保持一致。
- 缓存限 64 项、32 MiB 逻辑预算，含 mask 数据和估算保留几何字节。当前批次引用的 mask 在 fence 完成前不会淘汰，预算不足时继续直接 GPU 光栅化。驱动分配对齐、对象开销不计入该预算；这不是进程显存占用测量。
- 专用管线与输入：按命令种类特化着色器，绘制参数放入 uniform buffer；边和参数统一上传显存，避免内核循环读取主机映射内存。图片轴对齐 AA 横纵分离，保留采样位置。
- BGRA 输出：Vulkan 直接生成 Avalonia framebuffer 的 BGRA，保留 PMA/stride，移除每帧 RGBA 中间缓冲和逐像素通道交换。Opaque 目标保留旧窗口通过 PMA staging 的物理存储语义，Src/Clear 可写非不透明 alpha，新增两种格式的精确回归；Straight 目标仍明确拒绝。
- 覆盖与同步：参数验证后跳过数学上无贡献的透明 SrcOver；仅在能够证明新命令覆盖整张目标时删除之前的排队绘制。按保守绘制包围盒判断依赖，重叠才加输出同步，无依赖区域可以并行执行。

GPU 时间戳诊断显式开启，会把绘制串行化，**不纳入上面的性能表**。tick 没有直接当作纳秒或 CPU 占用。默认诊断关闭；可用示例的 `--vulkan-profile` 输出逐条命令和阶段数据。

## 同机原版 Vulkan 对照

另用 .NET 8.0.32，原版 SkiaSharp 3.119.4 和 SimdSkia 都绘制、提交、等待实际完成并回读整帧；3 个进程 × 7 批、每批目标 60 ms、预热至少 100 ms / 3 帧，关闭 tiered/R2R/server GC。两个尺寸各 12 场景。该结果不与上面的窗口 CPU 时间混算。表中 ms 为批均值中位数；自身加速只比较冻结前后结果。

| 尺寸 | 场景 | 优化前 ms | 优化后 ms | 自身加速 | 本轮原版 GPU ms | 原版画质门槛 |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| 256×256 | clear | 0.136 | 0.137 | 1.00× | 0.194 | 通过 max=0 |
| 256×256 | rectangles | 0.695 | 0.218 | 3.19× | 0.245 | 通过 max=0 |
| 256×256 | blend | 0.779 | 0.340 | 2.29× | 0.233 | 通过 max=0 |
| 256×256 | evenodd | 0.177 | 0.146 | 1.22× | 0.212 | 通过 max=0 |
| 256×256 | stroke | 0.178 | 0.147 | 1.21× | 0.204 | 通过 max=0 |
| 256×256 | rect-aa | 0.177 | 0.152 | 1.16× | 0.208 | 失败 41>2 |
| 256×256 | gradient | 0.175 | 0.147 | 1.19× | 0.211 | 通过 max=1 |
| 256×256 | image | 0.171 | 0.151 | 1.13× | 0.203 | 失败 249>0 |
| 256×256 | curve-aa | 0.308 | 0.146 | 2.11× | 0.215 | 失败 45>32 |
| 256×256 | chinese | 2.191 | 0.197 | 11.14× | 0.216 | 失败 173>32 |
| 256×256 | curve-changing | 0.397 | 0.328 | 1.21× | 0.195 | 失败 45>32 |
| 256×256 | chinese-changing | 3.040 | 2.175 | 1.40× | 0.216 | 失败 173>32 |
| 1024×768 | clear | 0.762 | 0.734 | 1.04× | 2.160 | 通过 max=0 |
| 1024×768 | rectangles | 1.515 | 0.951 | 1.59× | 2.167 | 通过 max=0 |
| 1024×768 | blend | 3.718 | 1.902 | 1.95× | 2.167 | 通过 max=0 |
| 1024×768 | evenodd | 1.248 | 0.852 | 1.46× | 2.105 | 通过 max=0 |
| 1024×768 | stroke | 1.065 | 0.793 | 1.34× | 2.229 | 通过 max=0 |
| 1024×768 | rect-aa | 1.193 | 0.882 | 1.35× | 2.090 | 失败 41>2 |
| 1024×768 | gradient | 1.209 | 0.897 | 1.35× | 2.252 | 通过 max=1 |
| 1024×768 | image | 1.210 | 0.893 | 1.36× | 2.259 | 失败 253>0 |
| 1024×768 | curve-aa | 1.557 | 0.825 | 1.89× | 2.206 | 失败 44>32 |
| 1024×768 | chinese | 4.093 | 0.810 | 5.05× | 2.136 | 失败 173>32 |
| 1024×768 | curve-changing | 1.831 | 1.491 | 1.23× | 2.170 | 失败 44>32 |
| 1024×768 | chinese-changing | 4.928 | 3.880 | 1.27× | 2.156 | 失败 173>32 |

**原版 GPU 画质仍为 12/24 通过。** 失败场景的速度只供诊断，不能判为超过原版。尤其持续变化的中文仍慢于原版，静态路径的缓存收益不能套用到所有动画。全部 CPU ms/op、机器/单核 CPU%、工作集、Private、保留堆和分配见[逐项报告](PERFORMANCE-VULKAN-ROUND3-DETAILS-2026-10-22.md)及 [CSV](evidence/vulkan-round3-after-2026-10-22.csv)。

微基准分配只统计调用线程，窗口分配统计所有线程；原生分配不在托管计数内。微基准内存是完整进程的绝对快照，峰值可能包括先前场景和驱动；不能解读为单个渲染面的内存。

## 测试结果与剩余工作

- 540 项 GPU 检查通过：531 组逐字节对照、3 个分块边界、4 个缓存/预算与 BGRA 检查、1 个时间戳检查、1 个不支持命令拒绝。参考为不受限 GPU 扫描；另验证全部 144 张冻结前后 managed 帧逐字节相同、144 张原版帧稳定。
- 优化前后实际窗口 `presented.png` 及离屏 `window.png` SHA-256 相同；覆盖首帧后 resize、中文、路径、图片、输入事件和呈现。
- Vulkan 590 采样检查、Avalonia GPU 12174、Direct2D 344、字形合批 60、字体发现 10 项及失败帧清理通过。基础、图片 484、CPU 精确对照 7845、文字 595、控件 8147、GPU 文字 599 与 25 个原版行为探针通过。
- Windows Native AOT 实际运行 CPU/Direct2D/Vulkan 三个完整控件窗口和 Vulkan 时间戳模式，确认实际呈现、resize、`NativeAot=True`，没有加载 Skia/HarfBuzz/coreclr/clrjit。时间表仍为 JIT，不混入 AOT 数据。
- 优化源码提交 `2257b85dcec8b5ca98271eb4a63d7e573c43422c` 的三平台[托管/标量 CI](https://github.com/hnlyf168/SimdSkia/actions/runs/39720596815)、[实际桌面 JIT/AOT/包消费 CI](https://github.com/hnlyf168/SimdSkia/actions/runs/39720596809)及 [Windows 双后端 AOT](https://github.com/hnlyf168/SimdSkia/actions/runs/39720596787)全部通过；已下载核对 [9 条实际窗口记录](evidence/vulkan-round3-ci-2026-10-22.json)。云端窗口运行使用 CPU，不是物理 Linux Vulkan 验证。
- 完整 `verify.ps1 -Reference -Gpu` 返回 2：保留原版画质失败。SK* 仍为 425/3235 匹配、2810 缺失、0 额外/不同；旧图像、完整 UI、字体和高级画刷的既有差异继续记录，不承诺二进制兼容或完整 Avalonia 替换。
- Vulkan 仍通过 framebuffer 回读呈现，128 条命令自动批次仍同步；此次窗口约每帧一个批次。原生 Vulkan swapchain、物理 Linux GPU、macOS GPU/Metal 和更复杂动态路径优化仍待实现/验证。

这台机器的当前控件场景，CPU 的内存更低，Windows Direct2D 的绘制时间最低；Vulkan 的严重延迟瓶颈已消除，但仍有固定提交、回读与宿主成本。

## 构建与复现

```powershell
dotnet build SimdSkia.sln -c Release
powershell -File tools/verify.ps1 -Reference -Gpu
python tools/run-desktop-benchmarks.py --output artifacts/my-window-run --runs 3
powershell -File tools/run-gpu-benchmarks.ps1 -Output artifacts/my-gpu-run -Report artifacts/my-gpu-report.md -Forks 3 -Samples 7 -SampleMilliseconds 60 -WarmupMilliseconds 100
dotnet publish examples/CrossPlatform -c Release -r win-x64 -p:PublishAot=true -o artifacts/my-aot
```

发行构建使用已提交的 SPIR-V；修改 GLSL 后运行 `tools/build-shader.ps1 -Compiler <Khronos glslang 路径>`。生产包不包含编译工具或 Skia 原生库。当前 GPU 启用代码仍为 `options.UseGpu(SimdSkiaGpuBackend.Vulkan)`。

[完整证据、逐帧数据及哈希](evidence/vulkan-round3-2026-10-22.json) · [优化前 CSV](evidence/vulkan-round3-before-2026-10-22.csv) · [优化后 CSV](evidence/vulkan-round3-after-2026-10-22.csv) · [上一轮报告](PERFORMANCE-COMPATIBILITY-2026-10-22.md)。本机工作进程的全部采样与像素保留在 `artifacts/vulkan-round3`，部分帧无损归档为 `.rgba.gz`；SHA-256 对应解压后的原始 RGBA，报告工具同时支持两种存储。

时间戳实现依据 Khronos 的 [vkCmdWriteTimestamp](https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdWriteTimestamp.html) 和 [vkGetQueryPoolResults](https://docs.vulkan.org/refpages/latest/refpages/source/vkGetQueryPoolResults.html)，检查队列有效位数、在命令中 reset、等待 fence 后读取并处理计数回绕。
