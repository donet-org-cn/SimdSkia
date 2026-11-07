"""Freeze completed hotspot trials and generate their report and charts."""
import argparse,gzip,hashlib,json,pathlib,shutil,statistics,subprocess
p=argparse.ArgumentParser();p.add_argument('--input',type=pathlib.Path,default=pathlib.Path('artifacts/scroll-opt-2026-11-07'));p.add_argument('--output',type=pathlib.Path,default=pathlib.Path('docs/evidence/hotspots-2026-11-07'));p.add_argument('--resume-unsealed',action='store_true');a=p.parse_args()
root=a.input;out=a.output
if out.exists() and (not a.resume_unsealed or (out/'sha256.json').exists()):raise ValueError('Evidence already frozen; use a fresh directory')
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
for stage in ['scroll-final','slow-verified','gpu-pages-final']:
    if not load(root/stage/'manifest.json')['complete']:raise ValueError('Incomplete '+stage)
out.mkdir(parents=True,exist_ok=a.resume_unsealed);frames={}
def save(path,value):path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(value,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
stages=['scroll-final','slow-verified','gpu-pages-final','event-pair','slow-cases-pilot2','compact-pilot','slow-final','scroll-before']
for stage in stages:
    for f in (root/stage).rglob('*'):
        if not f.is_file():continue
        rel=f.relative_to(root);dst=out/rel
        if f.suffix=='.rgba':
            data=f.read_bytes();digest=hashlib.sha256(data).hexdigest();obj=f'objects/{digest}.rgba.gz';target=out/obj;target.parent.mkdir(exist_ok=True)
            if not target.exists():target.write_bytes(gzip.compress(data,mtime=0))
            frames[rel.as_posix()]=dict(object=obj,length=len(data),sha256=digest)
        elif f.suffix in ['.json','.txt','.ticks']:
            dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dst)
for f in root.glob('*'):
    if f.is_file() and (f.name in ['validation-final.json','source-final.json','final-run.py','before-aot-inputs.txt','after-aot-inputs.txt'] or f.name.startswith('churn-verified-') or f.name.startswith('verify-') or f.name.startswith('publish-source-') or f.name=='build-final-verified.log'):
        shutil.copy2(f,out/f.name)
for f in (root/'rejected-general-batch').iterdir():
    dst=out/'rejected-general-batch'/f.name;dst.parent.mkdir(exist_ok=True);shutil.copy2(f,dst)
save(out/'frame-objects.json',frames)
changed=subprocess.check_output(['git','diff','--name-only'],text=True).splitlines()
new=['benchmarks/GlyphRunChurn/GlyphRunChurn.csproj','benchmarks/GlyphRunChurn/Program.cs','src/SimdSkia.Gpu/Shaders/renderer-ordered.comp','src/SimdSkia.Gpu/Shaders/renderer-ordered.spv','tools/build-ordered-rectangle-shader.ps1','tools/run-scroll-regression.py','tools/run-gpu-slow-cases.py','tools/report-hotspots.py','docs/shader-build-ordered.json']
for name in set(changed+new):
    f=pathlib.Path(name)
    if f.exists() and (name.startswith(('src/','tests/','benchmarks/','tools/','examples/')) or name=='docs/shader-build-ordered.json'):
        dst=out/'source'/name;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dst)
(out/'product.patch').write_bytes(subprocess.check_output(['git','diff','--binary','--','src','tests','benchmarks','examples','tools']))
save(out/'context.json',dict(stableVersion='0.1.2',published=False,baselineCommit='a1e5aaaeeedce0104b5c3c18f0390c8b4be55a1e',selectedStages=['scroll-final','slow-verified','gpu-pages-final'],intermediateStages={'event-pair':'one-fork event-always candidate: CPU lower, latency higher; final opt-in only','slow-cases-pilot2':'rejected general ordered shader, not selected','compact-pilot':'16-command compact shader before final area restriction','slow-final':'three-fork checkpoint before large-edge count restriction; higher 1024 curve memory; not final','scroll-before':'initial --no-restore accidentally reused 0.1.2 package assets; before exe equals stable exe; excluded from source before/after attribution'},aotInputs='Explicit restored frozen source-before assemblies versus current source-after project references; exact ILC inputs retained',limitations=['one Windows GPU only','no new native-swapchain original comparison','full-page/text/curve native quality gates still fail','memory is whole process, not VRAM'],harnessRetry='one incorrect Optimization test DLL path returned 1 before the corrected path passed; retained in validation-final.json'))
micro=load(root/'slow-verified/summary.json');scroll=load(root/'scroll-final/summary.json');pages=load(root/'gpu-pages-final/summary.json');quality=load(root/'gpu-pages-final/quality.json')
for case in pages:
    for v in ['vulkan','direct2d']:
        old=(root/'gpu-before'/f"{case['scene']}-{case['scale']}-{v}-0/scene.rgba").read_bytes()
        new=(root/'gpu-pages-final'/f"{case['scene']}-{case['scale']}-{v}-0/scene.rgba").read_bytes()
        if old!=new:raise ValueError('Whole-page pixels changed in '+v)
