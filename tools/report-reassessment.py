"""Measured feasibility report: CPU, process memory, GPU, and isolated cache-memory experiment."""
import argparse, hashlib, json, statistics
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--cpu',type=Path,default=Path('artifacts/performance/reassessment-2026-10-12'))
p.add_argument('--gpu',type=Path,default=Path('artifacts/performance-gpu/reassessment-2026-10-12'))
p.add_argument('--experiment',type=Path,default=Path('artifacts/performance-gpu/host-cached-experiment-2026-10-12'))
p.add_argument('--output',type=Path,default=Path('docs/REASSESSMENT.md'))
a=p.parse_args()

def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def complete(root):
    m=read(root/'run.json')
    if not m['Complete']:raise ValueError('Incomplete run: '+str(root))
    return m

def index(summary):return {(r['Scene'],r['Width'],r['Height'],r['Engine']):r for r in summary['Rows']}
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
cpum=complete(a.cpu);gpum=complete(a.gpu);expm=complete(a.experiment)
cpu=read(a.cpu/'summary.json');gpu=read(a.gpu/'summary.json')
cr=index(cpu);gr=index(gpu)
oldcpu=index(read(Path('artifacts/performance/optimized-2026-10-12/summary.json')))
oldcpu.update(index(read(Path('artifacts/performance/changing-2026-10-12/summary.json'))))
oldgpu=index(read(Path('results/gpu-opt5/summary.json')))
workers=[(x.parent,read(x)) for x in a.experiment.glob('simd-*/results.json')]
if len(workers)!=gpum['Forks']:raise ValueError('Incomplete experiment workers.')
prototype=[]
for r in gpu['Rows']:
    if r['Engine']!='simd':continue
    k=(r['Scene'],r['Width'],r['Height'],'simd');parts=[]
    for root,worker in workers:
        w=next(w for w in worker['Workloads'] if (w['Scene'],w['Width'],w['Height'])==k[:3]);parts.append((root,worker,w))
        if worker['Runtime']['FontSha256']!=gpu['Runtime']['FontSha256']:raise ValueError('Experiment font mismatch.')
        for field in ['Samples','SampleMilliseconds','WarmupMilliseconds','Sizes','Seed']:
            if worker['Settings'][field]!=gpu['Workers'][0]['Settings'][field]:raise ValueError('Experiment configuration mismatch: '+field)
    basehashes={sha(x) for x in a.gpu.glob(f"simd-*/{r['Scene']}-{r['Width']}x{r['Height']}.rgba")}
    newhashes={sha(root/w['PixelsFile']) for root,_,w in parts}
    if len(basehashes|newhashes)!=1:raise ValueError('Prototype changed pixels: '+str(k))
    samples=[s for _,_,w in parts for s in w['Samples']]
    frames=sum(s['Operations'] for s in samples);wall=sum(s['TotalMilliseconds'] for s in samples);processor=sum(s['CpuMilliseconds'] for s in samples)
    # Same summary definitions as the C# report.
    med=statistics.median(s['TotalMilliseconds']/s['Operations'] for s in samples)
    prototype.append(dict(Scene=k[0],Width=k[1],Height=k[2],MedianMs=med,CpuMsPerOp=processor/frames,CpuPercent=100*processor/wall,MachineCpuPercent=100*processor/wall/cpu['Runtime']['LogicalProcessors'],ManagedBytesPerOp=sum(s['AllocatedBytes'] for s in samples)/frames,PrivateBytes=statistics.median(w['WarmResident']['PrivateBytes'] for _,_,w in parts),WorkingSetBytes=statistics.median(w['WarmResident']['WorkingSetBytes'] for _,_,w in parts),Frames=frames,Forks=len(parts),Samples=len(samples),PixelsIdenticalToCurrent=True,PixelPass=r['PixelPass'],Comparable=r['PixelPass'],MaxPixelError=r['MaxPixelError'],AllowedPixelError=r['AllowedPixelError'],CurrentMs=r['MedianMs'],OriginalMs=gr[(*k[:3],'original')]['MedianMs'],Gain=r['MedianMs']/med))
(a.experiment/'summary.json').write_text(json.dumps(dict(Variant=expm['Variant'],Rows=prototype),ensure_ascii=False,indent=2),encoding='utf-8')

