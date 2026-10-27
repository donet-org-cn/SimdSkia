# 用纯 C# 替换 Avalonia 图形后端：SimdSkia 的 AOT 体积、性能与内存实测

我正在开发 SimdSkia，一个使用 C# 实现绘图、字体处理和图片编解码的独立 2D 引擎，并为 Avalonia 提供渲染适配器。它不调用 Google Skia 进行生产绘图。

本文围绕几个实际问题展开：默认 Avalonia 应用换后端后能省多少文件？绘制速度怎样？CPU 和内存有没有一起下降？哪些功能已经能用，哪些还需要原版？

数据来自 **2026 年 10 月 26—27 日**的本机实际 AOT 发布与运行。固定版本为 **Avalonia 12.1.4、SkiaSharp 3.119.4、SimdSkia 0.1.0-preview.7**；不把结果推广到其他 Avalonia/SkiaSharp 版本。

先给出测量结果：

{{SUMMARY}}

## 一、测试如何安排

测试机为 **{{CPU_NAME}}**，{{PROCESSORS}} 个逻辑处理器、{{RAM_GIB}} GiB 物理内存，Windows x64，.NET SDK **{{SDK}}**。这是一台工作站的测量，不是多硬件统计。

我把三个测试分开：

| 测试 | 应用与配置 | 回答的问题 |
| --- | --- | --- |
| 默认应用体积 | 官方空白模板，Fluent + Inter，Release / net10.0 / win-x64 / Native AOT | 用户实际要分发多大、少了哪些文件 |
| 默认应用空闲 | 同一批未插入测量代码的 EXE，真实可见窗口，预热 5 秒、测量 10 秒，各 3 个进程 | 空窗口的 CPU 和进程内存 |
| 控件绘制负载 | 相同中英文 Fluent 控件与列表、相同 TTF、双方均 Win32 软件宿主；3 进程、预热 2 秒、测量 5 秒 | CPU 绘制速度、60 FPS 节拍下的 CPU、分配与内存 |

**默认应用**保留原版 `.UsePlatformDetect()`，原版进程加载了 Skia、HarfBuzz、ANGLE；SimdSkia 默认使用 CPU。**控件绘制负载**双方都使用 CPU，不把一边 GPU 的计时与另一边 CPU 混成速度排名。本文没有重新测 GPU 性能。

控件测试分别发布通用 x64 与 `x86-64-v3` AOT，合计 **90 个正式进程**，按场景、进程轮换执行。v3 需要相应 CPU 指令集支持，不能当成通用 x64 应用分发。通用二进制实际报告 AVX2=false，v3 报告 AVX2=true。

这里的 AVX2 报告值属于 C# AOT 代码；原版的 Skia/HarfBuzz 原生 DLL 在两组中逐字节相同，没有随 `IlcInstructionSet` 重新编译。原生库自己的 CPU 派发也不由这个 C# 开关直接控制，因此不能把“通用组”解释成两边的底层都禁用了 AVX2。

控件基准双方都使用 `OptimizationPreference=Speed`；上面的默认空白应用体积保持 SDK 默认优化偏好。基准还共同携带一份 ZCOOL XiaoWei 中英文字体与测量代码，其 EXE/资源体积没有混进默认应用的体积表。

默认空闲窗口另外测了 6 个进程。正式测量不与编译、绘图工具初始化同时运行；此前受环境初始化影响的预跑完整保留，但不进入下面的统计。表格与图使用 3 进程中位数，误差线表示最小值到最大值，不是置信区间。

控件负载组在预热后，对双方执行相同的完整 GC，再预分配计时缓冲；因此它描述的是该基准的稳态，不是业务应用任意时刻的内存。默认空白窗口的独立采样不强制 GC。控件/列表场景在进程之间轮换顺序，测量的是整进程资源，不单独扣除原版的原生堆。

CPU% 按 `进程 CPU 时间 ÷ 墙钟时间 ÷ 逻辑处理器数 × 100%` 计算，因此占满单个逻辑核大约为 {{ONE_CORE_PERCENT}}%。内存分别记录 **Working Set（驻留工作集）**和 **Private Bytes（私有提交）**，两者不能相加；它们也不包含完整显存统计。

## 二、默认应用 AOT 后到底多大

