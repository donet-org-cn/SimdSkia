# 依赖与许可证

产品 Core/Cpu/Text/Imaging/Gpu/Compatibility 六个程序集没有第三方 NuGet 包。字体、排版、图片编解码均由 C# 实现，不调用 Skia、FreeType、HarfBuzz、System.Drawing 或 native zlib。GPU 按用户授权调用系统 Vulkan loader/驱动，shader 是 GLSL/SPIR-V。

| 组件 | 用途 | 许可证/原生性质 | 产品关系 |
| --- | --- | --- | --- |
| .NET 8 runtime/BCL、SDK 10 | 执行/构建 C# | .NET 发行许可证/主要 MIT，运行时本身含平台组件 | 标准执行环境 |
| 系统 Vulkan loader/驱动 | Windows/Linux GPU API 与硬件执行 | Khronos loader 通常 Apache-2.0；驱动许可证由厂商决定；原生 | GPU 运行环境，不打包替代系统库 |
| glslang 16.6.0 独立工具 | 开发阶段 GLSL -> SPIR-V | 官方 LICENSE 列 BSD/MIT/Apache 等组成；原生可执行 | 不作为产品依赖，不安装系统软件 |
| Vulkan-Headers v1.3.290 | 仅 ABI/prototype 调查 | 官方 headers license（Apache-2.0/MIT 相关文件） | 自写 C# interop，未复制头文件到产品 |
| Zen Old Mincho Regular | 可下载的真实 CJK/Latin 测试字体 | SIL OFL 1.1，无原生代码；license 随夹具保留 | 可选测试数据，不是字体引擎库 |
| 系统 SimHei/Arial | 本机对照/示例字体文件 | 用户已有系统字体许可证，不再分发 | 只读实际安装字体 |
| Avalonia.Skia/Avalonia 12.1.4 | 隔离版本恢复探针 | MIT，transitive 原生 Skia/HarfBuzz assets | 不在生产引用图 |
| SkiaSharp/NativeAssets 3.119.4 | 隔离 API/行为/像素/CPU 基准 | MIT 包和随包第三方 notices；原生 Skia | 仅 ReferenceRenderer |
| HarfBuzzSharp 8.3.1.3/NativeAssets | 探针 transitive | 随包 MIT/HarfBuzz notices；原生 | 不用于本引擎 shaping |
| MicroCom.Runtime 0.11.6 | 探针 transitive | MIT，托管 interop | 非产品 |
| Avalonia.BuildServices 11.3.2 | 探针 transitive 构建任务 | MIT、托管 DLL | 非产品 |

[glslang 官方许可证](https://github.com/KhronosGroup/glslang/blob/main/LICENSE.txt) 是混合许可说明，不能只写一个简化标签代表所有组成。[字体 OFL](https://github.com/google/fonts/blob/dac28ca2a883c27a817104b72bfa8d5e7e973954/ofl/zenoldmincho/OFL.txt) 与固定文件一起保留在 baseline/fonts。字体 commit 和 SHA-256 固定在 fetch-test-font.ps1。

baseline/ 被忽略；原版 DLL 只复制至独立测试输出。tools/check-production-dependencies.ps1 检查六个产品 assets 和输出。参考 Python/zlib 只生成一个 547 B fixed Huffman 测试数据，不是产品/测试运行依赖。

Avalonia 专项额外读取固定 12.1.4 官方 MIT 源码及隔离 Avalonia.Skia.dll，做 IL/API 调查；源码 archive 与 54 个 C# 文件 SHA 在 avalonia-source-baseline.json。没有将 Avalonia 或其原生依赖引入六个生产程序集。新增 Python 报告中的 zlib 仅用于测试差异图 PNG 的编码，产品编解码仍全部 C#；独立 zlib/PNG 回归夹具的完整计数见 VALIDATION.md。

已编译 SPIR-V 随源码提供，普通构建无需 glslang。shader 源码/编译器/输出版本与哈希见 shader-build.json。项目本身尚未选择发布授权许可证；上游依赖许可不等于擅自给本项目选择许可。


OCR 专项基准额外只读链接 Qcxt.OCR（Apache-2.0 源码及其原有第三方/模型 notices）、Qcxt.OCR.SkiaSharp（MIT）和本机缓存 SkiaSharp/Win32 NativeAssets 4.153.1（MIT 包及随包 notices，原生组件，仅 Reference）。目标资源保持原项目的许可归属；这些项目不属于六个生产程序集的引用图。基准使用 .NET 10 执行实际目标 CPU 内核，不修改全局兼容基线，也不把旧版/本项目的 native handle 或二进制兼容结果推广到 4.x。
