# NuGet 预览版发布

版本仅接受 `数字.数字.数字-preview.数字`，首版为 `0.1.0-preview.1`。八个包：SimdSkia、Core、Cpu、Text、Imaging、Gpu、Compatibility、Avalonia。SimdSkia 是兼容层的依赖汇总包；Avalonia 应用通常只引用适配器，不需要 SK* 汇总包。

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
