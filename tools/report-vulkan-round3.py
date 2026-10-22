"""Publish reproducible Vulkan measurements, including failures and frozen pixel hashes."""
import argparse
import hashlib
import gzip
import json
from pathlib import Path
import re
import shutil
import statistics

p=argparse.ArgumentParser()
p.add_argument('--root',type=Path,default=Path('artifacts/vulkan-round3'))
p.add_argument('--report',type=Path,default=Path('docs/VULKAN-OPTIMIZATION-2026-10-22.md'))
args=p.parse_args();root=args.root;report=args.report
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def pixel_digest(path):
    data=path.read_bytes() if path.exists() else gzip.decompress(path.with_suffix(path.suffix+'.gz').read_bytes())
    return hashlib.sha256(data).hexdigest()
def log(path):
    data=path.read_bytes()
    return data.decode('utf-16' if data[:2] in (b'\xff\xfe',b'\xfe\xff') else 'utf-8-sig')
def runs(directory):return [read(path) for path in sorted(directory.glob('*/benchmark.json'))]

before=read(root/'window-before/summary.json');after=read(root/'window-verified-final/summary.json')
old_runs=runs(root/'window-before');new_runs=runs(root/'window-verified-final')
earlier_after=read(root/'window-final/summary.json');earlier_runs=runs(root/'window-final')
assert len(old_runs)==len(new_runs)==9
assert all(row['sampleCountSufficient'] and row['cpuFallbackCommands']==0 for row in old_runs+new_runs)
assert not any(row.get('gpuTimestampProfilingEnabled',False) for row in old_runs+new_runs)
for key in ['width','height','scaling','scene','nativeAot','os','architecture']:
    assert len({row[key] for row in old_runs+new_runs})==1,key
gpu_dirs={'before':root/'gpu-before','after':root/'gpu-verified-final'}
gpu_before=read(gpu_dirs['before']/'summary.json');gpu_after=read(gpu_dirs['after']/'summary.json')
gpu_manifests={stage:read(directory/'run.json') for stage,directory in gpu_dirs.items()}
assert all(m['Complete'] for m in gpu_manifests.values())
for key in ['Forks','Samples','TargetSampleMilliseconds','WarmupMilliseconds','Sizes','FontSha256','TieredCompilation','ReadyToRun','ServerGc']:
    assert gpu_manifests['before'][key]==gpu_manifests['after'][key],key
assert len(gpu_before['Rows'])==len(gpu_after['Rows'])==48
for stage,window_stage in [('before','before'),('after','final')]:
    dll=root/f'{window_stage}-runtime/SimdSkia.Gpu.dll'
    recorded=[sha for name,sha in gpu_manifests[stage]['RuntimeSha256'].items() if Path(name).name=='SimdSkia.Gpu.dll']
    assert recorded==[digest(dll).upper()],(stage,'GPU worker/window DLL mismatch')
by_key=lambda r:(r['Scene'],r['Width'],r['Height'],r['Engine'])
old_rows={by_key(r):r for r in gpu_before['Rows']}
frames=[]
for row in gpu_after['Rows']:
    if row['Engine']!='simd':continue
    old=old_rows[by_key(row)]
    assert all(row[k]==old[k] for k in ['MaxPixelError','AllowedPixelError','PixelPass']),by_key(row)
    name=f"{row['Scene']}-{row['Width']}x{row['Height']}.rgba"
    hashes={}
    for engine in ('simd','original'):
        files=[gpu_dirs[stage]/f'{engine}-{fork}'/name for stage in ('before','after') for fork in range(3)]
        values={pixel_digest(f) for f in files};assert len(values)==1,(engine,name,values)
        hashes[engine]=values.pop()
    frames.append({'scene':row['Scene'],'width':row['Width'],'height':row['Height'],'sha256':hashes,'managedFilesChecked':6,'nativeFilesChecked':6,'beforeAfterExactlyEqual':True})

