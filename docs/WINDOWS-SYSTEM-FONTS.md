# Windows 系统默认字体

源码更新（尚未包含在公开 preview.5）：系统字体注册先读取元数据，首次使用字面才加载字形；同一 TTC 的字面共享文件快照。枚举家族/样式不再加载所有回退字形，显式 `FontFiles` 仍保留完整注册校验。系统字体文件在发现与使用间改变家族/样式会报错。三轮真实 CPU 窗口工作集 252.6→116.0 MiB，原版对照及剩余差距见 [内存报告](MEMORY-PERFORMANCE-2026-10-22.md)。

Windows 上现在可以省略字体配置和字体资源文件：

```csharp
AppBuilder.Configure<App>()
    .UseStandardRuntimePlatformSubsystem()
    .UseWin32()
    .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
    .UseSimdSkia()
    .StartWithClassicDesktopLifetime(args);
```

空的 `FontFiles` 列表也采用这一默认行为。已有显式配置保持不变：

```csharp
.UseSimdSkia(options => options.FontFiles.Add("fonts/MyChineseFont.ttf"))
```

默认家族取自 Windows 当前的消息界面字体，不固定写成“微软雅黑”。本机实际返回 `Microsoft YaHei UI`，会选择 TTC 中对应的 UI 字面。这里只跟随家族名称，字号、字重仍由 Avalonia 控件和样式决定。

实现通过只读的 `SystemParametersInfoW(SPI_GETNONCLIENTMETRICS)` 查询 `lfMessageFont`，通过 HKLM/HKCU Fonts 注册信息定位字体文件。布局见微软的 [NONCLIENTMETRICSW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-nonclientmetricsw) 和 [LOGFONTW](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-logfontw)。没有创建原生字体句柄，也不调用 GDI/DirectWrite/FreeType/HarfBuzz 进行字体解析、排版或光栅化。

字体由 C# 直接从已安装的 TTF/TTC 读取，不复制到输出目录。除默认家族的真实字面外，读取已安装的 Segoe UI、Consolas、Segoe UI Symbol，以及一个可用的中文回退家族（依次考虑微软雅黑、宋体、黑体）。这是有界的回退集合，不提前解析整个字体目录，也不代表系统中所有字体都已注册。每次启动读取一次，运行中修改系统字体设置需要重启应用。

Windows 字体发现没有新增 NuGet 或随程序分发的原生组件，也不分发 Windows 字体。preview.2 已增加 Linux/macOS 的纯 C# 自动系统字体发现，见[跨平台说明](CROSS-PLATFORM.md)。默认字体缺失或采用尚不支持的 CFF、可变、彩色等格式会明确报错；本轮没有扩大字体格式、hinting、复杂排版及 emoji 支持。中文显示依赖本机安装了可用的中文字体。

## 验证

```powershell
dotnet run --project tests/WindowsSystemFonts -c Release
dotnet run --project tests/WindowsSystemFonts -c Release -- baseline/fonts/ZCOOLXiaoWei-Regular.ttf
```

本机通过系统模式 13 项、显式模式 10 项断言，检查默认名称、正确 TTC 字面、真实中文/Latin 字形、粗体、抗锯齿像素和原生字体库隔离。显式模式检查未自动加载系统字体，既有固定字体控件回归的 8109 项断言也通过。截图位于 `artifacts/windows-system-fonts`。

真实 RemoteNetwork 应用已改用零参数注册，移除资源字体及复制设置。无资源字体的 11 个 JIT 界面场景通过，包括中文登录、二维码、六个设置页、个人资料、服务器编辑和 ANSI 粗体终端。后续查明旧实例持有单实例锁且窗口隐藏，修复了业务程序重复启动时不恢复窗口的问题；旧实例退出后，已实际启动 `win-x64-startup-fix` 的 AOT 程序，验证登录和设置窗口绘制、正常退出以及无 CLR/JIT/Skia/HarfBuzz/ANGLE 模块。本轮还用真实 NuGet 包单独编译并运行系统字体 AOT 窗口。原始日志分别在 `artifacts/remotenetwork-startup-fix` 与 `artifacts/nuget-consumer`，没有把历史启动结果冒充新包验证。
