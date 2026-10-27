# 进度：2026-10-27

发布状态：**0.1.0-preview.9 已公开发布并验证全部 12 包下载内容**，包含独立兼容层/PDF 和 VelaShell 修正。三平台托管、桌面 JIT/AOT/实际包消费及 Windows AOT 验证通过；可信发布前两次因策略不匹配失败，更新策略后第 3 次上传和公开内容核对成功，原标签与 CI 包未改变。每个包都带 README、CHANGELOG 和 NuGet 发行说明；[发布记录和证据](NUGET-PREVIEW.md)保留全部结果及已知限制。下文“未发布”保留测量当时状态，不代表 preview.8 包含这些新增功能。

新增真实目标：[VelaShell 固定提交的整体兼容测试](VELASHELL-COMPATIBILITY-2026-10-27.md)。本轮保留两项已验证修正：不支持合成字体时按 Try 合同返回 false/null，让 Avalonia 选择真实近邻字面；按 double ULP 归一化图片布局尾差。最终 9,750 项控件回归、13 项系统字体检查通过。CPU/Direct2D/Vulkan 各 23 个常规页面通过；原版完整视图 548/548，排除资源监视后同组 514 项原版全过、替代 393 过/7 失败/114 未完成。组合字符会崩溃、资源监视滚动超时，完整视图/画质和移除原生 Skia 未通过。未解决的阴影缓存实验已撤回，**未发布新包**。

**当前未发布开发进度：** 已完成网友反馈的独立调用链缺口：GetPixels、字体族/样式匹配、GIF/SKCodec、SKRoundRect/圆角裁剪、SaveLayer、SKRect.Inflate；新增多页栅格 PDF 导出、可选纯托管 PDF 阅读和真实窗口查看器。API 签名匹配 **892/3235**，新增行为测试 **原版 26 / 替代 37**，新像素对照 **11/14**，三项圆角/文字画质仍失败。PDF、AOT、性能和剩余项见[本轮报告](STANDALONE-COMPATIBILITY-2026-10-27.md)。**尚未发布新包，以下 preview.8 内容是历史发布记录。**

**preview.8 已公开发布并验证 11 个包下载内容。** 三平台托管、JIT/AOT/nupkg 窗口和 Windows AOT 检查全部成功，已核对 9 份桌面记录；包内使用示例编译通过。详见[发布记录](NUGET-PREVIEW.md)和[发布证据](evidence/nuget-preview8-2026-10-27.json)。完整原版画质仍失败，预览发布不改变该结论。

发布决定更新：2026-10-27 用户在了解当前缺口后授权发布 **0.1.0-preview.8** 并重写博客园文章。此前完整验收结论保持不变；本次是披露限制的实验性预览发布，状态见[发布记录](NUGET-PREVIEW.md)。下文“未发布”描述的是性能测量完成时的状态。

本轮未发布候选已完成 **75 个 CPU / 36 个 GPU 串行进程**的正式复测：[完整优化、CPU、内存及画质报告](PARITY-OPTIMIZATION-2026-10-27.md)。按用户要求，各后端场景耗时比的几何平均数须 ≤1.00，每例须 ≤1.10。通用 x64 Native AOT 的 CPU 吞吐综合比 **0.9557**，含 60 FPS 的全部场景综合比 **0.9330**，最慢场景 **1.0383**；Vulkan / Direct2D 相对每例较快原版 GPU 的综合比为 **0.7021 / 0.6474**。三项**速度门槛通过**。GPU 是 JIT 离屏完成帧与回读，不能替代 AOT、真实窗口呈现或显示器帧率验收。

已实现 SSE2 字形/图像合成、精确采样舍入、连续及中等大小色块填充、无需图层时减少状态记账、有界字形串缓存命中优化、紧密 framebuffer 整块复制和画布缓存容器按需分配。60 FPS 滚动每帧托管分配由 preview.7 的 **155.14 降至 28.05 KiB**，原版 **12.87 KiB**；普通控件页工作集仍为 **79.89 vs 原版 60.91 MiB**，不能宣称内存追平。

完整回归：**471,569** 项精确检查（含可选字体），SSE2 限制和完全标量各 **471,557** 项（未传可选字体）；真实 Avalonia **9,741**、Direct2D **344**、Vulkan 内核 **540**、GPU 集成 **12,174** 项通过。最终通用 x64 **真实 Native AOT 460,050** 项采样通过，并从外部验证 PE/已加载模块。优化前后 CPU 页面像素全部相同，但原版整页画质仍为 **CPU 0/5、GPU 0/12**，`verify.ps1 -Reference -Gpu` 和两组正式验收均返回 **2**。已分离字体指令和覆盖率两个问题，诊断副本及解释器原型未混入生产。**当前仍是 `0.1.0-preview.7`；未发布新包，未按新成绩重写博客园文章。** 后续画质修复还须守住速度门槛，并复核呈现与平台回归。

以下为此前交付与历史测量，不与上面的候选数据混算。

