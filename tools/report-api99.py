"""Freeze the fixed-baseline milestone with measured results and retained failures."""
import collections,gzip,hashlib,html,json,math,re,shutil
from pathlib import Path
source=Path('artifacts/api-completion-2026-11-06');out=Path('docs/evidence/public-api-99-2026-11-07')
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
audit=load(source/'comparison-final-99b.json');inventory=load(source/'implemented-final-99b.json')
assert audit['BaselineEntries']==3235 and audit['MatchingEntries']==3136 and len(audit['MissingOrDifferent'])==99 and not audit['AddedOrDifferent']
assert audit['BaselineHash']=='03A164AC1097F84793632DF806DA91E2829F0FCA65D8F2B922E45554AB083296'
for name in ['api99-timing-2','raster-final-timing-2','ui-final-timing-2']:assert load(source/name/'manifest.json')['complete'],name
reg=load(source/'regression-12/status.json');assert all(x['exitCode']==0 for x in reg if x['suite']!='jpeg-known-failure');assert next(x for x in reg if x['suite']=='jpeg-known-failure')['exitCode']!=0
for name in ['ContextCapabilities-4','PdfAnnotations-2']:assert load(source/name/'Original/values.json')==load(source/name/'Managed/values.json')
cache_quality=[]
for scene in ['controls-1-cpu-0','controls-1.5-cpu-0']:
 a=(source/'ui-final-timing-1'/scene/'scene.rgba').read_bytes();b=(source/'ui-final-timing-2'/scene/'scene.rgba').read_bytes();assert a==b,'Cache policy changed pixels: '+scene;cache_quality.append(dict(scene=scene,exact=True,sha256=hashlib.sha256(b).hexdigest()))
if out.exists():raise SystemExit('Use a new snapshot directory.')
out.mkdir(parents=True);objects={}
(out/'cache-policy-pixels.json').write_text(json.dumps(cache_quality,indent=2)+'\n',encoding='utf-8')
def archive(file,relative):
 dest=out/relative;dest.parent.mkdir(parents=True,exist_ok=True)
 if file.suffix in ['.bin','.rgba']:
  data=file.read_bytes();digest=hashlib.sha256(data).hexdigest();target=out/'frame-objects'/(digest+'.gz');target.parent.mkdir(exist_ok=True)
  if not target.exists():target.write_bytes(gzip.compress(data,mtime=0))
  objects[str(relative).replace('\\','/')]=dict(sha256=digest,length=len(data),object=str(target.relative_to(out)).replace('\\','/'))
 else:shutil.copy2(file,dest)
folders=['api99-validation-2','api99-timing-2','raster-final-timing-2','ui-final-timing-2','regression-12','ContextCapabilities-4','PdfAnnotations-2','perlin-nohw-1','example99','cache-profile-786432','cache-profile-1572864','cache-final-1','cache-final-1.5','cache-final-scroll']
for folder in folders:
 for file in (source/folder).rglob('*'):
  if file.is_file() and file.suffix in ['.json','.log','.bin','.rgba','.pdf','.png']:archive(file,Path(folder)/file.relative_to(source/folder))
for name in ['full-build-17.log','font-programs-global-cache-3.log','cache-controls-managed-3.json','aot-publish-15.log','aot-result-15.log','aot-result-15-scalar.log','perlin-scalar-summary.log','example99.log','drawing-additions-1.log','drawing-additions-2.log','ContextCapabilities-Original-2.log','aot-result-10.log']:
 archive(source/name,Path(name))
for folder in ['mesh-atlas-1','mesh-atlas-2','drawing-additions-1','regression-10','ui-final-timing-1']:
 for name in ['comparison.json','optimization.log','status.json','INVALID.txt','runs.json']:
  file=source/folder/name
  if file.exists():archive(file,Path('prior-failures')/folder/name)
