# 纯 C# 追上 Skia？Avalonia 绘制耗时降了 59%，CPU/GPU 与内存实测

之前测 SimdSkia 时，最刺眼的数字来自通用 x64 AOT：同一张 Avalonia 控件页，原版只要约 0.92 ms，我们却要 1.87 ms。只展示 AVX2 的好成绩没有解决用户默认发布程序的实际问题。

这次把普通 x64 当成主要优化对象，做了 SSE2 合成与采样、字形缓存、填充和分配优化，再与原版重新测量。结果是同一控件页 **1.8722→0.7611 ms/帧**，耗时降低 **59.3%**；同轮原版为 **0.9159 ms/帧**。

本文对应 **SimdSkia 0.1.0-preview.8** 的渲染实现，固定 **Avalonia 12.1.4 / SkiaSharp 3.119.4 / HarfBuzzSharp 8.3.1.3**。这是一个公开限制的实验性预览版：**本机已测速度门槛通过，完整画质仍未通过，常驻内存也没有追平原版。** 不能据此宣称任意 Avalonia 项目都能无差别替换。

代码、NuGet 包、使用示例及当前上架状态：[GitHub：hnlyf168/SimdSkia](https://github.com/hnlyf168/SimdSkia)、[preview.8 发布记录](https://github.com/hnlyf168/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)。

## 先看结果与适用范围

| 测量 | 结果 | 不能据此推断的内容 |
| --- | --- | --- |
| 通用 x64 AOT CPU 吞吐，三个真实控件场景 | 综合耗时低于原版 **4.4%**，最慢场景落后 **3.8%** | 任意操作、字体或硬件都更快 |
| CPU 含两组 60 FPS 节拍，共五场景 | 综合耗时低于原版 **6.7%**；相对 preview.7 各场景减少 **50.2%–62.7%** | 屏幕帧率提高相同比例 |
| Vulkan / Direct2D 完成帧 | 综合耗时低于每例较快原版 GPU **29.8% / 35.3%** | GPU 窗口呈现、AOT 或所有驱动都有同样收益 |
| 普通控件页进程工作集 | SimdSkia **79.89 MiB**，原版 **60.91 MiB** | 内存已经追平 |
| 60 FPS 滚动托管分配 | preview.7 **155.14** → 本轮 **28.05 KiB/帧**，原版 **12.87** | 常驻内存会按相同比例下降 |
| 默认空白项目 AOT 体积，**preview.7 历史实测** | 运行文件 **36.53→19.51 MiB**，少三个原生 DLL | 这是 preview.8、GPU 或业务项目的体积 |

表中的“综合”取各场景耗时比的几何平均数，CPU 和 GPU 分开计算。门槛为综合比 ≤1.00、每例 ≤1.10；不把耗时、CPU%、内存和画质混成一个分数。

## 如何测量，避免比较错对象

硬件为 Windows 11 / **i7-8700K（6 核 12 线程）/ 31.93 GiB RAM / GTX 1050 Ti**，SDK 10.0.402，.NET 10.0.13。正式测量日期为 2026-10-27。双方使用相同 Fluent 控件树、尺寸与 ZCOOL XiaoWei 中英文字体，包含按钮、文本框、边框、透明度、图片画刷和滚动列表。

- **CPU：75 个串行进程。** 原版、已发布 preview.7、本轮源码候选各五次；每次预热 2 秒、采样 5 秒。100%/150% DPI 控件页、滚动列表做吞吐测试，控件页和列表另外做 60 FPS 节拍测试。
- **GPU：36 个串行进程。** 原版 Vulkan、原版硬件 ANGLE/D3D11、SimdSkia Vulkan、SimdSkia Direct2D 各三次；预热 3 秒、采样 5 秒。每例以两个原版中较快者作速度门槛。
- 顺序逐轮轮换，正式采样期间不同时编译、生成图表或运行其他基准；图中柱和表格取独立进程中位数，原始数据保留所有进程。

CPU 使用**通用 x64 Native AOT**，实际检查 PE 与进程模块，没有 CLR/JIT，AVX2=false。原版的 Skia 原生 DLL 保留自己的正常指令集分派；不能把 C# 的 AVX2 标志解释成双方底层都关闭了 AVX2。

GPU 使用 **.NET 10 JIT、离屏完整帧与 BGRA 回读**。每帧包含 Avalonia 绘制、提交、等待 GPU 完成和全帧回读；不是只量提交调用，也不是显示器 FPS。两种生产后端中的 C# 字形图集、软件图层和回退照常计入，诊断计数保留。Direct2D 真窗口采用交换链，而本次为了共同可读的完整帧对照仍执行回读，因此不能套用为窗口性能结论。

原版 CPU 保留默认逐绘制透明度优化，SimdSkia 使用完整组透明度语义；原版的渲染策略没有为基准刻意改慢。两者现存的画质差距也保留，后文给出失败数据。

## 通用 x64：先把默认发布程序优化好

{{CPU_TIME_TABLE}}

![通用 x64 CPU 绘制时间](2026-10-27-simdskia-preview8-assets/cpu-time.png)

普通控件页已经从明显落后变成较快；150% DPI 和吞吐滚动仍分别落后约 1.2%、3.8%。因此准确结论是**这组场景综合达到目标，仍有个别场景稍慢**。

这轮主要改了五处：

1. **补齐 SSE2 热点。** 通用 AOT 不能靠 AVX2 成绩兜底。字形覆盖率、图像透明合成与双线性采样增加 SSE2 路径，保留整数除法、浮点舍入、预乘 Alpha、stride 和短尾结果。
2. **减少重复填充成本。** 白色/透明连续缓冲走字节填充；适合的中等色块初始化后批量复制；超出受测范围的缓冲保留原路径。黑色字形检查实际目标像素后采用等价公式。
3. **让缓存命中更便宜。** 按不可变字形串身份查询，避免每帧复制大键、重排链表和反复写入 GC 引用；淘汰仍有明确预算。串预算从 2 MiB 调到 4 MiB，内存代价没有隐藏。
4. **减少无用的状态工作。** opacity=1 且没有实际离屏层时减少脏区记账；纯平移字形不再计算未使用的逆矩阵。
5. **控制帧内分配和复制。** 画布缓存容器按需创建，紧密 framebuffer 整块复制，带 padding 的缓冲仍逐行处理。

没有以减少普通路径抗锯齿采样、忽略颜色处理或跳过控件绘制来获得这些成绩；本轮 CPU 页面捕获与 preview.7 逐字节相同。

## CPU 占用和内存：速度提升没有解决所有问题

CPU% 使用 `进程 CPU 时间 / 墙钟时间 / 12 × 100%`。无限吞吐场景会尽量跑满一个逻辑核，两边都在约 8.3%，不能仅看这个百分比判断效率；还应看 CPU ms/帧和每帧处理器周期。

{{CPU_RESOURCE_TABLE}}

![CPU 工作集](2026-10-27-simdskia-preview8-assets/cpu-memory.png)

![60 FPS 整机 CPU 占用](2026-10-27-simdskia-preview8-assets/cpu-paced.png)

![60 FPS 每帧托管分配](2026-10-27-simdskia-preview8-assets/cpu-allocation.png)

普通控件页工作集 **79.89 vs 60.91 MiB**，私有提交 **59.96 vs 36.40 MiB**，仍明显高于原版。滚动的每帧分配大幅下降，但常驻缓存仍存在，工作集没有按 82% 的分配降幅一起下降。

60 FPS 的进程 CPU 时间受 Windows 短时调度记账影响，逐进程数据另保留 `QueryProcessCycleTime`。例如普通控件页，原版/本轮约 **4.958 / 4.341 百万周期/帧**；不能把表中 CPU 时间的降幅直接当成相同比例的计算工作下降，更不能把偶发 0 当成没有开销。周期也不能直接换算成毫秒。[Windows API 说明](https://learn.microsoft.com/en-us/windows/win32/api/realtimeapiset/nf-realtimeapiset-queryprocesscycletime)。

WS 是驻留工作集，Private 是私有提交，两者不相加。托管分配只描述 GC 管理的分配，不代表原版没有原生分配。两边的字体、缓存、运行时与原生资源均留在进程测量中；GPU 表不是独立显存统计。

## Vulkan 与 Direct2D：等待实际完成后比较

{{GPU_TIME_TABLE}}

![GPU 完成帧时间](2026-10-27-simdskia-preview8-assets/gpu-time.png)

Vulkan 综合耗时比为 **0.7021**，Direct2D 为 **0.6474**。每例都比较较快原版，未只挑慢的原版后端。原版 Vulkan 与原版 ANGLE 也不是同一种实现路径，完整表中分别保留。

{{GPU_RESOURCE_TABLE}}

![GPU 进程 Private](2026-10-27-simdskia-preview8-assets/gpu-memory.png)

Direct2D 的无限吞吐 CPU% 在部分场景反而更高：它单位时间内完成的帧数更多，不能把较短帧时间自动写成较低 CPU%。Vulkan 的完成与回读成本仍存在；本次只是证明这组**离屏完成帧**较快，并没有完成真窗口/多驱动/跨平台的性能普查。

## 画质与功能兼容：预览版仍有硬伤

本轮精确回归通过 **471,569 项**，SSE2 限制与完全标量各 **471,557 项**；实际 Avalonia **9,741**、Direct2D **344**、Vulkan 内核 **540**、GPU 集成 **12,174** 项通过。通用 x64 的真实 Native AOT 另通过 **460,050 项**采样检查。

这些说明已实现功能、优化等价性和受测生命周期通过检查，**不代表原版整页画质通过**：CPU **0/5**、GPU **0/12** 组达到原先规定的最大通道误差 ≤32 且 RMSE ≤2。

{{QUALITY_TABLE}}

优化前后画面一样，但原有差异仍在。隔离调查已发现字体指令处理、字形轮廓和边缘覆盖规则的不同。实验中移除字体指令或回放原版轮廓有助于定位问题，但没有把这些诊断手段塞进生产路径，也没有修改正式测试字体来提高分数。

`verify.ps1 -Reference -Gpu` 仍返回质量失败状态；发布流水线验证构建、已实现功能和真实包消费，不把它的绿勾包装成全部画质全绿。本版是在披露限制后提供的预览更新。

{{FEATURE_TABLE}}

SK* 公共 API 清单仍为 **37 个类型、425/3235 个公共条目匹配**，包含明确拒绝的 API，不等于 425 个功能已完整验收。`SimdSkia.Avalonia` 通过 Avalonia 平台服务接入，不要求实现全部 SkiaSharp API；依赖 `ISkiaSharpApiLeaseFeature`、SK*/GR 原生接口的自绘控件仍需迁移，不承诺二进制替换。

绘图、字体和编解码由 C# 实现，不代表系统窗口和驱动没有原生代码。macOS 保留官方 `libAvaloniaNative.dylib` 窗口桥；Vulkan / Direct2D 调用系统 API。生产 JPEG 使用 MIT 的纯托管 JpegLibrary，Avalonia 适配器使用 BSL-1.0 的托管 Clipper2。Skia/HarfBuzz/ANGLE 只在隔离原版对照程序中使用。[依赖与许可证清单](https://github.com/hnlyf168/SimdSkia/blob/main/docs/DEPENDENCIES.md)。

## 默认 Avalonia AOT 少了哪些文件

这里保留 **2026-10-26 的 preview.7 历史体积实测**，不冒充 preview.8 新数据。两边使用官方空白模板、Avalonia 12.1.4、Fluent 和 Inter、net10.0 / win-x64 / Native AOT。SimdSkia 默认 CPU；原版保留默认后端。这里只比较发布文件，不能用作 CPU/GPU 速度排名。

{{SIZE_TABLE}}

![默认空白项目历史 AOT 体积](2026-10-27-simdskia-preview8-assets/aot-size-preview7.png)

运行文件减少 **46.59%**；减少的是三个原生 DLL，EXE 本身反而增加约 **0.95 MiB**：

{{REMOVED_TABLE}}

Native AOT 把用到的托管代码编译进本机程序，“没有 DLL”不等于引擎代码不占体积。PDB 是调试符号，上表同时列出含符号的原始 publish；运行体积排除符号但没有手动删绘图库或字体。[.NET Native AOT 部署说明](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)。

这些数字只属于该空白 CPU 配置。GPU 适配器、第三方控件、资源和业务逻辑都会改变结果，preview.8 的最终应用体积也应重新发布测量。[历史逐文件证据](https://github.com/hnlyf168/SimdSkia/blob/main/docs/DEFAULT-AVALONIA-AOT-SIZE-2026-10-26.md)。

## 怎么在自己的 Avalonia 项目中使用

Avalonia 系列包统一为 **12.1.4**，所有直接引用的 SimdSkia 包统一为 **0.1.0-preview.8**。

| NuGet 包 | 适用情况 |
| --- | --- |
| `SimdSkia.Avalonia.Desktop` | 普通 Windows/Linux/macOS 桌面应用优先选这个，包含宿主选择和基础适配器 |
| `SimdSkia.Avalonia` | 自行配置平台宿主时使用的绘图、字体和排版服务 |
| `SimdSkia.Avalonia.Gpu` | 需要 GPU 时另加的可选适配器 |
| `SimdSkia` / `SimdSkia.Compatibility` | 使用已实现 SK* 源码外观；普通 Avalonia 应用通常不需要 |

现有默认模板项目移除原版桌面后端，添加新包：

```shell
dotnet remove package Avalonia.Desktop
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.8
```

如果项目直接引用了 `Avalonia.Skia`，也要移除。保留自己的 App、XAML、MVVM、Fluent 主题，将 `.UsePlatformDetect()` 改为 `.UseSimdSkiaDesktop()`：

```csharp
using System;
using Avalonia;
using SimdSkia.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseSimdSkiaDesktop()
            .LogToTrace();
}
```

以上使用项目原有的 `App`。新建项目所需的完整 `.csproj` 和 App/Window 双文件示例在 [GitHub README](https://github.com/hnlyf168/SimdSkia#avalonia-快速开始windows--linux--macos)，包内 README 也提供相同代码。无需下载源码重编译 Avalonia。

使用系统字体时移除 `.WithInterFont()` 和不再需要的 `Avalonia.Fonts.Inter`；也可以像历史体积对照一样保留二者。支持的已安装静态 TTF/TTC 自动发现，中文要求系统实际存在受支持的中文字体；不要假设 Linux/macOS 都有微软雅黑。必要时显式指定：

```csharp
.UseSimdSkiaDesktop(options => options.FontFiles.Add(@"C:\Fonts\MyFont.ttf"))
```

Windows 想用 Direct2D，再加同版本 GPU 包：

```shell
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.8
```

```csharp
.UseSimdSkiaDesktop(options => options.UseGpu(SimdSkiaGpuBackend.Direct2D))
```

Windows/Linux Vulkan 改为 `SimdSkiaGpuBackend.Vulkan`，需要系统 Vulkan loader/驱动；当前窗口路径仍有回读。macOS GPU 未实现。现有不支持的画刷等默认明确报错；如显式允许 CPU 回退，应查看诊断计数，不把混合路径说成全 GPU 渲染。

Windows AOT 发布示例：

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <PublishAot>true</PublishAot>
  <SelfContained>true</SelfContained>
</PropertyGroup>
```

```shell
dotnet publish -c Release -r win-x64 -o publish-win-x64
```

`WinExe` 避免额外控制台窗口；AOT 仍要求业务代码和第三方控件兼容裁剪。需要对应平台的原生编译工具链；Linux/macOS 应分别在对应系统构建。发布后检查依赖树与实际文件，第三方控件可能重新引入 Skia/HarfBuzz/ANGLE。

## 复现与资料

- [本轮完整性能与验收报告](https://github.com/hnlyf168/SimdSkia/blob/main/docs/PARITY-OPTIMIZATION-2026-10-27.md)：构建命令、每进程数据、源码/二进制哈希、P95、CPU 周期、内存及失败项。
- [CPU AOT 示例](https://github.com/hnlyf168/SimdSkia/tree/main/examples/AvaloniaAot)、[GPU 完成帧基准](https://github.com/hnlyf168/SimdSkia/tree/main/benchmarks/AvaloniaGpuParity)、[默认空白模板](https://github.com/hnlyf168/SimdSkia/tree/main/examples/DefaultAvaloniaAot)。
- [源码兼容清单](https://github.com/hnlyf168/SimdSkia/blob/main/docs/COMPATIBILITY.md)、[发行说明](https://github.com/hnlyf168/SimdSkia/blob/main/docs/RELEASE-NOTES.md)、[发布及平台验证](https://github.com/hnlyf168/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)。
- 本文图表附 PNG/SVG 和 JSON 证据，生成器为 `tools/report-preview8-cnblogs.py`；图表工具只参与报告生成，不进入生产依赖或计时路径。

这一版最实在的变化，是默认通用 x64 不再依赖 AVX2 专用发布才能取得较好的控件绘制成绩。它仍需要把文字画质、常驻内存和更广的功能兼容补上；预览包的用途是让真实应用能够验证收益和暴露差距，而不是替代这些尚未完成的工作。
