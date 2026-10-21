# 架构

新增另一入口：`Avalonia Controls/Layout -> SimdSkia.Avalonia -> Core/Cpu/Text/Imaging`。它实现 Avalonia 12.1.4 的平台渲染/字体/排版服务，调用自有 CPU 后端，不经过 Avalonia.Skia 或 SkiaSharp 兼容层。窗口由托管 Win32 宿主调用系统 API；适配层保存托管 framebuffer，绘制上下文释放时拷贝可见行并解锁。DPI、离屏层 Blit、窗口 resize 和资源持有有实际窗口/无头合成器断言。Avalonia GPU context 请求明确拒绝。

适配层用 Clipper2 的托管实现计算布尔几何和描边轮廓；字体仍由自有 TrueType 解析器读取，GPOS pair1/2、extension 及 legacy kern0 用 C# 解释，非支持 lookup 明确报错。注册字体以 SHA-256 匹配内容共享只读数据，流/锁持有独立生命周期。控件层不能把未实现 effect 静默忽略，故后端显式实现效果接口并拒绝。

命令增加 AA `CoverageClip`、源图片矩形、采样方法、曲线容差和不可变 `IColorShader`。CPU 实现真实覆盖率相乘、PMA 空间过滤、轴向采样和 SIMD SrcOver。Vulkan 暂时拒绝这些新增状态，不能据 CPU 支持推断 GPU 支持。裁剪全局 LRU 最多 64 项/8 MiB，文字/形状归一化 coverage 缓存沿用最多 512 项/16 MiB，key 包含曲线容差；绘制状态和像素不进入形状 key。分组像素来自已清零的有界像素池，跟踪实际改动范围并按组合成；并非逐 draw 乘不透明度。

具体接口及版本约束见 [真实 Avalonia 接入](AVALONIA-INTEGRATION.md)。以下仍描述原 SK* 入口，其 API 覆盖率单独维护。

续轮 Avalonia 文字改为不可变 `GlyphRunGeometry` / `DrawGlyphRun` 命令。CPU 原始字形 mask LRU 最多 4096 项/8 MiB，带亚像素相位、baseline 策略及可选 sRGB 预混合 luminance 的行 LRU 最多 512 项/2 MiB；预算保守计入被 key 持有的轮廓，不是实际进程内存上限。连续且像素不重叠的字形打包合成，重叠则按输入顺序分批，避免改变透明合成。矩形硬 clip 采用已核对原版的设备坐标舍入；16×16 几何 AA 不变。Vulkan 明确拒绝新字形命令，详见 [文字/首绘报告](AVALONIA-TEXT-CACHE.md)。

    SkiaSharp.SK* / SimdSkia.VulkanSurface / TextRendering
                         |
    SimdSkia.Compatibility：验证、状态、快照、所有权
                         |
    SimdSkia.Core：矩阵、路径、Paint、DrawingCommand、ICanvasBackend
              /                             \
    SimdSkia.Cpu                         SimdSkia.Gpu
    标量覆盖/Alpha/Vector<T>             Vulkan compute / GLSL / SPIR-V
              \                             /
                    PixelBuffer / 完成帧读取

    SimdSkia.Text -> Core：托管 TrueType/字形/基本布局
    SimdSkia.Imaging -> Core：托管 PNG/DEFLATE/颜色转换

六个生产程序集没有第三方包。Core/Cpu/Text/Imaging 不调用 native 绘图、字体或编解码；Gpu 仅调用系统 Vulkan loader/驱动，没有 CUDA/Skia。公共绘图命令不携带 GPU 句柄；Vulkan 创建使用 SimdSkia 品牌，不假冒 GRContext。

定位文字沿 SKTextBlobBuilder → 不可变几何 run → DrawingCommand 进入同一后端。每个 TrueTypeFont 通过 ConditionalWeakTable 关联 64 项/4 MiB 的定位几何 LRU，完整比较 glyph/position/字体矩阵，缓存键复制可写输入；不绑定字体包装对象寿命，字形轮廓字典加锁。blob 构建后独立于 builder/font，保留真实几何和 AA 选项。