for sourceName,destName in [('implemented-final-99b.json','implemented.json'),('comparison-final-99b.json','comparison.json')]:archive(source/sourceName,Path(destName))
archive(Path('docs/api/baseline-3.119.4.json'),Path('baseline-3.119.4.json'))
(out/'frame-objects.json').write_text(json.dumps(objects,indent=2)+'\n',encoding='utf-8')
hashes={str(p.relative_to(Path('.'))).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for folder in ['src','tests/CompletionAot','examples/Compatibility99','benchmarks/Api99'] for p in Path(folder).rglob('*') if p.is_file() and p.suffix in ['.cs','.csproj','.json'] and not {'bin','obj'}.intersection(p.parts)}
hashes['artifacts/api-completion-2026-11-06/aot-15/CompletionAot.exe']=hashlib.sha256((source/'aot-15/CompletionAot.exe').read_bytes()).hexdigest()
(out/'source-hashes.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8')
cpu=load(source/'api99-timing-2/summary.json');raster=load(source/'raster-final-timing-2/summary.json');ui=load(source/'ui-final-timing-2/summary.json');uiQuality=load(source/'ui-final-timing-2/quality.json');uiGate=load(source/'ui-final-timing-2/gate.json')
status=dict(matching=3136,baseline=3235,missing=99,extra=0,publicTypes=inventory['Types'],baselineHash=audit['BaselineHash'],replacementHash=audit['ReplacementHash'],goalQuantityReached=True,published=False,newAotChecks=24,exactOptimizationChecks=474413,knownJpegFailure=True,wholePagePassed=sum(x['passed'] for x in uiQuality),wholePageTotal=len(uiQuality),releaseEligible=uiGate['releaseEligible'])
(out/'status.json').write_text(json.dumps(status,indent=2)+'\n',encoding='utf-8')
lines=['# 公共 API 缺口降至 99 项：功能、画质、CPU/GPU 与内存实测','',
'2026-11-07，Windows x64，源码开发快照，**尚未发布**。固定 SkiaSharp **3.119.4 / 3235 项**，现 **3136 匹配、99 缺口、200 公开类型、0 额外签名**（96.94% 元数据匹配）。从上一冻结快照 2726 增加 410 项，缺口由 509 降至 99；更早 1146 快照的缺口为 2089。没有缩小基线、用空绘图或只补抛异常签名凑数。NuGet **0.1.1 不变，不含本轮源码新增能力**。','',
'## 兼容范围','',
'这是源码兼容清单：含类型、方法/重载、属性、枚举、接口、可见性、默认参数和工具检查的属性。元数据匹配不证明所有参数、绘图结果或生命周期已兼容；protected API、工具未提取的属性、nullable 等另需审查。SkiaSharp 强名称程序集与本程序集名称/公钥不同，**不承诺二进制直接替换**。原生 Skia Handle 不伪造，调用方像素指针与系统 Vulkan/GL 互操作另有实际实现。','',
'- RuntimeEffect：C# 类型化 AST 解释执行，uniform/数组/矩阵/完整 32 位整数、child shader/filter/blender、builder 与资源租约。无动态代码生成，AOT 可运行；完整 SkSL 的 helper/loop/switch/unsigned/derivative 等未实现语法返回编译错误。','- Lattice/nine-patch、Picture/Recorder/Drawable、NoDraw/NWay/Overdraw、NullSurface、float 渐变/两圆锥、RawShader/ApplyImageFilter、Perlin、Vertices/Atlas/Coons Patch 和 PDF 注释都参与真实绘图或其定义的非绘图合同。Picture shader 当前为有界光栅 tile，不是随 CTM 自适应的矢量重绘；SKP 序列化仍缺。','- GRContext/SKSurface 新增真实的借用 Vulkan 设备和离屏绘制链路，含完成同步、资源统计、缓存清理与弃用；只拥有本库新建资源，不销毁调用方 VkDevice。单采样 compute 路径；mips/MSAA/保护上下文及外部纹理导入仍有缺口或明确拒绝。GL 接口只验证真实函数表/能力，不表示实现了 GRContext.CreateGl 绘图。','- PDF 生成仍为光栅页，文字不可搜索；新增 URL、跨页命名目标、录制注释回放、SKWStream 输出。读取支持明确无外观且无边框的导航链接；可见注释、表单、其他未实现 PDF 特性仍报错。PDF 不是 SkiaSharp 的原生 PDF 读取 API。','- GPU 不会静默遗漏新增 CPU 功能。当前 GPU 对固定网格/Atlas/Patch、部分 shader/filter/lattice/save-layer 命令明确拒绝；Avalonia 既有配置的回退另有统计。','',
'## 正确性与生命周期','',
'完整 Release **零警告/零错误**。独立回归 basic/pixels/codecs/pdf/features/platform-fonts/optimization/Avalonia/controls/Direct2D/Vulkan/GPU-sampling 全通过；精确优化 **474413** 项，PDF **22** 项。Imaging 在 **12410** 项独立检查后仍失败于既有 JPEG 原版编码画质门槛，未改绿。','',
'| 功能族 | 原版像素通过/总数 | 元数据差异 |','|---|---:|---:|']
for p in sorted((source/'api99-validation-2').glob('*/comparison.json')):
 d=load(p)
 if p.parent.name=='runtime-values':continue
 lines.append(f"| {p.parent.name} | {sum(c['passed'] for c in d['cases'])}/{len(d['cases'])} | {len(d['differences'])}/{d['metadata']} |")
