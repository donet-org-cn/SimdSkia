# SimdSkia.Avalonia

最新 [采样、画刷缓存与窗口对比报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/OPTIMIZATION-ROUND6-2026-10-22.md)：系统字体控件 **0.793→0.694 ms/帧**；连续更新图片画刷短批次 **7.553→1.681 ms/帧**，该压力场景 GC 后 Private **58.6→27.9 MiB**，原版 **0.729 ms、26.5 MiB**。新增精确双线性 SIMD、抗锯齿计数优化、不透明画刷行复制，修复旧图片版本持有及矩形覆盖图重复缓存。**普通控件内存、滚动、GPU、启动仍有差距，完整画质/API 门槛未通过；尚未发布新 NuGet，下方 preview.5 保持原内容。** 报告区分短批次与稳态测量，保留全部进程指标、失败项及 CPU/GPU 分开对照。

上一轮 [启动、CPU 与内存报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/STARTUP-PERFORMANCE-2026-10-22.md)：系统字体控件 **1.008→0.800 ms/帧**，工作集 **111.9→94.8 MiB**。CPU 白色阶段估计 **280→218 ms**，原版约 **95 ms**；白屏未消除。这是此前独立测量，不与最新表格拼接计算增益。

上一轮 [内存与性能报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/MEMORY-PERFORMANCE-2026-10-22.md)：系统字体 CPU 窗口工作集 **252.6→116.0 MiB**，Private **197.6→60.6 MiB**；固定字体 CPU 控件耗时下降 5%–9%，每帧分配减少 66%–70%。该轮优化也尚未发布到 NuGet。

最新 [Vulkan 专项优化与测试报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VULKAN-OPTIMIZATION-2026-10-22.md)：真实控件窗口 P50 22.250→2.316 ms，约快 9.6 倍；同场景 CPU 2.067 ms、Direct2D 1.552 ms。包含 CPU、工作集、Private、分配、原版 GPU 对照及失败项。**本轮优化纳入 preview.5，发布状态见下方发布记录；CPU/内存没有获得同等幅度改善，完整画质/API 验收仍未通过。** 上一轮 CPU 与分配优化见 [历史报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/PERFORMANCE-COMPATIBILITY-2026-10-22.md)。

独立 C# 2D 引擎，内部使用 `SimdSkia`，源码兼容层保留 `SkiaSharp.SK*`。不使用 Google Skia 绘图；字体、基础排版及图片编解码均由 C# 实现。通过 `SimdSkia.Avalonia` 可以接入 Avalonia，无需重新编译 Avalonia。

本轮版本为 **`0.1.0-preview.5`**，固定支持 **Avalonia 12.1.4**。Windows、Linux X11/XWayland、macOS 已通过真实窗口验证；macOS 使用经用户确认允许的官方原生窗口宿主，绘图、字体、排版和编解码仍是 C#。默认使用 CPU，可选 Windows Direct2D 原生交换链或 Windows/Linux Vulkan 绘图。**macOS GPU/Metal 尚未实现，完整兼容性和画质验收仍未全部通过。**

## NuGet 包怎么选

