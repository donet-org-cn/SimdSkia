# 最终公共 API 补全工作记录

固定基线为 SkiaSharp 3.119.4、3235 项，SHA256 `03A164AC1097F84793632DF806DA91E2829F0FCA65D8F2B922E45554AB083296`。上一冻结快照匹配3136、缺99、额外0，仍见 `PUBLIC-API-99-2026-11-07.md`；本文件是后续开发记录，未发布，不覆盖旧报告。

用户已明确选择：`SKObject.Handle` 和 `SkiaSharpVersion` 的4项原生版本合同，共5项继续保留真实缺口，禁止替代句柄或伪造原生版本。当前任务为完成其余94项并提供完整新测试/性能报告，不能把5项例外删除基线后宣称3235/3235。

## 进行中

- 已实现4个 `SKImage.FromPicture`：真正延迟光栅化、sRGB信息、独立持有画笔与Picture快照资源；懒图PeekPixels不会偷偷生成像素，ReadPixels/ToRasterImage(true)真实生成。ToRasterImage(false)与本机原版一样可返回相同托管对象；raster-3误将这个别名提前Dispose而造成原版访问冲突，错误夹具已修正。raster-4为28/28图、14元数据一致、双方各176检查通过。
- 已实现3个 `SKPixmap.Encode(SKWebpEncoderOptions)` 入口，并接通常规Bitmap/Image的WebP编码。复用已固定的Sdcb.SimdCodecs.Webp1.4.0，无新增生产依赖或原生组件；lossless质量映射压缩努力，lossy使用依赖自身质量刻度，整数取整和码率不等同于libwebp刻度，不能承诺字节流或压缩率一致。
- `raster-2`：28/28图像、10元数据一致、双方各168项检查通过；lossless/图片max3/RMSE.75，lossymax48/RMSE8为首次测试前确定的独立门槛。前次`raster-1`错误地假定ToRasterImage()必然去掉lazy属性，原版反例保留，已改为验证ensurePixelData=true。
- Alpha8/ExtractAlpha 的4项入口已接通真实1字节像素、stride、模糊/表格遮罩、原地提取、读取和绘图；`alpha-8` 为208/208图像、81元数据一致、双方各369检查。包含新增48幅普通/原始A8图片着色器，校正精确最近邻边界，预先合并画布与局部采样矩阵，避免逐像素重复组合；`raster-shaders-2`扩展旧套件746/940过严格门槛，仍有194幅失败；普通渐变套件50图+30擦除+16行为/480标量检查通过。
- SVG 3项入口输出真实向量，不把全页转成图片；原版导出器在实测中丢失渐变/透明图层，并把Difference裁剪导成Intersect。Edge独立渲染 `svg-render-1` 为4/8与原版SVG相符、14元数据一致，失败保留，未宣称SVG原版行为全兼容。
- XPS 6项入口写出真实多页OPC向量包、PNG资源与所需关系。双方各17项输出/流检查；独立WPF阅读器渲染本实现5页、13项尺寸/精确颜色/渐变检查通过。相同阅读器渲染原版文件出现3个语义失败（图片/图层），不把原版导出限制当作我们必须复制的正确结果。
- SKP 8项入口实现原生v103文件格式，保留向量路径/裁剪/矩阵/普通图层/图片和注释；双方5幅文件交叉读取成功、各56项入口/流检查。`skp-3` 25元数据一致、14/35画质过严格max3/RMSE.75，曲线/AA裁剪已有差异仍失败；这不是完整SKP flattenable图支持。滤镜/着色器/字体表和其余操作码明确报错；没有把全页栅格图塞入私有格式冒充SKP。独立3203项随机损坏/截断/恶意长度/短读/调用方流位置检查通过。
- SKPaint ColorF/SetColor/Blender/IsDither 4项已接通实际CPU阶段、保留浮点/HDR颜色、颜色空间转换、8x8有序抖动、独立生命周期的算术混合器。`paint-3` 19元数据一致、25/29画面过max3/RMSE.75；曲线边缘和图片透明度舍入仍失败。高级状态按需保存并复用，避免普通画笔每次绘图分配；GPU尚须显式回退这些新阶段。更细的圆锥曲线细分未解决4幅失败，已撤回该没有证实收益的尝试。
- 中间审计 `comparison-2.json`：3168/3235、缺67；曾有一个多余SVG公开构造函数，已改为私有，待下次审计确认额外签名归零。根清单仍保留此前99项冻结快照，任务继续。GL/D3D/Metal及外部GPU资源、LCD/嵌入字体尚未完成。用户没有Mac GPU，Metal实机结果必须为未验证。
- `full-build-3` 零错误/警告；`regression-3` 除既有JPEG严格画质失败外，其余CPU、平台字体、Avalonia、Vulkan、Direct2D回归通过。后续采样修复还需再次全构建和回归，不能复用前次通过结论。
- 已收到本次完成后的推送/发布新版本（包括NuGet）与所有文档授权；当前未达到完成条件，不提前发布。