新增[博客园独立文章：AOT 体积、速度、CPU 与内存实测](blog/2026-10-27-simdskia-avalonia-aot-cnblogs.md)，附 6 张 PNG/SVG、逐进程数据、像素/二进制/源码哈希与配图压缩包。本轮用已发布 preview.7 重新构建通用/v3 AOT，串行轮换运行 90 个 Fluent 控件/列表进程，再用前轮原始默认 EXE 测量 6 个可见空闲窗口。预跑与环境初始化重叠，明确排除并完整保留，所有正式场景完整重采。

100% DPI 控件页：通用 AOT 原版/SimdSkia **1.134 / 2.359 ms**，v3 **0.935 / 0.689 ms**；60 FPS v3 控件页 CPU **0.96% / 0.70%**（整机 12 逻辑处理器口径），工作集 **61.9 / 79.3 MiB**。默认空窗工作集 **68.32 / 46.12 MiB**、Private **94.85 / 23.55 MiB**，不可替代中文控件负载。滚动页分配仍偏高，已明确列为缺口。本轮页面对照 **0/20** 达到既定门槛，进程与测量检查通过；两种 AOT 的 15/15 对捕获相同、30/30 组跨进程捕获稳定。未测 GPU 性能，未将 v3 成绩当成通用二进制的成绩；原版原生 DLL 在两个 ISA 中完全相同。

新增[默认 Avalonia 空白模板的 AOT 体积实测](DEFAULT-AVALONIA-AOT-SIZE-2026-10-26.md)：相同 Avalonia 12.1.4 / .NET 10 / win-x64，保留 Fluent 和 Inter 字体，使用 NuGet.org 的 SimdSkia preview.7 替换默认后端。真实 AOT 窗口、文字、模块与正常退出检查通过；原版运行文件 36.53 MiB，SimdSkia 19.51 MiB，减少 46.59%。DLL 17.97 MiB → 0，减少 Skia/HarfBuzz/ANGLE 三个原生 DLL；EXE 增大约 0.95 MiB。原始含 PDB 的 publish 为 201.27 / 86.62 MiB，另有完整逐文件、符号和 ZIP 记录。此处为默认 CPU SimdSkia 与原版默认 GPU 的体积对照，没有据此宣称速度或完整画质兼容。

发布完成：本轮优化与半透明修复已随 `0.1.0-preview.7` 公开发布，11 个包均已实际下载，DLL、README、`CHANGELOG.md`、许可证和发行说明与 CI 一致。NuGet 发行说明及通用/三个 Avalonia 包内 README 列出优化范围、最终实测、画质修复、内存与兼容性限制。三平台托管/真实 JIT/AOT/nupkg 窗口、Windows 双后端 AOT 与通用/v3 采样全部通过；[发布记录](NUGET-PREVIEW.md)和[证据](evidence/nuget-preview7-2026-10-26.json)保留版本、标签、全部包文档及公开下载校验。API 报告更新为实际发布 DLL 的哈希，仍为 425/3235。

已修复上一轮两个半透明图片类别的原版质量失败：固定 x64 低精度图像合成使用近似 /256，旧实现在透明度缩放和背景贡献上使用 /255，造成约 1–2 色阶差异。统一现代图像采样、直拷贝、图层回贴及 Vulkan，原门槛下 **24/24** 组通过。新增 **12,288 个独立原版样本、393,216 次回放检查**，完整精确检查 **468,137** 项通过；控件 9734、Direct2D 344、Vulkan 采样 590 及 GPU 内核 540 通过。两种真实 AOT 各通过 460,050 项采样检查。[原因、源码依据、画质和速度复测](ALPHA-COMPATIBILITY-2026-10-26.md)。完整原版画质验证仍因既有字体/路径缺口返回 2，未发布 NuGet。

核心 CPU 图片缩放完成新一轮优化：八像素 AVX2 gather、过滤前精确预乘、保持原舍入和尾部回退，不透明源使用正确行合成。非预乘不透明 1024×768 **46.317→7.423 ms**（约 6.24×），原版 **7.830 ms**；Opaque **8.815→6.415 ms**，原版 **7.188 ms**。96 个核心场景进程记录 / 672 批、36 个实际控件进程完成；24 组核心前后/标量图像和 9 对控件帧逐字节一致。真实页面中位数改善约 3%–5%，托管保留量没有明显下降。[完整速度、CPU、内存和失败项报告](CORE-PERFORMANCE-2026-10-26.md)。

74,921 项精确检查在正常、关闭 AVX2、关闭硬件指令时全部通过；通用和 v3 AOT 各实际通过 66,834 项采样检查并确认无 CLR/JIT。完整 CPU/GPU 回归仍因既有原版画质门槛返回 2，API 保持 425/3235；原版核心采样质量 12/24、整页 0/3 通过。没有声称全面追平，没有重测 GPU/白屏，没有发布 NuGet。以下保留此前交付记录。

新增 [NativeControls 发光控件](../examples/NativeControls/README.md)：自绘文本框、焦点呼吸光、渐变流光进度、悬停/按下按钮，具有实际输入编辑、暂停/继续和重置行为。22 项编辑边界检查和真实 AOT 窗口交互通过。EXE **1.343 MiB**，较纯中英文窗口增加 **33 KiB**；[报告与截图](NATIVE-CONTROLS-2026-10-23.md)。

