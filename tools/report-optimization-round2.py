"""Publish measured CPU/window/Vulkan results, retaining every failed quality gate."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

p=argparse.ArgumentParser()
p.add_argument('--root',type=Path,default=Path('artifacts/optimization-round2'))
p.add_argument('--report',type=Path,default=Path('docs/PERFORMANCE-COMPATIBILITY-2026-10-22.md'))
args=p.parse_args();root=args.root;report=args.report
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
cpu=read(root/'cpu-final/results.json')
before=read(root/'desktop-measured-before/summary.json');after=read(root/'desktop-measured-after/summary.json')
gpu_before=read(root/'gpu-baseline-v2/summary.json');gpu=read(root/'gpu-final/summary.json')
features_before=read(root/'features-before.json');features=read(root/'features-final-report/results.json')
assert all(a['name']==b['name'] and (not b['passed'] or a['passed']) for a,b in zip(features['cases'],features_before['cases']))
legacy=read(Path('artifacts/diff/results.json'));api=read(Path('docs/api/comparison.json'))
integrity=read(root/'measurement-integrity.json')
assert not integrity['changedCpuRuntimeFiles']
assert all(v['complete'] and not v['changedSourceFiles'] for v in integrity['gpu'].values())
assert len(cpu['runs'])==36 and len(gpu['Rows'])==48 and len(gpu_before['Rows'])==48
assert all(q['beforeAfterExactlyEqual'] for q in cpu['quality'])
assert all(r['allSampleCountsSufficient'] and r['cpuFallbackCommands']==0 for r in before+after)
required={'verification.log':['PASS 7845','PASS: 8147','PASS 595','PASS: 484','PASS: 25'],
          'scalar-optimization.log':['PASS 7845','False'],
          'scalar-controls.log':['PASS: 8147'],
          'test-Direct2D.log':['PASS 344'],
          'test-AvaloniaGpu.log':['PASS 12172'],
          'test-GlyphBatches.log':['PASS 60'],
          'test-PlatformFonts.log':['PASS 10','PASS framebuffer'],
          'dependency-isolation.log':['PASS: Core','PASS: optional']}
for name,expected in required.items():
    data=(root/name).read_bytes();text=data.decode('utf-16' if data[:2] in (b'\xff\xfe',b'\xfe\xff') else 'utf-8-sig',errors='replace')
    for message in expected:assert message in text,(name,message)
def capture_runs(directory):return [read(f) for f in sorted(directory.glob('*/benchmark.json'))]
evidence={'baseline':'Avalonia 12.1.4 / SkiaSharp 3.119.4',
          'measurementIntegrity':integrity,
          'cpu':{'manifest':read(root/'cpu-final/manifest.json'),**cpu},
          'desktop':{'before':before,'after':after,
                     'beforeManifest':read(root/'desktop-measured-before/manifest.json'),
                     'afterManifest':read(root/'desktop-measured-after/manifest.json'),
                     'beforeRuns':capture_runs(root/'desktop-measured-before'),
                     'afterRuns':capture_runs(root/'desktop-measured-after')},
          'vulkan':{'before':gpu_before,'after':gpu,
                    'beforeManifest':read(root/'gpu-baseline-v2/run.json'),
                    'afterManifest':read(root/'gpu-final/run.json'),
                    'frozenRuntime':read(root/'gpu-before-runtime-v2/provenance.json')},
          'featuresBefore':features_before,'featuresAfter':features,
          'legacyPixels':legacy,
          'api':{k:v for k,v in api.items() if k not in ('MissingOrDifferent','AddedOrDifferent')},
          'verificationLogSha256':{name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in required},
          'nativeAot':{backend:(root/f'aot-{backend}/runtime.txt').read_text(encoding='utf-8') for backend in ('cpu','direct2d','vulkan')}}
out=report.parent/'evidence';out.mkdir(parents=True,exist_ok=True)
images=report.parent/'images';images.mkdir(parents=True,exist_ok=True)
for label,source in [('before','features-before'),('native','features-reference'),('after','features-phase4')]:
    shutil.copyfile(root/source/'image-Tile-Uniform-1.5.png',images/f'tile-round2-{label}-2026-10-22.png')
evidence_name='optimization-round2-2026-10-22.json'
(out/evidence_name).write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for variant,directory in [('before','gpu-baseline-v2'),('after','gpu-final')]:
    shutil.copyfile(root/directory/'results.csv',out/f'vulkan-round2-{variant}-2026-10-22.csv')
def mib(v):return v/1048576
def reduction(old,new):return 100*(1-new/old)
def c(scene,scale,variant):return next(r for r in cpu['summary'] if (r['scenario'],r['scale'],r['variant'])==(scene,scale,variant))
def g(data,scene,width,engine):return next(r for r in data['Rows'] if (r['Scene'],r['Width'],r['Engine'])==(scene,width,engine))
lines=['# 性能与兼容性复测：2026-10-22',
       '',
       '本轮 CPU 三个实际 Avalonia 场景耗时降低约 9%–12%；Direct2D/Vulkan 控件窗口每帧托管分配分别减少约 90%/94%，峰值私有内存分别减少约 39%/40%。ImageBrush 原版对照从 15/40 提升到 38/40。完整 UI、字体/曲线画质与 SK* API 兼容仍未通过，不能据此宣称已经全面替代原版。',
       '',
       f'完整数字、运行清单、程序与字体 SHA、每进程数据及失败项目见 [JSON](evidence/{evidence_name})；Vulkan 全字段见 [优化前 CSV](evidence/vulkan-round2-before-2026-10-22.csv) / [优化后 CSV](evidence/vulkan-round2-after-2026-10-22.csv)。历史报告保留原测量，不用本轮数字覆盖。',
       '',
       '## 测量条件',
       '',
       'Windows x64，i7-8700K / 12 逻辑处理器，GTX 1050 Ti。基线固定 Avalonia 12.1.4、SkiaSharp 3.119.4，原版仅在隔离进程。优化前引擎来自提交 `9d3743f`，已在改代码前冻结运行目录；GPU 冻结 worker 使用重新构建的未改动调用层配合冻结引擎，清单逐一记录文件 SHA。最初误用了旧 1.0 调用层的试跑启动失败，没有纳入任何统计。',
       '',
       '所有正式测量串行进行，未与 profiler 或构建并行。CPU 控件与 Vulkan 离屏使用 .NET 8.0.32；真实窗口使用 .NET 10 JIT，三组数据分开比较。每配置 3 个进程。基准采集时包元数据仍为 preview.3，本轮 C# 优化代码随后以 preview.4 打包；测量 SHA 指向实际测得的二进制，不能当作新包的容器 SHA。发布后的性能没有换用旧版报告数字。',
       '',
       'CPU ms/帧是整个进程处理器时间/帧数；机器 CPU% = 单核归一占用/12。满速循环的 CPU% 不能直接当作固定 60 FPS 占用。工作集、Private、峰值都是整个进程，包含运行时/驱动/缓存，均不是显存。CPU 控件/窗口的 B/帧计入所有托管线程；Vulkan 微基准只计调用线程，均不包含原生分配。短批处理器计时有量化误差，表中使用多进程汇总。',
       '',
       '## CPU：真实控件、布局与模板',
       '',
       '共享 `ControlScene`、固定 OFL 中文字体，`RenderTargetBitmap + ImmediateRenderer`，每进程预热 2.5 秒，测量 600 帧，合计 36 进程 / 21,600 计时帧。表中为每进程均值的中位数。原版同时测试默认逐次 opacity 与完整 opacity 组；两种处理在重叠处可能不同，不挑较慢模式作为唯一对手。完整组是与 SimdSkia 本组处理一致的配置；默认模式另供部署参考。',
       '',
       '| 场景 / 像素 | 优化前 ms | 优化后 ms | 降低 | 原版默认 ms | 原版完整组 ms | 后/原默认 |',
       '|---|---:|---:|---:|---:|---:|---:|']
for scene,scale,label in [('controls',1,'控件 1000×720'),('controls',1.5,'控件 1500×1080'),('scroll',1,'滚动 1000×720')]:
    b,a,n,f=[c(scene,scale,v) for v in ('before','after','default','reference')]
    lines.append(f"| {label} | {b['millisecondsPerFrame']:.3f} | {a['millisecondsPerFrame']:.3f} | {reduction(b['millisecondsPerFrame'],a['millisecondsPerFrame']):.1f}% | {n['millisecondsPerFrame']:.3f} | {f['millisecondsPerFrame']:.3f} | {a['millisecondsPerFrame']/n['millisecondsPerFrame']:.2f}× |")
lines+=['','优化前三个场景与优化后的最终帧**逐字节相同**。与原版完整组的门槛仍是 max≤32、RMSE≤2；三项均失败，默认模式也三项失败。因此原版耗时仅作诊断，不作为“同画质胜过原版”的结论。','',
        '| 场景 / DPI | 引擎 | CPU ms/帧 | 工作集 MiB | Private MiB | 托管 KiB/帧 | GC 后保留 MiB |',
        '|---|---|---:|---:|---:|---:|---:|']
for scene,scale in [('controls',1),('controls',1.5),('scroll',1)]:
    for variant,label in [('default','原版默认'),('before','Simd 优化前'),('after','Simd 优化后')]:
        r=c(scene,scale,variant)
        lines.append(f"| {scene} / {scale} | {label} | {r['cpuMillisecondsPerFrame']:.3f} | {r['workingSetMiB']:.1f} | {r['privateMiB']:.1f} | {r['allocatedBytesPerFrame']/1024:.2f} | {r['managedRetainedAfterGcMiB']:.1f} |")
lines+=['','CPU 内存差距仍在：普通控件的工作集约高 21 MiB，Private 约高 26 MiB，每帧分配约为原版默认的 2.2 倍。本轮主要降低 CPU 热点耗时，未把这项内存差距说成已经解决。P95/P99、首绘、GC 和每个 fork 的范围均在 JSON。','',
        '## 实际窗口：CPU、Direct2D、Vulkan 各自优化前后',
        '',
        '相同动态 Fluent 控件、1000×720 逻辑尺寸、125% DPI（1250×900 像素）；每次预热 2 秒，测量至少 3 秒且至少 60 帧。窗口在屏幕外，测量渲染/呈现服务耗时，不是可见显示器 FPS。Direct2D 使用原生 DXGI 交换链并等待 D3D11 event；Vulkan 等待 fence、回读并复制到系统 framebuffer。截图不在计时中。以下只有 SimdSkia 自身前后比较，没有把它当作与原版窗口 GPU 的同条件比较。',
        '',
        '| 后端 | 版本 | P50 / P95 ms | CPU 机器% | 峰值 WS MiB | 峰值 Private MiB | KiB/帧 |',
        '|---|---|---:|---:|---:|---:|---:|']
for b,a in zip(before,after):
    assert b['backend']==a['backend']
    for r,label in [(b,'优化前'),(a,'优化后')]:
        lines.append(f"| {r['backend']} | {label} | {r['drawingP50Ms']:.3f} / {r['drawingP95Ms']:.3f} | {r['cpuPercentMachine']:.2f} | {mib(r['peakWorkingSetBytes']):.1f} | {mib(r['peakPrivateBytes']):.1f} | {r['allocationBytesPerFrame']/1024:.2f} |")
lines+=['','Direct2D P50 下降 14.4%，Vulkan 下降 9.2%。CPU 窗口 P50 改善约 5.8%，但 P95 从 3.941 升到 4.273 ms，不能声称尾延迟也改善；只有三个进程，CPU% 的进程间波动也保留在原始数据中。窗口峰值是计时区间定时采样的最高值，可能漏掉瞬时峰值，不与 CPU 的进程历史峰值混用。所有 18 次窗口运行都达到采样数要求，GPU 不支持命令的 CPU 回退计数为 0；C# 字形图集和托管离屏层仍是实际工作，不算成纯 GPU 栅格化。','',
        '## 与原版 Vulkan：相同完成与回读边界',
        '',
        '同一 GTX 1050 Ti、相同字体/格式/场景，12 场景×2 尺寸×2 引擎×3 进程×7 批；另重跑冻结引擎的完整对照。每批目标 60 ms，至少 100 ms/3 帧预热再校准；关闭 TieredCompilation、ReadyToRun、Server GC。原版 `ReadPixels` 阻塞等待完成；SimdSkia 显式 fence 后回读。下面是全部命令、提交、完成和回读，绝非只有提交时间，也不能推导原生交换链的可见帧率。',
        '',
        '| 像素 | 场景 | 原版 ms | Simd 前 ms | Simd 后 ms | 后/原版 | Simd P95 ms | 画质 |',
        '|---|---|---:|---:|---:|---:|---:|---|']
for r in gpu['Rows']:
    if r['Engine']!='simd':continue
    native=g(gpu,r['Scene'],r['Width'],'original');old=g(gpu_before,r['Scene'],r['Width'],'simd')
    ratio=f"{r['MedianMs']/native['MedianMs']:.2f}×" if r['PixelPass'] else '—'
    status=f"PASS max={r['MaxPixelError']}" if r['PixelPass'] else f"FAIL {r['MaxPixelError']}>{r['AllowedPixelError']}"
    lines.append(f"| {r['Width']}×{r['Height']} | {r['Scene']} | {native['MedianMs']:.3f} | {old['MedianMs']:.3f} | {r['MedianMs']:.3f} | {ratio} | {r['P95Ms']:.3f} | {status} |")
lines+=['','离屏静态场景原本就持有同一图片对象，不能期待内容去重产生与窗口一样大的提升。小尺寸多矩形/混合仍慢于原版，复杂文字/变化几何仍有明显分配和画质差距。24 场景仅 12 项通过现有 GPU 画质 gate；未通过行不判性能胜负。','',
        '| 1024×768 场景 | 原/Simd CPU ms | 原/Simd WS MiB | 原/Simd Private MiB | 原/Simd KiB/帧 |',
        '|---|---:|---:|---:|---:|']
for r in gpu['Rows']:
    if r['Engine']!='simd' or r['Width']!=1024:continue
    n=g(gpu,r['Scene'],1024,'original')
    lines.append(f"| {r['Scene']} | {n['CpuMsPerOp']:.3f} / {r['CpuMsPerOp']:.3f} | {mib(n['WorkingSetBytes']):.1f} / {mib(r['WorkingSetBytes']):.1f} | {mib(n['PrivateBytes']):.1f} / {mib(r['PrivateBytes']):.1f} | {n['ManagedBytesPerOp']/1024:.2f} / {r['ManagedBytesPerOp']/1024:.2f} |")
lines+=['','最近邻边界额外核查：原版 CPU 与原版 Vulkan 的结果本身不同。例如 256×256 夹具的 (15,8)，CPU 为 RGBA (17,7,0,255)，Vulkan 为 (0,0,0,255)；(8,15) 两者均为 (3,13,0,255)。不能把 GPU 的单轴取样差异全局套到 CPU。本轮没有这样修改通用 `DrawBitmap`，也没有放宽 image 的 0 误差 gate；图片 GPU 行仍明确失败。CPU 原版探针位于 `artifacts/optimization-round2/nearest-cpu-reference`。','',
        '## 实际优化与兼容修正',
        '',
        '- dotnet-trace 10.0.750501 的 sampled-thread-time 诊断显示轴向图片采样 18.16%、水平渐变 13.56%、Span 填充 12.21%、清屏 11.52% 独占采样时间；该比例用于定位此基准热点，不是机器 CPU 占用率。采用运行时展开的 Span.Fill、紧密缓冲整块填充、完全不透明渐变直接行复制、图片扫描行复用与整行 SIMD 合成；关闭硬件指令仍运行同一整数舍入规则。',
        '- Direct2D/Vulkan 纹理缓存增加不同对象间的内容复用。哈希只筛候选，宽高/格式/Alpha/必要 stride 与快照逐字节验证才决定复用；更新 LRU，不增加无界别名。仍限制 256 项/64 MiB，Vulkan 被已提交命令引用的旧纹理等待 fence 后释放。只优化重复上传，不省略颜色转换、覆盖裁剪或 GPU 完成等待。',
        '- 修复 Direct2D Opaque 源图片使用非 255 物理 A 字节的问题。新增硬件检查覆盖两像素格式、三 Alpha、不同包装、源修改、源提前 Dispose、同批旧/新版本和超过缓存上限的淘汰。',
        '- 图片画刷最近邻处理中修正固定精度接缝、单轴镜像的重复边界，以及中间图片边缘的解析面积覆盖率。未把普通路径的 16×16 AA 改成低质量模式。38 个已过场景保存原版压缩 RGBA 夹具，并加入正常 Avalonia 回归；完整 114 项探针仍报告剩余失败。',
        '',
        '| 原版功能探针 | 优化前 | 优化后 |',
        '|---|---:|---:|']
for feature in ['ImageBrush','VisualBrush','Blur','DropShadow','BoxShadow']:
    a=[r for r in features['cases'] if r['feature']==feature];b=[r for r in features_before['cases'] if r['feature']==feature]
    lines.append(f"| {feature} | {sum(r['passed'] for r in b)}/{len(b)} | {sum(r['passed'] for r in a)}/{len(a)} |")
lines+=['',f"总计 {features_before['passed']}/{features_before['total']} → **{features['passed']}/{features['total']}**，没有已通过项目退化。ImageBrush 剩余 `None/Fill` 和 `None/Uniform` 的 150% DPI 采样相位；VisualBrush 高 DPI、圆角阴影和一项 blur 仍失败。",'',
        '150% DPI、Tile + Uniform 的实际渲染：旧 raw RGBA 最大误差 255，修正后为 0。PNG 压缩器不同，比较的是解码后的像素。',
        '',
        '| 优化前 | 原版 | 优化后 |',
        '|---|---|---|',
        '| ![优化前图片画刷](images/tile-round2-before-2026-10-22.png) | ![原版图片画刷](images/tile-round2-native-2026-10-22.png) | ![优化后图片画刷](images/tile-round2-after-2026-10-22.png) |','',
        '## 验证结果与边界',
        '',
        '- Release 全解决方案 0 警告/0 错误；36 基础行为、484 图片、7845 CPU/编解码、595 文字、8147 Avalonia 断言通过。7845/8147 在 DOTNET_EnableHWIntrinsic=0 下再次通过。',
        '- Direct2D 344、Vulkan 采样 590、Vulkan 优化 313、GPU 集成 12172、字形合批 60 项通过；字形合批 480 次描画合为 200 次且保留重叠顺序。字体发现 10 项及失败 framebuffer 清理通过，生产依赖隔离通过。',
        '- Windows Native AOT 实际运行 CPU、Direct2D、Vulkan 三个完整控件窗口，NativeAot=True，首次呈现后 resize，通过且未加载 Skia/HarfBuzz/coreclr/clrjit。AOT 仅验证功能，未混入上述 JIT 性能。三平台 CI 和包发布状态见 [验证文档](VALIDATION.md) / [预览发布](NUGET-PREVIEW.md)。',
        '- 原版旧图像 108/120，完整 UI 0/3，GPU 12/24；保持失败。完整 UI max/RMSE 分别为 96/2.408、117/2.321、75/2.595。新功能 82/114；这些是不同样本集，不能相加成“全兼容率”。',
        '- SK* 公开清单仍是 425/3235 匹配，2810 缺失，0 额外/不同；本轮没有用空壳扩充 API。程序集名称/强名称仍不同，不支持二进制替换。Avalonia 适配器通过平台服务接入，不依赖伪造 SkiaSharp 程序集。',
        '- CFF/可变/彩色字体、hint/LCD/复杂排版/RTL、多种混合模式与部分格式仍未完成；macOS GPU、原生 Wayland、Linux 物理 GPU及 Vulkan 原生窗口交换链仍未验收。字体/曲线 AA 与 CPU 常驻缓存仍是完整替换的主要阻碍。',
        '',
        '## 复现',
        '',
        '先用旧提交构建并复制 `tests/AvaloniaControls` 与 `examples/CrossPlatform` 的完整运行目录，再检出新代码构建；GPU 冻结目录必须来自一致的程序集依赖版本，不能混用旧 1.0 调用层。完整日志与 PNG/RGBA/diff/ticks 在本机 `artifacts/optimization-round2`，正式报告生成不删除失败行。',
        '',
        '```powershell',
        'dotnet build SimdSkia.sln -c Release',
        'powershell -File tools/run-avalonia-controls.ps1 -BeforeDirectory artifacts/optimization-round2/controls-before -Output artifacts/optimization-round2/cpu-final -Frames 600 -Forks 3',
        'python tools/run-desktop-benchmarks.py --output artifacts/optimization-round2/desktop-measured-after --runs 3',
        'powershell -File tools/run-gpu-benchmarks.ps1 -Output artifacts/optimization-round2/gpu-final -Report artifacts/optimization-round2/gpu-final-report.md',
        'powershell -File tools/verify.ps1 -Reference -Gpu',
        'python tools/report-optimization-round2.py',
        '```',
        '',
        '输出目录已有测量清单时请换新目录，以保存旧证据。前三个基准脚本/完整 Reference 验证在画质未过时会返回 2；它表示验收失败，不应改成“全部成功”。窗口脚本只测实际服务，不执行原版画质 gate；汇总脚本也在写完全部证据后以 2 保留未通过状态。']
report.write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('Wrote',report,'and',out/evidence_name)
if not all(q['passed'] for q in cpu['quality']) or features['passed']!=features['total']:raise SystemExit(2)