## 后续已验证的进展（任务仍在进行）

- 最新实际审计 `comparison-3.json` 为 **3171/3235、201公开类型、缺64、额外0**。剩余为GRContext 8、SKImage 23、SKSurface 28，以及用户要求保留的SkiaSharpVersion 4/SKObject.Handle 1；也就是还需实现59项GPU上下文与资源互操作。没有删除基线或伪造原生Skia对象。根docs/api仍是99项冻结报告，最终报告时统一更新。
- A8固体路径先去除每条命令的整页RGBA扩展/回收，再对实测热点加入1字节Alpha SIMD；支持SSE2/AVX2及标量回退。`alpha-reference-1`与`alpha-optimized-1`的324幅输出、案例表和90项元数据逐字节一致。扩大原版曲线路径测试后为270/324严格通过，54幅曲线边缘仍失败；不能把原来208/208的较小集合当作324/324。密集覆盖、29种混合、padded stride也被覆盖。
- 新浮点统一色合成保持浮点输入到混合完成，不提前量化为8位；增加AVX2八像素和SSE2四像素路径。`float-simd-2`与禁用AVX2的`float-sse2-1`分别5568个缓冲区与标量逐字节一致，覆盖全部29模式、RGBA/BGRA、3种Alpha、1..32短尾及stride。原版画笔画质集合仍25/29，不把提速当作4幅失败已解决。
- `raster-timing-2`：同机.NET10.0.14 JIT、三轮交错、每个场景独立进程、0.6秒预热/0.8秒测量。A8为0.0903ms，原版1.0223ms；浮点画笔0.0428ms，原版0.0746ms。它们是独立微场景，不能推广为整页/整应用或GPU领先。算术混合约1.106ms/原版0.801ms，SKP读写仍较慢；WebP lossless约25.10ms/原版2.04ms，且托管分配约2.18MB/次，是明确剩余性能问题。所有数字和端点WS/private在summary.json；这些是进程内存诊断，不是峰值采样、原生堆分配或显存。A8端点WS仍34.64MiB/原版29.50MiB，不能因移除工作面而宣称进程内存已低于原版。
- LCD开关及SKFont.SubpixelAntialias接通实际3通道覆盖。根据原版实测，水平LCD仅在BGRA目标启用，RGBA及垂直几何退回灰度；旧画笔LCD标志与IsAntialias独立保存。60幅生命周期/读回场景各120检查；BGRA集合65项状态/是否彩色边缘元数据一致，像素仍不同，未声称完整LCD画质兼容。GPU新LCD阶段目前明确要求显式CPU回退。
- EmbeddedBitmaps/IsEmbeddedBitmapText接通真实C# EBLC索引1..5与EBDT单色图像1/2/5/6/7，按匹配字号选择点阵，不存在点阵时用轮廓；文字blob快照和几何缓存包含开关，不污染另一种模式。灰度/复合/彩色点阵目前明确报错或原有格式门禁，仍是行为限制。本机基线默认false；原版在本组Windows设置下切换开关没有改变画面，而本引擎会使用系统宋体12/14/16像素点阵，这是明确平台行为差异。`embedded-3` 23元数据一致、双方各32检查，20整串文字画质均未过严格门槛；独立FontTools4.68.0解析的9个中文点阵与实际绘制逐像素相等，3个没有点阵的ASCII字形明确记录为轮廓回退，没有计作点阵通过。
- `full-build-4`暴露Direct2D新校验误用了COM target字段；已改用pixels字段，`full-build-5`零错误/警告。其后字体点阵新增还需全量重建；不能复用较早构建结论。
- `aot-1`真实win-x64 Native AOT编译成功，正常Vector128和--scalar分别38项检查通过，Perlin原始像素SHA一致；包含新WebP、A8、懒图、SKP、SVG、XPS、浮点和算术混合入口。该快照在EBDT新增之前，最终AOT还需加入点阵测试并重跑。