churn={v:{k:statistics.median(load(root/f'churn-verified-{v}-{i}.json')[k] for i in range(3)) for k in ['msPerFrame','cpuMsPerFrame','allocatedBytesPerFrame','workingSetMiB','privateMiB']} for v in ['before','after']}
if len({load(root/f'churn-verified-{v}-{i}.json')['pixelsSha256'] for v in churn for i in range(3)})!=1:raise ValueError('Churn pixels differ')
save(out/'overview.json',dict(micro=micro,scroll=scroll,pages=pages,quality=quality,churn=churn,wholePageBeforeAfterExact=True))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
assets=pathlib.Path('docs/assets/hotspots-2026-11-07');assets.mkdir(parents=True,exist_ok=True)
fig,axes=plt.subplots(1,2,figsize=(13,4.5),layout='constrained')
labels=[r['scene'].replace('-changing','')+'\n'+str(r['width']) for r in micro]
for j,(v,label) in enumerate([('original','Skia Vulkan'),('before','Simd before'),('ordered','Simd after')]):
    axes[0].bar([i+(j-1)*.25 for i in range(len(micro))],[r[v]['milliseconds'] for r in micro],width=.24,label=label)
    axes[1].bar([i+(j-1)*.25 for i in range(len(micro))],[r[v]['allocation'] for r in micro],width=.24,label=label)
axes[0].set_yscale('log');axes[0].set_ylabel('Completed draw + readback ms (log)')
axes[1].set_yscale('symlog',linthresh=1);axes[1].set_ylabel('Managed bytes/frame (symlog, linear below 1)')
for axis in axes:axis.set_xticks(range(len(labels)),labels,fontsize=8);axis.legend(fontsize=8);axis.grid(axis='y',alpha=.2);axis.set_axisbelow(True)
fig.suptitle('Windows GTX 1050 Ti, JIT, 3 forks; separate from AOT and native presentation\nOnly blend passes native pixel gate; curve/text timings are diagnostic',fontsize=11)
for ext in ['png','svg']:fig.savefig(assets/f'gpu-hotspots.{ext}',dpi=150)
plt.close(fig)
lines=['# 性能热点复查：滚动异常、动态中文与 GPU 混合（2026-11-07）','',
'本批为未发布源码，正式 NuGet 仍为0.1.2。Windows11 / i7-8700K 6核12线程 / GTX1050Ti / .NET10.0.14、SDK10.0.403；Avalonia12.1.5、原版SkiaSharp3.119.4、固定ZCOOL中文字体。构建、测试与测量串行；没有修改显卡/电源设置。CPU与GPU、JIT与Native AOT分别报告。','',
'## 已实施','',
'- 文字内容哈希只在不可变文字对象构造时计算；相同内容的新包装对象复用覆盖率，并在哈希命中后检查完整内容，身份索引不会保留所有临时对象。',
'- 子像素相位共享同一文字轮廓，按引用计数计费；字形/文字默认字节预算保持不变。设备文字覆盖率改为逐项LRU、最多512项/4MiB估算预算，整数平移共享不可变数组，避免64项后整批清空。预算含保留源遮罩及继承裁剪，不等于进程内存上限。',
'- Vulkan小面积、轴对齐、无AA纯色矩形可在同一像素内顺序混合：一次全局读写、最多64条命令、区域≤128K像素；其他命令保留原路径。没有把AA命令改成无AA。通用着色器候选变慢已撤回；8条管线初始化时创建，运行时无着色器编译。',
'- Vulkan小覆盖率边数据允许128项，较大条目仍64项，16MiB预算不变；移除每次未命中都排序并分配整个缓存数组的操作。曾测出大条目扩容增加内存，最终限制大条目数量。',
'- Direct2D离屏事件等待降低CPU但增加延迟，默认保留原有Map路径。可在创建设备前显式设置 `AppContext.SetSwitch("SimdSkia.Direct2D.BlockingReadback", true)` 选择该取舍；原生窗口继续使用上一批两帧完成队列。','',
'## 60 FPS滚动：旧7.528倍记录的复核','',
'同一未修改的0.1.2二进制在本轮未重现旧5.5232ms/约3.21MiB分配。旧证据保留，根因仍未定位，不能把这段差值归因于本批修复。正式复测强制还原来源，另构建冻结源码修改前/当前源码两个真正AOT；保留ILC输入和不同EXE哈希，排除了最初一次误用NuGet还原缓存的试验。2秒预热、5秒测量、3独立进程中位数；相同1000×720、portable x64/Vector128。','',
'| AOT滚动/60 FPS | 绘制ms | p95 ms | 进程CPU ms/帧 | 整机CPU% | 采样WS MiB | Private MiB | 分配KiB/次 |','|---|---:|---:|---:|---:|---:|---:|---:|']
for v,label in [('original-full','原版'),('stable-0.1.2','未修改0.1.2'),('before','源码修改前'),('after','源码修改后')]:
    r=scroll[v];lines.append(f"| {label} | {r['drawMsPerFrame']:.4f} | {r['p95Ms']:.4f} | {r['cpuMsPerFrame']:.3f} | {r['cpuPercentMachine']:.3f} | {r['sampledMedianWorkingSetMiB']:.2f} | {r['sampledMedianPrivateMiB']:.2f} | {r['allocatedBytesPerFrame']/1024:.2f} |")