lines+=['','常规新绘图门槛 max channel ≤3、RMSE ≤0.75（以各 comparison.json 的 gate 为准）；未放宽门槛隐藏曲线/文字差异。','',
'Lattice 的 56/56 是明确的 packed-lattice 语义 oracle：基线 C# 包装层把 int 枚举数组交给 byte 布局，原始包装层失败另留历史记录，不能称未修改包装层逐字节一致。Path Boolean 使用 Clipper2 托管运算，曲线按 1/1024 单位展平、六位小数裁剪，结果不保留原曲线 verbs；OpBuilder 的一个拓扑差异与原版全 Union 对 even-odd 输入的优化有关。ApplyImageFilter 只对比定义的 outSubset/offset，未把 native backing 的 padding 当作有效内容。','',
'Runtime uniform 值合同 34/34、48 个 runtime-effect 元数据核对通过；Mesh/Atlas 各 215 个局部断言，新增绘图各 109 个；字体/资源缓存原版和本实现各 30 个并发/禁用/清理检查。真实 ANGLE/Vulkan/COM 各 7 个检查及能力元数据一致；独立 PdfPig 对两种 DPI 的链接、坐标、跨页目标解析一致，各 12 个检查。','',
'实际 Native AOT 新增集成 **24 项**分别在 Vector128 与显式关闭本库 SIMD 的 Scalar 清屏模式通过（不是 JIT 的 PublishAot 属性）。JIT Perlin 144 幅图在所有硬件 intrinsics 禁用时与正常输出逐字节相同，Vector.IsHardwareAccelerated=false。AOT 解释器 child 的外部像素 release callback 在最后一个 Picture/paint 所有者释放后恰好触发一次。原版存在的崩溃、早期失败、回归期间发现并修复的普通路径一字节差异均保留。','',
'## 性能与内存方法','',
'同机 i7-8700K 6 核/12 线程、GTX 1050 Ti、Windows 26300、.NET SDK10.0.403/runtime10.0.14。CPU JIT 三轮、轮换、独立串行进程；新 API 每场景预热0.5s/测量0.8s，旧通用绘图预热0.4s/测量0.8s。最终轮采集期间不构建或并行跑测试。首轮发现 run 缓存仅256KiB导致抖动，已中断并标INVALID：控件100%约6.1ms、150%约12.4ms、每帧约1.58MB分配。保持总预算2MiB，重新分配给packed run后才复验和采集最终轮；没有用增大总预算掩盖问题。下表为三轮中位数，CPU% 分母为12逻辑处理器，满速吞吐的一核饱和约8.3%，不是固定FPS下的CPU/能耗。分配为托管计数，WS/Private为进程测量点，包含JIT/GC/此前场景/缓存，不能当单个对象的净占用。','',
'### 新 API：优化前/当前/原版','','![新增 API 相对原版耗时](images/api99-speed-2026-11-07.png)','',
'优化前为同一 worker 的本轮开发二进制，**不是公开0.1.1，也不是上一冻结2726快照**。优化移除每三角形临时 SKShader/SKPaint，记录时仍由原始 paint 的独立租约保活；固定三角覆盖缓存懒分配、最多4096项/约2MiB，避免曲面轮流挤出64项普通路径 LRU；普通路径边的计算顺序保留旧行为。','',
'| 场景 | 原版 ms | 优化前 ms | 当前 ms | 当前/原版 | 分配 前→后 B/帧 |','|---|---:|---:|---:|---:|---:|']
for r in cpu:lines.append(f"| {r['scene']} | {r['original']['msPerFrame']:.4f} | {r['before']['msPerFrame']:.4f} | {r['after']['msPerFrame']:.4f} | {r['afterOverOriginal']:.2f}× | {r['before']['allocatedBytesPerFrame']:.1f}→{r['after']['allocatedBytesPerFrame']:.1f} |")
lines+=['','| 场景 | 原版/当前 CPU ms/帧 | 原版/当前 CPU% | 原版/当前 WS MiB | 原版/当前 Private MiB |','|---|---:|---:|---:|---:|']
for r in cpu:
 a=r['original'];b=r['after'];lines.append(f"| {r['scene']} | {a['cpuMsPerFrame']:.3f}/{b['cpuMsPerFrame']:.3f} | {a['cpuPercentOfMachine']:.2f}/{b['cpuPercentOfMachine']:.2f} | {a['workingSetMiB']:.2f}/{b['workingSetMiB']:.2f} | {a['privateMiB']:.2f}/{b['privateMiB']:.2f} |")
