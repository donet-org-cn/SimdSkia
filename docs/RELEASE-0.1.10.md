# SimdSkia 0.1.10

本版减少 Avalonia 绘制时的托管分配和大对象堆留存，包含 OxyPlot 流式几何兼容修复。

```sh
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.10
```

- 对内容相同的连续裁剪扫描行共享一份存储。阴影保留完整模糊边缘，减少中间像素缓冲区。Direct2D 分块上传裁剪遮罩。
- 为轴对齐虚线使用紧凑的描边表示，复用小几何、字形和图像资源，减少重复的路径构建、像素拷贝与纹理上传。
- 降低透明度图层和 CPU 滤镜的临时缓冲区开销。OxyPlot 的真实 Direct2D 窗口首帧和更新帧分别与上一轮逐像素一致。

在本机 Windows x64 的 OxyPlot 样例中，三轮 45 秒刷新测试的托管分配中位数为 1.219 MiB/s，工作集为 161.7 MiB，完整 GC 后的大对象堆留存为 1.52 MiB。此前对 NuGet 0.1.9 的同一示例进行独立的 60 秒测试，结果分别为 37.79 MiB/s、290.1 MiB 和 20.18 MiB。原生 Avalonia 在本轮对照中的分配速率为 0.876 MiB/s，私有工作集比本版低 9.9 MiB。CPU 实测范围有重叠。

Win64 NativeAOT 软件控件示例的完整发布目录为 31.31 MiB，原生为 41.16 MiB。本次优化使可执行文件比上一轮本地构建增加 4 KiB。报告包含 Fluent 控件、列表滚动、分配调用栈和 GC 触发原因。[性能报告](PERFORMANCE-OXYPLOT-2026-11-19.md)

十三个 NuGet 包统一为 0.1.10，目标框架为 .NET 10。桌面、GPU 和渲染适配器要求 Avalonia 12.1.0 或更高版本，Android 要求 12.1.5 或更高版本。[Avalonia 版本兼容说明](AVALONIA-VERSION-COMPATIBILITY.md)