这里使用官方 `avalonia.app` 空白项目，窗口仅显示 `Welcome to Avalonia!`。本机模板包为 Avalonia.Templates 12.0.0，生成时显式指定 Avalonia 12.1.4 / net10.0；不是复杂控件基准程序。

两边保留同一个 Fluent 主题和 Inter 字体，没有通过删除字体资源缩小 SimdSkia。只把 `Avalonia.Desktop` 换成 `SimdSkia.Avalonia.Desktop`，并替换后端初始化调用。

单位 MiB，1 MiB = 1,048,576 字节：

{{SIZE_TABLE}}

![默认 Avalonia 应用 AOT 发布体积](2026-10-27-simdskia-assets/aot-size.png)

运行文件由 **36.53 MiB → 19.51 MiB**，减少 **17.02 MiB / 46.59%**。同时，EXE 从 18.56 MiB 增加到 19.51 MiB。

Native AOT 将存活的托管代码和资源编译进 EXE，所以不能只看到“没有 DLL”就认为引擎代码不占体积。原版剩下的 3 个 DLL 是原生组件，不能像托管库一样自动并入 EXE。[Native AOT 的部署方式见 .NET 文档](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)。

### 少了哪几个文件

{{REMOVED_TABLE}}

两边各自仍有 `DefaultAvalonia.exe` 与 `DefaultAvalonia.pdb`。PDB 是调试符号，原始发布目录完整保留；运行 ZIP 排除 PDB，但没有手动删 DLL、裁剪字体或修改二进制。Inter 的资源保留在应用内，没有单独复制字体文件。

这套结果针对默认 CPU SimdSkia 配置。加上可选 GPU 适配器、更复杂控件、业务代码和资源后，需要重新测量，不能直接套用 19.51 MiB。

## 三、空白窗口的 CPU 与内存

这部分直接运行上面的默认应用 EXE，外部采集进程时间与内存，不向应用加入定时器或测试代码，也没有测量前强制 GC。

{{IDLE_TABLE}}

![默认 AOT 空闲窗口的进程内存](2026-10-27-simdskia-assets/idle-memory.png)

{{IDLE_DISCUSSION}}

空窗口几乎不持续绘图，低 CPU 数值还受到进程计时粒度和系统调度影响。我不会根据接近零的差值声称几倍提速；它也不能代替有动画、列表滚动的负载测试。

## 四、真实控件页绘制：速度对照

控件负载包含按钮、文本框、边框、透明图层、图片画刷和中英文文字；列表场景持续改变绘制平移量。三种尺寸为 1000×720 控件页、1500×1080 高 DPI 控件页和 1000×720 滚动列表。

下面的 **ms/帧是同步 `RenderTargetBitmap.Render` 的离屏绘制时间**，不是屏幕刷新率。测量排除 PNG 保存、差异图计算与后续绘图工具；同机两个后端使用相同控件、TTF、尺寸和 CPU 宿主。

{{SPEED_TABLE}}

![相同 CPU 场景的绘制耗时，越低越好](2026-10-27-simdskia-assets/cpu-draw.png)

{{SPEED_DISCUSSION}}

### 透明度语义也会影响成绩

原版默认可以使用逐次绘制透明度优化，SimdSkia 的受测路径使用完整透明组。为让差异可检查，我还给原版显式开启相同的完整组语义，并保留第三组结果：

{{OPACITY_TABLE}}

这一组能帮助观察透明度路径成本，但完整页面的像素门槛仍然没有通过，不能因此说三组画质已经相同。

## 五、60 FPS 节拍下的 CPU、分配和内存

无上限吞吐测试通常会吃满一个核，所以只看 CPU% 不容易分辨效率。这里补充 60 FPS 节拍测试：按时等待、不忙等，统计整个进程 CPU 时间；绘制超过预算时记录错过的时限，不把“目标 60 FPS”当作保证达到。

节拍组让出 UI 线程，CPU 时间还包含合成器、定时调度和真实 Win32 软件窗口的工作，不能与上一节的纯离屏绘制 ms/帧互换。

表中的错过时限指基准循环超过计划时间点的次数，包含定时器和调度影响，不等同于显示器实际丢帧数。

{{PACED_TABLE}}

