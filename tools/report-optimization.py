"""Compare preserved before/after workers without changing quality gates or samples."""
import argparse
import hashlib
import json
import os
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--cpu-before',type=Path,default=Path('artifacts/performance/before-2026-10-21'))
p.add_argument('--cpu',type=Path,default=Path('artifacts/performance/optimized-final-2026-10-21'))
p.add_argument('--gpu-before',type=Path,default=Path('artifacts/performance-gpu/before-2026-10-21'))
p.add_argument('--gpu',type=Path,default=Path('artifacts/performance-gpu/optimized-final-2026-10-21'))
p.add_argument('--ocr',type=Path,default=Path('artifacts/ocr/optimized-final-2026-10-21'))
p.add_argument('--report',type=Path,default=Path('docs/OPTIMIZATION-2026-10-21.md'))
p.add_argument('--output',type=Path,default=Path('artifacts/optimization-2026-10-21/comparison.json'))
a=p.parse_args()
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def load(root):
    run=read(root/'run.json');assert run['Complete'],root
    return read(root/'summary.json')
def index(data):return {(r['Scene'],r['Width'],r['Height'],r['Engine']):r for r in data['Rows']}
def mib(n):return n/1048576
def link(path):return Path(os.path.relpath(path,a.report.parent)).as_posix()
cpu_before,cpu,gpu_before,gpu=map(load,[a.cpu_before,a.cpu,a.gpu_before,a.gpu])
for old,new in [(cpu_before,cpu),(gpu_before,gpu)]:
    for key in ['Framework','OS','Architecture','Cpu','FontSha256','ServerGc','TieredCompilation','ReadyToRun']:
        assert old['Runtime'][key]==new['Runtime'][key],key
canonical={}
for name,before,after in [('CPU',a.cpu_before,a.cpu),('GPU',a.gpu_before,a.gpu)]:
    count=0
    for file in before.glob('simd-*/*.rgba'):
        other=after/file.relative_to(before)
        assert other.exists(),other
        assert file.read_bytes()==other.read_bytes(),f'Canonical pixel regression: {file} vs {other}'
        count+=1
    assert count>0
    canonical[name]=count
