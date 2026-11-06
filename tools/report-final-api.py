"""Freeze final fixed-baseline tests/timings and generate the Chinese report.

The retained 5 native gaps, quality failures, corrected D3D12 fixture and
unverified Metal are explicit. Raw frames are content-addressed gzip objects.
"""
import gzip,hashlib,json,math,shutil
from pathlib import Path

root=Path('artifacts/api-final-2026-11-07')
out=Path('docs/evidence/api-final-2026-11-07')
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
audit=load(root/'comparison-release.json')
assert audit['BaselineEntries']==3235 and audit['MatchingEntries']==3230
assert len(audit['MissingOrDifferent'])==5 and not audit['AddedOrDifferent']
assert audit['BaselineHash']=='03A164AC1097F84793632DF806DA91E2829F0FCA65D8F2B922E45554AB083296'
timings=['raster-timing-3','ui-final-timing-1','gl-timing-3','gl-paced-2','vk-timing-2','vk-paced-1','api-timing-final-1','d3d-timing-1','d3d-paced-1']
for name in timings:assert load(root/name/'manifest.json')['complete'],name
timed_quality={}
for scene in ['alpha8','float-color','arithmetic']:
    x=(root/'raster-timing-3'/f'{scene}-Original-0/pixels.bin').read_bytes();y=(root/'raster-timing-3'/f'{scene}-Managed-0/pixels.bin').read_bytes()
    assert len(x)==len(y)>0
    delta=[abs(a-b) for a,b in zip(x,y)];maximum=max(delta);rmse=math.sqrt(sum(d*d for d in delta)/len(delta))
    timed_quality[scene]=dict(maximum=maximum,rmse=rmse,passed=maximum<=3 and rmse<=.75)
(root/'raster-timing-3/timed-drawing-quality.json').write_text(json.dumps(timed_quality,indent=2)+'\n',encoding='utf-8')
if out.exists():raise SystemExit('Use a new evidence snapshot; do not overwrite frozen results.')
out.mkdir(parents=True)
objects={}
def archive(file,relative):
    dest=out/relative;dest.parent.mkdir(parents=True,exist_ok=True)
    if file.suffix in ['.bin','.rgba']:
        data=file.read_bytes();digest=hashlib.sha256(data).hexdigest();target=out/'frame-objects'/(digest+'.gz');target.parent.mkdir(exist_ok=True)
        if not target.exists():target.write_bytes(gzip.compress(data,mtime=0))
        objects[str(relative).replace('\\','/')]=dict(sha256=digest,length=len(data),object=str(target.relative_to(out)).replace('\\','/'))
    else:shutil.copy2(file,dest)
folders=timings+['regression-7','api99-final-1','final-validation-1','gpu-completion-final-1','svg-final-2','gl-aot-runtime-4','aot-normal-5','aot-scalar-5','float-sse2-final','example-final-1','jpeg-release','gl-lifetime-probe-1','gl-pending-1','gl-pending-2','d3d-adapter-final-2','vk-device-copy-1','vk-cpu-bridge-reference-1','vk-timing-1','gl-timing-1']
for name in folders:
    for file in (root/name).rglob('*'):
        if any(part.startswith('browser-profile') for part in file.parts):continue
        if file.is_file() and file.suffix in ['.json','.log','.txt','.bin','.rgba','.png','.pdf','.svg','.xps','.skp','.webp']:
            archive(file,Path(name)/file.relative_to(root/name))
for pattern in ['full-build-11.log','production-dependencies-release.log','basic-release.log','jpeg-release.log','aot-build-5.log','aot-normal-5.log','aot-scalar-5.log','gl-aot-build-4.log','gl-aot-runtime-4.log','float-sse2-final.log','example-final-1.log','final-performance-status.json','final-performance-extra-status.json','post-timing-source-delta.json']:
    archive(root/pattern,Path(pattern))
