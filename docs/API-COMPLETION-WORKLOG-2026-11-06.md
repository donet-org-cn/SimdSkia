# 公共 API 持续补全工作日志（未完成、未发布）

用户本轮验收要求：在固定 SkiaSharp 3.119.4 的 **3,235 项公共元数据基线**上，缺口降至 **最多 99 项**才算完成。不得删基线、加入只抛异常的签名或空实现凑数量。此任务仍在进行，当前没有达到停止条件；元数据匹配也不等于完整行为兼容。

本轮起点为 1,279 匹配 / 1,956 缺口。中间快照已到 2,079 匹配 / 1,156 缺口，后续实现仍在核验，最终数字以重新提取的清单为准。

已加入真实实现并继续验证：

- 4×4/旋转缩放矩阵、色彩空间数学值、字体指标及 GPU 资源描述值；后者不代表外部 GPU 互操作已经实现。
- SKData 共享所有权、子视图、稳定地址、共享流；内存/文件/托管/前缀缓冲流和写入流。流回调和释放回调参与实际读写及生命周期。
- 原始/常规路径遍历、反向路径、SVG 解析与输出、端点式椭圆弧、圆锥转二次曲线。常规遍历已去除整路径临时列表。
- SKPathMeasure 自适应长度表、距离查询、切线/矩阵及保留原曲线类型的截取。
- 字体字符/编码/字形/宽度/坐标/截断/轮廓重载、字体表导出、系统静态 TrueType 目录和回退；复杂排版、hinting、位图/彩色字体仍有原有缺口。
- 外部像素位图、直接光栅 Surface、Image 引用/复制/子图/编码数据；连续 Span、实际 stride、共享生命周期与一次性释放回调。
- 旧 SKPaint 文字转发和文字 Blob 创建（新增部分仍在做原版对照）。

性能定位已找到 cmap 4 逐区段扫描的热点，改为验证排序后做二分查找，并继续验证固定容量字形映射缓存及直接 Span 输出。第一轮二分查找实测，中英文测量与偏移组合从约 54.4 µs 降至 2.64 µs，仍比原版约 0.80 µs 慢，不宣称整体追平。迭代器单次分配从约 88 KB 降至 64 B，速度仍落后原版。最新缓存版本测量尚在进行。

已取得的对照记录：基础值/流 373 组、颜色数学与 32,896 组预乘/反预乘颜色通过；99 组路径和 688 组路径测量通过。新增字体与图像严格对照有失败，检查工具保留退出 2；Windows 原版使用 DirectWrite 的纹理包围盒，当前解析轮廓的保守包围盒并不完全相同；预乘读取存在少量 1 级通道舍入差异。历史 JPEG、描边、整页画质失败继续保留，尚未新测完整应用的 CPU/GPU 性能。

所有原始日志暂存 `artifacts/api-completion-2026-11-06`。完成当前批的构建、回归、AOT 和性能验证后，将固化证据并更新主兼容清单。当前不发布、不宣称二进制替换兼容。

当前已固化到 2317/3235、918 缺口、0 额外，仍在继续。完整阶段报告和可复现实测见 [FOUNDATIONS-API-2026-11-06.md](FOUNDATIONS-API-2026-11-06.md)，40 项真实 AOT 与新区域/圆角对照已归档。后续实现继续追加新快照，不改写已经冻结的证据。

图像归因更正：初始 `image-check-3` 的 48 组差异并非全部 1 级舍入，最大通道差曾为 45。已采用两端完全相同的物理字节输入，确认 FromBitmap 保留输入 Alpha，较大差异来自 FromEncodedData 默认输出直通 Alpha，而原版输出 N32 预乘。兼容解码工厂现已按原版输出；通过原地通道/Alpha 转换与共享存储重标记避免第二张完整图像。`image-check-6` 为 181 组、两端各 230 项生命周期检查，仍 32 组严格字节差异，最大 1；底层 ImageCodec 继续保留直通 Alpha 输出，不把合同差异归咎于解码包。新增混合模式和点 API 仍在持续验证，本批报告的冻结性能数据不作为后续修改后的整应用成绩。

后续光栅批次已到 2384/3235、146 类型、851 缺口、0 额外。CPU 全混合、图片着色器与滤镜的真实实现、生命周期、AOT 和逐项性能继续记录在 RASTER-API-2026-11-06.md；旧 2317 快照及其失败不改写。源码仍未发布，目标保持进行。

