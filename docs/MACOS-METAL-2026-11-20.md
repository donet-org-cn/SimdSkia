# macOS Metal 窗口呈现（2026-11-20）

本轮把 macOS 桌面从「只有 CPU」改为 **Metal 优先**：`.UseSimdSkiaDesktop()` 在 macOS 上让 Avalonia 原生宿主进入 Metal 模式，SimdSkia 直接画进宿主的 `CAMetalLayer`；Metal 不可用时回退到原来的 CPU framebuffer。此前已有但从未实机运行的 Metal 核心（`MetalCanvas` / `GRContext.CreateMetal`）也是第一次在真机上验证。

验证机器：Apple M5，macOS 27.2，.NET 10 运行时，Avalonia 12.1.6。没有在 Intel Mac / AMD GPU 上运行过。

## 结构

| 层 | 做法 |
| --- | --- |
| 呈现 | `MetalWindowTarget`：每帧从宿主取 drawable，场景画在一块**保留上一帧内容**的统一内存 RGBA 缓冲里，再用一个全屏三角形渲染通道写进 drawable（drawable 是 framebufferOnly，不能计算写入或 blit）。提交后不等待 GPU；宿主在同一队列上随后呈现。 |
| 零拷贝像素 | 窗口目标与离屏图层都是 shared storage `MTLBuffer`，用 `PixelBuffer.Wrap` 包成 `PixelBuffer`。GPU 直接画进去；需要 CPU 回退的命令先排空设备，再由托管光栅器画在**同一块内存**上，没有上传/读回。 |
| 一帧一个命令缓冲 | 根画布与所有图层编码进设备的同一个命令缓冲、同一个串行计算编码器，GPU 顺序就是编码顺序，图层之间不需要 CPU 等待。 |
| 设备图层 | `DrawingContext` 的透明度组、图层、蒙版、效果改由后端提供（`ILayerBackendProvider`）。Metal 后端给出 GPU 图层，合成时原地采样；CPU 后端行为不变。 |
| 设备效果 | `IFilterBackend`：高斯模糊与投影在 Metal 上计算。模糊使用 `ManagedFilters` 的同一组 14 位整数权重与透明延伸，**字节与 CPU 结果一致**；投影着色用 `PixelBuffer.Multiply` 的取整，最后的 SourceOver 为浮点（±1）。 |
| 解析覆盖率 | 轴对齐矩形、图片四边形、硬裁剪矩形在 kernel 里按面积计算；`DrawRectangle` 的圆角矩形与实心描边带一个可选的 `AnalyticShape`，Metal 用圆角矩形 SDF（描边为外环减内环）。其它后端忽略它，继续用路径。 |
| 缓存 | 路径覆盖率缓存改为设备级、LRU；形状完全在目标内时以平移的小数部分为键，整数平移与不同图层原点复用同一份字节（`CoverageClip.Offset`）。掩码上传按存储身份缓存，2 帧未用即释放；图片 10 秒未用释放。文字图集在 Metal 下把位置量化到 1/4 像素（与 Skia 的亚像素文字相同），根画布与图层共享图集。 |
| 内存回收 | 共享缓冲池闲置 1 秒释放；界面静止时没有帧，由计时器回收。每个宿主图形上下文只建一个设备（合成器重试/重建目标、多窗口共享）。 |
| 线程 | Avalonia.Native 会在 UI 线程同步渲染（resize）、平时在渲染线程渲染；合成器锁保证不并发，因此窗口设备标记为 host-serialized，不再要求固定线程。其它用法（`GRContext`）保持线程绑定。 |
| 启动 | Metal 选中时，启动预热线程在系统默认设备上离屏画一帧（矩形、圆角、描边、路径、图片、透明度、投影、文字图集），在窗口出现前完成 JIT 与驱动管线预热（约 30 ms，后台）。设备与程序在绑定窗口 Metal 表面**之前**创建，编译失败时窗口保持未绑定，回退走纯 CPU 路径。 |

## 验证

