# SimdSkia

独立 C# 2D 引擎，内部使用 `SimdSkia`，源码兼容层保留 `SkiaSharp.SK*`。不使用 Google Skia 绘图；字体、基础排版及图片编解码均由 C# 实现。通过 `SimdSkia.Avalonia` 可以接入 Avalonia，无需重新编译 Avalonia。

当前 NuGet 版本为 **`0.1.0-preview.1`**，Avalonia 适配器固定支持 **Avalonia 12.1.4**。Windows 桌面窗口使用 CPU 软件呈现；完整兼容性和画质验收尚未全部通过。独立的 Vulkan 离屏实现尚未接入 Avalonia 窗口。

## NuGet 包怎么选

下列八个包均已发布 `0.1.0-preview.1`。在 Visual Studio 的 NuGet 搜索中勾选“包括预发行版”。

| 包名 | 用途 |
| --- | --- |
| [SimdSkia.Avalonia](https://www.nuget.org/packages/SimdSkia.Avalonia/0.1.0-preview.1) | **Avalonia 应用选这个**：绘图、字体和基础文字排版后端适配器 |
| [SimdSkia](https://www.nuget.org/packages/SimdSkia/0.1.0-preview.1) | 需要 `SkiaSharp.SK*` 源码兼容 API 时使用的依赖汇总包 |
| [SimdSkia.Compatibility](https://www.nuget.org/packages/SimdSkia.Compatibility/0.1.0-preview.1) | `SKBitmap`、`SKCanvas` 等兼容层，也由 `SimdSkia` 汇总包引入 |
| [SimdSkia.Core](https://www.nuget.org/packages/SimdSkia.Core/0.1.0-preview.1) | 几何、路径和绘图状态 |
| [SimdSkia.Cpu](https://www.nuget.org/packages/SimdSkia.Cpu/0.1.0-preview.1) | CPU 光栅化、像素合成及 SIMD |
| [SimdSkia.Text](https://www.nuget.org/packages/SimdSkia.Text/0.1.0-preview.1) | TrueType 字体解析和基础排版 |
| [SimdSkia.Imaging](https://www.nuget.org/packages/SimdSkia.Imaging/0.1.0-preview.1) | 图片编解码和颜色处理 |
| [SimdSkia.Gpu](https://www.nuget.org/packages/SimdSkia.Gpu/0.1.0-preview.1) | Vulkan 离屏后端，需要系统 Vulkan loader/驱动 |

Avalonia 应用通常只需添加 `SimdSkia.Avalonia`，其引擎依赖会自动还原，无需额外安装 `SimdSkia` 或手动复制底层 DLL：

```powershell
dotnet add package SimdSkia.Avalonia --version 0.1.0-preview.1
```

**安装包后还必须配置启动代码。** 以下是完整的新程序示例；现有项目按后面的迁移步骤调整。

## Avalonia 快速开始（Windows）

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
    <PackageReference Include="SimdSkia.Avalonia" Version="0.1.0-preview.1" />
    <PackageReference Include="Avalonia" Version="[12.1.4]" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="[12.1.4]" />
    <!-- 只使用 Win32 包内的托管窗口组件，避免引入 ANGLE 原生资源。 -->
    <PackageDownload Include="Avalonia.Win32" Version="[12.1.4]" />
    <Reference Include="$(NuGetPackageRoot)/avalonia.win32/12.1.4/lib/net8.0/*.dll" />
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
            .UseStandardRuntimePlatformSubsystem()
            .UseWin32()
            .With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Software]
            })
            .UseSimdSkia()
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

`WinExe` 避免出现额外控制台窗口。Windows 默认使用当前系统消息界面字体及受支持的已安装回退字体，无需随程序附带字体文件；中文需要系统已安装包含中文字形的静态 TrueType 字体。

## 现有 Avalonia 项目如何切换

1. 将项目引用的 Avalonia 系列包统一为 **12.1.4**，添加上面的 `SimdSkia.Avalonia` 包。
2. 将项目的窗口相关引用改为示例中的 `Avalonia`、`Avalonia.Themes.Fluent` 和托管 Win32 引用。移除 `Avalonia.Desktop`、`Avalonia.Skia`、`Avalonia.Fonts.Inter` 等原版后端/字体引用及 `.WithInterFont()`；检查其他第三方包是否仍间接引入 Skia/HarfBuzz/ANGLE。
3. 在 `Program.cs` 添加 `using SimdSkia.Avalonia;`，将 `.UsePlatformDetect()` 换成示例中的 `.UseStandardRuntimePlatformSubsystem().UseWin32().With(...).UseSimdSkia()` 启动链。保留自己的 `App`、XAML、MVVM 和窗口创建代码。

`.UseSimdSkia()` 注册 Avalonia 的绘图、字体和基础排版服务，因此无需修改 Avalonia 源码。它不会把 `SkiaSharp.dll` 替换为同名程序集；直接依赖原版 SK*/GR API 或 `ISkiaSharpApiLeaseFeature` 的自定义控件仍需迁移。

如需显式指定受支持的 TTF/TTC，将 `.UseSimdSkia()` 改为：

```csharp
.UseSimdSkia(options => options.FontFiles.Add(@"C:\Fonts\MyFont.ttf"))
```

显式列表会替代自动系统字体列表；随程序分发字体时，应设置复制到输出/发布目录并遵守字体许可证。Linux/macOS 尚无自动系统字体发现，本预览版的桌面窗口宿主实际验证限 Windows。更多字体、迁移及排错说明见 [Avalonia 包内 README](src/SimdSkia.Avalonia/README.md)。

## Avalonia Native AOT 发布

在已安装 Visual Studio C++ 工具链及 Windows SDK 的 Windows 环境，对上述示例执行：

```powershell
dotnet publish -c Release -r win-x64 -p:PublishAot=true -o publish/win-x64
```

运行 `publish/win-x64/Demo.exe`，无需安装 .NET 运行时。现有业务程序的反射、动态代码和序列化也需满足 AOT 要求。若没有窗口，先检查 `desktop.MainWindow` 是否设置，以及旧进程/单实例逻辑是否使新进程提前退出。

以上两个文件与已发布 NuGet 包中的示例保持一致，并已通过仅使用 NuGet.org 源的独立还原和 Release 编译。发布流水线还使用真实 nupkg 验证了 Windows Native AOT 窗口呈现。流水线、许可证及打包验证见 [预览发布说明](docs/NUGET-PREVIEW.md)；画质、字体和 API 剩余缺口见 [兼容验证](docs/VALIDATION.md)。

## 示例、实现与测试报告

新增 [Avalonia Native AOT 双后端程序](examples/AvaloniaAot/README.md)：相同控件分别使用 SimdSkia 与原版，提供 Windows x64 通用及 AVX2 版本，实际比较完整发布体积、启动/绘制速度、CPU 和内存。结果见 [AOT 实测报告](docs/AVALONIA-AOT-COMPARISON.md)；既有报告是另一套 JIT/离屏基准，数据不混用。

SK* 源码兼容的固定基线是 Avalonia.Skia 12.1.4 实际解析的 SkiaSharp 3.119.4。Core/Cpu/Text/Gpu 无第三方 NuGet 依赖，Imaging/Compatibility 使用固定版本纯托管 JpegLibrary；可选 Avalonia 层另有纯托管依赖，见 [许可证与依赖](docs/DEPENDENCIES.md)。

路径、抗锯齿、基本中英文 glyf TTF/TTC 字体、PNG/JPEG/部分 ICC、图像/视觉画刷、模糊/阴影、SIMD 与 Vulkan 离屏均有真实实现。**完整 Avalonia 替换尚未通过验收**：文字/曲线及部分新功能画质仍失败，通用字体排版与 Vulkan 窗口等仍有缺口。

最新交付：[测试报告](docs/TEST-REPORT-2026-10-21.md)、[CPU/内存/滤镜性能](docs/PERFORMANCE-FINAL-2026-10-21.md)、[原版 Vulkan GPU 对比](docs/PERFORMANCE-GPU-FINAL-2026-10-21.md)、[实现范围与缺口](docs/IMPLEMENTATION-DETAILS-2026-10-21.md)。报告保留未通过项，不承诺替代原版强名称程序集。

2026-10-21 的 CPU、GPU、真实 OCR、CPU 时间和内存对比见 [本轮优化报告](docs/OPTIMIZATION-2026-10-21.md)。保留优化前数据及失败的画质检查。

[真实 Avalonia 接入](docs/AVALONIA-INTEGRATION.md)：`UseSimdSkia` 已运行实际控件与 Win32 软件窗口，8109 项控件/字形/滤镜/字体集合断言通过。较早的[字形/首绘优化](docs/AVALONIA-TEXT-CACHE.md)及通用报告保留为历史数据；当前结论以本轮报告为准。

```powershell
dotnet build SimdSkia.sln -c Release
powershell -NoProfile -File tools/fetch-avalonia-font.ps1
dotnet run --project examples/AvaloniaControls -c Release
# Windows 交互窗口
dotnet run --project examples/AvaloniaControls -c Release -- --window
```

![SimdSkia 实际 Avalonia 控件](docs/images/avalonia-text-cache-managed.png)

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

固定 OFL 测试字体含 CJK/拉丁字形；区域字形风格应使用应用自己的中文字体。没有字体时明确失败或标记字体测试未执行，不生成假字形。GPU 需要系统 Vulkan loader/驱动：Windows vulkan-1.dll，Linux libvulkan.so.1；当前为 64 位小端 RGBA/Premul 离屏目标，不提供 swapchain 或 Skia GR 句柄替换。

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

Git 仓库保留源码、测试夹具和文档。`baseline/`、`artifacts/`、`results/`、构建缓存及 OCR 本机配置不上传；报告中的这些路径指本机原始证据，可按所列命令重新生成。项目采用 [MIT 许可证](LICENSE)，第三方依赖保留各自许可证，详见下方文档。

[进度](docs/PROGRESS.md) · [兼容报告](docs/COMPATIBILITY.md) · [验证与性能](docs/VALIDATION.md) · [架构](docs/ARCHITECTURE.md) · [依赖与许可证](docs/DEPENDENCIES.md)

## 原版性能对比

[多场景性能报告](docs/PERFORMANCE.md) 已实测原版/标量/SIMD：12 场景、两尺寸、每实现 3 个独立进程及每场景 7 批采样，包含逐帧 P95、托管分配、GC、进程内存、CPU 使用率、PNG 大小和像素 gate。画质未通过的场景只作诊断。随后实际优化引擎并复测，见 [优化前后实测](docs/OPTIMIZATION.md)；旧结果、失败门槛和像素证据保留。

[GPU(Vulkan)对比](docs/PERFORMANCE-GPU.md) 用同一台 GTX 1050 Ti 实测原版 SkiaSharp Vulkan 光栅后端与 SimdSkia compute 管线：每帧完整 round-trip（绘制+提交+等待+回读）计时，含托管分配、进程内存和 CPU 使用率；未过像素 gate 的场景只作诊断。

复现时指定新的输出目录，以保留旧数据：

    powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-run
    powershell -NoProfile -File tools/run-gpu-benchmarks.ps1 -Output artifacts/performance-gpu/my-run

CPU 光栅结果见 PERFORMANCE.md；GPU(Vulkan) round-trip 对照见 PERFORMANCE-GPU.md，二者不混用。原版依赖仍仅在 benchmarks/Reference 与 benchmarks/GpuReference，benchmark 项目不属于生产引用图。

CPU 优化包括连续像素 SIMD 合成、AA 矩形覆盖、路径活动边与区间计数、有界布局/描边/覆盖率缓存、图片分段及托管 PNG/DEFLATE。GPU 优化包括包围盒限定 dispatch、FillPath 边缓存、持久化 arena 与预分配描述符、源图身份+快照缓存；两者细节与实测见 [优化前后实测](docs/OPTIMIZATION.md)。额外 [变化场景基准](docs/PERFORMANCE-CHANGING.md) 每帧移动并超过缓存容量，用于查看未命中成本。

当前修复版本的 [CPU/GPU、内存与继续投入评估](docs/REASSESSMENT.md) 已重新实测，包含相对原版和上轮的变化、每帧 CPU 成本与进程内存。独立 HOST_CACHED 实验验证了 GPU 回读优化空间；复杂 GPU 文字仍需要改变算法。
