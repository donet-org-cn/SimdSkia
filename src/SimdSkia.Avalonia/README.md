# SimdSkia.Avalonia

独立 C# 2D 引擎，内部使用 `SimdSkia`，源码兼容层保留 `SkiaSharp.SK*`。不使用 Google Skia 绘图；字体、基础排版及图片编解码均由 C# 实现。通过 `SimdSkia.Avalonia` 可以接入 Avalonia，无需重新编译 Avalonia。

本轮版本为 **`0.1.0-preview.3`**，固定支持 **Avalonia 12.1.4**。Windows、Linux X11/XWayland、macOS 已通过真实窗口验证；macOS 使用经用户确认允许的官方原生窗口宿主，绘图、字体、排版和编解码仍是 C#。默认使用 CPU，可选 Windows Direct2D 原生交换链或 Windows/Linux Vulkan 绘图。**macOS GPU/Metal 尚未实现，完整兼容性和画质验收仍未全部通过。**

## NuGet 包怎么选

本轮包含下列十一个包，统一版本 `0.1.0-preview.3`；发布状态见 [预览发布](https://github.com/hnlyf168/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)。在 Visual Studio 的 NuGet 搜索中勾选“包括预发行版”。

| 包名 | 用途 |
| --- | --- |
| [SimdSkia.Avalonia.Desktop](https://www.nuget.org/packages/SimdSkia.Avalonia.Desktop/0.1.0-preview.3) | **桌面应用优先选这个**：自动选择 Windows/Linux/macOS 宿主，传递引入渲染适配器 |
| [SimdSkia.Avalonia](https://www.nuget.org/packages/SimdSkia.Avalonia/0.1.0-preview.3) | 绘图、字体和基础文字排版服务，也可配合自行配置的宿主 |
| [SimdSkia.Avalonia.Gpu](https://www.nuget.org/packages/SimdSkia.Avalonia.Gpu/0.1.0-preview.3) | 可选 Avalonia GPU 绘图、Direct2D 直接呈现和回退/缓存诊断 |
| [SimdSkia.Gpu.Direct2D](https://www.nuget.org/packages/SimdSkia.Gpu.Direct2D/0.1.0-preview.3) | Windows Direct2D/D3D11，纯 C# 系统 API 互操作，无第三方绑定包 |
| [SimdSkia](https://www.nuget.org/packages/SimdSkia/0.1.0-preview.3) | 需要 `SkiaSharp.SK*` 源码兼容 API 时使用的依赖汇总包 |
| [SimdSkia.Compatibility](https://www.nuget.org/packages/SimdSkia.Compatibility/0.1.0-preview.3) | `SKBitmap`、`SKCanvas` 等兼容层，也由 `SimdSkia` 汇总包引入 |
| [SimdSkia.Core](https://www.nuget.org/packages/SimdSkia.Core/0.1.0-preview.3) | 几何、路径和绘图状态 |
| [SimdSkia.Cpu](https://www.nuget.org/packages/SimdSkia.Cpu/0.1.0-preview.3) | CPU 光栅化、像素合成及 SIMD |
| [SimdSkia.Text](https://www.nuget.org/packages/SimdSkia.Text/0.1.0-preview.3) | TrueType 字体解析和基础排版 |
| [SimdSkia.Imaging](https://www.nuget.org/packages/SimdSkia.Imaging/0.1.0-preview.3) | 图片编解码和颜色处理 |
| [SimdSkia.Gpu](https://www.nuget.org/packages/SimdSkia.Gpu/0.1.0-preview.3) | Vulkan 离屏后端，需要系统 Vulkan loader/驱动 |

新桌面应用添加 `SimdSkia.Avalonia.Desktop`，其渲染服务和引擎依赖会自动还原，无需额外安装 `SimdSkia` 或手动复制底层 DLL：

```powershell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.3
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
    <PackageReference Include="SimdSkia.Avalonia.Desktop" Version="0.1.0-preview.3" />
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
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.3
```

Windows 将 `.UseSimdSkiaDesktop()` 改为：

```csharp
.UseSimdSkiaDesktop(options => options.UseGpu(SimdSkiaGpuBackend.Direct2D))
```

通过 C# 调用系统 Direct2D/D3D11，并使用 DXGI 原生交换链呈现；正常帧不强制回读像素，GPU 完成以事件查询确认。文字默认由 C# 生成并缓存覆盖率纹理，再交 GPU 合成；透明图层、滤镜等仍有 CPU 工作，诊断分别记录。可选择 `GpuOutlines` 进行轮廓 GPU 绘制，该模式当前更慢且文字覆盖率与默认模式不同。

Linux/Windows 可改用 `SimdSkiaGpuBackend.Vulkan`，需硬件 Vulkan 设备；当前通过完成栅栏后回读并交窗口 framebuffer 呈现。**Vulkan 完整控件目前明显慢于 CPU，保持实验性质；macOS 调用 GPU 配置会明确报错。** 不支持的画刷等默认报错；可显式设置 `allowCpuFallback: true`，并通过 `SimdSkiaGpuDiagnostics` 查看原因和数量。详见 [跨平台说明](https://github.com/hnlyf168/SimdSkia/blob/main/docs/CROSS-PLATFORM.md) 和 [本轮性能报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/DESKTOP-GPU-2026-10-22.md)。

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