![60 FPS 节拍下的整机口径 CPU 占用](2026-10-27-simdskia-assets/paced-cpu.png)

内存由父进程每 25ms 独立采样。下表使用各进程采样中位数的三进程中位数，分配量为整进程托管分配/帧；原版的原生堆不会被算进托管分配，但会反映在进程 Private Bytes 等指标中。

{{MEMORY_TABLE}}

![60 FPS 控件页的工作集与私有提交](2026-10-27-simdskia-assets/paced-memory.png)

{{MEMORY_DISCUSSION}}

Working Set 会受文件映射、系统回收影响；Private Bytes 是提交量而非物理 RAM。文中的采样值、每进程范围与原始记录一起保留，不用单次任务管理器截图代替测量。

## 六、兼容性：能运行默认窗口不等于完整替换

SimdSkia 对外有两条接入路径：

1. `SimdSkia.Compatibility` 使用 `SkiaSharp` 命名空间和已实现的 SK* 类型，提供子集源码兼容。
2. `SimdSkia.Avalonia` 实现 Avalonia 的绘图、字体与基本排版服务。它通过 `IPlatformRenderInterface`、`IFontManagerImpl`、`ITextShaperImpl` 接入，不需要让 Avalonia.Skia 的全部 SK* 调用都获得实现。

因此，给 Avalonia 选择一个后端，和用同名 DLL 二进制替换 SkiaSharp，是不同的接入工作。

| 兼容维度 | 当前结果 |
| --- | --- |
| SkiaSharp 源码兼容 | 固定 3.119.4；37 个类型、425/3235 个公开 API 元数据条目匹配，2810 项缺失。条目包含类型、枚举、签名等，不代表 425 个完整功能 |
| SkiaSharp 二进制兼容 | 未成立：程序集名称、强名称/公钥等不同，不能直接替换已编译的第三方 SkiaSharp DLL |
| Avalonia 接入 | 固定 12.1.4；通过平台服务适配器接入，保留应用的 XAML/MVVM/控件代码。其他 Avalonia 版本需要单独验证 |
| 行为及生命周期 | 有参数、Dispose、像素、裁剪层和控件回归；不代表所有异常、边界或 native handle 行为相同 |
| 整体画质 | 仍有字体、曲线和整页差距，完整验收没有通过 |

发行前 preview.7 已有 468,137 项 SIMD/标量优化回归，两种实际 AOT 采样各 460,050 项检查，以及三平台窗口验证。它们是**已有发行验证**，不是本文重新运行了全套测试。

此前两类半透明图片的 SourceOver 舍入问题已修复，固定核心图片微基准 24/24 组通过原门槛；这个局部通过不能替代整个 Avalonia 页面通过。

本次控件性能测量同时保留原版像素对照：

{{QUALITY_TABLE}}

{{ISA_QUALITY}}

![同一控件页的原版、SimdSkia 与像素差异](2026-10-27-simdskia-assets/quality.png)

图中的差异按通道绝对值放大 4 倍，仅帮助观察位置；表格误差来自原始 RGBA。完整页面门槛仍为最大通道误差 ≤32、RMSE ≤2，没有为让性能结果“过关”而放宽。不同字体/后端也不必被一概要求逐像素相等，但一旦未达到已定义的门槛，就明确列为缺口。

## 七、功能与平台对照

