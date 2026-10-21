# 验证：2026-10-21

新增真实 Windows Native AOT 验证：[详细报告](AVALONIA-AOT-COMPARISON.md)。4 份发布物（两种渲染器 × 两种 ISA）均成功生成原生代码，90 个正式进程运行检查 AOT 标志、PE 头、真实 Fluent 模板、字体、ImageBrush 像素及模块隔离；原有 8109 项控件回归通过。15/15 跨 ISA 图像逐字节相同；原版整页画质门槛 0/20，通过性能测量不代表通过替换验收。新增 CI 是 Windows AOT 编译/运行 smoke，不能当作共享云主机性能基准。

本轮最终结果见 [测试报告](TEST-REPORT-2026-10-21.md)。已运行 Release、原版 API/像素、实际 Avalonia/Win32、CPU/GPU、全硬件回退、PNG/JPEG/ICC、TTC、压缩与依赖隔离。功能回归通过，完整兼容性仍失败；`verify.ps1 -Reference -Gpu` 在执行全部检查后返回 2，失败像素行不会被跳过。CPU/GPU 性能及内存见 [最终报告](PERFORMANCE-FINAL-2026-10-21.md)，本地完整日志 `artifacts/completion/delivery-validation.log`。

以下为同日此前的历史验收记录。

最新续轮通过 6581 项真实 Avalonia/字形检查（硬件与 DOTNET_EnableHWIntrinsic=0），7845 项 CPU/编解码及 595 项旧文字链双模式通过；313 项 Vulkan 含新增字形命令明确拒绝、实际 Win32 呈现、生产依赖隔离通过。原版 22 份硬裁剪夹具一致，24 组独立文字及 3 个完整 UI 像素 gate 仍失败，阈值未放宽。最终 36 进程 CPU/内存/P95/P99/首绘和 SHA 清单见[续轮报告](AVALONIA-TEXT-CACHE.md)。验证日志在 `artifacts/text-cache-verification.log`。

## 前轮接入验收（历史记录）

前轮验收已改为真实 Avalonia 控件及合成器，见 [接入文档](AVALONIA-INTEGRATION.md) 和 [实际控件 CPU/内存报告](AVALONIA-CONTROLS-PERFORMANCE.md)。914 项断言验证窗口 framebuffer、鼠标点击、中文输入、DPI/resize、分组重叠/mask 像素、AA 裁剪、格式/stride/Alpha、标量与 AVX2 合成、图片轴向/通用路径、可变 pen/路径失效、拒绝合成字体与原版 kern/GPOS 夹具。另实际运行 Win32 软件窗口，完成呈现且未加载 Skia/HarfBuzz/ANGLE。新增原版排版夹具允许误差 0.000001；原版全文 UI 像素门槛未放宽，三个性能场景仍全部失败。

最终性能对比 27 个进程：托管、原版完整透明组、原版默认各三个配置×三个进程，每进程预热 2.5 秒并测量 600 次，记录 P95/P99、吞吐、CPU、GC 和内存。此前优化前后与排版修复/透明度核查的各轮数据另存；第一轮托管优化前后逐字节一致，后续字距修正及透明度选项变化未声称仍逐像素一致。保存 manifest/程序集及字体 SHA、逐配置 PNG/RGBA 与差异图。`run-avalonia-controls.ps1` 画质失败时返回 2。原版只在 Reference 项目；依赖扫描覆盖六模块零包依赖及可选适配层五个纯托管包、示例输出无原生绘制组件。

## 前轮 SK 调用回放（历史记录）

最新 [Avalonia 专项报告](AVALONIA-OPTIMIZATION.md) 区分 SK 调用回放、完整应用与窗口 GPU。定位文字基准 3 进程×5 批，优化前 75 批，最终原版/标量/SIMD 共 225 批；另有独立冷启动探针。原版文字画质 0/4 通过，图片 1/1 逐字节通过，没有放宽阈值。标量/SIMD 5 个最终帧相同；本轮坐标归一化使文字前后最大变化 1 个通道级。

