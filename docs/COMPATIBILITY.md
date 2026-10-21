# 兼容报告：SkiaSharp 3.119.4

## 固定基线

核查日期 2026-10-12。Avalonia.Skia 12.1.4 的实际恢复锁文件解析到 SkiaSharp 3.119.4，见 [依赖探针](../probes/AvaloniaDependencyProbe/packages.lock.json)。这是独立探针，不是用户现有应用的使用清单。官方 [NuGet 依赖](https://www.nuget.org/packages/Avalonia.Skia/12.1.4) 与 [版本源码](https://github.com/mono/SkiaSharp/tree/v3.119.4/binding/SkiaSharp) 用于调查；包哈希见 baseline-packages.json。

## 三种兼容

| 维度 | 当前结果 |
| --- | --- |
| 源码 | 已实现子集可重编译；同一绘图/行为调用分别编译至原版与替代。37 类型/425 公共项与基线匹配，2810 缺口；尚不支持完整 Avalonia 后端。 |
| 二进制 | 未成立。原版 SkiaSharp, Version=3.119.0.0, PublicKeyToken=0738eb9f132ed756；替代 SimdSkia.Compatibility, Version=1.0.0.0, PublicKeyToken=null。未伪造强名称、公钥或承诺已编译依赖直接替换。 |
| 行为 | 36 原有/14 扩展测试通过，25 基线探针一致；原版画质 108/120 达标、12 曲线/文字不达标。GPU 对 CPU 所测场景一致，仍非全行为证明。 |

## 清单

[基线](api/baseline-3.119.4.json) 包含 202 类型/3235 项；[替代](api/implemented.json) 37 类型/425 项；[逐项差异](api/comparison.json) 2810 项缺失，0 项额外或不同。425 包含类型、枚举字段和明确拒绝的 API，不等于 425 功能已完全验收。

清单覆盖公开类型/基类/可见接口/struct layout、构造/方法/重载/返回、字段与枚举值、属性访问器/事件、泛型约束、参数名/in/out/ref/pointer/默认值/optional/required modifiers，以及 Obsolete/Flags/ParamArray/MarshalAs。尚缺 protected 扩展面、全部 custom attributes/nullable/ABI 调用细节；metadata 不代替行为或二进制验收。

## 实现范围

| 类型/领域 | 已实现 | 明确缺口/限制 |
| --- | --- | --- |
| SKColor/SKColors、几何/矩阵/ImageInfo | 既有基础子集、RGBA/BGRA/三 Alpha/stride/值/仿射/字节信息 | 很多重载/尺寸类型/颜色转换缺失，checked 溢出可能不同 |
| SKBitmap/SKSurface/SKImage | 托管分配、真实 Span/Bytes/GetPixel/SetPixel、Clear/Copy/Snapshot、PNG | raw pointers/InstallPixels/GR surface 等拒绝或缺失；部分分配/解码异常与原版不同 |
| SKPath | Move/Line/Quad/Cubic/Close、AddRect/AddCircle、四种 fill rule、Bounds、快照 | conic/arc/path ops/多数 API 缺失 |
| SKPaint/SKCanvas | AA 填充、butt/miter stroke、线/圆、硬矩形 clip、状态、两色 Clamp gradient、最近邻图片、透明度 | StrokeAndFill、其他 caps/joins/AA clip/一般 clip path/其他 blend 与 shader/filter/高级采样未支持，明确报错 |
| SKTypeface/SKFont | 真实 glyf TTF、cmap 4/12、metrics、XY composite、字形轮廓、基本中英文 | TTC/CFF/可变/彩色/组件点附着/hint VM/复杂 shaping/fallback 未支持 |
| SKTextBlob/Builder/RunBuffer | 定位字形 run 构建、glyph/position Span、Build 后独立快照、跨画布覆盖率缓存与真实绘制；SKFont 的 codepoint→glyph 和 Span 度量 | 仅定位 run 子集；字体灰度、默认选项、glyph bounds 仍有差异；重叠字形透明合成不完整；无 native text blob handle |
| 文本布局 | 同位置 DrawText、度量/对齐；SimdSkia.TextRendering 支持基本换行与 Latin word/CJK 边界折行并返回布局 | 不是完整 Unicode line-break/shaping；不支持 Bidi/mark，段落 API 是品牌 API，不伪造基线重载 |
| PNG/SKData | 无原生依赖编码/解码、过滤/CRC/zlib、sRGB/gAMA、透明度 | JPEG/WebP/16-bit/Adam7/APNG/ICC 等未提供；PNG encoder stored 块较大；质量参数不改变 PNG 无损像素 |
| SIMD | Vector<T> 清屏/Alpha/不透明图片行，AVX2 覆盖率合成/Adler，SSE2/SSSE3 PNG 过滤/转换；真实标量 fallback | 各路径依硬件选择；未宣称所有功能都已优化或 ARM 实机验证 |
| GPU | SimdSkia.VulkanSurface 创建真实 Vulkan 离屏后端；CPU/GPU 消费同命令，VkImage 缓存 | 无 GR/native handle 替代、窗口 swapchain、完整 GPU API 或二进制接入；Linux 未实测 |

## 默认值和生命周期

Font 当前明确选择 Hinting=None、Subpixel=true，和原版默认 Normal/false 不同；设置其他 hint 或关闭 subpixel 会明确拒绝。字体 edging 支持 Alias/Antialias，LCD subpixel AA 拒绝。DrawText 不静默忽略 shaping；复杂脚本/mark/Bidi 输入报错。CJK/Latin 的 glyph ID、advance、控制点 bounds 对照一致，但灰度边缘仍有差异。

新增 BaselineSnap=false、LinearMetrics=true，原版默认分别为 true、false，已在隔离原版中实际核对；反向设置明确报错。Embolden/ForceAutoHinting 支持 false，true 报错。定位文字需显式选相同的 None hinting / Antialias / Subpixel / unsnapped / linear 配置才可对照。`GetGlyphWidths` 的固定 6 字形样本仍有 g 底部 1 px 差异；Blob.Bounds 有约 0.00001 浮点差异。新 UI 文字 4/4 场景未过画质阈值，详见 [Avalonia 专项报告](AVALONIA-OPTIMIZATION.md)。

TextBlobBuilder.Build 后清空待建 run，blob 不依赖 font/builder 的后续寿命或可写 run 缓冲；旧 run 属性显式失效，已取得的 Span 无法撤销。空 builder/全零长度 run 返回 null。文字覆盖率全局缓存最多 512 项/16 MiB 估算数据，可能在 blob Dispose 后保留至淘汰；每个活字体的定位几何缓存 64 项/4 MiB，通过弱所有者关联释放。不把缓存预算等同于进程总内存。

bitmap 释放不使已有 canvas 像素视图失效；canvas 不释放 bitmap；surface 拥有其 canvas；snapshot 独立。paint 保留 gradient 值，shader wrapper Dispose 不影响已赋画笔。已有 Span 不能撤销。Native handle、pinning、callback 和 native 引用身份规则未兼容。

Dispose 后包装对象访问采取显式异常保护；只宣称探针对照点一致，不能声称所有 native 未检查访问具有同样行为。PNG invalid data 通常抛 InvalidDataException，原版可能返回 null；unsupported codecs/颜色配置报错。原版宽松边界不能用成功假数据代替。

原版 curves/text 超初设 32 通道误差，失败详见 VALIDATION.md，未扩大容差宣称兼容。基本光栅/矢量功能已实现不代表全画质完成。

## Avalonia 接入

普通 Avalonia.Skia 仍引用原版 SkiaSharp/NativeAssets/HarfBuzzSharp，不会自动使用本引擎。要源码重编译或做独立后端适配，还须补实际 SK* 使用面及纯 C# shaping，不能仅把 DLL 换名。尚无真实 Avalonia 窗口应用集成验证。

2026-10-21 已对真实 Avalonia.Skia.dll 做 [逐成员与调用者扫描](avalonia-api-12.1.4.json)：92 个 Skia 类型中本实现有 33 个；346 个成员引用中 120 个存在签名，226 个缺失。原始源码、MIT 许可与 SHA 固定在 [源码基线](avalonia-source-baseline.json)，可用 tools/fetch-avalonia-source.ps1 重取。属性访问器按成员计数，内联常量不在计数内；存在不等于行为验证通过。

原 GlyphRunImpl 的构造期固定请求 Full hinting/LCD 来度量 bounds，目前会明确报错。DrawImage/SKSamplingOptions、round-rect/AA/path clip、SaveLayer/opacity mask/shadow、几何/regions、字体匹配以及宿主 GPU device/image/present 等仍有接入缺口。本轮只完成实际定位文字调用链的独立回放与优化，没有宣称整个 Avalonia 已能重编译或替换运行。