文字批次冻结清单已到 **2455/3235、153 类型、780 缺口、0 额外**，仍未达到最多 99 项。实际 AOT 56 项、Direct2D 246418 项通过；严格文字图像只通过 15/392、字体仍 122 字段差异，不宣称画质或全应用性能持平。报告见 [文字 API 与 GPU 遮罩修复](TEXT-API-2026-11-06.md)；既有失败保留，公开 0.1.1 不变，后续 API 继续实现。

图像滤镜与工具批次冻结到 **2549/3235、686 缺口、0 额外**。真实 AOT 63 项、生命周期/字节 60132 项通过；严格图像仍 79/756 幅失败。偏移/颜色较快、模糊与阴影仍慢，默认缓存与 16MiB 原版缓存的内存诊断单独归档。见 IMAGE-FILTER-API-2026-11-06.md；旧版文字转发和放大镜继续实现，不计入本冻结快照，目标仍进行。


## 继续进行：查询、颜色空间、路径效果、遮罩与编码选项

- 固定基线仍为 SkiaSharp 3.119.4 的 3235 项，未修改分母；`comparison-options-1.json` 最近一次提取为 **2709 匹配、165 类型、526 缺口、0 额外签名**。这是进行中状态，未达到最多 99 项，公开 0.1.1 未更改，未发布。
- 查询和图层：`image-filter-check-21` 为 **746/840**，元数据一致；生命周期正确的直接 native SaveLayerRec 16/16、普通有界 SaveLayer 4/4 通过。原版托管 SaveLayerRec 有界指针重载的四幅空白差异另行保留，不复制未定义行为。
- 颜色空间：真实 matrix/TRC、ICC 字节所有权/按需固定、Gamma/矩阵派生、像素转换和写入；`color-spaces-5` **436 元数据和 24/24 转换**通过。RGB/XYZ parametric ICC 之外、非 sRGB 直接绘制/跨空间重采样/带 ICC 编码仍明确拒绝，不能误称全部颜色管线完成。
- 路径效果：真实 dash/corner/trim/discrete/1D translate/rotate/morph/2D line/path/compose/sum，Paint/Clone/GetFillPath/绘制接入；缓存每 Paint 最多 16 项/2 MiB。修复 aliased 半像素取整、组合不适用时回退与隐式闭合形变。`path-effects-2` **369/720** 过 max32/RMSE2，8 项几何差异保留（含旧描边闭合表示和已有 region 栅格差异）。不是画质全部通过。
- 遮罩：纯 C# 覆盖率表、gamma/clip、四类 blur style、CTM/固定 sigma；小 sigma 使用 Bessel 离散核，轴对齐矩形使用基线解析 profile 与九宫格。`mask-filters-4` **42/312** 过 max3/RMSE.75，12 元数据一致。曲线覆盖率、若干 blur/phase 仍失败。GPU 显式回退/拒绝；图像遮罩还未实现，不静默忽略。
- PNG/JPEG：只对有真实编码链路的 JPEG/PNG 增加 Encode 重载。WebP 选项是有效值合同；由于依赖没有编码能力，WebP Encode 重载仍保持缺失。JPEG 4:2:2 也明确不支持；4:2:0/4:4:4、Ignore/BlendOnBlack 真实生效。PNG 由 Sdcb 生成容器，明确选项时重新过滤并按数字级别压缩，默认路径保留 Sdcb 快路径，未替换整套编解码依赖。
- `encoder-options-3` **88/88** 像素/采样场景通过，65 元数据一致、两边各 572 独立断言。PNG 必须精确相等；JPEG 门槛 max20/RMSE3。此为编码选项专用图，不覆盖此前独立 JPEG 失败及整页失败。
- 定位并修复 PMA ReadPixels 的 float 运算顺序及 ties-to-even：全部 32896 个有效预乘通道组合与原版吻合；`pma-read-probe-2` 包含标量/SIMD、尾部宽度、通道交换检查。新增 Vector256 / Vector128 路径并保留标量，正在冻结三方 CPU/分配/内存测量。原图像编码仅 1 级差异来自读取转换，现已消除。
- 本批 Release 整库、最新 Native AOT 和全量回归尚待执行；不能用上一快照结果替代本批验证。尚未达到本次用户停止条件，继续实施。

