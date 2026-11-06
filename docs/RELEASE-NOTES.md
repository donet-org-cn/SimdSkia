# SimdSkia 更新日志 / Release notes

## 0.1.2-preview.2 — CI复核修正

- CI复核新增修正：连续MoveTo保留空轮廓/点；Src/Clear/SrcIn/DstIn/SrcOut/DstATop/Modulate的SaveLayer bounds按原版扩展到父裁剪。原版几何300组/4636值、464组图层组合（2,375,680字节精确比较）和兼容扩展均通过。preview.1标签的CI失败，未上传NuGet；本次使用preview.2保留追溯。
- 包含下方preview.1候选的公共API、GPU互操作、SIMD/分配优化与全部已披露限制，固定基线仍3230/3235。Metal没有Mac GPU，未实机验证；JPEG和整页画质仍失败，性能/内存差距保留。
- 十二包统一0.1.2-preview.2，Avalonia接受>=12.1.0且<13.0.0，已验证宿主12.1.0–12.1.5；Sdcb.SimdCodecs 1.4.0保持。完整性能、CPU/GPU/内存与源快照范围见[最终报告](PUBLIC-API-FINAL-2026-11-07.md)。


## 0.1.2-preview.1 — CI失败候选，未上传NuGet

- 固定3.119.4基线匹配3230/3235，201公开类型，额外0；从99项内部快照补入94项。5项原生句柄/版本接口按用户决定保留，不伪造native Skia对象。
- 汇入此前未发布的路径/混合/滤镜/文字/Picture/RuntimeEffect子集、Perlin、网格/Atlas/曲面、Lattice、PDF注释及资源回调实现；仍有明确参数/格式/语义限制。
- 本轮补充实际1字节Alpha8/ExtractAlpha、延迟Picture图片、WebP选项编码、浮点ColorF/Blender/抖动、BGRA水平LCD与中文EBLC/EBDT点阵、真实向量SVG/XPS、原生v103 SKP子集读写与双方交叉读取。
- 新增GL/GLES、D3D12、Metal核心及外部纹理/目标的真实借用/接管/方向/回调/完成读取。Metal没有Mac GPU，未实机验证。更正早期D3D12夹具缺少Adapter导致的“原版不可用”误判：完整描述符下双方10/10基础画面、346/346互操作画面通过，元数据一致，并补充同后端性能采集。
- Vulkan非重叠矩形合批、设备内复制取代外部表面CPU桥接、GPU清屏/完成读回及去分配；关闭/清理缓存会真实释放覆盖缓存。GPU图像被shader/Picture持有时延长回调至最后消费者；GL待完成draw也持有纹理至实际完成，修复早于原版回调。ToRasterImage独立复制。GPU对象须按创建线程显式Dispose，未建立与原版等同的GC终结回收合同。
- 566项GPU检查/556次精确缓冲对照、AVX2与SSE2各5568浮点缓冲对照通过；真实Native AOT新增CPU链路正常/标量各41检查、系统GL另18检查通过。完整CPU/Vulkan/Direct2D/GL、CPU时间/占用与内存实测见[最终报告](PUBLIC-API-FINAL-2026-11-07.md)，不以提交时间代替GPU完成。
- Avalonia接受>=12.1.0且<13.0.0；已验证宿主范围12.1.0–12.1.5，未来补丁需补宿主/回归。编解码依赖保持Sdcb.SimdCodecs1.4.0，.NET系统压缩和macOS官方窗口宿主的原生例外保持。
- **仍有JPEG、曲线/字体/整页严格画质失败、CPU内存和部分GPU/RuntimeEffect/WebP速度差距。预览发布不代表全面行为/二进制兼容，也不代表整体持平/最多落后10%目标已实现。**

以下为此前开发/发行历史，不能当成本版独立新测量。


## 未发布 — 2026-11-07

- 固定3.119.4基线达到3136/3235，剩余99项、额外0；相比上次冻结2726新增410项，未变更NuGet 0.1.1。
- 新增真实RuntimeEffect子语言、Picture/Drawable、Lattice、Vulkan上下文/表面、float/Perlin shader、Vertices/Atlas/Coons Patch、画布视图、缓存控制与PDF注释/输出。
- 移除网格临时包装，给曲面增加有界覆盖缓存，约0.99MB/帧分配降到噪声量级；总字体预算仍2MiB，通过packed run占比修复整页缓存抖动。
- 完整构建、474413精确优化、255129字体与24新增实际AOT检查通过。JPEG/部分曲线/文字/整页画质失败仍保留，不宣称全面兼容或同画质全面追平。
- [逐项能力、限制、CPU/GPU及内存实测](PUBLIC-API-99-2026-11-07.md)，[集成示例](../examples/Compatibility99/README.md)。下列内容保留历史时点。

