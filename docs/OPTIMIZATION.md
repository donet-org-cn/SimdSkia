# CPU 优化实测：2026-10-12

本轮修改实际引擎代码，重跑原版/标量/SIMD各 3 个独立进程，12 场景、2 尺寸、7 批；完整 1512 批。硬件、输入、字体 SHA、运行时、采样配置与优化前一致。表中原版为本轮重新实测值，优化前/后为 Simd SIMD 批均值中位数。

## 时间与分配

### 256×256

| 场景 | 优化前 ms | 优化后 ms | 自身加速 | 本轮原版 ms | 后/原版耗时 | 托管 B/op 前→后 | 质量/可比性 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| clear | 0.007 | 0.007 | 1.0× | 0.005 | 1.37× | 0→0 | 通过 max=0 |
| rectangles | 0.954 | 0.051 | 18.7× | 0.038 | 1.36× | 1→0 | 通过 max=0 |
| blend | 8.545 | 0.528 | 16.2× | 0.935 | — | 5→0 | 诊断：max 2>1 |
| evenodd | 1.144 | 0.018 | 64.5× | 0.015 | 1.22× | 81384→0 | 通过 max=0 |
| stroke | 0.378 | 0.016 | 23.4× | 0.008 | 2.10× | 84271→0 | 通过 max=0 |
| rect-aa | 3.931 | 0.030 | 133.2× | 0.024 | 1.21× | 311605→0 | 通过 max=1 |
| gradient | 3.477 | 0.014 | 244.8× | 0.189 | 0.08× | 2→0 | 通过 max=0 |
| image | 3.199 | 0.179 | 17.8× | 0.246 | — | 81708→0 | 诊断：max 253>0 |
| curve-aa | 3.039 | 0.029 | 106.1× | 0.041 | — | 219984→0 | 诊断：max 57>32 |
| chinese | 83.666 | 0.153 | 546.3× | 0.073 | — | 1182953→0 | 诊断：max 173>32 |
| png-encode | 10.445 | 0.346 | 30.2× | 2.751 | — | 2561528→787440 | 诊断：压缩策略不同 |
| png-decode | 4.057 | 1.347 | 3.0× | 0.305 | 4.41× | 543529→277822 | 通过 max=0 |

### 1024×768

| 场景 | 优化前 ms | 优化后 ms | 自身加速 | 本轮原版 ms | 后/原版耗时 | 托管 B/op 前→后 | 质量/可比性 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| clear | 0.083 | 0.081 | 1.0× | 0.073 | 1.11× | 0→0 | 通过 max=0 |
| rectangles | 13.823 | 0.278 | 49.7× | 0.214 | 1.30× | 7→0 | 通过 max=0 |
| blend | 103.321 | 3.539 | 29.2× | 10.064 | — | 34→2 | 诊断：max 2>1 |
| evenodd | 14.618 | 0.165 | 88.4× | 0.148 | 1.12× | 835751→0 | 通过 max=0 |
| stroke | 3.679 | 0.106 | 34.7× | 0.079 | 1.34× | 839074→0 | 通过 max=0 |
| rect-aa | 46.810 | 0.223 | 209.9× | 0.210 | 1.06× | 1556810→0 | 通过 max=1 |
| gradient | 42.137 | 0.166 | 254.6× | 2.257 | 0.07× | 17→0 | 通过 max=0 |
| image | 41.125 | 0.959 | 42.9× | 3.185 | — | 835385→0 | 诊断：max 253>0 |
| curve-aa | 27.229 | 0.178 | 153.0× | 0.215 | — | 1246932→0 | 诊断：max 63>32 |
| chinese | 302.151 | 0.427 | 706.8× | 0.266 | — | 4143817→0 | 诊断：max 173>32 |
| png-encode | 124.193 | 4.550 | 27.3× | 29.733 | — | 27205464→9440176 | 诊断：压缩策略不同 |
| png-decode | 46.662 | 15.572 | 3.0× | 3.795 | 4.10× | 6508471→3290442 | 通过 max=0 |

自身加速可用于检查优化是否有效；质量未过和 PNG 压缩策略不同的场景不能据此宣布超过原版。旧、新运行不在同一时间，操作系统调度、温度、频率及后台活动仍会影响对比。本轮原版重复测量作为参照，不把两次原版当固定常数。逐项原版漂移见 optimization-summary.json。

## 改动及缓存边界

- 矩形按连续像素填充；AA 矩形直接计算与原来 16×16 网格一致的边缘覆盖率。高坐标浮点采样精度不足时回到通用路径算法。
- 预乘 Alpha 合成使用 Vector<byte>/Vector<ushort>，保留整数舍入、饱和、标量尾部；Straight Alpha 使用原有精确标量换算。显式 SIMD 可关闭；运行时标准内存拷贝/JIT 优化仍开启。
- 路径按包围盒生成覆盖率，使用活动边表和区间差分计数；固定覆盖率编译为行内 spans，省去每帧扫描整张 mask。
- 画布覆盖率 LRU：64 项、16 MiB 数据预算（计入 mask、spans 与估算几何字节），渐变行缓存 16 项/4 MiB。SKCanvas 描边 64 项/4 MiB；SKFont 布局 64 项/4 MiB；小矩形几何表另有数量上限。字典/节点和数组对象头不计入数据预算。全部随 Dispose 清除。
- key 包含不可变路径快照、变换、AA、clip；布局 key 含文字/坐标/对齐，字号/scale/skew 变化清空布局；描边 key 含宽度/miter/变换。缓存不会保存目标像素或可变源图内容；Span 改图立即生效，源/目标重叠维持旧顺序。
- 图片最近邻按相同源像素分段，复用 X 索引缓冲区；仍使用原来的采样公式，没有通过改采样规则制造速度收益。
- PNG 使用 slicing-by-8 CRC、分块 Adler、Huffman 前缀表、位缓冲、重叠 back-reference 拷贝、分过滤器循环、256 项 gamma LUT。编码保持 stored 块和相同无损像素；解码临时行缓冲使用 ArrayPool，CRC/Adler/gamma 没有省略。

