# VelaShell 整体兼容测试：实际接入、修正和阻碍完整替换的问题

发行关联：本报告记录测量当时的源码与结果；这些增量纳入 0.1.0-preview.9。公开状态见[发布记录](NUGET-PREVIEW.md)，未改写 preview.8 的历史内容或测量成绩。

2026-10-27，Windows x64 本机实测。目标为 [VelaShell](https://github.com/joesdu/VelaShell)
提交 `3c8a20ef739ef4fc2d3363a9d7aaf8ba15ca7b7c`，Avalonia **12.1.4**。
本轮使用 SimdSkia 当前未发布源码，**不代表公开 NuGet preview.8 已包含修正**。

**结论：受测常规界面可用，仍不适合宣称 VelaShell 已能完整替换原版、删除全部 Skia 原生库。**
CPU、Direct2D、Vulkan 的常规 23 个场景均成功绘制；深层视图测试、组合字符、
Skia 专属控件及整页一致性仍有明确缺口。以下区分构建、功能、输入、画质和原生依赖。

## 环境和隔离方式

| 项目 | 实际条件 |
| --- | --- |
| 目标应用 | 上述固定 Git 提交；浅克隆保留于 `artifacts/velashell-compat-2026-10-27/VelaShell` |
| .NET | SDK 11.0.100-rc.1.26425.128；本机 RC1 运行时，与网友日志中的 RC3 不同 |
| Avalonia | 目标项目精确使用 12.1.4，与当前适配器基线相同 |
| 构建 | Debug；后续深层诊断另用 `Optimize=true`，保留 Debug 的内部访问契约，不伪造作者签名 |
| 机器 | Windows 10.0.26300 / i7-8700K / 12 逻辑处理器 / GTX 1050 Ti |
| 数据 | 每个运行单独传 `--data-root`，不使用日常连接、凭据或单实例锁 |
| 原版 | 未改源码先构建成功；随后使用同一组诊断入口，保留原版绘图服务 |
| SimdSkia | 直接引用当前引擎/适配器源码，通过 `.UseSimdSkiaDesktop()` 配置宿主 |

目标原版及接入后的构建均为 **0 错误、9 条 SourceLink 警告**，警告是隔离克隆的调试源信息，
不能当作渲染异常。SDK 选择文件只放在实验目录，不修改用户全局 SDK。没有提交到上游或发布新包。

## 网友报告的启动异常

已用 `bad-host` 模式真实复现同一条异常：

```text
NotSupportedException: Configure the OS host for framebuffer presentation;
optional GPU drawing is selected by SimdSkia options.
at SimdSkia.Avalonia.RenderInterface.CreateBackendContext(...)
```

VelaShell 的原启动链使用 `.UsePlatformDetect()`，并通过 `ResolveRenderingMode()` 默认设置
`AngleEgl, Software`。只在末尾添加 `.UseSimdSkia()` 会留下平台图形上下文。
这一模式的进程退出码为 **2**，记录到渲染循环异常，**实际呈现帧为 0**。
同一进程仍可能用 RenderTargetBitmap 得到非空图片，因此仅凭离屏图片不能判定窗口已正常显示。

正确接入是引用 `SimdSkia.Avalonia.Desktop`，改用 `.UseSimdSkiaDesktop()`，并移除随后
覆盖 `Win32PlatformOptions.RenderingMode` 的原设置。GPU 通过 SimdSkia 的 `UseGpu(...)` 选择。
VelaShell 的「硬件加速」设置还需要映射到这些后端；测试入口是显式选择，不是该设置的完整迁移。
原 `CreateBackendContext` 防护仍保留，没有简单吞掉异常。

## 测试中实际修复的两项问题

### 合成字体的 Try 返回合同

初次接入时主窗口能显示，但设置窗口初始化失败，截图为透明。目标自身的崩溃日志显示：

```text
Synthetic bold/italic font outlines are not implemented.
at SimdSkia.Avalonia.FontManager.TryCreateGlyphTypeface(Stream, FontSimulations, ...)
...
at VelaShell.Views.MainWindow.OpenSettingsAsync(...)
```

固定 Avalonia 12.1.4 源码允许该 Try 接口返回 `false/null`，由字体集合保留最近的真实字面。
本轮按合同返回失败，保留真实字重元数据，而不是抛异常中断整个窗口布局。
**没有实现伪造粗体/斜体**；强制用合成标志构造的字形仍会明确拒绝。
某些字重会与原版合成效果不同，这是保留的画质差距。

回归验证了 Bold、Oblique、两者组合的拒绝结果、不消费字体流，以及真实字面的回退元数据。

### 图片 Uniform 布局的浮点误差

字体修正后，支持页的普通 PNG 图片仍失败。实际参数为：

```text
Source [0, -5.684342E-14, 620, 736]
inside a 620 x 736 bitmap
```

这是正常布局减法产生的舍入尾差。本轮在 Avalonia 边界按图像尺寸的 **8 个 double ULP**
归一化接近边界的值，再转成 float。真正越界继续报错；没有放宽为任意像素容差。
回归确认该尾差与完整图片逐字节相同，`-0.01` 像素的真实越界仍被拒绝。
核心异常消息另补充实际坐标和图像尺寸，便于定位类似问题。

最终 **9,750 项**控件/绘图/字体/生命周期回归、**13 项**系统字体检查通过。

## 真实桌面常规场景

23 个场景包含：主窗口、15 个设置分页、浅色主题、中英文切换、终端 Latin/CJK/ANSI 样式、
可编辑多行文本框。窗口必须已显示、图片必须非单色，异常与呈现计数同时记录。
独立组合字符失败例不混入这组常规通过数。

| 后端 | 场景成功 | 渲染异常 | 实际呈现帧 | 硬件命令 / 完成批次 |
| --- | ---: | ---: | ---: | ---: |
| 原版 Avalonia.Skia | 23/23 | 0 | 原版未提供同一诊断计数 | 不用替代计数估算 |
| SimdSkia CPU | **23/23** | **0** | **32** | 不适用 |
| SimdSkia Direct2D | **23/23** | **0** | **31** | **3809 / 31** |
| SimdSkia Vulkan | **23/23** | **0** | **34** | **3872 / 34** |

Direct2D 记录 **31 次原生交换链呈现**；Vulkan 采用完成后回读再交 framebuffer 呈现。
两种 GPU 的受统计后端命令没有记录 CPU fallback。普通图层及离屏 Bitmap 仍可使用 CPU，
这不等于整个程序所有步骤都在 GPU。

这些模式下保存的页面 PNG 来自 **CPU RenderTargetBitmap**；硬件执行由独立 GPU 计数证明。
不能把这些 PNG 称作 GPU 交换链画质对照，也不能用一秒一步的页面测试推导吞吐或 CPU 占用率。

## 上游测试和边界

上游的真实终端像素测试（不是空的 HeadlessDrawing）在原版和 SimdSkia 上均 **9/9 通过，0 跳过**。
覆盖字形实际有墨水、批量字形寿命、反白/彩色文字、选区、高亮与中文 IME 预编辑。
这不覆盖任意 Unicode shaping，也不等于实际系统输入法端到端测试。

原版上游 Views 套件 **548/548 通过，0 跳过**。
SimdSkia 的首次完整运行在资源监视页阻塞，停止前报告 **334 通过、4 失败，共 338 项**；
**其余 210 项没有完成，不能当成跳过后通过。** 三条滚动/滚动条超时共享已被占住的 UI 线程，
不应把后续排队超时都解释为独立错误。

标题栏失败在单独进程及 `Optimize=true` 下重复出现：
`RecordingPlayerTitleBarUiTests.DoubleClickingAButton_DoesNotMaximize_ButTheBlankTitleBarDoes`。
空白标题栏双击后仍为 Normal，而原版为 Maximized；尚未修复。

资源监视 `NicStrip_WheelOverTheCards_ScrollsSideways` 在优化构建下单独运行也超过 **45 秒**，
被测试超时机制终止。只读 dotnet-stack 捕获到活动 UI 线程主要位于：

```text
ManagedFilters.Blur
DrawingContext.DrawBoxShadow
DrawingContext.DrawShadowedRectangle
... compositor render ...
ResourceMonitorUiTests.NicStrip_WheelOverTheCards_ScrollsSideways
```

因此这既是兼容性阻碍，也是此类真实页面的性能问题。独立有界阴影缓存实验没有消除超时，
已撤回，源码和日志只留在 artifacts；没有关闭阴影或把超时改判为通过。
其余视图采用排除 `ResourceMonitorUiTests` 的相同过滤器、优化 Debug 构建另行复测：

| 这组共选择 514 项 | 原版 | SimdSkia |
| --- | ---: | ---: |
| 通过 | **514** | **393** |
| 失败 | 0 | **7** |
| 未完成、无最终结果 | 0 | **114** |

7 项失败分别为本地路径选择器的回车/焦点行为、录像播放器标题栏双击，以及 5 种启动预览
样式/主题的像素断言。测试随后在 `AppearancePage_OffersTheSixChoicesWithALivePreview`
等待超过 45 秒而中止。没有把这 114 项标为「跳过后通过」。单独复测也重现标题栏失败。
首轮与过滤后复测有重叠，**不相加计算一个虚假的总通过数**。

过滤组原版完成约 1 分 2 秒，替代的部分运行约 7 分 54 秒后中止。这是测试执行记录，
不是受控吞吐基准，但说明当前深层交互场景仍有明显问题，不能用常规页面通过数覆盖它。

组合字符在独立进程输入 `e\u0301`、`A\u030A` 时可重复导致未处理异常：
`Contextual/combining text shaping is not implemented (U+301)`，进程退出码 **-532462766**。
这是实际终端输出可触发的问题，仍未解决。

## 整页差异与直接 Skia 依赖

普通功能测试全绿不等于原版画质一致。使用原版应用默认字体/排版，按既有最大通道误差 32、
RMSE 2 做整页差异诊断，CPU **0/23 达标**，没有放宽门槛。

| CPU 页面 | 最大通道误差 | RMSE |
| --- | ---: | ---: |
| 主窗口 | 162 | 2.523 |
| 常规设置 | 255 | 18.477 |
| 外观设置 | 255 | 15.744 |
| 支持页 | 255 | 52.281 |
| 英文终端 | 211 | 3.579 |
| 中文终端 | 227 | 3.325 |
| 文本框 | 255 | 7.939 |

差异包含字形 hinting/覆盖率、真实字面回退、字体度量引起的布局位移和 Skia 专属预览缺失。
二维码等高对比图片整体移动几像素也会产生很大的通道误差，不意味着图片本身全部解码错误。
部分页面含动画和光标，未冻结动画状态；这些数字用于迁移后整页诊断，不能将全部误差归于
单一光栅算法或作为静态 GPU 像素验收。画质及布局兼容仍未成立。

另外，目标程序 `Splash/` 使用原版 SK* 绘制原生启动画面；设置里的 `SplashPreview` 请求
`ISkiaSharpApiLeaseFeature`。SimdSkia 不提供原版对象租约，该控件会提前 return，导致预览缺失。
**这项即使不抛异常也按不兼容记录。** 三种 SimdSkia 运行中仍实际加载了 `libSkiaSharp.DLL`。
关闭单独启动画面只是为了隔离桌面测量，不能据此声称已经删除原生依赖。

## 复现、查看和剩余范围

可复现工具与说明：[VelaShellProbe](../tools/VelaShellProbe/README.md)。
运行记录、源哈希、隔离修改补丁、TRX 和截图：[证据](evidence/velashell-2026-10-27/results.json)。
目标数据与完整日志在被忽略的 `artifacts/velashell-compat-2026-10-27`，报告不分发其配置和密钥文件。

手动查看本机测试副本：

```powershell
$env:VELASHELL_PROBE_BACKEND = 'cpu'
$env:VELASHELL_PROBE_INTERACTIVE = '1'
$env:VELASHELL_NO_SPLASH = '1'
& F:/simdSkia/artifacts/velashell-compat-2026-10-27/VelaShell/src/VelaShell/bin/Debug/net11.0/VelaShell.exe --data-root F:/simdSkia/artifacts/velashell-manual
```

完整替换仍需完成：组合字符与更多文字排版、资源监视阴影/滚动性能、标题栏输入案例、
Skia 专属启动画面/预览的迁移、整页画质和布局。远程 SSH/SFTP 真实服务器、全部插件功能、
Linux/macOS、VelaShell 的 AOT 以及新版 RC3 运行时，本轮没有验收。

本轮为功能与问题定位测试。Debug 进程采样包含截图、日志、预览和诊断缓冲，不能用于宣称
速度或常驻内存追平；已有常规控件基准成绩也不能覆盖这里暴露的阴影热点。