## 未发布 — 2026-11-06

- 持续补全到 **2384/3235 项**（146 类型、851 缺口、0 额外），固定 SkiaSharp 3.119.4，仍未满足最多 99 缺口的目标。本次不发布，不修改公开 0.1.1。
- 新增 26 种 CPU 混合模式、点/旧文字/图像转发、图片 ToShader（独立平铺、最近邻/线性/双三次）、组合/算术 SKBlender、颜色矩阵/HSLA/查表/伽马/高对比度等滤镜，并参与真实绘图与引用生命周期。
- 修正默认图片解码为原版 N32/预乘合同，原地转换不增加第二张完整图片；底层 ImageCodec 仍返回直通 RGBA。旧 PNG 测试改为严格核对预乘物理字节，历史失败记录保留。
- 测得热点后优化 Src 图片扫描行复用、Multiply AVX2/标量回退、4 MiB/64 项有界图片着色器行缓存、常量滤镜整行填充与一次性量化；普通画笔不增加终结器。滤镜对照 2448 幅通过、精确优化 474413 项通过、真实 AOT 48 项通过。严格画质仍有失败，GPU 对新增未支持模式/滤镜显式拒绝，不能宣称全面兼容或整应用追平。[测试与性能报告](RASTER-API-2026-11-06.md)。

- 像素批次新增133条元数据，1279/3235、70类型、1956缺口、0额外签名。实现SKPixmap保留/外部像素视图、读取/子集/擦除/缩放，位图复制/Resize、图像与Surface读取、SKColorF及SKAutoCanvasRestore；复制按原版紧凑行分配。仅RGBA/BGRA8888，不伪造完整格式、mipmap或原生句柄。
- 连续行复制、Vector<uint>通道交换、横向采样表复用及现有AVX2双线性内核，始终保留标量回退。三轮局部CPU：1080p读取0.279ms（原版0.284）、通道交换0.604（原版0.615）、最近邻/双线性512²为0.160/0.851ms（原版0.808/2.233）。四基准及246共同用例前后精确相同，六种可复用操作稳态零托管分配；进程内存仍部分高于原版。
- 462组像素、1358项本地检查和Native AOT10项通过；Span仍7组19字段差异，默认工具退出2。旧描边和JPEG严格失败保留，未新测整页/GPU性能、未发布；新示例examples/Pixels。[本批完整报告、内存图和缺口优先级](PIXEL-API-2026-11-06.md)。以下保持前批时点。

- 最新批次新增26条元数据：GetFillPath十二重载、SKPath形状查询和SKPathSegmentMask；当前1146/3235、65公开类型、2089缺口、0额外签名。绘制支持Butt/Round/Square端点及Miter/Round/Bevel接头，DrawLine按原版忽略Style；其他形状StrokeAndFill仍明确拒绝。
- 描边法线预计算、栈/池复用；每画笔最多16项/约1MiB片段载荷的有界轮廓缓存，Reset/Dispose释放持有。局部三轮实测：同一路径复用约10.6倍、8个固定路径轮换约7.2倍，后者每帧分配约1.97MB降到测量噪声量级。查询程序内存仍高于原版，不代表持续变形路径或整页同样提速。
- 新增53组605值、606项几何/缓存、18项Vulkan/Direct2D回读、11项真实AOT检查与examples/Strokes；原版像素27/48通过，21幅失败保持原门槛。既有回归通过；历史JPEG/整页画质缺口保留，无本批整页GPU性能新成绩、未改CPU默认、未发布。详见[本批API/性能/内存报告](STROKE-API-2026-11-06.md)。下方保持前批时点数据。

- 最新15个签名：圆锥/相对圆锥、椭圆/圆弧/切线弧、DrawOval/DrawArc、DrawColor/DrawPaint；总匹配1120/3235，剩余2115。圆形点表示改为9点，核心片段仍28字节，仿射变换保留权重。
- GPU渐变范围验证不再复制数组；Direct2D按shader和透明度缓存至多64个原生画刷，淘汰/Dispose释放，148060条相关硬件断言通过。局部重复渐变约42倍提速，真实控件页约10%，Vulkan主要减少分配；不能将局部收益当整应用收益。
- 129进程CPU/Vulkan/Direct2D/原版与实际窗口测试：CPU默认保持，GPU仍显式启用；受测GPU内存更高。370组/6810值通过；28新绘图16过12未过，原版整页0/18，失败门槛不变，不声明完整行为兼容。Native AOT、标量回退、资源与既有回归通过；新示例examples/Conics。
- [最新报告及CPU/内存图](API-GPU-2026-11-06.md)。未发布新版本；下方前批数据保持原时点。