| 功能 | 原版 Avalonia.Skia / SkiaSharp | SimdSkia preview.7 |
| --- | --- | --- |
| 绘图实现 | Skia 原生引擎及托管绑定 | C# CPU 光栅化、像素合成；不调用 Google Skia |
| 基础路径、填充、描边、AA、裁剪、渐变 | 成熟实现 | 引擎与 Avalonia 适配层已有实现；SK* 外观只覆盖子集，部分组合与画质仍有缺口 |
| 图片采样与透明合成 | 原版实现 | 最近邻、双线性、Mitchell 等受支持路径；AVX2/SSE/标量按硬件选择，部分采样组合有限制 |
| TrueType 与中文 | 原版字体/排版体系 | 纯 C# 静态 glyf TTF/TTC、基础中英文布局、系统字体发现；中文须有受支持字体 |
| 复杂文字、字体高级特性 | 支持范围广 | 完整复杂脚本、Bidi/mark、CFF、可变/彩色字体、hinting/LCD 等仍有缺口，部分请求明确报错 |
| 图片编解码 | 支持范围广 | C# PNG、常见 JPEG 与受支持颜色处理；WebP/GIF、部分 JPEG/ICC 场景等未完成 |
| Windows / Linux / macOS | 官方平台实现 | 三平台 CPU 窗口已有验证；Linux 为 X11/XWayland，非完整原生 Wayland 后端 |
| Windows GPU | 原版图形栈 | 可选 Direct2D/D3D11 原生交换链，或 Vulkan；不在本次 CPU 速度排名中 |
| Linux GPU | 原版图形栈 | 可选 Vulkan 路径，当前有完成后 framebuffer 回读，部署硬件需验证 |
| macOS GPU | 原版后端支持 | 未实现 Metal/macOS GPU；CPU 窗口使用官方 `libAvaloniaNative.dylib` 宿主 |
| 自定义 Skia 控件 | 直接使用其原生 API | 依赖 SK*/GR 或 `ISkiaSharpApiLeaseFeature` 的控件需要迁移，不能保证直接工作 |
| Native AOT | 支持，仍需应用兼容 AOT | 本文两边均已真实 AOT 编译运行；动态反射与第三方控件仍需单独检查 |

“C# 实现”描述的是引擎绘图、字体、编解码代码，并不表示操作系统、GPU 驱动或窗口系统没有原生代码。macOS 官方窗口宿主是明确保留的例外；Windows/Linux GPU 调用系统图形 API，Vulkan loader/驱动不是本库内置的 Skia 绘图依赖。

依赖也需要看清：JPEG 使用 MIT 的纯托管 JpegLibrary；Avalonia 适配器使用 BSL-1.0 的托管 Clipper2，Avalonia 相关组件保留 MIT 通知。项目本身为 MIT，不要求生产绘图加载 Skia/HarfBuzz。

## 八、默认 Avalonia 项目怎么切换

### 1. 固定版本并选择包

本文使用的版本组合是 Avalonia **12.1.4** 与 SimdSkia **0.1.0-preview.7**。先检查现有项目的 Avalonia 版本，不要只加包后忽略版本约束。

| 包名 | 用途 |
| --- | --- |
| `SimdSkia.Avalonia.Desktop` | 普通桌面项目优先使用；提供平台宿主选择和基础渲染适配器 |
| `SimdSkia.Avalonia` | 已经自行选择宿主时使用的绘图/字体/排版适配层 |
| `SimdSkia.Avalonia.Gpu` | 需要可选 GPU 接入时另加，版本与其他 SimdSkia 包保持一致 |
| `SimdSkia` / `SimdSkia.Compatibility` | 使用已实现 SK* 源码外观时选择；普通 Avalonia 项目通常不需要 |

默认模板项目中执行：

```shell
dotnet remove package Avalonia.Desktop
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.7
```

保留 `Avalonia`、`Avalonia.Themes.Fluent`，并将所有直接引用的 Avalonia 包统一为 12.1.4。本文为了相同字体资源对照，**保留** `Avalonia.Fonts.Inter` 与 `.WithInterFont()`；如果选择系统字体，可以一起移除这两项，体积需重新测量。

### 2. 修改 Program.cs

以下以默认模板的 `DefaultAvalonia` 命名空间为例，现有项目替换成自己的命名空间：

```csharp
using System;
using Avalonia;
using SimdSkia.Avalonia;

namespace DefaultAvalonia;

class Program
{
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseSimdSkiaDesktop()
            .WithInterFont()
            .LogToTrace();
}
```

关键是将 `.UsePlatformDetect()` 改为 `.UseSimdSkiaDesktop()`，同时移除原版后端引用。无需修改或重编译 Avalonia 本身。若依赖的第三方包又引入 Skia/HarfBuzz/ANGLE，发布目录还可能保留对应文件，应检查实际依赖树。

系统字体由受支持的已安装字体中选择，也可以显式传入静态 TTF/TTC：

```csharp
.UseSimdSkiaDesktop(options => options.FontFiles.Add(@"C:\Fonts\MyFont.ttf"))
```

