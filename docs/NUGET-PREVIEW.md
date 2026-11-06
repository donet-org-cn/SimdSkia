# NuGet 发布记录

## 0.1.2-preview.3 — 已公开，十二包独立核对通过

十二包已公开下载，全部DLL、nuspec、宿主/buildTransitive资产和包内README/CHANGELOG/LICENSE与冻结CI包一致；GitHub CI及本机独立下载两次验证结果一致。[发布流水线](https://github.com/donet-org-cn/SimdSkia/actions/runs/42308006858)和关联三平台核心/桌面、几何、Windows AOT均成功，标签`v0.1.2-preview.3`固定在`82e9575971aff578762e879aea8afdbbdf9c811d`。[GitHub预发行](https://github.com/donet-org-cn/SimdSkia/releases/tag/v0.1.2-preview.3)。

同一适配器SHA256覆盖Windows x64/Linux X11 x64/macOS ARM64的Avalonia 12.1.0–12.1.5：18组控件/实际窗口与最低/最新共6组Native AOT通过。本机另以仅NuGet.org源、全新私有缓存恢复并AOT发布实际窗口，系统默认Microsoft YaHei UI字体、真实呈现帧和无CLR/JIT检查通过。[完整发布证据](evidence/nuget-0.1.2-preview.3-2026-11-07/)与[汇总](evidence/nuget-0.1.2-preview.3-2026-11-07/release.json)保留数据。macOS的CPU窗口通过不等于Metal GPU通过，后者没有Mac GPU实机验证。

固定API仍3230/3235，5项原生合同缺口按要求保留。连续MoveTo/特殊混合SaveLayer修复及464组逐字节原版对照已进入发布门槛。最终源码另重建并跑完63进程控件页：CPU/Vulkan/Direct2D综合耗时比分别0.778/0.682/0.575；controls×1 CPU工作集原版91.00/本库110.75MiB。**整页画质仍0/18，JPEG及部分GPU/API速度仍有差距，不代表全面行为兼容或整体同画质持平。** [最终源码性能/CPU/内存报告](PERFORMANCE-PREVIEW3-2026-11-07.md)、[完整测试与限制](PUBLIC-API-FINAL-2026-11-07.md)。

十二包统一0.1.2-preview.3 / .NET10；Sdcb.SimdCodecs1.4.0、可选PdfPig0.1.16保持。修正文档版本同步脚本，不再改写第三方依赖号。两个未发布候选和失败/取消记录保留如下。

```sh
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.2-preview.3
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.2-preview.3
dotnet add package SimdSkia.Pdf --version 0.1.2-preview.3
```

## 0.1.2-preview.2 — 上传前取消，未发布

行为修复、三平台核心/桌面、几何和Windows AOT均已通过，但包内PdfPig依赖说明被旧更新脚本误替换，实际依赖始终正确。上传前取消[发布作业](https://github.com/donet-org-cn/SimdSkia/actions/runs/42307036472)，未上传NuGet；修正说明和校验后使用preview.3。旧候选证据和标签保留。

## 0.1.2-preview.1 — CI失败，未发布

本轮预览统一十二包，固定3.119.4基线3230/3235，保留5项真实原生合同缺口；补充SVG/XPS/SKP、Alpha8、浮点颜色、GL/GLES/D3D12/Metal与GPU资源链路，优化Vulkan复制/合批/读回及分配。Metal没有Mac GPU，JPEG/部分文字与整页画质、性能/内存差距仍披露。[完整测试与性能](PUBLIC-API-FINAL-2026-11-07.md)。当前尚未完成本版CI与公开下载验证，不能把下方0.1.1的通过结果当作本版发布成功。

```sh
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.2-preview.1
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.2-preview.1
```

## 正式补丁版 0.1.1 — 已完成公开验证

十二包已公开，全部 DLL、六套官方宿主/桥接库、buildTransitive 目标、README/CHANGELOG/LICENSE 和 nuspec 与冻结 CI 产物一致，本地及 GitHub CI 两次独立下载结果一致。纯 NuGet.org 源与空缓存安装、构建和实际窗口运行通过。[正式发行工作流](https://github.com/donet-org-cn/SimdSkia/actions/runs/41831366323)全部成功；标签 `v0.1.1` 固定在 `b45f5cb09664c55ee421f9617c9fc57fab56f6b7`，[GitHub 正式发行](https://github.com/donet-org-cn/SimdSkia/releases/tag/v0.1.1)。

同一份适配器二进制覆盖 Windows x64 / Linux X11 x64 / macOS ARM64 的 12.1.0–12.1.5，18 组控件/真实窗口及最低/最新版 6 组 Native AOT 通过。Windows 最低/最新 Direct2D、Vulkan 四组窗口冒烟通过；这不是新性能跑分。十二包统一 0.1.1 / net10.0，仓库 SDK 10.0.403+，编解码依赖保持 Sdcb.SimdCodecs 1.4.0。新窗口宿主按实际 Avalonia 版本选择，仅一套进入应用；未知未来 12.x 要补充回归与宿主，13.x 不在本版范围。完整 API/原版画质尚不等价，严格 JPEG 仍 21/24、qualityPassed=false。[报告](AVALONIA-VERSION-COMPATIBILITY.md)与[证据](evidence/avalonia-0.1.1-2026-11-04/results.json)保留结果和公开下载地址。

```sh
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.1
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.1
```

以下为此前版本的历史记录。

## 正式版 0.1.0 — 2026-11-04

**已正式上架：12 包均已公开下载，并与冻结的 CI DLL、nuspec、README、CHANGELOG 和 LICENSE 核对一致。** [发布流水线](https://github.com/donet-org-cn/SimdSkia/actions/runs/41813144199)成功，标签 `v0.1.0` 固定在 `32988cf63c5f88c28f8622faf2b056788cf94ea9`；[正式发行](https://github.com/donet-org-cn/SimdSkia/releases/tag/v0.1.0)和[完整证据](evidence/nuget-stable-0.1.0-2026-11-04.json)保留数据。十二包统一 0.1.0，.NET 10，Avalonia 12.1.5 / Sdcb.SimdCodecs 1.4.0。稳定范围限于文档列出的已实现功能；严格 JPEG 和原版整页画质仍有差距，原门槛不变。代码、测试与限制见[报告](STABLE-0.1.0-2026-11-04.md)。下面为历史预览发布记录。


## 第十预览版 0.1.0-preview.10

**已公开发布：12 个包均已实际公开下载并核对 DLL、nuspec、README、CHANGELOG 和 LICENSE，与冻结的 CI 产物一致。** [NuGet 可信发布流水线](https://github.com/donet-org-cn/SimdSkia/actions/runs/41310992540)成功，标签 `v0.1.0-preview.10` 固定在 `115ffdf87e9fb446932e8fe3ed5f25fb6148b9b6`，[GitHub 预发行](https://github.com/donet-org-cn/SimdSkia/releases/tag/v0.1.0-preview.10)与[完整发布证据](evidence/nuget-preview10-2026-11-01.json)记录实际结果。 十二包统一版本，升级为 .NET 10 / C# 14，完整接入 Sdcb.SimdCodecs 1.2.0；保留允许 .NET 系统原生压缩的明确例外。Avalonia 12.1.4 / SkiaSharp 源码基线 3.119.4 不变，.NET 8 用户需先迁移或保留 preview.9。

README、每包 CHANGELOG 和 NuGet 发行说明均写入[123 进程原版性能对照](SKIA-COMPARISON-2026-11-01.md)：CPU 控件页 0.891→0.549 ms，Vulkan/Direct2D 含完成回读的综合耗时比为 0.678/0.624；CPU 工作集 90.39→109.81 MiB，纹理 PNG 解码耗时 2.03 倍、UI PNG 编码慢约 19%，JPEG 编码文件大小代价同时披露。不是整体同画质/同内存持平结论。

本地源码 Release 0 警告/错误，369 项独立图片/损坏输入/压缩、1,416 项集成与标量、471,569 项精确优化、9,750 项控件及既有兼容/PDF 回归通过。严格 JPEG 对照仍为 21/24，原门槛不变；发布与托管 CI 显式记录 `qualityPassed=false`，只允许三个已披露的精确差异作为本版预览发布例外，新差异阻止发布。CPU/GPU 整页画质仍为 0/3、0/12；发布验证通过不能改写该结论。

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.10
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.10
# 按需读取 PDF：
dotnet add package SimdSkia.Pdf --version 0.1.0-preview.10
```

[三平台托管回归](https://github.com/donet-org-cn/SimdSkia/actions/runs/41310605824)、[三平台桌面 JIT/AOT/实际 nupkg 和 PDF 消费](https://github.com/donet-org-cn/SimdSkia/actions/runs/41310605830)、[Windows 双采样 AOT 与兼容/PDF/编解码 AOT](https://github.com/donet-org-cn/SimdSkia/actions/runs/41310605792)全部成功。已下载核对三平台 9 份桌面运行记录，Windows 新编解码实际 AOT 1,417 项通过；三平台 JPEG JSON 均保留 21/24 / qualityPassed=false。发布作业额外消费实际编解码包 1,416 项、真实 AOT 像素窗口、桌面/PDF 包后上传。旧版本、标签与测量快照保持原样。

## 第九预览版 0.1.0-preview.9

**已公开发布：12 个包均已实际下载并核对内容。** [可信发布流水线第 3 次执行](https://github.com/donet-org-cn/SimdSkia/actions/runs/40490758078/attempts/3)成功，全部公开包的 DLL、README、CHANGELOG、MIT 许可证和 nuspec（含发行说明）与冻结的 CI 产物一致。标签 `v0.1.0-preview.9` 固定于 `044d9e30176aa55eadd9e487f551a695eff24906`，[GitHub 预发行](https://github.com/donet-org-cn/SimdSkia/releases/tag/v0.1.0-preview.9)保留完整更新内容；[发布证据](evidence/nuget-preview9-2026-10-27.json)列出全部公开下载地址、内容核对与历史失败。

12 个包统一版本，新增可选 `SimdSkia.Pdf`。本版包括真实像素指针、系统字体族/样式、GIF Codec、圆角/图层/几何和采样 API、多页栅格 PDF 导出、受限 PDF 阅读，以及 VelaShell 字体 Try 回退/图片舍入边界修复。

[三平台托管检查](https://github.com/donet-org-cn/SimdSkia/actions/runs/40490757996)、[三平台 JIT/AOT/nupkg 窗口和 PDF 包消费](https://github.com/donet-org-cn/SimdSkia/actions/runs/40490757943)、[Windows 双 AOT 与新增兼容/PDF AOT](https://github.com/donet-org-cn/SimdSkia/actions/runs/40490546007)均成功。已下载核对同一源码提交的 9 份桌面记录与 3 份 PDF 包消费记录（每平台 18 项）。发布 build 作业验证全部 12 包和隔离 AOT/桌面/PDF 消费，publish 完成 OIDC 身份交换、上传和公开下载内容核对。

前两次 publish 在 OIDC 阶段返回 HTTP 401，未上传包；仓库从 `hnlyf168/SimdSkia` 转移至 `donet-org-cn/SimdSkia` 后，更新 NuGet 可信发布策略，第 3 次成功。版本、标签和已验证 CI 包均未改变。历史失败保留在发行证据中，不将认证失败当作构建或画质失败。

每个包包含对应 README、`CHANGELOG.md`、NuGet 发行说明及许可证；PDF 的 PdfPig 依赖与 Apache-2.0 通知只进入可选 PDF 模块。发行变化详见[preview.9 更新记录](RELEASE-NOTES.md)。Avalonia 固定 12.1.4、SkiaSharp 源码基线固定 3.119.4。

这是披露限制后发布的实验性预览版：VelaShell 常规页面可呈现，但组合字符、资源监视页性能、部分输入、专属 Skia 预览和完整画质仍有缺口，不能宣称整应用替换。PDF 是受限子集，导出为栅格页面。本版没有完整重测 Avalonia CPU/GPU 吞吐和常驻内存，旧 preview.8 成绩保持历史版本标注。

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.9
# 按需安装同版本 GPU / PDF 模块：
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.9
dotnet add package SimdSkia.Pdf --version 0.1.0-preview.9
```

桌面启动使用 `.UseSimdSkiaDesktop()`；PDF 导出只需 Compatibility，PDF 阅读才需要另装 Pdf。

## 第八预览版 0.1.0-preview.8

**已公开发布：11 个包均已实际下载并核对内容。** [可信发布流水线](https://github.com/donet-org-cn/SimdSkia/actions/runs/40429937427)全部成功，公开包的 DLL、README、CHANGELOG、MIT 许可证和 nuspec（含发行说明）与 CI 一致。标签 `v0.1.0-preview.8` 固定在 `a636296b2b7fec05c44c18f25ae62f4ba03de74a`；[发布证据](evidence/nuget-preview8-2026-10-27.json)包含 11 包版本、哈希、下载地址和实际窗口记录。

[三平台托管回归](https://github.com/donet-org-cn/SimdSkia/actions/runs/40429073489)、[三平台 JIT/AOT/nupkg 真实窗口](https://github.com/donet-org-cn/SimdSkia/actions/runs/40429073275)、[Windows AOT 及两种采样指令集](https://github.com/donet-org-cn/SimdSkia/actions/runs/40429073278)全部成功，下载核对 9 份桌面结果。通用/v3 AOT 各通过 460,050 项采样；本机真实包 CPU/Direct2D/Vulkan 控件窗口和包内 README 的 CPU/GPU 编译通过。这些窗口检查验证发布可用性，不作为速度排名。

11 个包统一为 preview.8，固定 Avalonia 12.1.4。根据用户在了解画质、内存及验证范围后确认的发布要求，本次作为实验性预览版发布；完整质量门槛和原始失败记录不变。

包含通用 x64 SSE2 合成/采样、填充、字形串缓存、画布分配及 framebuffer 优化。75 个 CPU / 36 个 GPU 正式进程的[性能报告](PARITY-OPTIMIZATION-2026-10-27.md)已完成；CPU 综合速度门槛通过，Vulkan / Direct2D 为 JIT 离屏完成帧结果。常驻内存未追平，完整整页画质仍未通过，不能把发布验证成功当作全面替换验收成功。

包内 README、CHANGELOG 和 NuGet 发行说明均列出改动、实测范围及限制；[博客园文章](blog/2026-10-27-simdskia-avalonia-aot-cnblogs.md)附性能图、功能兼容表、CPU/内存和历史 AOT 体积说明。

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.8
# 需要 GPU 时再加：
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.8
```

## 第七预览版 0.1.0-preview.7

**已公开发布：11 个包均已实际下载并核对内容。** [可信发布流水线](https://github.com/donet-org-cn/SimdSkia/actions/runs/40344837096)全部成功；公开包中的 DLL、README、`CHANGELOG.md`、MIT 许可证及 nuspec（含发行说明）与 CI 产物逐字节一致。标签 `v0.1.0-preview.7` 固定在 `1490f164084c24c1bd3f3dbaf727d7598a14abee`，旧包和标签保持原内容。[发布与包文档证据](evidence/nuget-preview7-2026-10-26.json)包含全部包哈希、公开下载地址和实际 NuGet 页面更新内容检查。

[三平台托管回归](https://github.com/donet-org-cn/SimdSkia/actions/runs/40344702599)、[三平台 JIT/AOT/nupkg 真实窗口](https://github.com/donet-org-cn/SimdSkia/actions/runs/40344702666)、[Windows AOT 及通用/v3 采样](https://github.com/donet-org-cn/SimdSkia/actions/runs/40344702836)全部成功；已下载核对九份桌面窗口记录，两种采样 AOT 各通过 460,050 项检查。原版全量质量/API 缺口仍保留，不将冒烟测试的帧时间作为性能对比。

本版包含精确八像素 AVX2 双线性采样、半透明图像合成修复、Vulkan 裁剪采样一致性修复，以及独立自绘/AOT 示例。每个包都包含 [完整更新日志](RELEASE-NOTES.md)；三个 Avalonia 包继续带有完整中文接入说明。Avalonia 保持 12.1.4，不要求重编译 Avalonia。

最终核心图片质量 24/24 通过原门槛；性能及未完成项详见 [半透明修复报告](ALPHA-COMPATIBILITY-2026-10-26.md)，不将之前的 3%–5% 页面测试误写成修复后整页或 GPU 复测。内存未见明显改善，完整兼容性仍未通过。

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.7
# 需要 GPU 时再加：
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.7
```

## 第六预览版 0.1.0-preview.6

正在准备可信发布与公开下载验证；在本节明确更新为“已公开发布”前，不将打包或上传成功视为已上架。11 个包统一为 `0.1.0-preview.6`，Avalonia 继续锁定 `12.1.4`。完整包名、跨平台接入、GPU 和 AOT 命令见 [首页](../README.md)，三个 Avalonia 包的 README 与首页自动同步。

本版纳入 preview.5 之后的系统字体分页/TTC 共享、光栅缓冲复用、精确 SIMD 双线性采样、图片画刷缓存生命周期修复、几何边界复用及字形缓冲优化。新增经典桌面 JIT 后台启动准备，线程完成即退出，无窗口程序和 Native AOT 跳过；可以通过 `EnableStartupPreparation = false` 关闭。

[最新实测报告](STARTUP-ROUND7-2026-10-22.md)保留 70 次可见启动、原版 CPU、GPU 前后对照、内存、AOT 体积和全部失败项。JIT 白色阶段采样中位数为 CPU 114→61 ms、Direct2D 289→198 ms、Vulkan 363→266 ms；原版 CPU 49 ms。常驻内存和最新一轮的持续 GPU 吞吐没有明显改善，完整画质/API 仍未通过，不宣称完整替换。发布只更改版本、依赖锁定和文档，渲染实现对应已测源码 `1438833`。

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.6
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.6
```

第二行仅用于需要 GPU 的应用。所有直接引用的 SimdSkia 包应使用同一版本；旧版本和旧标签保持原内容。

## 第五预览版 0.1.0-preview.5

**已公开发布，11 个包均已实际下载，DLL 和 README 与发布 CI 产物逐字节一致。** [可信发布流水线](https://github.com/donet-org-cn/SimdSkia/actions/runs/39746371476)全部成功，完成构建、全部包内容检查、真实 Native AOT 像素/窗口验证、隔离包消费、上传及公开下载核对。标签 `v0.1.0-preview.5` 固定在 `c12c3db16dda9b5fa7505a146bf1b948d3311934`。下载 SHA、实际包和测试证据见 [发布记录](evidence/nuget-preview5-2026-10-22.json)。

推荐桌面应用统一升级到 `0.1.0-preview.5`，包含本轮 Vulkan 优化；旧 preview.4 内容不变。安装 [SimdSkia.Avalonia.Desktop](https://www.nuget.org/packages/SimdSkia.Avalonia.Desktop/0.1.0-preview.5)，GPU 按需另加 [SimdSkia.Avalonia.Gpu](https://www.nuget.org/packages/SimdSkia.Avalonia.Gpu/0.1.0-preview.5)，完整接入代码见 [首页](../README.md)。

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.5
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.5
```

本版纳入 [Vulkan 专项优化](VULKAN-OPTIMIZATION-2026-10-22.md)：有界覆盖率缓存、保守分块、宽绕数与专用计算管线、显存输入、BGRA 直接输出、依赖同步，以及可选 GPU 时间戳诊断。11 个包统一版本；Avalonia 继续锁定 12.1.4。实测窗口 P50 22.250→2.316 ms，CPU 为 2.067 ms，内存没有同比改善；完整 API/画质仍未通过。渲染源码与报告中实测提交相同，仅变更版本、依赖锁定和说明，保留实际测量二进制哈希。

本机真实 preview.5 包的 CPU/Direct2D/Vulkan 控件窗口均通过，GPU 均为 0 条不支持命令的回退，另外明确记录 6 条 CPU 图层命令。[三平台托管回归](https://github.com/donet-org-cn/SimdSkia/actions/runs/39746371489)、[三平台 JIT/AOT/nupkg 窗口](https://github.com/donet-org-cn/SimdSkia/actions/runs/39746343255)及 [Windows 双后端 AOT](https://github.com/donet-org-cn/SimdSkia/actions/runs/39746342939)全部成功；已下载核对三平台九份实际窗口记录。单次包窗口启动/完成时间含冷初始化，不能作为性能比较。

## 第四预览版 0.1.0-preview.4

**已公开发布。** 本次已实际下载全部 11 包，DLL 和 README 与原 CI 产物逐字节一致，[发布证据](evidence/nuget-preview4-2026-10-22.json)保留最初超时及后来成功下载的记录。[发布流水线](https://github.com/donet-org-cn/SimdSkia/actions/runs/39657888167)的构建、包内容检查、AOT/nupkg 实际窗口与上传已完成；当时 NuGet 尚未完成索引，20 分钟公开下载校验超时，因此原流水线仍显示失败，不改写历史。源码标签 `v0.1.0-preview.4` 固定在 `5eef27d936dc72cb0dd61c7074292b3192bf9a30`，旧标签与包不改写。

本轮包含实测 CPU 热点优化、GPU 有界纹理内容复用、Direct2D Opaque Alpha 修正和高 DPI ImageBrush 38/40 修正；不宣称完整兼容。独立 [性能与兼容报告](PERFORMANCE-COMPATIBILITY-2026-10-22.md)包含原版 CPU/Vulkan、新旧窗口、CPU/内存/分配和失败画质项目。三平台回归、实际窗口/AOT/nupkg 与 Windows 双后端 AOT 已全部通过，详见 [验证](VALIDATION.md)及 [9 条 CI 记录](evidence/desktop-preview4-ci-2026-10-22.json)。

preview.4 包与标签保持原内容，不包含后续 Vulkan 专项优化；该优化随 preview.5 发布。桌面应用安装 `SimdSkia.Avalonia.Desktop`，可选 `SimdSkia.Avalonia.Gpu`，使用方式见 [首页](../README.md)。

## 第三预览版 0.1.0-preview.3

**已公开发布。** 11 个 `0.1.0-preview.3` 包均已实际下载，DLL 和 README 与发布 CI 产物逐字节一致，[发布流水线 39648463806](https://github.com/donet-org-cn/SimdSkia/actions/runs/39648463806)全部成功。修复 GPU 文字图集在覆盖裁剪下的重叠合成顺序与滚动缓存；preview.2 标签和包保持原样。推荐安装 [SimdSkia.Avalonia.Desktop](https://www.nuget.org/packages/SimdSkia.Avalonia.Desktop/0.1.0-preview.3)，GPU 另加 [SimdSkia.Avalonia.Gpu](https://www.nuget.org/packages/SimdSkia.Avalonia.Gpu/0.1.0-preview.3)，两者版本均为 `0.1.0-preview.3`；完整使用方式在[首页](../README.md)。公开下载核对见[发布证据](evidence/nuget-preview3-2026-10-22.json)。

已从已发布的包内 README 提取完整程序，在仅 NuGet.org 源、全新缓存下完成 CPU 和 Direct2D 配置的恢复与编译，两者均 0 警告/0 错误。另从 NuGet.org 恢复 CrossPlatform 控件示例，检查全部 SimdSkia 依赖来自包，在本机 GTX 1050 Ti 上实际创建、呈现及缩放 Direct2D 窗口通过。原始记录为 `artifacts/nuget-public-preview3-demo` 和 `artifacts/nuget-public-preview3-desktop`。

## 第二预览版 0.1.0-preview.2

已公开：11 个包均完成公开下载及 CI DLL/README 内容核对，[发布流水线](https://github.com/donet-org-cn/SimdSkia/actions/runs/39646305172)成功。该版本存在重叠字形与覆盖裁剪的图集顺序问题，现已在 preview.3 修复；推荐使用新版本。旧标签和包不改写。

本轮增加 `SimdSkia.Avalonia.Desktop`、`SimdSkia.Avalonia.Gpu`、`SimdSkia.Gpu.Direct2D`，共 11 个包。桌面用户安装 Desktop，GPU 按需另加 Avalonia.Gpu；三个 Avalonia 包共享完整中文 README，包含三平台双文件程序、安装与迁移、GPU 配置、系统字体和 AOT。核心包仍不携带原生字体/绘图库；仅 Desktop 的官方 Avalonia.Native 依赖在 macOS 携带经允许的窗口桥。源码支持和性能结果见 [报告](DESKTOP-GPU-2026-10-22.md)，公开发布以流水线及实际下载验证为准。

```powershell
powershell -NoProfile -File tools/pack-preview.ps1 -Version 0.1.0-preview.2 -Output artifacts/nuget-preview-2
powershell -NoProfile -File tools/test-desktop-package.ps1 -Packages artifacts/nuget-preview-2
```

桌面消费程序使用私有 NuGet 缓存，检查没有引擎 ProjectReference，并实际创建系统窗口；三平台 CI 另做 Native AOT。下面为首版发布历史与仍适用的可信发布配置。

## 首版 0.1.0-preview.1（历史）

版本仅接受 `数字.数字.数字-preview.数字`，首版为 `0.1.0-preview.1`。八个包：SimdSkia、Core、Cpu、Text、Imaging、Gpu、Compatibility、Avalonia。SimdSkia 是兼容层的依赖汇总包；Avalonia 应用通常只引用适配器，不需要 SK* 汇总包。

**首版已公开发布。** 2026-10-22 已确认全部八个包可从 NuGet.org 实际下载，DLL 和 README 与 CI 产物一致。[SimdSkia.Avalonia 包页面](https://www.nuget.org/packages/SimdSkia.Avalonia/0.1.0-preview.1)及 [GitHub 首页使用说明](../README.md)均提供接入示例。首页两个文件已在全新包缓存、仅 NuGet.org 源下成功还原并编译（0 警告/0 错误）。

各实现包包含 net8.0 程序集、包内 README、MIT 许可证及符号包；相关第三方许可证保留。没有打包字体、Skia/HarfBuzz/ANGLE 原生库或 Vulkan loader。NuGet 页面会显示包内 README；[Avalonia 专用 README](../src/SimdSkia.Avalonia/README.md) 包含完整的双文件程序、已有应用迁移、系统字体、AOT 发布和排错步骤。

## GitHub Actions

工作流：[nuget-preview.yml](../.github/workflows/nuget-preview.yml)。可手动运行，或推送 `v0.1.0-preview.1` 形式的 tag。手动运行可关闭 publish，仅生成经过验证的包。

构建作业执行受测功能回归、打包、检查 nuspec/版本/许可证/符号/原生依赖，并在隔离 NuGet 缓存中使用真实 nupkg 编译一个无 ProjectReference 的 Windows Native AOT 程序。该程序验证 SK* 像素链路和真实 Avalonia Win32 呈现，检查原生 GUI PE、进程没有 CLR/JIT/Skia/HarfBuzz，保存截图和指标。CI 使用固定 OFL 测试字体，不依赖 runner 安装中文字体。本机另用系统字体运行同一个 AOT 消费验证。

只有构建和消费测试全部通过，发布作业才通过 OIDC 交换一小时有效的 NuGet 凭据并上传包；没有长期 API Key。NuGet 与 GitHub 的配置为：

| 字段 | 值 |
| --- | --- |
| NuGet.org 用户 | dotnet.org.cn |
| GitHub repository owner | hnlyf168 |
| Repository | SimdSkia |
| Workflow file | nuget-preview.yml |
| GitHub environment | nuget-preview |
| 仓库变量 | NUGET_USER=dotnet.org.cn |
| 包范围 | SimdSkia*，允许创建新包和更新版本 |

NuGet 策略由账户拥有者在 [Trusted Publishing](https://www.nuget.org/account/trustedpublishing) 配置；对应字段以 [NuGet 官方文档](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) 为准。首次发布允许创建新包，后续版本仍需带 preview 后缀。dotnet push 的重复版本跳过只用于重试；不可用相同版本替换已发布的不同内容。

上传后还需等待 NuGet 验证和索引。后续流水线会轮询实际公开 nupkg 下载，并逐字节核对其中的 DLL、README 与 CI 产物；NuGet 对 ZIP 容器增加仓库签名，因此不把整个 nupkg 的签名后哈希与未签名文件强行对比。等待上限 20 分钟，超时会明确失败，不以 PUT 成功代替可安装验证。首版发布的上传日志为 [GitHub Actions 39628597818](https://github.com/donet-org-cn/SimdSkia/actions/runs/39628597818)。

## 本机验证

```powershell
powershell -NoProfile -File tools/pack-preview.ps1 -Version 0.1.0-preview.1
powershell -NoProfile -File tools/test-preview-consumer.ps1 -Version 0.1.0-preview.1 -SystemFonts
```

`artifacts/nuget-preview/manifest.json` 保存所有包的 SHA256、字节数和依赖。`artifacts/nuget-consumer/metrics.json` 保存真实 Native AOT 消费验证结果，`window.png` 是实际渲染截图。所用包缓存按当前 manifest 哈希隔离，避免旧的同版本本地包造成误判。

包预览标记不改变已有兼容性缺口，验收和性能限制继续见 [VALIDATION](VALIDATION.md)。单帧的消费测试只验证功能与发布可用性，不是性能基准。