- 第二批补充多色标线性/径向/扫描渐变、四种平铺、硬色标、局部矩阵及嵌套、颜色/空shader和区域擦除；新增15个精确签名，累计1105/3235，剩余2130项，未建立二进制替换保证。
- 位图擦除改用连续/stride感知的批量填充；不透明两色线性渐变增加Vector<T>行内核，保留双精度投影、float插值和偶数舍入、标量回退，无整图像素缓存。512×512受测擦除快约56–59倍；斜向渐变10.942→0.483ms/帧（原版0.733），像素不变。
- 新增50像素、30擦除字节、480标量/SIMD和Native AOT14项，472085项精确内核回归在硬件开启/关闭均通过；新示例examples/Gradients。BSD许可的扫描角多项式仅作C#翻译，随包保留归属，不调用Google原生绘图库。
- 内存并非全面下降，旧JPEG及整页画质失败保留；新复杂渐变未宣称追平原版、没有新的GPU性能成绩。详见[本批实测](SHADER-COMPATIBILITY-2026-11-06.md)。下方为前批变更。

- 路径、矩阵、颜色、画笔及画布重载扩展；API匹配892→1090，含134个新增颜色字段，仍有2145个公共元数据缺口。
- 修复隐式路径起点、Close/相对命令、字体切换与来源字体释放后的保留视图；画笔Clone保留shader数据。
- 热边界查询及重复直线/圆形几何缓存优化；核心游程掩码不再重复保留密集网格，像素不变。
- 新增原版对照、真实AOT和隔离CPU基准；本次未发布，公开0.1.1包不变。
- 本轮实际速度、CPU、内存与未过画质门槛见[报告](COMPAT-OPTIMIZATION-2026-11-06.md)。整页内存仍高于原版，不能宣称整体同画质/同内存持平。

## 0.1.1 — 2026-11-04

发布补记：十二包已公开下载，GitHub CI 与本地双重逐项核对通过；纯 NuGet.org 空缓存恢复及实际窗口通过。三平台 18 组版本/窗口、两端 6 组 AOT、Windows 四组 GPU 冒烟通过。实际标签来源与证据见[版本兼容报告](AVALONIA-VERSION-COMPATIBILITY.md)及[发布记录](NUGET-PREVIEW.md)。此补记不改写已上架包内容。

- Avalonia 依赖由精确 12.1.5 调整为 **[12.1.0,13.0.0)**，最低编译基线为 12.1.0。桌面包按解析后的 Avalonia.Base 版本选择匹配的 Win32/X11/macOS 托管宿主，仅复制一套到应用输出；不能统一使用 12.1.0 宿主，因为 12.1.2 新增了窗口接口成员。
- 同一套实际 NuGet 包覆盖目前发布的 **12.1.0–12.1.5**；验证控件像素、真实窗口、CPU/GPU 接入及 Native AOT。检查实际解析版本和 DLL 哈希，不为每个版本重新编译适配器。
- 保留启动时版本/图像画刷 ABI 检查；拒绝 <12.1.0 和 >=13.0.0，未知未来 12.x 需要继续回归。应用 Avalonia 家族包应统一版本。
- 构建最低 SDK 更新为 10.0.403，使用包含安全修复的 .NET 10.0.14 AOT 工具链；保留 NuGet 漏洞审计。
- Sdcb.SimdCodecs 1.4.0 及渲染算法保持；没有本补丁的新性能测量，也不宣称补齐原版画质、完整 SkiaSharp API、二进制替代或 macOS GPU。
- 测试证据与命令见 [版本兼容报告](AVALONIA-VERSION-COMPATIBILITY.md)。

## 0.1.0 — 2026-11-04

发布补记：十二包已公开上架并下载核验，三平台与 Windows AOT、实际包消费及发布验证均通过。原版画质失败保留；详见[正式发布记录](NUGET-PREVIEW.md)。此补记不改写已发布包内容。

首个按已实现功能范围发布的正式版，需要 .NET 10。Avalonia 升级到 12.1.5，完整 Sdcb.SimdCodecs 升级到 1.4.0；Clipper2 2.0.0、PdfPig 0.1.16 保持已核验的最新稳定版本。SkiaSharp 源码对照基线仍为 Avalonia.Skia 实际引用的 3.119.4，没有将其误升到不同契约的 SkiaSharp 4。

