# SimdSkia 0.1.1 发行证据

`results.json` 为总表，固定标签来源和实际冻结 DLL 哈希。三个 OS 目录保存六版本测试和实际 JIT/AOT 窗口记录；gpu 为 Windows GTX 1050 Ti 的四组冻结包冒烟测试，不能作为速度跑分。`public-verification.json` 为十二个公开包的实际下载内容检查；两次独立下载（本地和 GitHub CI）结果一致。

`StandardConsumer.csproj` 只用普通 NuGet 引用，链接仓库窗口示例，不含引擎 ProjectReference。纯公开源复现：

```powershell
dotnet restore docs/evidence/avalonia-0.1.1-2026-11-04/StandardConsumer.csproj --source https://api.nuget.org/v3/index.json --packages artifacts/reproduce-public-011
dotnet run --project docs/evidence/avalonia-0.1.1-2026-11-04/StandardConsumer.csproj -c Release --no-restore -- --smoke --controls --font baseline/fonts/ZCOOLXiaoWei-Regular.ttf
```

需要 .NET SDK 10.0.403+ 和固定许可字体。Linux 窗口使用 X11/Xvfb；macOS 保留已允许的官方窗口桥接库。`jpeg-parity.json` 明确保留 qualityPassed=false / 21/24，不把已披露的严格画质失败改判为通过。