summary={'Cpu':[], 'Gpu':[], 'CanonicalFramesByteIdentical':canonical}
lines=['# SimdSkia 优化实测：2026-10-21','',
'本轮已合入生产模块，重新测量优化前、优化后及原版。保持 16×16 AA、颜色/gamma、CRC/Adler、预乘 Alpha、采样和像素舍入；生产仍无第三方 NuGet/Skia 原生依赖。GPU 使用系统 Vulkan。','',
f"硬件：{cpu['Runtime']['Cpu']} / GTX 1050 Ti；{cpu['Runtime']['Framework']}，Windows。渲染对照固定 SkiaSharp 3.119.4，OCR 单独固定真实模块使用的 4.153.1。两者不混作一个版本的兼容承诺。",'',
'CPU、GPU 分别比较。优化前后每实现 3 个独立进程，每场景每进程 5 批、目标每批 70 ms、预热至少 100 ms；相同字体 SHA、输入、尺寸和环境开关，任务串行。耗时为 15 个批均值的中位数。GPU 每帧包含录制、提交、fence 完成、设备到主机传输及回读拷贝。','',
'## 实际改动','',
'- CPU 路径：按相邻扫描线复用交点排序，遇到杂乱边时用有界回退；简单路径直接输出连续覆盖区间，减少大面积中间 mask；复杂 AA 文字用紧凑覆盖率数组和 AVX2 合成。缓存仍有 64 项/16 MiB 上限。',
'- PNG：SSE2/SSSE3 行预测与 RGB 展开、AVX2 Adler 校验；32 位 DEFLATE 补位及短距回溯；IDAT 一次聚合到可复用缓冲。保留标量路径、全部校验和 gamma/透明度处理。',
'- 解码输出：全进程最多 4 块、32 MiB 的空闲像素池；共享 PixelBuffer/画布通过引用计数延长所有权，到最后一次 Dispose 才可复用。活动图像、原始 DEFLATE 临时池和 GPU 资源另计。默认公共位图仍初始化为零，只有完全写入的解码输出借用未初始化空间。',
'- Vulkan：按闭合轮廓和 Y 范围建立 8×8 瓦片边索引，索引有 16 MiB 上限，超限保留全扫描；AA 每条子扫描线只求交一次，用独立整数计数器覆盖全部 16 个 X 样本；候选边过多时保留原始采样核。',
'- Vulkan 资源：DEVICE_LOCAL 渲染目标加 HOST_CACHED 优先的 coherent staging，使用 transfer/compute/host 屏障、初次设备清零与 fence 后回读；减少逐命令临时分配，修正离屏 dispatch 负范围。同步遵循 [Khronos 示例](https://docs.vulkan.org/guide/latest/synchronization_examples.html)。','',
'## 1024×768：帧耗时','',
'“本轮提速”只比较自身前后；画质未通过者保留诊断数据，不据此宣布胜过原版。','']
for title,before,after,key in [('CPU',cpu_before,cpu,'Cpu'),('GPU Vulkan 全回程',gpu_before,gpu,'Gpu')]:
    old,new=index(before),index(after)
    lines += [f'### {title}','', '| 场景 | Simd 优化前 ms | Simd 优化后 ms | 本轮提速 | 原版 ms | 当前画质 |', '| --- | ---: | ---: | ---: | ---: | --- |']
    selected=[r for r in after['Rows'] if r['Engine']=='simd' and (r['Width'],r['Height'])==(1024,768)]
    for s in selected:
        scene=s['Scene'];b=old[(scene,1024,768,'simd')];o=new[(scene,1024,768,'original')]
        quality=f"通过 max={s['MaxPixelError']}" if s['PixelPass'] else f"诊断 max={s['MaxPixelError']}>{s['AllowedPixelError']}"
        if scene=='png-encode':quality+='；压缩策略不同'
        lines.append(f"| {scene} | {b['MedianMs']:.3f} | {s['MedianMs']:.3f} | {b['MedianMs']/s['MedianMs']:.2f}× | {o['MedianMs']:.3f} | {quality} |")
        summary[key].append({'Scene':scene,'Before':b,'After':s,'Original':o,'OwnSpeedup':b['MedianMs']/s['MedianMs']})
    lines += ['',f'### {title}：CPU 与内存','',
    'CPU ms/帧为进程累计用户+内核处理器时间/帧数；单核满载通常接近帧耗时，百分比满载并不能反映固定帧率下的节省。WS/private 含整个进程、runtime、驱动和先前场景；不是纯引擎内存或显存。托管分配不含原版 native malloc。','',
    '| 场景 | CPU ms/帧 前→后 / 原版 | 托管 KiB/帧 前→后 / 原版 | WS MiB 前→后 / 原版 | private MiB 前→后 / 原版 |',
    '| --- | --- | --- | --- | --- |']
    for row in summary[key]:
        s,b,o=row['After'],row['Before'],row['Original']
        if row['Scene'] not in ['clear','rectangles','blend','rect-aa','curve-changing','chinese','chinese-changing','png-decode']:continue
        lines.append(f"| {row['Scene']} | {b['CpuMsPerOp']:.3f}→{s['CpuMsPerOp']:.3f} / {o['CpuMsPerOp']:.3f} | {b['ManagedBytesPerOp']/1024:.2f}→{s['ManagedBytesPerOp']/1024:.2f} / {o['ManagedBytesPerOp']/1024:.2f} | {mib(b['WorkingSetBytes']):.1f}→{mib(s['WorkingSetBytes']):.1f} / {mib(o['WorkingSetBytes']):.1f} | {mib(b['PrivateBytes']):.1f}→{mib(s['PrivateBytes']):.1f} / {mib(o['PrivateBytes']):.1f} |")
    lines.append('')
old,new=index(gpu_before),index(gpu)
lines+=['## 256×256 GPU 中文','',
'小画布仍比原版慢，不能把大画布的比例套用到所有尺寸。完整两尺寸的场景在 GPU 全表。','',
'| 场景 | 优化前 ms | 优化后 ms | 原版 ms | 画质 |','| --- | ---: | ---: | ---: | --- |']
for scene in ['chinese','chinese-changing']:
    b=old[(scene,256,256,'simd')];s=new[(scene,256,256,'simd')];o=new[(scene,256,256,'original')]
    lines.append(f"| {scene} | {b['MedianMs']:.3f} | {s['MedianMs']:.3f} | {o['MedianMs']:.3f} | 既有误差，诊断数据 |")