lines+=['','完整原版像素误差见 api99-timing-2/quality.json，不能把低分配等同于原版吞吐持平。Runtime AST、CPU 网格/Atlas 仍有显著性能差距；Perlin 在本场景快于原版。中英文热路径虽更快，但该基准文字图像与原版 max207/RMSE23.54，未过严格画质门槛，不能作为同画质性能优势。缓存的全局字体默认预算现与原版实测2MiB对齐：outline128KiB、glyph128KiB、run1536KiB、text256KiB；缓存 byte 是估算载荷，Count 是本引擎 owner 数，不是 native strike 数，也不包括存活字体源文件和所有 canvas/GPU 缓存。','',
'### 已有 CPU 绘图复测','',
'| 场景 | 原版 µs | 当前 µs | 当前/原版 | 当前 B/帧 |','|---|---:|---:|---:|---:|']
for r in raster:lines.append(f"| {r['scene']} | {r['original']['msPerFrame']*1000:.2f} | {r['after']['msPerFrame']*1000:.2f} | {r['afterOverOriginal']:.3f}× | {r['after']['allocatedBytesPerFrame']:.2f} |")
lines+=['','### 真实 Avalonia 控件：CPU、Vulkan、Direct2D','','![Avalonia 控件页耗时](images/api99-ui-speed-2026-11-07.png)','',
'使用同一共享控件 harness，三轮，每次3s预热/5s采集，controls(1×/1.5×)和scroll(1×)。GPU 等待实际完成并回读完整 BGRA8888；区分 recording 与 completion，不能拿仅提交时间比较 CPU 完成帧。不是窗口 swapchain 呈现、启动白屏或 AOT 性能。原版 Vulkan 与 ANGLE，以及本实现 Vulkan/Direct2D 是独立 GPU 结果，CPU 单列。','',
'| 场景/缩放 | 原版 CPU 默认/参考 ms | 当前 CPU ms | 原版 Vulkan/ANGLE ms | 当前 Vulkan/Direct2D ms |','|---|---:|---:|---:|---:|']
for r in ui:lines.append(f"| {r['scene']}/{r['scale']} | {r['original-cpu-default']['meanMs']:.3f}/{r['original-cpu']['meanMs']:.3f} | {r['cpu']['meanMs']:.3f} | {r['original-vulkan']['meanMs']:.3f}/{r['original-angle']['meanMs']:.3f} | {r['vulkan']['meanMs']:.3f}/{r['direct2d']['meanMs']:.3f} |")
lines+=['','| 场景/缩放/后端 | CPU% | CPU ms/帧 | 分配 B/帧 | WS MiB | Private MiB |','|---|---:|---:|---:|---:|---:|']
for r in ui:
 for v in ['original-cpu-default','cpu','original-vulkan','vulkan','original-angle','direct2d']:
  a=r[v];lines.append(f"| {r['scene']}/{r['scale']}/{v} | {a['cpuPercentMachine']:.2f} | {a['cpuMsPerFrame']:.3f} | {a['allocatedBytesPerFrame']:.0f} | {a['sampledMedianWorkingSetMiB']:.2f} | {a['sampledMedianPrivateMiB']:.2f} |")
