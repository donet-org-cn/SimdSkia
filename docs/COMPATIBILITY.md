# 兼容报告：SkiaSharp 3.119.4

最新真实应用验收：[VelaShell 整体兼容](VELASHELL-COMPATIBILITY-2026-10-27.md)。常规页面可以通过独立 Avalonia 适配器绘制；实际发现并修正字体 Try 失败语义和图片舍入边界。字体合成仍未实现，Try 返回 false 后由 Avalonia 保留真实近邻字面，不会伪造 Bold/Oblique 元数据。完整应用、原生 Skia 移除和默认整页一致性仍未通过，不能仅以 23 个页面有像素判定全部兼容。

**当前源码包含未发布扩展**：[独立兼容层与 PDF 实测](STANDALONE-COMPATIBILITY-2026-10-27.md)。下表 API 清单已更新至当前源码；NuGet preview.8 的历史数据仍为 37 类型、425/3235 项。新增 PDF 阅读是品牌扩展，不能算作 SkiaSharp 兼容项。

2026-10-22 第二轮核验：[原版对照与性能](PERFORMANCE-COMPATIBILITY-2026-10-22.md)。ImageBrush 38/40、VisualBrush 5/10，新功能总计 82/114；旧图像 108/120、完整 UI 0/3、GPU 12/24，完整行为兼容仍未成立。下表区分 SK* 外观与独立 Avalonia 平台适配器，后者通过服务接口接入，并不要求完整 SK* 表面。

## 固定基线

