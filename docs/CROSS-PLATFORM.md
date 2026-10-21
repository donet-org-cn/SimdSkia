# 跨平台桌面接入（开发中）

新增源码项目 `src/SimdSkia.Avalonia.Desktop`，为固定 Avalonia 12.1.4 提供统一入口：

```csharp
AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
```

引用此桌面项目后，无需在应用中固定调用 `UseWin32`。Windows 选择 Win32 软件宿主，Linux 选择 X11 软件宿主（Wayland 会话需 XWayland），macOS 选择官方 Avalonia.Native 软件宿主。图形、基础排版、字体解析及图片编解码仍使用 SimdSkia C# 实现，不安装或调用 Skia/HarfBuzz。

macOS 按用户最新确认允许随官方窗口宿主分发 `libAvaloniaNative.dylib`；此例外仅限窗口和系统交互，不是字体或绘图引擎。X11 使用系统 X11/GLib 等环境库；通过固定 PackageDownload 只引用官方 X11/Win32 托管组件，避免它们的 Skia/ANGLE 包依赖。

Linux/macOS 新增纯 C# 静态 TrueType 系统字体发现。只读取字体元数据后选择有界的默认、中文和等宽家族，不预加载所有已安装轮廓。自动选择的优先级不复刻 fontconfig/CoreText；可用 `SIMDSKIA_DEFAULT_FONT_FAMILY` 指定已安装家族，或继续用 `FontFiles` 显式配置。CFF、可变和彩色字体会列入发现结果的 Exclusions，未找到任何受支持字体会报错。

源码示例：

```powershell
dotnet run --project examples/CrossPlatform -c Release
dotnet run --project tests/PlatformFonts -c Release
dotnet run --project examples/CrossPlatform -c Release -- --smoke
```

首版 NuGet `0.1.0-preview.1` 不含这个桌面扩展，不得把源码新能力当作已发布版本能力。三平台工作流为 `.github/workflows/desktop.yml`：实际创建系统窗口并检查 framebuffer 帧、resize、按钮路由和非空像素，Linux 使用 Xvfb 虚拟 X11 显示。平台运行结果以工作流真实结果和截图为准；尚未运行的检查不计通过。

此阶段宿主采用软件 framebuffer 呈现；GPU 命令绘制接入、直接 GPU swapchain 呈现和性能测量分别记录，不能混为同一能力。
