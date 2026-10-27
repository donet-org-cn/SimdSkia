# Avalonia Native AOT 对照应用

两份项目编译同一套 `Shared/*.cs` 和既有的真实控件场景 `tests/AvaloniaControls/ControlScene.cs`。
SimdSkia 版使用 `.UseSimdSkia`；Original 版使用原版 `.UseSkia().UseHarfBuzz()`。
固定 Avalonia 12.1.4、SkiaSharp 3.119.4、HarfBuzzSharp 8.3.1.3、.NET 10。
窗口均使用 Win32 软件呈现，未测 GPU。SimdSkia 发布物不包含 Skia/HarfBuzz/ANGLE 原生组件。

Windows 现在以 `WinExe`（GUI 子系统）构建，双击 exe 不创建额外命令行窗口。自动测量通过父进程重定向 stdout/stderr，仍可读取 READY 和测量结果。新版运行包位于 `artifacts/aot-gui/portable`、`artifacts/aot-gui/v3`，各有两种后端；旧性能目录保留实测时的控制台版本和哈希。Windows 普通 AvaloniaControls 示例同样改为 WinExe，其他系统保留原构建方式。

## 发布与交互运行

在仓库根目录执行，需要 .NET 10 SDK、Windows x64 和 MSVC C++ 桌面工具链/Windows SDK。
不需要在运行发布后的 exe 的机器上安装 .NET。

```powershell
powershell -NoProfile -File tools/fetch-avalonia-font.ps1
dotnet publish examples/AvaloniaAot/SimdSkia/AvaloniaAot.SimdSkia.csproj -c Release -o artifacts/aot-gui/portable/simd
dotnet publish examples/AvaloniaAot/Original/AvaloniaAot.Original.csproj -c Release -o artifacts/aot-gui/portable/original
& artifacts/aot-gui/portable/simd/AvaloniaAot.SimdSkia.exe
& artifacts/aot-gui/portable/original/AvaloniaAot.Original.exe
```

默认显示可交互窗口，包含按钮、输入框、复选框、中英文文字、圆角、曲线、渐变、图片和透明组。
`--full-opacity` 要求原版使用完整透明组；原版默认使用 Avalonia 的逐次绘制透明度优化。
SimdSkia 始终进行完整组混合，因此报告同时保留原版默认和完整组的数据。

可选 `-p:ComparisonIsa=v3` 编译 x86-64-v3（含 AVX2）专用版本，必须发布至独立目录。
该版本提高最低 CPU 要求，不应分发到不支持 x86-64-v3 的机器。默认项目仍保持通用 x64 构建。
原版项目使用完全相同的 AOT 配置；原版 Skia 原生 DLL 自身的 CPU 分派由上游构建决定。

## 自动测量

以下命令会发布并串行启动独立进程，每组运行三次，轮换次序，预热 2 秒、测量 5 秒。
需要 Python 3，无额外 Python 包。输出目录必须尚不存在。

```powershell
python tools/run-avalonia-aot.py --output artifacts/aot-comparison/portable
python tools/run-avalonia-aot.py --isa v3 --publish artifacts/aot-comparison/publish-v3 --output artifacts/aot-comparison/v3
```

已经完成相同 ISA 发布后，可添加 `--no-build`；脚本会检查 exe 的 PE 头、运行时标志、没有 CoreCLR/JIT 的模块清单和 AVX2 标志，拒绝混入 JIT/错误 ISA 的结果。`PublishAot` 生成的 runtimeconfig 即便用于 JIT 运行，也可能令 `IsDynamicCodeSupported=false`；该标志和 apphost 的 PE 头均不能单独证明 AOT，单个 exe 内的标志不是完整验证。完整对照请使用父脚本。

测试包含 1000×720 控件、1500×1080 控件、1000×720 滚动列表的不限帧绘制，以及控件和列表的 60 FPS 定时更新。
不限帧吞吐计时仅包含 `RenderTargetBitmap.Render` 和指定的列表平移更新，排除 PNG 保存；并非屏幕显示 FPS。
60 FPS 模式用异步定时器释放 UI 线程，CPU 包含离屏绘制、调度和实际 Win32 软件窗口的呈现工作。
启动指标为父进程启动子进程至第一次离屏绘制完成的 READY 信号，包含字体/主题/窗口创建；不冒充 DWM 首次显示时间，也未清空文件缓存。