原始记录位于 `artifacts/api-final-2026-11-07`。59项GPU余项、完整新回归、完整CPU/GPU/内存采集、最终报告、推送和NuGet发布均未完成，继续执行。

## GPU 互操作追加验证（仍未完成总任务）

- `full-build-6` 零错误/警告；`regression-5` 除既有JPEG严格画质失败外，其余回归通过。`aot-2` 将系统宋体点阵加入真实win-x64 Native AOT验证，正常Vector128和标量分别41项检查通过，Perlin SHA一致。这些快照早于下述GL/D3D改动，最终必须重新构建、回归和AOT验证。
- GPU设备边界改为 `IGpuCanvas`，原有Vulkan仍是实际GPU执行；重构后 `gpu-context-refactor2` 的10图、3元数据、67设备检查通过，capabilities-refactor的7项系统能力检查通过。
- OpenGL/GLES2 新驱动真实创建GPU程序、纹理、帧缓冲、GPU合成与同步读取。覆盖由C#计算，画笔/渐变/图片采样和混合在GPU执行；没有调用原生Skia。`gl-context-1` 10/10画面通过max3/RMSE.75，8幅逐像素一致、两幅渐变max1。四个CreateGl重载另外在系统WGL兼容/3.3 Core上下文验证：`wgl-1` 8/8画面、2元数据一致，双方各16检查；生产库不携带ANGLE，本组ANGLE仅为隔离测试宿主。
- 外部GL纹理/帧缓冲直接使用调用方的实际GPU资源；实现借用、接管、释放回调、TopLeft/BottomLeft、GPU上传/拷贝快照及同步读回。GL→GL绘制纹理直接绑定资源，避免先读CPU再上传。`gl-interop-2` 的347/347画面、99元数据一致，双方各574检查、8次回调一致，另8项本实现线程/丢失/多活动表面/上下文先释放/零缓存预算检查通过。包含全部23个SKImage及28个SKSurface新增重载，未仅添加空签名。
- 修复GL零预算仍缓存大图片、矩形反复构造几何、每条命令临时状态数组、弱引用图像注册表无限增长、多个活动表面同时Abandon(false)抛错的问题。GL资源统计为已知纹理负载字节，不能当成驱动分配峰值或显存占用。
- Direct3D12两个上下文工厂通过系统D3D11On12在调用方实际D3D12 device/DIRECT queue执行C# Direct2D驱动，并校验queue属于同一设备、持有COM资源生命周期。`d3d-context-2/managed` 8幅输出、56真实设备检查通过。原版两个工厂在相同GTX1050Ti、SkiaSharp3.119.4包上均返回null，`original/capability.json`明确记录为unavailable；不能伪造同D3D12后端原版性能比值。D3D12外部资源导入仍未实现。
- 最新签名审计 `comparison-5.json` **3228/3235、缺7、额外0**：2项Metal上下文工厂仍未实现，5项为用户要求保留的原生Skia接口。公共签名匹配不等于所有后端/格式/行为完成；本批外部资源接口目前仅实现GL，Vulkan/D3D12/Metal导入仍明确报错。没有Mac GPU，Metal实机验证始终为未验证。GPU/MSAA、非sRGB目标、部分采样/滤镜/LCD/高级混合限制仍须在最终兼容报告逐项保留。

当前仍需：Metal实际实现、其余后端互操作行为、扩大的画质验证、更新后的全回归/AOT、CPU/GPU/内存性能采集及最终报告；完成后才执行已授权的推送和NuGet新版本发布。

## 最新检查点：签名、画质、AOT与互操作性能