for source,dest in [('comparison-release.json','comparison.json'),('implemented-release.json','implemented.json')]:archive(root/source,Path(dest))
for name in ['baseline-3.119.4.json','intentional-native-gaps.json']:archive(Path('docs/api')/name,Path(name))
for name in ['shader-build.json','shader-build-batch.json']:archive(Path('docs')/name,Path(name))
old=load(Path('docs/evidence/public-api-99-2026-11-07/comparison.json'))
added=sorted(set(old['MissingOrDifferent'])-set(audit['MissingOrDifferent']))
assert len(added)==94
(out/'implemented-additions.json').write_text(json.dumps(added,indent=2)+'\n',encoding='utf-8')
(out/'frame-objects.json').write_text(json.dumps(objects,indent=2)+'\n',encoding='utf-8')
sources={str(p).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest()
         for folder in ['src','tests','benchmarks','tools','examples'] for p in Path(folder).rglob('*')
         if p.is_file() and not {'bin','obj','__pycache__'}.intersection(p.parts) and p.suffix in ['.cs','.csproj','.props','.targets','.comp','.spv','.py','.ps1']}
for name in ['Directory.Build.props','global.json','NuGet.config','.github/workflows/nuget-preview.yml']:
    p=Path(name)
    if p.exists():sources[name]=hashlib.sha256(p.read_bytes()).hexdigest()
(out/'source-hashes.json').write_text(json.dumps(sources,indent=2)+'\n',encoding='utf-8')
executables={str(p).replace('\\','/'):dict(bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest())
             for p in [root/'aot-5/CompletionAot.exe',root/'gl-aot-4/Managed.exe']}
(out/'aot-executables.json').write_text(json.dumps(executables,indent=2)+'\n',encoding='utf-8')

