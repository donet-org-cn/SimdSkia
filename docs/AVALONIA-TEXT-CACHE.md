# Avalonia 文字、硬裁剪与首绘优化：2026-10-21

本轮继续优化真实 Avalonia CPU 后端：按字形复用覆盖率、按设备亚像素定位、补上 sRGB 文字 mask 预混合，并修复半像素硬裁剪。控件源码、字体、尺寸、采样和透明组语义保持相同；修正文字边缘和裁剪会改变像素，不能声称优化前后逐字节一致。16×16 AA 和曲线容差没有降低。

## 同机 CPU 与内存

i7-8700K / Windows 10.0.26300 / .NET 8.0.32；固定 Avalonia 12.1.4、SkiaSharp 3.119.4 和相同 SHA-256 的 ZCOOLXiaoWei 字体。100% 为 1000×720，150% 为 1500×1080。4 个实现配置 × 3 个场景 × 3 个独立进程，共 36 个进程，交替顺序，每进程预热 2.5 秒后计时 600 次完整离屏重绘。下表是三个进程的中位数；帧时间 / 进程 CPU 时间单位为 ms。

| 场景 | 优化前 | SimdSkia 本轮 | 原版默认 | 原版完整组 |
|---|---:|---:|---:|---:|
| 控件 100% | 1.492 / 1.510 | 1.083 / 1.094 | 0.855 / 0.859 | 3.181 / 3.177 |
| 控件 150% | 2.783 / 2.812 | 2.393 / 2.396 | 1.607 / 1.589 | 6.829 / 6.797 |
| 24 行列表滚动 | 0.723 / 0.833 | 0.433 / 0.521 | 0.370 / 0.391 | 0.368 / 0.365 |

原版默认采用逐 draw 透明度；完整组对透明 Border 开启 `RequiresFullOpacityHandling`，按组处理重叠，见[前轮语义核验](AVALONIA-CONTROLS-PERFORMANCE.md)。SimdSkia 始终保证透明分组语义。列表没有半透明组，原版两列在该场景语义相同。原版默认仍比本轮快，不能只选择较慢的原版完整组来宣布整体胜出。

| 场景 | 配置 | 分配 KiB/帧 | 工作集 / Private MiB | GC 后托管保留 MiB |
|---|---|---:|---:|---:|
| 控件 100% | 优化前 | 20.89 | 118.9 / 79.4 | 20.9 |
| 控件 100% | SimdSkia 本轮 | 16.43 | 118.4 / 78.7 | 20.0 |
| 控件 100% | 原版默认 | 7.77 | 96.0 / 50.8 | 2.3 |
| 控件 100% | 原版完整组 | 7.94 | 95.7 / 51.0 | 2.3 |
| 控件 150% | 优化前 | 22.75 | 129.0 / 90.1 | 31.4 |
| 控件 150% | SimdSkia 本轮 | 18.29 | 129.0 / 89.2 | 30.6 |
| 控件 150% | 原版默认 | 7.77 | 99.9 / 54.9 | 2.3 |
| 控件 150% | 原版完整组 | 7.94 | 98.2 / 53.3 | 2.3 |
| 24 行列表滚动 | 优化前 | 10.09 | 106.8 / 74.0 | 16.5 |
| 24 行列表滚动 | SimdSkia 本轮 | 4.46 | 102.5 / 69.5 | 15.9 |
| 24 行列表滚动 | 原版默认 | 8.03 | 86.5 / 47.9 | 1.4 |
| 24 行列表滚动 | 原版完整组 | 8.03 | 86.3 / 47.7 | 1.4 |

CPU 是整个进程 TotalProcessorTime 差值，包含后台 JIT/GC，系统计时粒度也会影响小样本。工作集、Private、托管保留是不同口径，不能相加；工作集为计时结束快照，原始 JSON 另存测量前和峰值。这是 CPU 离屏完整重绘，不含 GPU、窗口 present 或真实应用启动。

| 场景 | 优化前首绘 ms | 本轮首绘 ms | 原版默认 ms | 原版完整组 ms |
|---|---:|---:|---:|---:|
| 控件 100% | 89.21 | 18.56 | 6.04 | 8.54 |
| 控件 150% | 114.77 | 65.95 | 17.33 | 23.05 |
| 24 行列表滚动 | 59.77 | 9.17 | 5.09 | 5.62 |

