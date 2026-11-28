# SimdSkia 0.1.13

使用 Avalonia 12.2.0 和 Fluent2 的桌面应用现在可以启动。NuGet 构建会选用与应用实际解析版本匹配的渲染适配器与 Win32、X11、macOS 宿主。发行包包含用于 Avalonia 12.1.x 和 12.2 的适配器，每个应用输出只包含所选版本。[Issue #13](https://github.com/donet-org-cn/SimdSkia/issues/13) · [Avalonia 版本兼容说明](AVALONIA-VERSION-COMPATIBILITY.md)

```sh
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.13
```

- 系统字体回退按码点选择候选面并按需加载。彩色 Emoji 增加 `sbix`、`COLRv0/CPAL` 和 `CBDT/CBLC`。实际覆盖取决于字体格式与文字整形能力。[#7](https://github.com/donet-org-cn/SimdSkia/issues/7) · [#8](https://github.com/donet-org-cn/SimdSkia/issues/8)
- CPU 投影复用相同遮罩的模糊结果，路径展平、光栅化与覆盖率复用暂存空间。缓存仅针对符合复用条件的内容。[#9](https://github.com/donet-org-cn/SimdSkia/issues/9) · [#10](https://github.com/donet-org-cn/SimdSkia/issues/10)
- Vulkan 和 Metal 增加径向、扫掠渐变和图像平铺。Direct2D 原生执行支持的径向渐变，其他画刷和不支持的参数组合仍可通过 CPU 绘制。[#11](https://github.com/donet-org-cn/SimdSkia/issues/11)
- PDFViewer 会随程序发布示例 PDF，支持 `--cpu` 对照启动。报告中的 Win10 整窗黑屏需要在原环境复测。[#12](https://github.com/donet-org-cn/SimdSkia/issues/12)
- 编解码器更新到 Sdcb.SimdCodecs 2.1.0。桌面 D-Bus 更新到 Tmds.DBus.Protocol 0.95.2。

## 验证与性能记录

Win64 的实际 NuGet 包通过 Avalonia 12.1.0、12.1.6 和 12.2.0 + Fluent2 控件与窗口检查。12.1.0 和 12.2.0 通过 NativeAOT 实窗检查。12.2 控件回归通过 14,250 项断言，编解码 NativeAOT 通过 204,421 项断言。发布流程另外检查三平台桌面包与 Android ARM64 Release APK。

以下数据来自 Win64 上将 0.1.12 与渲染优化候选构建进行的同场景比较。耗时和分配为各轮中位数。

| CPU 场景 | 0.1.12 耗时 | 候选构建耗时 | 0.1.12 分配/帧 | 候选构建分配/帧 |
| --- | ---: | ---: | ---: | ---: |
| 平移投影 | 53.04 ms | 33.29 ms | 1,977,867 B | 8,435 B |
| 内容变化的投影 | 53.18 ms | 48.65 ms | 1,977,867 B | 31,139 B |
| 路径光栅化，关闭分层 JIT | 8.96 ms | 8.58 ms | 1,339,993 B | 247,042 B |

同一个 Windows AOT 测试应用的 EXE 从 20,202,496 B 增至 20,252,160 B。这些数据覆盖独立用例，Apple M5 上报告的应用耗时、GC 提交量与 RSS 仍需要原场景实测。
