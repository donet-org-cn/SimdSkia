# SimdSkia 0.1.5

0.1.5 新增逐帧 GIF 动画编码，修复 `actionj.ttf` 绘制中断，并升级到 Sdcb.SimdCodecs 2.0.0。十三个 NuGet 包统一为 0.1.5，目标框架为 .NET 10。

```sh
dotnet add package SimdSkia --version 0.1.5
```

- `GifAnimationWriter` 可以直接把 `SKBitmap` 或 `PixelBuffer` 逐帧写入流。每一帧可设置延迟及帧处置方式，`loopCount: 0` 表示无限循环，正数表示总播放次数。GIF 透明度只有透明和不透明两档。跨播放器使用透明背景时，首帧也应包含透明像素。[#3](https://github.com/donet-org-cn/SimdSkia/issues/3) · [GIF 示例](../README.md#逐帧写入-gif-动画)
- TrueType 字形微调指令失败时会回退到未微调的轮廓。已用报告里的原始 `actionj.ttf` 验证 `A7B9` 可以完成绘制。思源黑体的 CFF 版本仍未支持，可选用含所需字形的静态 `glyf` TrueType 字体。[#2](https://github.com/donet-org-cn/SimdSkia/issues/2) · [字体选择](../README.md#选择中文字体)
- 编解码器升级为 Sdcb.SimdCodecs 2.0.0。如果应用直接调用 Sdcb API，升级时请同步迁移相关调用。[上游迁移说明](https://github.com/sdcb/SimdCodecs/blob/v2.0.0/docs/migrating-to-2.0.md)
- 本版还包含 0.1.4 之后的像素处理、采样、字形缓存和 Avalonia 内存优化。[性能报告](PERFORMANCE-2026-11-10.md) · [第二轮验证](VALIDATION-PERFORMANCE-2026-11-10-ROUND2.md)

本地验证覆盖 Release 构建、TrueType 回归、编解码集成、标量模式和 Windows Native AOT。Pillow 独立核对了 GIF 的帧像素、透明度、延迟和循环次数。原版 JPEG 的严格像素对照仍为 21/24，通过发布检查中三个已有差异项的核验。[NuGet 发布记录](NUGET-PREVIEW.md)
