# SimdSkia 0.1.8

0.1.8 解除 Avalonia 的包依赖和运行时版本上限，加入 12.1.6 桌面宿主。十三个 NuGet 包统一为 0.1.8，面向 .NET 10。

```sh
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.8
```

| 组件 | Avalonia 最低版本 |
| --- | --- |
| 渲染适配器、桌面和 GPU | 12.1.0 |
| Android | 12.1.5 |

桌面优先选用与应用解析的 Avalonia 版本一致的宿主。没有对应宿主时使用随包最新宿主，构建日志会记录两个版本。后续 Avalonia 版本是否能正常运行取决于其接口兼容性。Android 随包提供适配过的 12.1.5 宿主，应用核心和主题包可以选用较新版本。

桌面发布矩阵覆盖 12.1.0–12.1.6 的 Windows、Linux X11 与 macOS 窗口，最低和最高版本执行 NativeAOT。Android 使用 Avalonia 12.1.6 验证独立包消费及 ARM64 Release APK，含 Mono AOT。[兼容性说明](AVALONIA-VERSION-COMPATIBILITY.md)

本版包含 0.1.7 的 Direct2D 位图 GC、字体、CPU 绘制与 Vulkan 优化。[0.1.7 性能更新](RELEASE-0.1.7.md) · [CPU、内存与 Win64 NativeAOT 报告](PERFORMANCE-AOT-CPU-MEMORY-2026-11-18.md)
