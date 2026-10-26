# 半透明图片画质修复：2026-10-26

**此前两类失败是「非预乘含透明」和「预乘含透明」图片；已修复，固定门槛下全部 24/24 组通过。** 最大误差从 2 降到 1，RMSE 从约 0.58 降到约 0.05–0.11。门槛仍为最大通道误差≤2、RMSE≤0.5，没有放宽。

[完整数据、CPU 时间/内存、样本、哈希和回归结果](evidence/alpha-compatibility-2026-10-26.json) · [原版样本说明](../tests/Optimization/Fixtures/linear-blend-native-3.119.4.json) · [回归测试](../tests/Optimization/RecordedBlendValidation.cs)

## 原因

不是上一轮 AVX2 采样把颜色算错了，而是图像透明合成没有匹配基线的整数近似。SkiaSharp 3.119.4 对应 mono/skia 提交 **7dbfc07dd33181f84e0958afb7ee805c6c769f0b**。该 x64 低精度图像流水线的透明度缩放和 `srcover_rgba_8888` 背景贡献都使用 `(value*alpha+255)>>8`，我们之前使用的是精确 `/255` 四舍五入。原版近似具有平台/执行路径特征，不能把它推广为所有颜色乘法的通用规则。

例如红色源分量为 0、Alpha=17、背景红色为 10 时，旧规则得到 9，原版得到 10。很多像素都相差 1，加上多阶段舍入偶尔相差 2，整体 RMSE 就超过了门槛；不是大片几何或透明区域丢失。

源码依据：[标签中的 Skia 子模块](https://github.com/mono/SkiaSharp/tree/v3.119.4/externals)、[固定提交的低精度合成阶段](https://github.com/mono/skia/blob/7dbfc07dd33181f84e0958afb7ee805c6c769f0b/src/opts/SkRasterPipeline_opts.h#L5942)、[对应 div255](https://github.com/mono/skia/blob/7dbfc07dd33181f84e0958afb7ee805c6c769f0b/src/opts/SkRasterPipeline_opts.h#L4773)。另外由原版 DLL 实际输出 12,288 个源/目标透明度与全局透明度组合，全部与此规则一致，未只依赖源码推测。

## 修复范围

只校正现代图像采样/拷贝在预乘目标上的 source-over，源 texel 的初始预乘仍使用精确规则。普通色块、渐变、Clear/Source 混合、旧版默认采样路径和高精度 Mitchell 路径未一概改成近似除法。

最近邻、线性过滤、原尺寸图像拷贝与图层回贴必须一致：初版仅改了线性采样，完整回归发现高 DPI 裁剪层不一致，随后将现代最近邻/直拷贝也按独立原版探针统一。保留原断言，没有跳过或增大容差；旧测试中「精确 /255」的图像 oracle 改为实测基线规则，并由独立原版像素样本支撑。

同步 Vulkan 的对应整数合成；同时修复 GPU 在存在 CoverageClip 时没有像 CPU 一样选择现代采样路径的分流问题。提交的 SPIR-V 已重新编译。没有调用原版 Skia 进行生产绘图，也没有新增原生组件。

## 原版画质结果

| 场景 | 尺寸 | 原最大误差 / RMSE | 修复后最大误差 / RMSE | 原门槛结果 |
|---|---|---:|---:|---|
| 非预乘，含透明 | 320×240 | 2 / 0.593298 | 1 / 0.108793 | 通过 |
| 非预乘，含透明 | 1024×768 | 2 / 0.577080 | 1 / 0.058926 | 通过 |
| 预乘，含透明 | 320×240 | 2 / 0.593298 | 1 / 0.108793 | 通过 |
| 预乘，含透明 | 1024×768 | 2 / 0.577811 | 1 / 0.054035 | 通过 |

上述每个场景各 3 个进程；加上两个原先通过的不透明类别，共 24 组全部通过。所有修复后 SIMD 与标量输出仍逐字节一致。修复前后有意发生像素变化，因此没有把「保持旧错误像素」作为本次的通过条件。

## 性能复测

同机 i7-8700K、Windows x64、.NET 8.0.32、SkiaSharp 3.119.4。冻结修复前完整 worker，最终源码重新构建后串行轮换 before/after/original/scalar 各 3 个进程、每场景 7 批；同样的 512×384 源图、清屏和 1024×768 线性过滤。数据与上一轮分开，不用历史时间拼接计算收益。

| 场景 | 修复前 ms | 修复后 ms | 原版 ms |
|---|---:|---:|---:|
| 非预乘不透明 | 7.107 | 6.667 | 7.714 |
| 非预乘含透明 | 7.574 | 6.758 | 7.617 |
| 预乘含透明 | 5.758 | 5.412 | 7.404 |
| Opaque 元数据 | 5.869 | 5.816 | 6.804 |

保留了上一轮 AVX2 批处理，没有出现数量级回退。表格是三进程中位数，样本范围有重叠，部分小尺寸波动较大，不把这次兼容性修复宣传成新的普遍提速。完整 CPU 时间、分配和进程内存在 JSON；原版 microbenchmark 有公共 fixture 开销，内存不作为引擎间排名。本轮没有重测整个 Avalonia 稳态吞吐或 GPU 性能。

## 验证与仍未完成项

- 原版记录 12,288 个独立组合，在两种像素格式、两种采样、原尺寸/向量块及尾部、标量/SIMD 下累计回放 393,216 次；加上已有检查，**468,137 项通过**。关闭 AVX2、关闭全部硬件指令后同样通过。
- 9,734 项真实 Avalonia 控件/生命周期检查通过，包括裁剪层与完整层结果一致；Direct2D 344 项通过。
- Vulkan 590 项采样检查通过，最大误差 1、最高 RMSE 约 0.0093；540 项 GPU 内核及 599 项 GPU 文字链检查通过。没有放宽原 GPU 容差。
- 通用与 x86-64-v3 两个实际 Native AOT 程序各通过 **460,050** 项采样检查，并外部确认没有加载 CLR/JIT。通用版验证回退，v3 版验证 AVX2。
- Release 完整构建无警告/错误。完整 `verify.ps1 -Reference -Gpu` 仍返回 **2**，因为既有字体/路径等原版画质缺口没有由本次修复解决。**不能把这两类图片通过说成完整 Avalonia 画质已通过。**

自动化原版基线是 Windows x64 的固定 DLL；其他原生平台的低精度/高精度实现可能不同，本轮没有声明跨所有平台逐像素等价。修复已随 `0.1.0-preview.7` 公开发布，[发布记录](NUGET-PREVIEW.md)保留全部包的实际公开下载核对和三平台验证。

## 复现

```powershell
dotnet build benchmarks/Sampling -c Release
dotnet benchmarks/Sampling/bin/Release/net8.0/Sampling.dll --alpha-probe --output artifacts/new-native-probe
dotnet run --project tests/Optimization -c Release -- baseline/fonts/ZCOOLXiaoWei-Regular.ttf
powershell -NoProfile -File tools/build-shader.ps1
powershell -NoProfile -File tools/verify.ps1 -Reference -Gpu
python tools/run-core-sampling-comparison.py --output artifacts/new-alpha-comparison --before-directory artifacts/alpha-compat/before-sampling --allow-pixel-changes --require-native-quality
```

`--allow-pixel-changes` 只放开有独立依据的修复前/后像素变化；修复后/标量一致和既定原版画质门槛仍为强制条件。before 必须是修改前冻结的完整 worker，不能复制当前版本冒充旧版本。