独立窗口最小发布已完成：纯图形 EXE **1.172 MiB**、中英文 **1.311 MiB**，较首次分别减少 **36.8% / 32.0%**。修正无效的旧 AOT 参数，新增 `MinimumSize` 发布配置，移除示例诊断依赖，并从外部验证最终 EXE。6 个候选加 2 个最终部署复验共 32 帧，全部 RGB 与冻结基线相同；2 个移除反射元数据的变体启动失败，明确排除。关闭地区格式化/部分诊断的取舍及原始证据见[最小发布报告](NATIVE-WINDOW-MINIMUM-2026-10-23.md)。没有改动生产渲染库或发布 NuGet。

新增不依赖 Avalonia 的 [Win32 / Native AOT 自绘窗口](../examples/NativeWindow/README.md)：纯图形完整部署 **1.87 MiB**，含系统中英文文字 **1.94 MiB**；两版都实际验证 3 帧、缩放、鼠标点击、窗口 DC 像素、抗锯齿及真实 AOT 模块。无附带字体或绘图 DLL。此示例仅提供简单 Windows 自绘宿主，没有 Avalonia 控件/布局等功能，不与完整框架作同功能比较。[报告与证据](NATIVE-WINDOW-AOT-2026-10-23.md)。本次未修改生产渲染代码，未重新发布 NuGet。

最新生产源码 `1438833`：[桌面启动、CPU/GPU 与内存报告](STARTUP-ROUND7-2026-10-22.md)。实现桌面 JIT 一次性后台绘图准备，短线程完成即退出，无窗口应用与 Native AOT 跳过；精确几何边界缓存随路径修改失效，字形合批减少覆盖缓冲复制，保留共享掩码不变。70 次可见启动：JIT 白色阶段 CPU 114→61 ms、Direct2D 289→198 ms、Vulkan 363→266 ms；原版 CPU 为 49 ms，白屏尚未完全消除。同一程序关闭准备的对照也保留。

系统字体控件 0.695→0.666 ms/帧，Private 基本持平（48.6 MiB，原版默认 42.8 MiB）。CPU 窗口每帧分配下降约 17%，但 P50/P95 没有改善；GPU 吞吐基本不变、部分 CPU 占用上升。实际 AOT 首帧约 337 ms，与原版接近，发布目录 26.99 vs 38.33 MiB；AOT 常驻内存仍高于原版。这些数字均为本轮独立测量，不以首帧改善替代持续性能。

8665 优化/完全标量、9734 控件/生命周期检查通过；三平台托管、桌面 JIT/AOT/nupkg 及 Windows 双实现 AOT 全部成功。15 对 CPU 帧、6 对窗口截图与冻结版本逐字节相同。功能探针仍 82/114、旧图像 108/120、API 425/3235，完整验证返回 2。**尚未发布新 NuGet，preview.5 不含这些改动。** 以下为此前各轮记录。

上一轮生产源码 `6c017e7`：[图片采样、画刷缓存与窗口报告](OPTIMIZATION-ROUND6-2026-10-22.md)。实现保持舍入的 SSE4.1 双线性采样、精确 AA 计数移位、不透明图片画刷行复制；释放不可复用的旧图片版本，并用弱关联身份避免缓存持有源对象。追加修复矩形转路径时每帧生成新几何、重复保留同一覆盖图的问题。系统字体控件 0.793→0.694 ms/帧；连续换图短批次 7.553→1.681 ms/帧、GC 后 Private 58.6→27.9 MiB，后者与原版逐字节相同，但原版仍为 0.729 ms、26.5 MiB。

8665 优化、9664 控件/生命周期检查以及完全标量回退通过；Direct2D 344、GPU 集成 12174、平台字体 36 项通过。三平台托管/桌面 JIT/AOT/nupkg 和 Windows 双实现 AOT 验证成功。普通控件内存没有明显下降、滚动未提速，白屏及完整画质/API 缺口仍保留；真实窗口和启动指标均在报告中，不用离屏成绩替代。没有发布新 NuGet。以下为此前各轮记录。

本轮启动与性能优化：[报告及逐进程证据](STARTUP-PERFORMANCE-2026-10-22.md)。生产源码 `1c6af37` 已提交：系统字体分页、TTC 精确导出、索引扫描线/小路径栈缓冲、向量化裁剪段。系统字体控件平均 1.008→0.800 ms/帧，工作集 111.9→94.8 MiB、Private 67.0→48.5 MiB；原版默认 0.911 ms、88.4 MiB、42.8 MiB。固定字体标准/高 DPI 耗时下降约 24%/14%，滚动略慢，固定字体内存基本不变。

35 次真实可见启动：CPU 白色阶段估计 280→218 ms，原版也有约 95 ms。初始四次 GetPixel 粗测漏掉原版白色阶段，已更正为单次屏幕读取；白屏仍未消除。CPU/GPU 窗口的 2+3 秒及 8+8 秒观测均保留：内存下降，GPU 吞吐基本不变，CPU/Direct2D 较长观测的总 CPU 占用仍有回退。