核查日期 2026-10-12。Avalonia.Skia 12.1.4 的实际恢复锁文件解析到 SkiaSharp 3.119.4，见 [依赖探针](../probes/AvaloniaDependencyProbe/packages.lock.json)。这是独立探针，不是用户现有应用的使用清单。官方 [NuGet 依赖](https://www.nuget.org/packages/Avalonia.Skia/12.1.4) 与 [版本源码](https://github.com/mono/SkiaSharp/tree/v3.119.4/binding/SkiaSharp) 用于调查；包哈希见 baseline-packages.json。

## 三种兼容

新 `SimdSkia.Avalonia` 是独立的平台服务适配层，已运行固定 Avalonia 12.1.4 的受测 CPU 控件与 Win32 软件窗口；见 [接入限制](AVALONIA-INTEGRATION.md)。它不要求编译原 Avalonia.Skia 到替代 SK* 类型，不改变下述 SkiaSharp 清单，也不承诺其 Skia 专属自绘控件或任何已编译 SkiaSharp 依赖能直接替换。

| 维度 | 当前结果 |
| --- | --- |
| 源码 | 已实现子集可重编译；同一绘图/行为调用分别编译至原版与替代。63 类型/892 公共项与基线匹配，2343 缺口；完整 SkiaSharp 与任意 Avalonia 应用替换尚未验收。 |
| 二进制 | 未成立。原版 SkiaSharp, Version=3.119.0.0, PublicKeyToken=0738eb9f132ed756；本轮实际构建的替代为 SimdSkia.Compatibility, Version=0.1.0.0, PublicKeyToken=null。未伪造强名称、公钥或承诺已编译依赖直接替换。 |
| 行为 | 本轮独立调用链原版 26、替代 37 项检查通过；新增像素对照 11/14 达标，圆角/文字 3 项未通过。历史整页画质缺口仍在，完整行为兼容未成立。 |

## 清单

[基线](api/baseline-3.119.4.json) 包含 202 类型/3235 项；[替代](api/implemented.json) 63 类型/892 项；[逐项差异](api/comparison.json) 2343 项缺失，0 项额外或不同。892 包含类型、枚举字段、值类型重载和明确拒绝的 API，不等于 892 个绘图功能已完全验收。提取时程序集 InformationalVersion 仍含 preview.8 和当前 HEAD；**文件 SHA 标识此次未提交构建，不能当成公开 preview.8 包的 SHA**。

清单覆盖公开类型/基类/可见接口/struct layout、构造/方法/重载/返回、字段与枚举值、属性访问器/事件、泛型约束、参数名/in/out/ref/pointer/默认值/optional/required modifiers，以及 Obsolete/Flags/ParamArray/MarshalAs。尚缺 protected 扩展面、全部 custom attributes/nullable/ABI 调用细节；metadata 不代替行为或二进制验收。

## 实现范围

| 类型/领域 | 已实现 | 明确缺口/限制 |
| --- | --- | --- |
| SKColor/SKColors、几何/矩阵/ImageInfo | 基础子集、RGBA/BGRA/三 Alpha/stride/仿射；补 Point/PointI/Point3、Size/SizeI、Rect/RectI 值操作及 Inflate | 全部颜色/矩阵 API 仍未覆盖，checked 溢出可能不同 |
| SKBitmap/SKSurface/SKImage | 托管分配、Span/Bytes/GetPixel/SetPixel、Clear/Copy/Snapshot、PNG/JPEG/GIF 首帧；真实 GetPixels 指针 | 外部 InstallPixels/SetPixels、GR surface、native Handle 等拒绝或缺失；部分分配/解码异常与原版不同 |
| SKPath | Move/Line/Quad/Cubic/Close、AddRect/AddCircle、四种 fill rule、Bounds、快照 | conic/arc/path ops/多数 API 缺失 |
| SKPaint/SKCanvas/SKRoundRect | AA 填充、butt/miter stroke、线/圆/圆角、路径/AA/差集 clip、状态、整组透明度 SaveLayer、两色 Clamp gradient、最近邻/双线性/Mitchell 图片 | StrokeAndFill、其他 caps/joins、其他 blend 与 shader/filter、mipmap/各向异性采样仍拒绝；SaveLayer 使用 CPU 临时层；圆角像素边缘与原版有差异 |
| SKTypeface/SKFont/SKFontStyle | glyf TTF/TTC、face/cmap/metrics/XY composite/轮廓、基本中英文；独立 FromFamilyName 及字体样式/本地化名称匹配 | 独立字体引擎的 CFF/可变/彩色/组件点附着/hint VM/复杂 shaping/逐字符 fallback 未支持；新字体发现本轮只在 Windows 实测 |
| SKTextBlob/Builder/RunBuffer | 定位字形 run 构建、glyph/position Span、Build 后独立快照、跨画布覆盖率缓存与真实绘制；SKFont 的 codepoint→glyph 和 Span 度量 | 仅定位 run 子集；字体灰度、默认选项、glyph bounds 仍有差异；重叠字形透明合成不完整；无 native text blob handle |
| 文本布局 | 同位置 DrawText、度量/对齐；SimdSkia.TextRendering 支持基本换行与 Latin word/CJK 边界折行并返回布局 | 不是完整 Unicode line-break/shaping；不支持 Bidi/mark，段落 API 是品牌 API，不伪造基线重载 |
| PNG/JPEG/GIF/SKCodec/SKData | C# PNG/Adam7/16 位、JPEG、部分 ICC 转 sRGB；GIF LZW/交错/处置/随机帧、SKCodec 三格式解码 | WebP/BMP/APNG、四分量渐进 JPEG、全部 ICC、增量/扫描线 Codec 仍未完成；GIF Plain Text 明确拒绝；PNG quality 不改变无损像素 |
| SKDocument/PDF | 多页栅格 PDF 导出、DPI/元数据/调用者流生命周期；可选 SimdSkia.Pdf 解析并渲染已有 PDF 页面 | 无矢量/可搜索文字输出、PDF/A；阅读仅支持已列明的路径、字体、图像和颜色子集，详见专项报告 |
| SIMD | Vector<T> 清屏/Alpha/不透明图片行，AVX2 覆盖率合成/Adler，SSE2/SSSE3 PNG 过滤/转换；真实标量 fallback | 各路径依硬件选择；未宣称所有功能都已优化或 ARM 实机验证 |
| GPU | VulkanSurface 离屏；独立 Avalonia.Gpu 可选 Windows Direct2D 原生交换链、Windows/Linux Vulkan 完成后回读；有界纹理内容缓存 | 无 GR/native handle 替代/完整 GPU API/二进制接入；Vulkan 直接窗口交换链、macOS GPU、Linux 物理 GPU 验收仍缺失 |

## 默认值和生命周期

Font 当前明确选择 Hinting=None、Subpixel=true，和原版默认 Normal/false 不同；设置其他 hint 或关闭 subpixel 会明确拒绝。字体 edging 支持 Alias/Antialias，LCD subpixel AA 拒绝。DrawText 不静默忽略 shaping；复杂脚本/mark/Bidi 输入报错。CJK/Latin 的 glyph ID、advance、控制点 bounds 对照一致，但灰度边缘仍有差异。

新增 BaselineSnap=false、LinearMetrics=true，原版默认分别为 true、false，已在隔离原版中实际核对；反向设置明确报错。Embolden/ForceAutoHinting 支持 false，true 报错。定位文字需显式选相同的 None hinting / Antialias / Subpixel / unsnapped / linear 配置才可对照。`GetGlyphWidths` 的固定 6 字形样本仍有 g 底部 1 px 差异；Blob.Bounds 有约 0.00001 浮点差异。新 UI 文字 4/4 场景未过画质阈值，详见 [Avalonia 专项报告](AVALONIA-OPTIMIZATION.md)。

TextBlobBuilder.Build 后清空待建 run，blob 不依赖 font/builder 的后续寿命或可写 run 缓冲；旧 run 属性显式失效，已取得的 Span 无法撤销。空 builder/全零长度 run 返回 null。文字覆盖率全局缓存最多 512 项/16 MiB 估算数据，可能在 blob Dispose 后保留至淘汰；每个活字体的定位几何缓存 64 项/4 MiB，通过弱所有者关联释放。不把缓存预算等同于进程总内存。

bitmap 释放不使已有 canvas 像素视图失效；canvas 不释放 bitmap；surface 拥有其 canvas；snapshot 独立。paint 保留 gradient 值，shader wrapper Dispose 不影响已赋画笔。已有 Span 不能撤销。`GetPixels()` 懒固定托管像素存储，bitmap 重分配或 Dispose 后指针失效；异步写入者必须先停止并同步再释放。它是像素地址，不是 native Skia Handle；外部内存接管、释放 callback 和 native 引用身份未兼容。

Dispose 后包装对象访问采取显式异常保护；只宣称探针对照点一致，不能声称所有 native 未检查访问具有同样行为。PNG invalid data 通常抛 InvalidDataException，原版可能返回 null；unsupported codecs/颜色配置报错。原版宽松边界不能用成功假数据代替。

原版 curves/text 超初设 32 通道误差，失败详见 VALIDATION.md，未扩大容差宣称兼容。基本光栅/矢量功能已实现不代表全画质完成。

## Avalonia 接入

下面是 **2026-10-21 的历史 Avalonia.Skia 重编译调查**，未因本轮新增 SK* 项重跑调用者扫描。当前推荐的独立 `SimdSkia.Avalonia` 适配器已通过真实窗口测试；它和“原样重新编译整个 Avalonia.Skia”是不同的验收目标。

普通 Avalonia.Skia 仍引用原版 SkiaSharp/NativeAssets/HarfBuzzSharp，不会自动使用本引擎。要源码重编译整个原适配器，仍须补其实际 SK* 使用面及纯 C# shaping，不能仅把 DLL 换名。真实窗口请使用已实现的独立 SimdSkia.Avalonia 适配器。

2026-10-21 已对真实 Avalonia.Skia.dll 做 [逐成员与调用者扫描](avalonia-api-12.1.4.json)：92 个 Skia 类型中本实现有 33 个；346 个成员引用中 120 个存在签名，226 个缺失。原始源码、MIT 许可与 SHA 固定在 [源码基线](avalonia-source-baseline.json)，可用 tools/fetch-avalonia-source.ps1 重取。属性访问器按成员计数，内联常量不在计数内；存在不等于行为验证通过。

原 GlyphRunImpl 的构造期固定请求 Full hinting/LCD 来度量 bounds，目前会明确报错。DrawImage/SKSamplingOptions、round-rect/AA/path clip、SaveLayer/opacity mask/shadow、几何/regions、字体匹配以及宿主 GPU device/image/present 等仍有接入缺口。本轮只完成实际定位文字调用链的独立回放与优化，没有宣称整个 Avalonia 已能重编译或替换运行。
