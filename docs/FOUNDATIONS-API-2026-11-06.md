# 持续补全与性能检查：2317 / 3235 公共元数据（未发布）

2026-11-06，Windows x64。**本轮目标仍未完成：还缺 918 项，要求是最多 99 项。** 固定 SkiaSharp NuGet 3.119.4 / 3,235 项基线，未缩减分母，没有用返回假数据、绘制空白或只抛异常的新签名补数。本轮从 1,279 增至 **2,317 匹配、144 公开类型、918 缺口、0 额外/不同签名**。新增 1,038 条包含枚举字段、结构成员、委托及重载，不能当作 1,038 种完整绘图能力。

这是开发中的源码兼容子集，**不是新发行包，不承诺强名称二进制替换**。公开 0.1.1 不包含本轮代码。Windows 以外的平台没有在本轮重新运行；先前已发布版本的跨平台证据不替代这次验证。

## 真实实现

- 4×4/旋转缩放矩阵、色彩转换数学、32 位预乘颜色；完整转移函数包括 PQ/HLG。色彩数学值可独立使用，不代表光栅管线已经支持任意颜色空间。GPU 描述值结构也不代表已有外部 GPU 资源互操作。
- SKData 共享存储、子视图、稳定地址与流；文件/内存/托管/前缀缓冲流、写入流、真实回调、一次性释放和独立视图生命周期。
- 路径原始/常规遍历、逆序、SVG、端点椭圆弧、圆锥转二次曲线；SKPathMeasure 自适应长度表、距离/切线/矩阵查询、保留原曲线类型的截取。
- 字体的编码/字形/宽度/位置/截断/轮廓重载、指标、表导出、系统静态 TrueType 目录、CSS3 样式选择、基本字符回退；旧 Paint 文字转发和 Blob 创建。
- 外部像素位图、直接光栅 Surface、Image 引用/复制/子图/编码数据及光栅工厂；使用实际 stride、连续 Span 和共享存储，避免额外整帧拷贝。Pixmap 的安全租约可比原对象活得更久，这是与原版非拥有视图不同的明确生命周期策略。
- 整数区域的规范扫描带、并/交/差/异或、查询、遍历、边界路径和 Canvas 绘制/裁剪，不按坐标范围分配巨型位图。
- 圆角矩形由近似 cubic 改为原版的有理 conic，支持全部起点/方向及形状查询、仿射变换保留形状标签。

## 原版验证与失败

| 验证 | 结果 |
| --- | --- |
| 基础数学/数据/流/格式 | 404 组通过；颜色数学通过；32,896 对颜色逐字节一致 |
| 路径、SVG、圆角、迭代器 | 419 组严格行为对照通过 |
| 路径测量/截取 | 688 组严格对照通过 |
| 字体与旧 Paint/Blob | 2,738 组、9,000 多标量；严格数值仍有 **186 字段差异** |
| 两字体完整 BMP + 补充平面采样 | 合计 **143,360 字符字形映射**，二进制输出精确一致 |
| 图像工厂、外部像素、释放 | 133 组；双方各 218 本地生命周期检查通过；严格字节仍 **48 组差异** |
| 区域 | 1,243 组；仍 **8 字段差异**，来自同一二次曲线的 1 像素差及其反向填充 |
| 实际 Native AOT | **40 项通过**，执行时验证没有动态代码支持 |
| 完整 Release | 0 警告、0 错误 |

上述严格检查有差异就退出 **2**，没有改成全绿。字体主要差在 Windows 原版 DirectWrite 的纹理包围盒与本库解析轮廓的保守包围盒，部分缺字轮廓还有小数精度差；测量和字形映射一致不等于复杂文字排版一致。初始图像对照的 48 组差异最大曾为 45，不能全部归为 1 级舍入；后续相同物理输入与解码工厂合同修正见文末更正。区域复制了 3.119.4 的可观察行为：`ClipIterator` 只筛选与 clip 相交的块，返回原块而非修剪后的矩形；上游 C# `Next` 调用了 `sk_region_iterator_rect`，对照记录保留了这一细节。

既有回归通过：基础 36、像素 1,358、编解码 1,417、PDF 18、字体发现 43、Avalonia 文字链 595、控件 9,750、精确优化含字体 472,085；圆角变更后的再次精确优化 472,073 项（该次未传字体夹具，不能写成 472,085）。Direct2D 148,060、Vulkan 设备/生命周期及 590 采样检查通过，设备 GTX 1050 Ti，包含完成等待与回读。首次 Features 的旧“文字测量必须拒绝阿拉伯文”断言已经按原版 SKFont 的未排版测量语义修正；复杂 DrawText 仍明确拒绝，更新后通过，旧失败日志保留。

**历史 JPEG 严格门槛继续失败，既有整页画质 0/18 未重测、未改判。** 本轮没有新做完整应用或 GPU 吞吐对照，GPU 回归通过不能当作 GPU 性能追平。

## CPU 性能、CPU 时间、内存

同机 i7-8700K（6 核 / 12 线程）、.NET SDK 10.0.403 / runtime 10.0.14，Release JIT；每报告 9 个串行进程、3 轮轮换顺序，预热 0.4 秒、测量 0.8 秒，取中位数。计时使用冻结发布目录并核对 SHA-256，测量期间没有构建重叠。基础基准的 before 是新增 API 的首次真实实现，不是公开 0.1.1。