8085 优化/标量、8299 控件、36 平台字体、344 Direct2D、12174 GPU 集成通过；12 对 CPU 帧和 6 对实际窗口/呈现截图精确一致。生产源码三平台托管/标量/JIT/AOT/nupkg 窗口和 Windows 双实现 AOT 均通过。本轮尚未发布 NuGet；preview.5 不含本轮及上一轮内存优化。完整原版画质验证仍返回 2，API 仍 425/3235。以下保留历史记录。

内存专项源码已完成：[报告与原始数据](MEMORY-PERFORMANCE-2026-10-22.md)。系统字体延迟加载、TTC 共享、摘要按需计算、光栅器/内部状态复用和渐变弱缓存已实现。真实系统字体 CPU 窗口工作集 252.6→116.0 MiB，Private 197.6→60.6 MiB；固定字体控件/高 DPI 耗时下降 5.2%/9.2%，分配下降 66.1%/69.5%。滚动与 GPU 未获稳定提速，默认短窗口 Vulkan CPU 占用上升也保留在报告中。原版同系统字体 CPU 0.906 ms/帧、88.4 MiB，本实现仍为 0.999 ms、111.8 MiB，完整画质/API 缺口仍在。

优化源码 `cd9f5d8` 的 8245 控件、12174 GPU 集成及三平台托管/标量/JIT/AOT/nupkg 消费窗口通过，9 对 CPU 帧与 12 对窗口截图完全相同。此轮没有发布新 NuGet，已公开 preview.5 不含上述内存优化；下文为此前版本与历史记录。

发布续轮：preview.5 的 11 个包已公开发布并逐项实际下载，DLL/README 与成功发布的 CI 产物一致，包含下述 Vulkan 专项优化。本机真实包的 CPU/Direct2D/Vulkan 窗口、三平台托管回归与 JIT/AOT/nupkg 窗口、Windows 双后端 AOT 全部通过。旧 preview.4 也已完成全部公开下载核对，原流水线失败由索引等待超时造成，包与标签保持原样。[版本与发布证据](NUGET-PREVIEW.md)

Vulkan 专项源码优化已完成并实测：[检查、测试与性能报告](VULKAN-OPTIMIZATION-2026-10-22.md)。本机 100% DPI 的真实控件窗口三进程 P50 22.250→2.316 ms（9.61×），CPU 2.067 / Direct2D 1.552 ms；Vulkan 仍慢约 12%，CPU/内存没有同比下降。实现 GPU 有界覆盖率缓存、宽绕数内核、保守分块、uniform/显存输入、BGRA 直接输出、覆盖消除与依赖同步。

540 GPU 检查（531 组精确对照）、590 采样、12174 GPU 集成通过；144 张冻结前后基准帧、实际窗口/离屏截图逐字节相同。CPU/Direct2D/Vulkan 的 Windows AOT 控件窗口与 AOT 时间戳诊断实际运行通过。原版 GPU 仍 12/24 通过，完整验证返回 2；本轮优化已随 preview.5 发布，preview.4 产物保持原内容。下文保留此前数据。

本轮第二次优化：[完整报告](PERFORMANCE-COMPATIBILITY-2026-10-22.md)包含本次重新运行的原版 CPU/Vulkan 对照、36 进程控件测量、18 次窗口测量、CPU/工作集/Private/分配与全部画质失败项。三个 CPU 场景比优化前降低 9%–12% 耗时，最终帧逐字节相同；Direct2D/Vulkan 窗口每帧托管分配减少 90%/94%，峰值 Private 减少约 39%/40%。CPU 窗口 P95 波动未隐藏，CPU 常驻内存差距仍在。

图片画刷原版探针由 15/40 提升至 38/40，完整功能探针由 59/114 提升至 82/114。38 个原版 RGBA 夹具加入正常回归；GPU 新增不同对象同内容、源修改/Dispose/排队版本和缓存淘汰检查。硬件与完全标量的 7845/8147 项通过，Direct2D 344、GPU 采样 590、GPU 集成 12172 项通过；Windows AOT 的 CPU/Direct2D/Vulkan 实际控件窗口均通过。

仍未完成：完整 UI 画质 0/3、旧图像 108/120、原版 GPU 12/24，SK* 公共面 425/3235；字体/曲线、VisualBrush 高 DPI、圆角阴影、复杂排版和更多 API 继续保留缺口。preview.4 已通过三平台回归与实际 JIT/AOT/nupkg 窗口检查，NuGet 已接收 11 包但公开下载校验超时，上架未确认；最终状态见 [预览发布](NUGET-PREVIEW.md) 和 [验证](VALIDATION.md)。下文为此前进度。

最终发布：11 个 `0.1.0-preview.3` 包已从 NuGet.org 公开下载，DLL/README 与成功发布的 CI 产物一致。公开包的 CPU/GPU README 程序在私有缓存中编译通过，公开 Direct2D 包的真实控件窗口通过；不依赖本地引擎 ProjectReference。[发布记录](NUGET-PREVIEW.md)