lines+=['','绘制计时排除Task.Delay；总进程CPU/分配包含让出UI线程后的窗口呈现和调度。截止时间延迟不等于显示器丢帧，离屏Render不等于扫描输出FPS。源码修改前后固定帧逐字节相同，原版严格画质失败仍保留。','',
'## Vulkan历史慢项：真正完成绘制与读回','',
'3fork，每进程每项5批×目标150ms，预热≥500ms并校准；TieredCompilation/ReadyToRun关闭。双方同GPU、RGBA/Premul，每帧绘制+提交+fence等待+主机读回，采集固定frame=0像素在计时外。原版与本库使用各自正常缓存，修改前/后及关闭新批处理全部保留。','',
'| 场景 | 原版ms | 修改前ms | 修改后ms | 后/前 | 后/原版 | 原版画质 |','|---|---:|---:|---:|---:|---:|---|']
for r in micro:lines.append(f"| {r['scene']} {r['width']}×{r['height']} | {r['original']['milliseconds']:.4f} | {r['before']['milliseconds']:.4f} | {r['ordered']['milliseconds']:.4f} | {r['ordered']['milliseconds']/r['before']['milliseconds']:.3f} | {r['orderedOverOriginal']:.3f} | {'通过' if r['nativeQualityPassed'] else '失败/仅诊断'} max={r['maximumError']} |")
lines+=['','![GPU热点耗时与分配](assets/hotspots-2026-11-07/gpu-hotspots.png)','','图使用对数/对称对数轴；曲线和中文原版像素门槛仍失败，速度数字只能用于排查成本。所有修改前后固定帧完全相同；独立标量/未优化GPU回归另覆盖多个相位和混合顺序。','',
'| GPU微场景 | CPU ms/帧 前→后 | 分配B/帧 前→后 | WS MiB 前→后 | Private MiB 前→后 |','|---|---:|---:|---:|---:|']
for r in micro:
    b,n=r['before'],r['ordered'];lines.append(f"| {r['scene']} {r['width']} | {b['cpuMilliseconds']:.3f}→{n['cpuMilliseconds']:.3f} | {b['allocation']:.1f}→{n['allocation']:.1f} | {b['workingSetMiB']:.2f}→{n['workingSetMiB']:.2f} | {b['privateMiB']:.2f}→{n['privateMiB']:.2f} |")