- `comparison-6.json` 已实际达到 **3230/3235、缺5、额外0**，基线没有变化。仅余用户明确要求保留的SKObject.Handle与SkiaSharpVersion相关项。新增Metal工厂进入实际C# Objective-C/Metal计算管线（MSL、共享设备缓冲、命令批次、完成等待、错误状态与缓存）；没有Mac GPU，`metal-unverified-1` 明确为 **unverified，未运行硬件测试**，不能作为Metal通过记录。
- 扩大GL集合后发现旧347图没有覆盖的边界。`gl-interop-8` 为 **361/365**、111元数据一致、双方各592检查，本实现另10项线程/丢失/PBO/pixel-store/零预算检查通过。4幅透明最近邻精确边界图仍未过max3/RMSE.75；不能只引用旧347/347。已修正格式不匹配返回值、GPU图片AA矩形角点覆盖，增加有预算的覆盖缓存并改为顶点插值纹理坐标。`gl-interop-5` 曾暴露GLES两阶段整数精度不一致，修复后保留原失败记录。
- GL普通Flush不再强制整帧CPU读回；RGBA整帧读取可直接进入调用方缓冲，GPU快照执行实际纹理拷贝。重置PBO、row-length及skip状态，防止外部GL状态使指针变成PBO偏移、造成错误写入。GPU SaveLayer原本仅拒绝Vulkan、会让新驱动隐式进入CPU层；现统一显式拒绝，直到实现真正GPU离屏层，不能宣称所有GPU层已完成。
- `full-build-8` 零警告/错误；`regression-6` 的basic/pixels/codecs/pdf/features/platform-fonts/optimization/avalonia/controls/direct2d/vulkan/gpu-sampling均通过，既有JPEG严格画质仍失败。该快照早于最新Vulkan互操作，后续必须重新验证。
- 系统OpenGL真正Native AOT：首次`gl-aot-build-1`因测试程序反射JSON序列化失败，已改用源生成；`gl-aot-build-2`零警告/错误，`gl-aot-runtime-2`在.NET10.0.14实际运行，**nativeAot=true，16检查通过、8幅输出**。没有把JIT执行或只有IL裁剪当作AOT通过。
- `gl-timing-1`：同一GTX1050Ti、系统OpenGL Core、640×480、每帧完成RGBA读回，1秒预热/2秒测量、3个独立进程、10ms采样。矩形0.4382/原版0.7384ms，渐变0.4806/0.7594ms，图片0.4699/0.7432ms，三个场景画质通过；路径0.4480/0.8415ms但画质失败，不能计为同画质领先。工作集分别82.49/原版72.69、92.01/74.16、79.43/73.57MiB，仍高于原版。吞吐满速下整机CPU占用约15–16%/原版8–12%，每帧CPU时间需单独看；不能用更高帧率的占用率冒充同帧率节省。每帧分配约0的读数含测量标记的摊销，不能宣称微小B/frame差值代表引擎分配优化。完整summary/runs/采样与哈希在该目录，尚非整页Avalonia或呈现/AOT性能结果。
- Vulkan image新增真实转移管线，检查格式/usage/原点/队列族/专用分配，恢复声明的布局并等待fence，生命周期不依赖伪造Skia句柄。SKImage导入、接管、上传、GPU快照及SKSurface外部纹理/目标已进入实际VkImage，当前表面仍有CPU读回再上传的桥接成本，**尚非零拷贝，性能还需优化**。`gpu-context-transfer-1` 10图/3元数据/67检查通过。
- `vk-interop-3`：双方共同的 **242/242** 画面过严格门槛，原版178项元数据、本实现194项；32项元数据差异和48幅额外图全部来自原版Vulkan接管工厂返回null、本实现支持真实VkImage接管，不能把总报告写成全兼容通过。本实现426检查（含外部VkImage真实像素验证）、原版290检查、释放回调各8次。前次夹具缺少原版GetProcedureAddress导致context=null，已修正；另一次未在销毁外部资源前提交原版所有待处理命令，旧命令写入复用VkImage句柄导致55图失败，已加显式完成等待后242共同图全部通过。原始失败均保留。

尚需：Vulkan桥接成本优化、Direct3D12/Metal外部资源和其余GPU行为限制处理、最新全量回归与AOT、统一CPU/Vulkan/Direct2D/内存报告、证据冻结与文档、推送和NuGet发布。3230项签名匹配不是行为全兼容或最终任务完成。

- Vulkan外部表面已进一步去除CPU回传后再上传：现在在同一批次记录VkImage→计算输出buffer→VkImage设备复制与屏障，保持调用方原布局。底部原点逐行复制区域仅构造一次并复用。`vk-device-copy-1` 的290幅输出、案例表、194元数据与 `vk-cpu-bridge-reference-1` 正确旧桥接 **逐字节相同**；像素SHA256为 `3fe53d5e322d646c26e6c087ce88111b74512e0fe1ae82d6d97967b133aa4052`，本实现538检查（增加实际VkImage部分更新与方向验证）。与原版共同的242/242画面仍过严格门槛；原版不支持接管产生的32元数据差异/48额外输出仍明确保留。设备内部有真实复制，不把它称为完全零拷贝；显式读取仍计GPU完成与读回成本。新性能数字待采集。

