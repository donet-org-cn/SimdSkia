# 独立窗口最小 AOT 发布：2026-10-23

**实测最小的可运行成品：纯图形 EXE 1.172 MiB，中英文 EXE 1.311 MiB。** 这是本轮配置比较中通过完整窗口验证的最小结果，不是理论上的大小下限。生成的是普通 Windows x64 Native AOT EXE，没有 UPX 或其他压壳；运行内容与[首次测量](NATIVE-WINDOW-AOT-2026-10-23.md)一致。

[源码与使用方法](../examples/NativeWindow/README.md) · [发布配置](../examples/NativeWindow/Properties/PublishProfiles/MinimumSize.pubxml) · [独立验证脚本](../tools/test-native-window-minimum.py) · [逐文件大小、SHA-256、源码哈希及验证结果](evidence/native-window-minimum-2026-10-23.json)

## 最终成品

Windows 11 10.0.26300、.NET SDK 10.0.402 / Native AOT 10.0.13，与首次同机。MiB = 1,048,576 字节；完整部署包含 EXE 和 13,751 字节许可证，不含 PDB；ZIP 是完整部署的 DEFLATE level 9 压缩包。

| 内容 | EXE 字节 | EXE MiB | 完整部署 MiB | ZIP 字节 | ZIP MiB | EXE 较首次减少 |
|---|---:|---:|---:|---:|---:|---:|
| 纯图形 | 1,228,800 | 1.172 | 1.185 | 620,391 | 0.592 | 36.8% |
| 图形 + 中英文 | 1,374,720 | 1.311 | 1.324 | 692,347 | 0.660 | 32.0% |

成品在 `artifacts/native-window-minimum/publish/portable-shapes/NativeWindow.exe`、`portable-text/NativeWindow.exe`；同目录的 `native-window-minimum-shapes.zip`、`native-window-minimum-text.zip` 可分发。文字读取系统 `Windows\Fonts\msyh.ttc`，没有附带字体、Skia/HarfBuzz 或其他绘图 DLL。不需要安装 .NET 运行时。

## 怎样缩小，以及代价

修正首次无效的 `IlcOptimizationPreference`，使用 .NET 10 的 **`OptimizationPreference=Size`**。本次逐份检查编译响应文件，确认实际存在 `--Os`、`--dehydrate`。体积优先可能影响吞吐，本轮不宣称运行更快。[Microsoft AOT 优化说明](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing)

把 JSON、进程模块枚举、BMP 导出及内置测试状态机用编译条件移出最小 EXE，由外部 Python 验证实际发布程序。普通配置仍保留内置验证。系统字体路径改为只查询 Windows 目录，避免引入完整 Shell 特殊目录映射；Windows API 失败仍保留实际错误码。

最小配置启用 `InvariantGlobalization`，关闭 `StackTraceSupport`、`DebuggerSupport`、`EventSourceSupport`、`MetricsSupport`，并启用 `UseSystemResourceKeys`。**取舍是没有地区敏感的格式化/排序、完整异常堆栈、部分诊断，以及完整的 System.* 异常说明文字**。异常处理和参数验证仍保留；基本中英文和抗锯齿没有移除，且全部像素实测一致。这些是示例的显式发布配置，不改动 SimdSkia 库或其他应用的默认行为。[Microsoft 裁剪开关说明](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options)

正式配置还直接导入 Windows 自带的 user32/gdi32/kernel32，与动态解析版本比较后入选。两者仍使用同一 SimdSkia CPU 光栅化代码；GDI 仅复制完成的像素。命名发布配置会在项目计算编译常量及 ILC 条目之前解析，并检查实际生效参数，避免 SDK 较晚导入 `.pubxml` 导致配置表面启用、实际未裁剪。

## 比较所有候选

以下均使用最终源码，不把前期参数或截图脚本调试结果计入正式数据：

| 配置 | 纯图形 EXE 字节 | 中英文 EXE 字节 | 结果 |
|---|---:|---:|---|
| 有效 Size 优化 + 移除内置验证 | 1,530,368 | 1,639,936 | 通过 |
| 再开启最小运行库配置 | 1,274,880 | 1,375,744 | 通过 |
| 再移除反射元数据（实验） | 1,214,464 | 1,310,208 | **失败，不发布** |
| 最小运行库配置 + 直接 Windows 导入，保留必要元数据 | **1,228,800** | **1,374,720** | **通过，最终成品** |

`--reflectiondata:none` 的两个实验在窗口创建前以 **0xc0000602** 退出，不能因为文件更小就宣称成功。正式发布配置明确设置 `MinimalMetadata=false`。本次没有删减绘图算法、关闭抗锯齿、改色彩精度、强制更高 CPU 指令集或取消内存安全检查。

## 验证方法与结果

最终源码的 8 个配置均成功编译，没有编译/裁剪/AOT 警告；其中 6 个正常运行，另外 2 个反射实验失败。两个最终部署目录又各独立运行一次，合计 **8 次成功进程、32 帧完整图像比较**。

外部脚本直接启动最终 EXE，没有额外内置测试代码。它读取真实窗口客户区 DC，先等待显示动画稳定，再检查初始画面、调整大小、点击变绿、再次点击变蓝，最后正常关闭。每帧全部 RGB 与首次冻结 BMP 对照；只有 GDI 目标的未定义第四字节被归一化为 255，不对 RGB 使用误差容忍或修改。中文区域仍有 2,817 个非背景像素，圆边缘仍有 35 种颜色。原始 BMP 哈希和外部采样图哈希均保留。

额外验证 GUI PE 没有 CLR header，实际进程未加载 CoreCLR/JIT、Avalonia、SkiaSharp 或 HarfBuzz；ZIP 每个文件均与最终部署的哈希一致。最终成品没有命令行窗口。绘图库源码未修改。

测试脚本前期曾在初次显示动画尚未结束时立即 resize，截图包含桌面内容；修正为等待窗口稳定后再操作，随后才完成上述正式测量。这是体积/功能验证，**没有测启动时间、稳态吞吐或内存**，等待时间不被当作性能数据。该例只实现 Windows 简单自绘宿主，不含 Avalonia 控件、布局、数据绑定或 GPU。

## 一条命令发布

```powershell
dotnet publish examples/NativeWindow/NativeWindow.csproj -c Release -r win-x64 -p:PublishProfile=MinimumSize -p:IncludeText=true -o artifacts/my-small-window
```

纯图形改为 `-p:IncludeText=false`。需要完整地区格式化/诊断时去掉 `-p:PublishProfile=MinimumSize`，文件会增大。

已有冻结基线时，重新测量并选择最小成功配置：

```powershell
python tools/test-native-window-minimum.py --output artifacts/native-window-minimum/new-run
```

全新 checkout 先运行 `python tools/test-native-window-aot.py` 生成同场景基线，再运行最小验证。已有历史基线时不要覆盖；使用新目录可保留原始对照数据。
