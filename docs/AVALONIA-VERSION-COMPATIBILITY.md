# Avalonia 12.x 版本兼容验证（0.1.1）

版本范围：渲染适配器以 Avalonia 12.1.0 为最低编译基线；NuGet 接受 `[12.1.0,13.0.0)`。目前公开版本为 12.1.0–12.1.5，全部进入矩阵。未知未来 12.x 需加入测试并补充桌面宿主，不能承诺未来接口不变；13.x 不在本版范围。

## 真实发现与修复

仅把 `UseSimdSkia` 的精确版本检查放开是不够的。以同一份初始候选包实测，12.1.0/12.1.1 窗口通过，12.1.2 抛 `TypeLoadException`：旧版 `Avalonia.Win32.PopupImpl` 缺少新增的 `SetHitTestVisible` 实现。

最终方案：渲染适配器仍编译一次；桌面包内保存官方 MIT 许可的六版 Win32/X11/macOS 托管宿主，`buildTransitive` 在构建时读取 **实际解析的 Avalonia.Base.dll** 版本，自动选择对应宿主。仅选中的六份托管宿主 DLL 进入应用输出/AOT 链接；其他版本不会复制到应用。NuGet 下载包因此增大，应用输出不会携带六套宿主。未知宿主版本在构建时明确报错，不能静默挑旧版。

macOS 同样选择匹配的官方 Avalonia.Native 托管宿主和 libAvaloniaNative.dylib 窗口桥接库；核心渲染、字体仍为 C#。不把这些官方宿主称作自研 C# 窗口系统，也不调用 Google Skia 绘图。NuGet 家族中的其他直接 Avalonia 引用应选用相同版本。

构建采用 .NET SDK 10.0.403 / runtime 10.0.14；旧 10.0.13 AOT 编译器被 NuGet 高危漏洞审计拦截，未关闭审计绕过，详见 [微软公告](https://github.com/advisories/GHSA-p3jp-9v83-76w9)。

## 验证方法与状态

同一份实际打包的适配器在六版中复用，不逐版本重编；每次检查包内/运行目录 SHA256、实际 Avalonia 程序集版本，以及六个配套宿主的版本。每版执行 9,750 项控件、像素、几何、文字、SIMD、生命周期断言，另启动真实窗口验证帧提交、非空像素、resize 与按钮事件。最低/最新版额外发布并运行 Native AOT，检查没有 CLR/JIT，也未加载 Skia/HarfBuzz。

正式标签 `v0.1.1` 固定于 `b45f5cb09664c55ee421f9617c9fc57fab56f6b7`。[正式流水线](https://github.com/donet-org-cn/SimdSkia/actions/runs/41831366323)的完整构建、编解码/标量/控件/兼容/PDF 回归、实际 NuGet 消费及下列矩阵全部通过，之后才启动可信发布。**十二个正式包均已公开下载核验通过**；本地与 GitHub CI 两次检查全部 DLL、宿主/桥接库、构建目标、发行文档和 nuspec，与冻结产物一致。另从唯一 NuGet.org 源和空缓存安装、构建并运行真实窗口通过，0 警告/错误。[NuGet](https://www.nuget.org/packages/SimdSkia.Avalonia.Desktop/0.1.1) · [GitHub 正式发行](https://github.com/donet-org-cn/SimdSkia/releases/tag/v0.1.1)。

| 冻结包运行平台 | 12.1.0 | 12.1.1 | 12.1.2 | 12.1.3 | 12.1.4 | 12.1.5 | Native AOT |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Windows x64 | 通过 | 通过 | 通过 | 通过 | 通过 | 通过 | 12.1.0、12.1.5 通过 |
| Linux X11 x64 | 通过 | 通过 | 通过 | 通过 | 通过 | 通过 | 12.1.0、12.1.5 通过 |
| macOS ARM64 | 通过 | 通过 | 通过 | 通过 | 通过 | 通过 | 12.1.0、12.1.5 通过 |

合计 **18 组控件及真实窗口、6 组 AOT**。三个平台全部使用 Windows 构建作业冻结的同一份适配器（SHA256 `B55573493E4D16540B861A017A2310E4DA3469B39C3DB3136BC184D1D81E33B2`），不是为不同 Avalonia 版本/OS 重编底层后自称二进制兼容。这里指 SimdSkia 适配器跨 Avalonia 版本加载，不是 SkiaSharp 强名称 DLL 的二进制替代承诺。

另在 Windows i7-8700K / GTX 1050 Ti 上，对正式冻结包复测 **12.1.0、12.1.5 × Direct2D、Vulkan** 四组实际 GPU 窗口，均有 132 条 GPU 命令、2 个完成批次；Direct2D 有原生交换链呈现。各例有 6 条显式 CPU 图层命令，动态 CPU 回退命令为 0；不能因此宣称所有效果都在 GPU。普通 Avalonia PackageReference（不手工引用 runtime DLL）的两端真实窗口也通过。

这四项 GPU 仅是兼容/提交完成/呈现冒烟测试，**不是性能跑分**；Linux GPU、macOS GPU/Metal 不在本次新增测试范围。

发布作业实际编解码包 1,417 项、PDF 包 18 项、实际包 AOT 窗口通过；JPEG 仍 `qualityPassed=false` / 21/24，仅接受此前公开的三个精确差异，新差异会阻止发布。原版 CPU/GPU 整页画质没有在本补丁重测，不把旧失败更改为通过。

冻结 CI 的桌面 nupkg 约 **7.69 MiB**（8,065,408 字节），因为保存六套官方宿主；每次构建仅选择一套，Windows 输出没有其他宿主版本目录及 macOS dylib。它是下载/缓存大小，不是 AOT 应用增大到六倍。Windows CI 缓存改用较短且按包清单哈希隔离的目录，避免 Native AOT 链接器的 MAX_PATH 限制；自定义 NuGet 根目录不带尾部分隔符也能正确构建。

[完整证据](evidence/avalonia-0.1.1-2026-11-04/results.json)包含每个平台的版本表、实际窗口/AOT 运行记录、冻结包清单、四组 GPU 与普通引用结果；30 个宿主包的 MIT 许可与额外依赖已逐一核查。

```powershell
pwsh ./tools/pack-release.ps1 -Version 0.1.1 -Output artifacts/avalonia-matrix/packages
pwsh ./tools/test-avalonia-versions.ps1 -Version 0.1.1 -Packages artifacts/avalonia-matrix/packages -NativeAot
# Linux 请在 xvfb-run 下执行第二条；CI 已包含这一步。
```

本补丁调整版本兼容和宿主选择，没有新的性能跑分。0.1.0 的 CPU/Vulkan/Direct2D 数据保留为历史结果，见 [性能报告](STABLE-0.1.0-2026-11-04.md)。严格 JPEG 21/24、原版 CPU/GPU 整页画质 0/3、0/12 的未通过状态不变；SkiaSharp 源码兼容子集与强名称二进制替换仍是不同承诺。