| CPU 场景 | 原版 µs/次 | 本库 µs/次 | 本库 / 原版 |
| --- | ---: | ---: | ---: |
| regular-iterator-512-curves | 5.536 | 11.503 | 2.08 |
| raw-iterator-512-curves | 6.531 | 8.079 | 1.24 |
| font-measure-and-offsets | 0.805 | 0.250 | 0.31 |
| path-distance-query | 0.023 | 0.026 | 1.10 |
| data-subset-stream | 0.477 | 0.064 | 0.13 |
| region-contains-rectangle | 0.008 | 0.008 | 0.96 |
| region-contains-region | 0.011 | 0.013 | 1.20 |
| region-intersects-region | 0.013 | 0.012 | 0.92 |
| region-rectangle-iterator | 0.308 | 0.052 | 0.17 |
| region-boolean-operation | 0.511 | 0.514 | 1.01 |

耗时不是全部：普通路径迭代器仍约慢 2.08 倍，raw 约慢 1.24 倍；路径距离查询略慢。重复中英文测量/坐标由初始约 53.64 µs 降至 0.25 µs，原版约 0.805 µs。优化是 cmap 4 二分查找、固定 128 槽的并发安全字符缓存及直接 Span 输出，不是关闭画质或省略字符。普通迭代器分配约 88,136 B → 64 B，原版 88 B。

| 场景 | CPU 时间：原版 / 本库 µs | 工作集：原版 / 本库 MiB | Private：原版 / 本库 MiB |
| --- | ---: | ---: | ---: |
| regular-iterator-512-curves | 5.514 / 11.458 | 37.94 / 31.83 | 18.27 / 13.66 |
| font-measure-and-offsets | 0.812 / 0.254 | 35.55 / 36.38 | 15.06 / 16.42 |
| data-subset-stream | 0.503 / 0.064 | 39.55 / 35.56 | 18.34 / 15.13 |
| region-boolean-operation | 0.508 / 0.516 | 33.12 / 31.11 | 13.31 / 12.72 |

字体文件改为按页读取、保留只读文件句柄和有界页缓存，而不是整体 File.ReadAllBytes；同一基础基准字体场景本库工作集约 47.36 → 36.38 MiB，原版约 35.55 MiB。仍有差距，不能推导整个 Avalonia 应用的工作集都变小。表中内存是工作进程当时总量，受前序场景与 GC 影响；完整每轮 CPU 占机器百分比和分配量见原始 runs/summary。

区域查询不再构建临时布尔结果，三个查询为稳态零分配；迭代器只保留扫描带视图。区域布尔运算约 1.063 → 0.514 µs，原版 0.511 µs；托管分配约 2,440 → 504 B，原版 80 B。**托管分配不含原版的原生堆分配**；WS/Private 才是整个进程的量，两者不能混为一列。

## 复现与后续

在仓库根目录，使用 SDK 10.0.403+：

```powershell
dotnet build SimdSkia.sln -c Release
python tools/check-foundations.py --suite Foundations --output artifacts/my-foundations
python tools/check-foundations.py --suite PathOperations --output artifacts/my-paths
python tools/check-foundations.py --suite PathMeasure --output artifacts/my-measure
python tools/check-foundations.py --suite FontOperations --output artifacts/my-fonts
python tools/check-foundations.py --suite ImageFactories --output artifacts/my-images
python tools/check-foundations.py --suite Regions --output artifacts/my-regions
# 上面后三项严格对照当前退出 2，失败记录必须保留。
dotnet publish tests/Foundations/NativeAot -c Release -r win-x64 -o artifacts/my-aot
./artifacts/my-aot/NativeAot.exe --require-aot
```

输出目录须是新的。原版只进入 Original 测试/基准项目。基准程序分别在 `benchmarks/FoundationOperations` 和 `benchmarks/Regions`，冻结两端发布目录后用 `tools/run-foundation-benchmarks.py`；参数与原始文件哈希在证据 manifest。库内没有加载 Google Skia、原生字体库或动态生成机器码。

还需补全 Image/Shader、滤镜、文本运行、路径布尔运算、记录/回放、颜色管线及外部 GPU API 等。原生 Skia handle、hinting、复杂文字和许多颜色格式依然明确不支持；格式元数据可计算不等于能够分配或绘制所有格式。任务保持进行，**918 缺口不满足用户的最多 99 项条件**。

[固定基线与逐项 API 差异](api/comparison.json) · [本批原始证据与校验](evidence/foundations-2026-11-06/sha256.json) · [持续工作日志](API-COMPLETION-WORKLOG-2026-11-06.md)。

图像归因更正：初始 `image-check-3` 的 48 组差异并非全部 1 级舍入，最大通道差曾为 45。已采用两端完全相同的物理字节输入，确认 FromBitmap 保留输入 Alpha，较大差异来自 FromEncodedData 默认输出直通 Alpha，而原版输出 N32 预乘。兼容解码工厂现已按原版输出；通过原地通道/Alpha 转换与共享存储重标记避免第二张完整图像。`image-check-6` 为 181 组、两端各 230 项生命周期检查，仍 32 组严格字节差异，最大 1；底层 ImageCodec 继续保留直通 Alpha 输出，不把合同差异归咎于解码包。新增混合模式和点 API 仍在持续验证，本批报告的冻结性能数据不作为后续修改后的整应用成绩。
