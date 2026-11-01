# 完整接入 Sdcb.SimdCodecs：.NET 10 本地试用与真实前后对照

日期：2026-11-01。用户确认允许 **.NET 自带原生压缩** 后，当前源码已完整引用 `Sdcb.SimdCodecs [1.2.0]`，目标框架改为 .NET 10 / C# 14。
这是实际实现和本地试用，**不是公开 NuGet preview.9 的更新，也不是全部质量/兼容门槛通过的正式版本**。公开 preview.9 的包和标签没有改写。

后续补测已完成：[当前源码与原版 SkiaSharp 的 CPU/GPU/编解码对照](SKIA-COMPARISON-2026-11-01.md)，123 个计时进程、CPU/内存及性能图齐全。本文下方的性能表仍是新旧 SimdSkia 前后对照，不冒充原版结果。

## 已经接入的内容

- Imaging 只直接引用完整汇总包，15 个同版本 MIT 包进入其传递图；生产 `JpegLibrary` 引用已移除。JPEG/PNG 像素编解码、GIF LZW/合成均由新库执行，没有隐蔽回退到旧像素算法。
- 原有 `PngCodec` / `JpegCodec` / `GifImage` 名称保留为适配：预算、CRC、合法过滤器、调色板索引、ICC/gamma→sRGB、像素格式/stride/Alpha 和资源生命周期继续符合引擎合同。通用 ManagedZlib 仍服务 PDF/元数据，不是旧图片解码回退。
- 支持 Bitmap/Image PNG、JPEG、GIF、BMP、ICO 编码；品牌 `ImageCodec.Encode` 另有 TIFF、QOI、Netpbm。WebP 无编码器，明确报错。JPEG2000/JBIG2 仅解码，不能把缺失编码改成 PNG 后假报成功。
- `SKBitmap.Decode` 接入新格式；`SKCodec` 正确报告 PNG/JPEG/GIF/BMP/ICO/WebP 格式，WebP 动画有真实矩形/时长/混合/处置元数据及随机帧读取；GIF 保留原有元数据和原版一致的处置/回退结果。
- `ImageCodec.OpenFrames` 暴露 WebP 动画、多页 TIFF、GIF、ICO 图像及静态图。返回的 PixelBuffer 独立存活；dispose reader 后调用会抛异常。PNG/JPEG 的帧视图使用同一颜色适配，避免 gAMA/ICC 被绕过。
- TIFF/QOI/JPEG2000/JBIG2 没有对应的基线 SKEncodedImageFormat，`SKCodec.Create` 对无法表达的枚举明确拒绝；通过品牌 API 读取，不伪造“PNG”枚举。
- 几何、CPU 光栅、合成、字体和排版不改后端；PNG/TIFF 可调用 .NET 系统压缩，仍不使用 Google Skia/libpng/libjpeg 等第三方原生绘图库。

## 验证与未通过项

| 检查 | 本地 Windows x64 结果 |
| --- | --- |
| 整仓 Release | 0 警告 / 0 错误 |
| 新增格式/动画/颜色/stride/释放集成 | 1,416 断言通过 |
| 上述集成关闭全部硬件指令 | 1,416 通过 |
| 真实 Native AOT | 1,417 通过，验证非动态代码运行时 |
| 独立现有解码夹具 | 41/41 通过，包括先前被拒绝的渐进 CMYK、Display-P3 |
| 原有图片/颜色/损坏输入/压缩 | JPEG 原版门槛前 369 项通过 |
| 原版 JPEG 联合回读 | **21/24 组过门槛，3 组失败；最大通道差 2，原门槛 1 未放宽** |
| 精确优化/损坏 PNG | 471,569 项通过；修复新路径接受畸形过滤器/调色板索引的问题 |
| 真实 Avalonia 控件 | 9,750 项通过 |
| 文字链路 / 字形批次 | 595 / 60 通过 |
| 基础 / 既有兼容扩展 / PDF | 36 / 37 / 18 通过 |
| CPU / Direct2D / Vulkan 实际窗口 | 各呈现 2 帧；GPU 各 132 命令、2 个完成批次、0 条不支持命令回退；另有 6 条 CPU 图层命令 |
| 本地实际 nupkg | 12 包检查通过；隔离桌面窗口和实际包的 1,416 项编解码调用通过，无源码引擎引用 |