首绘仅测首次 Render，包含该次 JIT 和覆盖率生成，不含此前字体注册、框架启动、主题和布局。每进程只有一次首绘，完整范围见原始数据，不能解读成应用打开时间。

| 场景 | 优化前 P95 / P99 ms | 本轮 P95 / P99 ms | 原版默认 P95 / P99 ms | 原版完整组 P95 / P99 ms |
|---|---:|---:|---:|---:|
| 控件 100% | 1.628 / 1.967 | 1.173 / 1.514 | 0.923 / 1.207 | 3.393 / 4.037 |
| 控件 150% | 3.028 / 3.744 | 2.626 / 3.098 | 1.724 / 2.366 | 7.437 / 8.491 |
| 24 行列表滚动 | 0.797 / 0.967 | 0.456 / 0.598 | 0.426 / 0.588 | 0.391 / 0.509 |

全部结果、GC、帧时间范围与 SHA 清单见[本轮原始数据](avalonia-text-cache-results.json)。本地证据在 `artifacts/avalonia-controls/validated-text-cache-final-2026-10-21`；保留了本轮中间测量目录，未覆盖前轮报告。

## 画质仍未验收通过

| 场景 | 前轮 RMSE | 本轮 RMSE | 前轮最大误差 → 本轮 | 结果 |
|---|---:|---:|---:|---|
| 控件 100% | 5.079 | 2.408 | 99 → 96 | FAIL |
| 控件 150% | 4.681 | 2.321 | 216 → 117 | FAIL |
| 24 行列表滚动 | 6.419 | 2.595 | 110 → 75 | FAIL |

RMSE 下降且高 DPI 丢笔画已修复，但三个配置仍全部未达最大误差 ≤32、RMSE ≤2 的既定门槛。它们不构成与原版同画质的胜出结论；这是明确报告残留差异的工程诊断。首绘、Private 和工作集也仍高于原版。

![本轮真实 Avalonia 控件](images/avalonia-text-cache-managed.png)


## 实际实现

- `Core.GlyphRunGeometry` 保存不可变轮廓和已排版位置，绘制命令与 Avalonia/CPU 后端分离。适配层不再为每次文字 draw 包装一份整行几何；下划线交点查询才延迟构造整行路径。
- CPU 使用原始字形 mask LRU，最多 4096 项/8 MiB；已定位、预混合的行缓存最多 512 项/2 MiB。预算包含保守估算的引用轮廓数据；不是进程总内存上限，活动批次和并发未命中的临时数据另计。整数滚动复用覆盖率。更换字体轮廓、矩阵、相位、AA、曲线容差、baseline snap 或预混合 luminance 会改变相应 key；剪裁、目标像素与颜色 Alpha 不污染缓存。
- 将连续且覆盖像素互不重叠的字形打包为一张 mask。出现重叠时另起一批，保留输入顺序；半透明重叠字形仍执行两次合成，避免整行 union 改变结果。热帧复用现有 AVX2 可变覆盖率合成，标量算法仍可独立运行。
- 灰度文字的设备位置采用 1/4 像素相位。关闭 baseline snap 时 X/Y 都保留相位；开启时在水平/垂直基线的相应方向整数对齐。Alias 使用整数位置。显式 TextRenderingMode 优先于形状 EdgeMode，未指定才继承 EdgeMode。
- 文字 mask 预混合由标准 sRGB 转换和 luminance/contrast 公式生成，使用色彩规范化后的 8 组查找表；没有从对照图拟合覆盖率。它只用于适配层文字策略，不修改路径 AA、PNG gamma 或目标颜色空间，也不是完整的线性光合成/ICC 实现。
- 轴对齐的 Avalonia 矩形 clip 先变换到设备坐标，再以 `floor(edge+0.5)` 舍入两端。此前分数 clip 被当作 AA 面积或 pixel-center 边界，150% DPI 时会整行裁掉最后的文字笔画。22 份原版探针固定了正负坐标与半像素 ties 的行为；旋转 clip 和圆角/几何 AA clip 仍走几何覆盖率。