lines+=['','内存为该微场景采样结束、全收集后但目标仍存活时的整个进程值，含JIT、驱动映射、此前场景缓存；顺序按相同fork打乱。不是纯渲染缓冲或显存测量，WS和Private不可相加。短批CPU计数有粒度，不能把接近0解释为无成本。','',
'## 真实Avalonia页面GPU对照','',
'独立JIT程序，3秒预热/5秒测量/3fork，共36进程。原版Vulkan与ANGLE(D3D11)、本库Vulkan与Direct2D均完成同尺寸BGRA GPU读回；不把CPU对手当作GPU原版，不代表原生交换链或AOT性能。','',
'| 页面 | 原版Vulkan ms | 原版ANGLE ms | Simd Vulkan ms | Simd Direct2D ms |','|---|---:|---:|---:|---:|']
for r in pages:lines.append(f"| {r['scene']} ×{r['scale']} | {r['original-vulkan']['meanMs']:.4f} | {r['original-angle']['meanMs']:.4f} | {r['vulkan']['meanMs']:.4f} | {r['direct2d']['meanMs']:.4f} |")
lines+=['','| 页面/实现 | CPU ms/帧 | 整机CPU% | 采样WS MiB | Private MiB | 分配KiB/帧 |','|---|---:|---:|---:|---:|---:|']
for r in pages:
    for v in ['original-vulkan','original-angle','vulkan','direct2d']:
        x=r[v];lines.append(f"| {r['scene']} ×{r['scale']} / {v} | {x['cpuMsPerFrame']:.3f} | {x['cpuPercentMachine']:.2f} | {x['sampledMedianWorkingSetMiB']:.2f} | {x['sampledMedianPrivateMiB']:.2f} | {x['allocatedBytesPerFrame']/1024:.2f} |")
lines+=['',f"严格原版整页画质通过 **{sum(q['passed'] for q in quality)}/{len(quality)}**（max≤32、RMSE≤2，未改阈值）。本库新旧同后端捕获相同；原版门槛失败时不宣称同画质胜出。Vulkan窗口同步读回、Metal未实机、原生窗口总CPU和内存差距仍需继续解决。",'',
'## CPU文字包装对象压力测试','',f"单独JIT合成场景，2秒预热/3秒/3fork，2048个内容重复的不可变文字对象，每次仍清屏并绘制32个轮廓；不是整应用或原版对照。耗时 {churn['before']['msPerFrame']:.5f}→{churn['after']['msPerFrame']:.5f}ms，分配 {churn['before']['allocatedBytesPerFrame']:.2f}→{churn['after']['allocatedBytesPerFrame']:.3f}B/次；三轮像素哈希相同。",'',
'## 验证与复现','',
'最终Release全解构建、核心36项、474413项标量/SIMD优化检查、13853项Avalonia检查、599项GPU检查（588精确字节）、12174项GPU集成及131项偏好/原生窗口/离屏独立完成事件检查通过。CPU与GPU测试包含缓存淘汰、并发、颜色/透明度、97个文字相位、stride、跨帧回退、资源释放。Native AOT两种源码来源及原版都实际运行，无CoreCLR/JIT；本库未加载Google Skia/HarfBuzz。一次基准驱动DLL路径拼错已修正，记录保留。',
'','```powershell','dotnet build SimdSkia.sln -c Release','dotnet run --project tests/AvaloniaControls -c Release -- --frames 5','dotnet run --project tests/GpuOptimization -c Release -- baseline/fonts/ZCOOLXiaoWei-Regular.ttf','dotnet run --project tests/GpuPreferred -c Release -- --hardware','dotnet build benchmarks/GpuManaged -c Release','dotnet build benchmarks/GpuReference -c Release','python tools/run-gpu-slow-cases.py --output artifacts/new-slow-cases','python tools/run-gpu-parity.py --output artifacts/new-pages','python tools/verify-final-evidence.py --directory docs/evidence/hotspots-2026-11-07','```','',
'AOT复测必须显式还原来源：`dotnet publish examples/AvaloniaAot/SimdSkia -c Release -p:UsePackedSimdSkia=false -p:RestoreForce=true -o <新目录>`；检查ILC输入指向src而非旧NuGet缓存，完整冻结/修改后/原版命令在证据final-run.py与validation-final.json中。图形着色器由[Khronos glslang](https://github.com/KhronosGroup/glslang/releases/tag/16.6.0)离线生成，发布不携带编译器；源码/字节码/编译器哈希保留。','',
'[冻结证据与原始各轮](evidence/hotspots-2026-11-07/) · [汇总](evidence/hotspots-2026-11-07/overview.json) · [旧7.528倍记录](PERFORMANCE-STABLE-0.1.2-AOT-2026-11-07.md)。本批未发布NuGet，不宣称全面兼容、画质通过或所有负载持平。','']
pathlib.Path('docs/PERFORMANCE-HOTSPOTS-2026-11-07.md').write_text('\n'.join(lines),encoding='utf-8')
save(out/'sha256.json',{f.relative_to(out).as_posix():hashlib.sha256(f.read_bytes()).hexdigest() for f in out.rglob('*') if f.is_file() and f.name!='sha256.json'})
save(assets/'manifest.json',dict(generatorSha256=hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),images={f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in assets.iterdir() if f.suffix in ['.png','.svg']}))
print('Frozen evidence:',out)