verification=log(root/'verification.log')
for expected in ['PASS: 484','PASS 7845','PASS 595','PASS: 8147','PASS: 25','PASS 540','PASS 599','Matching: 425/3235','missing or different: 2810; added or different: 0']:
    assert expected in verification,expected
assert 'FAIL: original pixel quality gate' in verification
for name,expected in [('Direct2D','PASS 344'),('GlyphBatches','PASS 60'),('PlatformFonts','PASS 10')]:
    assert expected in log(root/f'test-{name}.log'),name
assert 'PASS 12174' in log(root/'test-AvaloniaGpu.log')
assert 'PASS: Core' in log(root/'dependency-isolation.log')
kernel=read(Path('artifacts/optimization-2026-10-21/gpu-kernel-tests.json'))
assert kernel['Checks']==540 and kernel['Comparisons']==531 and kernel['Passed']
smoke={stage:{name:digest(root/f'smoke-{stage}'/name) for name in ('presented.png','window.png')} for stage in ('before','after')}
assert smoke['before']==smoke['after'],'Frozen actual/offscreen window pixels changed'
aot={backend:{'result':(root/f'aot-{backend}/result.txt').read_text(encoding='utf-8-sig'),'runtime':(root/f'aot-{backend}/runtime.txt').read_text(encoding='utf-8-sig')} for backend in ('cpu','direct2d','vulkan')}
assert all('PASS' in row['result'] and 'NativeAot=True' in row['runtime'] for row in aot.values())
profile=read(root/'profile-final/vulkan-profile.json');initial_profile=read(root/'profile-initial/vulkan-profile.json')
assert read(root/'profile-final/benchmark.json')['gpuTimestampProfilingEnabled']
assert all(r['nativeAot'] for r in [read(root/'aot-profile/benchmark.json')])
gpu_pass=sum(r['PixelPass'] for r in gpu_after['Rows'] if r['Engine']=='simd')
assert gpu_pass==12
evidence={
    'baseline':'Avalonia 12.1.4 / SkiaSharp 3.119.4',
    'beforeRepositoryCommit':'3b32f46527cded46a1fc196178dcd9ec1ebe0f78',
    'optimizedSourceCommit':'2257b85dcec8b5ca98271eb4a63d7e573c43422c',
    'desktop':{'before':before,'after':after,'beforeRuns':old_runs,'afterRuns':new_runs,
        'earlierOptimized':earlier_after,'earlierOptimizedRuns':earlier_runs,
        'beforeManifest':read(root/'window-before/manifest.json'),'afterManifest':read(root/'window-verified-final/manifest.json')},
    'vulkan':{'before':gpu_before,'after':gpu_after,'manifests':gpu_manifests,
        'canonicalFrames':frames,'managedCanonicalFilesChecked':144,'nativeCanonicalFilesChecked':144,'ownMaximumChange':0,'nativeQualityPassed':gpu_pass,'nativeQualityTotal':24},
    'diagnostics':{'initial':initial_profile,'final':profile,'gpuClockSnapshot':(root/'profile-final/gpu-clock.csv').read_text(encoding='utf-8-sig'),
        'note':'Raw device ticks, not nanoseconds. Timestamp diagnostics serialize dispatches and are excluded from performance tables. Initial profile already includes the isolated image-AA optimization.'},
    'validation':{'kernel':kernel,'frozenWindowSha256':smoke,'nativeAot':aot,'verificationExit':2,
        'nativeAotExecutable':{'sha256':digest(root/'aot/CrossPlatform.exe'),'bytes':(root/'aot/CrossPlatform.exe').stat().st_size},
        'functionalLogSha256':{name:digest(root/name) for name in ['verification.log','test-AvaloniaGpu.log','test-Direct2D.log','test-GlyphBatches.log','test-PlatformFonts.log','dependency-isolation.log','aot-publish.log']}}
}
out=report.parent/'evidence';out.mkdir(parents=True,exist_ok=True)
evidence_name='vulkan-round3-2026-10-22.json'
(out/evidence_name).write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for stage in ('before','after'):
    shutil.copyfile(gpu_dirs[stage]/'results.csv',out/f'vulkan-round3-{stage}-2026-10-22.csv')
