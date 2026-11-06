"""Freeze the tested filter/utilities snapshot without overwriting older failures."""
import gzip,hashlib,json,shutil,statistics
from pathlib import Path
src=Path('artifacts/api-completion-2026-11-06');out=Path('docs/evidence/image-filter-api-2026-11-06')
if out.exists():raise SystemExit('Use a fresh evidence directory.')
out.mkdir(parents=True);load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
for name in ['image-filter-check-11','utility-check-2','filter-timing-1','filter-cache-diagnostics','regression-7']:
    for f in (src/name).rglob('*'):
        if not f.is_file():continue
        if name.startswith('regression') and f.parent!=src/name:continue
        if f.suffix not in ['.json','.log','.bin','.rgba']:continue
        dest=out/f.relative_to(src);dest.parent.mkdir(parents=True,exist_ok=True)
        if f.suffix in ['.bin','.rgba']:
            with gzip.GzipFile(str(dest)+'.gz','wb',mtime=0) as z:z.write(f.read_bytes())
        else:shutil.copy2(f,dest)
for name in ['full-build-10.log','aot-publish-7b.log','aot-result-7b.log','image-filter-lifetime-6.log','filter-source-hashes.json']:
    shutil.copy2(src/name,out/name)
for name in ['baseline-3.119.4.json']:shutil.copy2(Path('docs/api')/name,out/name)
for kind in ['implemented','comparison']:shutil.copy2(src/f'{kind}-filter-3.json',out/f'{kind}.json')
audit=load(out/'comparison.json');pixels=load(out/'image-filter-check-11/comparison.json');timing=load(out/'filter-timing-1/summary.json')
assert load(out/'filter-timing-1/manifest.json')['complete']
groups={}
for row in pixels['cases']:groups.setdefault(row['metadata']['name'],[]).append(row)
status=dict(goalComplete=False,targetMaximumMissing=99,matching=audit['MatchingEntries'],missing=len(audit['MissingOrDifferent']),extra=len(audit['AddedOrDifferent']),publicTypes=156,baselineHash=audit['BaselineHash'],replacementHash=audit['ReplacementHash'],published=False,filterImagesPassed=pixels['passedCases'],filterImages=len(pixels['cases']),filterMetadataPassed=pixels['metadataPassed'],utilityCases=195,utilityPassed=True,lifetimeAndExactChecks=60132,aotChecks=63,knownJpegFailure=True,wholePage='0/18; not rerun',scope='CPU bitmap image-filter microbenchmarks, no whole-app or GPU throughput parity')
(out/'status.json').write_text(json.dumps(status,indent=2)+'\n',encoding='utf-8')
lines=['# 图像滤镜 API、像素链路和 CPU 热点优化（未发布）','',
'2026-11-06，Windows x64。固定 SkiaSharp 3.119.4 的 **3235 项**公共基线，当前冻结快照 **2549 匹配、156 类型、686 缺口、0 额外签名**。比文字快照增加 94 项元数据，仍未达到最多 99 项；公开 0.1.1 不变。元数据匹配不证明完整行为兼容，也不是二进制替换保证。后续旧版文字重载、放大镜等改动不包含在本快照中。','',
'## 真实实现','',
'- 增加独立于后端的图像滤镜合同和带设备原点的离屏中间图像；CPU 实现颜色、矩阵、偏移、合并、混合/算术、模糊、阴影、膨胀/腐蚀、卷积、位移、图像源、着色器源和六类光照。滤镜对象通过 Paint 和 SaveLayer 参与实际绘图。',
'- 图像源、输入节点、派生图、Paint、Clone 和延迟恢复的图层有独立资源租约；最后一个所有者才触发外部像素释放。普通绘图透明度属于滤镜输入；SaveLayer 的透明度在滤镜之后应用。',
'- CPU 图像模糊按基线引用的 Skia **7dbfc07dd33181f84e0958afb7ee805c6c769f0b** 实现 SVG 三次 box 累加、大 sigma 的 tent 切换，以及每个轴结束时舍入。原本不同的高斯核不能作为原版画质一致性的依据。',
'- 先保留独立的 64 位标量三遍算法，再加入 128 位四通道 SIMD 环形缓冲和精确商修正；不支持 intrinsics 时走标量。减少透明边距处理，整数 PMA 传递使用行拷贝/合成，颜色滤镜复用相同颜色的连续区段。',
'- 中间图像用连续缓冲和池化，每幅限制 128 MiB；图深超过 64、Gaussian sigma 超过 256 device pixels、卷积超过 4096 系数等资源边界明确拒绝。它们属于与原版仍不同的行为限制，不能只看签名认为完全兼容。',
'- 新图像滤镜的 Vulkan / Direct2D 设备实现尚未完成，独立后端在修改像素前明确拒绝；Avalonia 的显式 CPU 回退识别滤镜/混合，文字 atlas 不会绕过滤镜后当成成功。',
'- StringUtilities 真实转换 UTF-8/16/32 并保留基线旧版验证行为；SKSwizzle 连接现有标量/SIMD 通道交换，覆盖原地与重叠内存。ReadonlySpan 目的参数是基线遗留的可写内存约定，必须传入可写存储。Span 全长重载按 32 位像素计数，避免照搬旧绑定把字节数当像素数而越界；这个安全修正明确不同于危险的旧行为。','',
'## 测试结果','',
'| 检查 | 结果 |','| --- | --- |',
'| 原版滤镜逐图对照 | **677/756** 过 max≤3、RMSE≤0.75；79 幅仍失败，严格工具退出 2 |',
'| 工厂空值、输入包装释放和 Clone/Reset | 已测元数据一致，2 项原版生命周期检查通过 |',
'| 托管生命周期、真实 GPU 拒绝及精确优化 | **60132** 项通过，覆盖共享外部像素的最后释放、SaveLayer 延迟恢复、padding、两像素格式、Alpha、裁剪和大窗口边界 |',
'| SIMD 对独立标量 | 覆盖 1×1、2×7、9×3、31×17 和 15 个 sigma（含 134/135/136、255.9/256），逐字节一致 |',
'| 编码转换/Swizzle 原版对照 | **195** 组通过；另外 68 项本地安全检查通过 |',
'| 实际 Native AOT | **63** 项通过，包含滤镜、文字、外部像素和动态代码不可用检查 |',
'| 整库 Release 构建 | **0 警告、0 错误** |',
'| 基础/像素/编解码/PDF/特性/平台字体/优化/Avalonia/控件/Direct2D/Vulkan/采样 | regression-7 通过；JPEG 独立严格门槛仍失败 |',
'',
'regression-6 曾因沙箱禁止测试临时字体文件重命名而失败，正常权限重跑的平台字体 43 项和 regression-7 通过；不把环境失败隐藏为绿色。JPEG、旧文字 15/392、字体 122 字段、区域和整页 **0/18 未重测**的既有差异继续保留。滤镜通过数不是原版整页画质通过数。','',
'| 滤镜场景组 | 通过/总数 | 最大通道差 |','| --- | ---: | ---: |']
for name,rows in groups.items():lines.append(f"| {name} | {sum(r['passed'] for r in rows)}/{len(rows)} | {max(r['maximum'] for r in rows)} |")
lines+=['','失败主要为分数偏移与采样、部分混合、位移/平铺边界及图像源插值；point diffuse 还有一幅 RMSE 略超过固定 0.75。全部失败原样归档，不放宽阈值。','',
'## CPU 速度、CPU 时间与内存','',
'同机 i7-8700K（6 核/12 线程）、Windows 26300、.NET SDK 10.0.403/runtime 10.0.14，CPU JIT bitmap 384×192，240×80 半透明矩形。相同采样、颜色和滤镜参数；每 worker 预热 2 s、计时 0.8 s，三轮轮换、9 个串行进程。冻结 DLL/JSON 并核验哈希，期间没有编译或其他测试。before 是真实滤镜初版，after 是本报告快照。','',
'| 场景 | 原版 µs | 初版 µs | 当前 µs | 当前/原版耗时 | 原版/当前 CPU µs | 当前分配 B/帧 |','| --- | ---: | ---: | ---: | ---: | ---: | ---: |']
for r in timing:
    o,b,n=(r[v] for v in ['original','before','after'])
    lines.append(f"| {r['scene']} | {o['msPerFrame']*1000:.3f} | {b['msPerFrame']*1000:.3f} | {n['msPerFrame']*1000:.3f} | {r['afterOverOriginal']:.3f}× | {o['cpuMsPerFrame']*1000:.3f}/{n['cpuMsPerFrame']*1000:.3f} | {n['allocatedBytesPerFrame']:.1f} |")