preview.3 修复重叠文字的半透明覆盖裁剪顺序与滚动缓存；GPU 硬件集成断言 12172 项通过，含先复现失败再修复的回归。修复版的三平台 JIT、Native AOT 及真实包窗口已全部通过 [CI 39647833834](https://github.com/hnlyf168/SimdSkia/actions/runs/39647833834)；本机 Direct2D/Vulkan AOT 硬件也再次通过。修复版九进程 JIT 控件 P50：CPU 3.539 / Direct2D 2.049 / Vulkan 21.437 ms，工作集 114.5 / 176.1 / 216.6 MiB；完整数据、实际截图、公开发布及限制见[交付报告](DESKTOP-GPU-2026-10-22.md)。

新增 preview.2：三平台桌面统一入口、Linux/macOS 托管系统字体发现、Windows Direct2D/D3D11 原生交换链、Avalonia Vulkan 回读绘图已实际实现。三平台 JIT 窗口 CI 通过；本机三后端均在呈现后 resize 通过。Direct2D 硬件 54 项、GPU 集成 12124 项、字形合批 60 项与原回归通过。三进程 JIT 控件 P50：CPU 3.569 / Direct2D 1.948 / Vulkan 20.972 ms；工作集 114.0 / 175.4 / 217.0 MiB。默认文字图集由 C# 生成，GPU 合成；不把 CPU 图层算成 GPU 光栅化。Native AOT、包消费和公开发布作为独立检查持续记录。[完整报告](DESKTOP-GPU-2026-10-22.md)

NuGet 公开发布与首页说明已完成：八个 `0.1.0-preview.1` 包均可公开下载，DLL 和 README 与成功发布的 CI 产物逐字节一致。GitHub 首页新增完整包名/用途表、安装命令、Avalonia 12.1.4 双文件程序、现有项目迁移、系统字体和 Windows AOT 发布说明。直接从首页提取两个文件，在全新包缓存、仅 NuGet.org 源下还原并编译，0 警告/0 错误；代码与已发布包内 README 示例一致。[首页](../README.md) · [发布记录](NUGET-PREVIEW.md)

## 2026-10-21 记录

预览发布：新增 MIT 许可、八个 `0.1.0-preview.1` NuGet 包、专用 Avalonia 中文使用 README 和 OIDC 可信发布工作流。Release 构建 0 警告/0 错误，36 个行为、484 个图片、8109 个控件断言通过；包内双文件示例已直接提取并成功编译。真实 NuGet 消费程序的系统字体 Native AOT 窗口通过，无本地 ProjectReference、无 CLR/JIT/Skia/HarfBuzz。RemoteNetwork 隐藏窗口的单实例恢复已修复，最新业务 AOT 登录/设置窗口也实际通过。[发布配置](NUGET-PREVIEW.md)

Windows 字体默认行为：新增零参数 `UseSimdSkia()`，读取当前系统消息界面字体并定位已安装的静态 TrueType 字面和有界回退集合。字体解析、排版及绘制仍由 C# 完成；显式文件配置保留原行为。系统模式 13 项、显式模式 10 项检查通过，RemoteNetwork 无资源字体的 11 个 JIT 界面场景通过，另发布独立 AOT 包；当前旧版运行实例占用单实例锁，新版 AOT 启动状态单独记录，不沿用旧版验收。[使用及边界](WINDOWS-SYSTEM-FONTS.md)

启动体验修复：AOT 两种后端改为 `WinExe`，普通 AvaloniaControls 示例在 Windows 同样使用 GUI 子系统。重新发布通用 x64/AVX2 共 4 个 exe，子系统均为 2，AOT/重定向输出/与原版发布物像素一致检查全部通过；旧性能二进制与哈希保留，GUI 新包位于 `artifacts/aot-gui`。[验证记录](aot-gui-validation.json)

Native AOT 续轮已完成：共享源码的 SimdSkia / 原版 Avalonia Win32 窗口，通用 x64 与 x86-64-v3 两套实际发布，共 90 次正式进程测量。v3 控件 1000×720：1.396 vs 原版默认 1.059 ms，工作集 85.7 vs 60.5 MiB；完整运行包 25.68 vs 41.10 MiB。通用版 SimdSkia 为 3.422 ms，不能把 v3 成绩当作通用 x64 成绩。8109 项回归通过，15/15 跨 ISA 固定输出逐字节相同；新整页画质 0/20 通过，完整替换仍 FAIL。详见 [AOT 实测报告](AVALONIA-AOT-COMPARISON.md) 与 [可运行程序](../examples/AvaloniaAot/README.md)。

本轮交付：图像/视觉画刷、Blur/DropShadow/内外阴影、ROI 层缓冲、Gaussian AVX2、TTC face、Adam7/16 位/部分 ICC、纯 C# JPEG 与 PNG LZ77 压缩、Vulkan 裁切/采样/覆盖裁剪均有实际代码。8109 项真实 UI、484 项图片、7845 项 CPU、595 项文字在硬件及全标量通过；Vulkan 新 445 项和旧 313 项通过。**完整兼容仍 FAIL**：425/3235 SK 项匹配、完整 UI 0/3、新功能 59/114、原版 GPU 12/24；Vulkan 窗口和通用字体排版尚缺。详见 [测试报告](TEST-REPORT-2026-10-21.md)、[CPU/内存/滤镜](PERFORMANCE-FINAL-2026-10-21.md)、[GPU](PERFORMANCE-GPU-FINAL-2026-10-21.md) 和 [逐项进度](COMPLETION-TRACKER.md)。

以下为同日此前的历史数据，不替代本轮测量与当前支持范围。

最新续轮：按字形共享/分批 mask、设备亚像素与 baseline、sRGB 文字预混合、半像素硬 clip 舍入已实现。6581 项真实 UI/字形/资源/原版夹具断言在硬件与完整标量回退通过；7845 项 CPU/编解码、595 项旧文字链通过，313 项 Vulkan 检查及实际 Win32 呈现通过。36 进程同机复测：100% 控件 1.492→1.083 ms，首绘 89.2→18.6 ms；列表 0.723→0.433 ms。工作集仍约 118 vs 原版默认 96 MiB。整页 RMSE 降到约 2.3–2.6，但最大误差仍超阈值，**0/3 画质 gate，完整替换与窗口 GPU 仍未完成**。详见[本轮 CPU/内存与画质](AVALONIA-TEXT-CACHE.md)。

下一步继续降低文字栅格误差、首绘与层缓冲内存，再补 ImageBrush/VisualBrush、滤镜、字体/编解码和 Vulkan 窗口；当前受测控件子集不等于目标应用完整兼容。

## 前轮实际接入记录

前轮完成：独立 `SimdSkia.Avalonia` 已运行真实 Avalonia 12.1.4 控件、Fluent 模板、布局和输入，并实际通过 Win32 软件窗口 framebuffer 呈现。新增 Image 裁切/过滤、AA 几何/圆角裁剪、区域、分组透明度/mask、独立圆角、多色渐变、弧/布尔几何、caps/joins/dashes、托管字体服务和 Latin/DFLT kern/GPOS 字距。原版仅在 Reference 项目运行，六个原有引擎模块仍无包依赖；新增适配层只用固定版本托管 Avalonia 和 Clipper2。

914 个真实 UI/像素/资源/排版断言通过，含按钮点击、中文输入、DPI/resize、SIMD 独立预期、15 份原版排版夹具和拒绝合成字体。CPU 最终三进程中位数：1000×720 控件 1.47 ms，原版完整组 3.18、原版默认 0.86；150% 为 2.67 / 6.73 / 1.59；滚动约 0.71 vs 原版 0.37。原版默认 opacity 与完整组行为有差异，报告单列。初版控件 32.3 ms 已改善，工作集仍约 119 vs 96 MiB，首绘仍慢。**像素 gate 仍为 0/3，完整替换和窗口 GPU 未完成**。接入、命令、当前限制见 [真实接入](AVALONIA-INTEGRATION.md)，CPU/内存及失败差异见 [实际控件报告](AVALONIA-CONTROLS-PERFORMANCE.md)。

下一步：修复文字/曲线/DPI 画质、首绘与工作集，然后补图像/视觉画刷、滤镜、复杂排版/字体格式及 Vulkan 窗口呈现。需要目标应用后再做实际集成验收，不将示例覆盖当作全应用兼容。

## 前轮 SK 兼容 API 回放（历史记录）

最新目标转为替换 Avalonia 的 Skia 渲染后端。已核对固定 12.1.4 源码、程序集与恢复锁文件；新增可复用的逐调用者 API 扫描：33/92 个引用类型存在，120/346 个成员签名存在，不能直接接入。SkiaSharp 总清单更新为 37 类型、425/3235 项，2810 缺失、0 个额外/不同，二进制仍不兼容。

已实现 SKTextBlobBuilder 定位 run → Build → SKCanvas.DrawText(blob)，含 glyph Span/位置/度量、独立快照、字体/构建器生命周期，以及线程安全有上限的跨画布文字覆盖率和每字体定位几何缓存。轴对齐非 AA 图片直接生成行映射，不透明连续行 SIMD 拷贝仍遵守格式/Alpha/stride。字体轮廓缓存增加并发保护。

实测与剩余接入要求见 [Avalonia 专项报告](AVALONIA-OPTIMIZATION.md)。四个文字热场景约 10–12 ms → 0.16–0.22 ms，像素仍超阈值，不能视为同画质胜出。相同文字重建可命中内容缓存，不代表全新文本性能；首帧仍慢，工作集通常高于原版。图片是现有 DrawBitmap 场景，Avalonia 实际 DrawImage 采样重载仍缺失。没有窗口 GPU 提速或完整 Avalonia 应用帧率结论。

本轮验证：7845 CPU/编解码检查在硬件与全回退模式通过；595 个定位文字/缓存/生命周期检查通过，含共享字体并发度量/构建/渲染；额外 4 个 CPU/Vulkan 定位文字对照通过；原 312 项 GPU 检查通过。原版新增行为探针记录默认值差异、g bounds 1 px 差距，未视为全部行为兼容。新增示例 examples/AvaloniaText 可输出实际 PNG。原始数据、冷启动、逐帧 ticks、CPU/内存、失败差异图均保留。

## 同日较早的通用 CPU/GPU/OCR 优化记录

本轮已合入 CPU 扫描线/覆盖率合成、PNG SIMD/DEFLATE/校验、共享像素缓冲复用，以及 Vulkan 瓦片边索引、子扫描线求交和设备内存/staging。详见 [本轮优化实测](OPTIMIZATION-2026-10-21.md)，含 CPU、GPU、CPU 时间、分配、working set/private 和真实 OCR。

同机、1024×768：PNG 解码 14.43→3.86 ms（原版 3.45）；动态曲线 2.11→0.60 ms；CPU 动态中文 9.03→4.23 ms；GPU 矩形 12.96→1.46 ms；GPU 中文 979.26→4.14 ms。曲线/中文仍有既有画质差异，不能据诊断时间宣布超过原版。GPU 动态文字的索引增加了分配和工作集；基础 CPU 小幅变动没有作为稳定提速。

PNG 解码托管分配从约 3.14 MiB/帧降到 21.1 KiB；CPU 动态中文从 1107→183 KiB。解码空闲像素池全进程最多 4 块/32 MiB，活动图像另计，最后一个共享视图释放后才归还。重复 Dispose、画布持有、失败解码和并发租用已验证。

验证：Release 构建 0 警告/错误，36 基础测试；6981 项 CPU/编解码回归在硬件和全回退模式通过，含逐字节、异常和生命周期；312 项 GPU 回归通过，含 309 组原始采样核逐字节比较及 3 项索引边界。优化前后保存的 84 个 CPU 和 72 个 GPU 帧逐字节相同。原版画质仍 108/120；默认 SimHei 行为 25/25 一致，固定 Zen 有两项优化前已存在的浮点字符串微差。公共 API 仍 403/3235，未扩大二进制或版本兼容承诺。

真实 Qcxt OCR 仍独立用 4.153.1：500² PNG→OCR 60.34 ms 对原版 59.85 ms；原始像素控制组波动约 3.8%，不据此证明普遍持平。9 对 PNG 像素、18 次完整识别输出一致。JPEG、透明数据语义和缺少的 SKStream/SKPixmap 仍阻止直接替换完整模块。未改目标仓库。

本轮前后 CPU 各 1260 批、GPU 各 720 批；最终 OCR 36 进程/180 批。源文件与 shader SHA、逐帧 ticks、CSV/JSON 和日志全部保留。后续重点为原版曲线/字体画质、CPU 动态文字、GPU 索引跨帧复用与内存，以及剩余解码/API；Linux GPU 尚未实机验证。

## 2026-10-12 起的历史记录

最新优先级：先让路径、抗锯齿、字体、编解码、SIMD、GPU 六类都有真实实现。GPU 指定 Windows/Linux Vulkan；字体须包含中文 TrueType 和基本中英文排版；字体/编解码全部 C#。

| 领域 | 实际实现 | 剩余差距 |
| --- | --- | --- |
| 调查/基础链路 | 精确基线、bitmap/canvas/surface/snapshot/矩阵/stride/Alpha | 403/3235 签名匹配；Avalonia 应用未集成，二进制不兼容 |
| 路径 | Move/Line/Quad/Cubic/Close、矩形/圆、四种 fill rule、仿射变换、butt/miter 描边 | conic/arc/path ops、其他 cap/join、StrokeAndFill 缺失 |
| AA | CPU 标量 16×16 覆盖率，GPU 使用同样质量；独立面积验证 | 原版曲线/文字差异超预设阈值；一般 path clip/AA clip 缺失 |
| 渐变/图片 | 两色 Clamp 线性渐变及 paint Alpha、硬矩形裁剪/状态恢复、最近邻 | 多停止点/其他渐变/高阶采样/滤镜/更多色彩处理缺失 |
| 字体/文字 | glyf、cmap 4/12、metrics、简单/XY composite 字形；真实中英文、对齐、基本换行/词与 CJK 边界折行 | hint VM、GSUB/GPOS/Bidi/复杂脚本、CFF/TTC/可变/彩色字体、点附着、字体回退缺失并明确拒绝 |
| 编解码 | 纯 C# PNG 与 stored/fixed/dynamic DEFLATE；RGBA/RGB/gray/gray-alpha/palette、适用 1/2/4/8 位、五种过滤、CRC/tRNS/gAMA | 编码用无损 stored 块，文件较大；16 位/Adam7/APNG/ICC/JPEG/WebP 等缺失 |
| SIMD | uint/Vector<T> 连续填充、预乘 Alpha 合成；逐字节验证及禁用指令回退通过 | 见 OPTIMIZATION.md；动态覆盖率、Straight Alpha 合成、PNG decode 仍有性能差距 |
| Vulkan | GPU 覆盖/采样/合成、128 命令批次、真实 VkImage/view/sampler 缓存、staging/layout barriers、离屏/fence/线程/错误状态/释放 | 只在 Windows NVIDIA 实机验证；Linux loader/ABI 分支已实现未运行；swapchain/真实设备丢失待补；原版 GPU 同机比较见 PERFORMANCE-GPU.md |

examples/Features 的 CPU/Vulkan 组合示例均已运行。GPU 出错后进入 faulted 状态，拒绝返回“成功的新帧”，需 Dispose 后重建，无隐式 CPU fallback。

本轮验收：36 原有、14 扩展测试通过；SIMD 回退通过；GPU 比较与生命周期检查通过；25 原版行为探针一致。120 原版像素场景中 108 达标、12 不达标。微基准只支持清屏内核比较，未证明全引擎更快。

下一步处理曲线/文字覆盖率兼容差异和 Linux Vulkan 实机验证，再补路径/clip/采样、字体 shaping/hinting 和更多格式。六类已实现可运行链路，但阶段三至六的全部 API 和验收尚未完成。

## 首次原版性能对比（优化前）

已完成独立进程 CPU 多场景对比，旧结果见 [PERFORMANCE-BEFORE-OPTIMIZATION.md](PERFORMANCE-BEFORE-OPTIMIZATION.md)。原版、显式清屏 SIMD 关闭/开启各 3 个进程；12 场景×2 尺寸×3 实现×3 进程×7 批，实际 1512 批，保存逐帧 ticks、像素、CSV/JSON、日志和图表。保持原版/替代相同代码与输入，PNG decode 强制相同输出布局。

结果显示清屏接近原版，其余已通过质量 gate 的矩形/路径/AA/解码存在明显性能和分配差距。新基准还发现密集混合、复杂采样与该字体/大小下文字的像素误差，未扩大门槛或算作可比速度优势。PNG 压缩策略不同单列诊断。当前仍未有原版 GPU 性能对照。

## 本轮 CPU 优化

已实际改动矩形/AA/路径/描边/字体布局/渐变/图片合成及 PNG/DEFLATE 热点，详见 [优化前后实测](OPTIMIZATION.md)。暖缓存不作为全部场景的性能承诺，另有超过容量、持续移动的路径/中英文文字对照 [PERFORMANCE-CHANGING.md](PERFORMANCE-CHANGING.md)。动态曲线约 2.25 ms、移动中英文约 9.56 ms，仍有明显成本。

本轮保留 16×16 AA、整数合成舍入、gamma 和 CRC/Adler；5267 项冻结旧实现/独立 fixture 检查在硬件 SIMD 与禁用硬件模式通过，既有行为/功能/GPU 检查通过。对原版曲线/文字的既有画质失败仍保留；尚未完成全 API、复杂字体、更多编解码、Linux Vulkan 及原版 GPU 性能比较。

## 当前修复版本复评

已重新实测 CPU 1764 批、GPU 1008 批，并进行 HOST_CACHED 目标内存隔离实验 504 批，报告见 [REASSESSMENT.md](REASSESSMENT.md)。补充 CPU ms/帧、单核/机器 CPU% 口径、private/working set、分配/GC；修复基准采样器对象混入分配量以及 CPU 结束读取落在内存采样之后的问题。源码、GLSL/SPIR-V 和配置哈希随运行保存并核验稳定。

CPU 核心与上轮相同，基础场景已接近原版，但移动中英文约 9.59 ms、1.08 MiB/帧分配。当前 GPU 清屏约 11.08 ms，缓存内存实验约 1.23 ms；四行中英文仍约 0.9–1 秒。实验 72 个保存帧与当前 GPU 字节一致，实验不在生产引用图，尚未合并其分配策略。生产代码仅修复边缓冲 descriptor offset 后 range 越界。

原版画质复测仍 108/120 通过，12 个曲线/文字失败；性能场景还暴露大型图片/密集混合质量差异，未提高容差。建议保留受控 CPU 子集，完整 Avalonia 替换暂不投入生产；GPU 文字须先验证瓦片边列表/字形缓存，基础回读有已测出的优化空间。下一步取舍及完整资源数据见评估报告。

## Qcxt OCR 专项调查

读取真实目标 F:/基础/qcxt.framework/src/Qcxt.OCR.SkiaSharp 后确认其依赖 SkiaSharp 4.153.1，区别于本引擎原来的 3.119.4 兼容基线。模块只做解码/像素接入，OCR 核心自行预处理和推理；绘图/字体/Vulkan 性能不决定这条流程。

已完成实际 ONNX 模型、500×500 真实夹具的 36 个独立进程/180 批 CPU 基准，详见 [OCR-PERFORMANCE.md](OCR-PERFORMANCE.md)。原版 PNG 解码约 2.29 ms，当前 SimdSkia 约 7.64 ms；PNG→OCR 约 64.43→72.54 ms，约慢 13%。所有不透明 PNG 像素与 18 次 OCR 验证结果逐项一致（16 行文本/框/置信度）。

完整扩展对当前 SimdSkia 编译失败，SKStream/SKPixmap 等尚缺；JPEG 夹具原版成功、SimdSkia 拒绝；透明 PNG 的物理预乘数据语义不同。尚不构成 4.153.1 全 API 或二进制兼容，原目标库无修改。建议保持原版解码，已有原始像素则直接使用 Qcxt.OCR.Run；纯 C# 强约束下应把目标缩至托管解码接入，不需要先完善整个渲染引擎。