- PNG 开启 LosslessReduction，在不改变 RGBA 像素的前提下优化编码表示；颜色处理、透明隐藏 RGB 和尺寸/预算验证保留。
- PNG 严格校验不省略。直接从固定生命周期的分片 IDAT 输入流解压校验，省去全量压缩数据复制和对应池缓存；unsafe 仅用于该同步有界流，流不能逃逸 fixed 范围。
- 新增逐字节分片、空 IDAT、带偏移 Span 和并发 GC 回归；独立损坏 PNG、所有颜色/位深/Adam7 与既有像素检查继续运行。
- JPEG 优化 Huffman 表试验增加耗时和内存，未纳入默认配置。
- 性能、CPU/内存、压缩大小与画质重新对照，最终数据见[本版报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/STABLE-0.1.0-2026-11-04.md)。本轮完整123进程数据如下，不沿用旧版成绩。

### 本版完整性能记录

123 个独立串行进程，Windows x64 / .NET 10.0.13 JIT，三轮中位数。原版 SkiaSharp 3.119.4 / Avalonia 12.1.5；GPU包含完成等待及回读，非AOT或显示器FPS。

| 场景 | 原版 | SimdSkia 0.1.0 源码 |
| --- | ---: | ---: |
| CPU控件100% | 0.842 ms/帧 | 0.525 ms/帧 |
| CPU控件150% | 1.591 ms/帧 | 1.255 ms/帧 |
| CPU滚动 | 0.352 ms/帧 | 0.332 ms/帧 |
| PNG编码UI 1080p | 59.467 ms/张 | 10.177 ms/张（无损） |
| PNG编码纹理1080p | 439.060 ms/张 | 156.274 ms/张（无损） |
| 控件100% WS / Private | 91.26 / 47.40 MiB | 110.40 / 71.16 MiB |

Vulkan / Direct2D 相对各例较快原版GPU的综合耗时比为 **0.691 / 0.629**。CPU输出与preview.10逐字节相同，但原版整页画质仍CPU 0/3、GPU 0/12，JPEG严格21/24。纹理PNG解码约慢一倍；部分编码内存/分配仍高，UI JPEG文件约大52%。正式改号不代表原版追平验收通过。

### 正式版支持范围

稳定发布针对文档列出的已实现功能。SkiaSharp 为源码兼容子集，未承诺完整公共 API、强名称二进制替换或原版逐像素一致。JPEG 原版严格 1 LSB 检查与 CPU/GPU 整页画质差异仍需披露；不因正式改号把失败改判为通过。字体/复杂排版、未实现编码器、CMYK+ICC/HDR、PDF 子集和 macOS GPU 等既有限制仍明确报错。发布回归只允许已记录的精确 JPEG 差异；新差异和功能错误阻止发布。

每个包包含本 CHANGELOG、README、NuGet 发行说明和许可证。十二包统一 0.1.0；桌面安装 SimdSkia.Avalonia.Desktop 并调用 UseSimdSkiaDesktop，GPU/PDF 按需安装同版本包。实际 CI 和公开下载状态见[发布记录](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)。


## 0.1.0-preview.10 — 2026-11-01

发布补记：**十二包已公开上架并下载核验**；三平台托管、桌面 JIT/AOT/实际包消费、Windows AOT 及本版发布验证全部成功。严格 JPEG 与整页质量失败没有改判；完整链接及证据见[发布记录](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)。此补记不改写冻结的已发布包内容或历史测量。

- 将源码目标框架迁移至 .NET 10 / C# 14，完整引用精确版本 `Sdcb.SimdCodecs 1.2.0`（15 个同版本包），移除生产 `JpegLibrary` 引用和自有 PNG/JPEG 像素编解码、GIF LZW/合成；输入校验、ICC/gamma→sRGB、Alpha/stride、GIF 元数据与生命周期合同留在适配层。
- 用户已确认允许 .NET 自带原生压缩。该例外用于 PNG/TIFF 等压缩路径，不引入 Skia/libpng/libjpeg 等第三方原生绘图库。
- 新增 BMP/ICO/GIF 编码、TIFF/QOI/Netpbm 品牌 API 编码，接入 WebP/动画、多页 TIFF、JPEG2000/JBIG2 等受支持解码；缺失的 WebP 编码、无法映射到基线 SKEncodedImageFormat 的格式及不支持的 HDR/CMYK ICC 路径明确报错。
- 接回 Display-P3 转换；增加 PNG 过滤器、调色板索引及精确膨胀长度验证，避免新库较宽松的输入路径降低原有检查。保持原版质量阈值，JPEG 24 组回读中仍有 3 组最大通道差 2（阈值 1），未改判为成功。
- 新增集成回归、独立 Pillow/OpenJPEG/libwebp 夹具、AOT 探针和切换前后性能报告。先前 `0.1.0-codecs-trial.1` 的本地验证现纳入 preview.10，十二包统一发布；旧 preview.9 包/标签不改写。**升级需要 .NET 10，不能直接供 .NET 8 应用使用。**

