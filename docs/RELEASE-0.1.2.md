# SimdSkia 0.1.2 正式版：从0.1.1起的累计更新


本版汇集0.1.1之后的开发与0.1.2预览批次，十二包统一发布0.1.2，继续要求.NET 10。本次正式化不更换preview.3的绘图算法；正式版范围是文档列出的已实现能力，严格画质、完整行为及二进制兼容仍有明确差距。

### 公共API与行为

- 从NuGet.org实际下载0.1.1程序集，用同一工具和固定SkiaSharp 3.119.4基线核对：匹配892/3235→3230/3235，累计新增**2338项公共元数据签名**，公开类型63→201，缺口2343→5，额外签名仍0；数量含类型、字段、枚举、属性和重载，不等于2338个独立功能。逐项见[累计清单](api/changes-0.1.1-to-0.1.2.json)。
- 扩展SKColor/SKColorF、矩形/矩阵、路径查询/变换/测量/布尔运算、相对/圆锥/圆弧命令；完善描边端点/接头与GetFillPath、区域/渐变及绘图重载。修复隐式起点、Close后相对命令和连续MoveTo丢失点/空轮廓问题。
- 扩展SKPixmap/Bitmap/Image/Surface的像素视图、外部像素、读取、复制、子集、擦除和缩放，按格式/stride/预乘Alpha处理；ExtractAlpha现在使用真实1字节Alpha8，不附带整张RGBA工作面。补充延迟Picture图片、WebP编码选项与明确格式限制。
- 增加26种CPU混合模式以及组合/算术Blender、颜色矩阵/HSLA/查表/伽马/高对比度等滤镜、图片shader平铺/采样和颜色转换；浮点颜色、HDR输入、抖动进入实际绘图。扩展RuntimeEffect子语言、Perlin噪声、Vertices/Atlas/Coons Patch、Lattice、Picture/Drawable与画布视图，复杂状态仍按已实现子集报错。
- 完善SKFont/SKTypeface/SKTextBlob等文字/测量API及资源保留；补充BGRA水平LCD三通道和中文TrueType EBLC/EBDT嵌入点阵。字体切换、来源字体释放后保留视图和缓存生命周期有修复，仍不等同完整hinting/复杂脚本排版。
- 增加真实向量SVG、多页OPC/XPS与原生v103 SKP子集读写/双方交叉读取，扩展PDF链接/注释及文档生命周期。已有PDF读取/显示仍是可选SimdSkia.Pdf子集，不能解释为本版首次提供或完整PDF规范支持。
- 修复SaveLayer在Clear/Src/SrcIn/DstIn/SrcOut/DstATop/Modulate下错误裁剪bounds：按原版将提示范围扩展到父裁剪。新增464组/2,375,680字节原版精确比较；几何300组/4636值通过。

### GPU、速度与内存

- 在已有Vulkan/Direct2D基础上，补充GL/GLES、D3D12、Metal核心及SK*外部纹理/目标工厂；实现真实资源借用/接管、原点/布局、回调与完成读回。D3D12使用系统D3D11On12/Direct2D；部分外部表面仍有CPU桥接。Metal无Mac GPU实机验证，尚未接入Avalonia窗口呈现。
- Vulkan外部表面由CPU回传/再上传改为设备内复制，非重叠矩形至多16条合批dispatch，清屏使用设备填充，完成读回直接写调用方缓冲；移除提交中的LINQ、临时计时/排序分配。关闭/清理覆盖缓存会释放对应资源，保留正确标量/旧路径对照。
- 增加/优化连续行复制、SIMD通道交换、线性渐变行内核、Multiply AVX2、双线性采样及浮点画笔SIMD；复用采样表、Span/池缓冲、画刷/轮廓/字形覆盖和有界着色器行缓存。曲面重复临时分配及字体缓存抖动得到改善，未降低采样质量。
- GPU共享纹理由shader/Picture/尚未完成的GL draw持有至最后消费者/实际完成后回调，ToRasterImage生成独立CPU副本；补充GL创建线程/当前上下文/丢失上下文、pixel-store/PBO、预算清理与生命周期验证。GPU对象仍要求创建线程显式Dispose，GC终结回收合同未与原版等同。
- 更正原版D3D12对照夹具漏传Adapter导致的误判，完整设备描述符下重新验证10/10基础图和346/346互操作图，并补充同后端CPU时间/内存/完成帧性能；原始错误记录保留。
- 继承preview.3最终源码的63进程实测，**不是正式版新跑分，也不是与SimdSkia0.1.1直接计时对照**：相对较快原版SkiaSharp路径，CPU/Vulkan/Direct2D控件页综合耗时比为0.778/0.682/0.575；Direct2D对ANGLE是同GPU不同API。controls×1 CPU工作集原版91.00、本库110.75MiB；严格整页画质仍0/18，不能宣传为同画质整体领先。部分Vulkan/D3D12微场景、RuntimeEffect/网格/WebP仍慢于原版。[完整速度/CPU/内存与图](PERFORMANCE-PREVIEW3-2026-11-07.md)、[全部测试和限制](PUBLIC-API-FINAL-2026-11-07.md)。