images=report.parent/'images';images.mkdir(exist_ok=True)
for image in gpu_dirs['after'].glob('*.svg'):
    shutil.copyfile(image,images/f'vulkan-round3-{image.name}')
details=report.parent/'PERFORMANCE-VULKAN-ROUND3-DETAILS-2026-10-22.md'
text=details.read_text(encoding='utf-8')
if '[窗口与冻结前后比较]' not in text:
    text=text.replace('\n\n','\n\n[窗口与冻结前后比较](VULKAN-OPTIMIZATION-2026-10-22.md)；下面保留所有原版 GPU 质量失败项及 CPU/内存数据。\n\n',1)
text=re.sub(r'!\[([^\]]*)\]\(\.\./artifacts/vulkan-round3/gpu-verified-final/([^)]*\.svg)\)',r'![\1](images/vulkan-round3-\2)',text)
text=text.replace('[JSON](../artifacts/vulkan-round3/gpu-verified-final/summary.json)','[完整证据 JSON](evidence/'+evidence_name+')')
text=text.replace('[CSV](../artifacts/vulkan-round3/gpu-verified-final/results.csv)','[CSV](evidence/vulkan-round3-after-2026-10-22.csv)')
text=text.replace('[配置](../artifacts/vulkan-round3/gpu-verified-final/run.json)','[配置和哈希](evidence/'+evidence_name+')')
details.write_text(text,encoding='utf-8')

lookup=lambda rows,backend:next(r for r in rows if r['backend']==backend)
v0=lookup(before,'vulkan');v1=lookup(after,'vulkan');cpu=lookup(after,'cpu');d2d=lookup(after,'direct2d')
speedup=v0['drawingP50Ms']/v1['drawingP50Ms'];slow=v1['drawingP50Ms']/cpu['drawingP50Ms']
def mb(n):return n/1048576
lines=['# Vulkan 检查与优化：2026-10-22','',
    f"真实 Avalonia 控件窗口，Vulkan P50 从 **{v0['drawingP50Ms']:.3f} ms 降至 {v1['drawingP50Ms']:.3f} ms**，自身加速 **{speedup:.2f}×**。同场景 CPU **{cpu['drawingP50Ms']:.3f} ms**、Direct2D **{d2d['drawingP50Ms']:.3f} ms**；Vulkan 仍比 CPU 多耗时约 **{(slow-1)*100:.0f}%**。CPU 占用与 Private 没有随帧时间同比下降，完整兼容性仍未通过。",'',
    '本轮是仓库源码优化，已发布的 preview.4 产物不包含这些改动。优化前仓库提交为 `3b32f46527cded46a1fc196178dcd9ec1ebe0f78`；优化后以证据中的源文件、SPIR-V 和运行 DLL 的 SHA-256 标识。', '',
    '## 真实窗口：CPU、GPU、内存', '',
    'i7-8700K / 12 逻辑处理器、GTX 1050 Ti、Windows 10.0.26300 x64。共享控件场景、固定 ZCOOL 字体、1000×720、**100% DPI**，.NET 10 JIT；每种后端 3 个独立进程，轮转顺序，预热 2 秒，正式测量至少 3 秒且 60 帧。以下为三个进程指标的中位数。所有后端均为 SimdSkia；原版 SkiaSharp 的独立 GPU 对照在后文。', '',
    '| 后端 | P50 ms | P95 ms | 机器 CPU% | CPU ms/帧 | 峰值工作集 MiB | 峰值 Private MiB | 托管 KiB/帧 |',
    '| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |']
for stage,rows,raw in [('前',before,old_runs),('后',after,new_runs)]:
    for backend,title in [('cpu','CPU'),('direct2d','Direct2D'),('vulkan','Vulkan')]:
        r=lookup(rows,backend);cost=statistics.median(x['cpuMilliseconds']/x['timedFrames'] for x in raw if x['backend'].lower()==backend)
        lines.append(f"| {title} 优化{stage} | {r['drawingP50Ms']:.3f} | {r['drawingP95Ms']:.3f} | {r['cpuPercentMachine']:.2f} | {cost:.2f} | {mb(r['peakWorkingSetBytes']):.1f} | {mb(r['peakPrivateBytes']):.1f} | {r['allocationBytesPerFrame']/1024:.2f} |")