- 新增 595 个定位文字/缓存/生命周期检查，硬件与 DOTNET_EnableHWIntrinsic=0 均通过；GPU 模式另加 4 个真实 CPU/Vulkan 定位 blob 对照（最大允许误差 2）。包括跨画布、clip/颜色变化、变换/负比例/离屏、缓存上限、共享字体并发度量/构建/渲染、字体提前 Dispose、可写 run 后续修改不污染 blob。
- CPU/codec 检查增至 7845，含原版冻结标量对照及不透明图片任意 A 字节、跨度/SIMD 尾部/格式/三 Alpha/stride/裁剪。硬件及完全回退均通过。
- 真实原版定位文字行为探针：9 项边界/构建流程通过；glyph IDs 一致、advance 微差约 0.000001，g bounds 仍差 1 px；默认 BaselineSnap/LinearMetrics/Hinting/Subpixel 差异保留。没有把对照产物自动算作全行为通过。
- Release 构建、36 基础与功能测试、原 312 项 GPU 检查、零生产 NuGet/Skia/HarfBuzz 扫描通过。总 API 更新为 425/3235，2810 缺失、0 个额外/不同；Avalonia 使用面 120/346 签名存在。native handle/默认字体请求仍会拒绝。
- 重新绘制原版/替代既有 120 场景，108/120 通过，原有曲线/字体 12 项失败；PixelCompare 仍以 1 非零退出。默认 SimHei 的 25 个行为探针一致。结果在 `artifacts/avalonia/legacy-diff`，没有把非零画质退出作为全通过。

证据目录 `artifacts/avalonia/performance-final-2026-10-21`、`tests-managed`、`tests-reference`、`tests-fallback`、`tests-gpu`；最终完整常规验证日志 `artifacts/avalonia/verification-final.log`。字体与 CPU 画质仍是接入前必须解决的功能差距。

## 同日较早的通用 CPU/GPU/OCR 验证记录

本轮最终构建与回归日志在 `artifacts/optimization-2026-10-21/verification-final.log`，性能与资源实测见 [OPTIMIZATION-2026-10-21.md](OPTIMIZATION-2026-10-21.md)。运行时 .NET 8.0.32；真实 OCR 用 .NET 10.0.13；i7-8700K、GTX 1050 Ti、Windows x64。

- Release 解决方案构建 0 警告/错误；36 基础测试及中英文字体、路径/AA/裁剪/PNG/合成等功能检查通过。
- 6981 项回归检查分别在硬件 SIMD 和 `DOTNET_EnableHWIntrinsic=0` 通过，含逐字节比较、异常和生命周期；fixture 来自 Python 标准库独立编码，含 213 zlib、471 PNG、2 个通过 CRC/Adler 但语义错误的 PNG。覆盖全部过滤器、块/向量边界、透明/gamma、共享租用、重复释放、失败返回和并发解码。
- 312 项 Vulkan 检查通过，含 309 组优化核对原始无限制全边/16×16 核逐字节比较、3 项索引边界。包含 winding/inverse、候选边过多时的回退、严格采样 ties、负比例、clip/gradient、CJK 亚像素、stride/初次清零、完全离屏索引与预算回退。原有线程/Dispose/纹理版本/批次检查通过。
- 本轮优化前后 3 进程的 84 个 CPU、72 个 GPU 保存帧逐字节一致。原版画质 corpus 仍为 108/120；12 个失败和差异图保留在 `artifacts/optimization-2026-10-21/diff-final`，PixelCompare 仍非零退出。
- 行为：默认 SimHei 25/25 与原版一致；固定 Zen 的 Advance/Bounds 两个浮点字符串有约 0.000004 以内差异。保存的优化前 DLL 已复现相同差异，两种字体均为本轮前后 25/25 完全相同。证据在 `behavior-validation.json`；未放宽断言。
- 公共 API 403/3235 匹配，2832 缺失，0 个额外/不同；六个生产项目零 NuGet/Skia/HarfBuzz 依赖。CPU 与 Vulkan 的 Features 示例均运行并输出实际图片。
- 最终 CPU 1260 批、GPU 720 批，源码 SHA 在运行结束检查稳定。GPU 完整计时含 fence、设备到主机 copy、主机拷贝。原版与本实现的 CPU 时间、working set/private、分配、GC 均保留；Vulkan validation layers、Linux/其他 GPU、能耗及显存峰值仍未验证。
- 真实 OCR 独立采用 4.153.1，36 进程/180 批，9 对 PNG 像素及 18 次文本/框/置信度完整输出一致。仍保留 JPEG 失败和透明 premul/straight 差异，未将 PNG 适配冒充完整 API 替换。

