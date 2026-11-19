# SimdSkia 0.1.9

修复 OxyPlot.Avalonia 绘制坐标轴次刻度时报出的 `End the preceding figure.` 异常。流式几何允许连续调用 `BeginFigure`。每次调用都开始一段独立轮廓，上一段开放轮廓保持开放。

```sh
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.9
```

在 Windows x64 上使用 Avalonia 12.1.6 和 OxyPlot.Avalonia 2.2.0 验证了图表启动、两条曲线绘制及实时刷新。几何与控件回归通过 14,018 项断言，覆盖多段开放路径的长度、像素和间隔。

十三个 NuGet 包统一为 0.1.9，面向 .NET 10。Avalonia 渲染适配器、桌面和 GPU 包的依赖下限为 12.1.0，Android 为 12.1.5。[Avalonia 版本兼容说明](AVALONIA-VERSION-COMPATIBILITY.md)
