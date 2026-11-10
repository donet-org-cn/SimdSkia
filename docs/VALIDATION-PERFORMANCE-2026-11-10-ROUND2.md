# 第二轮优化测试报告（2026-11-10）

最终源码重建后执行 85 个独立验收命令，59 个返回成功，26 个原版门槛或 API 审计返回非成功。返回码和原始日志完整保留。初轮调查与优化候选不计入最终通过数。

公共 API 元数据匹配 3230/3235。保留用户指定的五项原生句柄/版本合同缺口。元数据匹配与行为、画质及二进制兼容分别评估。

## 新增回归

- Native AOT 数值、采样、并发、绑定和解释器回退检查 270,719 项，每次变化 uniform 的绑定分配 240 B。JIT、关闭 AVX2、关闭全部硬件指令的相同测试另行执行。

- 字体流验证覆盖已注册字体复用、大对象分配上限、非零流位置、非寻址流、不同字体的快照及调用方流生命周期。

- 图层验证覆盖轻量不透明作用域嵌套真实半透明组、错误 Pop 的异常及后续恢复。完整 Avalonia 控件、几何、文字、SIMD、生命周期和效果用例由完整验收记录计数。

- Native AOT portable / x86-64-v3 各实际执行 460,050 项采样检查。运行时、网格、路径、滤镜、PDF 的 38 项 AOT 检查各执行 SIMD 与标量。

## 画质与剩余差距

AOT 整页通过 0/10，GPU 整页通过 0/12，保持既有 max=32、RMSE=2 门槛。所有性能场景中的修改前后固定帧由采集脚本逐字节检查。

冻结旧引擎回放覆盖原有失败套件及四个通过的着色器套件，合计 21 个套件、651 个输出，全部相同的前提由回放脚本强制断言。该项只检查新增回归，原版差异另列。

额外纳入已通过原版门槛的网格/图集、运行时效果、浮点着色器及梯度兼容测试。梯度测试中的50张预览PNG由Python对照工具生成，最初回放将其视为C#输出导致文件缺失，修正后对其对应完整BGRA源逐字节验证。初次记录单独保留。

| 验收命令 | 返回码 | 状态 |
| --- | --- | --- |
| check-alpha-compatibility | 2 | reference-gate-failed |
| check-api99-batch | 2 | reference-gate-failed |
| check-blends | 2 | reference-gate-failed |
| check-canvas-operations | 2 | reference-gate-failed |
| check-canvas-queries | 2 | reference-gate-failed |
| check-conic-compatibility | 2 | reference-gate-failed |
| check-embedded-bitmaps | 2 | reference-gate-failed |
| check-final-paint | 2 | reference-gate-failed |
| check-gl-interop | 2 | reference-gate-failed |
| check-image-filters | 2 | reference-gate-failed |
| check-image-operations | 2 | reference-gate-failed |
| check-jpeg-parity | 2 | reference-gate-failed |
| check-mask-filters | 2 | reference-gate-failed |
| check-path-boolean | 2 | reference-gate-failed |
| check-path-effects | 2 | reference-gate-failed |
| check-pictures | 2 | reference-gate-failed |
| check-pixel-compatibility | 2 | reference-gate-failed |
| check-raster-shaders | 2 | reference-gate-failed |
| check-skp-documents | 2 | reference-gate-failed |
| check-stroke-compatibility | 2 | reference-gate-failed |
| check-svg-documents | 2 | reference-gate-failed |
| check-text-blobs | 2 | reference-gate-failed |
| foundations-FontOperations | 2 | reference-gate-failed |
| foundations-Regions | 2 | reference-gate-failed |
| interop-VulkanInterop | 2 | reference-gate-failed |
| api-compare | 2 | reference-gate-failed |

## 原版整页差异

| 场景 | 后端/参考 | max | RMSE | 通过 |
| --- | --- | --- | --- | --- |
| controls ×1 | vulkan / original-vulkan | 96 | 2.435 | False |
| controls ×1 | vulkan / original-angle | 96 | 2.435 | False |
| controls ×1 | direct2d / original-vulkan | 96 | 2.433 | False |
| controls ×1 | direct2d / original-angle | 96 | 2.433 | False |
| controls ×1.5 | vulkan / original-vulkan | 127 | 2.376 | False |
| controls ×1.5 | vulkan / original-angle | 127 | 2.376 | False |
| controls ×1.5 | direct2d / original-vulkan | 127 | 2.379 | False |
| controls ×1.5 | direct2d / original-angle | 127 | 2.379 | False |
| scroll ×1 | vulkan / original-vulkan | 75 | 2.588 | False |
| scroll ×1 | vulkan / original-angle | 75 | 2.588 | False |
| scroll ×1 | direct2d / original-vulkan | 75 | 2.588 | False |
| scroll ×1 | direct2d / original-angle | 75 | 2.588 | False |
| controls-1-throughput | SimdSkia / original-default | 96 | 2.418 | False |
| controls-1-throughput | SimdSkia / original-full | 96 | 2.408 | False |
| controls-1.5-throughput | SimdSkia / original-default | 117 | 2.333 | False |
| controls-1.5-throughput | SimdSkia / original-full | 117 | 2.321 | False |
| scroll-1-throughput | SimdSkia / original-default | 75 | 2.589 | False |
| scroll-1-throughput | SimdSkia / original-full | 75 | 2.589 | False |
| controls-1-paced | SimdSkia / original-default | 96 | 2.418 | False |
| controls-1-paced | SimdSkia / original-full | 96 | 2.408 | False |
| scroll-1-paced | SimdSkia / original-default | 75 | 2.589 | False |
| scroll-1-paced | SimdSkia / original-full | 75 | 2.589 | False |

## 测试边界

Windows x64 CPU、Vulkan、Direct2D 和原生窗口通过本机执行。macOS Metal GPU、Linux GPU、Android 及 iOS 未在本轮硬件矩阵执行。API 元数据覆盖不代表全部方法、平台和参数组合的行为兼容。

额外24个AOT进程复测60 FPS滚动页，追加12个JIT进程检查系统Microsoft YaHei UI字体；五个实际字体表哈希及各轮修改前后固定帧均一致。系统字体整页原版严格画质为0/2，六轮AOT滚动为0/2。

333份实现源码与正式测量和最终验收的哈希一致。初次AOT快照的PDF锁文件多一个空的net10.0/win-x64条目，已按原哈希精确还原并记录；后续恢复移除了空条目，包版本和实现代码一致。AOT窗口示例的引用图不包含PDF模块。

各场景仍存在相对原版的耗时、内存和画质差距，整体持平目标尚未达成。详细性能、完整失败记录和可复跑命令见配套报告与证据目录。[完整归档](evidence/performance-2026-11-10-round2/)
