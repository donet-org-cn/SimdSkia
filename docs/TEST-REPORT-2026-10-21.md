# 测试报告：2026-10-21 本轮交付

**功能回归通过，完整兼容性验收未通过。不能把当前版本直接当作完整 Avalonia.Skia 替代品上线。** 固定基线 Avalonia 12.1.4 / SkiaSharp 3.119.4；内部命名空间 SimdSkia，兼容层保留 SkiaSharp.SK*，程序集没有冒充原版强名称。

本机：Windows 10.0.26300，.NET 8.0.32 x64，i7-8700K（12 逻辑 CPU），GTX 1050 Ti。固定简体中文字体 ZCOOL XiaoWei SHA-256：`A42B620140F493DB42F741351DFBF343C0936D58588EE8004B8B2A218D997FF1`。历史 SK 文字链另用固定 Zen Old Mincho 字体，未混淆两组图片/性能数据。

## 实际运行结果

| 检查 | 结果 | 验证范围 |
|---|---|---|
| Release 解决方案构建 | PASS，0 warning / 0 error | 新增 Imaging、Filters benchmark、GpuSampling 项目均参加构建 |
| 基础行为 | 36/36 PASS | 位图→画布→清屏→矩形→读像素，参数、生命周期、格式、变换与分配 |
| 功能示例回归 | 13/13 PASS；隔离原版 PNG 对照启用后 14/14 | 路径/填充规则/AA/描边/渐变/图片/CJK/PNG/JPEG 与资源持有 |
| CPU 精确优化检查 | 7845 PASS，硬件及关闭全部 intrinsics 均通过 | 与冻结标量实现比较；stride、Alpha、格式、缓存、变异、PNG/DEFLATE 校验 |
| 新图片/颜色/压缩检查 | 484 PASS，两种硬件模式均通过 | 41 个独立输入夹具（40 个可解码、1 个明确拒绝），PNG 五种行过滤、Adam7/低位深/16 位、JPEG、sRGB/P3 ICC、损坏数据、压缩级别、原版 JPEG Alpha |
| 独立编码流验证 | 33/33 PASS | 单独 Python/zlib 进程解码 C# LZ77/fixed/stored 输出；引擎与正常 C# 测试不调用 native zlib |
| 原版 JPEG Alpha 固定夹具 | 24/24 PASS，阈值每通道 1 | Opaque/Premul/Unpremul × Alpha 0/32/128/255 × Erase/直接写存储；包含在上述 484 断言内 |
| 既有定位文字链 | 595 PASS，硬件及全标量均通过 | glyph/cluster/advance、共享缓存、线程与生命周期 |
| 实际 Avalonia 平台/控件 | 8109 PASS，硬件及全标量均通过 | Fluent 控件、输入、DPI/resize、字形 mask、画刷、效果、卷积独立预期、ROI 层和 TTC |
| Vulkan 新采样/裁剪 | 445 PASS | 实际设备 + fence/readback；CPU 数值对照最大误差 2，最大 RMSE 0.259；最近邻/双线性/Mitchell、裁切、覆盖、格式/Alpha/stride/曲线 |
| Vulkan 内核优化 | 313 PASS | 309 次逐字节比较、3 个 bin 预算检查、1 个未支持 glyph-run 的明确拒绝 |
| Vulkan 实际功能/资源 | PASS | 批量边界、纹理版本、源提前释放、缓存淘汰、中文轮廓、创建线程与 Dispose |
| 旧文字链 Vulkan 对照 | 599 PASS | 595 项基础 + 4 项 CPU/GPU 对照 |
| 实际 Win32 软件呈现 | PASS | 启动真实窗口、完成 framebuffer 呈现、自动退出，未加载原版绘制/字体组件 |
| 生产依赖隔离 | PASS | 四模块零包；Imaging/Compatibility 仅固定托管 JPEG；适配层六包无 native assets，示例无 Skia/HarfBuzz/ANGLE |

这些计数有重叠，不相加作为独立测试总数。`8109` 中的卷积测试同时覆盖标量/AVX2、两种格式/三种 Alpha、各种宽度和尾数；关闭 intrinsics 后仍调用相同测试，确认没有 CPU 指令集要求。ROI 层与未裁剪全尺寸图再恢复 clip 的结果逐字节一致；没有裁去 layer bounds 提示外的内容。

41 个输入夹具由独立 Python PNG 组装/zlib、Pillow libjpeg/lcms 生成并固定存放在 [fixtures.json](../tests/Imaging/fixtures.json)。30 个 PNG 与 10 个 JPEG 输入通过原先定义的像素门槛；四分量渐进 CMYK 另 1 个必须明确拒绝，不算成支持的解码格式。RGB JPEG 的最大误差实测 2，Display P3→sRGB 实测最大误差 1、RMSE 约 0.108。ICC 的所有形态、YCCK 等并未因此全部验收，范围见 [实现说明](IMPLEMENTATION-DETAILS-2026-10-21.md)。

## 失败的兼容性门槛

**API：425/3235 项匹配，2810 缺失，0 项多余或签名不同；公开类型 37 个。** [逐项清单](api/comparison.json) 包括成员、枚举、重载、属性、默认参数等。原 Avalonia.Skia 的 SK 调用面仍约 120/346 个成员存在；独立 `UseSimdSkia` 适配器运行受测控件，不代表这 2810 项已补完。原版程序集强名称、公钥和名称均不同，二进制替换未通过且未承诺。