本轮包含下列十一个包，统一版本 `0.1.0-preview.5`；发布状态见 [预览发布](https://github.com/hnlyf168/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)。在 Visual Studio 的 NuGet 搜索中勾选“包括预发行版”。

| 包名 | 用途 |
| --- | --- |
| [SimdSkia.Avalonia.Desktop](https://www.nuget.org/packages/SimdSkia.Avalonia.Desktop/0.1.0-preview.5) | **桌面应用优先选这个**：自动选择 Windows/Linux/macOS 宿主，传递引入渲染适配器 |
| [SimdSkia.Avalonia](https://www.nuget.org/packages/SimdSkia.Avalonia/0.1.0-preview.5) | 绘图、字体和基础文字排版服务，也可配合自行配置的宿主 |
| [SimdSkia.Avalonia.Gpu](https://www.nuget.org/packages/SimdSkia.Avalonia.Gpu/0.1.0-preview.5) | 可选 Avalonia GPU 绘图、Direct2D 直接呈现和回退/缓存诊断 |
| [SimdSkia.Gpu.Direct2D](https://www.nuget.org/packages/SimdSkia.Gpu.Direct2D/0.1.0-preview.5) | Windows Direct2D/D3D11，纯 C# 系统 API 互操作，无第三方绑定包 |
| [SimdSkia](https://www.nuget.org/packages/SimdSkia/0.1.0-preview.5) | 需要 `SkiaSharp.SK*` 源码兼容 API 时使用的依赖汇总包 |
| [SimdSkia.Compatibility](https://www.nuget.org/packages/SimdSkia.Compatibility/0.1.0-preview.5) | `SKBitmap`、`SKCanvas` 等兼容层，也由 `SimdSkia` 汇总包引入 |
| [SimdSkia.Core](https://www.nuget.org/packages/SimdSkia.Core/0.1.0-preview.5) | 几何、路径和绘图状态 |
| [SimdSkia.Cpu](https://www.nuget.org/packages/SimdSkia.Cpu/0.1.0-preview.5) | CPU 光栅化、像素合成及 SIMD |
| [SimdSkia.Text](https://www.nuget.org/packages/SimdSkia.Text/0.1.0-preview.5) | TrueType 字体解析和基础排版 |
| [SimdSkia.Imaging](https://www.nuget.org/packages/SimdSkia.Imaging/0.1.0-preview.5) | 图片编解码和颜色处理 |
| [SimdSkia.Gpu](https://www.nuget.org/packages/SimdSkia.Gpu/0.1.0-preview.5) | Vulkan 离屏后端，需要系统 Vulkan loader/驱动 |

新桌面应用添加 `SimdSkia.Avalonia.Desktop`，其渲染服务和引擎依赖会自动还原，无需额外安装 `SimdSkia` 或手动复制底层 DLL：

```powershell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.5
```

**安装包后还必须配置启动代码。** 以下是完整的新程序示例；现有项目按后面的迁移步骤调整。

## Avalonia 快速开始（Windows / Linux / macOS）

使用 .NET 10 SDK，在空目录创建以下两个文件。所有 Avalonia 包统一为 **12.1.4**，无需克隆或编译本仓库。

**Demo.csproj**：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SimdSkia.Avalonia.Desktop" Version="0.1.0-preview.5" />
    <PackageReference Include="Avalonia" Version="[12.1.4]" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="[12.1.4]" />
  </ItemGroup>
</Project>
```

**Program.cs**：

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using SimdSkia.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
        => AppBuilder.Configure<App>()
            .UseSimdSkiaDesktop()
            .StartWithClassicDesktopLifetime(args);
}

public sealed class App : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "SimdSkia 预览",
                Width = 600,
                Height = 300,
                Content = new StackPanel
                {
                    Margin = new Thickness(24),
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock { Text = "你好，SimdSkia / Hello Avalonia", FontSize = 24 },
                        new Button { Content = "普通 Avalonia 按钮" }
                    }
                }
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

在该目录运行：

```powershell
dotnet restore
dotnet run
```

Windows 的 `WinExe` 避免额外控制台窗口。三平台自动发现受支持的已安装静态 TrueType 字体，无需附带字体资源；中文需要相应字形的已安装字体。Linux 需要 X11 或 XWayland 及系统窗口库。macOS 会分发官方 MIT `libAvaloniaNative.dylib`，只负责窗口和系统交互。字体解析、文字栅格化及编解码均由 C# 完成。

## Avalonia GPU 与 Direct2D

在上面的桌面程序中增加可选包：

```powershell
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.5
```

Windows 将 `.UseSimdSkiaDesktop()` 改为：

```csharp
.UseSimdSkiaDesktop(options => options.UseGpu(SimdSkiaGpuBackend.Direct2D))
```

通过 C# 调用系统 Direct2D/D3D11，并使用 DXGI 原生交换链呈现；正常帧不强制回读像素，GPU 完成以事件查询确认。文字默认由 C# 生成并缓存覆盖率纹理，再交 GPU 合成；透明图层、滤镜等仍有 CPU 工作，诊断分别记录。可选择 `GpuOutlines` 进行轮廓 GPU 绘制，该模式当前更慢且文字覆盖率与默认模式不同。

Linux/Windows 可改用 `SimdSkiaGpuBackend.Vulkan`，需硬件 Vulkan 设备；当前通过完成栅栏后回读并交窗口 framebuffer 呈现。**main 源码优化后，本机完整控件场景约比 CPU 多耗时 12%，保持实验性质；macOS 调用 GPU 配置会明确报错。** 不支持的画刷等默认报错；可显式设置 `allowCpuFallback: true`，并通过 `SimdSkiaGpuDiagnostics` 查看原因、回退数及 Vulkan 覆盖缓存和同步计数。详见 [跨平台说明](https://github.com/hnlyf168/SimdSkia/blob/main/docs/CROSS-PLATFORM.md) 和 [本轮性能报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VULKAN-OPTIMIZATION-2026-10-22.md)。

## 现有 Avalonia 项目如何切换

1. 将 Avalonia 系列包统一为 **12.1.4**，添加 `SimdSkia.Avalonia.Desktop`；需要 GPU 时再添加 `SimdSkia.Avalonia.Gpu`。
2. 移除 `Avalonia.Desktop`、`Avalonia.Skia`、`Avalonia.Fonts.Inter` 等原版后端/字体引用及 `.WithInterFont()`，使用上述包配置；检查第三方控件是否仍间接引用 Skia/HarfBuzz/ANGLE。
3. 添加 `using SimdSkia.Avalonia;`，将 `.UsePlatformDetect()` 或固定 `.UseWin32()` 链替换为 `.UseSimdSkiaDesktop()`。保留自己的 `App`、XAML、MVVM 和窗口创建代码。

`.UseSimdSkia()` 注册 Avalonia 的绘图、字体和基础排版服务，因此无需修改 Avalonia 源码。它不会把 `SkiaSharp.dll` 替换为同名程序集；直接依赖原版 SK*/GR API 或 `ISkiaSharpApiLeaseFeature` 的自定义控件仍需迁移。

如需显式指定受支持的 TTF/TTC，将 `.UseSimdSkia()` 改为：

```csharp
.UseSimdSkia(options => options.FontFiles.Add(@"C:\Fonts\MyFont.ttf"))
```

显式列表会替代自动系统字体列表；随程序分发字体时，应设置复制到输出/发布目录并遵守字体许可证。Linux/macOS 按受支持静态字体的优先级选择默认家族，可用 `SIMDSKIA_DEFAULT_FONT_FAMILY` 指定已安装家族，不复刻 fontconfig/CoreText 的所有规则。更多说明见 [Avalonia 包内 README](https://github.com/hnlyf168/SimdSkia/blob/main/src/SimdSkia.Avalonia/README.md)。

## Avalonia Native AOT 发布

在目标平台安装 Native AOT 工具链后执行对应命令。Windows 需要 Visual Studio C++ 和 Windows SDK，Linux 需要 clang/zlib 开发工具，macOS 需要 Xcode 命令行工具。AOT 发布必须在相应操作系统完成：

```powershell
dotnet publish -c Release -r win-x64 -p:PublishAot=true -o publish/win-x64
dotnet publish -c Release -r linux-x64 -p:PublishAot=true -o publish/linux-x64
dotnet publish -c Release -r osx-arm64 -p:PublishAot=true -o publish/osx-arm64
```

运行目标目录中的 `Demo.exe`（Windows）或 `Demo`（Linux/macOS），无需安装 .NET 运行时。业务代码仍需满足 AOT 要求。若没有窗口，检查 `desktop.MainWindow` 和单实例提前退出逻辑。macOS 正式分发还需自行准备应用包、签名和公证。

上述两个文件与包内 README 保持一致。流水线、许可证、真实 nupkg 的消费验证与发布状态见 [预览发布说明](https://github.com/hnlyf168/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)；本轮平台测试与性能见 [交付报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/DESKTOP-GPU-2026-10-22.md)，画质和 API 剩余缺口见 [兼容验证](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VALIDATION.md)。

## 示例、实现与测试报告

新增 [Avalonia Native AOT 双后端程序](https://github.com/hnlyf168/SimdSkia/blob/main/examples/AvaloniaAot/README.md)：相同控件分别使用 SimdSkia 与原版，提供 Windows x64 通用及 AVX2 版本，实际比较完整发布体积、启动/绘制速度、CPU 和内存。结果见 [AOT 实测报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/AVALONIA-AOT-COMPARISON.md)；既有报告是另一套 JIT/离屏基准，数据不混用。

SK* 源码兼容的固定基线是 Avalonia.Skia 12.1.4 实际解析的 SkiaSharp 3.119.4。Core/Cpu/Text/Gpu 无第三方 NuGet 依赖，Imaging/Compatibility 使用固定版本纯托管 JpegLibrary；可选 Avalonia 层另有纯托管依赖，见 [许可证与依赖](https://github.com/hnlyf168/SimdSkia/blob/main/docs/DEPENDENCIES.md)。

路径、抗锯齿、基本中英文 glyf TTF/TTC 字体、PNG/JPEG/部分 ICC、图像/视觉画刷、模糊/阴影、SIMD、Vulkan 离屏及窗口回读呈现、Direct2D 原生交换链均有真实实现。**完整 Avalonia 替换尚未通过验收**：文字/曲线及部分新功能画质仍失败，通用字体排版、Vulkan 直接交换链和 macOS GPU 等仍有缺口。

2026-10-21 历史交付：[测试报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/TEST-REPORT-2026-10-21.md)、[CPU/内存/滤镜性能](https://github.com/hnlyf168/SimdSkia/blob/main/docs/PERFORMANCE-FINAL-2026-10-21.md)、[原版 Vulkan GPU 对比](https://github.com/hnlyf168/SimdSkia/blob/main/docs/PERFORMANCE-GPU-FINAL-2026-10-21.md)、[实现范围与缺口](https://github.com/hnlyf168/SimdSkia/blob/main/docs/IMPLEMENTATION-DETAILS-2026-10-21.md)。报告保留未通过项，不承诺替代原版强名称程序集。

2026-10-21 的 CPU、GPU、真实 OCR、CPU 时间和内存对比见 [本轮优化报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/OPTIMIZATION-2026-10-21.md)。保留优化前数据及失败的画质检查。

[真实 Avalonia 接入](https://github.com/hnlyf168/SimdSkia/blob/main/docs/AVALONIA-INTEGRATION.md)：`UseSimdSkia` 已运行实际控件与 Win32 软件窗口，8109 项控件/字形/滤镜/字体集合断言通过。较早的[字形/首绘优化](https://github.com/hnlyf168/SimdSkia/blob/main/docs/AVALONIA-TEXT-CACHE.md)及通用报告保留为历史数据；当前结论以本轮报告为准。

```powershell
dotnet build SimdSkia.sln -c Release
powershell -NoProfile -File tools/fetch-avalonia-font.ps1
dotnet run --project examples/AvaloniaControls -c Release
# Windows 交互窗口
dotnet run --project examples/AvaloniaControls -c Release -- --window
```

![SimdSkia 实际 Avalonia 控件](https://github.com/hnlyf168/SimdSkia/blob/main/docs/images/avalonia-text-cache-managed.png)

## 构建和运行

需要 .NET 10 SDK 和 .NET 8 runtime。

    dotnet build SimdSkia.sln -c Release
    dotnet run --project tests/SimdSkia.Tests -c Release
    dotnet run --project tests/Features -c Release
    dotnet run --project tests/Avalonia -c Release
    dotnet run --project examples/AvaloniaText -c Release
    dotnet run --project tests/Gpu -c Release
    dotnet run --project examples/Features -c Release
    dotnet run --project examples/Features -c Release -- --gpu

组合示例绘制渐变、曲线、孔洞、描边、中英文和经过 PNG 编解码的图片，输出 artifacts/features-cpu.png / features-vulkan.png。GPU 实际执行覆盖率与合成，等待 fence 完成后读取像素，没有 CPU fallback 冒充 GPU。

字体须是真实 glyf TrueType 文件。Windows 示例默认使用系统 SimHei，其他环境传入字体路径：

    powershell -NoProfile -File tools/fetch-test-font.ps1
    dotnet run --project tests/Features -c Release -- baseline/fonts/ZenOldMincho-Regular.ttf
    dotnet run --project examples/Features -c Release -- baseline/fonts/ZenOldMincho-Regular.ttf --gpu

固定 OFL 测试字体含 CJK/拉丁字形；区域字形风格应使用应用自己的中文字体。没有字体时明确失败或标记字体测试未执行，不生成假字形。独立 Vulkan 后端需要系统 loader/驱动：Windows vulkan-1.dll，Linux libvulkan.so.1；main 源码支持 64 位小端 RGBA/BGRA 预乘或 Opaque 存储目标，窗口直接输出 BGRA 并通过回读呈现，不提供 Vulkan swapchain 或 Skia GR 句柄替换。Windows Direct2D 另有原生 DXGI 交换链。

## 实际验收

- Release 构建 0 警告、0 错误，原有 36 项测试通过。
- 扩展功能测试带原版 PNG 夹具时 14 项通过，包含真实中文字体、换行/折行、独立面积、三类 DEFLATE 块和 SIMD 等价性。
- 150 个格式/Alpha/stride/尾部组合一致，另实际禁用硬件指令验证标量回退。
- GTX 1050 Ti 上，GPU/CPU 的矩形、AA 路径、中文、渐变、描边、图片/裁剪和批次边界逐像素一致；验证了缓存内容变化、淘汰、线程约束和 Dispose。
- 原版 120 个 CPU 像素场景中 108 达到预设阈值，12 个曲线 AA/文字场景未通过；25 个行为和字体度量探针一致。失败未隐藏或放宽阈值。
- 425/3235 条公共元数据匹配，仍有 2810 条缺口；Avalonia 引用的成员中 120/346 个存在签名，尚未完整接入，未承诺二进制兼容。Linux 尚未实机运行。

## 复现验证

    powershell -NoProfile -File tools/verify.ps1 -Gpu
    powershell -NoProfile -File tools/check-production-dependencies.ps1
    powershell -NoProfile -File tools/fetch-baseline.ps1 -IncludeWindowsReferenceRuntime
    powershell -NoProfile -File tools/verify.ps1 -Reference
    dotnet run --project tests/Features -c Release -- --reference-pngs
    dotnet run --project tools/CpuBench -c Release
    dotnet run --project tests/ReferenceRenderer -c Release -- --benchmark

-Reference 当前因上述 12 个像素差异非零退出，查看 artifacts/diff/results.json 和差异图。原版只在隔离对照进程。测试使用可执行断言程序，不应将 dotnet test 的无测试输出当作通过。

项目附 GLSL 和 SPIR-V，普通构建不需要 shader 编译器。更改 shader 后使用 tools/build-shader.ps1 -Compiler <glslang路径>；编译器仅供开发。

Git 仓库保留源码、测试夹具和文档。`baseline/`、`artifacts/`、`results/`、构建缓存及 OCR 本机配置不上传；报告中的这些路径指本机原始证据，可按所列命令重新生成。项目采用 [MIT 许可证](https://github.com/hnlyf168/SimdSkia/blob/main/LICENSE)，第三方依赖保留各自许可证，详见下方文档。

[进度](https://github.com/hnlyf168/SimdSkia/blob/main/docs/PROGRESS.md) · [兼容报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/COMPATIBILITY.md) · [验证与性能](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VALIDATION.md) · [架构](https://github.com/hnlyf168/SimdSkia/blob/main/docs/ARCHITECTURE.md) · [依赖与许可证](https://github.com/hnlyf168/SimdSkia/blob/main/docs/DEPENDENCIES.md)

## 原版性能对比

[多场景性能报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/PERFORMANCE.md) 已实测原版/标量/SIMD：12 场景、两尺寸、每实现 3 个独立进程及每场景 7 批采样，包含逐帧 P95、托管分配、GC、进程内存、CPU 使用率、PNG 大小和像素 gate。画质未通过的场景只作诊断。随后实际优化引擎并复测，见 [优化前后实测](https://github.com/hnlyf168/SimdSkia/blob/main/docs/OPTIMIZATION.md)；旧结果、失败门槛和像素证据保留。

[GPU(Vulkan)对比](https://github.com/hnlyf168/SimdSkia/blob/main/docs/PERFORMANCE-GPU.md) 用同一台 GTX 1050 Ti 实测原版 SkiaSharp Vulkan 光栅后端与 SimdSkia compute 管线：每帧完整 round-trip（绘制+提交+等待+回读）计时，含托管分配、进程内存和 CPU 使用率；未过像素 gate 的场景只作诊断。

复现时指定新的输出目录，以保留旧数据：

    powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-run
    powershell -NoProfile -File tools/run-gpu-benchmarks.ps1 -Output artifacts/performance-gpu/my-run

CPU 光栅结果见 PERFORMANCE.md；GPU(Vulkan) round-trip 对照见 PERFORMANCE-GPU.md，二者不混用。原版依赖仍仅在 benchmarks/Reference 与 benchmarks/GpuReference，benchmark 项目不属于生产引用图。

CPU 优化包括连续像素 SIMD 合成、AA 矩形覆盖、路径活动边与区间计数、有界布局/描边/覆盖率缓存、图片分段及托管 PNG/DEFLATE。GPU 优化包括包围盒限定 dispatch、FillPath 边缓存、持久化 arena 与预分配描述符、源图身份+快照缓存；两者细节与实测见 [优化前后实测](https://github.com/hnlyf168/SimdSkia/blob/main/docs/OPTIMIZATION.md)。额外 [变化场景基准](https://github.com/hnlyf168/SimdSkia/blob/main/docs/PERFORMANCE-CHANGING.md) 每帧移动并超过缓存容量，用于查看未命中成本。

当前修复版本的 [CPU/GPU、内存与继续投入评估](https://github.com/hnlyf168/SimdSkia/blob/main/docs/REASSESSMENT.md) 已重新实测，包含相对原版和上轮的变化、每帧 CPU 成本与进程内存。独立 HOST_CACHED 实验验证了 GPU 回读优化空间；复杂 GPU 文字仍需要改变算法。