- Direct3D12纹理导入、接管、上传和GPU快照已进入真实ID3D12Resource，经D3D11On12显式Acquire/Release恢复声明状态，并等待实际D3D12 queue fence。`d3d-resource-context-1`暴露新初始化局部变量遮蔽，导致Immediate字段为0；已修正，`d3d-resource-context-2`的56设备检查通过，失败日志保留。`d3d-interop-1`完成346幅输出、98元数据、516检查，包含全部新增工厂和真实外部资源读取；与原版OpenGL同卡共同的346/346画面过严格门槛，结果在cross-backend-quality.json。这是**跨后端画质**，原版D3D12工厂仍不可用，不能当成同D3D12性能/行为完全验证。外部D3D12表面当前仍有CPU桥接，须如实计入性能与限制。只在数学上不会触及最近邻边界的整数放大/整像素映射允许D2D普通采样，其余精确边界/立方采样仍明确报错。
- Metal外部MTLTexture导入、接管、blit读写、私有纹理、GPU快照与表面桥接也已写入实际系统Metal调用；尺寸/格式/设备/单层/单采样验证，256字节对齐共享转移缓冲、完成等待及资源生命周期均有实现。`metal-resource-build-1`零警告/错误，但无Mac GPU，MSL/Objective-C ABI/实机渲染和互操作均仍为**未验证**，不能由Windows编译推断通过。
- `wgl-2`增加没有当前GL上下文时应明确失败、恢复后能继续绘图的检查；2元数据、8/8画面一致，原版16、本实现18检查。无当前上下文时的Abandon(false)只放弃资源跟踪，不调用GL驱动；不把没有画面的GL调用当作成功。

## Vulkan 合批、完成读回及共享纹理生命周期

- 非重叠矩形最多16条进入单次真实GPU dispatch，重叠或无法证明无依赖时保持原顺序；全画布Clear走真实vkCmdFillBuffer，保持预乘与RGBA/BGRA字节。独立batch SPIR-V由同一源生成，保留通用计算管线作正确性对照；未减少AA采样或放宽误差。`vk-final-optimization-1.log`为546检查、536次逐字节对照、48次实际矩形合批dispatch通过。
- full-frame、同格式、Premul/sRGB的ReadPixels从完成的Vulkan映射读回缓冲直接写调用方目标，省掉中间CPU shadow复制；普通Flush仍更新shadow。初始/无绘图/外部表面/stride与格式转换走各自正确分支；GPU计时仍等待fence。
- `vk-timing-1`三独立进程、640×480、1秒预热/2秒测量、10ms WS/private采样：新设备复制路径相较保留的CPU桥接参考，矩形/路径/渐变/图片耗时分别下降约24%/24%/40%/39%，所有输出逐字节相同。对原版同Vulkan后端耗时比仍为1.69/1.29/1.41/1.82，路径画质失败；没有达到整体持平/最多慢10%的要求。矩形WS76.31/原版89.79MiB，不能推广到所有后端或整应用。此快照早于下一条去分配修复。
- 移除Vulkan每批提交中的LINQ枚举链及Stopwatch对象分配；关闭/清理GL及Metal路径缓存也会实际清理其有界CPU覆盖缓存。性能效果待后续独立采集，不能直接沿用上一快照数字。
- 新增原版shader/Picture共享纹理回调对照，确认原版在SKImage释放后回调为0、最后消费者释放后为1。修复共享PixelBuffer的真实GPU资源租约，使shader/Picture生命周期保持同一行为；ToRasterImage显式生成独立CPU副本，不延长纹理租约。`gl-lifetime-probe-1`双方各595检查、115元数据全部一致，本实现另10项管理检查；362/366图过严格门槛，原有4幅透明最近邻边界失败仍保留。
- `full-build-9`包含新增Vulkan/D3D12互操作、GPU基准与诊断项目，零错误/警告。最终回归、AOT与性能采集继续进行。

## 末轮生命周期与夹具更正

