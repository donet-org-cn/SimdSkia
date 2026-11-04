# Avalonia 12.x 版本兼容验证（0.1.1）

版本范围：渲染适配器以 Avalonia 12.1.0 为最低编译基线；NuGet 接受 `[12.1.0,13.0.0)`。目前公开版本为 12.1.0–12.1.5，全部进入矩阵。未知未来 12.x 需加入测试并补充桌面宿主，不能承诺未来接口不变；13.x 不在本版范围。

## 真实发现与修复

仅把 `UseSimdSkia` 的精确版本检查放开是不够的。以同一份初始候选包实测，12.1.0/12.1.1 窗口通过，12.1.2 抛 `TypeLoadException`：旧版 `Avalonia.Win32.PopupImpl` 缺少新增的 `SetHitTestVisible` 实现。

最终方案：渲染适配器仍编译一次；桌面包内保存官方 MIT 许可的六版 Win32/X11/macOS 托管宿主，`buildTransitive` 在构建时读取 **实际解析的 Avalonia.Base.dll** 版本，自动选择对应宿主。仅选中的六份托管宿主 DLL 进入应用输出/AOT 链接；其他版本不会复制到应用。NuGet 下载包因此增大，应用输出不会携带六套宿主。未知宿主版本在构建时明确报错，不能静默挑旧版。

macOS 同样选择匹配的官方 Avalonia.Native 托管宿主和 libAvaloniaNative.dylib 窗口桥接库；核心渲染、字体仍为 C#。不把这些官方宿主称作自研 C# 窗口系统，也不调用 Google Skia 绘图。NuGet 家族中的其他直接 Avalonia 引用应选用相同版本。

构建采用 .NET SDK 10.0.403 / runtime 10.0.14；旧 10.0.13 AOT 编译器被 NuGet 高危漏洞审计拦截，未关闭审计绕过，详见 [微软公告](https://github.com/advisories/GHSA-p3jp-9v83-76w9)。

## 验证方法与状态

同一份实际打包的适配器在六版中复用，不逐版本重编；每次检查包内/运行目录 SHA256、实际 Avalonia 程序集版本，以及六个配套宿主的版本。每版执行 9,750 项控件、像素、几何、文字、SIMD、生命周期断言，另启动真实窗口验证帧提交、非空像素、resize 与按钮事件。最低/最新版额外发布并运行 Native AOT，检查没有 CLR/JIT，也未加载 Skia/HarfBuzz。

Windows 本地与 Windows/Linux/macOS CI 结果待本次矩阵结束后补记。发布工作流要求三平台六版本矩阵及最低/最新版 AOT 成功，才允许 NuGet 可信发布。包中 README/CHANGELOG 会随发布冻结，公开包发布后再逐一下载核对。

```powershell
pwsh ./tools/pack-release.ps1 -Version 0.1.1 -Output artifacts/avalonia-matrix/packages
pwsh ./tools/test-avalonia-versions.ps1 -Version 0.1.1 -Packages artifacts/avalonia-matrix/packages -NativeAot
# Linux 请在 xvfb-run 下执行第二条；CI 已包含这一步。
```

本补丁调整版本兼容和宿主选择，没有新的性能跑分。0.1.0 的 CPU/Vulkan/Direct2D 数据保留为历史结果，见 [性能报告](STABLE-0.1.0-2026-11-04.md)。严格 JPEG 21/24、原版 CPU/GPU 整页画质 0/3、0/12 的未通过状态不变；SkiaSharp 源码兼容子集与强名称二进制替换仍是不同承诺。