### 本版测试与性能记录

同机 .NET 10.0.13 JIT / Windows x64 / i7-8700K / GTX 1050 Ti，共 **123 个独立计时进程**，三轮中位数。固定 SkiaSharp 3.119.4、Avalonia 12.1.4、中文字体和尺寸；GPU 包含完成等待和回读，不是 Native AOT 或显示器 FPS。

| 项目 | 原版 | preview.10 源码 |
| --- | ---: | ---: |
| CPU 控件页 100% | 0.891 ms/帧 | 0.549 ms/帧 |
| CPU 控件页 150% | 1.701 ms/帧 | 1.353 ms/帧 |
| CPU 滚动 | 0.372 ms/帧 | 0.348 ms/帧 |
| 控件页 WS / Private | 90.39 / 47.36 MiB | 109.81 / 71.08 MiB |
| Vulkan / Direct2D 综合耗时比 | 各例较快原版 = 1 | 0.678 / 0.624 |
| JPEG 解码 | 基线 | 本例快约 2.27–2.90 倍 |

JPEG 编码本例快约 4.09–5.93 倍，但 UI 文件大约增加 52%；不能当成相同码率压缩效率的胜出。纹理 PNG 解码耗时为原版 **2.03 倍**、UI PNG 编码慢约 **19%**；CPU 工作集仍高约 **18%–22%**。这些慢项和内存代价没有排除。

- 本地完整接入：编解码集成 1,416、标量 1,416、真实 Native AOT 1,417、精确优化 471,569、真实控件 9,750 项通过；独立解码夹具 41/41、JPEG 门槛前图片/损坏输入/压缩 369 项通过。实际十二个本地试用包验证、CPU/Direct2D/Vulkan 窗口均通过，GPU 冒烟不冒充性能数据。
- 原版 JPEG 联合回读 **21/24 通过、3/24 失败**，最大差 2，门槛 1 不变。默认 Imaging 测试继续严格失败；发布/多平台 CI 将功能集和严格 JPEG 对照分开，JSON 保留 `qualityPassed=false`，只为这三个已披露身份和已观察到的 RGB 差异启用 preview.10 发布例外，其余差异阻止发布。
- 原版整页画质 **CPU 0/3、GPU 0/12**，max≤32 且 RMSE≤2 的门槛未放宽。组合字符、字体、API、HDR/CMYK+ICC 与通用 PDF 等既有缺口未因性能较快而解决。
- 此处是版本改号前、相同绘图实现的源码实测；发布验证与三平台 CI 结果另列[发布记录](NUGET-PREVIEW.md)，未将待运行作业写成通过。

详见[带图原版对照报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/SKIA-COMPARISON-2026-11-01.md)、[完整接入和测试记录](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/SIMDCODECS-INTEGRATION-2026-11-01.md)。十二包均包含本 CHANGELOG、使用 README、NuGet 发行说明及许可证。桌面安装 `SimdSkia.Avalonia.Desktop 0.1.0-preview.10` 并调用 `.UseSimdSkiaDesktop()`；GPU/PDF 按需安装同版本包。

完整使用、测试数据、性能及剩余差距见[试用报告](SIMDCODECS-INTEGRATION-2026-11-01.md)。下面的 preview.9 及更早内容仍是历史发行说明。

## 0.1.0-preview.9 — 2026-10-27

本版补齐独立 SK* 调用链、GIF 和受限 PDF 支持，并修正真实 VelaShell 接入问题。新增可选 `SimdSkia.Pdf`，共 **12 个包**统一版本；Avalonia **12.1.4**、SkiaSharp 源码兼容基线 **3.119.4** 不变。公开上传和下载验证状态见[发布记录](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)。

### 新增与修复

- 真实 VelaShell 接入修正：字体 `TryCreateGlyphTypeface` 不支持合成时返回 false/null，按 Avalonia 合同保留真实近邻字面，避免设置窗口布局被异常中断；图片 Uniform 布局的微小 double 舍入尾差按 ULP 归一化，真实越界仍拒绝。9,750 控件回归通过。
- 新增 VelaShell 原版/CPU/Direct2D/Vulkan 页面及上游终端/视图测试工具和报告；常规场景可显示，组合字符、资源监视性能、部分输入、专属 Skia 预览和整页质量问题保留。不是完整兼容版本。