```powershell
powershell -NoProfile -File tools/verify.ps1 -Gpu
$env:DOTNET_EnableHWIntrinsic='0'
dotnet tests/Optimization/bin/Release/net8.0/Optimization.dll baseline/fonts/ZenOldMincho-Regular.ttf
Remove-Item Env:DOTNET_EnableHWIntrinsic
python tools/report-optimization.py
```

## 2026-10-12 起的历史验证

环境：Windows 10.0.26300 x64，SDK 10.0.401/net8.0，Vector256，NVIDIA GTX 1050 Ti Vulkan hardware。Linux 未实测，本机没有可用 WSL；CI 配置不等于执行证明。

## 命令

    dotnet build SimdSkia.sln -c Release
    dotnet run --project tests/SimdSkia.Tests -c Release
    dotnet run --project tests/Features -c Release -- --reference-pngs
    dotnet run --project tests/Gpu -c Release
    dotnet run --project examples/Features -c Release -- --gpu
    powershell -NoProfile -File tools/check-production-dependencies.ps1

构建 0 警告/0 错误，36 原有和 14 扩展测试通过。字体实测系统 SimHei 和固定 OFL Zen Old Mincho：真实 CJK/Latin glyph、advance、轮廓、换行/折行。PNG 原版夹具使用 dynamic Huffman；fixed 夹具由独立 zlib 1.3.1 生成；stored 块由托管编解码互测。产品不依赖参考压缩实现。

SIMD 对比 150 组宽度/格式/Alpha/stride/尾部组合，并实际 DOTNET_EnableHWIntrinsic=0 测回退。源码和生成文件均保留。

## GPU

GTX 1050 Ti，software=False。与 CPU 的矩形、AA 路径、渐变/透明度、miter 描边、中文、纹理/裁剪和批次边界逐像素一致；另测试源图版本捕获、缓存淘汰、跨线程拒绝、Dispose。缓存为 VkImage，不是上传整个 CPU 帧。读回前等待 fence。

artifacts/features-cpu.png / features-vulkan.png 是独立组合示例。artifacts/vulkan.json 是实际批次提交/完成时间，不是完整 GPU 基准，也未与原版 GPU 比较。Linux、swapchain、多设备和真实 device loss 尚未测试。

## 原版画质：保留失败

同一源码分别编译入 ManagedRenderer 与 ReferenceRenderer。120 场景为 20 种×2 格式×3 Alpha，64×48/272 stride。25 行为/字体探针一致，包括 CJK glyph ID/advance/控制点 bounds。

阈值：整数绘制/裁剪/孔洞/描边/图片最大误差 0，混合/渐变 1，AA 矩形 2，曲线 AA/字体 32。108/120 达标，12 曲线/文字未通过；PixelCompare 和 verify -Reference 非零退出。

曲线最大误差 65..66，RGBA/Premul 平均绝对通道误差约 0.325；文字最大 61..62，平均约 0.687。失败详情和放大差异图在 artifacts/diff。未放宽阈值，未降低质量换速度。

独立二次曲线像素 (14,16) 积分约 0.33006，原版 Contains 64×64 采样约 0.33008，但原版 DrawPath Alpha 为 17/255；本项目覆盖率约 78/255，与积分相差约 6 个 Alpha 值。ReferenceRenderer --probe-aa 可复现。此例说明覆盖率实现差异，不证明本项目所有曲线/字体更好，也不替代兼容验收。