lines=['# SimdSkia 0.1.2-preview.1：公共 API、测试与性能报告','',
'固定源码基线 **SkiaSharp 3.119.4 / 3235 项**。最终匹配 **3230 项、201 公开类型、5 缺口、0 额外签名**；从99项冻结快照补入94项。基线SHA256仍为 `'+audit['BaselineHash']+'`。这五项按用户要求保留，不提供替代句柄或虚构原生版本。','',
'本预览还汇入此前未发布的路径/滤镜/RuntimeEffect/Picture/网格等实现。**公共签名覆盖不等于全面行为兼容，更不等于二进制替换。JPEG、部分文字/曲线、整页画质及性能仍有失败，Metal没有Mac GPU实机验证。** 新包的公开下载状态以[发布记录](NUGET-PREVIEW.md)为准。','',
'## 兼容范围与真实实现','',
'| 新增项 | 本轮条目数 | 实际能力与限制 |','|---|---:|---|',
'| SKImage.FromPicture | 4 | 真正延迟生成，持有录制资源；PeekPixels不隐式生成，ToRasterImage(true)实际生成 |',
'| SKPixmap WebP选项编码 | 3 | 真正lossless/lossy；依赖Sdcb.SimdCodecs1.4.0，质量/压缩率不等于libwebp刻度 |',
'| SKBitmap.ExtractAlpha | 4 | 真正1字节Alpha8、stride、偏移与遮罩；不再保留RGBA工作面 |',
'| SKPaint ColorF/SetColor/Blender/IsDither | 4 | 浮点精度、颜色转换、实际算术混合和有序抖动；新阶段GPU明确拒绝，需显式CPU回退 |',
'| LCD与嵌入点阵 | 3 | BGRA水平LCD三通道；真实EBLC/EBDT中文点阵；RGBA/垂直几何回退灰度，不是完整hinting/复杂排版 |',
'| SKSvgCanvas及Create | 3 | 实际向量路径、裁剪、渐变、图层和图片；不是全页PNG包装 |',
'| SKDocument.CreateXps | 6 | 实际多页OPC/XPS向量包、图片及流生命周期，独立WPF阅读器验证 |',
'| SKPicture Serialize/Deserialize | 8 | 实际原生v103 SKP向量子集，双方交叉读取；其余flattenable/操作码明确报错 |',
'| GRContext GL/D3D12/Metal | 8 | 系统GL/GLES程序、D3D11On12/Direct2D、Metal计算管线；Metal仅编译，未硬件验收 |',
'| SKImage外部纹理/上传 | 23 | 实际GL/VkImage/ID3D12Resource/MTLTexture，借用/接管/回调/方向/完成读回；限制按后端明确拒绝 |',
'| SKSurface外部texture/target | 28 | 真实外部GPU资源内容、生命周期与GPU快照；D3D12/Metal导入表面仍有CPU桥接，Vulkan为设备内复制 |','',
'上述合计94；完整类型/重载/属性/枚举/默认值/签名见[最终清单](api/implemented.json)、[逐项比较](api/comparison.json)及[本轮新增94条](evidence/api-final-2026-11-07/implemented-additions.json)。审计工具不覆盖全部protected API、nullable注解或所有自定义属性，不能将3230解释为整个SkiaSharp全部可用。','',
'保留缺口：`SKObject.Handle`，`SkiaSharpVersion`类型及其`Native`、`NativeMinimum`、`CheckNativeLibraryCompatible`。实际像素指针/系统GPU资源不是native Skia对象句柄。程序集名称/强名称公钥不同，不能让既有SkiaSharp二进制依赖直接装载替换。','',
'GL→GL纹理绘制直接绑定GPU纹理。Vulkan外部表面通过VkImage↔计算buffer设备复制；显式读回直接进入调用方缓冲，普通Flush仍维护CPU shadow。其他GPU图片来源/快照及D3D12/Metal导入表面仍可能发生CPU读回/再上传，不能称全部零拷贝。','',
'所有GPU仍限制MSAA、非sRGB目标、部分像素格式、mips/各向异性/部分采样、复杂着色器/滤镜/混合、LCD及SaveLayer等；不支持时抛明确异常。`IsTextureBacked`表示存在实际设备资源，不代表每个操作都无CPU步骤。预算统计是已知资源负载/分配，不能当作完整驱动VRAM计量。GPU对象必须按创建线程显式Dispose，GC终结回收尚未与原版等同；不能依赖遗忘Dispose后的自动清理时机。','',
'## 测试与保留失败','',
'Release全解决方案构建零错误/警告；生产依赖图检查通过。CPU/几何/光栅/文字为C#；编解码固定托管Sdcb1.4.0，PNG/TIFF允许.NET系统压缩；可选PdfPig0.1.16。GPU仅调用系统图形API/驱动；macOS官方窗口宿主是已允许的原生例外，Metal绘图没有调用Google Skia。','',
'| 测试集合 | 原版像素门槛结果 | 行为/独立检查 |','|---|---:|---|']
for name,label in [('final-raster','延迟Picture/WebP'),('alpha-compatibility','Alpha8/遮罩/采样'),('final-paint','浮点颜色/混合/抖动'),('skp-documents','原生SKP交叉读取'),('embedded-bitmaps','中文嵌入点阵'),('GlContext','GL/GLES绘制'),('GlSystem','系统WGL兼容/Core'),('GlInterop','GL外部纹理/目标'),('VulkanInterop','Vulkan互操作')]:
    folder='gpu-completion-final-1' if name in ['GlInterop','GlSystem'] else 'final-validation-1'
    d=load(root/folder/name/'comparison.json');cases=d['cases'];passed=sum(c['passed'] for c in cases)
    metadata=d.get('metadata','—');differences=len(d.get('differences',[]));extra=len(d.get('extraCases',[]))
    detail=f'{metadata}元数据、{differences}差异'
    if extra:detail+=f'；{extra}额外画面（原版接管工厂返回null）'
    if name=='embedded-bitmaps':detail+='；独立FontTools9/9点阵逐字节正确，整串文字与原版不同'
    if name=='GlInterop':detail+='；双方各595检查，本实现另10生命周期/上下文检查'
    lines.append(f'| {label} | {passed}/{len(cases)} | {detail} |')
