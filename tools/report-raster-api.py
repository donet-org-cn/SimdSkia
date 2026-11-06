"""Freeze this incremental raster/API evidence without rewriting earlier failures."""
import hashlib,json,shutil
from pathlib import Path
src=Path('artifacts/api-completion-2026-11-06');out=Path('docs/evidence/raster-api-2026-11-06')
if out.exists():raise SystemExit('Evidence directory already exists; create a new snapshot.')
out.mkdir(parents=True)
for folder in ['image-check-6','canvas-check-3','blend-check-6','raster-check-7','filter-check-8']:
 shutil.copytree(src/folder,out/folder)
for folder in ['raster-timing-1','raster-timing-4','regression-3']:
 for file in (src/folder).rglob('*'):
  # Keep logs and timing metadata; raw benchmark frames/control screenshots
  # remain in ignored artifacts, with hashes here rather than 240 MiB in Git.
  if file.is_file() and ((folder.startswith('raster-timing') and file.suffix=='.json') or (file.parent==src/folder and file.suffix in ['.json','.log'])):
   dest=out/folder/file.relative_to(src/folder);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,dest)
 write_placeholder=None
 frames={str(f.relative_to(src/folder)):hashlib.sha256(f.read_bytes()).hexdigest() for f in (src/folder).rglob('*') if f.is_file() and f.suffix in ['.rgba','.png','.ppm']}
 (out/folder/'frame-hashes.json').write_text(json.dumps(frames,indent=2)+'\n',encoding='utf-8')
for name in ['full-build-6.log','aot-publish-5.log','aot-result-5.log','optimization-6.log','optimization-fallback-6.log','features-6.log','imaging-5.log']:
 shutil.copy2(src/name,out/name)