# Confirm previous-vs-current CPU images and annotate which historical scenarios are available.
history=[]
for k,r in cr.items():
    if k[3]!='simd' or k not in oldcpu:continue
    o=oldcpu[k]
    root=Path('artifacts/performance/changing-2026-10-12') if '-changing' in k[0] else Path('artifacts/performance/optimized-2026-10-12')
    hashes={sha(x) for base in [root,a.cpu] for x in base.glob(f"simd-*/{k[0]}-{k[1]}x{k[2]}.rgba")}
    if len(hashes)!=1:raise ValueError('CPU output changed since previous turn: '+str(k))
    history.append(dict(Scene=k[0],Width=k[1],Height=k[2],PreviousMs=o['MedianMs'],CurrentMs=r['MedianMs'],Ratio=r['MedianMs']/o['MedianMs'],PixelsIdentical=True))

lines=['# SimdSkia 当前性能与继续投入评估','',
'本次重新构建并实测当前代码。CPU：14 场景×2 尺寸×3 实现×3 进程×7 批，共 1764 批；GPU：12 场景×2 尺寸×2 实现×3 进程×7 批，共 1008 批。每批目标 100 ms，实际较慢帧允许超过目标。另做 HOST_CACHED 隔离实验 504 批，均串行运行；未用旧报告替代本轮测量。','',
f"硬件：{cpu['Runtime']['Cpu']}、{gpu['Workers'][0]['Runtime']['ExplicitSimd']}；{cpu['Runtime']['Framework']}；{cpu['Runtime']['OS']}。原版 SkiaSharp 3.119.4；同一固定字体 SHA、图像、绘图代码、质量门槛。CPU 与 GPU 各自比较，GPU 每帧包括命令录制、提交、等待和主机回读。",'',
'## 如何读这些数值','',
'耗时是 21 个采样批均值的中位数，P95 在完整报告。CPU ms/帧是累计进程用户+内核处理器时间除以累计帧数，包括 GC/进程内驱动线程；满速 CPU% 是累计 CPU 时间/累计墙钟，100% 表示一个逻辑核持续忙。本机 12 逻辑核，机器总占用等于该数除以 12。它不是固定帧率应用的实际占用。[Microsoft Process.TotalProcessorTime](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.totalprocessortime?view=net-8.0)。','',
'WS 是当前场景预热后的整个进程工作集，private 是整个进程私有提交，均包含运行时、驱动与此前场景；不能当成纯引擎缓冲区、峰值内存或显存。托管 B/帧是计时循环线程分配，不包含 native malloc、其他线程分配和 GPU 显存。完整 CSV 保留 private/WS/retained/peak/GC。','',
'## CPU：1024×768','',
'以下每个箭头为原版→当前 Simd SIMD。诊断场景不作超过原版的性能结论；画质门槛未放宽。','',
'| 场景 | 上轮 Simd ms | 原版 ms | 当前 Simd ms | CPU ms/帧 原→Simd | WS MiB 原→Simd | private MiB 原→Simd | 托管 KiB/帧 原→Simd | 质量 |',
'| --- | ---: | ---: | ---: | --- | --- | --- | --- | --- |']
for scene in ['clear','rectangles','evenodd','stroke','rect-aa','gradient','blend','image','curve-aa','chinese','curve-changing','chinese-changing','png-encode','png-decode']:
    r=cr[(scene,1024,768,'simd')];o=cr[(scene,1024,768,'original')];prev=oldcpu.get((scene,1024,768,'simd'))
    q=f"通过 max={r['MaxPixelError']}" if r['Comparable'] else ('诊断：stored 编码压缩不同' if r['PixelPass'] else f"诊断 max={r['MaxPixelError']}>{r['AllowedPixelError']}")
    lines.append(f"| {scene} | {prev['MedianMs']:.3f} | {o['MedianMs']:.3f} | {r['MedianMs']:.3f} | {o['CpuMsPerOp']:.3f}→{r['CpuMsPerOp']:.3f} | {o['WorkingSetBytes']/1048576:.1f}→{r['WorkingSetBytes']/1048576:.1f} | {o['PrivateBytes']/1048576:.1f}→{r['PrivateBytes']/1048576:.1f} | {o['ManagedBytesPerOp']/1024:.3f}→{r['ManagedBytesPerOp']/1024:.3f} | {q} |")
