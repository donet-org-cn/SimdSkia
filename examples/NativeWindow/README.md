# 不依赖 Avalonia 的 SimdSkia 窗口

Windows x64 的真实 Native AOT 示例：Win32 创建窗口和处理输入，SimdSkia CPU 绘制背景、矩形、抗锯齿圆和中英文文字。GDI 只将已经绘制完成的 BGRA 像素复制到窗口。支持调整大小、点击窗口切换矩形颜色、关闭及资源释放；`WinExe` 不创建命令行窗口。

2026-10-23 最小发布实测：纯图形 EXE **1.172 MiB**、完整部署 **1.185 MiB**；中英文 EXE **1.311 MiB**、完整部署 **1.324 MiB**。每个最终 EXE 都通过外部窗口全 RGB 像素、调整大小和点击验证。[最小发布报告](../../docs/NATIVE-WINDOW-MINIMUM-2026-10-23.md) · [此前包含诊断代码的测量](../../docs/NATIVE-WINDOW-AOT-2026-10-23.md)。

## 构建和运行

需要 Windows x64、.NET 10 SDK，以及 Visual Studio 的 C++ 桌面开发工具和 Windows SDK（Native AOT 链接器使用）。从仓库根目录执行：

```powershell
# 最小中英文版；实际选中的发布配置
dotnet publish examples/NativeWindow/NativeWindow.csproj -c Release -r win-x64 -p:PublishProfile=MinimumSize -p:IncludeText=true -o artifacts/my-small-window
& .\artifacts\my-small-window\NativeWindow.exe

# 最小纯图形版
dotnet publish examples/NativeWindow/NativeWindow.csproj -c Release -r win-x64 -p:PublishProfile=MinimumSize -p:IncludeText=false -o artifacts/my-small-shapes

# 保留地区格式化、异常堆栈和内置验证的版本
dotnet publish examples/NativeWindow/NativeWindow.csproj -c Release -r win-x64 -p:IncludeText=true -o artifacts/native-window-text
& .\artifacts\native-window-text\NativeWindow.exe
```

`MinimumSize` 只作用于本示例：使用 `OptimizationPreference=Size`、不变全球化、关闭异常堆栈/调试器/EventSource/Metrics、精简系统异常资源文字、直接导入 Windows 系统 DLL，并移除内置测试代码。仍保留异常处理和绘制质量。它不能直接当作任意业务应用的默认配置：需要地区敏感的日期/数字格式、排序，或需要完整诊断时，请使用普通配置。最小版由外部脚本测试，不提供 `--verify` 内置入口。

本机已验证的最小成品位于 `artifacts/native-window-minimum/publish/portable-text` 和 `portable-shapes`；对应 `native-window-minimum-text.zip` / `native-window-minimum-shapes.zip` 包含许可证、没有 PDB。

文字版读取 `%WINDIR%\Fonts\msyh.ttc` 第一字面，通过 SimdSkia 的 C# TrueType 实现绘制。没有内嵌或复制字体；目标机器没有微软雅黑时会明确报错，可修改 `Program.cs` 的字体路径选择已安装的中文 TrueType 字体。这里演示基本的单行中英文，不包括复杂文字排版或输入法编辑控件。

窗口宿主仅适用于 Windows；SimdSkia 绘图核心仍跨平台。本示例没有按钮、输入框、布局引擎、数据绑定、无障碍或 GPU 后端；不能直接代替完整 Avalonia 应用框架。

## 复现实测

```powershell
# 新 checkout 先生成基线（当前普通配置），已有冻结基线时不要覆盖
python tools/test-native-window-aot.py
# 外部测试实际最小 EXE；已有输出时指定新的目录
python tools/test-native-window-minimum.py --output artifacts/native-window-minimum/new-run
```

脚本需要可见桌面，会依次发布两种变体并短暂显示各自的窗口，验证初始呈现、调整大小和点击变色后自动关闭。它检查绘制缓冲和窗口 DC 的实际像素、曲线抗锯齿、中英文版的中文墨迹、GUI PE、Native AOT 标志及进程模块，拒绝加载 CoreCLR/JIT 或原版绘图库的结果。

输出位于 `artifacts/native-window-aot`：

- `portable-text/NativeWindow.exe`、`portable-shapes/NativeWindow.exe`：从无 PDB 的部署目录实际运行验证过的程序；不带参数时保持窗口打开。
- `native-window-text.zip`、`native-window-shapes.zip`：可分发文件，包含许可证，不包含 PDB。
- `results.json`：逐文件大小、SHA-256、构建命令、源码哈希及运行检查。
- `verify-*/frame-*.bmp`：绘制缓冲快照；`metrics.json`：模块和验证结果。
- `publish-*.log`：构建日志。

普通配置包含内置验证；最小配置把测试移到外部 Python 进程。两者均使用本机 .NET 10 实际支持的 `OptimizationPreference=Size`，未使用 UPX 或手工修改 EXE。早期配置写过 `IlcOptimizationPreference`，在此 SDK 未生效，已修正。未使用的库代码由 AOT 裁剪；源码项目引用兼容层的完整依赖图，不等于最终 EXE 保留全部绘图、编解码和 GPU 功能。

最小验证脚本会比较四组配置，包括一个**故意保留为失败对照**的 `MinimalMetadata=true` 实验。移除反射元数据导致本机进程在创建窗口前退出，该变体不会入选发布包；正式 `MinimumSize.pubxml` 明确保持 `MinimalMetadata=false`。脚本记录全部失败项并只打包通过验证的最小候选。