PMA 三方计时已完成（`pma-timing-1`，3 worker/fork、9 个串行进程、各场景 2s 预热/.8s 计时）：512 RGBA 342.90→220.65 µs，带交换/padding 366.10→221.39 µs，720p 2473.77→1560.44 µs（原版→当前）。相对本轮旧快照加速 6.35/6.57/6.36 倍。新输出逐字节对照待上方脚本记录核验；这是 CPU 像素转换，不代表整页或 GPU 已持平。当前工作集约 37.5–38.5MiB，Private 约 16.4–17.4MiB，原版 Private 约 14.7–16.0MiB，仍不能宣称全局内存更少。


Codec 快照进一步完成：`comparison-codec-1` **2726/3235、165 类型、509 缺口、0 额外签名**。新增 JPEG/BMP 逐行转移与跳行、PNG/GIF 延迟写入、SKStream 所有权、缩放/子集能力查询及 EXIF 元数据。`codec-streaming-4` 为 **418 元数据一致、21/21 输出通过**，JPEG 方向 1–8 都真实测试；保持基线 PNG EncodedOrigin=TopLeft（引擎元数据工具仍能读取真实 eXIf）。完整缓冲输入仍在创建时解码；不是压缩数据实时增量/低内存解码，scaled/subset GetPixels 仍是差距。Native PNG 在 Success 不设置 rowsDecoded 的旧栈值单独归档，不当作合法兼容目标。

本批 `full-build-12` 零警告/错误；`regression-9` 基础/像素/编解码/PDF/特性/字体/优化/Avalonia/控件/Direct2D/Vulkan/采样全过，独立 JPEG 已知严格门槛继续失败。`aot-9` 实际 Native AOT **80 项通过**。`regression-8` 曾发现旧颜色空间拒绝断言失效及图像 alias 边界回退，原记录保留，basic-8c / gpu-sampling-8b 和完整 regression-9 修正后通过。

PMA 正常/全部硬件指令禁用/AVX2 禁用三种输出均与 Native 完整 65536 像素探针相同，含 32896 个有效预乘组合。计时场景原版/当前的差异字节数都为 0，旧快照分别为 5048、5048、35763。`raster-api-timing-1` 因旧 DLL 目录错误已中断并标 INVALID，不使用；真实旧目录为 raster-benchmark-managed，哈希已验证不同，正在执行 raster-api-timing-2。


下一批进行中：已实现 RuntimeEffectUniform 的真实 float/int/Span/颜色/矩阵字节序列化与强类型 child 值合同；`runtime-values-2` 34/34 对照通过，33 次缓冲前后 guard 检查。原版私有 SKBlenderStatic 类名仅记 local.json；公开类型分类和持有对象 identity 均验证。**尚未实现 RuntimeEffect 语言编译/执行，不能因值合同已补充而宣称着色器功能完成。** 元数据 `comparison-runtime-values-1` **2759/3235，167 类型、476 缺口、0 额外签名**；主 README/清单仍指向已冻结的 2726 快照。

原始 TrueType glyf 轮廓缓存原先无界，现每字体最多 512 项/约 4 MiB 片段及条目估算载荷，second-chance 不在热命中时重连对象引用；驱逐/清空不修改已被 glyph run 持有的几何。`font-programs-raw-cache-1` 共 255129 项通过，含 1600 glyph 压力、驱逐/清空后重解析逐片段一致。此改动发生在冻结报告 QUERY-EFFECTS-CODEC 之后，不能把旧 CPU 数据作为它的新性能测量；后续仍需验证字体热路径和工作集。