内存包括整个进程的工作集、私有提交、峰值、托管堆、父进程每 25ms 采样的稳态内存和工作集占物理 RAM 的比例。
CPU 同时提供单核比例和除以全部逻辑处理器的整机比例。PDB 单列，运行目录的其他文件全部计入体积；字体、DLL 和 ZIP 均列出明细和 SHA256。

每个 exe 都验证真实 Fluent 控件模板、Win32 初始化、字体加载、ImageBrush 的指定像素；SimdSkia 另外验证确实呈现了软件窗口帧且未加载原版原生模块。
两边使用相同 OFL 字体 SHA256。画质沿用最大通道误差 32、RMSE 2 的既有整页门槛，失败仍保留性能数据并返回退出码 **2**。
退出码 **1** 表示构建、运行或测量本身失败。当前画质差距和兼容性限制见 [实际报告](../../docs/AVALONIA-AOT-COMPARISON.md)。

直接运行单个短探针：

```powershell
& artifacts/aot-comparison/publish/simd/AvaloniaAot.SimdSkia.exe --hidden --output artifacts/aot-comparison/manual-smoke --warmup 0 --seconds 0.2
```

`metrics.json`、`scene.png`、原始 `scene.rgba` 写入指定目录。`--hidden` 的窗口位于屏幕之外，自动完成后退出；正常运行不带这些参数即可操作窗口。

对已经测完的结果、四份发布物/ZIP 和 JIT 伪 AOT 标志进行审计：

```powershell
python tools/test-avalonia-aot.py --portable artifacts/aot-comparison/portable --v3 artifacts/aot-comparison/v3
```

运行时来源的最终判定包含没有加载 CoreCLR/JIT，不能只读取 `metrics.json` 中的布尔标志。每个测量目录保留 `run-harness.py.txt`，用于对照执行时的父脚本 SHA。

## preview.8 通用 x64 优化与对照

最新[性能与验收报告](../../docs/PARITY-OPTIMIZATION-2026-10-27.md)包括 75 个通用 AOT CPU、36 个 JIT GPU 正式进程；[博客园文章与配图](../../docs/blog/2026-10-27-simdskia-avalonia-aot-cnblogs.md)使用这组新结果。CPU 的源码候选由 `tools/publish-parity-candidate.py` 发布，再用 `tools/run-cpu-parity.py` 对照冻结的原版及 preview.7；GPU 使用独立 `benchmarks/AvaloniaGpuParity`。图文生成器为 `tools/report-preview8-cnblogs.py`。

速度部分通过本机固定场景门槛，但原版整页画质仍失败，常驻内存未追平；GPU 测量不能代替窗口呈现。预览包上架及跨平台检查见[发布记录](../../docs/NUGET-PREVIEW.md)。

## preview.7 历史包消费复测

2026-10-26—27 新测量使用已发布的包。给 SimdSkia 项目的发布命令增加 `-p:UsePackedSimdSkia=true`，会引用 `SimdSkia.Avalonia 0.1.0-preview.7`；默认不加时继续使用源码工程。两边均为相同 Win32 CPU 宿主与固定 CJK 字体。

历史数据包含通用/v3 两组 90 个正式进程、默认空白窗口另外 6 个进程的资源测量、完整失败画质门槛及源码/程序集哈希，保留在 `docs/blog/2026-10-27-simdskia-assets/data`。当前文章已更新为 preview.8 实现的性能结果；其中默认应用体积仍明确引用 preview.7 的独立空白模板实测，未使用本复杂控件基准的发布体积替代。

图文由 `tools/report-cnblogs-simdskia.py` 从已结束的测量结果生成，绘图依赖为开发工具 matplotlib（本次 3.11.2），不进入产品 NuGet 或正式测量进程。默认空白窗口的独立采样工具为 `tools/measure-default-avalonia-idle.py`。