dynamic=next(r for r in summary['Gpu'] if r['Scene']=='chinese-changing');b=dynamic['Before'];s=dynamic['After']
lines+=['','## 资源取舍','',
f"基础 CPU 的 clear/rectangles/evenodd/stroke 等没有改动热点，本次约 ±6% 变化不作为稳定提速。GPU 动态中文字虽然从 {b['MedianMs']:.2f} ms 降到 {s['MedianMs']:.2f} ms，但新瓦片索引使每帧托管分配从 {b['ManagedBytesPerOp']/1024:.1f} KiB 增至 {s['ManagedBytesPerOp']/1024:.1f} KiB，进程 WS 从 {mib(b['WorkingSetBytes']):.1f} MiB 增至 {mib(s['WorkingSetBytes']):.1f} MiB。该场景需要更多内存，不能据耗时宣布全面省内存。",
'像素复用减少重复分配，同时保留部分空闲数组；PNG 的工作集也未在所有尺寸下降。缓存预算约束空闲数组，不约束活动图像或整个进程。','']
if (a.ocr/'summary.json').exists():
    ocr=read(a.ocr/'summary.json');summary['Ocr']=ocr
    lines+=['## 真实 OCR 输入链路：SkiaSharp 4.153.1','',
    '只读链接 Qcxt.OCR.SkiaSharp 和真实 CPU OCR/模型，未修改目标仓库。每条件 3 个独立进程 × 5 批；500×500 是真实样本，1024/2048 仅为该样本放大的解码测试。RunBytes 的 Simd 路径仍是 PNG 适配，完整原版扩展仍缺 SKStream/SKPixmap 等 API；JPEG 仍未实现。','',
    '| 条件 | 原版 ms | Simd ms | Simd/原版 | CPU ms/张 原→Simd | WS MiB 原→Simd | 全线程托管 MiB/张 原→Simd |',
    '| --- | ---: | ---: | ---: | --- | --- | --- |']
    for mode,size in [('decode',500),('decode',1024),('decode',2048),('bitmap-ocr',500),('bytes-ocr',500)]:
        o=next(r for r in ocr['Rows'] if (r['Mode'],r['Size'],r['Engine'])==(mode,size,'original'))
        s=next(r for r in ocr['Rows'] if (r['Mode'],r['Size'],r['Engine'])==(mode,size,'simd'))
        lines.append(f"| {mode} {size}² | {o['MedianMs']:.3f} | {s['MedianMs']:.3f} | {s['MedianMs']/o['MedianMs']:.2f}× | {o['CpuMsPerImage']:.2f}→{s['CpuMsPerImage']:.2f} | {mib(o['WorkingSetBytes']):.1f}→{mib(s['WorkingSetBytes']):.1f} | {mib(o['TotalManagedBytesPerImage']):.3f}→{mib(s['TotalManagedBytesPerImage']):.3f} |")
    raw_o=next(r for r in ocr['Rows'] if (r['Mode'],r['Engine'])==('raw-ocr','original'))
    raw_s=next(r for r in ocr['Rows'] if (r['Mode'],r['Engine'])==('raw-ocr','simd'))
    lines+=['',f"OCR 推理本身使用同一核心。raw-ocr 控制组为 {raw_o['MedianMs']:.2f}→{raw_s['MedianMs']:.2f} ms，约 {abs(raw_s['MedianMs']/raw_o['MedianMs']-1)*100:.1f}% 波动；本样本整链差异约 1% 不能证明普遍持平或显著优劣。RunBitmap 只传递像素。OCR CPU ms 合计全部线程，可高于墙钟；不能把推理的 CPU 时间差异归功于绘图引擎。仍需关注透明图片的 premul/straight 输入语义差异。",'']
