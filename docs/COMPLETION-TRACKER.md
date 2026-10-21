# 持续实施与最终验收清单

固定基线 Avalonia 12.1.4 / SkiaSharp 3.119.4。引擎、字体、编解码必须是 C#；原版仅在隔离对照进程。当前开发不按小函数要求用户确认，最终报告保留未通过项。

2026-10-22 preview.2 更新：[桌面与 GPU 报告](DESKTOP-GPU-2026-10-22.md)补充三平台实际窗口、自动字体、Windows Direct2D 原生交换链、Vulkan 窗口回读呈现和新性能数据。下表的旧窗口缺口以该报告为准；完整画质和 API 缺口仍保留。macOS 官方原生窗口宿主已按用户最新确认允许，绘图/字体/编解码继续为 C#。

| 工作面 | 本轮状态 | 验收依据 |
|---|---|---|
| 图片/视觉画刷、stretch/source/destination/tiling/transform | 已有实际 CPU 实现，原版仅 20/50 场景通过 | 高 DPI/采样相位仍失败，不能视为完整画刷兼容 |
| Blur、DropShadow、内外 BoxShadow | 已有实际实现，AVX2 + 标量和独立卷积检查通过 | 原版 Blur 7/8、DropShadow 8/8、BoxShadow 24/48；圆角阴影仍失败 |
| 更多混合模式 | 未完成 | 目前 Core 仅 SourceOver/Source/Clear；其他图片 blend 明确拒绝 |
| PNG Adam7/16 位/颜色配置 | 已实现 Adam7、低/高位深、部分 ICC、纯 C# LZ77 压缩 | 484 图片检查及 33 个独立 zlib 对照通过；全部 ICC 模型/intent/生命周期尚未验收 |
| JPEG 与常用图片格式 | 已实现 8 位 sequential/progressive 常见 JPEG 与基线 CMYK | 24 个原版 Alpha 夹具；四分量渐进拒绝，YCCK 未独立验收；WebP/GIF/BMP/APNG 未实现 |
| 字体格式、替换与定位特性 | 增加 glyf TTC v1/v2、face 与 sfnt 提取校验 | 基本中英及已有 kern 通过；CFF/变量/彩色、hint/LCD/复杂 OpenType/RTL 未完成 |
| 层缓冲内存和 CPU 热点 | 已实现裁剪区域层、halo、原点与单色热路径；卷积展开 SIMD | ROI 与全层像素相同；原有完整 UI 帧前后逐字节一致，整体内存差距仍在 |
| 完整 CPU 画质 | 未通过 | 旧图像 108/120；完整 UI 0/3；新功能 59/114，原阈值保留 |
| Vulkan 新命令和窗口呈现 | 已实现裁切/三种采样/覆盖 clip/细曲线、多色渐变、文字纹理与窗口回读呈现；直接 swapchain 未完成 | 原回归及新 GPU 集成 12172 断言通过；原版 GPU 12/24 的旧门槛未改判通过，Linux 物理 GPU 未实测 |
| 实际 Avalonia 控件与窗口回归 | 本机硬件与全标量 8109 项；三平台 JIT/AOT/真实 nupkg 窗口通过 | CI run 39647833834；Windows Direct2D/Vulkan GPU 和 AOT 在本机硬件单独通过，macOS GPU 未实现 |
| API/依赖/构建/完整测试报告 | 本轮已运行并交付，完整兼容 FAIL | 425/3235 匹配、2810 缺失、0 额外；二进制不兼容；生产无原生字体/编解码 |
| CPU/GPU 性能与内存报告 | 已交付三进程同机数据、工作集/Private/分配/CPU 与失败 gate | CPU 控件、Gaussian 内核、Vulkan 各自比较；GPU 等待 fence + 回读 |

Skia 专属 native handle、自绘扩展和二进制程序集身份不伪造；任何无法直接兼容的项目必须继续列入兼容报告。完整目标应用集成需要应用源码，受测控件不等于任意应用已验收。

报告：[测试](TEST-REPORT-2026-10-21.md)、[CPU/内存/滤镜](PERFORMANCE-FINAL-2026-10-21.md)、[GPU](PERFORMANCE-GPU-FINAL-2026-10-21.md)、[实际算法与支持范围](IMPLEMENTATION-DETAILS-2026-10-21.md)。持续目标仍未全部完成，不将本表未完成项隐藏或改为成功。