API：33 类型/403 项匹配，2832 缺口，0 额外或不同项。签名匹配不意味着 font 默认值、hinting、native handle 等行为一致。

## 性能

清屏固定 1024×768 RGBA/Premul、同一颜色、20 轮预热/80 轮计时：

| CPU 内核 | 本次 ms/frame | 计时段线程分配 |
| --- | --- | --- |
| 原有逐像素验证/写入（改动前） | 5.867 | 40 B，含 Stopwatch 对象 |
| 新 uint 标量 | 0.373 | 0 B |
| 新 Vector256 | 0.088 | 0 B |
| 原版 SkiaSharp CPU | 0.078 | 0 B |

见 artifacts/cpu-before.json、cpu-after.json、cpu-reference.json。这只是一次微基准，本次 Vector256 清屏仍略慢于原版，未证明全引擎优势。path/text 分配及其他热点待继续优化。WorkingSet 含不同进程初始化/runtime 开销，不能当纯像素内存公平比较。GPU 不与 CPU 表混算。

后续已运行 [12 场景多进程性能比较](PERFORMANCE.md)，新结果以该报告为准，旧表保留为历史微基准。benchmarks/Managed 与 Reference 共编译同一 workload/worker；Report 核查 runtime、字体哈希、decode 输入哈希、逐进程像素与帧数。PNG encode 因压缩策略不同不列胜负；其他质量 gate 失败同样仅作诊断。上述为优化前首次比较；原始报告已归档至 PERFORMANCE-BEFORE-OPTIMIZATION.md。本轮随后优化产品算法，结果见 OPTIMIZATION.md 和更新后的 PERFORMANCE.md。

## GPU(Vulkan)对比验证补充

[GPU 报告](PERFORMANCE-GPU.md) 在同一 GTX 1050 Ti 上比较原版 SkiaSharp 3.119.4 Vulkan 光栅（graphics queue + GRContext）与 SimdSkia compute 管线。benchmarks/GpuReference 手写 vulkan-1 P/Invoke 初始化并反射 GRVkExtensions；benchmarks/GpuManaged 走 VulkanSurface。两 worker 共编译同一 GpuWorkloads/GpuWorker；GpuReport 复用 Report 的批采样/像素 gate 逻辑，以原版 GPU 输出为像素基准。每帧 round-trip（绘制+提交+等待 fence+回读）统一计时口径，另采集托管分配、进程内存与 CPU 使用率。clear/evenodd/stroke/gradient/blend/rectangles 像素级通过或 max≤1；rect-aa/curve-aa/chinese 因两种 AA/字形算法差异仅列诊断（如 rect-aa 256×256 仅 12/262144 通道超容差）。运行数据在 artifacts/performance-gpu/latest。

## CPU 优化验证补充

[本轮报告](OPTIMIZATION.md) 包含优化前后时间、分配和像素核验，旧运行 artifacts/performance/comparison-2026-10-12 保留。本轮 complete 运行在 artifacts/performance/optimized-2026-10-12；额外 moving 路径/文字在 artifacts/performance/changing-2026-10-12。CPU 与 GPU 时间没有混用。

`tests/Optimization` 冻结优化前 CPU 渲染器并逐字节比较，覆盖随机仿射、AA、反向填充、格式/stride/Alpha、Source/SourceOver/Clear、shader、图片、LRU 淘汰、路径修改、字体参数、描边参数、可变源图及别名绘制。包含高坐标浮点网格回退和 SIMD 除 255 全域整数恒等式核查。81 个独立 zlib + 31 个 PNG fixture 涵盖三种 DEFLATE、所有过滤器、低位灰度/调色板、gamma，检查截断/校验错误拒绝。合计 5267 项通过；`DOTNET_EnableHWIntrinsic=0` 下同样通过。fixture 用 Python 标准库生成，只用于测试，产品不引用 Python/zlib 或 System.IO.Compression。

    dotnet run --project tests/Optimization -c Release -- baseline/fonts/ZenOldMincho-Regular.ttf
    $env:DOTNET_EnableHWIntrinsic='0'
    dotnet tests/Optimization/bin/Release/net8.0/Optimization.dll baseline/fonts/ZenOldMincho-Regular.ttf
    Remove-Item Env:DOTNET_EnableHWIntrinsic