选择系统字体时不能假设所有机器都有微软雅黑；Linux/macOS 的安装字体不同，中文字体及许可证需要按部署环境处理。

### 3. 发布 AOT

主程序项目加入：

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <OutputType>WinExe</OutputType>
  <PublishAot>true</PublishAot>
  <SelfContained>true</SelfContained>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
```

```shell
dotnet publish -c Release -r win-x64 -o publish-win-x64
```

`WinExe` 对应 GUI 程序，不会因为默认控制台子系统而额外出现命令行窗口。Avalonia 的 XAML、绑定、反射及第三方控件也需要满足 AOT 要求，不能仅靠一个开关保证整个业务应用兼容。[Avalonia AOT 指南](https://docs.avaloniaui.net/docs/deployment/native-aot)说明了相关部署注意事项。

若明确要求 x86-64-v3 CPU，可为两边另外发布并分别测试：

```shell
dotnet publish -c Release -r win-x64 -p:IlcInstructionSet=x86-64-v3 -o publish-win-x64-v3
```

这会提高最低硬件要求；本文没有把 v3 的速度标在默认通用发布包上。

### 4. 可选 GPU 不等于默认 CPU 测试

例如 Windows Direct2D 需要另加同版本 GPU 适配器：

```shell
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.7
```

```csharp
.UseSimdSkiaDesktop(options =>
    options.UseGpu(SimdSkiaGpuBackend.Direct2D))
```

Direct2D 默认使用原生交换链；Vulkan 当前的窗口路径仍有等待完成及回读成本。GPU 有独立资源、上下文及平台限制，不能把本文 CPU 离屏图直接当作 GPU 性能报告。

## 九、如何复现控件负载

配套源码的基准入口是 `examples/AvaloniaAot`，默认空白应用在 `examples/DefaultAvaloniaAot`。从仓库根目录先获取固定 OFL 测试字体，再发布。下面先测通用 AOT，选择全新的输出目录：

```powershell
powershell -NoProfile -File tools/fetch-avalonia-font.ps1
dotnet publish examples/AvaloniaAot/Original/AvaloniaAot.Original.csproj -c Release -p:ComparisonIsa=portable -o artifacts/blog-rerun/publish/portable/original
dotnet publish examples/AvaloniaAot/SimdSkia/AvaloniaAot.SimdSkia.csproj -c Release -p:UsePackedSimdSkia=true -p:ComparisonIsa=portable -o artifacts/blog-rerun/publish/portable/simd
python tools/run-avalonia-aot.py --no-build --publish artifacts/blog-rerun/publish/portable --output artifacts/blog-rerun/portable-measured --isa portable --forks 3 --warmup 2 --seconds 5
```

v3 使用 `-p:ComparisonIsa=v3`，发布到另外的 `publish/v3/{original,simd}`；运行时将 `--isa` 改成 `v3`，同时改用单独的发布和结果目录。先构建完两个程序，再串行采样，不在测量期间进行编译或绘图。

该工具返回 **2** 表示测量完成但原版画质门槛失败；构建、运行或测量失败返回 **1**。本次两组均为 2，完整失败记录已保留，不能将它们改写为全通过。

## 十、这次测试给我的判断

{{CONCLUSION}}

当前更适合按固定版本和具体页面逐项验证：先看字体/图片/控件是否达到自己的要求，再比较实际发布物、目标帧率 CPU 和进程内存。对于依赖高级 Skia API、复杂文字、图形滤镜或 macOS GPU 的应用，需要先解决功能差距。

项目与安装入口：[SimdSkia GitHub](https://github.com/hnlyf168/SimdSkia)、[NuGet 桌面包 preview.7](https://www.nuget.org/packages/SimdSkia.Avalonia.Desktop/0.1.0-preview.7)。基线依赖见 [Avalonia.Desktop 12.1.4](https://www.nuget.org/packages/Avalonia.Desktop/12.1.4) 和 [Avalonia.Skia 12.1.4](https://www.nuget.org/packages/Avalonia.Skia/12.1.4)。

本篇图表由实际结果生成，原始记录在配套 `2026-10-27-simdskia-assets/data/`，包含逐进程结果、汇总、画质门槛、二进制和源码哈希。失败项、测量范围和资源清单一并保留，便于复核。
