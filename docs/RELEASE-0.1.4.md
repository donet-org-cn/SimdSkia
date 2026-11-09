# SimdSkia 0.1.4 Android NuGet 接入

本版新增 `SimdSkia.Avalonia.Android`，十三个包统一为0.1.4正式版，目标.NET 10。Android窗口宿主固定Avalonia 12.1.5，绘图采用C# CPU/通用SIMD。桌面适配器的已验证版本矩阵为12.1.0–12.1.5。

```sh
dotnet add package SimdSkia.Avalonia.Android --version 0.1.4
```

在Android启动Application的 `CreateAppBuilder()` 中使用以下入口，配置受支持的静态TrueType字体。

```csharp
return AppBuilder.Configure<App>()
    .UseSimdSkiaAndroid(options => options.FontFiles.Add(fontFile));
```

现有模板的 `Avalonia.Android` 直接引用需要移除，由本包提供宿主。Activity使用AppCompat主题。完整字体资产、Application、Activity和单视图配置见[Android使用说明](../examples/Mobile.Android/README.md)。桌面应用继续使用 `SimdSkia.Avalonia.Desktop`。

## 更新内容

- Android宿主固定SHA256校验，移除默认Skia/HarfBuzz注册与程序集引用，校验临时产物后原子发布。
- 新增Android静态系统TrueType字体目录发现，可显式选择字体。示例使用带OFL许可证的中文静态TTF。
- 新增中文、输入框、按钮、动画进度条及圆角渐变示例，Activity暂停/恢复时管理动画。
- 修复首次真机验证发现的AppCompat主题启动退出，补充深色启动背景。
- Android包独立缓存消费、Release/Mono AOT构建、APK依赖和许可证检查纳入发行流水线。NuGet包携带使用说明和累计CHANGELOG。

## 验证范围

Android16 ARM64源码真机记录覆盖中文、触摸、系统输入、动画像素变化和前后台恢复。前轮源码APK为14.57MiB，记录保存在[真机报告](ANDROID-2026-11-09.md)。本次手机已断开，按用户选择沿用该真机记录，新增NuGet包另做独立缓存消费构建和内容验证。最终包结果以[发行记录](NUGET-PREVIEW.md)为准。

Android API23为项目声明的最低版本，其他版本和机型仍待实测。Android GPU、可变/CFF系统字体，以及iOS的Avalonia呈现适配列为后续工作。iOS还需要Mac/Xcode构建和设备验证。

API元数据仍按SkiaSharp3.119.4基线验证，五项原生合同缺口继续列在报告中。桌面既有性能数据见[热点报告](PERFORMANCE-HOTSPOTS-2026-11-07.md)，本次为移动端接入和打包交付，JPEG、文字/曲线及整页画质和部分性能/内存差距继续列入兼容报告。
