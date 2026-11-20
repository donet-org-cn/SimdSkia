# SimdSkia 0.1.12

本版合并了 macOS Metal 窗口呈现、CJK 和 TTC 字体修复，以及 CPU 阴影和路径光栅化优化。0.1.11 的发布任务已取消，本版包含那次构建的改动。

```sh
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.12
```

- macOS 桌面优先使用 Metal，通过 Avalonia 原生宿主的 `CAMetalLayer` 呈现，支持 GPU 图层、模糊和投影。显式选择 Metal 严格模式时，首帧初始化失败会抛出原始异常。反向填充保留完整的变换与目标尺寸。感谢 [@239573049](https://github.com/239573049) 的实现、Apple M5 实机验证和回归测试。[PR #4](https://github.com/donet-org-cn/SimdSkia/pull/4) · [macOS 实机报告](MACOS-METAL-2026-11-20.md)
- 中文回退按区域选择 SC 或 TC 字体，显式指定的字体优先。TTC 发现和加载会保留同一集合中的正常字形，支持 Mac Roman 字族名称。[#5](https://github.com/donet-org-cn/SimdSkia/issues/5) · [#6](https://github.com/donet-org-cn/SimdSkia/issues/6)
- CPU 投影沿用原有高斯核，只对 alpha 通道做卷积。路径光栅化复用临时扫描行列表，并省去只需要像素遮罩时的扫描段分配。[#9](https://github.com/donet-org-cn/SimdSkia/issues/9) · [#10](https://github.com/donet-org-cn/SimdSkia/issues/10)

## 本机性能对照

在 Windows x64 上用 1024 × 640 的相同输入对照暂停前的 0.1.11 预检包。每项三轮独立进程，表内为中位数。每个场景的输出哈希一致。[逐轮数据](evidence/issue-fixes-2026-11-20.json)

| 指标 | 暂停前 0.1.11 | 0.1.12 |
|---|---:|---:|
| CPU 阴影耗时，SIMD（ms/帧） | 31.40 | 18.14 |
| CPU 阴影耗时，标量（ms/帧） | 253.81 | 88.82 |
| CPU 阴影分配，SIMD（KiB/帧） | 5.73 | 5.88 |
| 路径光栅化分配（KiB/帧） | 126.89 | 46.10 |

路径分配优化覆盖扫描段的临时构建。路径展平和缓存所需的分配仍存在。[#10](https://github.com/donet-org-cn/SimdSkia/issues/10)

## macOS 验证范围

macOS CI 的 Metal 默认模式仍有缩放后帧尺寸与窗口及 DPI 不匹配的记录。0.1.12 的 macOS 发布矩阵运行显式 CPU 路径，默认模式由跨平台工作流单独检查。遇到缩放问题可通过 `.UseSimdSkiaDesktop(options => options.UseCpu())` 选择 CPU。[缩放检查记录](https://github.com/donet-org-cn/SimdSkia/actions/runs/44722057615/job/136245074286)

十三个 NuGet 包统一为 0.1.12，目标框架为 .NET 10。桌面、GPU 和渲染适配器接受 Avalonia 12.1.0 及以上，Android 接受 Avalonia 12.1.5 及以上。[Avalonia 版本兼容说明](AVALONIA-VERSION-COMPATIBILITY.md)
