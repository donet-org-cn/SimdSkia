# SimdSkia 更新日志 / Release notes

## 0.1.0-preview.8 — 2026-10-27

本版集中优化通用 x64 Native AOT 的真实 Avalonia 控件绘制，并补充原版 Vulkan / ANGLE 与 SimdSkia Vulkan / Direct2D 的完整帧对照。11 个包统一版本，Avalonia **12.1.4**、SkiaSharp 源码兼容基线 **3.119.4** 不变。

### 实现与分配优化

- 增加 SSE2 字形覆盖率、预乘图像合成和双线性采样路径，维持精确整数除法、浮点舍入、Alpha、stride 和尾部语义；保留 AVX2 及完整标量回退。
- 连续白色/透明缓冲、整宽矩形与 16–256 KiB 通用 x64 色块优化填充；黑色字形合成检查真实目标像素后走等价路径。
- 字形串缓存使用不可变身份快速查询、有界第二次机会淘汰，减少大键复制与 GC 引用写屏障；串预算 2→4 MiB，字形预算保持 8 MiB。缓存预算不是进程内存上限。
- 无实际透明图层时减少脏区记账；纯平移字形省去未使用的矩阵求逆。紧密 framebuffer 整块复制，带 padding 的缓冲保留逐行路径。
- 画布中的路径、矩形与渐变缓存容器按需创建。60 FPS 滚动的托管分配由 preview.7 的 **155.14 降到 28.05 KiB/帧**；原版为 **12.87 KiB/帧**。

### 性能实测与边界

同机 Windows x64 / i7-8700K / GTX 1050 Ti，固定 Fluent 控件树及中英文字体。CPU 共 75 个串行进程（各 5 次，预热 2 秒、采样 5 秒），GPU 共 36 个（各 3 次，预热 3 秒、采样 5 秒）。生产绘图实现对应报告中冻结的候选源码，发布只更新版本、依赖锁定和文档。

| 项目 | 结果 |
| --- | --- |
| 通用 x64 AOT CPU 吞吐 | 控件 100% / 150% DPI 与滚动列表综合耗时比原版低 **4.4%**，最慢场景落后 **3.8%** |
| CPU 含 60 FPS 的全部五场景 | 综合耗时比原版低 **6.7%**；各场景相对 preview.7 耗时减少 **50.2%–62.7%** |
| Vulkan / Direct2D | JIT 离屏完成帧含等待与回读，综合耗时比每例较快原版 GPU 低 **29.8% / 35.3%** |
| 工作集 | 普通控件页 **79.89 MiB**，原版 **60.91 MiB**；未追平 |

综合比取场景耗时比的几何平均数。GPU 不代表 AOT、真实窗口呈现或显示器帧率；不能将此结果推广到其他硬件、任意业务界面或所有图形操作。CPU、内存、分配及未通过项见[完整报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/PARITY-OPTIMIZATION-2026-10-27.md)。

### 验证与已知问题