既有原版 CPU 渲染对照：**108/120 图像 PASS，12 FAIL**；失败为中文和 path-AA 的六种布局组合，最大通道误差约 61–66，原定阈值 32 未改。25 个既有参数/行为探针无差异。完整命令 `tools/verify.ps1 -Reference -Gpu` 继续执行后续 GPU 检查，最终明确返回 **2**。

完整控件 100%、150% 与列表滚动三场景仍为 **0/3 PASS**，门槛最大误差 32、RMSE 2 未改；本轮优化前后这三帧仍逐字节一致，因此性能优化也没有解决此前的文字/曲线差异。最终数字、图片和源指纹见 [性能报告](PERFORMANCE-FINAL-2026-10-21.md)。

新增 114 个原版同场景功能对照：

| 功能 | 通过 | 最坏最大误差 | 最坏单场景 RMSE | 固定门槛 max / RMSE |
|---|---:|---:|---:|---:|
| ImageBrush | 15/40 | 255 | 15.318 | 2 / 0.5 |
| VisualBrush | 5/10 | 80 | 6.946 | 2 / 0.5 |
| Blur | 7/8 | 9 | 0.991 | 8 / 1.5 |
| DropShadow | 8/8 | 6 | 0.617 | 8 / 1.5 |
| BoxShadow | 24/48 | 56 | 1.310 | 32 / 2 |

合计 **59/114 PASS**，55 个失败保留。VisualBrush 的通过项是 100% DPI，150% 仍失败；BoxShadow 通过非圆角矩形，圆角 24 个失败。最大误差与最坏 RMSE 是分别取极值，不一定来自同一张图。[全部逐场景统计](feature-quality-results.json)；本地原图及差异图在 `artifacts/completion/features-final` 与 `artifacts/completion/feature-diff`。阈值在首个原版对照之前已定义，未为提速调整。

原版 Vulkan 同机 24 个工作负载的画质仅 **12/24 PASS**，矩形 AA、图片、曲线和中英文仍有失败；[GPU 报告](PERFORMANCE-GPU-FINAL-2026-10-21.md) 保存所有失败行，返回 2。445 项 CPU/GPU 数值检查通过不等于与原版 GPU 的全部行为相同。

## 复现与原始证据

```powershell
dotnet build SimdSkia.sln -c Release
powershell -NoProfile -File tools/fetch-test-font.ps1
powershell -NoProfile -File tools/fetch-avalonia-font.ps1
powershell -NoProfile -File tools/verify.ps1 -Gpu
# 以下开启原版对照；当前预期返回 2，表示已记录的画质失败
powershell -NoProfile -File tools/verify.ps1 -Reference -Gpu
python tools/verify-managed-encoding.py
dotnet run --project examples/AvaloniaControls -c Release -- --smoke-window

# 114 个真实画刷/效果示例（PNG + RGBA），原版进程单独执行
dotnet run --project tests/AvaloniaControls -c Release -- --feature-probe --output artifacts/my-features/managed
dotnet run --project tests/AvaloniaControls.Reference -c Release -- --feature-probe --output artifacts/my-features/native
python tools/report-feature-probes.py artifacts/my-features/managed artifacts/my-features/native --output artifacts/my-features/results.json

# 完整标量回退
$env:DOTNET_EnableHWIntrinsic='0'
dotnet run --project tests/Optimization -c Release -- baseline/fonts/ZenOldMincho-Regular.ttf
dotnet run --project tests/Imaging -c Release
dotnet run --project tests/AvaloniaControls -c Release -- --frames 5 --output artifacts/my-scalar
Remove-Item Env:DOTNET_EnableHWIntrinsic
```

原版对照需先恢复 `baseline`，参见 `tools/fetch-baseline.ps1`。引擎不需要这些原版 DLL；只有隔离 Reference 项目使用它们。再生成图片输入夹具才需要 Pillow；运行已固定夹具和普通产品构建不需要 Python/Pillow。

完整本机日志：`artifacts/completion/delivery-validation.log`；四组标量日志以 `scalar-*-final.log` 命名。[Vulkan 采样统计](gpu-sampling-test-results.json)、[旧 CPU 原版像素统计](compatibility-pixel-results.json)、版本清单和基准配置随报告保存；工作区 artifacts 含完整 PNG/RGBA、ticks 与进程日志。

Windows/Linux/macOS 的托管 CI 状态以本轮 GitHub Actions 链接为准，GPU 实测只覆盖本机 Windows/NVIDIA。未在 Linux GPU、Linux/macOS 桌面宿主或用户真实完整 Avalonia 应用上验收，不把构建通过扩大为这些环境已完成。

## 尚未完成

首要阻断项是上面的画质失败、缺失的 SK API、Vulkan 窗口/swapchain 与 GPU 字形/画刷/效果集成；此外还有更多混合模式、CFF/变量/彩色字体、hint/LCD、RTL/mark/复杂 OpenType、其他图片格式及完整颜色 profile 生命周期。详情逐项维护在 [进度清单](COMPLETION-TRACKER.md)。所有这些项目仍属于完整替换的剩余工作，不以通过的子集宣称全部完成。
