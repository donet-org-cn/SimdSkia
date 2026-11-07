# SimdSkia 0.1.3：桌面 GPU 优先、文字缓存与内存优化

十二个包统一为 **0.1.3 正式版**，目标 .NET 10。发布与公开下载验证结果见[发布记录](NUGET-PREVIEW.md)。本版汇集0.1.2之后的三个优化批次，原始测试快照保持原版本和测量口径。

## 升级与默认行为

Avalonia仍支持 **>=12.1.0 且 <13.0.0**，本次发布回归覆盖12.1.0–12.1.5。固定SkiaSharp3.119.4源码基线，3230/3235元数据项、五项真实原生合同缺口保持；不承诺二进制替换或完整行为兼容。

```sh
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.3
```

```csharp
using SimdSkia.Avalonia;
// Windows: Direct2D原生交换链优先；Linux: 硬件Vulkan优先；诊断后回退CPU。
AppBuilder.Configure<App>().UseSimdSkiaDesktop()
    .StartWithClassicDesktopLifetime(args);
// 保留0.1.2的CPU选择：
// .UseSimdSkiaDesktop(options => options.UseCpu())
```

macOS窗口仍为C# CPU绘图，使用已允许的官方MIT窗口桥接库；Metal核心尚无Mac GPU实机验证。Linux Vulkan窗口仍包含同步读回。GPU不是通用省CPU或省内存方案；显式严格GPU选项仍可能抛出初始化/不支持功能异常。

## 从0.1.2更新了什么

- 桌面GPU优先、初始化诊断和CPU回退；区分设备丢失与窗口表面失效，避免调整尺寸后错误禁用硬件。
- Direct2D完成事件替代忙轮询、最多两帧在途、flip-discard双缓冲及常驻纹理；保留局部帧，修复跨帧第一条CPU回退读取陈旧像素的问题；重复绘制状态缓存。
- 相同内容的文字对象复用覆盖率，构造时计算内容哈希并核对完整内容；子像素相位共享轮廓计费，默认字节预算保持。设备文字遮罩改为逐项LRU和共享不可变数组，最多512项/4MiB估算预算。
- Vulkan小面积纯色重叠矩形按像素保留绘制顺序合批；AA/渐变等命令保留原路径。八条管线初始化准备，小边数据缓存避免重复构建，大条目保持64项及16MiB预算。
- Direct2D可选低CPU离屏事件等待，默认关闭该取舍；基准与AOT来源检查、测试、性能图和CPU/内存原始证据补充。

## 实测改善与限制

Windows GTX1050Ti、3fork、JIT Vulkan绘制+fence+读回，自身修改前→后：

| 场景 | 前ms | 后ms | 备注 |
|---|---:|---:|---|
| 动态中文256×256 | 2.3583 | 0.1554 | 原版画质失败，仅性能诊断 |
| 动态中文1024×768 | 3.9985 | 0.7707 | 原版画质失败，仅性能诊断 |
| 混合256×256 | 0.4462 | 0.3237 | 与原版像素相同，仍慢约39% |
| 混合1024×768 | 1.9787 | 1.6242 | 与原版像素相同 |

动态中文分配降至约168–169B/帧，CPU时间下降约92%–96%；这些微场景工作集下降约2–8MiB。真实Avalonia整页完成读回比值Vulkan约0.709、Direct2D约0.592，但严格画质仍0/12，不能当作同画质胜出。Native AOT滚动0.7430→0.7289ms；未修改0.1.2也未重现旧7.528倍异常，根因未知，不能归功于本版修复。CPU AOT滚动工作集仍约68MiB，原版约56MiB。

JPEG、文字/曲线/整页画质、部分GPU性能和原生窗口总CPU/内存仍有差距；**整体持平且最多落后10%的目标未全部达到**。GPU资源仍要求创建线程显式释放。依赖保持Sdcb.SimdCodecs1.4.0、Clipper2 2.0.0、可选PdfPig0.1.16。

[热点测试与性能图](PERFORMANCE-HOTSPOTS-2026-11-07.md) · [Direct2D完成与CPU/内存](PERFORMANCE-GPU-COMPLETION-2026-11-07.md) · [GPU优先行为](GPU-PREFERRED-2026-11-07.md) · [完整更新日志](RELEASE-NOTES.md)。本机完整构建、36项核心、474413项CPU优化、13853项Avalonia、599项GPU、12174项GPU集成及131项策略/窗口检查通过；本版本包和三平台回归以发布流水线结果为准。