for name in ['baseline-3.119.4.json','implemented.json','comparison.json']:shutil.copy2(Path('docs/api')/name,out/name)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));audit=load(out/'comparison.json');summary=load(out/'raster-timing-4/summary.json');before={r['scene']:r for r in load(out/'raster-timing-1/summary.json')}
sourcefiles=[f for folder in ['src','tests','benchmarks','tools'] for f in Path(folder).rglob('*') if f.is_file() and f.suffix in ['.cs','.csproj','.props','.py','.ps1'] and not any(part in ['bin','obj','__pycache__'] for part in f.parts)]
write=lambda p,v:p.write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8')
write(out/'source-hashes.json',{str(f):hashlib.sha256(f.read_bytes()).hexdigest() for f in sourcefiles})
status=dict(goalComplete=False,targetMaximumMissing=99,matching=audit['MatchingEntries'],missing=len(audit['MissingOrDifferent']),extra=len(audit['AddedOrDifferent']),publicTypes=146,baselineHash=audit['BaselineHash'],replacementHash=audit['ReplacementHash'],published=False,aotChecks=48,exactOptimizationChecks=474413,strictFailures=dict(blends=48,canvasImages=36,canvasMetadata=1,rasterShaderImages=194,colorFilters=0,imageFactories=32,priorFontScalars=186,priorRegionFields=8,priorJpegStillFails=True,priorWholePage='0/18, not rerun'),scope='CPU raster microbenchmarks, API/behavior/pixels, AOT and Windows GPU regressions; no whole-app or GPU throughput parity claim')
write(out/'status.json',status)
lines=['# 光栅 API 继续补全与热点优化（未发布）','',f"2026-11-06，Windows x64。固定 SkiaSharp 3.119.4 / 3235 项基线，当前 **{status['matching']} 匹配、146 类型、{status['missing']} 缺口、0 额外签名**。比上一冻结快照增加 67 项元数据；仍未达到最多 99 项，任务继续。公开 0.1.1 不变，没有发布新包。元数据数目包含重载、字段和类型，不等于完整功能数量，也不证明二进制兼容。",'','## 真实实现','',
'- CPU 补齐另外 26 种 Porter–Duff/可分离/不可分离混合，既有三个模式的优化路径继续保留。SKBlender 的模式与算术公式参与实际组合着色器求值。',
'- SKBitmap/SKImage.ToShader、CreateBitmap/CreateImage 支持 Clamp/Repeat/Mirror/Decal、独立 X/Y 平铺、最近邻/线性/Mitchell/Catmull–Rom 采样及局部矩阵；在预乘空间插值。Mipmaps、各向异性仍显式拒绝。',
'- 颜色矩阵、HSLA、Lighting、模式混合、Luma、ARGB 查表、伽马、Compose/Lerp、高对比度支持着色器、画笔和图片链路。高对比度使用直通线性光 RGB 工作域。',
'- 位图着色器按可变性创建快照/保留视图；派生着色器、画笔和 Clone 有独立租约，最后释放才触发像素回调。仅资源租约有终结器，普通画笔/纯色着色器没有新增终结器；每次绘图不做引用计数。',
'- 默认解码匹配原版 N32/预乘合同，转换复用解码存储，不分配第二张完整图片。底层 ImageCodec 仍返回直通 RGBA。初始 image-check-3 的较大误差来自工厂合同，不能全部归为 1 级舍入；保留旧失败，修正后相同字节输入仍有 32 组最大 1 级严格差异。',
'- 点绘制与常用 Canvas 重载已经运行；细线/描边和默认字体 hinting 仍存在行为差异。',
'','## 测试结果','',
'| 对照或回归 | 结果 |','| --- | --- |',
'| 颜色滤镜三条绘图链路 | **2448/2448**，最大通道差 ≤1，工厂空值行为一致 |',
'| 29 种矩形混合 + SetPixel | **2928/2976**；新增 26 模式的 2496 幅和 192 组 SetPixel 通过；48 失败属于原有三个模式 |',
'| 图像/组合/算术着色器 | **746/940**；新增 Src 192 幅全部通过。194 失败为 AA 184、双线性 1、组合 7、算术 2；阈值保持 max≤2/RMSE≤0.5 |',
'| 点与线 | **144/180**；36 细线图失败，另默认字体测量原版 80 / 本库 80.874，抗锯齿初始标志已修正 |',
'| 图片工厂 | 181 组、各 230 生命周期检查，仍 32 组最大 1 字节差异，严格比较退出 2 |',
'| 精确优化 | **474413**，包括 2328 组 Multiply SIMD/标量、格式、非法 PMA 物理字节、完整向量和尾部；全部 intrinsics 禁用也通过 |',
'| 实际 Native AOT | **48**，执行验证动态代码不可用；外部像素/着色器/画笔释放回调只执行一次 |',
'| 基础 / 像素 / 编解码 / PDF / 字体发现 | 36 / 1358 / 1417 / 18 / 43 通过 |',
'| Avalonia 文字 / 控件 | 595 / 9750 通过 |',
'| Direct2D / Vulkan 采样 | 148114 / 617 通过；含新增模式/滤镜拒绝前不修改像素及真实完成等待 |',
'| 独立图片 | 12410 断言通过后，JPEG 编码严格门槛仍失败 |',
'',
'完整 Release 构建 0 警告、0 错误。旧 Features PNG 测试错误地要求低 Alpha 直通颜色在默认预乘解码后精确恢复，现严格核对预乘物理字节，并保留旧日志。新增检查没有把失败改为跳过或放宽阈值。既有字体 186 字段、区域 8 字段差异继续保留，整页画质 **0/18 尚未重测**。GPU 对新增未支持的混合/滤镜/图像着色器显式拒绝，由调用端选择 CPU 回退；GPU 回归通过不代表这些功能已 GPU 加速。',
'','## CPU 速度、CPU 时间与内存','',
'同机 i7-8700K（6 核/12 线程）、Windows 26300、.NET SDK 10.0.403/runtime 10.0.14，CPU bitmap JIT。每报告 6 个串行进程、3 轮轮换，预热 0.4 s、计时 0.8 s；两端尺寸、采样/抗锯齿/混合设置相同，冻结发布目录并核对 SHA-256，计时期间没有构建/回归并行。下表是热缓存微场景，不是整页、首帧、端到端 OCR、AOT 吞吐或 GPU 性能。',
'','| CPU 场景 | 原版 ms | 本库 ms | 本库/原版 | 本库初始实现 ms |','| --- | ---: | ---: | ---: | ---: |']
for r in summary:lines.append(f"| {r['scene']} | {r['original']['msPerFrame']:.5f} | {r['after']['msPerFrame']:.5f} | {r['afterOverOriginal']:.2f} | {before[r['scene']]['after']['msPerFrame']:.5f} |")
lines+=['','这九个微场景的最大相对落后约 6%，不表示整体兼容性/画质或 GPU 已满足要求。初始实现是本轮新增算法的第一版，不是已公开版本；两个报告各自原版中位数保留，不能声称整库相对 0.1.1 提升同样倍数。重复图片 Src 和渐变使用有界热缓存，冷启动没有从表中得到验证。',
'','| 场景 | CPU ms 原版/本库 | CPU 占整机 % 原版/本库 | 托管分配 B/帧 原版/本库 | WS MiB 原版/本库 | Private MiB 原版/本库 |','| --- | ---: | ---: | ---: | ---: | ---: |']
for r in summary:
 o=r['original'];m=r['after'];lines.append(f"| {r['scene']} | {o['cpuMsPerFrame']:.5f}/{m['cpuMsPerFrame']:.5f} | {o['cpuPercentOfMachine']:.2f}/{m['cpuPercentOfMachine']:.2f} | {o['allocatedBytesPerFrame']:.2f}/{m['allocatedBytesPerFrame']:.2f} | {o['workingSetMiB']:.2f}/{m['workingSetMiB']:.2f} | {o['privateMiB']:.2f}/{m['privateMiB']:.2f} |")