- 471,569 项精确检查通过（含字体）；SSE2 限制、完全标量各 471,557 项。真实 Avalonia 9,741、Direct2D 344、Vulkan 内核 540、GPU 集成 12,174 项通过。
- 真实通用 x64 Native AOT 460,050 项采样检查通过，并从外部验证 PE 与进程模块。发布构建及三平台 JIT/AOT/nupkg 消费结果见[发布记录](https://github.com/hnlyf168/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)。
- CPU 页面优化前后逐字节相同，但原版整页画质通过数仍为 **CPU 0/5、GPU 0/12**，完整画质未达标。字体指令处理和字形边缘覆盖率仍有差异，没有降低画质门槛或把失败改判为成功。
- 常驻内存仍高于原版，完整 SK* API 仅匹配 425/3235 公共条目；不承诺二进制兼容或任意 Avalonia 应用直接替换。macOS GPU 未实现，Vulkan 窗口路径仍有回读。
- 本次是在公开这些限制后发布的**实验性预览版**，不代表完整兼容验收通过。完整验收脚本保留失败状态，不能把 NuGet 发布流水线成功等同于画质全绿。

### 升级与示例

所有直接引用的 SimdSkia 包统一为 `0.1.0-preview.8`。桌面通常安装 `SimdSkia.Avalonia.Desktop` 并调用 `.UseSimdSkiaDesktop()`；需要 GPU 时另加同版本 `SimdSkia.Avalonia.Gpu`，配置方法见包内 README。无需重新编译 Avalonia。

新增默认 Avalonia AOT 体积示例、CPU/GPU 对照工具、独立字体轮廓诊断与带图的博客园文章。默认空白模板 **36.53→19.51 MiB** 是明确标注的 **preview.7 历史实测**，未冒充 preview.8 的体积测量；旧版本样例及数据保持可复现。

## 0.1.0-preview.7 — 2026-10-26

相对于 preview.6，新增以下实现与修复。11 个包统一版本；Avalonia 仍锁定 **12.1.4**，SkiaSharp 源码兼容基线仍为 **3.119.4**。本文件随每个 NuGet 包以 `CHANGELOG.md` 提供，NuGet 页面另有发行说明和包内 README。

### CPU 图片采样优化

- 新增每批八像素的 AVX2 双线性采样：连续目标写入、源行有界 gather，保留浮点操作顺序、舍入及预乘 Alpha 约束。
- 支持已实现的 RGBA/BGRA、非预乘/预乘/Opaque 输入；向量尾部继续走 SSE 或标量。没有 AVX2 的硬件仍可运行；通用 AOT 不保证启用 AVX2。
- 不增加公共 API，不需要调用方改变绘图代码。绘图、字体、编解码仍为 C#，原版 SkiaSharp 仅用于隔离的对照测试。

### 画质与 Vulkan 修复

- 修复非预乘、预乘半透明图片的 SourceOver 舍入，匹配固定基线 Windows x64 图像流水线。此前两类 RMSE 超标现已通过，未放宽质量阈值。
- 统一现代最近邻、线性过滤、原尺寸图像拷贝及裁剪图层回贴，修复高 DPI 图层边缘不一致。
- 同步 Vulkan 图像透明合成，并修复 CoverageClip 存在时的采样路径选择；已更新 SPIR-V。普通颜色、渐变和 Mitchell 路径保持各自算法。

### 实测结果与适用范围

同机 i7-8700K / Windows x64 / .NET 8.0.32，固定 SkiaSharp 3.119.4；512×384 源图缩放到 1024×768，包含清屏与线性过滤，各三进程中位数：

| 图片场景 | 本版 ms | 原版 ms |
| --- | ---: | ---: |
| 非预乘不透明 | 6.667 | 7.714 |
| 非预乘含透明 | 6.758 | 7.617 |
| 预乘含透明 | 5.412 | 7.404 |
| Opaque 元数据 | 5.816 | 6.804 |

这是特定 CPU 图片微基准，不能推广为所有 Avalonia 页面、硬件或 GPU 性能。核心图片对照 **24/24** 通过原门槛（最大通道误差 ≤2、RMSE ≤0.5）；SIMD 与标量逐字节一致。半透明场景最大误差降到 1，RMSE 约 0.05–0.11。

前一轮 AVX2 优化的独立测试中，1024×768 非预乘不透明图片由 46.317 降至 7.423 ms；该轮真实 Avalonia 页面耗时改善约 3%–5%。这组历史数据与上表分开，不拼接不同轮次计算收益。**本轮没有重测完整 Avalonia 稳态吞吐或 GPU 性能，常驻托管内存没有明显改善。** 原版微基准保留公共 fixture 开销，其进程内存不用于引擎排名。

### 验证与示例

- 468,137 项优化回归通过；关闭 AVX2、关闭全部硬件指令同样通过。其中独立原版样本有 12,288 个组合，跨格式/采样/尾部累计回放 393,216 次。
- 9,734 项 Avalonia 控件检查、344 项 Direct2D 检查、590 项 GPU 采样、540 项 GPU 内核、599 项 GPU 文字链检查通过。
- 通用与 x86-64-v3 实际 Native AOT 各通过 460,050 项采样检查；外部核对未加载 CLR/JIT。
- 新增源码示例 `examples/NativeWindow` 和 `examples/NativeControls`：不依赖 Avalonia 的 Win32 自绘窗口、发光文本框/按钮和动画进度条。示例程序、字体不捆绑进库包；历史 AOT 体积报告注明测量时间与配置。

### 升级及已知限制

将所有直接引用的 SimdSkia 包统一为 `0.1.0-preview.7`，Avalonia 统一保持 `12.1.4`。通常桌面应用只需引用 `SimdSkia.Avalonia.Desktop`，可选另加同版本 `SimdSkia.Avalonia.Gpu`；接入代码见包内 README。

完整公共 API、字体/路径/整页画质仍有缺口；完整原版对照验证仍返回质量失败状态，不能作为原版完全替代或二进制兼容承诺。原版像素样本固定 Windows x64，未承诺所有平台逐像素等价。macOS GPU/Metal 尚未实现；Vulkan 仍需系统 loader/驱动并有 framebuffer 回读。系统字体支持范围及 macOS 窗口宿主原生例外保持原说明。

详细报告：[最终画质及性能复测](https://github.com/hnlyf168/SimdSkia/blob/main/docs/ALPHA-COMPATIBILITY-2026-10-26.md)、[AVX2 优化历史对照](https://github.com/hnlyf168/SimdSkia/blob/main/docs/CORE-PERFORMANCE-2026-10-26.md)、[验证与差距](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VALIDATION.md)。公开发布状态以[发布记录](https://github.com/hnlyf168/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)为准。

## 0.1.0-preview.6 — 2026-10-22

系统字体分页、共享 TTC、光栅缓冲复用、图片画刷缓存生命周期修复、几何边界/字形缓冲复用，以及经典桌面 JIT 后台启动准备。缩短启动白色阶段但未彻底消除；不宣称该轮持续 GPU 性能或常驻内存明显改善。此前发行历史见仓库发布记录。