JPEG 的差异是编码+解码联合回读，不能仅归因某一环节；原测试继续以失败退出，没有删除或改判。
例如原版像素 `(22,11,5)`，新路径 `(22,11,7)`。不承诺与原版逐像素一致，也不把性能通过当成画质通过。

新库的 PNG 显示接口比旧合同宽松：本轮回归实际发现畸形行可被接受。适配新增有界 BCL 流式校验，检查过滤器、调色板索引和精确膨胀长度；像素仍交给新库解码。
这会额外解压验证一次，**开销完整包含在下表**，没有用不严格的裸解码器数据冒充完成接入后的性能。

仍有明确边界：带四通道 CMYK ICC 的 JPEG 需要原始 CMYK 样本，新显示接口没有暴露，当前会明确拒绝（属于旧路径相比之下的限制）；浮点/HDR 色调映射及未支持的颜色信令拒绝，不默默裁成 sRGB。
JPEG2000 的独立无损样例与 JBIG2 规范空白页通过，不代表所有字典/算术区域语料均验证。尚未在本轮新跑 Linux/macOS，不扩大成本机未测平台结论。

## 完整接入后的性能：ms/张

Windows x64、i7-8700K（6 核/12 线程）、.NET 10.0.13 Release JIT、AVX2。
旧端使用切换前冻结的编译产物（JpegLibrary/原 PNG）；新端使用当前适配后的 `ImageCodec/JpegCodec/PngCodec`。
双方同一运行时、同一压缩输入、相同输出所有权，文件读取不计时；每例三进程，串行交替，预热至少 0.5 秒、采样至少 1.2 秒，取中位数，合计 60 进程。
编译、AOT 和窗口测试没有与性能采样并行。不是原版 SkiaSharp/GPU 对照。

两个自制不透明 1920×1080 UI/纹理图案，**不是通用照片集**。JPEG 编码同为质量 90 / 4:4:4 / baseline / 默认 Huffman；PNG 同为 Optimal、单片，文件压缩比不同。
速度倍数=旧耗时/新耗时，小于 1 为变慢。

| 场景 | 操作 | 旧实现 | 完整接入后 | 速度倍数 |
| --- | --- | ---: | ---: | ---: |
| ui | decode-jpeg444 | 90.709 | 4.086 | 22.20× |
| ui | decode-jpeg420 | 149.492 | 3.553 | 42.08× |
| ui | decode-png | 1.941 | 1.873 | 1.04× |
| ui | encode-jpeg444 | 66.006 | 7.766 | 8.50× |
| ui | encode-png | 61.013 | 76.948 | 0.79× |
| texture | decode-jpeg444 | 98.592 | 9.012 | 10.94× |
| texture | decode-jpeg420 | 157.831 | 8.206 | 19.23× |
| texture | decode-png | 39.452 | 43.433 | 0.91× |
| texture | encode-jpeg444 | 85.835 | 9.832 | 8.73× |
| texture | encode-png | 243.705 | 433.065 | 0.56× |

JPEG 解码约 **10.9–42.1 倍**，编码约 **8.5–8.7 倍**；PNG 解码 UI 基本持平、纹理耗时增加约 **10%**。
PNG 编码耗时增加约 **26%–78%**，同时文件更小：UI 约 109.7 KB→12.6 KB，纹理约 6.31 MB→2.63 MB，均为无损输出。
这是一项兼容校验/压缩比/速度的实际取舍，不是“引用后所有操作都更快”。本轮没有重测完整 Avalonia/GPU 帧率、启动白屏或业务 OCR 全链路。

## 分配与内存

