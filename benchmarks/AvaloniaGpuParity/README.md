# Avalonia GPU 完成帧对照

固定 Avalonia 12.1.4 / SkiaSharp 3.119.4，编译同一份真实 Fluent 控件页与滚动列表，使用同一个字体文件。原版仅在 `Original` 隔离进程加载 Skia/HarfBuzz/ANGLE；`Managed` 使用生产 SimdSkia 的 Vulkan 或 Direct2D。ANGLE 原生包属于原版测试依赖，不进入生产包。

```powershell
dotnet build benchmarks/AvaloniaGpuParity/Original -c Release
dotnet build benchmarks/AvaloniaGpuParity/Managed -c Release
python tools/run-gpu-parity.py --output artifacts/gpu-parity/new-run --forks 3 --seconds 5 --warmup 3
```

使用新的输出目录。运行期间不要编译、修改生产源文件或并行运行其他性能测试。原始时间、外部进程内存采样、CPU 周期、截图、设备信息及源码/二进制 SHA-256 全部保存。

每帧包含真实 Avalonia 绘制、提交、GPU 完成和 BGRA8888 全帧回读。原版 `ReadPixels` 等待完成，SimdSkia 的 `Flush` 等待 fence/event 与回读。分别报告记录命令和完成部分的时间；不能只比较提交调用的时间。

这是 .NET 10 **JIT 离屏回读**测量，不是 Native AOT，也不是交换链呈现或显示器 FPS。生产 GPU 路径中存在的 C# 字形图集与软件图层照常计入，回退计数保留。两个原版 GPU 后端均需运行，并按各场景中较快的原版作速度门槛比较。

退出 2 表示测量执行成功但验收失败；查看 `gate.json` 和 `quality.json`，不能视作发布成功。当前版本的整页文字画质差距仍需修复。