lines+=['',
'CPU 核心文件与上轮测量 SHA 一致，本轮速度变化主要是执行环境波动；'+str(len(history))+' 个已有 scene-size 的 current/previous canonical 帧字节一致。没有将 GPU 改动解释成 CPU 算法提速。旧数据未采 CPU ms/帧，所以不伪造历史 CPU 使用率；旧/new 内存均可在对应原始 JSON 核查。','',
'## GPU：1024×768','',
'当前列为现有生产实现，实验列是独立项目，只改变渲染目标分配优先 HOST_CACHED，仍有相同 fence 和回读。其他 shader/字体/AA/混合算法不变；原版一列来自本轮原版 Vulkan。原版使用 graphics queue/GRContext，自研使用 compute；不是纯 GPU timestamp 时间。','',
'| 场景 | 上次 AI 报告 Simd ms | 本轮原版 ms | 当前 Simd ms | 隔离实验 ms | 实验相对当前加速 | 实验/原版耗时 | 质量 |',
'| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |']
pr={(r['Scene'],r['Width'],r['Height']):r for r in prototype}
for scene in ['clear','rectangles','blend','evenodd','stroke','gradient','image','rect-aa','curve-aa','chinese','curve-changing','chinese-changing']:
    r=gr[(scene,1024,768,'simd')];o=gr[(scene,1024,768,'original')];e=pr[(scene,1024,768)];prev=oldgpu[(scene,1024,768,'simd')]
    q=f"通过 max={r['MaxPixelError']}" if r['PixelPass'] else f"诊断 max={r['MaxPixelError']}>{r['AllowedPixelError']}"
    ratio=f"{e['MedianMs']/o['MedianMs']:.2f}×" if e['Comparable'] else '—'
    lines.append(f"| {scene} | {prev['MedianMs']:.3f} | {o['MedianMs']:.3f} | {r['MedianMs']:.3f} | {e['MedianMs']:.3f} | {e['Gain']:.2f}× | {ratio} | {q} |")
lines+=['','GPU 资源对照：','',
'| 场景 | CPU ms/帧 原/当前/实验 | WS MiB 原/当前/实验 | private MiB 原/当前/实验 | 托管 KiB/帧 原/当前/实验 |',
'| --- | --- | --- | --- | --- |']
for scene in ['clear','rectangles','blend','curve-aa','chinese','chinese-changing']:
    r=gr[(scene,1024,768,'simd')];o=gr[(scene,1024,768,'original')];e=pr[(scene,1024,768)]
    def series(field,scale=1,fmt='.3f'):return '/'.join(format(x[field]/scale,fmt) for x in [o,r,e])
    lines.append(f"| {scene} | {series('CpuMsPerOp')} | {series('WorkingSetBytes',1048576,'.1f')} | {series('PrivateBytes',1048576,'.1f')} | {series('ManagedBytesPerOp',1024)} |")