svg=load(root/'svg-final-2/comparison.json')
lcd=load(root/'final-validation-1/lcd-comparison.json')
lines += [f"| 独立Edge SVG | {sum(c['passed'] for c in svg['cases'])}/{len(svg['cases'])} | 14元数据一致；原版导出渐变/图层/Difference裁剪存在丢失，不能全算成我们渲染失败 |",
f"| LCD/灰度文字 | {sum(c['passed'] for c in lcd['cases'])}/{len(lcd['cases'])} | 65元数据一致；双方各120读回/生命周期检查，字形覆盖仍不同 |",
'| Direct3D12 | 10/10基础、346/346互操作 | 双方设备检查各68、互操作各516、释放回调各8次；完整Adapter/device/queue下同后端验证，元数据一致 |',
'| XPS | 不把双方差异合并为通过 | 本实现5页、独立WPF14项检查通过；原版同阅读器仍有语义失败 |',
'| Metal | 未验证 | Windows编译通过；无Mac GPU，原版/本实现硬件测试均unverified |','',
'正常回归basic/pixels/codecs/pdf/features/platform-fonts/optimization/avalonia/controls/direct2d/vulkan/gpu-sampling均通过；JPEG仍非零失败。另12个早期API原版对照中Picture、ImageOperations、PathBoolean仍有差异，完整status/comparison都保留。SVG首次新宿主运行超时，改为每幅独立配置目录后完成，原超时日志仍保留。早期D3D12夹具遗漏Adapter，原版返回null是夹具错误，不能作为原版不支持的证据；完整描述符下已更正并重新对照，旧结果仅保留审计历史。','',
'Vulkan精确优化：**566检查、556次逐字节对照、48次实际矩形合批dispatch**，包含AA、渐变、覆盖剪裁、重叠顺序、中文、stride及初始化。浮点绘制在AVX2与禁用AVX2后的SSE2各**5568**缓冲与标量逐字节一致。没有用低质量替代标量算法。','',
'Native AOT：win-x64实际exe运行，新增CPU链路SIMD/显式标量各**41项**通过，Perlin像素SHA均为`24E3170F259F4B25249C6F150A395BD47D273E3A11DCD4096CBABF81BDAC2BD4`；实际系统OpenGL AOT另**18项**通过、8幅输出。AOT不是只编译或在dotnet下运行IL；本次性能表是JIT，不能当作AOT速度/应用体积成绩。','',
'新增GPU共享资源租约修复：SKImage释放后，shader/Picture仍持有实际纹理，回调直到最后消费者释放才执行，双方时机一致；ToRasterImage产生独立CPU副本。追加pending-draw对照发现并修正GL提前回调，双方在draw后/图像释放后均为0，实际Flush完成后为1，读取像素逐字节一致。丢失上下文时先作废真实纹理，再释放待完成租约，避免无当前上下文仍调用GL。GL创建线程/当前上下文/PBO/pixel-store/丢失与上下文先释放均有检查。底部原点、借用与接管、实际外部资源内容均有读回，不以描述符非零当作绘图通过。','',
'门槛保持：独立API绘图/GPU微场景最大通道3、RMSE0.75；Avalonia整页沿用最大通道32、RMSE2（包含文字）；WebP有损最大48、RMSE8。文字/LCD严格像素结果用于揭示差异，不等于已证明字形兼容；未为获得绿灯调整阈值。','',
'## 性能方法','',
'Windows x64、i7-8700K（6核12线程）、GTX1050Ti，.NET10.0.14 / SDK10.0.403，同硬件/尺寸/场景/完成要求，三个独立进程取中位数，测试串行执行。GPU每帧等待实际完成并读回。整机CPU%=进程CPU时间/墙钟时间/12；满速吞吐和固定60FPS分别报告，CPU时间/帧另列。分配是当前托管线程，不含原生堆、驱动或完整跨线程分配。','',
'UI使用真实Avalonia12.1.5控件与同一固定字体：3秒预热、5秒测量、25ms进程采样，离屏/JIT，不是窗口呈现FPS。GPU微场景640×480：1秒预热、2秒测量、10ms采样；Vulkan双方使用同一外部设备测试宿主，进程内存含宿主，不能等同默认原版应用内存。CPU局部场景为端点WS/private，不能冒充采样峰值。驱动显存没有可靠采集，不提供伪造数字。','',
'CPU/UI/Vulkan测量快照早于最后GL回调及D3D12 Adapter修正，相关光栅/控件/Vulkan计时代码未变；保留原始二进制及源哈希，不冒充相同编译产物。差异文件和适用路径记录在post-timing-source-delta.json；发生变化的GL/D3D12路径已重新测试并采集性能。AOT也在这些修正后重新编译运行。','',
'## 完整Avalonia控件页：耗时、CPU与内存','',
'下表原版CPU给出默认/强制灰度两条真实结果。GPU分别列原版Vulkan/ANGLE，与本实现Vulkan/Direct2D；Direct2D对ANGLE是同硬件不同API，不冒充同后端。','',
'| 场景 | 实现/后端 | 完成ms/帧 | CPU ms/帧 | 整机CPU% | WS MiB | 私有MiB | 托管B/帧 |','|---|---|---:|---:|---:|---:|---:|---:|']
ui=load(root/'ui-final-timing-1/summary.json')
for row in ui:
    for key,label in [('original-cpu-default','原版CPU默认'),('original-cpu','原版CPU灰度'),('cpu','SimdSkia CPU'),('original-vulkan','原版Vulkan'),('original-angle','原版ANGLE'),('vulkan','SimdSkia Vulkan'),('direct2d','SimdSkia Direct2D')]:
        d=row[key];lines.append(f"| {row['scene']} ×{row['scale']} | {label} | {d['meanMs']:.4f} | {d['cpuMsPerFrame']:.4f} | {d['cpuPercentMachine']:.2f} | {d['sampledMedianWorkingSetMiB']:.2f} | {d['sampledMedianPrivateMiB']:.2f} | {d['allocatedBytesPerFrame']:.1f} |")
