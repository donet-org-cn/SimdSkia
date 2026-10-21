# SimdSkia.Avalonia 预览版

把 Avalonia 的绘图、字体和基础文字排版交给 SimdSkia 的 C# 实现。**当前只适配 Avalonia 12.1.4**，Windows 窗口使用 CPU 软件呈现。无需编译或修改 Avalonia 源码，也不需要将原版 SkiaSharp 程序集替换成同名文件。

这是功能和画质兼容尚未全部通过的预览版，不保证覆盖任意现有 Avalonia 应用，也不保证性能或像素结果与原版一致。

## 新程序：复制两个文件即可运行

在 Windows 创建空目录，使用 .NET 10 SDK。以下示例采用完整包引用，不引用 SimdSkia 的本地源码。

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

运行：

```powershell
dotnet restore
dotnet run
```

默认示例没有管理员权限清单，不需要图形驱动管理权限。`WinExe` 避免出现额外控制台窗口。NuGet 客户端搜索此包时需启用“包括预发行版”。

## 现有 Avalonia 程序如何切换

1. 将所有 Avalonia 包统一到 **12.1.4**，添加 `SimdSkia.Avalonia` 的预览版引用。
2. 如需完全避免分发 Skia/HarfBuzz/ANGLE，移除 `Avalonia.Desktop`、原版渲染/字体包和 `.WithInterFont()`，改用上述 Avalonia、Fluent 和托管 Win32 引用。
3. 把启动配置中的 `.UsePlatformDetect()` 改成上述 `.UseStandardRuntimePlatformSubsystem().UseWin32().With(...).UseSimdSkia()`。保留自己的 `App`、XAML、MVVM、窗口及 `.StartWithClassicDesktopLifetime(args)`。

**单纯安装包不会自动切换后端，必须调用 `.UseSimdSkia()`。** 此包是 Avalonia 后端适配器，通常无需再引用 `SimdSkia` 或 `SimdSkia.Compatibility` 的 SK* 兼容层。

## 字体

Windows 默认读取当前系统消息界面字体及已安装的中文、终端回退字体，不附带字体资源。字体解析、排版和绘制仍是 C#。中文显示需要机器已安装受支持的中文静态 TrueType 字体；系统设置变化后请重启应用。

也可以显式配置受支持的 TTF/TTC：

```csharp
.UseSimdSkia(options => options.FontFiles.Add(@"C:\Fonts\MyFont.ttf"))
```

显式列表的首个家族成为默认，不再自动加入系统字体。若随程序分发自己的字体，应设置字体文件 `CopyToOutputDirectory` / `CopyToPublishDirectory`，并遵守字体许可证。Linux/macOS 尚未实现自动系统字体发现，需显式字体列表；本预览版的桌面窗口宿主实际验证限 Windows。

## 发布 Native AOT

在安装了 Visual Studio C++ 工具链及 Windows SDK 的 Windows 环境执行：

```powershell
dotnet publish -c Release -r win-x64 -p:PublishAot=true -o publish/win-x64
```

运行 `publish/win-x64/Demo.exe`，无需安装 .NET 运行时。业务应用自身的反射、动态代码及序列化也需要满足 AOT 要求；此渲染包不能自动修复业务代码的裁剪问题。

## 排错和限制

- **没有窗口**：检查项目是否真的创建并设置了 `desktop.MainWindow`；若使用单实例互斥锁，检查旧进程是否仍在后台。业务项目的隐藏窗口/单实例逻辑不由此包管理。
- **版本错误**：适配器严格检查 Avalonia 12.1.4。不要混用 Avalonia 11、12.0 或其他版本的程序集。
- **中文/粗体异常**：检查已安装或显式配置的字体是否包含所需字形和真实字重。CFF、可变、彩色字体/emoji、hinting 和复杂脚本排版仍有缺口。
- **自定义控件仍要求 Skia**：请求 `ISkiaSharpApiLeaseFeature` 或直接调用原版 SK*/GR API 的控件需要迁移；此适配器不伪造原生 Skia 句柄。
- **GPU**：当前 Avalonia 窗口采用 CPU；单独的 `SimdSkia.Gpu` 是 Vulkan 离屏实现，并非可用的 Avalonia GPU 窗口后端。

MIT 许可；第三方组件保留各自许可。完整范围、性能和未通过的画质门槛见 [项目说明](https://github.com/hnlyf168/SimdSkia) 与 [兼容性验证](https://github.com/hnlyf168/SimdSkia/blob/main/docs/VALIDATION.md)。