lines += ['',
    '窗口放在屏幕外，计时覆盖绘制和呈现服务，**不是显示器 FPS**。Vulkan 等待 fence、回读完整帧，再交给 OS framebuffer；Direct2D 等待 GPU event 后原生 DXGI 呈现。三轮 GPU 都为 0 条不支持命令的 CPU 回退；文字图集首次生成及分组透明度离屏层仍由 C# CPU 处理，后续由 GPU 合成。', '',
    '机器 CPU% 包含整个进程的 UI、渲染和运行时线程，100% 表示 12 核逻辑容量全满；单核归一数字和每轮数据在证据中。CPU ms/帧用进程总 CPU 时间除以计时帧数。窗口工作集/Private 在计时区间采样，可能漏掉瞬时尖峰；它们不是显存。分配统计全部托管线程，包含 UI 布局。CPU 和 Direct2D 的绘图算法未改，独立重复测量仍存在数个百分点漂移。', '',
    f"最终 Vulkan 机器 CPU 占用 {v0['cpuPercentMachine']:.2f}%→{v1['cpuPercentMachine']:.2f}%，Private {mb(v0['peakPrivateBytes']):.1f}→{mb(v1['peakPrivateBytes']):.1f} MiB、分配 {v0['allocationBytesPerFrame']/1024:.2f}→{v1['allocationBytesPerFrame']/1024:.2f} KiB/帧。CPU 占用在这一轮下降，但此前一次优化后复测为 {lookup(earlier_after,'vulkan')['cpuPercentMachine']:.2f}%（P50 {lookup(earlier_after,'vulkan')['drawingP50Ms']:.3f} ms），不能据此认定稳定的 CPU 节省；内存没有明显下降。这两轮共 18 个优化后进程均保存在证据中，前一轮之后仅补齐 Opaque 接受条件与诊断开关的批次一致性。", '',
    '## 为什么慢，以及实际改动', '',
    '第一次时间戳诊断中，186×51 像素、348 条边的描边占 GPU 绘制 ticks 的 43.3%；GPU 回读占绘制加回读 ticks 的约 1.4%。该诊断版本已经包含独立的图片 AA 优化，不能当作未修改基线。只读时钟快照显示 GPU 处于 P0，频率由驱动按负载调整。', '',
    '- 高边数路径：原 8 位绕数内核在 128 条候选边时回到逐像素 256 次完整扫描。增加 16 位打包绕数，超过安全范围仍使用不受限扫描；8 位/16 位均保留原来 16×16 AA 样本和整数混合。32764/32768 边、正反绕向和填充规则加入对照。',
    '- 保守边分块：排除射线右侧的边，合并整块区域恒定的绕数贡献；GPU 工作组识别远离边界的整块内部/外部区域。极端坐标保留通用路径。C# 执行几何分块，像素覆盖率仍由 GPU 计算。',
    '- GPU 覆盖率缓存：以不可变路径、完整变换、细分容差、矩形 clip 和 AA 为 key，第二次相同使用才生成；颜色、渐变、复合模式和额外 coverage clip 每次重新合成。动态路径持续直接绘制，绘制顺序保持一致。',
    '- 缓存限 64 项、32 MiB 逻辑预算，含 mask 数据和估算保留几何字节。当前批次引用的 mask 在 fence 完成前不会淘汰，预算不足时继续直接 GPU 光栅化。驱动分配对齐、对象开销不计入该预算；这不是进程显存占用测量。',
    '- 专用管线与输入：按命令种类特化着色器，绘制参数放入 uniform buffer；边和参数统一上传显存，避免内核循环读取主机映射内存。图片轴对齐 AA 横纵分离，保留采样位置。',
    '- BGRA 输出：Vulkan 直接生成 Avalonia framebuffer 的 BGRA，保留 PMA/stride，移除每帧 RGBA 中间缓冲和逐像素通道交换。Opaque 目标保留旧窗口通过 PMA staging 的物理存储语义，Src/Clear 可写非不透明 alpha，新增两种格式的精确回归；Straight 目标仍明确拒绝。',
    '- 覆盖与同步：参数验证后跳过数学上无贡献的透明 SrcOver；仅在能够证明新命令覆盖整张目标时删除之前的排队绘制。按保守绘制包围盒判断依赖，重叠才加输出同步，无依赖区域可以并行执行。', '',
    'GPU 时间戳诊断显式开启，会把绘制串行化，**不纳入上面的性能表**。tick 没有直接当作纳秒或 CPU 占用。默认诊断关闭；可用示例的 `--vulkan-profile` 输出逐条命令和阶段数据。', '',
    '## 同机原版 Vulkan 对照', '',
    '另用 .NET 8.0.32，原版 SkiaSharp 3.119.4 和 SimdSkia 都绘制、提交、等待实际完成并回读整帧；3 个进程 × 7 批、每批目标 60 ms、预热至少 100 ms / 3 帧，关闭 tiered/R2R/server GC。两个尺寸各 12 场景。该结果不与上面的窗口 CPU 时间混算。表中 ms 为批均值中位数；自身加速只比较冻结前后结果。', '',
    '| 尺寸 | 场景 | 优化前 ms | 优化后 ms | 自身加速 | 本轮原版 GPU ms | 原版画质门槛 |',
    '| --- | --- | ---: | ---: | ---: | ---: | --- |']