gate=load(root/'ui-final-timing-1/gate.json');quality=load(root/'ui-final-timing-1/quality.json')
lines += ['',f"整页严格画质 **{sum(q['passed'] for q in quality)}/{len(quality)}**；性能门槛原始结果：`{json.dumps(gate,ensure_ascii=False)}`。未通过画质的耗时只作工程诊断，不能宣传同画质整体领先。即使局部吞吐更快，内存和全部功能仍需单独评估。",'',
'![Avalonia完整页耗时](assets/api-final-2026-11-07/ui-time.png)','',
'![Avalonia进程内存](assets/api-final-2026-11-07/ui-memory.png)','',
'![整页原图与差异热图](assets/api-final-2026-11-07/ui-quality.png)','',
'## 同后端GPU微场景','',
'| 后端/场景 | 原版ms | SimdSkia ms | 耗时比 | 原版/本实现CPU ms/帧 | 原版/本实现WS MiB | 画质 |','|---|---:|---:|---:|---:|---:|---|']
for backend,name in [('OpenGL','gl-timing-3'),('Vulkan','vk-timing-2'),('D3D12 / D2D-on-12','d3d-timing-1')]:
    for row in load(root/name/'summary.json'):
        o=row['Original'];m=row['Managed'];lines.append(f"| {backend}/{row['scene']} | {o['msPerFrame']:.4f} | {m['msPerFrame']:.4f} | {row['managedOverOriginal']:.3f} | {o['cpuMsPerFrame']:.4f}/{m['cpuMsPerFrame']:.4f} | {o['sampledMedianWorkingSetMiB']:.2f}/{m['sampledMedianWorkingSetMiB']:.2f} | {'通过' if row['quality']['passed'] else '失败'} |")