```sh
dotnet build SimdSkia.sln -c Release
# Metal 核心与原版 SkiaSharp Metal 逐像素对照
dotnet run --project tests/MetalContext/Managed -c Release -- artifacts/metal-context/managed
dotnet run --project tests/MetalContext/Original -c Release -- artifacts/metal-context/original
# 真实窗口：Metal 选中、首帧/resize/按钮路由（--cpu 为 CPU 对照）
dotnet run --project examples/CrossPlatform -c Release -- --smoke --controls --expect-gpu --output artifacts/metal-window
dotnet run --project examples/CrossPlatform -c Release -- --smoke --controls --cpu --output artifacts/cpu-window
dotnet run --project tests/GpuPreferred -c Release
dotnet run --project tests/AvaloniaControls -c Release -- --frames 5 --output artifacts/avalonia-controls
dotnet run --project tests/Avalonia -c Release -- baseline/fonts/ZenOldMincho-Regular.ttf
```

| 检查 | 结果 |
| --- | --- |
| MetalContext（10 个场景 × 2 种原点） | 122 项真机检查通过；与原版 SkiaSharp 3.119.4 Metal 的通道差最大 1 |
| 控件示例窗口 Metal vs CPU 呈现帧 | 0.03% 像素不同，最大差 9；`SelectedBackend=Metal native layer` |
| GpuPreferred / AvaloniaControls / Avalonia | 21 / 14149 / 596 项全部通过 |

### 真实应用

在一个真实的三栏 Avalonia 12 桌面应用（第三方控件库、带阴影的面板、Markdown 文本、动画侧栏）里，用真窗口动效冒烟逐帧计时，与同一应用的 Avalonia.Skia（Metal）交替运行，各 3 轮：

| | Avalonia.Skia | SimdSkia CPU（改前） | SimdSkia Metal |
| --- | --- | --- | --- |
| 侧栏滑出 | p50 16.7 ms | 共 4 帧，每帧约 600 ms | p50 16.7 ms，p95 约 17.5 ms |
| 展开/收起/侧栏 p95 | 17.1–17.9 ms | — | 17.2–17.7 ms |
| 峰值 RSS | 314–337 MB | 706–862 MB | 310–341 MB |
| 进程 CPU 时间 | 3.1–4.3 s | 约 11 s | 3.3–4.9 s |
| 空闲时物理内存（动画结束 3 s） | 367 MB | — | 360 MB |

Metal 与 SimdSkia CPU 对同一场景的最终画面：圆角 SDF 与扫描线覆盖率在小圆角的个别角点相差最多 16；文字因 1/4 像素量化有不超过 1/8 像素的位置差，其余一致。CPU 回退（`SIMDSKIA_METAL_FAIL=init|draw`）的呈现帧与 CPU 参考逐像素相同。180 次窗口 resize 压力后仍选中 Metal，内存不增长。

改前 CPU 路径的主要耗时是投影的 CPU 模糊（约 47%）与每帧整窗提交；Metal 路径中这两项都在 GPU 上，剩余 CPU 工作主要是非圆角路径与首次出现字形的光栅化。

## 诊断

- `SIMDSKIA_METAL_TRACE=1`：标准错误输出设备编译耗时、窗口目标创建/释放、首帧与慢帧耗时、每 30 帧的共享缓冲/缓存用量、Metal 故障详情、启动预热结果。
- `SIMDSKIA_METAL_FAIL=init|draw`：注入初始化失败或首帧失败，用于检查 CPU 回退。
- `SimdSkiaGpuDiagnostics.SelectedRenderer` 为 `Metal native layer` 或带原因的 CPU 回退；`LastInitializationFailure` 记录失败原因。

## 限制

- 只在 Apple Silicon（M5）上验证；Intel Mac、AMD GPU、外接 HDR / 广色域显示器未测。drawable 为 sRGB 像素格式时用解码后写入，未实测。
- 椭圆与任意路径（图标、虚线描边等）的覆盖率仍由 CPU 生成，已缓存复用；LCD 文字、自定义混合、颜色/图像滤镜仍走 CPU 回退（统计在 `FallbackReasons`）。
- 首次出现的字形仍在 CPU 光栅化：冷启动第一帧约 33 ms（其中约 40% 为字形）。
- 「首帧绘制失败后回退」只验证了呈现帧内容；没有屏幕录制权限，未截取窗口实际显示。初始化失败发生在绑定 Metal 表面之前，不受影响。
- 没有新增自动化测试项目；GPU 硬件检查依赖上面的手动命令。
