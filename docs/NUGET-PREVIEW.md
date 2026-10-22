# NuGet 预览版发布

## 第五预览版 0.1.0-preview.5

**已公开发布，11 个包均已实际下载，DLL 和 README 与发布 CI 产物逐字节一致。** [可信发布流水线](https://github.com/hnlyf168/SimdSkia/actions/runs/39746371476)全部成功，完成构建、全部包内容检查、真实 Native AOT 像素/窗口验证、隔离包消费、上传及公开下载核对。标签 `v0.1.0-preview.5` 固定在 `c12c3db16dda9b5fa7505a146bf1b948d3311934`。下载 SHA、实际包和测试证据见 [发布记录](evidence/nuget-preview5-2026-10-22.json)。

推荐桌面应用统一升级到 `0.1.0-preview.5`，包含本轮 Vulkan 优化；旧 preview.4 内容不变。安装 [SimdSkia.Avalonia.Desktop](https://www.nuget.org/packages/SimdSkia.Avalonia.Desktop/0.1.0-preview.5)，GPU 按需另加 [SimdSkia.Avalonia.Gpu](https://www.nuget.org/packages/SimdSkia.Avalonia.Gpu/0.1.0-preview.5)，完整接入代码见 [首页](../README.md)。

```shell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-preview.5
dotnet add package SimdSkia.Avalonia.Gpu --version 0.1.0-preview.5
```

本版纳入 [Vulkan 专项优化](VULKAN-OPTIMIZATION-2026-10-22.md)：有界覆盖率缓存、保守分块、宽绕数与专用计算管线、显存输入、BGRA 直接输出、依赖同步，以及可选 GPU 时间戳诊断。11 个包统一版本；Avalonia 继续锁定 12.1.4。实测窗口 P50 22.250→2.316 ms，CPU 为 2.067 ms，内存没有同比改善；完整 API/画质仍未通过。渲染源码与报告中实测提交相同，仅变更版本、依赖锁定和说明，保留实际测量二进制哈希。

本机真实 preview.5 包的 CPU/Direct2D/Vulkan 控件窗口均通过，GPU 均为 0 条不支持命令的回退，另外明确记录 6 条 CPU 图层命令。[三平台托管回归](https://github.com/hnlyf168/SimdSkia/actions/runs/39746371489)、[三平台 JIT/AOT/nupkg 窗口](https://github.com/hnlyf168/SimdSkia/actions/runs/39746343255)及 [Windows 双后端 AOT](https://github.com/hnlyf168/SimdSkia/actions/runs/39746342939)全部成功；已下载核对三平台九份实际窗口记录。单次包窗口启动/完成时间含冷初始化，不能作为性能比较。

## 第四预览版 0.1.0-preview.4

**已公开发布。** 本次已实际下载全部 11 包，DLL 和 README 与原 CI 产物逐字节一致，[发布证据](evidence/nuget-preview4-2026-10-22.json)保留最初超时及后来成功下载的记录。[发布流水线](https://github.com/hnlyf168/SimdSkia/actions/runs/39657888167)的构建、包内容检查、AOT/nupkg 实际窗口与上传已完成；当时 NuGet 尚未完成索引，20 分钟公开下载校验超时，因此原流水线仍显示失败，不改写历史。源码标签 `v0.1.0-preview.4` 固定在 `5eef27d936dc72cb0dd61c7074292b3192bf9a30`，旧标签与包不改写。

本轮包含实测 CPU 热点优化、GPU 有界纹理内容复用、Direct2D Opaque Alpha 修正和高 DPI ImageBrush 38/40 修正；不宣称完整兼容。独立 [性能与兼容报告](PERFORMANCE-COMPATIBILITY-2026-10-22.md)包含原版 CPU/Vulkan、新旧窗口、CPU/内存/分配和失败画质项目。三平台回归、实际窗口/AOT/nupkg 与 Windows 双后端 AOT 已全部通过，详见 [验证](VALIDATION.md)及 [9 条 CI 记录](evidence/desktop-preview4-ci-2026-10-22.json)。

preview.4 包与标签保持原内容，不包含后续 Vulkan 专项优化；该优化随 preview.5 发布。桌面应用安装 `SimdSkia.Avalonia.Desktop`，可选 `SimdSkia.Avalonia.Gpu`，使用方式见 [首页](../README.md)。

## 第三预览版 0.1.0-preview.3

**已公开发布。** 11 个 `0.1.0-preview.3` 包均已实际下载，DLL 和 README 与发布 CI 产物逐字节一致，[发布流水线 39648463806](https://github.com/hnlyf168/SimdSkia/actions/runs/39648463806)全部成功。修复 GPU 文字图集在覆盖裁剪下的重叠合成顺序与滚动缓存；preview.2 标签和包保持原样。推荐安装 [SimdSkia.Avalonia.Desktop](https://www.nuget.org/packages/SimdSkia.Avalonia.Desktop/0.1.0-preview.3)，GPU 另加 [SimdSkia.Avalonia.Gpu](https://www.nuget.org/packages/SimdSkia.Avalonia.Gpu/0.1.0-preview.3)，两者版本均为 `0.1.0-preview.3`；完整使用方式在[首页](../README.md)。公开下载核对见[发布证据](evidence/nuget-preview3-2026-10-22.json)。

已从已发布的包内 README 提取完整程序，在仅 NuGet.org 源、全新缓存下完成 CPU 和 Direct2D 配置的恢复与编译，两者均 0 警告/0 错误。另从 NuGet.org 恢复 CrossPlatform 控件示例，检查全部 SimdSkia 依赖来自包，在本机 GTX 1050 Ti 上实际创建、呈现及缩放 Direct2D 窗口通过。原始记录为 `artifacts/nuget-public-preview3-demo` 和 `artifacts/nuget-public-preview3-desktop`。

## 第二预览版 0.1.0-preview.2

已公开：11 个包均完成公开下载及 CI DLL/README 内容核对，[发布流水线](https://github.com/hnlyf168/SimdSkia/actions/runs/39646305172)成功。该版本存在重叠字形与覆盖裁剪的图集顺序问题，现已在 preview.3 修复；推荐使用新版本。旧标签和包不改写。

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

上传后还需等待 NuGet 验证和索引。后续流水线会轮询实际公开 nupkg 下载，并逐字节核对其中的 DLL、README 与 CI 产物；NuGet 对 ZIP 容器增加仓库签名，因此不把整个 nupkg 的签名后哈希与未签名文件强行对比。等待上限 20 分钟，超时会明确失败，不以 PUT 成功代替可安装验证。首版发布的上传日志为 [GitHub Actions 39628597818](https://github.com/hnlyf168/SimdSkia/actions/runs/39628597818)。

## 本机验证

```powershell
powershell -NoProfile -File tools/pack-preview.ps1 -Version 0.1.0-preview.1
powershell -NoProfile -File tools/test-preview-consumer.ps1 -Version 0.1.0-preview.1 -SystemFonts
```

`artifacts/nuget-preview/manifest.json` 保存所有包的 SHA256、字节数和依赖。`artifacts/nuget-consumer/metrics.json` 保存真实 Native AOT 消费验证结果，`window.png` 是实际渲染截图。所用包缓存按当前 manifest 哈希隔离，避免旧的同版本本地包造成误判。

包预览标记不改变已有兼容性缺口，验收和性能限制继续见 [VALIDATION](VALIDATION.md)。单帧的消费测试只验证功能与发布可用性，不是性能基准。