lines+=['',
'内存仍有差距，图片场景本库工作集比原版约高 7 MiB，不能只用接近零的稳态托管分配宣称占用更低。分配列不包含原版原生堆；WS/Private 是整个工作进程在该场景后的瞬时值，受前序 JIT、GC、缓存影响，既非峰值也非单 API 保留字节。',
'',
'热点修复包括：Src 图片复用采样行、连续复制并保留 AA/stride/格式语义；Multiply 的 AVX2 饱和运算与标量等价；图片着色器最近邻行缓存上限 64 条/4 MiB，按完整变换与源行定位；常量滤镜一次求值和量化、整行填充。关闭 SIMD 的回退不改变像素。',
'','## 复现与继续工作','',
'```powershell',
'dotnet build SimdSkia.sln -c Release',
'python tools/check-blends.py --output artifacts/my-blends',
'python tools/check-canvas-operations.py --output artifacts/my-canvas',
'python tools/check-raster-shaders.py --output artifacts/my-raster-shaders',
'python tools/check-color-filters.py --output artifacts/my-color-filters',
'# 前三项当前严格退出 2，颜色滤镜退出 0。所有输出目录须为新的。',
'python tools/run-api-completion-regressions.py --output artifacts/my-regressions',
'dotnet publish tests/Foundations/NativeAot -c Release -r win-x64 -o artifacts/my-aot',
'./artifacts/my-aot/NativeAot.exe --require-aot',
'# 原版和本库分别 publish 到独立目录，计时期间停止其他构建/测试：',
'python tools/run-raster-benchmarks.py --original artifacts/my-original --after artifacts/my-managed --output artifacts/my-timing',
'```','',
'仍需继续文本运行/Blob、滤镜、记录回放、颜色管理/格式、编解码细项、路径操作、外部 GPU 与其他公共 API。许多既有签名也仍有明确拒绝项；851 是元数据缺口，不是全部行为缺陷数量。下一批继续，不把本报告作为任务完成或发布许可。',
'',
'[逐项 API](api/comparison.json) · [原始证据及状态](evidence/raster-api-2026-11-06/status.json) · [源码及证据哈希](evidence/raster-api-2026-11-06/sha256.json) · [前一基础批报告](FOUNDATIONS-API-2026-11-06.md)']
Path('docs/RASTER-API-2026-11-06.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
write(out/'sha256.json',{str(f.relative_to(out)):hashlib.sha256(f.read_bytes()).hexdigest() for f in out.rglob('*') if f.is_file() and f.name!='sha256.json'})
print(status['matching'],status['missing'],len(list(out.rglob('*'))),'evidence entries')
