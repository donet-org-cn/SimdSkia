# 启动白屏、CPU 与内存优化：2026-10-22

后续优化及新一轮同机测量见 [图片采样、画刷缓存与窗口报告](OPTIMIZATION-ROUND6-2026-10-22.md)；本页数字保持原始测量。为释放本地测试盘空间，本页大体积 RGBA 已无损移至 `artifacts/startup-round5/frame-evidence.zip` 和 `remaining-frame-evidence.zip`，对应 `*-archive-manifest.json` 保存逐文件 SHA256，恢复时解压到原目录即可；公开证据和图片不变。

本轮实现已提交 [1c6af37](https://github.com/hnlyf168/SimdSkia/commit/1c6af3701631a6c2f75563c0d18bb4c732bc3ce9)，对照基线为上一轮内存优化后的 `7bcd362`，不是最早版本。系统字体控件 CPU 平均耗时 **1.008→0.800 ms/帧**，工作集 **111.9→94.8 MiB**，Private **67.0→48.5 MiB**。原版默认配置本次为 **0.911 ms、88.4 MiB、42.8 MiB**。这是指定场景的计时，原版画质门槛仍未通过，不能据此宣称整体性能或兼容性已超过 SkiaSharp。

**本机同场景的原版也有短暂白屏。** 外部屏幕采样估计其中位时长约 **95 ms**；SimdSkia CPU 从 **280 ms 缩短到 218 ms**，仍然更长。没有隐藏窗口、延迟 Show、增加启动遮罩或用背景色冒充首帧。

**这些改动尚未发布到 NuGet。** 已公开的 `0.1.0-preview.5` 不包含本轮及上一轮内存优化；验证工作流生成的 nupkg 仅用于消费测试。

公开证据：[完整逐进程 JSON、帧哈希与 CI 记录](evidence/startup-performance-2026-10-22.json) · [CPU CSV](evidence/startup-performance-2026-10-22.csv) · [窗口 CSV](evidence/startup-window-2026-10-22.csv)。

## 实际修改

1. 安装字体改为纯 C# 按需分页读取。TTC 各字面共享文件源；每个文件最多缓存 128 个 16 KiB 页面，常用 cmap/hmtx 常驻，glyf 按实际字形读取。小表缓存另有每字面 2 MiB 上限，大表显式导出由调用方持有。没有用其他字体替代微软雅黑，也没有删减字形或排版数据。
2. 字体数据仍做目录、偏移、loca、cmap 等校验。Windows 读句柄禁止就地改写、允许原子路径替换；Unix 在读取新数据前后检查长度与修改时间。修改或截断明确报错。返回的表内存始终为托管存储，不暴露可能失效的映射地址。流可独立读取、定位和 Dispose；TTC 导出逐表复制并保持原校验和，避免先复制整份集合。
3. 显式 `FontFiles` 和公开 span 构造保留完整快照语义；这一优化主要降低自动发现的系统字体开销。缓存上限不是整个字体对象的总内存上限：已解析字形、必要映射/度量表及调用方导出的数据仍占内存。
4. 光栅器排序和移动边索引，减少每个子扫描行的结构体复制；小路径使用有界栈缓冲，大路径保留缓冲池。首帧热点直接生成优化代码。16×16 覆盖采样、浮点求交顺序、填充规则和合成精度保留。
5. 实测发现圆角裁剪内部的长不透明段仍逐像素检查。现在用 `Span.IndexOfAnyExcept` 扫描相同覆盖值，完整覆盖段直接走原有复制/合成内核；边缘继续使用原覆盖率。保留显式标量路径及不支持硬件指令时的回退。

没有添加第三方依赖、字体资源、机器码生成器或可执行内存。

## 测量口径

- 本机 Windows x64 / Core i7-8700K，12 逻辑处理器 / GTX 1050 Ti；Avalonia **12.1.4**、原版 SkiaSharp **3.119.4**，Release JIT。CPU 离屏使用 .NET 8.0.32，真实窗口使用 .NET 10.0.13。没有在正式测量时打开 profiler、逐帧截图或 Vulkan 串行时间戳诊断。
- CPU：同一个真实 Fluent 控件场景、布局和字体，`RenderTargetBitmap`；预热 2.5 秒，每进程 600 帧，3 轮轮换进程顺序。固定字体 36 进程、系统字体 12 进程，共 28,800 个计时帧。
- 固定字体为相同的 ZCOOLXiaoWei-Regular.ttf。系统字体双方明确选 Microsoft YaHei UI；正常字面的 glyf/cmap/head/hmtx/OS/2 表 SHA256 一致，审计在计时后进行。
- 原版默认透明度与显式完整透明度组分别保留。完整组更接近这里的组语义，但成本不同，不能只挑较慢的原版模式比较。
- 启动：35 个全新进程，真实可见 Win32 窗口，CPU/Direct2D/Vulkan 优化前后各 5 次，原版 CPU 5 次。相同场景外围有相同蓝色标记，外部观察者仅从屏幕读取像素，不发送重绘请求。
- 工作集、Private 都是实际进程指标；GC 后托管堆不包含原版的原生分配，不能直接当作总内存比较。CPU 全机占用按进程 CPU 时间 / 墙钟时间 / 12 计算。显存未计入这些 RAM 数字。
- 没有清空 OS 文件缓存或驱动缓存。启动数字不是冷开机测试；Windows 的显示动画和约 31 ms 的屏幕回读采样间隔都影响结果。Native AOT 本轮做功能验证，未测它的白屏时长，不能用 JIT 数字替代 AOT 结果。

## CPU 与原版

系统字体，标准控件，100% DPI；表为 3 个进程指标的中位数：

| 实现 | 平均 ms/帧 | CPU ms/帧 | 分配 KiB/帧 | 工作集 MiB | Private MiB | GC 后托管 MiB |
|---|---:|---:|---:|---:|---:|---:|
| 优化前 | 1.008 | 1.016 | 5.67 | 111.9 | 67.0 | 32.8 |
| 优化后 | 0.800 | 0.807 | 5.67 | 94.8 | 48.5 | 14.9 |
| 原版默认 | 0.911 | 0.911 | 7.77 | 88.4 | 42.8 | 2.6 |
| 原版完整透明度组 | 3.298 | 3.307 | 7.94 | 88.1 | 42.8 | 2.6 |

帧耗时下降约 **20.6%**，CPU 时间下降约 **20.5%**；工作集减少 **17.1 MiB**，Private 减少 **18.4 MiB**。与原版默认的工作集差距从 **23.5 MiB** 缩小到 **6.4 MiB**，Private 差距从 **24.1 MiB** 缩小到 **5.7 MiB**。每帧分配基本不变。各进程平均耗时范围为优化前 0.949–1.228 ms、优化后 0.795–0.902 ms，原始波动保留。

固定文件字体，保持此前测试输入：

| 场景 / DPI | 实现 | 平均 ms/帧 | CPU ms/帧 | 工作集 MiB | Private MiB | 分配 KiB/帧 |
|---|---|---:|---:|---:|---:|---:|
| controls / 1 | 优化前 | 0.989 | 1.016 | 110.3 | 70.0 | 5.67 |
| controls / 1 | 优化后 | 0.753 | 0.755 | 110.1 | 69.7 | 5.67 |
| controls / 1 | 原版默认 | 0.903 | 0.911 | 96.2 | 51.0 | 7.77 |
| controls / 1 | 原版完整组 | 3.501 | 3.516 | 95.8 | 50.9 | 7.94 |
| controls / 1.5 | 优化前 | 2.073 | 2.057 | 114.4 | 74.0 | 5.67 |
| controls / 1.5 | 优化后 | 1.781 | 1.771 | 112.3 | 72.0 | 5.67 |
| controls / 1.5 | 原版默认 | 1.692 | 1.667 | 99.9 | 54.8 | 7.77 |
| controls / 1.5 | 原版完整组 | 7.062 | 7.083 | 98.4 | 53.5 | 7.94 |
| scroll / 1 | 优化前 | 0.396 | 0.417 | 96.2 | 62.5 | 3.22 |
| scroll / 1 | 优化后 | 0.408 | 0.443 | 96.1 | 62.4 | 3.22 |
| scroll / 1 | 原版默认 | 0.372 | 0.391 | 86.6 | 48.1 | 8.03 |
| scroll / 1 | 原版完整组 | 0.372 | 0.391 | 86.6 | 47.9 | 8.03 |

标准控件耗时下降约 **23.9%**，高 DPI 下降约 **14.1%**。滚动耗时反而上升约 **3.1%**，高 DPI 及滚动仍慢于原版默认；这些场景不宣称追平。固定字体的完整快照和像素缓冲仍常驻，没有系统字体场景那样的内存下降。

## 启动与白屏

下表为 5 次启动的中位数，单位 ms。“可见→内容”从发现可见 HWND 到屏幕四角呈现完整标记；“白色阶段”累计纯白采样段，不代表逐毫秒连续观测。

| 实现 | 创建进程→内容 | 可见→内容 | 白色阶段估计 |
|---|---:|---:|---:|
| CPU 优化前 | 1230 | 337 | 280 |
| CPU 优化后 | 1188 | 289 | 218 |
| 原版 CPU | 1035 | 165 | 95 |
| Direct2D 优化前 | 1401 | 499 | 439 |
| Direct2D 优化后 | 1387 | 455 | 405 |
| Vulkan 优化前 | 1437 | 534 | 468 |
| Vulkan 优化后 | 1405 | 506 | 437 |

CPU 白色阶段估计缩短约 **22%**，可见后的内容等待缩短约 **14%**，整个进程启动到内容出现仅缩短约 **3.5%**。不能把局部阶段的改善写成整个应用启动提速 22%。GPU 的改善只有约一个采样周期，证据不足以宣称显著启动提速。

**测量更正：** 最初四次 `GetPixel` 串行读取导致约 125 ms 的间隔，漏掉原版较短的白色阶段。“原版没有采到白帧”不能解释为“原版没有白屏”。最终改为一次 `BitBlt` 读取整帧，常见间隔约 31.2 ms；原版也稳定采到白色阶段。所有正式数字采用修正后的工具。

首帧线程采样显示字形覆盖率光栅化和描边处理是 SimdSkia 的额外开销。本轮削减了其中部分成本，**没有彻底消除白屏，也没有追平原版首帧**。这里验证的是仓库测试程序，不代表所有业务窗口、主题或显示设置。

## 实际 CPU/GPU 窗口

1000×720、100% DPI、系统字体、相同动态控件窗口。窗口移到屏幕外，测量的是绘制/提交/完成服务时间，不是显示器 FPS。Direct2D 包含 GPU 完成事件与 DXGI 呈现，Vulkan 包含 fence、像素回读和平台 framebuffer。GPU 字形图集构建及透明度软件层仍有 CPU 工作；所有运行都没有触发不支持命令的回退。

先保持此前的 **2 秒预热 + 至少 3 秒/60 帧观测**，每项 3 个进程，18 个进程共 1759 个完成帧：

| 后端 | 实现 | P50 ms | P95 ms | 全机 CPU % | 工作集峰值 MiB | Private 峰值 MiB | 分配 KiB/帧 |
|---|---|---:|---:|---:|---:|---:|---:|
| CPU | 优化前 | 0.776 | 1.098 | 3.07 | 116.3 | 60.6 | 18.52 |
| CPU | 优化后 | 0.748 | 1.068 | 2.75 | 98.9 | 42.6 | 19.46 |
| Direct2D | 优化前 | 0.726 | 0.995 | 3.51 | 141.9 | 112.0 | 15.32 |
| Direct2D | 优化后 | 0.736 | 1.011 | 3.12 | 123.8 | 93.9 | 15.25 |
| Vulkan | 优化前 | 1.440 | 1.653 | 2.73 | 156.6 | 137.8 | 16.99 |
| Vulkan | 优化后 | 1.439 | 1.766 | 3.73 | 141.7 | 122.0 | 17.63 |

CPU/Direct2D/Vulkan 工作集分别减少约 **17.4/18.1/14.9 MiB**。GPU P50 基本不变，部分 P95、CPU 占用和分配反而上升，不能称为全面提速。

为检查短时 CPU 数值，追加 **8 秒预热 + 至少 8 秒/60 帧观测**，仍使用默认 JIT 设置，每项 3 个进程，18 个进程共 4788 个完成帧。两边使用完全相同的时长测试宿主，引擎 DLL 与上面各自版本逐字节相同；前面的程序和哈希清单也重新验证未改变。

| 后端 | 实现 | P50 ms | P95 ms | 全机 CPU % | 工作集峰值 MiB | Private 峰值 MiB | 分配 KiB/帧 |
|---|---|---:|---:|---:|---:|---:|---:|
| CPU | 优化前 | 0.744 | 1.055 | 0.57 | 109.2 | 53.7 | 17.50 |
| CPU | 优化后 | 0.758 | 1.091 | 0.75 | 102.7 | 45.8 | 17.54 |
| Direct2D | 优化前 | 1.292 | 1.506 | 0.83 | 140.8 | 110.7 | 14.52 |
| Direct2D | 优化后 | 1.293 | 1.575 | 0.89 | 122.6 | 91.9 | 14.38 |
| Vulkan | 优化前 | 2.495 | 4.838 | 1.27 | 159.9 | 141.7 | 16.01 |
| Vulkan | 优化后 | 2.493 | 4.409 | 0.97 | 141.7 | 122.0 | 16.36 |

较长观测中，Vulkan CPU 上升没有重现，但 CPU 后端从 0.57% 升至 0.75%，Direct2D 从 0.83% 升至 0.89%。内存下降得到重复验证，**真实窗口的持续 CPU 开销并未全面改善**。CPU 离屏吞吐的收益不能直接替代动态窗口的总 CPU 指标。

改变观测阶段后，两边的 GPU 帧时间也变了。本次未单独记录 GPU 时钟，不能把变化全部归因于 JIT 或驱动；只在同一时长组内比较。8 秒观测也不代表长时间运行或所有业务程序。这一轮没有重测原版 GPU，原版 CPU 数字不得用作 GPU 对手；历史原版 GPU 对照见 [Vulkan 专项报告](VULKAN-OPTIMIZATION-2026-10-22.md)。

## 验证与边界

| 检查 | 结果 |
|---|---|
| 完整 Release 构建 | 0 警告、0 错误 |
| 标量冻结对照及 SIMD 优化 | 8085，通过；禁用硬件指令也通过 |
| 真实 Avalonia 控件/几何/文字/生命周期 | 8299，通过 |
| 平台字体发现/分页/并发/导出/文件替换 | Windows 36 项，通过；其余平台由 CI 验证 |
| Windows 系统字体 | 13，通过，无内嵌字体 |
| 基础行为 / 图片编解码 | 36 / 484，通过 |
| CPU / Vulkan 文字链 | 595 / 599，通过 |
| GPU 采样 / Vulkan 优化 / Direct2D | 590 / 540 / 344，通过 |
| Avalonia GPU 集成 / 字形合批 | 12174 / 60，通过 |
| CPU 优化前后最终帧 | 12/12 对，逐字节相同 |
| CPU/Direct2D/Vulkan 窗口及呈现截图 | 6/6 对，逐字节相同 |
| 原版固定字体整页 / 系统字体整页 | 0/3、0/1，仍 FAIL |
| 原版既有图像 / SK* API | 108/120、425/3235，缺口保留 |

新增 240 组裁剪对照使用独立 16×16 点计数和标量预乘合成，覆盖 SIMD 开关、负偏移、越界、零覆盖、常量段/随机边缘、两种像素格式、stride 和尾部。TTC 文件分页读取、共享内存读取及独立面导出的字节、校验和、中文/英文字形与度量也逐项比较。

整页最大通道误差 / RMSE：固定字体标准 **96 / 2.408**，高 DPI **117 / 2.321**，滚动 **75 / 2.595**；系统字体 **96 / 3.359**。门槛保持最大误差 ≤32、RMSE ≤2，不放宽。`verify.ps1 -Reference -Gpu` 仍返回 **2**，不能称为完整验收通过。

生产优化源码的跨平台验证均成功：

- [Windows / Linux / macOS 托管及关闭硬件指令回退](https://github.com/hnlyf168/SimdSkia/actions/runs/39759622776)。
- [三平台实际 JIT、Native AOT、nupkg 消费窗口](https://github.com/hnlyf168/SimdSkia/actions/runs/39759622830)，已下载核对 9 条窗口记录。
- [Windows 原版与 SimdSkia 双实现 Native AOT](https://github.com/hnlyf168/SimdSkia/actions/runs/39759622875)，已下载核对两份真实 AOT 运行记录。

跨平台 CI 证明这些用例能运行，不是跨平台性能成绩。本报告的性能数字只来自本机 Windows。

## 构建与复现

```powershell
dotnet build SimdSkia.sln -c Release
powershell -NoProfile -File tools/verify.ps1 -Reference -Gpu
dotnet run --project tests/PlatformFonts -c Release
dotnet run --project tests/Direct2D -c Release
dotnet run --project tests/AvaloniaGpu -c Release
```

完整验证的已知画质失败应单独读取，不能吞掉退出码。每次基准使用全新输出目录：

```powershell
python tools/run-system-font-comparison.py --family "Microsoft YaHei UI" --output artifacts/my-system --runs 3
powershell -NoProfile -File tools/run-avalonia-controls.ps1 -Output artifacts/my-fixed -Frames 600 -Forks 3

dotnet build tests/Startup/Managed -c Release
dotnet build tests/Startup/Reference -c Release
python tools/run-startup-comparison.py --output artifacts/my-startup --runs 5 --backends cpu direct2d vulkan

dotnet build examples/CrossPlatform -c Release
python tools/run-desktop-benchmarks.py --output artifacts/my-window --system-fonts --runs 3
python tools/run-desktop-benchmarks.py --output artifacts/my-window-long --system-fonts --runs 3 --warmup-seconds 8 --measurement-seconds 8
```

启动观察器需要真实可见 Windows 桌面，会短暂显示测试窗口，避免其他置顶窗口遮挡。优化前后对照须先在独立 checkout 构建 `7bcd362` 并冻结 DLL；CPU 用 `--before-directory` / `-BeforeDirectory`，启动用 `--before`，窗口用 `--before-executable`。延长窗口观测时，旧/新引擎需使用同一份支持时长参数的测试宿主，只替换 `CrossPlatform.exe/.dll/.pdb`，保留各自的引擎 DLL 和依赖清单；脚本会拒绝没有实际遵守时长的旧宿主。

本机原始数据、逐帧时间、屏幕颜色采样、差异图、哈希和 CI 下载记录保存在 `artifacts/startup-round5`。早期 `GetPixel` 粗测和开发中间测量不作为正式成绩。
