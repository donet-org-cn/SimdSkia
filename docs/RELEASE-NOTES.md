# SimdSkia 更新日志 / Release notes

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