lines+=['','偏移与颜色已快于原版，膨胀也较快；sigma 2 / 12 模糊和阴影仍约慢 102% / 59% / 22%，**没有达到整体最多落后 10%**。sigma 12 初版约 12.07ms→0.576ms，约 21 倍；阴影分配约 97KB→1.35KB，但还存在必要对象分配。不能拿某个快场景掩盖其余落后。','',
'默认设置下记录的内存如下。Native 堆不计入 GC 分配；工作集和 Private 才包含原生与托管整体进程开销。','',
'| 场景 | 原版/当前工作集 MiB | 原版/当前 Private MiB | 原版/当前整机 CPU % |','| --- | ---: | ---: | ---: |']
for r in timing:
    o,n=r['original'],r['after'];lines.append(f"| {r['scene']} | {o['workingSetMiB']:.2f}/{n['workingSetMiB']:.2f} | {o['privateMiB']:.2f}/{n['privateMiB']:.2f} | {o['cpuPercentOfMachine']:.2f}/{n['cpuPercentOfMachine']:.2f} |")
lines+=['','为检查内存差异是否来自公开资源缓存，追加相同原版 worker 的默认预算/16MiB 预算三轮轮换实验。公开资源缓存统计为 0，调整预算未解释工作集差异；内存归因尚未完成，不能直接认定是缓存或泄漏。该实验单独列出，不混入速度主表，也不把它称作整应用内存优势。','',
'| 原版场景 | 默认/16MiB 时间 µs | 默认/16MiB 工作集 MiB | 默认/16MiB缓存使用 MiB |','| --- | ---: | ---: | ---: |']
cache=out/'filter-cache-diagnostics'
for scene in [r['scene'] for r in timing]:
    summary={}
    for variant in ['default','limited']:
        data=[next(r for r in load(cache/f'{variant}-{fork}/results.json') if r['scene']==scene) for fork in range(3)]
        info=[load(cache/f'{variant}-{fork}/{scene}-cache.json') for fork in range(3)]
        summary[variant]=dict(time=statistics.median(r['msPerFrame'] for r in data)*1000,workingSet=statistics.median(r['workingSetMiB'] for r in data),used=statistics.median(r['totalBytesUsed'] for r in info)/1048576,limit=info[0]['totalByteLimit']/1048576)
    d,l=summary['default'],summary['limited'];lines.append(f"| {scene} | {d['time']:.3f}/{l['time']:.3f} | {d['workingSet']:.2f}/{l['workingSet']:.2f} | {d['used']:.2f}/{l['used']:.2f} |")