- `SKBitmap.GetPixels()` 提供懒固定的真实像素指针，支持 stride、逐行复制、GC 稳定地址和释放/重分配失效规则。
- 补充系统 TrueType/TTC 字体族及样式发现、`SKTypeface.FromFamilyName`、`SKFontStyle`、几何值类型和 `SKRect.Inflate`。
- 新增纯 C# GIF LZW、交错扫描、透明度、帧延迟/循环、背景/前帧处置和随机回读，接入 `SKCodec`；复用解码索引缓冲。
- 新增 `SKRoundRect`、圆角绘制/裁剪、路径/差集裁剪、`SaveLayer` 整组透明度与嵌套恢复，以及 `DrawImage` 采样重载。`Src/Clear` 图层的边界提示规则按原版验证。
- 新增 `SKDocument.CreatePdf` 多页**栅格**导出，不承诺矢量、可搜索文字或 PDF/A；可选 `SimdSkia.Pdf` 使用纯托管 PdfPig 0.1.16 解析已有 PDF，再由本引擎渲染。PDF 存在明确支持范围，不是通用 PDF 阅读器替代品。
- 新增 Documents 和 PdfViewer 示例、独立原版调用/像素对照、PDF 外部夹具与 Native AOT 检查。API 缺口、原版画质失败及性能适用范围见[专项报告](STANDALONE-COMPATIBILITY-2026-10-27.md)。

### 验证、性能范围与已知限制

- 整仓 Release 构建 0 警告、0 错误；真实 Avalonia 控件回归 **9,750 项**、系统字体 **13 项**通过。独立兼容调用链、PDF 及 Native AOT 结果见[专项报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/STANDALONE-COMPATIBILITY-2026-10-27.md)。
- 固定基线匹配公共条目从 preview.8 的 **425/3235** 增至 **892/3235**，仍缺 **2343** 项；这不是完整实现数量或二进制兼容承诺。
- VelaShell 常规窗口 CPU / Direct2D / Vulkan 各 **23/23**、终端测试 **9/9**；深层视图复测原版 **514/514**，本实现 **393 通过、7 失败、114 未完成**。组合字符 U+0301 仍会异常，资源监视页存在超时，部分焦点/标题栏/Skia 专属预览不兼容；目标应用仍直接加载原版 Skia 原生库，不能宣称整应用替换完成。完整日志见[VelaShell 报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/VELASHELL-COMPATIBILITY-2026-10-27.md)。
- 新增独立像素对照 **11/14** 通过；圆角 AA、圆角硬裁剪、微软雅黑文字三组尚未通过，整页一致性仍未通过，没有放宽门槛。
- 只重测 1080p 带 stride 的逐行像素复制：本实现 **0.5907 ms/帧**、原版 **0.5921 ms/帧**，视为基本持平；不是视频解码或整个应用的性能结论。**本版没有重测完整 Avalonia CPU/GPU 吞吐和常驻内存，不将下列 preview.8 的历史成绩当作新版测量。**
- PDF 导出为栅格页面，无矢量、可搜索文字或 PDF/A。PDF 阅读器遇到超出支持范围的可见内容明确报错，不能替代通用 PDF 阅读器。新 PDF 阅读/查看器本地验证平台为 Windows，其他平台执行由本轮 CI 单独记录。

### 升级

将所有直接引用的 SimdSkia 包统一为 `0.1.0-preview.9`。桌面安装 `SimdSkia.Avalonia.Desktop` 并使用 `.UseSimdSkiaDesktop()`；避免再用宿主选项覆盖 framebuffer 模式。GPU 另加同版本 `SimdSkia.Avalonia.Gpu`。PDF 导出位于 `SimdSkia.Compatibility`；读取已有 PDF 另安装 **`SimdSkia.Pdf`**，普通 Avalonia 用户不引入 PdfPig 依赖。

每个包提供本文件的 `CHANGELOG.md`、对应 README、NuGet 发行说明和许可证。下面旧版的内容、成绩和限制保持历史版本标注。

## 0.1.0-preview.8 — 2026-10-27

本版集中优化通用 x64 Native AOT 的真实 Avalonia 控件绘制，并补充原版 Vulkan / ANGLE 与 SimdSkia Vulkan / Direct2D 的完整帧对照。11 个包统一版本，Avalonia **12.1.4**、SkiaSharp 源码兼容基线 **3.119.4** 不变。

### 实现与分配优化

