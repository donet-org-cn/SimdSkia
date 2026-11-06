# SimdSkia

## 0.1.2-preview.1：公共API与GPU优化预览

固定 SkiaSharp **3.119.4 / 3235 项**，当前匹配 **3230 项、201 公开类型、5 缺口、0 额外签名**。从99项冻结快照补入94项；五项原生句柄/版本合同按用户要求保留，不伪造替代语义。新增真实Alpha8、浮点颜色/Blender/抖动、LCD/中文点阵、SVG/XPS、原生v103 SKP子集与GL/D3D12/Metal GPU资源链路。Vulkan增加非重叠矩形合批、设备内复制、直接完成读回与去分配；Metal没有Mac GPU实机验证。**JPEG、部分文字/曲线、整页画质及性能/内存差距仍存在，不代表全面行为或二进制兼容。**

[完整测试、CPU/GPU和内存报告](docs/PUBLIC-API-FINAL-2026-11-07.md) · [发布状态](docs/NUGET-PREVIEW.md) · [更新日志](docs/RELEASE-NOTES.md)

[带性能图、兼容范围与安装方法的文章](docs/blog/2026-11-07-simdskia-api-and-gpu.md)。正式旧版保持0.1.1；本预览公开下载验证状态单独记录。

独立 C# 2D 引擎，内部使用 `SimdSkia`，源码兼容层保留 `SkiaSharp.SK*`。不使用 Google Skia 绘图；字体、基础排版及图片编解码均由 C# 实现。通过 `SimdSkia.Avalonia` 可以接入 Avalonia，无需重新编译 Avalonia。

当前版本 **`0.1.2-preview.1`** 支持 **Avalonia >=12.1.0 且 <13.0.0**，已验证的 12.1.0–12.1.5 在此前版本已通过同一适配器二进制兼容测试。未来大版本需要另行适配；未来 12.x 要回归并补充对应宿主，不能保证未知版本的窗口 ABI。Windows、Linux X11/XWayland、macOS 已通过真实窗口验证；macOS 使用经用户确认允许的官方原生窗口宿主，绘图、字体、排版和编解码仍是 C#。默认使用 CPU，可选 Windows Direct2D 原生交换链或 Windows/Linux Vulkan 绘图。**Metal核心已有实现但无Mac GPU实机验证，尚未接入Avalonia窗口呈现；完整兼容性和画质验收仍未全部通过。**

## NuGet 包怎么选

本轮包含下列十二个包，统一版本 `0.1.2-preview.1`；发布状态见 [预览发布](docs/NUGET-PREVIEW.md)。0.1.2-preview.1 是预览版，NuGet 界面需要勾选“包含预发行版”；公开下载状态以发布记录为准。