后续检查点（任务继续，未达到停止条件）：RuntimeEffect 编译/AST执行、类型/数组/矩阵/32位整数、child 图及 builder 已真实实现，`runtime-effects-3` 37/37 图像与48元数据全过；不支持 helper/loop/unsigned 等完整SkSL语法会返回编译错误，不做静默替代。新增资源描述、lattice、picture/drawable与 Vulkan GRContext/离屏表面后，`comparison-gpu-context-1` 为 **3005/3235、192 类型、230缺口、0额外项**。这是待冻结开发快照，根清单仍保持2726历史记录。
`lattice-resources-4` 56/56语义图像、8元数据、各66断言通过；原版RectTypes把int枚举直接交给byte布局，原始公共包装层差异保留于 lattice-resources-1，对应语义oracle明确使用packed-lattice，不能把56/56称为未修正包装层逐字节一致。
`pictures-4` 64图中44通过max3/RMSE.75、14元数据一致、各237独立检查；旋转和曲线AA、少量采样边界仍失败。SKP二进制序列化尚未加入；picture shader是声明的有界光栅tile，尚非随CTM自适应重绘。`pictures-1`原版在录制结束前Dispose自定义Drawable触发native访问违规，原始崩溃保存；之后fixture让Drawable存活至EndRecording。
`gpu-context-2` 同一GTX1050Ti真实Vulkan设备上分别跑原版和本实现，10/10图像、3元数据、各67项同步/缓存/生命周期检查通过；这是功能测试，不是GPU性能报告。新Context是借用设备/队列，不拥有外部VkDevice，单采样compute路径，MSAA/mips/保护上下文仍显式拒绝，外部texture/render-target导入仍是缺口。


2026-11-07 持续检查点（尚未结束）：最新固定基线在 atlas/filter 编译快照为3122/3235、113缺口、2个CreatePaint Obsolete属性差异，随后已按原版error=true精确属性修正，待再审计。新增Float颜色/两圆锥在有标签与无标签sRGB目标共144/144通过；Perlin分形/湍流、平铺、局部/画布变换、alpha共144/144通过。
新增Mesh/Atlas真实颜色/UV插值和材质混合，初版85/85失败（混合顺序与单像素边缘），mesh-atlas-2修正混合后仍受固定边缘规则影响；按基线SkEdge的26.6初值、16.16步进实现TriangleCoverage后，`mesh-atlas-3` **85/85通过、各215检查**，门槛未放宽。固定网格目前CPU路径，Vulkan/Direct2D显式拒绝此命令；没有新增GPU网格能力承诺。Atlas有8项/约8MiB有界缓存，重复参数复用几何/材质，参数变更比较快照后重建。旧DrawBitmap统一转至PMA采样内核，消除旧的反复unpremultiply/re-premultiply路径，性能待新测。
`canvas-views-1` NoDraw/NWay/Overdraw与录制裁剪查询17/17图、4元数据、各19检查通过；NoDraw巨型逻辑画布只持有1x1存储。`cache-controls-managed-2`与原版各30检查通过，清理/禁用后重绘字节不变；原版实测默认FontCacheLimit=2MiB，本实现已对齐，并将全局outline/mask/run/text缓存分配到2MiB预算。FontCacheCountUsed明确表示本引擎活跃轮廓缓存owner数，不伪称native strike数；字节为估算缓存载荷，不是进程工作集。`font-programs-global-cache-2`255129检查通过，较小预算下canonical88字形/520802字节。
`path-boolean-2`184拓扑条目中183一致（原版OpBuilder全Union优化对evenodd输入有额外差异），160严格图像中40通过，曲线AA/alias差异保留；布尔运算为Clipper2托管计算，曲线1/1024单位展平、6位小数裁剪，输出不保留原贝塞尔verb。Clipper2 2.0.0本来已被Avalonia层使用，本轮增加Core的精确版本引用并复用Boost许可，不是新增native包。
`image-operations-3`有效outSubset/outOffset合同12元数据一致、28图22通过，6个RawShader最近邻浮点边界仍失败；原始backing布局单独保存（Skia可能返回带padding的较大backing，不能拿未定义的outSubset外像素冒充画质结果）。新增Paint/Picture生成滤镜按CTM重新绘制，NullSurface不创建真实像素画布；尚待对照。GL接口仅能力/函数表描述，GRContext.CreateGl仍缺失，不是OpenGL绘图后端；需完成真实ANGLE元数据对照。


2026-11-07最终冻结：3136/3235、200类型、99缺口、额外0，达到数量目标。新增API与缓存/性能实测、失败门槛和完整余项见 PUBLIC-API-99-2026-11-07.md，不能将metadata达标当作完整行为兼容。最终2MiB字体预算按128/128/1536/256KiB分配，修复首轮整页缓存抖动；首轮中断数据保留。完整Release17、regression12（仅旧JPEG严格失败）、474413精确优化、255129字体、AOT15正常/显式Scalar24项通过，未提交/发布。