lines += ['','![同后端GPU完成耗时](assets/api-final-2026-11-07/gpu-time.png)','',
'![固定60FPS的CPU占用](assets/api-final-2026-11-07/gpu-paced-cpu.png)','',
'固定60FPS（每帧同样完成读回），便于比较相同工作量的CPU占用：','',
'| 后端/场景 | 原版/本实现实际FPS | 原版/本实现整机CPU% | 原版/本实现CPU ms/帧 | 原版/本实现WS MiB | 原版/本实现私有MiB |','|---|---:|---:|---:|---:|---:|']
for backend,name in [('OpenGL','gl-paced-2'),('Vulkan','vk-paced-1'),('D3D12 / D2D-on-12','d3d-paced-1')]:
    for row in load(root/name/'summary.json'):
        o=row['Original'];m=row['Managed'];lines.append(f"| {backend}/{row['scene']} | {o['framesPerSecond']:.2f}/{m['framesPerSecond']:.2f} | {o['cpuPercentOfMachine']:.2f}/{m['cpuPercentOfMachine']:.2f} | {o['cpuMsPerFrame']:.4f}/{m['cpuMsPerFrame']:.4f} | {o['sampledMedianWorkingSetMiB']:.2f}/{m['sampledMedianWorkingSetMiB']:.2f} | {o['sampledMedianPrivateMiB']:.2f}/{m['sampledMedianPrivateMiB']:.2f} |")
lines += ['','CPU%在两秒短测/低负载下受系统计时粒度影响，保留所有fork与采样，不以单次小数差异断言节省。','',
'Vulkan新路径与同一二进制强制的CPU桥接参考逐字节相同；这里仅隔离设备复制/直接读回收益，双方都包含矩形合批/清屏/去分配优化：','',
'| 场景 | CPU桥接ms | 设备路径ms | 耗时下降 | 本实现托管B/帧 |','|---|---:|---:|---:|---:|']
for row in load(root/'vk-timing-2/summary.json'):
    r=row['Reference']['msPerFrame'];m=row['Managed']['msPerFrame'];lines.append(f"| {row['scene']} | {r:.4f} | {m:.4f} | {(1-m/r)*100:.1f}% | {row['Managed']['allocatedBytesPerFrame']:.1f} |")
lines += ['','矩形合批只接受可证明互不重叠的一组命令，保留覆盖/颜色/顺序；Clear使用真实设备填充。去除每批LINQ排序/枚举链与Stopwatch对象，零预算时不再保留覆盖缓存；没有用降低AA/忽略颜色交换性能。','',
'## CPU功能场景','',
'| 场景 | 原版ms | SimdSkia ms | 耗时比 | 原版/本实现CPU ms/帧 | 原版/本实现WS MiB | 原版/本实现私有MiB | 原版/本实现托管B/帧 | 质量核对 |','|---|---:|---:|---:|---:|---:|---:|---:|---|']
for folder in ['raster-timing-3','api-timing-final-1']:
    for row in load(root/folder/'summary.json'):
        q=timed_quality.get(row['scene'],row.get('quality'))
        quality_label=('通过' if q['passed'] else '失败') if q else '独立SKP/WebP测试另验，非输入缓冲画质'
        o=row['Original'];m=row['Managed'];lines.append(f"| {row['scene']} | {o['msPerFrame']:.4f} | {m['msPerFrame']:.4f} | {row['managedOverOriginal']:.3f} | {o['cpuMsPerFrame']:.4f}/{m['cpuMsPerFrame']:.4f} | {o['workingSetMiB']:.2f}/{m['workingSetMiB']:.2f} | {o['privateMiB']:.2f}/{m['privateMiB']:.2f} | {o['allocatedBytesPerFrame']:.1f}/{m['allocatedBytesPerFrame']:.1f} | {quality_label} |")
