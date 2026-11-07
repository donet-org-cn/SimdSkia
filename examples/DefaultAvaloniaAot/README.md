# 默认 Avalonia 桌面模板的 AOT 体积对比

两份相同的 `avalonia.app` 空白应用，窗口内容保持 `Welcome to Avalonia!`。最初使用官方 Avalonia.Templates 12.0.0，以 `--avalonia-version 12.1.4 --framework net10.0` 生成，随后给两边添加相同的 Native AOT / win-x64 / self-contained 属性。当前两边统一升级到 Avalonia 12.1.5，使用公开 SimdSkia 0.1.2。

- `Original`：默认 `Avalonia.Desktop 12.1.5`、`.UsePlatformDetect()`。
- `SimdSkia`：替换为公开的 `SimdSkia.Avalonia.Desktop 0.1.2`、`.UseSimdSkiaDesktop()`，默认 CPU 后端。
- 两边均保留 Fluent、`Avalonia.Fonts.Inter` 和 `.WithInterFont()`；App/MainWindow XAML、应用代码及 manifest 相同，没有加入应用内测试代码或额外控件。
- 使用 NuGet.org 包，没有源码 `ProjectReference`。目录内的 NuGet.Config 不使用本机其他私有包源。
- 没有设置最小体积参数、关闭堆栈或删除发布 DLL；保留默认符号，另行统计 PDB。

在有 Windows 桌面与 .NET 10 Native AOT 工具链的机器上，从仓库根目录运行，输出目录须为新的目录：

```powershell
python tools/compare-default-avalonia-aot.py --output artifacts/default-avalonia-aot-new --avalonia-version 12.1.5 --simdskia-version 0.1.2
```

也可以分别发布：

```powershell
dotnet publish examples/DefaultAvaloniaAot/Original/DefaultAvalonia.csproj -c Release -r win-x64 -o artifacts/default-avalonia-aot-new/publish/original
dotnet publish examples/DefaultAvaloniaAot/SimdSkia/DefaultAvalonia.csproj -c Release -r win-x64 -o artifacts/default-avalonia-aot-new/publish/simdskia
python tools/compare-default-avalonia-aot.py --output artifacts/default-avalonia-aot-new --skip-publish
```

验证脚本会短暂打开两份实际 AOT 窗口并截图，检查 GUI PE/无 CLR header、已加载模块、窗口非空与正常退出。然后统计完整文件及 SHA256，并打包全部非 PDB 文件。它不修改或裁剪 EXE/DLL；截图检查不等于完整画质一致性验收。

本机 2026-11-07 公开0.1.2包复测：原版运行目录 **36.55 MiB**，SimdSkia **24.02 MiB**，减少 **34.27%**；DLL 合计仍 **17.97 MiB → 0**。两者真实 AOT 窗口、模块隔离、逐文件哈希和完整运行 ZIP 验证通过。[本次报告与证据](../../docs/PERFORMANCE-STABLE-0.1.2-AOT-2026-11-07.md)。

历史保留：2026-10-26 的 preview.7 实测为36.53/19.51 MiB，[旧报告](../../docs/DEFAULT-AVALONIA-AOT-SIZE-2026-10-26.md)。当前本库程序更大；SDK、Avalonia、依赖和功能不同，不能沿用旧数字宣传新版体积。这里仅测默认窗口大小，不沿用复杂控件的速度/大小，也不包含可选GPU/PDF包；性能对照另有相同CPU后端夹具。