lines+=['','公开缓存统计不覆盖所有内部缓存或分配器保留内存，本实验不足以确定原版工作集较大的根因。SimdSkia 也没有同等滤镜输出缓存，复杂结果的长期重用尚需比较。还需要真实应用的长时间内存、冷启动、持续变化输入和 GPU 吞吐验证。','',
'## 复现与证据','',
'```powershell',r'$env:PATH="$PWD\artifacts\dotnet-sdk-10.0.403;"+$env:PATH',
'dotnet build SimdSkia.sln -c Release',
'python tools/check-image-filters.py --output artifacts/image-filter-new',
'python tools/check-utilities.py --output artifacts/utilities-new',
'dotnet run --project tests/ImageFilters/Lifecycle -c Release',
'dotnet publish tests/Foundations/NativeAot -c Release -r win-x64 -o artifacts/aot-filter-new',
r'.\artifacts\aot-filter-new\NativeAot.exe --require-aot',
'```','',
'后续新增 API 会扩展用例，当前源码重跑数量可能多于冻结快照。原始逐图字节以 gzip 无损压缩，按 cases.json offset/length 可复算；速度、CPU、Private、工作集、缓存元数据、源码哈希、命令与完整失败在 [image-filter-api-2026-11-06](evidence/image-filter-api-2026-11-06)。没有重新发布包，也没有重写已冻结的旧失败。']
Path('docs/IMAGE-FILTER-API-2026-11-06.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
for kind in ['implemented','comparison']:shutil.copy2(out/f'{kind}.json',Path('docs/api')/f'{kind}.json')
for name in ['README.md','docs/PROGRESS.md','docs/VALIDATION.md','docs/COMPATIBILITY.md']:
    p=Path(name);old=p.read_text(encoding='utf-8-sig');link='docs/IMAGE-FILTER-API-2026-11-06.md' if name=='README.md' else 'IMAGE-FILTER-API-2026-11-06.md'
    start=old.index('当前未发布源码匹配');end=old.index('\n',start)
    text=f'当前已冻结源码匹配 **2549/3235**、156 公开类型，仍缺 **686 项**，额外签名 0；未达到最多 99 项，任务继续。新增真实图像滤镜图、文字缓冲、编码转换与像素访问；原版滤镜画质 **677/756**、文字 **15/392**，其余失败保留。实际 AOT **63** 项、生命周期/精确对照 **60132** 项通过。偏移与颜色快于原版，但两个模糊微场景仍慢约 102%/59%，不宣称整应用或 GPU 持平。公开 **0.1.1 不变**，没有发布新包。[本批 API、CPU、内存和剩余差异]({link})。'
    p.write_text(old[:start]+text+old[end:],encoding='utf-8')
p=Path('docs/API-COMPLETION-WORKLOG-2026-11-06.md');p.write_text(p.read_text(encoding='utf-8-sig')+'\n图像滤镜与工具批次冻结到 **2549/3235、686 缺口、0 额外**。真实 AOT 63 项、生命周期/字节 60132 项通过；严格图像仍 79/756 幅失败。偏移/颜色较快、模糊与阴影仍慢，默认缓存与 16MiB 原版缓存的内存诊断单独归档。见 IMAGE-FILTER-API-2026-11-06.md；旧版文字转发和放大镜继续实现，不计入本冻结快照，目标仍进行。\n',encoding='utf-8')
(out/'manifest.json').write_text(json.dumps({str(p.relative_to(out)):hashlib.sha256(p.read_bytes()).hexdigest() for p in out.rglob('*') if p.is_file()},indent=2)+'\n',encoding='utf-8');print(json.dumps(status));print(sum(f.stat().st_size for f in out.rglob('*') if f.is_file()))