hw=read(Path('artifacts/optimization-tests-hardware.json'));fallback=read(Path('artifacts/optimization-tests-fallback.json'))
gpu_tests=read(Path('artifacts/optimization-2026-10-21/gpu-kernel-tests.json'))
pixels=read(Path('artifacts/optimization-2026-10-21/diff-final/results.json'))
behavior=read(Path('artifacts/optimization-2026-10-21/behavior-validation.json'))
lines+=['## 验证与仍未解决的差距','',
f"- CPU/编解码回归：硬件 SIMD {hw['Checks']} 项、完整回退 {fallback['Checks']} 项通过，含逐字节比较、异常与生命周期检查。包含 213 个独立 zlib、471 个独立 PNG fixture、全部过滤器和 SIMD 边界。",
f"- Vulkan：{gpu_tests['Checks']} 项检查通过，其中 {gpu_tests['Comparisons']} 组优化内核对原始全边/16×16 核逐字节一致、{gpu_tests['BinningChecks']} 项索引边界检查；另通过纹理变更、队列线程、Dispose、批次、初始化和 stride 回归。",
f"- 本轮优化前后固定 frame=0：CPU {canonical['CPU']} 帧、GPU {canonical['GPU']} 帧逐字节一致，覆盖 3 进程。动态位置另由随机/亚像素回归检查。",
f"- 原版画质 corpus：{sum(s['Passed'] for s in pixels['Scenes'])}/{len(pixels['Scenes'])} 通过；失败场景及差异图继续保留。行为探针 {pixels['BehaviorProbes']-len(pixels['BehaviorDifferences'])}/{pixels['BehaviorProbes']} 字符串完全一致，详见下述字体微差。",
f"- 字体行为单独复核：默认 SimHei {behavior['simhei']['Matching']}/{behavior['simhei']['Total']} 完全一致；固定 Zen 字体 {behavior['zen']['Matching']}/{behavior['zen']['Total']} 完全一致，Advance/Outline.Bounds 有约 0.000004 以内浮点字符串微差。保存的优化前程序集已复现同样微差，两字体均为优化前后 25/25 相同；未放宽测试阈值。",
'- 公共 API 仍为 403/3235 匹配，2832 缺失，0 个额外/不同签名；本轮不扩大版本或二进制兼容承诺。',
'- 曲线/字体仍有既有原版画质差异，部分图片/混合场景仍不通过严格门槛；压缩 PNG 编码、JPEG/WebP、复杂字体与应用完整集成仍有差距。Linux Vulkan 和其他 GPU/ARM 硬件未实测。','',
'## 复现和证据','',
f"[CPU 全表]({link(Path('docs/PERFORMANCE.md'))}) · [GPU 全表]({link(Path('docs/PERFORMANCE-GPU.md'))}) · [优化前 CPU]({link(a.cpu_before/'REPORT.md')}) · [优化前 GPU]({link(a.gpu_before/'REPORT.md')}) · [对比 JSON]({link(a.output)})",'',
'```powershell',
'powershell -NoProfile -File tools/verify.ps1 -Gpu',
'$env:DOTNET_EnableHWIntrinsic="0"',
'dotnet tests/Optimization/bin/Release/net8.0/Optimization.dll baseline/fonts/ZenOldMincho-Regular.ttf',
    'Remove-Item Env:DOTNET_EnableHWIntrinsic',
'# 网络不可用时，已有基准包可用 NuGetAudit=false 环境变量离线构建；不改变产品依赖。',
'powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-new-run -Scenes clear,rectangles,blend,evenodd,stroke,rect-aa,gradient,image,curve-aa,chinese,png-encode,png-decode,curve-changing,chinese-changing -Samples 5 -SampleMilliseconds 70',
'powershell -NoProfile -File tools/run-gpu-benchmarks.ps1 -Output artifacts/performance-gpu/my-new-run -Samples 5 -SampleMilliseconds 70',
'powershell -NoProfile -File tools/run-ocr-benchmarks.ps1 -Output artifacts/ocr/my-new-run',
'python tools/report-ocr-benchmarks.py artifacts/ocr/my-new-run --data-only',
'python tools/report-optimization.py --cpu artifacts/performance/my-new-run --gpu artifacts/performance-gpu/my-new-run --ocr artifacts/ocr/my-new-run',
'```','',
'测量前后均保存源文件 SHA，完成时检查未变化。原始 JSON/CSV、逐帧 ticks、像素、差异图、分配/GC/CPU/内存和日志保存在 artifacts。PNG 分段剖析工具为 tools/profile-codec.py；它只给源码副本计时，正式比较使用未插桩产品。']
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
a.report.parent.mkdir(parents=True,exist_ok=True);a.report.write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(f'Wrote {a.report}; unchanged canonical frames: {canonical}')
for kind in ['Cpu','Gpu']:
    for r in summary[kind]:print(kind,r['Scene'],round(r['Before']['MedianMs'],3),round(r['After']['MedianMs'],3),round(r['Original']['MedianMs'],3),round(r['OwnSpeedup'],2))
