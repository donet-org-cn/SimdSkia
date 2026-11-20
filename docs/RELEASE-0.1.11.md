# SimdSkia 0.1.11

macOS 桌面窗口默认优先使用 Metal，通过 Avalonia 原生宿主的 `CAMetalLayer` 呈现。Metal 不可用时会选择 CPU 渲染，也可以通过 `.UseSimdSkiaDesktop(options => options.UseCpu())` 显式选择 CPU。

```sh
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.11
```

- 支持 GPU 图层、高斯模糊和投影。窗口和图层使用共享像素缓冲区，减少 CPU 与 GPU 之间的复制。
- 轴对齐矩形和部分圆角矩形直接在 GPU 上计算覆盖率。复用其他路径的遮罩时，反向填充会保留实际变换和目标尺寸。
- 显式选择 `SimdSkiaGpuBackend.Metal` 并关闭 CPU 回退时，首帧初始化失败会抛出原始异常。覆盖率与严格模式都增加了回归测试。

作者在 Apple M5 和 Avalonia 12.1.6 的实机上验证了 Metal 窗口、实时动画、连续缩放和故障回退。独立的 Intel Mac、AMD GPU、HDR 与广色域验证仍待补充。[实机报告](MACOS-METAL-2026-11-20.md)

GitHub macOS CI 的默认模式在缩放窗口后捕获到与窗口及 DPI 不匹配的帧。0.1.11 的 macOS 发布矩阵验证显式 CPU 路径，默认模式仍由跨平台桌面工作流检查。遇到缩放问题时，可显式选择上面的 CPU 路径。[缩放检查记录](https://github.com/donet-org-cn/SimdSkia/actions/runs/44722057615/job/136245074286)

十三个 NuGet 包统一为 0.1.11，目标框架为 .NET 10。桌面、GPU 和渲染适配器接受 Avalonia 12.1.0 及以上，Android 接受 Avalonia 12.1.5 及以上。[Avalonia 版本兼容说明](AVALONIA-VERSION-COMPATIBILITY.md)

感谢 [@239573049](https://github.com/239573049) 贡献 macOS Metal 支持及回归测试。[PR #4](https://github.com/donet-org-cn/SimdSkia/pull/4)