这些暖场景允许正常缓存命中。另测每帧移动、97 个位置循环的路径和文字，容量仅 64 项，用于暴露缓存未命中成本；参见 [变化场景原版对照](PERFORMANCE-CHANGING.md)。该测试计时包含持续细分/布局/覆盖率生成，不能把暖缓存结果套用到所有动态场景。

## 验证与剩余差距

冻结优化前标量渲染器位于 tests/Optimization，仅作为测试参考。5267 项逐字节/格式/stride/Alpha/clip/曲线/反向填充/缓存失效与淘汰/源图修改/别名/字体/编解码检查通过；DOTNET_EnableHWIntrinsic=0 运行同样通过。独立 Python 标准库生成 81 个 zlib、31 个 PNG fixture，涵盖 stored/fixed/dynamic、重叠回溯、五种 filter、低位灰度/调色板和 gamma，并检查校验/截断拒绝。

基础 36 项、扩展 14 项通过；Windows GTX 1050 Ti Vulkan 回归与 CPU 像素一致、缓存及生命周期检查通过。原版 120 场景仍 108 达标、12 个曲线/文字不达标；质量门槛未变，PixelCompare 仍返回失败。API 保持 33 个公开类型/403 项匹配、2832 缺失，未扩大兼容承诺。

对优化前后两个尺寸、12 场景、两种 managed 模式、3 进程的 288 个保存帧逐项核验，所有 canonical RGBA 字节一致；本轮对原版的像素误差及 gate 状态也与优化前相同。

PNG 解码、动态覆盖率等仍有性能差距；曲线/字体、密集混合及某些图片变换的原版质量差距仍待修复。GPU 对照见下节与 [GPU 性能报告](PERFORMANCE-GPU.md)。

## GPU(Vulkan) 优化

在与原版 SkiaSharp 3.119.4（native Vulkan）同机同口径的对照基准上，对 SimdSkia.Gpu 做了四项优化与两处缺陷修复；原版 native 提交后基本立即返回，SimdSkia 每帧需等待 fence 并回读，这是结构性差距，不因优化消失。

- 按内容包围盒限定 compute dispatch：FillPath 用上传前的边数据求包围盒，与裁剪矩形相交后只 dispatch 覆盖区域，shader 取整/AA 采样留了向外余量。空白区域不再执行着色器。
- FillPath 边数据缓存：以（几何对象、变换）为键缓存展开后的边数组（LRU 64 项、16 MiB 字节预算）；同一几何仅平移时直接对祖先边数组做偏移复制，不重新展开轮廓。几何对象失效即失效。
- 持久化上传 arena 与预分配描述符：边/参数缓冲跨 Flush 复用，仅在容量不足时重分配；描述符池一次性预分配，逐帧不再创建/销毁 Vulkan 对象。
- DrawImage 源图缓存按对象身份加内容快照：以 PixelBuffer 引用为键，命中后先做快照校验，内容未变才复用纹理（上限 32 项/64 MiB，LRU 驱逐前先 Flush）。

修复：快照逐行比对曾用不带长度的 AsSpan 导致切片越界至行尾、比对恒假（image 场景每帧重传纹理并回归为内容未命中），已补行字节数；源图内容失效时旧纹理可能仍被本批未提交命令引用，原实现立即销毁构成 use-after-free，现改为移入退役队列，fence 等待后（GPU 空闲）统一释放，上传循环也覆盖退役纹理。

效果（gpu-opt5，3 进程×7 批，与原版同轮）：256×256 image 0.824 ms/1646 B/op、gradient 0.830 ms；1024×768 clear 10.8 ms、rectangles 13.2 ms。托管分配从初版每帧数千字节降到每批一次 arena 写入的稳态（独立探针实测稳态 Render 384 B/帧，初版 8792 B/帧）。像素 gate 与优化前相同：几何类场景全部通过，rect-aa/image/curve/chinese 为已知质量差异，未放宽门槛。完整数据、P95、CPU%、内存见 [GPU 报告](PERFORMANCE-GPU.md)。

## 数据与复现

[完整本轮性能、P95、进程内存](PERFORMANCE.md) · [优化前报告](PERFORMANCE-BEFORE-OPTIMIZATION.md) · [本轮 CSV](../artifacts/performance/optimized-2026-10-12/results.csv) · [前后摘要](../artifacts/performance/optimized-2026-10-12/optimization-summary.json)

```powershell
dotnet build SimdSkia.sln -c Release
dotnet run --project tests/Optimization -c Release -- baseline/fonts/ZenOldMincho-Regular.ttf
powershell -NoProfile -File tools/verify.ps1 -Gpu
powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-optimized-run
powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-changing-run -Scenes curve-changing,chinese-changing -Sizes 1024x768 -Report docs/PERFORMANCE-CHANGING.md
python tools/compare-optimization.py artifacts/performance/comparison-2026-10-12 artifacts/performance/my-optimized-run
powershell -NoProfile -File tools/run-gpu-benchmarks.ps1 -Output results/my-gpu-run
```

前后比较脚本会核查运行完整性、配置、环境、质量门槛及每个 managed 帧的字节；旧目录保留证据，脚本拒绝覆盖已有 run.json。