| 包名 | 用途 |
| --- | --- |
| [SimdSkia.Avalonia.Desktop](https://www.nuget.org/packages/SimdSkia.Avalonia.Desktop/0.1.2-preview.1) | **桌面应用优先选这个**：自动选择 Windows/Linux/macOS 宿主，传递引入渲染适配器 |
| [SimdSkia.Avalonia](https://www.nuget.org/packages/SimdSkia.Avalonia/0.1.2-preview.1) | 绘图、字体和基础文字排版服务，也可配合自行配置的宿主 |
| [SimdSkia.Avalonia.Gpu](https://www.nuget.org/packages/SimdSkia.Avalonia.Gpu/0.1.2-preview.1) | 可选 Avalonia GPU 绘图、Direct2D 直接呈现和回退/缓存诊断 |
| [SimdSkia.Gpu.Direct2D](https://www.nuget.org/packages/SimdSkia.Gpu.Direct2D/0.1.2-preview.1) | Windows Direct2D/D3D11及D3D12互操作，C#系统API绑定 |
| [SimdSkia](https://www.nuget.org/packages/SimdSkia/0.1.2-preview.1) | 需要 `SkiaSharp.SK*` 源码兼容 API 时使用的依赖汇总包 |
| [SimdSkia.Compatibility](https://www.nuget.org/packages/SimdSkia.Compatibility/0.1.2-preview.1) | `SKBitmap`、`SKCanvas` 等兼容层，也由 `SimdSkia` 汇总包引入 |
| [SimdSkia.Core](https://www.nuget.org/packages/SimdSkia.Core/0.1.2-preview.1) | 几何、路径和绘图状态 |
| [SimdSkia.Cpu](https://www.nuget.org/packages/SimdSkia.Cpu/0.1.2-preview.1) | CPU 光栅化、像素合成及 SIMD |
| [SimdSkia.Text](https://www.nuget.org/packages/SimdSkia.Text/0.1.2-preview.1) | TrueType 字体解析和基础排版 |
| [SimdSkia.Imaging](https://www.nuget.org/packages/SimdSkia.Imaging/0.1.2-preview.1) | 图片编解码和颜色处理 |
| [SimdSkia.Gpu](https://www.nuget.org/packages/SimdSkia.Gpu/0.1.2-preview.1) | Vulkan、GL/GLES与Metal核心（Metal未实机验证），需系统API/驱动 |
| [SimdSkia.Pdf](https://www.nuget.org/packages/SimdSkia.Pdf/0.1.2-preview.1) | 可选纯托管 PDF 阅读/页面绘制；导出 PDF 无需安装此包 |

新桌面应用添加 `SimdSkia.Avalonia.Desktop`，其渲染服务和引擎依赖会自动还原，无需额外安装 `SimdSkia` 或手动复制底层 DLL：

```powershell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.2-preview.1
```

**安装包后还必须配置启动代码。** 以下是完整的新程序示例；现有项目按后面的迁移步骤调整。

从旧预览版升级时，将直接引用的所有 `SimdSkia.*` 包统一到 `0.1.2-preview.1`，Avalonia 家族包统一选择 >=12.1.0 且 <13.0.0 中的同一版本。本次要求 .NET 10，并更新编解码依赖、格式支持及测试报告；继承此前独立 SK* API、PDF、SIMD 和启动准备，无需重新编译 Avalonia。具体性能、画质限制与发布验收见 [发布记录](docs/NUGET-PREVIEW.md)。

## Avalonia 快速开始（Windows / Linux / macOS）

使用 .NET 10 SDK，在空目录创建以下两个文件。所有 Avalonia 包统一为 **12.1.5**，无需克隆或编译本仓库。

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
    <PackageReference Include="SimdSkia.Avalonia.Desktop" Version="0.1.2-preview.1" />
    <PackageReference Include="Avalonia" Version="[12.1.5]" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="[12.1.5]" />
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

默认继续使用已启用SIMD的CPU。最新实际控件窗口中，CPU比Direct2D/Vulkan更快且内存更低；GPU并非通用省内存方案。选择GPU前按场景实测，细节见[129进程对照](docs/API-GPU-2026-11-06.md)。allowCpuFallback是绘图命令级回退，不保证设备初始化或所有窗口类型自动回退；分层/逐像素透明窗口需显式选择FramebufferReadback，macOS尚无GPU后端。

在上面的桌面程序中增加可选包：

```powershell
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.2-preview.1
```

Windows 将 `.UseSimdSkiaDesktop()` 改为：

```csharp
.UseSimdSkiaDesktop(options => options.UseGpu(SimdSkiaGpuBackend.Direct2D, allowCpuFallback: true))
```

通过 C# 调用系统 Direct2D/D3D11，并使用 DXGI 原生交换链呈现；正常帧不强制回读像素，GPU 完成以事件查询确认。文字默认由 C# 生成并缓存覆盖率纹理，再交 GPU 合成；透明图层、滤镜等仍有 CPU 工作，诊断分别记录。可选择 `GpuOutlines` 进行轮廓 GPU 绘制，该模式当前更慢且文字覆盖率与默认模式不同。

Linux/Windows 可改用 `SimdSkiaGpuBackend.Vulkan`，需硬件 Vulkan 设备；当前通过完成栅栏后回读并交窗口 framebuffer 呈现。**GPU 不保证快于 CPU，需按实际尺寸、DPI 和场景评估；macOS 调用 GPU 配置会明确报错。** 不支持的画刷等默认报错；可显式设置 `allowCpuFallback: true`，并通过 `SimdSkiaGpuDiagnostics` 查看原因、回退数及 Vulkan 覆盖缓存和同步计数。详见 [跨平台说明](docs/CROSS-PLATFORM.md)、[最新窗口/内存报告](docs/STARTUP-ROUND7-2026-10-22.md) 和 [此前 Vulkan 专项报告](docs/VULKAN-OPTIMIZATION-2026-10-22.md)。

## 现有 Avalonia 项目如何切换

1. 将 Avalonia 系列包统一为 **>=12.1.0 且 <13.0.0 的同一版本**，添加 `SimdSkia.Avalonia.Desktop`；需要 GPU 时再添加 `SimdSkia.Avalonia.Gpu`。
2. 移除 `Avalonia.Desktop`、`Avalonia.Skia`、`Avalonia.Fonts.Inter` 等原版后端/字体引用及 `.WithInterFont()`，使用上述包配置；检查第三方控件是否仍间接引用 Skia/HarfBuzz/ANGLE。
3. 添加 `using SimdSkia.Avalonia;`，将 `.UsePlatformDetect()` 或固定 `.UseWin32()` 链替换为 `.UseSimdSkiaDesktop()`。保留自己的 `App`、XAML、MVVM 和窗口创建代码。

`.UseSimdSkia()` 注册 Avalonia 的绘图、字体和基础排版服务，因此无需修改 Avalonia 源码。它不会把 `SkiaSharp.dll` 替换为同名程序集；直接依赖原版 SK*/GR API 或 `ISkiaSharpApiLeaseFeature` 的自定义控件仍需迁移。

如需显式指定受支持的 TTF/TTC，将 `.UseSimdSkia()` 改为：

```csharp
.UseSimdSkia(options => options.FontFiles.Add(@"C:\Fonts\MyFont.ttf"))
```

显式列表会替代自动系统字体列表；随程序分发字体时，应设置复制到输出/发布目录并遵守字体许可证。Linux/macOS 按受支持静态字体的优先级选择默认家族，可用 `SIMDSKIA_DEFAULT_FONT_FAMILY` 指定已安装家族，不复刻 fontconfig/CoreText 的所有规则。更多说明见 [Avalonia 包内 README](src/SimdSkia.Avalonia/README.md)。

## 启动准备

默认在经典桌面 JIT 初始化时，用一次性后台线程准备常用绘图路径；无窗口应用和 Native AOT 自动跳过。需要排查启动行为时可关闭：

```csharp
AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop(options => options.EnableStartupPreparation = false)
    .StartWithClassicDesktopLifetime(args);
```

准备任务不隐藏窗口、不延迟 `Show`、不绘制占位帧；它缩短部分首帧等待，不能保证所有系统和业务窗口完全无白屏。

## Avalonia Native AOT 发布

在目标平台安装 Native AOT 工具链后执行对应命令。Windows 需要 Visual Studio C++ 和 Windows SDK，Linux 需要 clang/zlib 开发工具，macOS 需要 Xcode 命令行工具。AOT 发布必须在相应操作系统完成：

```powershell
dotnet publish -c Release -r win-x64 -p:PublishAot=true -o publish/win-x64
dotnet publish -c Release -r linux-x64 -p:PublishAot=true -o publish/linux-x64
dotnet publish -c Release -r osx-arm64 -p:PublishAot=true -o publish/osx-arm64
```

运行目标目录中的 `Demo.exe`（Windows）或 `Demo`（Linux/macOS），无需安装 .NET 运行时。业务代码仍需满足 AOT 要求。若没有窗口，检查 `desktop.MainWindow` 和单实例提前退出逻辑。macOS 正式分发还需自行准备应用包、签名和公证。

上述两个文件与包内 README 保持一致。流水线、许可证、真实 nupkg 的消费验证与发布状态见 [预览发布说明](docs/NUGET-PREVIEW.md)；本轮平台测试与性能见 [交付报告](docs/DESKTOP-GPU-2026-10-22.md)，画质和 API 剩余缺口见 [兼容验证](docs/VALIDATION.md)。

## 示例与验证

```powershell
dotnet build SimdSkia.sln -c Release
dotnet run --project examples/Rectangle -c Release
dotnet run --project examples/Compatibility99 -c Release
python tools/run-final-compatibility.py --output artifacts/reproduce/final-api
```

Compatibility99实际输出中英文PNG、带链接PDF及读取后的页面、向量SVG/XPS/SKP、延迟图片和WebP，并读取验证真实Alpha8。需要先按tools/fetch-test-font.ps1准备许可明确的测试字体；应用默认发现系统字体，无需默认携带字体资源。

GPU对照需要真实驱动，Metal无硬件不能计为通过。严格画质失败保持退出2，无后端/无硬件保持退出3；没有测试执行的dotnet test输出不能当作通过。性能、所有失败、逐项清单及复现命令见上方报告，旧版结果只在各自日期报告中使用。

生产包不调用Google Skia，不携带其原生绘图库；PNG/TIFF允许.NET系统压缩，macOS窗口使用允许的官方原生宿主。CPU光栅/几何/字体/基础排版仍为C#。第三方依赖、用途/许可证与原生边界见[依赖说明](docs/DEPENDENCIES.md)。

[进度](docs/PROGRESS.md) · [兼容报告](docs/COMPATIBILITY.md) · [验证](docs/VALIDATION.md) · [架构](docs/ARCHITECTURE.md) · [MIT许可证](LICENSE)
