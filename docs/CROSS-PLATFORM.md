# 跨平台桌面接入（0.1.0-preview.2）

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

首版 NuGet `0.1.0-preview.1` 不含这个桌面扩展；新项目引用 `SimdSkia.Avalonia.Desktop 0.1.0-preview.2`，需 GPU 时再添加 `SimdSkia.Avalonia.Gpu`。三平台实际窗口运行已通过 [CI 39645744951](https://github.com/hnlyf168/SimdSkia/actions/runs/39645744951)：Windows x64、Ubuntu 24.04 x64、macOS 26 ARM64。工作流 `.github/workflows/desktop.yml` 继续检查初次 framebuffer、呈现后 resize、按钮路由和非空像素；Linux 使用 Xvfb 虚拟 X11 显示。Native AOT 是独立检查项，结果见交付报告，不以 JIT 窗口通过代替 AOT 验证。

默认 CPU。Windows 可选择 `options.UseGpu(SimdSkiaGpuBackend.Direct2D)`，使用硬件 D3D11/Direct2D 和 DXGI 原生交换链；Vulkan 可在 Windows/Linux 绘制后等待 fence、回读并交窗口 framebuffer 呈现。macOS 的 Metal/GPU 尚未实现，配置 GPU 会明确报错。

默认 `ManagedGlyphAtlas`：C# 按设备像素与实际文本颜色生成/缓存文字纹理，保持现有覆盖率预混合，再由 GPU 合成。另有较慢的 `GpuOutlines` 选项。图层和滤镜仍可用 C#，不把其 CPU 工作计作 GPU 光栅化。`allowCpuFallback` 默认关闭；独立画刷变换等不能满足行为约束的功能必须明确启用回退。Direct2D 原生窗口不支持分层透明 HWND，需显式选择 `FramebufferReadback`。Linux Vulkan 窗口尚无本机硬件运行验收，Windows Vulkan 当前完整控件慢于 CPU。

GPU 硬件测试、三进程 CPU/内存对照、已知差距与复现命令见 [本轮报告](DESKTOP-GPU-2026-10-22.md)。
