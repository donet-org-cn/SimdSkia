# 默认 Avalonia 项目 AOT 体积实测：2026-10-26

**相同默认窗口，原版运行文件 36.53 MiB，SimdSkia 19.51 MiB，减少 17.02 MiB（46.59%）。DLL 合计由 17.97 MiB 降为 0；SimdSkia 的 EXE 大约增加 0.95 MiB。** 托管代码随 Native AOT 编译进入 EXE，不能只看 DLL 大小判断应用的完整体积。

[完整逐文件清单、哈希、包来源与验证记录](evidence/default-avalonia-aot-2026-10-26.json) · [两份实际项目和命令](../examples/DefaultAvaloniaAot/README.md) · [原版窗口截图](evidence/default-avalonia-aot-2026-10-26-original.png) · [SimdSkia 窗口截图](evidence/default-avalonia-aot-2026-10-26-simdskia.png)

## 测试口径

- Windows 11 x64，10.0.26300；.NET SDK **10.0.402**、目标 **net10.0 / win-x64**，Release、Native AOT、self-contained。
- 官方 `avalonia.app` 空白模板，内容只有 `Welcome to Avalonia!`。本机模板包为 Avalonia.Templates **12.0.0**，生成时显式指定 `--avalonia-version 12.1.4 --framework net10.0`；不声称这是最新模板。
- 两边 **Avalonia 12.1.4**、同一 Fluent 主题、同一 `Avalonia.Fonts.Inter 12.1.4` 与 `.WithInterFont()`，没有通过删除字体包来缩小 SimdSkia。
- 原版使用 `Avalonia.Desktop` / `.UsePlatformDetect()`；实际加载 SkiaSharp **3.119.4**、HarfBuzzSharp **8.3.1.3** 及 ANGLE。SimdSkia 使用 NuGet.org 已发布的 `SimdSkia.Avalonia.Desktop 0.1.0-preview.7` / `.UseSimdSkiaDesktop()`，默认 CPU，没有引用可选 GPU 包。
- App/MainWindow XAML、应用代码及 manifest 完全相同。保持默认应用的发布设置，只给两边加入相同 AOT/RID/self-contained 和依赖锁定属性；不指定额外 ISA、最小化优化、无堆栈或无反射设置。
- 默认项目没有应用内测试或计时器；窗口、截图和模块检查都来自外部进程。未使用之前的复杂控件/基准应用替代默认项目。
- 不手动删除 DLL，不压缩或修改二进制。原始 publish 目录完整保留；PDB 单列，运行包只排除 PDB。ZIP 为全部非 PDB 文件、DEFLATE level 9。

## 总体大小

单位 **MiB = 1,048,576 字节**。

| 项目 | 原版默认后端 | SimdSkia 默认后端 |
| --- | ---: | ---: |
| EXE | 18.56 | 19.51 |
| DLL 数量 | 3 | 0 |
| 全部 DLL 合计 | 17.97 | 0.00 |
| 其他运行文件 | 0.00 | 0.00 |
| **全部运行文件：EXE + DLL** | **36.53** | **19.51** |
| PDB 调试符号 | 164.74 | 67.11 |
| **原始 publish 目录，含 PDB** | **201.27** | **86.62** |
| 运行包 ZIP，不含 PDB | 14.58 | 7.67 |

精确运行字节：原版 **38,304,808 B**，SimdSkia **20,458,496 B**，减少 **17,846,312 B**。两个运行目录分别为四个文件与一个文件。发布目录包含 PDB 时，分别为 **211,049,512 B** 和 **90,831,872 B**。

## 具体少了哪些文件

以下文件在原版 publish 中存在，SimdSkia publish 中不存在，没有在统计前手动删掉：

| 文件 | 字节 | MiB | 用途 |
| --- | ---: | ---: | --- |
| `libSkiaSharp.dll` | 11,628,896 | 11.09 | Skia 原生绘图 |
| `libHarfBuzzSharp.dll` | 1,816,088 | 1.73 | 原生字体排版 |
| `av_libglesv2.dll` | 5,394,096 | 5.14 | ANGLE / 原版默认 Windows GPU 路径 |
| `libSkiaSharp.pdb` | 84,033,536 | 80.14 | Skia 调试符号 |
| `libHarfBuzzSharp.pdb` | 20,918,272 | 19.95 | HarfBuzz 调试符号 |

两边仍各有 `DefaultAvalonia.exe` 与 `DefaultAvalonia.pdb`。EXE 由 **19,465,728 B → 20,458,496 B**；应用 PDB 由 **67,792,896 B → 70,373,376 B**。没有新增外置运行文件。

`Avalonia*.dll`、`SimdSkia*.dll` 等托管程序集的存活代码及资源已经编译进入 EXE；没有以独立文件出现。Inter 字体资源同样保留在应用中，未另复制字体文件。Native AOT 会裁剪不可达代码，所以此结果不适合按“每多一个包固定增加多少”推算业务项目。

## 实际运行验证

- 两份 AOT 发布均成功，发布日志没有警告或错误。
- 均为 x64 原生 PE、GUI 子系统、无 CLR/IL header；运行进程没有 `coreclr.dll` / `clrjit.dll` 等模块。
- 两边都出现真实 `DefaultAvalonia` 窗口并显示默认文字，截图客户区均为 882×553；正常关闭返回 0。窗口截图已经人工查看。
- 原版进程确实加载 Skia、HarfBuzz 和 ANGLE；SimdSkia 进程不加载它们。依赖树没有原版 Skia/HarfBuzz 包或引擎源码 `ProjectReference`，核心包来源均核查为 NuGet.org。
- 所有 publish 文件记录精确字节与 SHA256；运行 ZIP 与全部非 PDB 文件逐项核对，无缺失文件。

这里只比较默认配置的发布体积，没有进行速度、CPU/内存或完整画质测试。原版为平台默认 GPU 配置，SimdSkia 为默认 CPU；增加 SimdSkia GPU 适配器后应另测体积。截图可见文字栅格化存在差异，不能把窗口正常显示视为逐像素一致或完整 Avalonia 兼容。Linux/macOS 的体积也需要分别发布测量。

## 构建与复现

从仓库根目录执行，使用新的输出目录：

```powershell
python tools/compare-default-avalonia-aot.py --output artifacts/default-avalonia-aot-new
```

脚本内部的发布命令为：

```powershell
dotnet publish examples/DefaultAvaloniaAot/Original/DefaultAvalonia.csproj -c Release -r win-x64 -o artifacts/default-avalonia-aot-new/publish/original
dotnet publish examples/DefaultAvaloniaAot/SimdSkia/DefaultAvalonia.csproj -c Release -r win-x64 -o artifacts/default-avalonia-aot-new/publish/simdskia
```

本次实际完整目录保存在 `artifacts/default-avalonia-aot-2026-10-26/publish/{original,simdskia}`；运行 ZIP 为同级的 `original-runtime.zip` 和 `simdskia-runtime.zip`。构建日志、截图、验证输出及原始结果均保留在该目录，报告对应本次实际发布文件，不套用历史测量。
