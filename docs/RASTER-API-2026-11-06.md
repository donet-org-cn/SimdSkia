# 光栅 API 继续补全与热点优化（未发布）

2026-11-06，Windows x64。固定 SkiaSharp 3.119.4 / 3235 项基线，当前 **2384 匹配、146 类型、851 缺口、0 额外签名**。比上一冻结快照增加 67 项元数据；仍未达到最多 99 项，任务继续。公开 0.1.1 不变，没有发布新包。元数据数目包含重载、字段和类型，不等于完整功能数量，也不证明二进制兼容。

## 真实实现

- CPU 补齐另外 26 种 Porter–Duff/可分离/不可分离混合，既有三个模式的优化路径继续保留。SKBlender 的模式与算术公式参与实际组合着色器求值。
- SKBitmap/SKImage.ToShader、CreateBitmap/CreateImage 支持 Clamp/Repeat/Mirror/Decal、独立 X/Y 平铺、最近邻/线性/Mitchell/Catmull–Rom 采样及局部矩阵；在预乘空间插值。Mipmaps、各向异性仍显式拒绝。
- 颜色矩阵、HSLA、Lighting、模式混合、Luma、ARGB 查表、伽马、Compose/Lerp、高对比度支持着色器、画笔和图片链路。高对比度使用直通线性光 RGB 工作域。
- 位图着色器按可变性创建快照/保留视图；派生着色器、画笔和 Clone 有独立租约，最后释放才触发像素回调。仅资源租约有终结器，普通画笔/纯色着色器没有新增终结器；每次绘图不做引用计数。
- 默认解码匹配原版 N32/预乘合同，转换复用解码存储，不分配第二张完整图片。底层 ImageCodec 仍返回直通 RGBA。初始 image-check-3 的较大误差来自工厂合同，不能全部归为 1 级舍入；保留旧失败，修正后相同字节输入仍有 32 组最大 1 级严格差异。
- 点绘制与常用 Canvas 重载已经运行；细线/描边和默认字体 hinting 仍存在行为差异。

## 测试结果

| 对照或回归 | 结果 |
| --- | --- |
| 颜色滤镜三条绘图链路 | **2448/2448**，最大通道差 ≤1，工厂空值行为一致 |
| 29 种矩形混合 + SetPixel | **2928/2976**；新增 26 模式的 2496 幅和 192 组 SetPixel 通过；48 失败属于原有三个模式 |
| 图像/组合/算术着色器 | **746/940**；新增 Src 192 幅全部通过。194 失败为 AA 184、双线性 1、组合 7、算术 2；阈值保持 max≤2/RMSE≤0.5 |
| 点与线 | **144/180**；36 细线图失败，另默认字体测量原版 80 / 本库 80.874，抗锯齿初始标志已修正 |
| 图片工厂 | 181 组、各 230 生命周期检查，仍 32 组最大 1 字节差异，严格比较退出 2 |
| 精确优化 | **474413**，包括 2328 组 Multiply SIMD/标量、格式、非法 PMA 物理字节、完整向量和尾部；全部 intrinsics 禁用也通过 |
| 实际 Native AOT | **48**，执行验证动态代码不可用；外部像素/着色器/画笔释放回调只执行一次 |
| 基础 / 像素 / 编解码 / PDF / 字体发现 | 36 / 1358 / 1417 / 18 / 43 通过 |
| Avalonia 文字 / 控件 | 595 / 9750 通过 |
| Direct2D / Vulkan 采样 | 148114 / 617 通过；含新增模式/滤镜拒绝前不修改像素及真实完成等待 |
| 独立图片 | 12410 断言通过后，JPEG 编码严格门槛仍失败 |

完整 Release 构建 0 警告、0 错误。旧 Features PNG 测试错误地要求低 Alpha 直通颜色在默认预乘解码后精确恢复，现严格核对预乘物理字节，并保留旧日志。新增检查没有把失败改为跳过或放宽阈值。既有字体 186 字段、区域 8 字段差异继续保留，整页画质 **0/18 尚未重测**。GPU 对新增未支持的混合/滤镜/图像着色器显式拒绝，由调用端选择 CPU 回退；GPU 回归通过不代表这些功能已 GPU 加速。

## CPU 速度、CPU 时间与内存

同机 i7-8700K（6 核/12 线程）、Windows 26300、.NET SDK 10.0.403/runtime 10.0.14，CPU bitmap JIT。每报告 6 个串行进程、3 轮轮换，预热 0.4 s、计时 0.8 s；两端尺寸、采样/抗锯齿/混合设置相同，冻结发布目录并核对 SHA-256，计时期间没有构建/回归并行。下表是热缓存微场景，不是整页、首帧、端到端 OCR、AOT 吞吐或 GPU 性能。