以下是旧→新。分配包括适配对象/完整编码数组；工作集与 Private 为每进程采样结束快照，不是长期峰值或常驻应用总内存。
原始 CPU ms/张、帧数、模块和全部运行值在 [证据 JSON](evidence/simdcodecs-integration-2026-11-01/results.json)。

| 场景 / 操作 | 分配 KiB/张 | 工作集 MiB | Private MiB |
| --- | ---: | ---: | ---: |
| ui / decode-jpeg444 | 195.17 → 0.88 | 52.59 → 45.39 | 31.25 → 20.54 |
| ui / decode-jpeg420 | 161.30 → 0.88 | 51.41 → 45.22 | 30.15 → 20.27 |
| ui / decode-png | 31.00 → 8.68 | 60.32 → 53.84 | 37.10 → 29.59 |
| ui / encode-jpeg444 | 699.12 → 193.88 | 61.88 → 80.60 | 39.88 → 59.53 |
| ui / encode-png | 8555.60 → 55.78 | 80.27 → 50.10 | 58.36 → 27.25 |
| texture / decode-jpeg444 | 748.11 → 0.90 | 59.98 → 45.80 | 38.68 → 21.02 |
| texture / decode-jpeg420 | 716.06 → 0.90 | 55.96 → 45.64 | 34.68 → 20.85 |
| texture / decode-png | 859.29 → 8.76 | 62.61 → 52.18 | 39.96 → 30.07 |
| texture / encode-jpeg444 | 2788.10 → 733.46 | 87.69 → 87.20 | 68.43 → 65.32 |
| texture / encode-png | 36789.71 → 10811.26 | 206.80 → 63.78 | 191.26 → 41.08 |

JPEG 解码和 PNG 的这组内存结果更低；JPEG 编码 UI 场景工作集约 61.9→80.6 MiB、Private 39.9→59.5 MiB，不能声称所有场景更省。
1,417 项 AOT 测试控制台程序约 8.4 MiB，仅作为可执行代码真实性记录，不是默认 Avalonia 窗口体积或生产包增量比较。

## 如何实际试用

安装 .NET 10 SDK。公开 NuGet preview.9 不包含此改动，源码引用或本地包才会启用。

```powershell
dotnet build SimdSkia.sln -c Release
dotnet run --project tests/CodecIntegration -c Release
dotnet run --project examples/CrossPlatform -c Release -- --controls
```

既有应用把目标框架改为 `net10.0`，移除该应用对已发布适配包的直接引用，再加源码引用：

```xml
<PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
<ItemGroup>
  <ProjectReference Include="F:\simdSkia\src\SimdSkia.Avalonia.Desktop\SimdSkia.Avalonia.Desktop.csproj" />
</ItemGroup>
```

启动仍使用 `.UseSimdSkiaDesktop()`；如已有 `.UseSimdSkiaDesktop(options => options.UseGpu(...))` 配置，继续沿用。
Avalonia 仍固定 **12.1.4**，不是对其他 Avalonia 或 SkiaSharp 版本的兼容承诺。

也可生成已验证的本地包：

```powershell
./tools/pack-codecs-trial.ps1
./tools/test-codecs-trial-package.ps1
```

目录为 `artifacts/simdcodecs-integration-2026-11-01/packages`，12 个包统一 **`0.1.0-codecs-trial.1`**。
应用项目目录可放以下 NuGet.Config（保留自己的其他必要源）：

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="SimdSkiaTrial" value="F:\simdSkia\artifacts\simdcodecs-integration-2026-11-01\packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

```powershell
dotnet add package SimdSkia.Avalonia.Desktop --version 0.1.0-codecs-trial.1
# 原先直接引用其他 SimdSkia 包的，也统一使用这个本地版本。
```

完整质量验收 `tools/verify.ps1` 仍因原版 JPEG 门槛失败退出；这是一份可运行的试用，而非清零所有兼容差距。
没有上传 NuGet、推送发行标签或改写历史成绩。