CPU 对定位 blob 的覆盖率使用局部 16×16 采样网格，分离整数设备平移；跨 canvas 的全局 LRU 最多 512 项/16 MiB 估算数据，clip、颜色、像素格式不进入 coverage key，合成时再裁剪。第二次使用才保留，避免一次性对象占满缓存；超范围、极端坐标或过大目标回退现有完整光栅路径。缓存保留几何至淘汰，Dispose 不保证立即减少工作集。该新路径未改变旧 DrawText(string) 的采样坐标；与原版灰度仍有差异。

轴对齐、无 AA、非别名图片直接按采样中心计算行和 X 索引，避免为矩形生成路径 mask；连续且相同格式、opaque 源、满 paint Alpha、Src/SrcOver 时按行拷贝，再强制物理 A=255，保留 opaque 元数据语义。其余 Alpha、变换、AA、alias 情况继续完整采样合成。当前公开图片入口为 DrawBitmap；Avalonia 实际 DrawImage/SKSamplingOptions 仍需补齐。

路径快照不可变。曲线按设备容差细分，CPU 按扫描线交点及 winding/even-odd 求覆盖率；AA 为 16×16。ArrayPool 复用活动边、交点、行与区间差分计数，mask 按覆盖包围盒裁剪并编译为非零覆盖率 spans。轴对齐 AA 矩形直接求与 16×16 网格一致的覆盖率，浮点高坐标精度不足时走通用算法。细分/初次覆盖率仍有分配，暖缓存命中可避免重新生成；动态场景另有持续 miss 基准。butt/miter 描边生成轮廓并共用填充。PixelBuffer 是连续 byte[]，支持 padding，最后一行 ByteCount 保持核查的基线语义。

bitmap/canvas 共享数组、各自释放视图，snapshot/copy 独立存储。PNG 解码输出使用可共享的 PixelStorage 所有权，最后一个视图 Dispose 后才可回到像素池。空闲池为进程全局、最多 4 块/32 MiB，有锁保护；活动图像、ArrayPool 临时区和 GPU 存储另计。只有会写入全部可见字节的内部解码工厂能租用未初始化数组；公共新建位图仍初始化为零。Span 的有效期受所属存储生命周期约束，像素访问仍按单线程约定。paint 保存 shader 的不可变数据，shader 包装对象释放不影响已有画笔。

字体直接读取 SFNT/glyf/cmap/metrics，托管轮廓进入同一绘制管线；当前选择无 hint、subpixel 定位。基本中英文 LTR 支持度量、对齐、换行/自动折行；复杂脚本/mark/Bidi、可变/彩色字体等拒绝。TextRendering.DrawTextBlock 返回完整布局并按 bounds 裁剪，调用方检查高度溢出。

PNG 的过滤、CRC、zlib/DEFLATE、back-reference 和 Adler 都是 C#；产品不调用 System.IO.Compression/native zlib。编码是 stored 块，解码支持三类块；处理透明度和 gAMA 到 sRGB，ICC/不支持的色度明确拒绝。Opaque 图像按元数据采样/编码，GetPixel 保留基线物理字节语义。



CPU 几何缓存均限当前画布/字体：RasterCanvas 覆盖率 LRU 最多 64 项/16 MiB 估算数据，水平不透明渐变行最多 16 项/4 MiB；SKCanvas 描边最多 64 项/4 MiB；SKFont 布局最多 64 项/4 MiB。mask/轮廓 key 包含不可变几何、变换、AA/clip，布局含位置/对齐并随字号/scale/skew 变化失效。预算计数据和估算几何，不含字典/节点/对象头。小矩形几何表亦有项数上限；Dispose 清空这些缓存。全局空闲像素池和 BCL ArrayPool 独立于几何缓存。图像只缓存几何/复用 X 索引，像素每次读取；别名绘制保留即时顺序。

扫描线按前一次交点顺序做插入排序，移动次数超预算改用 O(n log n) 排序，避免杂乱输入退化。低复杂度路径以差分事件直接产生连续覆盖区间，不构建大面积 mask；密集文字保存紧凑覆盖率数组，避免比像素还大的逐段元数据。两种表示使用原有浮点求交表达式和 16×16 网格。