- 补充queued GL draw实验发现实际提前释放回调：原版image.Dispose后为0，本实现原为1。现GlCanvas按资源租约持有待完成图像，实际Flush/ReadPixels完成才释放；同一图像本批复用只保留一次，不为每draw创建租约对象。`gpu-completion-final-1/pending`双方阶段值0/0/1/1/1完全一致，输出逐字节相同。丢失GL上下文先作废设备资源再释放pending租约，防止Abandon(false)调用失效驱动。
- **更正此前“原版D3D12不可用”的结论**：D3DHost已找到硬件Adapter，但夹具只向GRD3DBackendContext传Device/Queue，遗漏Adapter。修正真实COM所有权和字段后，原版成功。`gpu-completion-final-1/D3DContext`10/10画面、3元数据一致，双方各68检查；`D3DInterop`346/346画面、98元数据一致，双方各516检查/8回调。此前null/exit3仅为无效夹具记录，不能当作设备或原版能力结论。新增真正的同D3D12性能对照，不再使用“无可用原版比值”的说法。
- 工厂缺Adapter时本实现与原版一致返回null；传入Adapter会通过实际设备的DXGI LUID检查，避免忽略错误设备。D3D12采用既有C# Direct2D驱动与系统D3D11On12，不调用Skia。
- `full-build-11`零警告/错误；GL全部原有595检查、115元数据及另10上下文检查保持，362/366严格画面通过，4幅透明最近邻边界仍失败。系统WGL仍8/8；Metal无Mac GPU仍未验证。GPU对象要求创建线程显式Dispose，GC终结回收合同尚未与原版等同，此项必须写入限制。
- 新版本准备为0.1.2-preview.1；README/包内更新记录/CI已更新，尚未推送或公开发布。CPU/UI/Vulkan已正式串行采集，最后GL/D3D12改动没有改变这些计时路径；旧哈希保留，变更文件明确记录在post-timing-source-delta.json。GL/D3D12与相关AOT正在重新采集，不把旧二进制描述为同一编译产物。

## 推送后的 CI 行为回归修正

- `48cd180` 已推送并标记 `v0.1.2-preview.1`；该候选在真实CI失败，发布作业跳过，未上传NuGet。三平台桌面流水线成功，但几何连续MoveTo及兼容扩展Src图层检查失败；保留失败标签，不移动标签覆盖历史。
- 连续MoveTo应保留空轮廓与点，不能用后一次Move替换；SaveLayer bounds对透明源仍影响目标的七种混合模式只能作为提示，需扩展到父裁剪。使用原版3.119.4确认后修复实际实现，未删测试或放宽门槛。
- 修复版统一 `0.1.2-preview.2`。原版几何300组/4636值通过；新增464组图层、2,375,680字节完全一致，覆盖Clear/空图层/局部绘图/变换父裁剪。进入几何和NuGet发布CI，不依赖人工重测。
- 修复后完整构建零警告/错误，36基础、474413精确优化和十二核心回归通过。真实Native AOT兼容扩展38检查通过，PE无CLR头；固定公共签名仍3230/3235，仅5项约定缺口，生产依赖隔离通过。
- Picture/WebP28/28、浮点25/29、SKP14/35与旧快照比较JSON完全一致；JPEG仍非零。九幅CPU/Vulkan/Direct2D整页与性能采集图逐字节一致，短采集只做输出回归，原版整页画质失败仍保留。新证据单独冻结在 `docs/evidence/api-final-ci-recheck-2026-11-07/`，不改旧1939文件。
- 本地十二包内容检查通过；正式发布仍须本版CI、三平台版本矩阵及公开下载验证，以NUGET-PREVIEW.md最终记录为准。无Mac GPU，Metal实机始终未验证。

## 包内依赖说明修正与第三候选

- preview.2的Mac核心回归成功，新几何及464图层原版检查也已通过；上传前复核发现旧文档脚本以子串替换0.1.1，误改PdfPig 0.1.16的说明，实际nuspec依赖一直正确。发布作业42307036472在上传前取消，未发布该候选，不移动旧标签。
- 重写README版本同步，只更新SimdSkia命令/标题/包对齐说明，第三方依赖版本从实际Pdf项目读取；包校验检查所有README中的PdfPig版本。当前候选0.1.2-preview.3，核心渲染代码相对preview.2没有变化，既有性能不是重新测量。
- 本地preview.2实际包还额外通过464组图层原版精确比较、1417编解码与22PDF检查；这些属于候选本地包证据，不是preview.3公开下载证明。最终公开版本和实际CI结果见发布记录。