lines+=['',f"整页画质 **{status['wholePagePassed']}/{status['wholePageTotal']}** 通过。正式性能/质量门槛完整结果见 [gate](evidence/public-api-99-2026-11-07/ui-final-timing-2/gate.json)，releaseEligible={status['releaseEligible']}；本次没有发布。签名数达标不意味着整页、GPU 或全部功能已经达到原版。",'',
'![Avalonia进程内存](images/api99-ui-memory-2026-11-07.png)','','## 剩余99项与行为差距','',
'完整逐签名列表见 [comparison.json](evidence/public-api-99-2026-11-07/comparison.json)。这些条目之外，已匹配签名的未支持参数、颜色空间/像素格式、字体/曲线画质、完整SkSL、GPU能力和 PDF 内容也属于行为差距，不能只数99个就宣称全面兼容。此前记录的 Span 7组19字段、路径效果/MaskFilter/描边等原版差异也未在本批证明全面消除，历史原始门槛与结果见 [效果与编解码报告](QUERY-EFFECTS-CODEC-API-2026-11-06.md) 和 [像素报告](PIXEL-API-2026-11-06.md)。','',
'| 类型 | 缺失/不同条目 |','|---|---:|']
counts=collections.Counter(re.search(r' (SkiaSharp\.\w+)',x).group(1) for x in audit['MissingOrDifferent'])
for key,n in counts.most_common():lines.append(f'| {key} | {n} |')
lines+=['','主要剩余：外部 GPU texture/render-target 创建和导入、GL/Metal/D3D GRContext、SKP 序列化、XPS/SVG 输出、ExtractAlpha，以及 Blender/ColorF/dither/LCD/embedded bitmap text 和原生版本/句柄合同。没有通过伪造版本、句柄或借用描述创建成功来计入绘图能力。','',
'## 复现','',
'```powershell','dotnet build SimdSkia.sln -c Release','dotnet run --project examples/Compatibility99 -c Release -- artifacts/examples/compatibility99','python tools/check-api99-batch.py --output artifacts/api99-local','python tools/run-api-completion-regressions.py --output artifacts/api99-regressions','dotnet publish tests/CompletionAot -c Release -r win-x64 -o artifacts/api99-aot','artifacts/api99-aot/CompletionAot.exe artifacts/api99-aot-normal','artifacts/api99-aot/CompletionAot.exe artifacts/api99-aot-scalar --scalar','dotnet run --project tools/ApiAudit -c Release -- extract src/SimdSkia.Compatibility/bin/Release/net10.0/SimdSkia.Compatibility.dll artifacts/api99-implemented.json','dotnet run --project tools/ApiAudit -c Release -- compare docs/api/baseline-3.119.4.json artifacts/api99-implemented.json artifacts/api99-comparison.json','```','',
'含已知差异的 oracle/regression/API compare 返回非零，这是保留失败，不是测试工具损坏。性能 worker 需先按本次 manifest 的文件/版本独立 publish；运行 run-api99-benchmarks.py、run-raster-benchmarks.py 与 run-ui-backend-parity.py（GPU 需真实驱动）。每次选择新的输出目录。','',
'本批 Core 直接引用既已用于 Avalonia 层的 **Clipper2 [2.0.0]**（BSL-1.0、托管路径布尔运算），随包分发许可；未增加 Google Skia 或其他 native 绘图库。字体/编解码仍为 C#，保留此前获准的 .NET 压缩与 macOS 官方窗口宿主例外。Perlin/Coons/固定三角边算法依据固定基线 BSD 源码移植；完整许可见 [DEPENDENCIES](DEPENDENCIES.md)。本次运行验证仅 Windows x64，不把它当 Linux/macOS/ARM 的实机结果。','',
'[示例源码](../examples/Compatibility99/Program.cs) · [验证摘要](evidence/public-api-99-2026-11-07/status.json) · [文件哈希](evidence/public-api-99-2026-11-07/sha256.json)。帧数据按 SHA256 去重保存为 gzip，原路径/字节数见 frame-objects.json。']
Path('docs/PUBLIC-API-99-2026-11-07.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
for name in ['implemented','comparison']:shutil.copy2(out/(name+'.json'),Path('docs/api')/(name+'.json'))
manifest={str(p.relative_to(out)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in out.rglob('*') if p.is_file()}
(out/'sha256.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(json.dumps(status,indent=2));print('Evidence MiB',sum(p.stat().st_size for p in out.rglob('*') if p.is_file())/1048576)
