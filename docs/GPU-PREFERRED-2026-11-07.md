# GPU 优先的桌面默认策略与 Impeller 架构借鉴（未发布源码）

本轮以用户要求的 GPU 优先为默认方向。**本文件描述工作区新增实现；NuGet 0.1.2 的默认策略仍是 CPU，已发布包没有被覆盖，也没有据此发布新版本。**

## 默认选择与显式覆盖

`UseSimdSkiaDesktop()` 现在随桌面包传递引入 GPU 适配器，并按可用表面选择：

| 系统 | 默认顺序 |
|---|---|
| Windows | Direct2D 原生 DXGI 交换链 → Direct2D framebuffer 读回 → Vulkan framebuffer 读回 → C# CPU/SIMD |
| Linux X11/XWayland | 硬件 Vulkan framebuffer 读回 → C# CPU/SIMD |
| macOS | C# CPU；Metal 核心尚无实机验收，也未接入 Avalonia 窗口，不伪称 GPU 已启用 |

保持 Avalonia OS 宿主的 software surface 配置，是为了取得 HWND/系统 framebuffer；实际绘图后端由 SimdSkia 选择，不能据此认定它还在用 CPU 绘图。原生 Direct2D 正常呈现不强制回读，Vulkan 窗口路径目前仍读回。

```csharp
// 新源码的桌面默认策略：GPU 优先。
AppBuilder.Configure<App>()
    .UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);

// 诊断、回归或明确选择 CPU：
.UseSimdSkiaDesktop(options => options.UseCpu())

// 显式后端保持严格的初始化合同；不启用自动设备回退。
.UseSimdSkiaDesktop(options =>
    options.UseGpu(SimdSkiaGpuBackend.Direct2D, allowCpuFallback: true))
```

`UseGpuPreferred()` 也可供自行配置窗口宿主的应用调用；独立 `UseSimdSkia()`、`RenderTargetBitmap` 和 SKBitmap/SKCanvas 的光栅语义保持 CPU。光栅位图并不会因为桌面默认策略而变成 GPU 纹理，此前离屏 CPU 基准也不能因此改标为 GPU。

## 设备与功能回退的合同

- 自动模式实际取得首个绘图上下文、完成设备/管线创建后才报告选中 GPU。缺少驱动/loader、设备能力、表面限制等会清理尝试对象，进入下一候选。
- 透明/分层窗口无法使用原生交换链时，可以选择 GPU 读回；不需要应用为每类窗口手动切换后端。
- 设备丢失标记当前目标损坏，让 Avalonia 重建；同一次渲染服务配置会跳过该已丢失后端，避免每个渲染循环反复创建失败设备。重新配置/启动可重新尝试。
- 不就绪的原生表面不会立即初始化 GPU。参数错误、对象已释放和 OOM 等不会被转换成成功的 CPU 回退。
- 不支持的绘图命令在自动模式允许有计数的 CPU 执行，完成 GPU→CPU 同步后绘制，需要时再上传。补齐浮点颜色、自定义 Blender、Dither、LCD、A8、网格/Lattice 等显式路由；没有直接丢弃命令。
- 软件 Vulkan 设备在创建本引擎管线前被拒绝用于桌面硬件加速。独立 VulkanCanvas 测试/使用的原有合同不变。

自动选择的诊断为进程内最近一次选择，不是所有窗口的统一状态：

```csharp
Console.WriteLine(SimdSkiaGpuDiagnostics.SelectedRenderer);
Console.WriteLine(SimdSkiaGpuDiagnostics.InitializationFailures);
Console.WriteLine(SimdSkiaGpuDiagnostics.LastInitializationFailure);
var commands = SimdSkiaGpuDiagnostics.GetStatistics();
Console.WriteLine($"GPU={commands.GpuCommands}, CPU fallback={commands.CpuFallbackCommands}");
```

初始化失败另写入 Trace；`InitializationFailures` 与命令级 `CpuFallbackCommands` 分开。不要把全 CPU 回退或软件 Vulkan 误标为硬件加速。

## 合理借鉴 Impeller 的部分

