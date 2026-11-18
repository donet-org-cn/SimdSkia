# SimdSkia 0.1.7

0.1.7 降低 Direct2D 连续位图上传的 GC 压力，优化字体、几何命中、CPU 光栅化、PNG 编码和 Vulkan 初始化。十三个 NuGet 包统一为 0.1.7，面向 .NET 10。编解码依赖为 Sdcb.SimdCodecs 2.0.0。

```sh
dotnet add package SimdSkia --version 0.1.7
```

## 更新内容

- Direct2D 可按 Avalonia 位图的写入版本更新纹理，连续更新时省去整帧内容快照。普通可变像素缓冲可复用比较快照。OpenGL 和 Metal 优化了纹理上传时的像素格式转换。
- Avalonia 复用字体回退排序和描边结果，减少首次打开字体流时的缓冲复制。TrueType 直接读取已校验的字形索引。几何命中测试复用可被 GC 收回的轮廓，编辑几何后会重新生成轮廓。
- CPU 优化双线性采样、连续像素填充、竖向渐变和不透明图像合成，修正着色器使用独立斜切矩阵时的渐变采样。PNG 减少 Sub 滤镜和逐行选择滤镜的中间分配。
- Vulkan 仅在创建管线时读取所需 SPIR-V，之后可释放临时缓冲。本机 Vulkan 样例完整 GC 后少保留约 170 KiB 托管内存。

## Win64 本机实测

结果使用 Intel Core i7-12700、RTX 3060、.NET 10.0.14 和 Avalonia 12.1.5 的相同场景。对照为从 NuGet.org 获取的 0.1.6。

| 指标 | 0.1.6 | 0.1.7 候选 |
| --- | ---: | ---: |
| Direct2D 连续更新 960×720 位图的托管分配 | 2,765,322 B/帧 | 303 B/帧 |
| Direct2D 测试内的 Gen 2 回收次数 | 13 次/120 帧 | 0 次/120 帧 |
| Direct2D 完整 GC 后存活 LOH | 23.73 MiB | 13.18 MiB |
| NativeAOT Fluent 控件页托管分配 | 5,620 B/帧 | 3,366 B/帧 |
| NativeAOT Fluent 控件页 CPU | 0.519 ms/帧 | 0.519 ms/帧 |
| Avalonia NativeAOT 示例 EXE，Speed | 26,309,632 B | 26,280,448 B |

同一候选配置为 `OptimizationPreference=Size` 时，桌面示例 EXE 缩至 19.19 MiB，控件页工作集从 Speed 的 72.79 MiB 增至 76.75 MiB。五轮固定绘图和编解码混合场景的 CPU 与存活堆变化较小。100 次 Win64 NativeAOT 运行完成，同一场景和后端的前后像素哈希一致。[完整 CPU、内存和 NativeAOT 对照](PERFORMANCE-AOT-CPU-MEMORY-2026-11-18.md) · [各渲染后端与编解码复测](PERFORMANCE-ENGINE-AVALONIA-2026-11-18.md)