lines+=['',
'隔离实验的全部 24 个 scene-size、3 进程（72 个保存帧）与当前 GPU 保存帧逐字节一致，原版质量 gate 状态保持不变。实验回归检查矩形、路径/AA、渐变、描边、中英文、纹理版本、缓存淘汰、128 命令批次、线程和 Dispose，通过。生产 GPU 中另修复 descriptor offset 后 range 超出实际缓冲区的 Vulkan 合法性问题；此修复已纳入“当前”代码与哈希。','',
'## 瓶颈与还能优化的地方','',
'实际 GPU 剖析显示清屏 Record 约 0.001 ms、fence 完成（含 submit）约 1 ms，整个 Flush 约 10.6 ms。渲染目标选中 memory type 8，flags=6（HOST_VISIBLE|HOST_COHERENT、非 HOST_CACHED/DEVICE_LOCAL）；同设备还有 type 9，flags=14，支持 HOST_CACHED。隔离实验使用后者，证明 CPU 回读拷贝部分存在可兑现优化空间。这里的阶段差值不是纯 GPU timestamp，不将所有余量归因于一个硬件事件。','',
'该分配仍是主机内存渲染目标。下一步可用 DEVICE_LOCAL 渲染目标 + 缓存的 staging readback，需正确 transfer barriers 与生命周期；是否更快要再实测，未承诺速度倍数。离散 GPU 的主机/设备内存传输及 staging 设计见 [Khronos Memory Allocation](https://docs.vulkan.org/guide/latest/memory_allocation.html)。','',
'更严重的是 GPU 文字：四行共有约 7156 条扁平边。现 shader 对像素扫描整条路径边，边界像素再执行 16×16 次全路径测试；字体布局/命令录制很快，主要等待落在 GPU。HOST_CACHED 解决回读成本，无法根治边扫描复杂度。值得验证按扫描带/瓦片分配相关边、GPU 字形覆盖率图集与纹理批量文字；不能靠关闭 AA 或直接增大缓存冒充解决。','',
'GPU AA 矩形还对内部像素执行完整 16×16 采样；轴对齐矩形可以用与原网格一致的分离边缘覆盖计算，CPU 已有数学实现可作为参考。这也是值得独立验证的优化，尚未在本轮实现或计入实验收益。', '',
'CPU 动态路径约每帧分配 1 MiB mask/spans，动态中英文约 1.08 MiB；97 个移动位置超出 64 项缓存。可流式输出 spans、按行复用覆盖率缓冲、按 glyph/亚像素相位缓存，避免整串重建。需要覆盖旋转/裁剪/变更和资源上限的验证，不是只增加缓存容量。PNG 解码仍分配输出图像并有 Paeth/DEFLATE 开销；可继续优化，但它不是当前 GPU 文字瓶颈。','',
'## 继续还是停止','',
'如果目标是近期给真实 Avalonia 应用提供完整的 SkiaSharp 替代，我建议暂停生产替换投入。当前只有 403/3235 基线公共签名匹配，2832 缺失；复杂字体/色彩/native handle 和二进制加载仍有阻碍，且曲线、文字和某些图片/混合质量尚未达标。继续逐个微调性能不能补齐这些结构性问题。','',
'如果纯 C# 是核心价值、应用仅需受控的 CPU 离屏绘制子集，基础 CPU 性能已经足以保留这条路线；继续应集中在真实目标应用与动态文字/内存分配。GPU 简单场景已有实验证明可优化，不建议仅因当前清屏回读慢就放弃整个项目；但复杂文字现有算法需要专项重设计，建议先用有限试验验证瓦片/字形图集，再决定是否扩展 GPU。','',
'以上是根据当前实现与实测作出的工程判断，不代表完整应用性能。未测实际窗口 swapchain、完整 Avalonia 帧、Linux GPU、显存常驻用量、能耗或其他硬件；private/WS 不能拿来宣称更省显存。','',
'## 验证与复现','',
'当前 Release 构建、36 基础测试、14 功能测试、5267 项冻结/fixture 检查、Windows Vulkan 回归通过。GLSL 重新编译所得 SPIR-V SHA 与产品嵌入文件一致。原版 120 场景复测仍为 108 通过、12 曲线/文字失败，25 个行为探针一致；原版压缩 PNG fixture 解码测试通过。性能场景仍保留失败画质 gate，见具体表。','',
'[CPU 完整报告](PERFORMANCE.md) · [GPU 完整报告](PERFORMANCE-GPU.md) · [上轮 CPU](OPTIMIZATION.md) · [其他 AI 的 GPU 报告快照](PERFORMANCE-GPU-AI-SNAPSHOT.md)','',
'```powershell',
'powershell -NoProfile -File tools/verify.ps1 -Gpu',
'powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-review -Scenes clear,rectangles,blend,evenodd,stroke,rect-aa,gradient,image,curve-aa,chinese,png-encode,png-decode,curve-changing,chinese-changing -SampleMilliseconds 100',
'powershell -NoProfile -File tools/run-gpu-benchmarks.ps1 -Output artifacts/performance-gpu/my-review -SampleMilliseconds 100',
'powershell -NoProfile -File tools/run-gpu-host-cached-experiment.ps1 -Baseline artifacts/performance-gpu/my-review -Output artifacts/performance-gpu/my-cached-review',
'python tools/report-reassessment.py --cpu artifacts/performance/my-review --gpu artifacts/performance-gpu/my-review --experiment artifacts/performance-gpu/my-cached-review',
'```','',
'数据与日志分别在 artifacts/performance/reassessment-2026-10-12、artifacts/performance-gpu/reassessment-2026-10-12、artifacts/performance-gpu/host-cached-experiment-2026-10-12；GPU 阶段/内存类型诊断在 artifacts/reassessment/gpu-diagnostics.json。未覆盖原有运行证据；实验不在生产引用图或解决方案中。']
a.output.write_text('\n'.join(lines)+'\n',encoding='utf-8')
Path('artifacts/reassessment/assessment.json').write_text(json.dumps(dict(CpuHistory=history,ExperimentRows=prototype,CpuComparable=sum(r['Comparable'] for r in cpu['Rows'] if r['Engine']=='simd'),CpuPixelPassing=sum(r['PixelPass'] for r in cpu['Rows'] if r['Engine']=='simd'),CpuSceneSizeCount=sum(r['Engine']=='simd' for r in cpu['Rows']),GpuGatePassing=sum(r['PixelPass'] for r in gpu['Rows'] if r['Engine']=='simd'),GpuSceneSizeCount=sum(r['Engine']=='simd' for r in gpu['Rows'])),ensure_ascii=False,indent=2),encoding='utf-8')
print('Wrote',a.output,'; experiment pixels unchanged; CPU history matches')