| CPU 场景 | 原版 ms | 本库 ms | 本库/原版 | 本库初始实现 ms |
| --- | ---: | ---: | ---: | ---: |
| clear-1024x512 | 0.04813 | 0.05114 | 1.06 | 0.05119 |
| opaque-rectangle | 0.04775 | 0.05050 | 1.06 | 0.05118 |
| alpha-rectangle | 0.86375 | 0.15320 | 0.18 | 0.20040 |
| linear-gradient | 1.35872 | 0.05209 | 0.04 | 0.05449 |
| nearest-image | 0.37154 | 0.04596 | 0.12 | 3.70939 |
| linear-image | 1.04276 | 0.41995 | 0.40 | 7.06187 |
| multiply-rectangle | 1.05066 | 0.15993 | 0.15 | 4.65598 |
| repeat-image-shader | 1.17216 | 0.02473 | 0.02 | 9.59743 |
| lighting-color-filter | 0.04924 | 0.05101 | 1.04 | 6.17685 |

这九个微场景的最大相对落后约 6%，不表示整体兼容性/画质或 GPU 已满足要求。初始实现是本轮新增算法的第一版，不是已公开版本；两个报告各自原版中位数保留，不能声称整库相对 0.1.1 提升同样倍数。重复图片 Src 和渐变使用有界热缓存，冷启动没有从表中得到验证。

| 场景 | CPU ms 原版/本库 | CPU 占整机 % 原版/本库 | 托管分配 B/帧 原版/本库 | WS MiB 原版/本库 | Private MiB 原版/本库 |
| --- | ---: | ---: | ---: | ---: | ---: |
| clear-1024x512 | 0.04794/0.05147 | 8.30/8.30 | 0.00/0.04 | 32.22/30.85 | 12.48/12.40 |
| opaque-rectangle | 0.04850/0.05030 | 8.46/8.30 | 0.00/0.04 | 31.85/31.11 | 11.79/12.08 |
| alpha-rectangle | 0.87085/0.15559 | 8.42/8.30 | 0.00/0.11 | 31.88/31.13 | 11.82/12.15 |
| linear-gradient | 1.35293/0.05188 | 8.30/8.30 | 0.00/0.08 | 32.04/33.84 | 11.93/13.80 |
| nearest-image | 0.36995/0.04667 | 8.30/8.30 | 0.00/0.11 | 29.71/37.06 | 9.84/16.80 |
| linear-image | 1.03760/0.41809 | 8.30/8.30 | 0.00/1.02 | 29.75/37.11 | 9.55/16.87 |
| multiply-rectangle | 1.04577/0.15928 | 8.29/8.30 | 0.00/0.46 | 29.42/29.84 | 9.27/9.66 |
| repeat-image-shader | 1.18960/0.02463 | 8.45/8.30 | 0.00/0.06 | 29.45/30.59 | 9.34/10.63 |
| lighting-color-filter | 0.04808/0.05081 | 8.30/8.30 | 0.00/0.12 | 29.38/30.70 | 9.23/10.60 |

内存仍有差距，图片场景本库工作集比原版约高 7 MiB，不能只用接近零的稳态托管分配宣称占用更低。分配列不包含原版原生堆；WS/Private 是整个工作进程在该场景后的瞬时值，受前序 JIT、GC、缓存影响，既非峰值也非单 API 保留字节。

热点修复包括：Src 图片复用采样行、连续复制并保留 AA/stride/格式语义；Multiply 的 AVX2 饱和运算与标量等价；图片着色器最近邻行缓存上限 64 条/4 MiB，按完整变换与源行定位；常量滤镜一次求值和量化、整行填充。关闭 SIMD 的回退不改变像素。

## 复现与继续工作

```powershell
dotnet build SimdSkia.sln -c Release
python tools/check-blends.py --output artifacts/my-blends
python tools/check-canvas-operations.py --output artifacts/my-canvas
python tools/check-raster-shaders.py --output artifacts/my-raster-shaders
python tools/check-color-filters.py --output artifacts/my-color-filters
# 前三项当前严格退出 2，颜色滤镜退出 0。所有输出目录须为新的。
python tools/run-api-completion-regressions.py --output artifacts/my-regressions
dotnet publish tests/Foundations/NativeAot -c Release -r win-x64 -o artifacts/my-aot
./artifacts/my-aot/NativeAot.exe --require-aot
# 原版和本库分别 publish 到独立目录，计时期间停止其他构建/测试：
python tools/run-raster-benchmarks.py --original artifacts/my-original --after artifacts/my-managed --output artifacts/my-timing
```

仍需继续文本运行/Blob、滤镜、记录回放、颜色管理/格式、编解码细项、路径操作、外部 GPU 与其他公共 API。许多既有签名也仍有明确拒绝项；851 是元数据缺口，不是全部行为缺陷数量。下一批继续，不把本报告作为任务完成或发布许可。

[逐项 API](api/comparison.json) · [原始证据及状态](evidence/raster-api-2026-11-06/status.json) · [源码及证据哈希](evidence/raster-api-2026-11-06/sha256.json) · [前一基础批报告](FOUNDATIONS-API-2026-11-06.md)
