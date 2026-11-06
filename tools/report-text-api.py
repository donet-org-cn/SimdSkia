"""Archive the validated incremental text snapshot, preserving every strict failure."""
import gzip,hashlib,json,shutil
from pathlib import Path
src=Path('artifacts/api-completion-2026-11-06');out=Path('docs/evidence/text-api-2026-11-06')
if out.exists():raise SystemExit('Use a fresh evidence directory.')
out.mkdir(parents=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
for directory in ['blob-check-15','font-check-8','blob-timing-2','blob-timing-3','regression-5','font-vm-probe']:
    for f in (src/directory).rglob('*'):
        if not f.is_file() or any(x in ['bin','obj'] for x in f.relative_to(src/directory).parts):continue
        if directory=='font-vm-probe' and f.suffix not in ['.json','.log','.rgba','.csproj']:continue
        if directory.startswith('regression') and f.parent!=src/directory:continue
        if directory.startswith('blob-timing') and f.suffix!='.json':continue
        if f.suffix not in ['.json','.log','.bin','.rgba','.csproj']:continue
        dest=out/f.relative_to(src);dest.parent.mkdir(parents=True,exist_ok=True)
        if f.suffix in ['.bin','.rgba']:
            dest=dest.with_suffix(dest.suffix+'.gz')
            with gzip.GzipFile(str(dest),'wb',mtime=0) as z:z.write(f.read_bytes())
        else:shutil.copy2(f,dest)
for name in ['full-build-9.log','aot-publish-6.log','aot-jit-6.log','aot-result-6.log','font-programs-1.log','d2d-glyph-8.log','gpu-glyph-5.log','text-source-hashes-3.json']:
    shutil.copy2(src/name,out/name)
shutil.copy2('docs/api/baseline-3.119.4.json',out/'baseline-3.119.4.json')
for kind in ['implemented','comparison']:shutil.copy2(src/f'{kind}-blob-3.json',out/f'{kind}.json')
audit=load(out/'comparison.json');blob=load(out/'blob-check-15/comparison.json');timing=load(out/'blob-timing-3/summary.json')
status=dict(goalComplete=False,targetMaximumMissing=99,matching=audit['MatchingEntries'],missing=len(audit['MissingOrDifferent']),extra=len(audit['AddedOrDifferent']),baselineHash=audit['BaselineHash'],replacementHash=audit['ReplacementHash'],publicTypes=153,published=False,textImagePass=blob['passedCases'],textImageCases=len(blob['cases']),textMetadataDifferences=len(blob['metadataErrors']),fontScalarDifferences=122,fontProgramChecks=58237,aotTextChecks=56,wholePage='0/18; not rerun',jpeg='known strict failure retained',scope='CPU JIT text microbenchmarks; no quality parity or whole-app/GPU throughput claim')
(out/'status.json').write_text(json.dumps(status,indent=2)+'\n',encoding='utf-8')
lines=['# 文字 API、字体轮廓与 GPU 遮罩修复（未完成、未发布）','',
'2026-11-06，Windows x64。固定 SkiaSharp 3.119.4 的 3235 项基线，**2455 匹配、153 类型、780 缺口、0 额外签名**。相比上一冻结光栅批次增加 71 项元数据，仍未达到最多 99 项，任务继续；公开 0.1.1 不变。元数据匹配不是画质、功能完整性或二进制兼容的保证。','',
'## 实现与生命周期','',
'- 实现普通、横向、XY、旋转缩放四种文字 Run 的原始/类型化缓冲、UTF-8 注释和 clusters；Build 复制不可变内容并使旧的可写缓冲失效。默认原始缓冲返回空 Span；非文字 Run 的 clusters 原版对应空指针，不暴露危险的伪有效 Span，而是明确拒绝。',
'- 路径定位、旋转缩放工厂、DrawTextOnPath 和文字截线支持真正的轮廓与字形位置。截线保留原版输入顺序、跳过 RS runs，解析曲线交点；不把不相邻间隔擅自合并。',
'- 所有 Run 类型共用按字体弱关联的 64 项 / 4 MiB 位置几何缓存，完整比较复制后的键；普通 DrawText 也复用字形覆盖，避免复制整段路径并保留重叠字形顺序。',
'- Windows 原版字形轮廓在 em 尺寸执行字体程序。用相同 OFL 字体去掉 fpgm/prep/cvt 与 glyph programs 做隔离证明后，移植 MIT 的托管 TrueType 指令解释器，加入托管边界检查、执行预算和错误传播；在 em 尺寸生成轮廓，按原版 64px 路径规格转换。没有调用原生字体栅格器。',
'- 规范轮廓缓存为 256 字形 / 4 MiB，保留后的轮廓不可变；执行后清掉解释器的最后字形数组引用。原始轮廓缓存仍有内存优化余地，不能据此宣称字体内存已全面受限。',
'- 这不等于实现全部 hinting、复杂文字排版、CFF/彩色字体或原版字形位图生成。仍只开放已支持的 Hinting=None，其他模式明确报错。解释器有执行预算，但不宣称已经完成恶意字体安全审计。',
'- 独立 Vulkan / Direct2D 增加托管字形遮罩到 GPU 的提交链路，设备负责合成；其托管遮罩转换缓存限制 64 项 / 4 MiB。没有声称字形覆盖计算也在 GPU 上。Direct2D 修复清屏 COPY 状态被遮罩层继承、擦掉背景的问题。','',
'## 验证结果','',
'| 检查 | 结果 |','| --- | --- |',
'| 字体指令、规范轮廓、预算、缓存驱逐 | **58237** 项通过，600 字形遍历后缓存 256 项 / 1646970 字节 |',
'| 实际 Native AOT 文字、外部像素、资源链路 | **56** 项通过；JIT 55，额外 AOT 检查动态代码不可用 |',
'| 文字 Run / 路径文字 / 普通文字原版图像对照 | **15/392** 通过 max≤32、RMSE≤2；其余失败保留，不能宣称文字画质兼容 |',
'| 7336 组文字元数据 | 仍 72 项差异：56 普通 run 包围盒字段 +16 路径命令数组表示；所有已测截线间隔对齐 |',
'| 两字体 2738 组字体操作、143360 个 glyph 映射 | 映射一致；严格字体标量差异由 186 降至 **122**，仍失败 |',
'| Avalonia 文字 / 控件 | **595 / 9750** 通过 |',
'| Direct2D 实际硬件 | **246418** 项通过，含两格式、AA、低透明度、重叠字形、裁剪与清屏后遮罩 |',
'| Vulkan 实际硬件 | 中文/英文 CPU 对照最大通道差 **0**；队列、采样、线程与驱逐通过 |',
'| 全部 Release 构建 | 0 警告、0 错误（full-build-9） |',
'',
'regression-4 首次暴露 Vulkan 字形命令缺失，修复后 regression-5 通过基础、像素、编解码、PDF、特性、系统字体、精确优化、Avalonia、控件、Direct2D、Vulkan 和采样；JPEG 独立严格门槛仍以非零退出。更早的整页 **0/18** 尚未重测，描边、区域和图片的已有差异仍保留。字体图像失败与 API 缺口分别统计，不用元数据数字掩盖行为差异。','',
'## CPU 吞吐、CPU 时间与内存','',
'同机 i7-8700K、GTX 1050 Ti、.NET 10.0.14，CPU JIT bitmap。每个 worker 预热 2s、计时 0.8s，三轮轮换、9 个串行进程；两端同字体、尺寸与绘图参数，冻结程序集并校验哈希，计时不并行构建或测试。before 是 em 字体解释器及普通文字字形链路之前的内部实现；新旧使用同一个重新编译的基准程序。','',
'**严格文字图像门槛尚未通过，因此以下速度不能作为同画质追平的证据。** 这些是热缓存文字微场景，不是启动、整页、真实 Avalonia 应用或 GPU 吞吐。','',
'| 场景 | 原版 µs | 改动前 µs | 当前 µs | 原版/当前 CPU µs | 原版/当前工作集 MiB | 当前托管分配 B/次 |','| --- | ---: | ---: | ---: | ---: | ---: | ---: |']
for r in timing:
    o,b,n=(r[v] for v in ['original','before','after'])
    lines.append(f"| {r['scene']} | {o['msPerFrame']*1000:.3f} | {b['msPerFrame']*1000:.3f} | {n['msPerFrame']*1000:.3f} | {o['cpuMsPerFrame']*1000:.3f}/{n['cpuMsPerFrame']*1000:.3f} | {o['workingSetMiB']:.2f}/{n['workingSetMiB']:.2f} | {n['allocatedBytesPerFrame']:.2f} |")
lines+=['','完整 JSON 同时记录整机 CPU 占比、Private MiB、进程次数和分配量；原版原生堆不计入 GC 分配，不把托管分配当总内存。RS 从最初约 368µs 降至约 6µs 的改动发生在前一缓存轮次，blob-timing-2 原样归档；本轮不重复归因。','',
'## 复现','',
'```powershell',
   r'$env:PATH="$PWD\artifacts\dotnet-sdk-10.0.403;"+$env:PATH',
'dotnet build SimdSkia.sln -c Release',
'dotnet run --project tests/FontPrograms -c Release',
'python tools/check-text-blobs.py --output artifacts/text-oracle-new',
'dotnet run --project tests/Direct2D -c Release',
'dotnet run --project tests/Gpu -c Release',
'```','',
'严格 oracle 失败返回 2，不能把该状态当绿色验证。实际 AOT 命令和日志见证据；.bin.gz/.rgba.gz 是原始像素的 gzip 无损压缩，解压后可按 cases.json 的 offset/length 重算误差。','',
'原始数据、执行日志、两轮冻结清单和源码哈希在 [text-api-2026-11-06](evidence/text-api-2026-11-06)。TrueType 解释器来源和许可证见 [依赖清单](DEPENDENCIES.md)；后续滤镜/API 改动不包含在这个文字快照的性能结果中。']
Path('docs/TEXT-API-2026-11-06.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
for kind in ['implemented','comparison']:shutil.copy2(out/f'{kind}.json',Path('docs/api')/f'{kind}.json')
for name in ['README.md','docs/PROGRESS.md','docs/VALIDATION.md','docs/COMPATIBILITY.md','docs/API-COMPLETION-WORKLOG-2026-11-06.md']:
    p=Path(name);old=p.read_text(encoding='utf-8-sig');link='docs/TEXT-API-2026-11-06.md' if name=='README.md' else 'TEXT-API-2026-11-06.md'
    p.write_text(old+f'\n文字批次冻结清单已到 **2455/3235、153 类型、780 缺口、0 额外**，仍未达到最多 99 项。实际 AOT 56 项、Direct2D 246418 项通过；严格文字图像只通过 15/392、字体仍 122 字段差异，不宣称画质或全应用性能持平。报告见 [文字 API 与 GPU 遮罩修复]({link})；既有失败保留，公开 0.1.1 不变，后续 API 继续实现。\n',encoding='utf-8')
(out/'manifest.json').write_text(json.dumps({str(p.relative_to(out)):hashlib.sha256(p.read_bytes()).hexdigest() for p in out.rglob('*') if p.is_file()},indent=2)+'\n',encoding='utf-8')
print(json.dumps(status,ensure_ascii=True));print(sum(p.stat().st_size for p in out.rglob('*') if p.is_file()))