这条新字形命令目前仅 CPU 支持；Vulkan 明确拒绝，不将 CPU mask 上传后当作 GPU 字形实现。单字形 raster mask 限 4 MiB，设备字形坐标限 ±65536，超限在写入任何字形前明确报错。每个画布/像素缓冲仍按单线程约定使用，不可变字形数据和全局缓存支持跨画布并发。

核对的是 SkiaSharp `v3.119.4` 固定子模块 [7dbfc07…](https://github.com/mono/SkiaSharp/tree/v3.119.4/externals)：其 [baseline snap](https://github.com/mono/skia/blob/7dbfc07dd33181f84e0958afb7ee805c6c769f0b/src/core/SkScalerContext.cpp)、[默认 sRGB gamma](https://github.com/mono/skia/blob/7dbfc07dd33181f84e0958afb7ee805c6c769f0b/include/core/SkTypes.h) 和 [文字预混合公式](https://github.com/mono/skia/blob/7dbfc07dd33181f84e0958afb7ee805c6c769f0b/src/core/SkMaskGamma.cpp) 用于行为调查，产品没有引入这些 C++ 文件或调用它们。不能只根据其他 Skia 分支推断当前基线；尤其关闭 baseline snap 后不能把 Y 固定到整数。

## 复现

```powershell
dotnet build SimdSkia.sln -c Release
powershell -NoProfile -File tools/fetch-avalonia-font.ps1
dotnet run --project tests/AvaloniaControls -c Release --no-build -- --frames 5
$env:DOTNET_EnableHWIntrinsic='0'
dotnet run --project tests/AvaloniaControls -c Release --no-build -- --frames 5 --output artifacts/avalonia-controls/text-scalar
Remove-Item Env:DOTNET_EnableHWIntrinsic
powershell -NoProfile -File tools/run-avalonia-controls.ps1 -Output artifacts/avalonia-controls/my-text-run
```

`run-avalonia-controls.ps1` 保存全部 CPU/内存数据和差异图，画质未过门槛时返回 2。如需重跑优化前四方比较，先从 `2fc157b` 构建并保存整个测试输出目录，再使用 `-BeforeDirectory` 指向该目录。不要把同目录的 DLL 覆盖后当作不同版本运行。

独立文字诊断绕过控件布局，两进程编译同一 `TextProbe.cs`：

```powershell
dotnet tests/AvaloniaControls/bin/Release/net8.0/SimdSkia.Avalonia.Controls.Tests.dll --text-probe --output artifacts/text-mask/managed
dotnet tests/AvaloniaControls.Reference/bin/Release/net8.0/AvaloniaControls.Reference.dll --text-probe --output artifacts/text-mask/native
python tools/report-text-masks.py artifacts/text-mask/managed artifacts/text-mask/native --output artifacts/text-mask/results.json
# --clip-probe 另存 22 份硬裁剪探针
```

24 个文字配置涵盖 12/18/27 em、100/150% scale、四个 X/Y 相位及黑色/蓝色前景。整体 RMSE 从 8.062 降到 3.810，超过 32 的通道数从 71193 降到 11149；最大值 110→111，并非每个像素都改善。该独立诊断仍 FAIL，见 [原始统计](avalonia-text-mask-results.json)。原版与本实现的栅格边缘仍有差异，不用不同字体掩盖它。

## 验收与剩余差距

新增检查涵盖字形对顺序路径的逐字节比较、两种格式/三种 Alpha、三种混合、stride、旋转/镜像、AA/alias/基线、剪裁/渐变/透明度变化、缓存淘汰和预算、整数滚动、并发、失败不产生部分输出、sRGB 正向混合方程，以及固定原版硬裁剪夹具。完整控件测试保留鼠标、中文输入、DPI、resize、生命周期与原版排版断言。

画质既定门槛仍是最大通道误差 ≤32、RMSE ≤2，未提高。字体 hinting/LCD、复杂排版、更多字体/编解码、ImageBrush/VisualBrush、滤镜及 Vulkan 窗口等 [接入缺口](AVALONIA-INTEGRATION.md) 仍存在。本轮没有扩大 SK* 签名数量，也没有二进制兼容或 GPU 提速承诺。