PNG 使用纯 C# slicing-by-8 CRC、分块 Adler、9-bit Huffman 前缀表与 ref struct 位缓冲；重叠回溯先扩展已解码前缀再分段复制。过滤器分支移出字节循环，gamma 转换用 256 项预计算表，inflate 暂存由 ArrayPool 复用、在异常路径也归还。校验、尺寸上限和不支持格式拒绝保持有效，编码仍用 stored 块。

位缓冲按 32 位补入；距离为一的回溯直接填充。单 IDAT 直接读输入，多 IDAT 按已校验的长度聚合到复用缓冲。SSE2/SSSE3 执行字节预测和 RGB→RGBA，AVX2 加速 Adler。所有方法均有标量路径，透明度/gamma 情况仍执行对应转换。

## SIMD

测量热点后增加 uint 行填充与 Vector<T> 预乘 Alpha 合成。常量颜色/覆盖率按连续 spans 处理，源预乘和格式索引移到像素循环外；Vector<byte> 展宽 ushort 后乘 Alpha，除 255 保留原整数舍入并做饱和，标量尾部和 Straight Alpha 换算不变。CpuOptions.EnableSimd 同时控制这些 CPU 显式内核；标准内存拷贝及运行时 JIT 优化仍可使用硬件。硬件可用性由 Vector.IsHardwareAccelerated/对应 IsSupported 决定，尾部标量处理、padding 不写；禁用指令仍正确。

密集覆盖率增加 AVX2 可变 Alpha 合成；CpuOptions.EnableSimd 控制 CPU 内核，PNG 独立检查指令集。Unsafe.ReadUnaligned 和 Vector.LoadUnsafe 均位于明确的行长度/块长度检查之后，尾部由标量处理；DOTNET_EnableHWIntrinsic=0 的全回退测试覆盖相同 fixture。

## Vulkan

按用户选择仅开发 Vulkan：64 位小端 Windows/Linux，Vulkan 1.0 compute。CPU 负责几何细分、描边及保守空间索引；覆盖率、渐变、采样与预乘合成在 shader 执行。Gpu 不引用 Cpu，不上传 CPU 光栅 mask。

离屏目标是优先 DEVICE_LOCAL 的 GPU storage buffer，另有优先 HOST_CACHED 的 host-visible/coherent staging 回读缓冲；缺少首选属性时选择兼容类型。首次在设备清零，compute→transfer copy→host read 的屏障和 fence 完成后才读回。图像纹理是 VkImage/view/sampler，staging 上传有 layout/access barriers；内容快照捕获图像版本，避免 Span 修改污染已记录命令。纹理缓存限 32 个/64 MiB，释放必须等有关批次完成；单纹理超过预算明确拒绝。目标像素暂限 128 MiB/单维 32768，此限制不含 staging、管理像素副本及其他资源。

GPU 路径按闭合轮廓包围盒和边的 Y 区间建立 8×8 候选边索引。索引只做保守剔除，覆盖率仍在 GPU 求值；空闲画布外的轮廓不集中到边缘瓦片。边索引单项限 16 MiB、几何缓存合计仍为 64 项/16 MiB 估算，超限或极端坐标保留完整扫描。AA 对每条子扫描线求交一次，用四组互不溢出的偏置字节计数器处理 16 个 X 样本；候选边达到 128 则退回无限制整数 winding/采样。轴对齐矩形分离 X/Y 样本计数。测试内的原始采样核与优化核逐字节比较。

批次最多 128 命令，pipeline/command pool/descriptor pool 复用；命令间有 shader barrier，命令参数用栈缓冲准备并写入复用 arena。Flush 包含 GPU 完成及 staging 回读，分别记录提交/完成时间。调用限定创建线程，Dispose 等在途任务后按依赖释放资源。

device lost/提交错误进入 faulted，不能取得“成功新帧”；超时不能释放在途存储，错误清理先等待/终结 device。实际 device loss 尚未注入验证，无自动恢复承诺，宿主重建 surface。

unsafe 仅用于 GPU ABI/pointer 互操作，关键结构大小初始化检查，对照 Vulkan-Headers v1.3.290。GLSL/SPIR-V 同时提供，shader 编译器不是产品依赖。