- 增加 SSE2 字形覆盖率、预乘图像合成和双线性采样路径，维持精确整数除法、浮点舍入、Alpha、stride 和尾部语义；保留 AVX2 及完整标量回退。
- 连续白色/透明缓冲、整宽矩形与 16–256 KiB 通用 x64 色块优化填充；黑色字形合成检查真实目标像素后走等价路径。
- 字形串缓存使用不可变身份快速查询、有界第二次机会淘汰，减少大键复制与 GC 引用写屏障；串预算 2→4 MiB，字形预算保持 8 MiB。缓存预算不是进程内存上限。
- 无实际透明图层时减少脏区记账；纯平移字形省去未使用的矩阵求逆。紧密 framebuffer 整块复制，带 padding 的缓冲保留逐行路径。
- 画布中的路径、矩形与渐变缓存容器按需创建。60 FPS 滚动的托管分配由 preview.7 的 **155.14 降到 28.05 KiB/帧**；原版为 **12.87 KiB/帧**。

### 性能实测与边界

同机 Windows x64 / i7-8700K / GTX 1050 Ti，固定 Fluent 控件树及中英文字体。CPU 共 75 个串行进程（各 5 次，预热 2 秒、采样 5 秒），GPU 共 36 个（各 3 次，预热 3 秒、采样 5 秒）。生产绘图实现对应报告中冻结的候选源码，发布只更新版本、依赖锁定和文档。

| 项目 | 结果 |
| --- | --- |
| 通用 x64 AOT CPU 吞吐 | 控件 100% / 150% DPI 与滚动列表综合耗时比原版低 **4.4%**，最慢场景落后 **3.8%** |
| CPU 含 60 FPS 的全部五场景 | 综合耗时比原版低 **6.7%**；各场景相对 preview.7 耗时减少 **50.2%–62.7%** |
| Vulkan / Direct2D | JIT 离屏完成帧含等待与回读，综合耗时比每例较快原版 GPU 低 **29.8% / 35.3%** |
| 工作集 | 普通控件页 **79.89 MiB**，原版 **60.91 MiB**；未追平 |

综合比取场景耗时比的几何平均数。GPU 不代表 AOT、真实窗口呈现或显示器帧率；不能将此结果推广到其他硬件、任意业务界面或所有图形操作。CPU、内存、分配及未通过项见[完整报告](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/PARITY-OPTIMIZATION-2026-10-27.md)。

### 验证与已知问题

- 471,569 项精确检查通过（含字体）；SSE2 限制、完全标量各 471,557 项。真实 Avalonia 9,741、Direct2D 344、Vulkan 内核 540、GPU 集成 12,174 项通过。
- 真实通用 x64 Native AOT 460,050 项采样检查通过，并从外部验证 PE 与进程模块。发布构建及三平台 JIT/AOT/nupkg 消费结果见[发布记录](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)。
- CPU 页面优化前后逐字节相同，但原版整页画质通过数仍为 **CPU 0/5、GPU 0/12**，完整画质未达标。字体指令处理和字形边缘覆盖率仍有差异，没有降低画质门槛或把失败改判为成功。
- 常驻内存仍高于原版，完整 SK* API 仅匹配 425/3235 公共条目；不承诺二进制兼容或任意 Avalonia 应用直接替换。macOS GPU 未实现，Vulkan 窗口路径仍有回读。
- 本次是在公开这些限制后发布的**实验性预览版**，不代表完整兼容验收通过。完整验收脚本保留失败状态，不能把 NuGet 发布流水线成功等同于画质全绿。

### 升级与示例

所有直接引用的 SimdSkia 包统一为 `0.1.0-preview.8`。桌面通常安装 `SimdSkia.Avalonia.Desktop` 并调用 `.UseSimdSkiaDesktop()`；需要 GPU 时另加同版本 `SimdSkia.Avalonia.Gpu`，配置方法见包内 README。无需重新编译 Avalonia。

新增默认 Avalonia AOT 体积示例、CPU/GPU 对照工具、独立字体轮廓诊断与带图的博客园文章。默认空白模板 **36.53→19.51 MiB** 是明确标注的 **preview.7 历史实测**，未冒充 preview.8 的体积测量；旧版本样例及数据保持可复现。

## 0.1.0-preview.7 — 2026-10-26

相对于 preview.6，新增以下实现与修复。11 个包统一版本；Avalonia 仍锁定 **12.1.4**，SkiaSharp 源码兼容基线仍为 **3.119.4**。本文件随每个 NuGet 包以 `CHANGELOG.md` 提供，NuGet 页面另有发行说明和包内 README。

### CPU 图片采样优化

