# Avalonia Native AOT：体积、速度、CPU 与内存实测

交付两份真实 Win32 软件窗口程序：共享相同中英文控件、Fluent 主题、字体、布局与基准代码，分别使用 SimdSkia 和原版 Avalonia.Skia。两边均实际 Native AOT 发布和运行，不使用 JIT 数字替代 AOT。

**应用与对照测量已完成，完整 Avalonia 替换仍未通过画质验收。** 本文保留失败的像素门槛与原版默认配置；这些速度数字不代表画质已经相同。

实测重点：v3 的 1000×720 控件页 SimdSkia 1.396 ms/帧，原版默认 1.059 ms/帧，耗时比 1.32×；通用 AOT 的 SimdSkia 为 3.422 ms/帧。v3 工作集约 85.7 vs 60.5 MiB。完整运行目录约 25.68 vs 41.10 MiB。60 FPS 的时间计数 CPU% 波动明显，以下同时列范围与独立 CPU 周期，不能只凭中位数百分比判断胜负。

## 实测环境与口径

- Windows 11 10.0.26300，Intel Core i7-8700K（6 核 / 12 线程），物理 RAM 31.93 GiB。SDK 10.0.402，Native AOT runtime 10.0.13。
- 固定 Avalonia / Avalonia.Skia / Fluent 12.1.4，SkiaSharp 3.119.4，HarfBuzzSharp 8.3.1.3；同一 ZCOOL XiaoWei Regular OFL TTF（6,313,808 B）。两边均使用 Win32 Software，不测 GPU。
- 通用 x64 与 x86-64-v3（含 AVX2）分别发布、分别测量。通用版记录为 AVX2=false / Vector128；v3 版为 AVX2=true / Vector256。**v3 提高最低 CPU 要求，不能当成通用 x64 二进制分发。**
- 每种 ISA 5 个场景 × 3 种后端/透明度模式 × 3 个独立进程，共 45 次；两种 ISA 总计 90 次。各进程预热 2 秒、测量 5 秒，轮换顺序，未并行运行对照。表格为三个进程的中位数，JSON 另有 min/max 与逐进程值。
- 不限帧测试计时为同步 RenderTargetBitmap 绘制和指定的列表平移更新，未计 PNG 保存、窗口提交或 DWM；吞吐 FPS 不能当成屏幕 FPS。60 FPS 模式异步让出 UI 线程，另包含真实窗口软件呈现、调度、光标等进程 CPU。
- CPU 时间来自整个进程 TotalProcessorTime；整机占用率=进程 CPU 时间/墙钟/12，单核比例另存原始数据。另由父进程在测量起止信号处用 [QueryProcessCycleTime](https://learn.microsoft.com/en-us/windows/win32/api/realtimeapiset/nf-realtimeapiset-queryprocesscycletime) 独立读取全部线程的 CPU 周期，不把周期数按假定频率换算成时间或百分比。工作集是物理驻留，私有提交不是物理 RAM，二者不能相加。父进程每 25ms 独立采样稳态内存。
- 启动为父进程创建 exe 至第一次离屏绘制完成的 READY 信号，包含字体、主题与窗口初始化；并非 DWM 首次显示，未重启系统或清空文件缓存，属于文件缓存已热的进程启动测量。
- SimdSkia 完整组混合固定开启；原版默认可用逐次绘制透明度优化，另测显式完整组。原版默认与 SimdSkia 的透明组语义不同，完整组一列用于更接近的行为对照。

表格的“原版默认”仅指 **CPU 软件后端默认透明度处理**，两边都显式选用 Win32 Software；本文未测试 GPU，也未测试平台自动选择的其他桌面渲染配置。

## 发布体积

单位 MiB（1,048,576 B）。发布目录保留字体、许可证及上游复制的 XML；所有 PDB 都单列排除出运行包，未只统计 exe。ZIP 为全部非 PDB 文件的 DEFLATE level 9。

| ISA | 后端 | exe | 原生绘图/字体 DLL | 字体 | 完整运行目录 | ZIP | 调试符号 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| portable | SimdSkia | 19.53 | 0.00 | 6.02 | 25.71 | 10.96 | 71.45 |
| portable | 原版 | 18.68 | 12.82 | 6.02 | 41.13 | 16.34 | 169.32 |
| v3 | SimdSkia | 19.49 | 0.00 | 6.02 | 25.68 | 10.94 | 71.69 |
| v3 | 原版 | 18.65 | 12.82 | 6.02 | 41.10 | 16.33 | 169.57 |

exe 大小和总部署体积是两个不同指标。原版还需要 libSkiaSharp.dll 与 libHarfBuzzSharp.dll；SimdSkia 不携带这些组件。上游 XML 文档并非运行必需，逐文件清单保留其大小，另看 exe+DLL 可排除文档复制差异。字体由两边共同携带，不能算作 SimdSkia 独有负担。

**公平裁掉两边全部 XML 文档后**，通用版部署为 SimdSkia 25.57 vs 原版 37.53 MiB；v3 为 **25.53 vs 37.50 MiB，约少 31.9%**。该数字仍保留全部字体、许可证、exe 和原版 DLL；原始发布目录与 ZIP 没有改动，便于核查。

## 不限帧：实际绘制速度

绘制 ms 是每帧纯绘制平均；CPU ms/帧来自整个进程，P95 是单帧绘制分位数。

| ISA | 场景 | 后端 | 绘制 ms/帧 | P95 ms | 绘制吞吐 帧/s | CPU ms/帧 | 整机 CPU % | 分配 KiB/帧 |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| portable | 控件 1000×720 | SimdSkia | 3.422 | 4.067 | 292.2 | 3.422 | 8.28 | 16.67 |
| portable | 控件 1000×720 | 原版默认 | 1.030 | 1.466 | 970.9 | 1.023 | 8.26 | 7.09 |
| portable | 控件 1000×720 | 原版完整组 | 3.202 | 3.883 | 312.3 | 3.181 | 8.28 | 7.27 |
| portable | 控件 1500×1080 | SimdSkia | 7.147 | 8.489 | 139.9 | 7.121 | 8.30 | 18.56 |
| portable | 控件 1500×1080 | 原版默认 | 1.991 | 2.860 | 502.1 | 1.973 | 8.25 | 7.09 |
| portable | 控件 1500×1080 | 原版完整组 | 7.491 | 8.568 | 133.5 | 7.438 | 8.27 | 7.27 |
| portable | 列表 1000×720 | SimdSkia | 1.288 | 1.583 | 776.5 | 1.276 | 8.31 | 3.90 |
| portable | 列表 1000×720 | 原版默认 | 0.439 | 0.568 | 2277.9 | 0.438 | 8.31 | 6.60 |
| portable | 列表 1000×720 | 原版完整组 | 0.453 | 0.682 | 2205.6 | 0.451 | 8.20 | 6.60 |
| v3 | 控件 1000×720 | SimdSkia | 1.396 | 2.042 | 716.4 | 1.383 | 8.25 | 16.67 |
| v3 | 控件 1000×720 | 原版默认 | 1.059 | 1.732 | 943.9 | 1.053 | 8.28 | 7.09 |
| v3 | 控件 1000×720 | 原版完整组 | 4.181 | 5.399 | 239.1 | 4.141 | 8.28 | 7.27 |
| v3 | 控件 1500×1080 | SimdSkia | 3.029 | 3.831 | 330.0 | 3.022 | 8.31 | 18.54 |
| v3 | 控件 1500×1080 | 原版默认 | 2.064 | 2.961 | 484.3 | 2.045 | 8.33 | 7.09 |
| v3 | 控件 1500×1080 | 原版完整组 | 8.746 | 9.630 | 114.3 | 8.714 | 8.30 | 7.27 |
| v3 | 列表 1000×720 | SimdSkia | 0.476 | 0.607 | 2101.9 | 0.476 | 8.33 | 3.90 |
| v3 | 列表 1000×720 | 原版默认 | 0.442 | 0.651 | 2263.0 | 0.436 | 8.31 | 6.60 |
| v3 | 列表 1000×720 | 原版完整组 | 0.442 | 0.645 | 2261.5 | 0.441 | 8.31 | 6.60 |

## 60 FPS 更新：CPU 与内存

低 CPU 占用需要固定负载。不限帧忙循环通常占满约一个逻辑处理器，单看其 CPU % 无法判断每帧成本；以下单列 60 FPS 模式。实测 Windows 时间计数在短突发/休眠交替负载下有明显跨进程波动，因此列出全部三次范围，并增加独立周期计数；不使用某次偶然偏低百分比宣称胜出。周期值计入此模式中整个进程的窗口呈现工作，不能与不限帧纯离屏吞吐混用。定时器 deadline 统计保留在原始数据，不等同于显示器漏帧。

| ISA | 场景 | 后端 | 实际更新/s | 绘制 ms/帧 | 整机 CPU % 中位数 [min,max] | CPU 百万周期/帧 | 工作集 MiB | 私有提交 MiB | 工作集/物理 RAM % |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| portable | 控件 60 FPS | SimdSkia | 59.9 | 3.608 | 3.17 [1.46, 3.59] | 13.62 | 84.7 | 65.1 | 0.259 |
| portable | 控件 60 FPS | 原版默认 | 59.9 | 1.411 | 1.19 [0.00, 2.52] | 5.59 | 60.0 | 35.8 | 0.183 |
| portable | 控件 60 FPS | 原版完整组 | 59.9 | 3.461 | 0.55 [0.21, 4.63] | 13.08 | 58.8 | 34.9 | 0.180 |
| portable | 列表 60 FPS | SimdSkia | 59.9 | 1.697 | 2.81 [1.41, 2.84] | 18.60 | 77.1 | 60.4 | 0.236 |
| portable | 列表 60 FPS | 原版默认 | 60.0 | 0.963 | 2.03 [1.46, 3.65] | 15.67 | 56.1 | 38.3 | 0.172 |
| portable | 列表 60 FPS | 原版完整组 | 59.9 | 0.961 | 2.00 [1.64, 2.06] | 15.65 | 56.2 | 38.3 | 0.172 |
| v3 | 控件 60 FPS | SimdSkia | 59.9 | 1.623 | 1.56 [0.18, 1.95] | 6.33 | 84.7 | 65.0 | 0.259 |
| v3 | 控件 60 FPS | 原版默认 | 59.9 | 1.421 | 1.43 [0.78, 2.34] | 5.62 | 60.0 | 35.8 | 0.183 |
| v3 | 控件 60 FPS | 原版完整组 | 60.0 | 4.711 | 3.28 [1.30, 4.19] | 17.51 | 57.2 | 33.3 | 0.175 |
| v3 | 列表 60 FPS | SimdSkia | 59.9 | 1.011 | 1.92 [0.86, 3.10] | 13.48 | 78.1 | 61.3 | 0.239 |
| v3 | 列表 60 FPS | 原版默认 | 59.9 | 1.017 | 2.36 [1.56, 2.39] | 16.89 | 55.0 | 33.8 | 0.168 |
| v3 | 列表 60 FPS | 原版完整组 | 59.9 | 1.033 | 1.33 [1.20, 4.06] | 17.30 | 55.2 | 34.0 | 0.169 |

## 启动与不限帧内存

以下使用每个配置三次独立进程的中位数；启动排在吞吐预热之前。首绘是窗口/字体初始化之后的第一次离屏绘制，不等于整个进程启动。内存含整个 Win32 程序与最多 0.4 MiB 的帧计时缓冲。

| ISA | 场景 | 后端 | 启动至首帧 ms | 首次绘制 ms | 工作集 MiB | 私有提交 MiB | OS 峰值工作集 MiB | 采样稳态工作集 MiB |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| portable | 控件 1000×720 | SimdSkia | 184.0 | 16.6 | 85.5 | 65.9 | 85.5 | 85.1 |
| portable | 控件 1000×720 | 原版默认 | 158.7 | 11.4 | 60.4 | 36.2 | 60.5 | 60.0 |
| portable | 控件 1000×720 | 原版完整组 | 136.9 | 14.1 | 60.1 | 36.1 | 65.1 | 60.5 |
| portable | 控件 1500×1080 | SimdSkia | 186.0 | 32.7 | 95.7 | 76.2 | 95.7 | 95.2 |
| portable | 控件 1500×1080 | 原版默认 | 154.2 | 14.7 | 63.7 | 39.7 | 63.8 | 63.2 |
| portable | 控件 1500×1080 | 原版完整组 | 168.2 | 21.1 | 62.1 | 38.1 | 74.0 | 64.6 |
| portable | 列表 1000×720 | SimdSkia | 144.4 | 9.4 | 75.6 | 59.0 | 75.6 | 75.1 |
| portable | 列表 1000×720 | 原版默认 | 111.0 | 2.8 | 56.2 | 34.8 | 56.2 | 55.6 |
| portable | 列表 1000×720 | 原版完整组 | 105.1 | 2.8 | 56.1 | 34.7 | 56.1 | 55.5 |
| v3 | 控件 1000×720 | SimdSkia | 186.4 | 14.0 | 85.7 | 66.0 | 85.7 | 85.3 |
| v3 | 控件 1000×720 | 原版默认 | 162.6 | 11.3 | 60.5 | 36.3 | 60.5 | 60.1 |
| v3 | 控件 1000×720 | 原版完整组 | 153.5 | 15.2 | 59.9 | 35.8 | 64.9 | 60.0 |
| v3 | 控件 1500×1080 | SimdSkia | 190.6 | 34.6 | 95.6 | 76.1 | 95.6 | 95.2 |
| v3 | 控件 1500×1080 | 原版默认 | 153.6 | 14.5 | 63.7 | 39.7 | 63.7 | 63.2 |
| v3 | 控件 1500×1080 | 原版完整组 | 155.3 | 23.8 | 61.5 | 37.5 | 73.4 | 65.2 |
| v3 | 列表 1000×720 | SimdSkia | 144.5 | 8.1 | 75.6 | 58.9 | 75.6 | 75.2 |
| v3 | 列表 1000×720 | 原版默认 | 112.8 | 2.7 | 56.0 | 34.6 | 56.0 | 55.5 |
| v3 | 列表 1000×720 | 原版完整组 | 111.4 | 2.8 | 56.0 | 34.6 | 56.0 | 55.5 |

## 画质与运行验证

固定整页门槛最大通道误差 ≤32、RMSE ≤2。没有降低 AA、修改字体、放宽门槛或排除文字来获取更快成绩。两种 ISA、两个原版透明度配置均比较原始 RGBA；完整组与默认组的结果分别记录。

| ISA | 场景 | 原版配置 | 最大误差 | RMSE | 结果 |
| --- | --- | --- | ---: | ---: | --- |
| portable | 控件 1000×720 | 原版默认 | 96 | 2.434 | FAIL |
| portable | 控件 1000×720 | 原版完整组 | 96 | 2.423 | FAIL |
| portable | 控件 1500×1080 | 原版默认 | 109 | 2.320 | FAIL |
| portable | 控件 1500×1080 | 原版完整组 | 109 | 2.307 | FAIL |
| portable | 列表 1000×720 | 原版默认 | 92 | 3.087 | FAIL |
| portable | 列表 1000×720 | 原版完整组 | 92 | 3.087 | FAIL |
| portable | 控件 60 FPS | 原版默认 | 96 | 2.434 | FAIL |
| portable | 控件 60 FPS | 原版完整组 | 96 | 2.423 | FAIL |
| portable | 列表 60 FPS | 原版默认 | 92 | 3.087 | FAIL |
| portable | 列表 60 FPS | 原版完整组 | 92 | 3.087 | FAIL |
| v3 | 控件 1000×720 | 原版默认 | 96 | 2.434 | FAIL |
| v3 | 控件 1000×720 | 原版完整组 | 96 | 2.423 | FAIL |
| v3 | 控件 1500×1080 | 原版默认 | 109 | 2.320 | FAIL |
| v3 | 控件 1500×1080 | 原版完整组 | 109 | 2.307 | FAIL |
| v3 | 列表 1000×720 | 原版默认 | 92 | 3.087 | FAIL |
| v3 | 列表 1000×720 | 原版完整组 | 92 | 3.087 | FAIL |
| v3 | 控件 60 FPS | 原版默认 | 96 | 2.434 | FAIL |
| v3 | 控件 60 FPS | 原版完整组 | 96 | 2.423 | FAIL |
| v3 | 列表 60 FPS | 原版默认 | 92 | 3.087 | FAIL |
| v3 | 列表 60 FPS | 原版完整组 | 92 | 3.087 | FAIL |

- 通用 AOT 与 v3 AOT 的固定姿态输出对比：15/15 逐字节相同，完整细节见 [ISA 输出核查](aot-comparison/isa-output-check.json)。
- 90 个发布 exe 均通过 AOT 标志、真实 Fluent 控件模板、固定 ImageBrush 像素、字体加载与模块检查；父脚本校验 PE 没有 CLR/IL header，另检查所有进程未加载 CoreCLR/JIT。AOT 的 runtimeconfig 可让中间 IL/JIT 进程也报告 IsDynamicCodeSupported=false，普通 apphost 也没有 CLR header，因此两者均不能单独证明 Native AOT。模块校验在负向探针发现此陷阱后补强，已对全部 90 份原始模块记录重新核查；没有修改受测 exe 或绘制计时代码。SimdSkia 检查确有 Win32 软件帧呈现且未加载原版原生绘图/字体组件；原版进程确认实际加载 Skia/HarfBuzz DLL。
- 适配层精确保留固定 Avalonia 12.1.4 内部 IRef<T>.Item getter 元数据，解决 Native AOT IL2075；只抑制这一个有明确 DynamicDependency 的反射分析警告，未全局关闭裁剪/AOT 诊断。新示例不使用无头宿主的反射初始化入口，也不使用反射 JSON 序列化。
- 额外运行现有控件回归：**8109/8109 PASS**；生产依赖审计 PASS。此回归是 .NET 8 JIT 测试，不混入本文 AOT 性能值。
- 全解决方案 Release 构建 PASS，0 警告、0 错误；四次实际 AOT 发布均无剩余编译/裁剪警告，原始构建日志已存档。
- [负向与产物审计](aot-comparison/validation.json)：真实 JIT 携带伪 AOT 标志仍被父脚本拒绝，IL PE 与非法测量时长被拒绝；4 个发布 exe 哈希与测量清单一致，4 个运行 ZIP 逐文件哈希/CRC 完整，受测源码及冻结的原始测量脚本哈希一致。当前父脚本只补强 AOT 验证并保留未来测量脚本副本，未修改受测程序。
- 画质失败时测量脚本仍保存完整数据并返回 **2**；构建/运行失败才返回 1。页面像素差距、通用字体排版和 Vulkan 窗口等此前缺口仍未因此解决。

截图为 v3 发布 exe 实际生成，差异图显示每像素最大通道误差 ×4（红色）：

| 场景 | SimdSkia | 原版默认 | 原版完整组 | 差异 ×4（与完整组） |
| --- | --- | --- | --- | --- |
| 控件 1000×720 | [PNG](aot-comparison/images/controls-1-throughput-simd.png) | [PNG](aot-comparison/images/controls-1-throughput-original-default.png) | [PNG](aot-comparison/images/controls-1-throughput-original-full.png) | [PNG](aot-comparison/images/controls-1-throughput-difference-x4.png) |
| 控件 1500×1080 | [PNG](aot-comparison/images/controls-1.5-throughput-simd.png) | [PNG](aot-comparison/images/controls-1.5-throughput-original-default.png) | [PNG](aot-comparison/images/controls-1.5-throughput-original-full.png) | [PNG](aot-comparison/images/controls-1.5-throughput-difference-x4.png) |
| 列表 1000×720 | [PNG](aot-comparison/images/scroll-1-throughput-simd.png) | [PNG](aot-comparison/images/scroll-1-throughput-original-default.png) | [PNG](aot-comparison/images/scroll-1-throughput-original-full.png) | [PNG](aot-comparison/images/scroll-1-throughput-difference-x4.png) |

## 复现与产物

```powershell
powershell -NoProfile -File tools/fetch-avalonia-font.ps1
python tools/run-avalonia-aot.py --output artifacts/aot-comparison/portable
python tools/run-avalonia-aot.py --isa v3 --publish artifacts/aot-comparison/publish-v3 --output artifacts/aot-comparison/v3
python tools/report-avalonia-aot.py artifacts/aot-comparison/portable artifacts/aot-comparison/v3
```

预期：当前画质仍 FAIL，两个测量命令返回 2；仍可执行第三条报告命令。`--no-build` 仅用于已完成同 ISA 发布的目录。运行交互程序时不带 `--hidden/--output`：

```powershell
& artifacts/aot-comparison/publish/simd/AvaloniaAot.SimdSkia.exe
& artifacts/aot-comparison/publish/original/AvaloniaAot.Original.exe
```

完整发布物在 `artifacts/aot-comparison/publish` 与 `publish-v3`；本轮正式测量目录为 `artifacts/aot-comparison/portable-measured` 与 `artifacts/aot-comparison/v3-measured`，各有不含 PDB 的 `simd-runtime.zip` / `original-runtime.zip`，保留字体与许可证。初步测量/中止批次不计入本报告的 90 次。
源代码、精确依赖和单进程用法见 [示例说明](../examples/AvaloniaAot/README.md)。JSON/CSV 存档如下：

- 通用版：[汇总 CSV](aot-comparison/portable/summary.csv)、[45 次逐进程数据](aot-comparison/portable/results.json)、[体积与文件 SHA](aot-comparison/portable/sizes.json)、[环境与 exe SHA](aot-comparison/portable/manifest.json)。
- v3 版：[汇总 CSV](aot-comparison/v3/summary.csv)、[45 次逐进程数据](aot-comparison/v3/results.json)、[体积与文件 SHA](aot-comparison/v3/sizes.json)、[环境与 exe SHA](aot-comparison/v3/manifest.json)。
- 每个运行目录保留 PNG/RGBA、stdout、25ms 资源采样；存档 source-files.json 记录未提交源码快照 SHA，与旧 HEAD 清楚区分。
- AOT 构建、8109 项回归与依赖隔离的实际日志位于 [logs](aot-comparison/logs/regression.txt)；每种 ISA 另存 run-harness.py.txt，是实际测量时父脚本的冻结副本。
- [.NET 官方 Native AOT 部署与工具链要求](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)；[运行时官方 AOT 指令集说明](https://github.com/dotnet/runtime/blob/main/src/coreclr/nativeaot/docs/optimizing.md)：默认指令集与显式提高最低硬件要求的配置应分别评估。