参考的是 [Flutter 官方架构目标](https://docs.flutter.dev/perf/impeller) 和 [Impeller README](https://github.com/flutter/flutter/blob/main/engine/src/flutter/impeller/README.md)，没有引入 Impeller/Google Skia 原生库或复制其 C++ 实现。

| 思路 | 本轮已做 | 明确边界 |
|---|---|---|
| GPU 优先、能力失败有回退 | 桌面自动选择、真实初始化、失效后端隔离与诊断；显式 CPU 可覆盖 | macOS 窗口 GPU 尚未实现/验收；不承诺驱动任意故障时同一帧无损恢复 |
| 离线着色器 | Vulkan 已有构建期 GLSL→SPIR-V，本轮共享经过头部/长度验证的两份二进制，避免每设备重复 MemoryStream/ToArray | 驱动仍会在创建管线时转译；不能称整个系统永远没有运行时编译 |
| 提前创建管线 | 原先首个矩形批次惰性创建的第七条 Vulkan 管线移至设备初始化；绘制前 PreparedPipelineCount=7 | 初始化成本只是前移；Direct2D 内部驱动管线不由本引擎完全控制 |
| 引擎明确控制缓存 | GPU 字形缓存由整批清空改为 LRU，保持最多256项、估算像素/clip载荷8MiB；超预算仅淘汰所需条目，常用内容继续复用 | 池容量、对象/轮廓、硬件纹理缓存和驱动内存不等于这项载荷预算 |
| 完成后释放资源 | 字形淘汰/哈希冲突释放前完成相关 GPU 队列，避免释放待处理命令借用的像素；补硬件初始化异常清理 | 淘汰可能带来同步；下一步可研究帧级退休队列，不以不安全释放换吞吐 |
| 可观测而非猜测 | 选中路径、初始化失败、命令回退、LRU淘汰与管线计数；真实窗口CPU/内存/完成时间单独测量 | 尚未完成完整 GPU capture、全资源标签或通用 frame graph |

没有同时扩张所有后端，也没有声称已达到 Impeller 的完整架构。后续真正有价值的工作包括 Vulkan 原生窗口交换链、按帧管理上传/中间纹理、离屏层的设备内处理，以及在真实目标机器验证 Metal。它们需要各自实现与画质/生命周期验收。

## 验证与复现

新增 `tests/GpuPreferred`：无设备的策略/故障注入检查纳入解决方案和 CI；Windows `--hardware` 覆盖 Vulkan/Direct2D 的小预算 LRU、在途像素生命周期与首批管线计数。精确像素参考是同一后端、每次绘制完成的非淘汰缓存路径，用于隔离缓存变更；不是用它证明与原版 Skia 画质相同。

```powershell
dotnet build SimdSkia.sln -c Release
dotnet run --project tests/GpuPreferred -c Release
dotnet run --project tests/GpuPreferred -c Release -- --hardware
dotnet run --project tests/AvaloniaGpu -c Release
dotnet run --project tests/GpuOptimization -c Release -- baseline/fonts/ZCOOLXiaoWei-Regular.ttf
dotnet run --project examples/CrossPlatform -c Release -- --smoke --controls --expect-gpu --font baseline/fonts/ZCOOLXiaoWei-Regular.ttf
dotnet run --project examples/CrossPlatform -c Release -- --smoke --controls --cpu --font baseline/fonts/ZCOOLXiaoWei-Regular.ttf
```

`--transparent` 验证透明窗口的自动选择，`--gpu vulkan --allow-cpu-fallback` 验证显式 Vulkan。窗口测试核对真实呈现帧、缩放、输入事件和未加载 Google Skia/HarfBuzz，不把离屏截图当作窗口呈现证明。

透明提示可能被软件 OS 宿主忽略：本机这次带 `--transparent` 的窗口仍进入原生交换链，说明没有触发真实 layered-window 条件。它只能记作“透明请求不崩溃”，不能作为已验证逐像素透明呈现的证据；检测到真实分层 HWND 后的自动回读路由仍需要相应宿主/窗口验证。

改动前后真实控件窗口的串行对照入口为 `tools/run-gpu-preferred-comparison.py`，保留显式 CPU、Direct2D、Vulkan 以及新默认 Auto；记录完成帧时间、CPU、进程内存和托管分配。结果见本轮证据和性能记录。既有原版严格整页画质失败与内存差距不因默认策略改变而改判。

[最终18进程性能、CPU/内存及图表](PERFORMANCE-GPU-PREFERRED-2026-11-07.md)：新默认Direct2D相对同轮显式CPU完成绘制耗时低约6.4%，但整机CPU占用高约24.9%、采样峰值WS增加30.51MiB；相对原有Direct2D耗时基本持平。Vulkan仍有读回代价。不能作为同画质整体领先或更省内存的结论。
