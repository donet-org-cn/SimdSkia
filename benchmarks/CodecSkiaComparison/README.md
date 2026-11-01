# 与原版 SkiaSharp 的隔离编解码对照

`Managed` 只引用当前 SimdSkia 兼容层；`Original` 只引用精确的 SkiaSharp 3.119.4。两者编译同一 worker，禁止同进程加载两个兼容程序集。默认使用 .NET 10 Release JIT。

```powershell
dotnet build benchmarks/CodecSkiaComparison/Managed -c Release
dotnet build benchmarks/CodecSkiaComparison/Original -c Release
python tools/run-codec-skia-comparison.py --output artifacts/skia-codecs-new `
  --fixtures artifacts/simdcodecs-evaluation-2026-11-01/fixtures --forks 3 --seconds 2
```

夹具目录需要 `fixtures.json`、两图案的 `.rgba` / `.png` / `-444.jpg` / `-420.jpg`。可通过 `benchmarks/CodecEvaluation prepare` 生成新夹具，重新生成的 JPEG 不保证与此前冻结样本相同；同轮双方必须共用输入并记录哈希。

每例独立进程串行轮换，预热 1 秒。解码含新输出所有权和释放；编码含完整输出 byte[]。输入 IO、source bitmap 构造与质量统计不计时。双方 RGBA8888 / Unpremul，JPEG 编码明确质量 90 / 4:4:4 / baseline，PNG 原版 AllFilters/zlib6、SimdSkia 公开 Optimal/serial。

计时后保存捕获，由同一原版解码器回读双方编码；验证 PNG 无损与 JPEG 实际 SOF/采样，报告 JPEG 误差、PSNR 和文件大小。并不将质量 90 解释为相同码率/相同熵编码策略；不修改原有 Imaging 的质量门槛。

WS/Private 为计时后进程快照，peak WS 包含预热；分配统计不包括原版 native malloc。输入/程序集/源码哈希、CPU、运行时与模块列表保留。没有进行 GPU、AOT 或实际窗口 FPS 测量。

合并 CPU/GPU/编解码报告工具 `tools/report-skia-comparison.py` 另外需要 Matplotlib。它只在性能计时结束后离线画图，不进入计时进程或产品依赖。当前结果见 [2026-11-01 报告](../../docs/SKIA-COMPARISON-2026-11-01.md)。