- 新增每批八像素的 AVX2 双线性采样：连续目标写入、源行有界 gather，保留浮点操作顺序、舍入及预乘 Alpha 约束。
- 支持已实现的 RGBA/BGRA、非预乘/预乘/Opaque 输入；向量尾部继续走 SSE 或标量。没有 AVX2 的硬件仍可运行；通用 AOT 不保证启用 AVX2。
- 不增加公共 API，不需要调用方改变绘图代码。绘图、字体、编解码仍为 C#，原版 SkiaSharp 仅用于隔离的对照测试。

### 画质与 Vulkan 修复

- 修复非预乘、预乘半透明图片的 SourceOver 舍入，匹配固定基线 Windows x64 图像流水线。此前两类 RMSE 超标现已通过，未放宽质量阈值。
- 统一现代最近邻、线性过滤、原尺寸图像拷贝及裁剪图层回贴，修复高 DPI 图层边缘不一致。
- 同步 Vulkan 图像透明合成，并修复 CoverageClip 存在时的采样路径选择；已更新 SPIR-V。普通颜色、渐变和 Mitchell 路径保持各自算法。

### 实测结果与适用范围

同机 i7-8700K / Windows x64 / .NET 8.0.32，固定 SkiaSharp 3.119.4；512×384 源图缩放到 1024×768，包含清屏与线性过滤，各三进程中位数：

| 图片场景 | 本版 ms | 原版 ms |
| --- | ---: | ---: |
| 非预乘不透明 | 6.667 | 7.714 |
| 非预乘含透明 | 6.758 | 7.617 |
| 预乘含透明 | 5.412 | 7.404 |
| Opaque 元数据 | 5.816 | 6.804 |

这是特定 CPU 图片微基准，不能推广为所有 Avalonia 页面、硬件或 GPU 性能。核心图片对照 **24/24** 通过原门槛（最大通道误差 ≤2、RMSE ≤0.5）；SIMD 与标量逐字节一致。半透明场景最大误差降到 1，RMSE 约 0.05–0.11。

前一轮 AVX2 优化的独立测试中，1024×768 非预乘不透明图片由 46.317 降至 7.423 ms；该轮真实 Avalonia 页面耗时改善约 3%–5%。这组历史数据与上表分开，不拼接不同轮次计算收益。**本轮没有重测完整 Avalonia 稳态吞吐或 GPU 性能，常驻托管内存没有明显改善。** 原版微基准保留公共 fixture 开销，其进程内存不用于引擎排名。

### 验证与示例

- 468,137 项优化回归通过；关闭 AVX2、关闭全部硬件指令同样通过。其中独立原版样本有 12,288 个组合，跨格式/采样/尾部累计回放 393,216 次。
- 9,734 项 Avalonia 控件检查、344 项 Direct2D 检查、590 项 GPU 采样、540 项 GPU 内核、599 项 GPU 文字链检查通过。
- 通用与 x86-64-v3 实际 Native AOT 各通过 460,050 项采样检查；外部核对未加载 CLR/JIT。
- 新增源码示例 `examples/NativeWindow` 和 `examples/NativeControls`：不依赖 Avalonia 的 Win32 自绘窗口、发光文本框/按钮和动画进度条。示例程序、字体不捆绑进库包；历史 AOT 体积报告注明测量时间与配置。

### 升级及已知限制

将所有直接引用的 SimdSkia 包统一为 `0.1.0-preview.7`，Avalonia 统一保持 `12.1.4`。通常桌面应用只需引用 `SimdSkia.Avalonia.Desktop`，可选另加同版本 `SimdSkia.Avalonia.Gpu`；接入代码见包内 README。

完整公共 API、字体/路径/整页画质仍有缺口；完整原版对照验证仍返回质量失败状态，不能作为原版完全替代或二进制兼容承诺。原版像素样本固定 Windows x64，未承诺所有平台逐像素等价。macOS GPU/Metal 尚未实现；Vulkan 仍需系统 loader/驱动并有 framebuffer 回读。系统字体支持范围及 macOS 窗口宿主原生例外保持原说明。

详细报告：[最终画质及性能复测](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/ALPHA-COMPATIBILITY-2026-10-26.md)、[AVX2 优化历史对照](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/CORE-PERFORMANCE-2026-10-26.md)、[验证与差距](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/VALIDATION.md)。公开发布状态以[发布记录](https://github.com/donet-org-cn/SimdSkia/blob/main/docs/NUGET-PREVIEW.md)为准。

## 0.1.0-preview.6 — 2026-10-22

系统字体分页、共享 TTC、光栅缓冲复用、图片画刷缓存生命周期修复、几何边界/字形缓冲复用，以及经典桌面 JIT 后台启动准备。缩短启动白色阶段但未彻底消除；不宣称该轮持续 GPU 性能或常驻内存明显改善。此前发行历史见仓库发布记录。