for row in gpu_after['Rows']:
    if row['Engine']!='simd':continue
    old=old_rows[by_key(row)];native=next(r for r in gpu_after['Rows'] if by_key(r)==(row['Scene'],row['Width'],row['Height'],'original'))
    gate=f"通过 max={row['MaxPixelError']}" if row['PixelPass'] else f"失败 {row['MaxPixelError']}>{row['AllowedPixelError']}"
    lines.append(f"| {row['Width']}×{row['Height']} | {row['Scene']} | {old['MedianMs']:.3f} | {row['MedianMs']:.3f} | {old['MedianMs']/row['MedianMs']:.2f}× | {native['MedianMs']:.3f} | {gate} |")
lines += ['',
    '**原版 GPU 画质仍为 12/24 通过。** 失败场景的速度只供诊断，不能判为超过原版。尤其持续变化的中文仍慢于原版，静态路径的缓存收益不能套用到所有动画。全部 CPU ms/op、机器/单核 CPU%、工作集、Private、保留堆和分配见[逐项报告](PERFORMANCE-VULKAN-ROUND3-DETAILS-2026-10-22.md)及 [CSV](evidence/vulkan-round3-after-2026-10-22.csv)。', '',
    '微基准分配只统计调用线程，窗口分配统计所有线程；原生分配不在托管计数内。微基准内存是完整进程的绝对快照，峰值可能包括先前场景和驱动；不能解读为单个渲染面的内存。', '',
    '## 测试结果与剩余工作', '',
    '- 540 项 GPU 检查通过：531 组逐字节对照、3 个分块边界、4 个缓存/预算与 BGRA 检查、1 个时间戳检查、1 个不支持命令拒绝。参考为不受限 GPU 扫描；另验证全部 144 张冻结前后 managed 帧逐字节相同、144 张原版帧稳定。',
    '- 优化前后实际窗口 `presented.png` 及离屏 `window.png` SHA-256 相同；覆盖首帧后 resize、中文、路径、图片、输入事件和呈现。',
    '- Vulkan 590 采样检查、Avalonia GPU 12174、Direct2D 344、字形合批 60、字体发现 10 项及失败帧清理通过。基础、图片 484、CPU 精确对照 7845、文字 595、控件 8147、GPU 文字 599 与 25 个原版行为探针通过。',
    '- Windows Native AOT 实际运行 CPU/Direct2D/Vulkan 三个完整控件窗口和 Vulkan 时间戳模式，确认实际呈现、resize、`NativeAot=True`，没有加载 Skia/HarfBuzz/coreclr/clrjit。时间表仍为 JIT，不混入 AOT 数据。',
    '- 优化源码提交 `2257b85dcec8b5ca98271eb4a63d7e573c43422c` 的三平台[托管/标量 CI](https://github.com/hnlyf168/SimdSkia/actions/runs/39720596815)、[实际桌面 JIT/AOT/包消费 CI](https://github.com/hnlyf168/SimdSkia/actions/runs/39720596809)及 [Windows 双后端 AOT](https://github.com/hnlyf168/SimdSkia/actions/runs/39720596787)全部通过；已下载核对 [9 条实际窗口记录](evidence/vulkan-round3-ci-2026-10-22.json)。云端窗口运行使用 CPU，不是物理 Linux Vulkan 验证。',
    '- 完整 `verify.ps1 -Reference -Gpu` 返回 2：保留原版画质失败。SK* 仍为 425/3235 匹配、2810 缺失、0 额外/不同；旧图像、完整 UI、字体和高级画刷的既有差异继续记录，不承诺二进制兼容或完整 Avalonia 替换。',
    '- Vulkan 仍通过 framebuffer 回读呈现，128 条命令自动批次仍同步；此次窗口约每帧一个批次。原生 Vulkan swapchain、物理 Linux GPU、macOS GPU/Metal 和更复杂动态路径优化仍待实现/验证。', '',
    '这台机器的当前控件场景，CPU 的内存更低，Windows Direct2D 的绘制时间最低；Vulkan 的严重延迟瓶颈已消除，但仍有固定提交、回读与宿主成本。', '',
    '## 构建与复现', '',
    '```powershell',
    'dotnet build SimdSkia.sln -c Release',
    'powershell -File tools/verify.ps1 -Reference -Gpu',
    'python tools/run-desktop-benchmarks.py --output artifacts/my-window-run --runs 3',
    'powershell -File tools/run-gpu-benchmarks.ps1 -Output artifacts/my-gpu-run -Report artifacts/my-gpu-report.md -Forks 3 -Samples 7 -SampleMilliseconds 60 -WarmupMilliseconds 100',
    'dotnet publish examples/CrossPlatform -c Release -r win-x64 -p:PublishAot=true -o artifacts/my-aot',
    '```', '',
    '发行构建使用已提交的 SPIR-V；修改 GLSL 后运行 `tools/build-shader.ps1 -Compiler <Khronos glslang 路径>`。生产包不包含编译工具或 Skia 原生库。当前 GPU 启用代码仍为 `options.UseGpu(SimdSkiaGpuBackend.Vulkan)`。', '',
    '[完整证据、逐帧数据及哈希](evidence/'+evidence_name+') · [优化前 CSV](evidence/vulkan-round3-before-2026-10-22.csv) · [优化后 CSV](evidence/vulkan-round3-after-2026-10-22.csv) · [上一轮报告](PERFORMANCE-COMPATIBILITY-2026-10-22.md)。本机工作进程的全部采样与像素保留在 `artifacts/vulkan-round3`，部分帧无损归档为 `.rgba.gz`；SHA-256 对应解压后的原始 RGBA，报告工具同时支持两种存储。', '',
    '时间戳实现依据 Khronos 的 [vkCmdWriteTimestamp](https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdWriteTimestamp.html) 和 [vkGetQueryPoolResults](https://docs.vulkan.org/refpages/latest/refpages/source/vkGetQueryPoolResults.html)，检查队列有效位数、在命令中 reset、等待 fence 后读取并处理计数回绕。', '']
report.write_text('\n'.join(lines),encoding='utf-8')
print(f'PASS report: {report}; {speedup:.2f}x window speedup; 144 managed + 144 native canonical files checked; original GPU quality {gpu_pass}/24')
