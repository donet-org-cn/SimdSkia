# SimdSkia.Avalonia.Android 0.1.4

该源码示例使用 Avalonia 12.1.5 和 SimdSkia 的 C# CPU/通用 SIMD 光栅化。Android 官方宿主负责 Activity、触摸、输入法和帧缓冲显示。

中文、输入框、按钮、动画进度条、圆角渐变及前后台恢复已在 Android 16 ARM64 USB 真机测试。0.1.4 新增可直接引用的 Android 包，包消费另做独立缓存 Release/Mono AOT 构建。iOS 呈现适配仍待实现。测试范围见[移动端报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/ANDROID-2026-11-09.md)，当前发行结果见[发布记录](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)。

## NuGet 接入

目标框架使用 `net10.0-android36.0`，Avalonia 家族包统一为12.1.5。在 Android 启动项目中安装以下包。

```powershell
dotnet add package SimdSkia.Avalonia.Android --version 0.1.4
dotnet add package Avalonia.Themes.Fluent --version 12.1.5
```

已有模板中的 `Avalonia.Android` 直接引用应先移除，使用本包提供的窗口宿主。`CreateAppBuilder()` 中调用 `UseSimdSkiaAndroid()`，并提供受支持的静态 TrueType 字体。

```csharp
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using SimdSkia.Avalonia;

[Application]
public sealed class AndroidApplication(nint handle, JniHandleOwnership ownership)
    : AvaloniaAndroidApplication<App>(handle, ownership)
{
    protected override AppBuilder CreateAppBuilder()
    {
        string font = Path.Combine(FilesDir!.AbsolutePath, "MyFont.ttf");
        using (var input = Assets!.Open("Fonts/MyFont.ttf"))
        using (var output = File.Create(font))
            input.CopyTo(output);
        return AppBuilder.Configure<App>()
            .UseSimdSkiaAndroid(options => options.FontFiles.Add(font));
    }
}

[Activity(MainLauncher = true, Exported = true,
    Theme = "@style/SimdSkiaTheme")]
public sealed class MainActivity : AvaloniaMainActivity;
```

项目开启 `ImplicitUsings`，字体以 AndroidAsset 引入。

```xml
<PropertyGroup>
  <TargetFramework>net10.0-android36.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
<ItemGroup>
  <AndroidAsset Include="Fonts/MyFont.ttf" />
</ItemGroup>
```

在 `Resources/values/styles.xml` 中设置 Activity 的 AppCompat 主题。

```xml
<resources>
  <style name="SimdSkiaTheme" parent="Theme.AppCompat.DayNight.NoActionBar">
    <item name="android:windowBackground">#202024</item>
  </style>
</resources>
```

`App` 在 `ISingleViewApplicationLifetime.MainView` 中设置控件。完整 Application、Activity、主题和控件代码可直接参考[可运行示例](https://github.com/donet-org-cn/SimdSkia/tree/main/examples/Mobile.Android)。

## 构建与安装

需要 .NET SDK 10.0.403 或更新的兼容版本、Android 工作负载、Android SDK API 36 和 JDK 21。在仓库根目录执行以下命令。

本机的编译环境已准备在 `artifacts`。使用这套私有 SDK 时，先在当前 PowerShell 会话设置以下变量。

```powershell
$env:DOTNET_ROOT = (Resolve-Path ./artifacts/dotnet-sdk-10.0.403).Path
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
```

```powershell
dotnet workload install android
./tools/fetch-avalonia-font.ps1

# 按自己的安装目录填写两个绝对路径。
dotnet build examples/Mobile.Android -t:InstallAndroidDependencies `
  -p:AndroidSdkDirectory=F:/SimdSkia/artifacts/android-sdk `
  -p:JavaSdkDirectory=F:/SimdSkia/artifacts/android-jdk `
  -p:AcceptAndroidSdkLicenses=True

dotnet publish examples/Mobile.Android -c Release `
  -p:AndroidSdkDirectory=F:/SimdSkia/artifacts/android-sdk `
  -p:JavaSdkDirectory=F:/SimdSkia/artifacts/android-jdk
```

输出为 `examples/Mobile.Android/bin/Release/net10.0-android36.0/android-arm64/publish/org.simdskia.androidsample-Signed.apk`。示例采用 Android 开发签名，正式应用需要配置自己的签名。Release 使用裁剪和 Mono AOT。

手机开启 USB 调试及 USB 安装，并允许安装弹窗。以下脚本只操作指定设备中的 `org.simdskia.androidsample` 测试应用，会清理该应用自己的两份旧诊断文件。

```powershell
adb devices -l
python tools/test-android-device.py `
  --adb F:/SimdSkia/artifacts/android-sdk/platform-tools/adb.exe `
  --serial <adb显示的设备序列号> `
  --apk examples/Mobile.Android/bin/Release/net10.0-android36.0/android-arm64/publish/org.simdskia.androidsample-Signed.apk `
  --output artifacts/android-smoke
```

脚本检查 APK 资产、冷启动、实际呈现帧、进程异常、运行程序集，并保存截图与诊断。可继续手动点击按钮、在输入框输入文字、切换前后台观察动画。

## 在其他源码项目中接入

项目目标设为 `net10.0-android36.0`，引用适配器并导入其源码宿主引用。路径按调用项目的位置调整。

```xml
<ItemGroup>
  <ProjectReference Include="../../src/SimdSkia.Avalonia.Android/SimdSkia.Avalonia.Android.csproj" />
</ItemGroup>
<Import Project="../../src/SimdSkia.Avalonia.Android/buildTransitive/SimdSkia.Avalonia.Android.targets" />
```

继承 `AvaloniaAndroidApplication<TApp>`，在 `CreateAppBuilder()` 中配置以下入口。完整 Activity、主题和单视图生命周期在本目录源码中。

```csharp
return AppBuilder.Configure<App>()
    .UseSimdSkiaAndroid(options => options.FontFiles.Add(fontFile));
```

适配器从官方 Avalonia.Android 12.1.5 包提取窗口宿主，先验证其 SHA256，再移除默认 `UseSkia` / `UseHarfBuzz` 注册及程序集引用。构建工具 Mono.Cecil 仅在开发机执行。宿主生成采用临时文件校验和原子替换，遇到未知宿主字节会明确失败。

本次固定移动端宿主为 Avalonia 12.1.5。Android API 23 是构建声明的最低版本，实际设备验证覆盖 Android 16 ARM64。

## 字体与渲染范围

引擎新增 `/system/fonts`、`/product/fonts`、`/system/product/fonts` 静态 TrueType 发现路径。部分手机的中文字体采用可变字体或 CFF，当前字体解析器会排除这些格式。示例显式携带固定 SHA256 的 OFL 站酷小薇体，使中文显示可复现。字体和许可证通过 `fetch-avalonia-font.ps1` 获取。

Android 本批使用 CPU/通用 SIMD 绘图，帧缓冲由系统显示。真机 Mono 运行时报告 `Vector=True`、`AdvSimd=False`，对应实现通过已有向量和标量分支执行。GPU 移动端绘图、更多系统版本和机型列入后续验证范围。0.1.4 更新记录随包保存在 CHANGELOG.md。

动画在 Activity 暂停时停止，恢复后继续。诊断文件保存在测试应用的外部文件目录 `/sdcard/Android/data/org.simdskia.androidsample/files`。

iOS 的 Avalonia 呈现适配仍待实现和 Mac/Xcode 验证，当前状态详见[移动端报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/ANDROID-2026-11-09.md)。