### Avalonia、依赖、示例与发行

- 延续0.1.1的Avalonia >=12.1.0且<13.0.0范围与12.1.0–12.1.5匹配宿主选择，保留三平台真实窗口、同一适配器二进制与最低/最新Native AOT矩阵。未知未来12.x仍需宿主适配/回归；macOS当前Avalonia绘图使用CPU，窗口使用已允许的官方宿主。
- 生产依赖版本保持：Sdcb.SimdCodecs 1.4.0、Clipper2 2.0.0、可选PdfPig 0.1.16。Core复用纯托管Clipper2用于路径布尔；没有引入Google Skia绘图。允许的.NET系统压缩和macOS官方窗口宿主例外保持，完整许可证随对应包分发。
- 新增/扩展Conics、Gradients、Strokes、Pixels、Compatibility99等可运行示例，以及隔离原版对照、标量/SIMD、原始像素/差异图、GPU完成等待、AOT和真实nupkg消费测试。AVX2/SSE2浮点各5568缓冲、GPU566检查/556精确比较及既有474413优化检查通过；其余未过画质门槛保持非零结果。
- 修正包内版本同步脚本，只更新SimdSkia版本，避免误替换PdfPig0.1.16子串；新增README/nuspec依赖一致性检查。所有包带累计CHANGELOG.md、README使用方法、MIT及第三方归属；新增冻结证据Git字节/哈希验证，保留未发布候选与旧测量，不覆盖历史。

### 升级与保留限制

- 将直接引用的全部SimdSkia.*包统一为0.1.2；Avalonia家族包统一为相同的已支持版本。桌面安装SimdSkia.Avalonia.Desktop，可选SimdSkia.Avalonia.Gpu；独立SK*用户安装SimdSkia或SimdSkia.Compatibility，PDF读取再加SimdSkia.Pdf。现有UseSimdSkiaDesktop接入方式不变，正式包无需勾选预发行版。
- **SK*默认图片解码已校正为原版N32/预乘Alpha**，与0.1.1的RGBA/直通默认不同；直接消费原始像素的应用要核对ColorType、AlphaType和RowBytes。底层ImageCodec的直通RGBA合同保持；不支持的格式/状态明确报错。
- 按用户要求保留SKObject.Handle及SkiaSharpVersion类型/Native/NativeMinimum/CheckNativeLibraryCompatible共5项原生合同缺口，不伪造native Skia句柄或版本。不建立原版已编译强名称程序集的二进制替换保证，同名SK*类型不能无别名混编。
- JPEG严格门槛仍21/24，曲线/LCD/文字/SKP/整页部分像素门槛未通过；复杂字体/SkSL/SKP/PDF、GPU高级采样/滤镜/SaveLayer等有明确子集限制，Metal未实机验收。**正式版本号不代表上述差距已消失，也不代表整体持平且最差落后不超过10%的目标已达成。**


## 发布验证状态

0.1.2十二个正式包已公开，CI与本机独立下载对DLL、nuspec、宿主/buildTransitive资产、README/CHANGELOG/LICENSE逐项核对一致。[发布流水线](https://github.com/donet-org-cn/SimdSkia/actions/runs/42324944145)及关联三平台核心/桌面、几何、Windows AOT均成功；标签`v0.1.2`固定在`de2ec4e55e9d888364f0d7ea95ce28701d97b8da`，[GitHub正式发行](https://github.com/donet-org-cn/SimdSkia/releases/tag/v0.1.2)。

同一适配器覆盖Windows x64/Linux X11 x64/macOS ARM64的Avalonia12.1.0–12.1.5：18组控件/实际窗口和6组最低/最新Native AOT通过。本机另以仅NuGet.org源、禁用HTTP缓存与全新私有包缓存AOT发布并运行窗口，自动使用Microsoft YaHei UI系统字体，捕获真实呈现帧，PE无CLR头且未加载CLR/JIT。[完整证据](evidence/nuget-stable-0.1.2-2026-11-07/)和[汇总](evidence/nuget-stable-0.1.2-2026-11-07/release.json)。

全部十二包均包含从0.1.1以来的完整累计CHANGELOG和NuGet更新说明；正式程序集确认892→3230/3235、累计2338项新增元数据，保留5项原生合同缺口。性能继承preview.3同一绘图代码的测量，没有以正式版本号改写JPEG21/24、整页0/18或Metal未验证状态。