lines += ['','A8的标量参考/优化输出在324幅用例及计时帧逐字节相等；空间优势指A8像素存储与省去RGBA工作面，不代表整个进程内存低于原版。RuntimeEffect解释执行、部分网格、WebP lossless等仍有明显差距；库内算法/依赖性能和整页缓存分别列示，不能归为同一个瓶颈。','',
'## 结论、限制与复现','',
'本轮94项已接通真实实现，五项原生合同继续缺失；**全面行为兼容、全部画质过门槛、整体持平且最差落后不超过10%的目标仍未全部达到**。预览发布不会改变这些结论。后续优先项是字形/曲线覆盖、GPU高级状态与离屏层、Vulkan计算/转移同步、解释执行/编码热点及内存，而不是继续增加无行为的公开签名。','',
'原版与本库同名SkiaSharp类型默认不能在同一编译单元无别名混用；独立进程用于隔离对照。SkiaSharp仅存在于Original测试项目，不进入生产包。系统字体依平台发现；中文TrueType与基本中英文布局已有实现，不承诺复杂脚本/全部字体格式。PDF读取/显示是可选SimdSkia.Pdf子集，非完整PDF规范实现。','',
'```powershell',
'dotnet build SimdSkia.sln -c Release',
'dotnet run --project examples/Compatibility99 -c Release',
'python tools/run-api-completion-regressions.py --output artifacts/reproduce/regression',
'python tools/check-api99-batch.py --output artifacts/reproduce/api99',
'python tools/run-final-compatibility.py --output artifacts/reproduce/final-api',
'python tools/run-ui-backend-parity.py --output artifacts/reproduce/ui',
'python tools/run-gpu-interop-benchmarks.py --output artifacts/reproduce/gl',
'python tools/run-gpu-interop-benchmarks.py --output artifacts/reproduce/gl60 --paced',
'python tools/run-vulkan-interop-benchmarks.py --output artifacts/reproduce/vk',
'python tools/run-vulkan-interop-benchmarks.py --output artifacts/reproduce/vk60 --paced',
'python tools/run-gpu-interop-benchmarks.py --backend d3d12 --output artifacts/reproduce/d3d12',
'python tools/run-gpu-interop-benchmarks.py --backend d3d12 --output artifacts/reproduce/d3d12-60 --paced',
'python tools/run-final-raster-benchmarks.py --output artifacts/reproduce/raster',
'python tools/run-final-api-benchmarks.py --output artifacts/reproduce/api',
'```','',
'需要.NET10.0.403+、已固定的测试字体与真实GPU驱动；将DOTNET_ROOT指向相同SDK，避免apphost拾取旧runtime。性能按上面顺序串行运行。检查工具质量失败保留退出2；无GPU/Metal硬件保留退出3，SVG宿主失败为执行错误，不能计作绘图通过。NativeAOT使用tests/CompletionAot与tests/GlSystem/Managed执行dotnet publish -c Release -r win-x64 -p:PublishAot=true，再运行发布exe。','',
'[完整冻结证据](evidence/api-final-2026-11-07/)包括status、comparison、每fork计时/进程采样、输入/二进制哈希、源文件哈希及压缩原始像素。frame-objects.json把原路径映射到SHA256对象，gzip解压后按length/hash验证。此前99项快照及试验失败均保留，未覆盖为新结果。']
Path('docs/PUBLIC-API-FINAL-2026-11-07.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
hashes={str(p.relative_to(out)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in out.rglob('*') if p.is_file()}
(out/'sha256.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8')
print('Frozen',len(hashes),'evidence files;',len(objects),'frame references; report written.')