原版 120 场景复测仍 108 通过、12 未达标，25 个行为探针一致。GPU Windows NVIDIA 回归：所有覆盖矩形/路径/AA/渐变/描边/中文/图片/批次及缓存/Dispose/线程约束检查通过。生产依赖扫描依然零 NuGet/native 字体/编解码依赖，系统 Vulkan loader 是已授权 GPU 互操作。

## 修复后性能复评验证

最新 CPU/GPU/内存及可继续优化评估在 [REASSESSMENT.md](REASSESSMENT.md)，当前代码的基准输出分别为 artifacts/performance/reassessment-2026-10-12 和 artifacts/performance-gpu/reassessment-2026-10-12。完整记录 1764/1008 批，均 3 个独立进程/实现、每项 7 批、目标 100 ms，CPU 与 GPU 分开测量。

CPU 处理器时间在绘制结束后立即读取，采样器对象在分配计数区间外创建；跨批累计加权 CPU% 并记录 CPU ms/帧及除以 12 逻辑核的机器占用，避免用短批中位 CPU% 掩盖实际成本。内存仍为整个进程，native malloc/显存及其他线程分配不在 B/op 中，不将 working set 当显存或纯引擎内存。

本次 Release、36 基础、14 功能（含原版 PNG fixtures）、5267 冻结/编解码检查、当前 GPU 和隔离实验 GPU 回归通过。原版 120 场景仍 108 通过/12 失败，25 行为探针一致，PixelCompare 非零退出并保存到 artifacts/reassessment/diff。GLSL 重编译 SPIR-V 与产品 SHA 一致；产品依赖扫描仍零 NuGet/native 字体/编解码。

隔离 HOST_CACHED 实验保存在 experiments/GpuHostCached，通过同一 GPU worker、同一质量、同一同步回读计时；504 批、72 个 canonical 帧与当前 GPU 字节一致。该实验不属于生产依赖图，完整 CPU/内存对照与源码哈希保存在对应 experiment 目录。生产 GPU 已修复 descriptor offset+range 超过边缓冲区大小的问题，尚未运行 Vulkan validation layers 或其他硬件，不能宣称完整 GPU 验证。

## 实际 OCR 模块验证

[OCR-PERFORMANCE.md](OCR-PERFORMANCE.md) 单独采用目标实际解析的 SkiaSharp/NativeAssets 4.153.1。链接当前 Qcxt.OCR 源码/模型，在本工作区构建 .NET 10 CPU 基准，目标仓库只读、源码和资源 SHA 前后核验稳定。模型加载及夹具读取不计时；默认 OCR 自动线程预算，两侧同一 OCR DLL 哈希；3 进程/条件、5 批/进程，共 36 进程/180 批，各条件新进程避免 OCR 驻留污染只解码内存。

500/1024/2048 三尺寸、3 进程的 9 对不透明 PNG canonical 像素一致；实际 500×500 夹具在 raw/bitmap/bytes 三模式、两侧、3 进程共 18 次验证，16 行的文本、框、置信度及完整结果字段一致。JPEG 原版成功、当前托管解码失败；2×1 透明 PNG 探针确认原版 Premul 与托管 Unpremul 的物理 RGB 不同，不宣称透明输入普遍等价。

未修改完整 SkiaOcrExtensions.cs 的编译探针失败，保留 artifacts/ocr/compatibility-probe.log；适配后的 PNG/RunBitmap 比较不等于完成模块替换。当前数据和源哈希在 artifacts/ocr/qcxt-4.153.1-2026-10-12，复现代码在 benchmarks/Ocr、tools/run-ocr-benchmarks.ps1 与 report-ocr-benchmarks.py。链接的目标核心源码本身有 5 个警告，按其原有非 warnings-as-errors 规则构建，未悄悄修改它们。
