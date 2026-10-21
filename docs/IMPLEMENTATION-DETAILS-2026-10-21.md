# 本轮实现与明确边界

固定 Avalonia 12.1.4 / SkiaSharp 3.119.4。实现与本轮验收见 [测试报告](TEST-REPORT-2026-10-21.md) 和 [性能报告](PERFORMANCE-FINAL-2026-10-21.md)。这份交付仍不是完整 Avalonia/SkiaSharp 替代品。

## 画刷、效果与层

`ImageBrush`、`VisualBrush` 现在实际绘制源图或视觉内容，处理 SourceRect、DestinationRect、Stretch、对齐、Tile/Flip、DPI、画刷变换、透明度与源版本失效。图片中间瓦片缓存最多 64 项/16 MiB，租约在绘制后释放。VisualBrush 内容递归限 32；超限报错。固定 Avalonia 内部图片引用通过缓存的接口 getter 读取，未伪造 native handle。

固定版本的 `SKPicture.ToShader` 默认最近邻：[原版绑定](https://github.com/mono/SkiaSharp/blob/v3.119.4/binding/SkiaSharp/SKPicture.cs)。视觉瓦片因此采用最近邻，普通 ImageBrush 按 RenderOptions 选择过滤。高 DPI、旋转/缩放视觉图案仍存在原版差异，不能视为已通过兼容验收。

Blur、DropShadow、内外 BoxShadow 有实际 PMA 栅格算法。Avalonia radius 按其固定源码转换为 `radius * 0.288675 + 0.5`，零半径不模糊。高斯核截断于 ceil(3σ)，固定权重和为 16384，以对称余数分配避免高 σ 出现负权重；透明边界不重新归一化。先建立标量及独立 double 二维卷积预期，再实现 AVX2 的 8 通道整数运算。未支持硬件继续执行相同标量公式。设备 σ 上限 256，相关 shear 的效果显式拒绝。

离屏层按有效恢复 clip 分配，mask 另受其实际矩形限制；滤镜扩展 halo 和偏移反向所需的输入范围。层 bounds 只作提示，不擅自裁去提示外的合法内容。局部原点参与嵌套变换、字形、mask 和 box shadow；测试将裁剪小层与全尺寸中间层再裁剪逐字节比较。透明度为 1 的普通 opacity 组继续复用父画布。

## 图片与颜色

PNG：原有 1/2/4/8 位灰度/调色板、8 位 RGB/GA/RGBA 加上 16 位灰度/RGB/GA/RGBA 与 Adam7 七次散布。仍验证 CRC、Adler、DEFLATE 长度、过滤与 chunk 顺序。16 位输入输出 8 位缓冲，取高 8 位；tRNS 比较原始完整样本。输出/膨胀预算各 256 MiB。

JPEG：纯 C# JpegLibrary 0.4.32；8 位基线与渐进 gray/YCbCr/RGB、基线 CMYK，中心对齐的色度上采样。编码为 4:4:4，quality 0..100。原版隔离探针验证默认编码消费物理 RGB：PMA 缓冲保留黑色底色，Straight/Opaque 存储不再乘物理 A；24 份固定原版夹具覆盖 Erase 和直接写字节。四分量渐进 JPEG 明确拒绝，不能返回经验证会错误的颜色。YCCK 颜色解释已写入，但尚缺独立 YCCK 夹具，未声明验收。12 位、无损/算术等变体未列入本轮支持承诺。

iCCP/APP2 的 ICC 输入经有界解压和顺序组装进入托管颜色变换：RGB matrix/TRC、灰度 TRC、lut8/lut16 A2B、XYZ/Lab PCS、D50→D65→sRGB。支持 curv 和 para0..4；未知 multi-stage mAB/mBA 明确拒绝。RGB matrix/TRC 的 sRGB、Display P3 输入有独立 lcms 夹具；lut8/lut16/CMYK profile 分支尚无完整外部验收，不能将有代码等同于全部 ICC 兼容。[ICC 规范](https://www.color.org/ICC_Minor_Revision_for_Web.pdf) 规定 LUT 矩阵只用于 XYZ 输入；本实现支持的设备 RGB/GRAY/CMYK 要求该矩阵为 identity。无 ICC 的非 sRGB cHRM、APNG、WebP/GIF/BMP 等仍显式拒绝。

`SKBitmap.Decode/Encode`、`SKImage.Encode` 与 Avalonia 位图加载/保存现接入 PNG/JPEG。解码归一为 8 位 sRGB，尚不保留任意源 profile 供完整 SKColorSpace 查询，这也属于行为兼容缺口。

PNG 编码现在实现纯 C# LZ77 + fixed Huffman DEFLATE，32 KiB 滑动窗口，三档有界 hash-chain 搜索；NoCompression 使用 stored，Fastest/Optimal/SmallestSize 分别搜索 4/32/256 个候选。压缩不能获益时回退到更短的 stored 输出，所有级别无损。测试以独立 Python/zlib 进程验证 33 个流；引擎和正常 C# 测试不调用 native zlib。枚举 compression level 的各选项会实际执行对应策略。

## 字体集合

TTC v1/v2 支持全部已注册 glyf face 的选择、表范围检查与独立 sfnt 提取。抽取时重定位绝对表偏移，重算各表 checksum 及 head.checksumAdjustment；流不再依赖原 collection 的生命周期。`SKTypeface.FromFile/FromStream(index)` 已使用真实索引；Avalonia FontFiles 注册 collection 的全部 face。实现依据 [OpenType 文件规范](https://learn.microsoft.com/en-us/typography/opentype/spec/otff)。字体数字签名不作信任验证。

基本中英 LTR、Unicode cmap4/12、默认 Latin/DFLT kern/GPOS pair 行为保持。CFF、变量/彩色字体、hint VM、LCD、RTL、mark、复杂脚本与通用 GSUB 仍未实现；合成 bold/oblique 仍拒绝。TTC 并不补齐这些排版功能。

## Vulkan

新增 GPU shader 中的源矩形裁切、nearest/bilinear/Mitchell PMA 采样、8 位覆盖裁剪和可选曲线容差；覆盖数据在提交时复制到只读 SSBO，GPU 执行过滤和合成。纹理版本捕获、批量提交、fence 等待、设备失效和创建线程限制保持。新增镜像图片半像素边界修复。非线性曲线的细分/边索引仍在 CPU，覆盖率与像素合成在 GPU。

这仍是离屏 Vulkan compute 后端：没有 Avalonia Vulkan 窗口/swapchain、GPU 字形 mask、GPU 图像/视觉画刷和效果组。未知 glyph-run 命令明确拒绝。CPU 控件帧率不能用于声称 Vulkan 窗口更快。
