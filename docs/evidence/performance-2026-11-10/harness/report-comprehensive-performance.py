"""Generate the comprehensive report from completed measurements."""
import argparse
import json
from pathlib import Path
import sys

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--input',type=Path,required=True)
parser.add_argument('--report',type=Path,required=True)
parser.add_argument('--assets',type=Path,required=True)
args=parser.parse_args()
sys.path.insert(0,str(Path('artifacts/report-python').resolve()))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

def load(stage,name='summary.json'):
    return json.loads((args.input/stage/name).read_text(encoding='utf-8-sig'))

cpu=load('cpu-formal');api=load('api-formal');codec=load('codec-formal')
micro=load('gpu-micro-formal');pages=load('gpu-pages-formal')
aot=load('aot-pages-formal');retention=load('retention-verified')
all_tests=load('validation','results.json')
excluded=[row for row in all_tests if row['name']=='check-optimization-regressions' and '--validation' not in row['command']]
tests=[row for row in all_tests if row not in excluded]
replay=load('old-engine-replay')
confirmation=load('gpu-chinese-confirmation')
args.assets.mkdir(parents=True,exist_ok=True)
plt.rcParams.update({'font.family':'Microsoft YaHei','axes.unicode_minus':False,'font.size':10})
colors=['#858c99','#e8a351','#167f9b']

def chart(name,labels,series,*,title,xlabel='ms / 帧',log=False,note=''):
    fig,ax=plt.subplots(figsize=(11,max(3.2,len(labels)*.55+1.5)))
    y=np.arange(len(labels));height=.72/len(series)
    for index,(label,values) in enumerate(series.items()):
        ax.barh(y+(index-(len(series)-1)/2)*height,values,height=height,label=label,color=colors[index])
    ax.set_yticks(y,labels);ax.invert_yaxis();ax.set_xlabel(xlabel);ax.set_title(title,loc='left',pad=15)
    if log:ax.set_xscale('log')
    ax.grid(axis='x',alpha=.2);ax.set_axisbelow(True);ax.legend(loc='best')
    if note:fig.text(.1,.015,note,fontsize=8,color='#555555')
    fig.tight_layout(rect=(0,.055,1,1));fig.savefig(args.assets/(name+'.png'),dpi=160);plt.close(fig)

chart('cpu-api',[r['scene'] for r in api],{name:[r[key]['msPerFrame'] for r in api] for name,key in [('原版','Original'),('修改前','Before'),('修改后','Managed')]},
      title='CPU API 热点 · 三进程中位数',log=True,note='bilingual 原版画质门槛失败，速度仅用于诊断。对数横轴。')
chart('decode-allocation',[r['scene']+' / '+r['operation'] for r in codec if r['operation'].startswith('decode')],
      {name:[r[key]['allocatedBytesPerImage']/1048576 for r in codec if r['operation'].startswith('decode')] for name,key in [('修改前','before'),('修改后','simd')]},
      title='1080p 解码 · 单张托管分配',xlabel='MiB / 张',note='不含原生 malloc。完整进程内存和长期保留数据另列。')
chart('retained-memory',[f"{r['width']}×{r['height']}"+(' 混合尺寸' if r['mixedSize'] else '') for r in retention],
      {name:[r[key]['held']['privateMiB'] for r in retention] for name,key in [('原版','original'),('修改前','before'),('修改后','current')]},
      title='同时保留 16 张图像 · 完整进程 Private',xlabel='MiB',note='全收集后快照。包括运行库、解码池、输出与原生分配。')
chart('vulkan-micro',[f"{r['scene']} {r['width']}×{r['height']}" for r in micro],
      {name:[r[key]['milliseconds'] for r in micro] for name,key in [('原版','original'),('修改前','before'),('修改后','ordered')]},
      title='Vulkan · 绘制、fence 等待与主机读回',note='曲线和中文原版画质门槛失败。中文 1024×768 的加长复测单独保留。')
page_labels=[f"{r['scene']} ×{r['scale']} / {backend}" for r in pages for backend in ['vulkan','direct2d']]
chart('gpu-pages',page_labels,{
    '较快原版':[min(r['original-vulkan']['meanMs'],r['original-angle']['meanMs']) for r in pages for _ in range(2)],
    '修改前':[r['before-'+b]['meanMs'] for r in pages for b in ['vulkan','direct2d']],
    '修改后':[r[b]['meanMs'] for r in pages for b in ['vulkan','direct2d']]},title='真实 Avalonia 控件树 · 完成绘制并读回',note='JIT 离屏 GPU 测量。整页严格原版画质门槛失败，耗时用于诊断。')
aot_cases=list(dict.fromkeys(r['case'] for r in aot))
aot_rows={case:{r['variant']:r for r in aot if r['case']==case} for case in aot_cases}
chart('aot-pages',aot_cases,{name:[aot_rows[case][key]['drawMsPerFrame'] for case in aot_cases]
      for name,key in [('原版默认','original-default'),('修改前','before'),('修改后','simd')]},
      title='Native AOT 真实窗口 · CPU 绘制',note='绘制时间排除限帧等待。原版严格整页画质 0/10，时间用于诊断。')
chart('aot-memory',aot_cases,{name:[aot_rows[case][key]['sampledMedianPrivateMiB'] for case in aot_cases]
      for name,key in [('原版默认','original-default'),('修改前','before'),('修改后','simd')]},
      title='Native AOT 真实窗口 · 完整进程 Private',xlabel='MiB',note='每 25 ms 外部采样，三进程中位数。页面内存差距仍明显。')

def n(value,digits=3):return f'{value:.{digits}f}'
def change(before,after):return f'{(after/before-1)*100:+.1f}%'
def quality(passed):return '通过' if passed else '失败，诊断'
text=[]
def add(value=''):text.append(value)
def table(headers,rows):
    add('| '+' | '.join(headers)+' |');add('| '+' | '.join(['---']*len(headers))+' |')
    for row in rows:add('| '+' | '.join(map(str,row))+' |')
    add()
asset=lambda name:'assets/'+args.assets.name+'/'+name+'.png'
add('# SimdSkia 性能优化与全面复测（2026-11-10）\n')
add('本批为工作区源码优化，NuGet 正式版为 0.1.4。修改前引擎冻结于 `1eceed6`，原版为 SkiaSharp 3.119.4，控件使用 Avalonia 12.1.5。Windows x64、i7-8700K / 12 逻辑处理器、GTX 1050 Ti、SDK 10.0.403、.NET 10.0.14。构建、回归与性能采集串行执行。CPU/GPU、JIT/Native AOT 分别列出。\n')
add('## 实施内容\n')
add('- 常量混合增加 SSE2/AVX2 通道内解包、整数除法与饱和打包路径，保留标量和其他架构回退。\n- 浮点着色器按最多 128 个像素采样并批量混合，使用固定栈空间，覆盖矩形和完整覆盖率路径段。部分覆盖、其他混合模式及滤镜继续走相应管线。\n- 默认位图解码的透明度扫描、预乘和 RGBA/BGRA 转换向量化。指定 RGBA/Unpremul 的临时解码可转移真实像素所有权，持续使用的 SKCodec 保持独立输出。解码池余量超过 6.25% 时生成精确尺寸的完整像素副本，限制长期保留的浪费。\n- Vulkan 有序矩形提前合并矩形裁剪、预乘颜色，参数步长 48→32 字节，64 项 uniform 范围 3104→2080 字节。实际绘制仍在 GPU，预编译 SPIR-V 随库提供。Direct2D 通过真实控件页和硬件回归复测。\n')
add('基准修正了两个旧的像素格式假设，双方解码均显式指定 RGBA8888/Unpremul。编解码对照程序使用相同源码，分别引用完整冻结旧模块与当前模块。初轮不完整模块回放、缺少 SKP 交叉输入的回放以及容量保护前的中间测量均排除在本报告最终数字之外。\n')
add('## CPU 基础场景\n')
add('每种实现 3 个独立进程，每场景 500 ms 预热、5 批×目标 100 ms。原版、修改前、当前标量、当前 SIMD 轮换串行。TieredCompilation/ReadyToRun 关闭，Server GC 关闭。帧完成同步，矩形和混合各包含 64 次绘制。下表使用当前 SIMD，24 组修改前后固定帧完全相同。\n')
table(['场景','尺寸','原版 ms','前 ms','后 ms','变化','后/原版','原版像素门槛'],[
    [r['scene'],f"{r['width']}×{r['height']}",n(r['original']['milliseconds']),n(r['before']['milliseconds']),n(r['simd']['milliseconds']),change(r['before']['milliseconds'],r['simd']['milliseconds']),n(r['afterOverOriginal'],2)+'×',quality(r['nativeQualityPassed'])+f" max={r['maximumError']} / {r['allowedError']}"] for r in sorted(cpu,key=lambda r:(r['width'],r['scene']))])
add('PNG 编码同时记录输出大小及压缩策略，按各自输出与压缩策略解读耗时。画质失败行作为诊断数据。\n')
table(['PNG 编码尺寸','原版输出 B','前输出 B','后输出 B','压缩策略可比'],[
    [f"{r['width']}×{r['height']}",int(r['original']['encodedBytes']),int(r['before']['encodedBytes']),int(r['simd']['encodedBytes']),str(r['compressionComparable'])] for r in cpu if r['scene']=='png-encode'])
table(['场景 / 尺寸','CPU ms 原/前/后','整机 CPU% 原/后','托管 B/op 原/前/后','WS MiB 原/前/后','Private MiB 原/前/后'],[
    [f"{r['scene']} {r['width']}×{r['height']}",'/'.join(n(r[v]['cpuMilliseconds']) for v in ['original','before','simd']),
     '/'.join(n(r[v]['cpuPercentMachine'],2) for v in ['original','simd']),'/'.join(n(r[v]['allocatedBytes'],1) for v in ['original','before','simd']),
     '/'.join(n(r[v]['workingSetMiB'],1) for v in ['original','before','simd']),'/'.join(n(r[v]['privateMiB'],1) for v in ['original','before','simd'])] for r in sorted(cpu,key=lambda r:(r['width'],r['scene']))])
add('## CPU 公共 API 热点\n')
add('三进程轮换，逐场景 0.5 s 预热、0.8 s 测量，256×192，默认 JIT 设置。原版门槛 max≤3、RMSE≤0.75。进程内存为逐场景结束快照，包含此前场景。\n')
table(['API 场景','原版 ms','前 ms','后 ms','变化','后/原版','原版画质'],[
    [r['scene'],n(r['Original']['msPerFrame']),n(r['Before']['msPerFrame']),n(r['Managed']['msPerFrame']),change(r['Before']['msPerFrame'],r['Managed']['msPerFrame']),n(r['managedOverOriginal'],2)+'×',quality(r['quality']['passed'])] for r in api])
add(f"![CPU API 热点]({asset('cpu-api')})\n")
table(['API 场景','CPU ms 前→后','托管 B/帧 前→后','WS MiB 前→后','Private MiB 前→后'],[
    [r['scene'],n(r['Before']['cpuMsPerFrame'])+'→'+n(r['Managed']['cpuMsPerFrame']),n(r['Before']['allocatedBytesPerFrame'],1)+'→'+n(r['Managed']['allocatedBytesPerFrame'],1),n(r['Before']['workingSetMiB'],1)+'→'+n(r['Managed']['workingSetMiB'],1),n(r['Before']['privateMiB'],1)+'→'+n(r['Managed']['privateMiB'],1)] for r in api])
add('运行时效果、图集、网格与补丁的实测耗时高于原版，后续继续处理这些热点。\n')
add('## 编解码与图像内存\n')
add('1920×1080 的 UI 和纹理输入，3 进程轮换，1 s 预热、2 s 测量。解码交付拥有独立生命周期的 RGBA8888/Unpremul，编码交付 byte[]。PNG 无损像素核验，JPEG 固定 4:4:4、质量 90；JPEG 原有 1-LSB 编码门槛独立报告。\n')
table(['输入 / 操作','原版 ms','前 ms','后 ms','变化','后/原版','前→后托管 MiB/张','输出 B 原/后'],[
    [r['scene']+' / '+r['operation'],n(r['original']['millisecondsPerImage']),n(r['before']['millisecondsPerImage']),n(r['simd']['millisecondsPerImage']),change(r['before']['millisecondsPerImage'],r['simd']['millisecondsPerImage']),n(r['simdTimeRatio'],2)+'×',n(r['before']['allocatedBytesPerImage']/1048576,3)+'→'+n(r['simd']['allocatedBytesPerImage']/1048576,3),str(int(r['original']['outputBytes']))+'/'+str(int(r['simd']['outputBytes']))] for r in codec])
add(f"![解码分配]({asset('decode-allocation')})\n")
table(['输入 / 操作','CPU ms 原/前/后','整机 CPU% 原/后','WS MiB 原/前/后','Private MiB 原/前/后'],[
    [r['scene']+' / '+r['operation'],'/'.join(n(r[v]['cpuMillisecondsPerImage']) for v in ['original','before','simd']),
     '/'.join(n(r[v]['cpuPercentMachine'],2) for v in ['original','simd']),'/'.join(n(r[v]['workingSetMiB'],1) for v in ['original','before','simd']),
     '/'.join(n(r[v]['privateMiB'],1) for v in ['original','before','simd'])] for r in codec])
add('另用 3 进程、16 张同时存活的图片测试长期占用，增加大图解码后再持有小图的混合尺寸场景。所有图片输出哈希一致，全收集后采集存活状态，再逐张 Dispose 并重新采集。\n')
table(['尺寸 / 场景','原版 Private','前 Private','后 Private','前→后托管存活 MiB','释放后 Private 前→后'],[
    [f"{r['width']}×{r['height']}"+(' 混合尺寸' if r['mixedSize'] else ''),n(r['original']['held']['privateMiB'],1),n(r['before']['held']['privateMiB'],1),n(r['current']['held']['privateMiB'],1),n(r['before']['held']['managedLiveMiB'],1)+'→'+n(r['current']['held']['managedLiveMiB'],1),n(r['before']['released']['privateMiB'],1)+'→'+n(r['current']['released']['privateMiB'],1)] for r in retention])
add(f"![长期图像占用]({asset('retained-memory')})\n")
add('托管分配计数只统计托管内存。Private/WS 覆盖完整进程、原生分配、解码池和运行库。Dispose 后的保留池及分配器缓存也计入进程内存。\n')
add('## Vulkan 与 Direct2D\n')
add('Vulkan 微场景包含实际绘制、提交、fence 完成等待与主机读回，同一 GTX 1050 Ti。3 进程，每场景 500 ms 预热、5×目标 150 ms；TieredCompilation/ReadyToRun 关闭。\n')
table(['场景','尺寸','原版 ms','前 ms','后 ms','变化','CPU ms 前→后','原版画质'],[
    [r['scene'],f"{r['width']}×{r['height']}",n(r['original']['milliseconds']),n(r['before']['milliseconds']),n(r['ordered']['milliseconds']),change(r['before']['milliseconds'],r['ordered']['milliseconds']),n(r['before']['cpuMilliseconds'])+'→'+n(r['ordered']['cpuMilliseconds']),quality(r['nativeQualityPassed'])] for r in micro])
add(f"![Vulkan 微场景]({asset('vulkan-micro')})\n")
table(['场景 / 尺寸','托管 B/帧 前→后','WS MiB 前→后','Private MiB 前→后'],[
    [f"{r['scene']} {r['width']}×{r['height']}",n(r['before']['allocation'],1)+'→'+n(r['ordered']['allocation'],1),
     n(r['before']['workingSetMiB'],1)+'→'+n(r['ordered']['workingSetMiB'],1),n(r['before']['privateMiB'],1)+'→'+n(r['ordered']['privateMiB'],1)] for r in micro])
add('中文 1024×768 的三进程首轮出现回退，额外使用 6 进程、1.5 s 预热、7×目标 500 ms 交替复测。原始首轮保留，补测前 '+n(confirmation['before']['milliseconds'])+' ms，后 '+n(confirmation['current']['milliseconds'])+' ms，变化 '+change(confirmation['before']['milliseconds'],confirmation['current']['milliseconds'])+'。该场景原版画质门槛仍失败。\n')
add('真实 Avalonia 控件树另测 3 进程、3 s 预热、5 s 测量。原版 Vulkan/ANGLE 与本库 Vulkan/Direct2D 均完成 BGRA8888 GPU 读回。这组是 JIT 离屏测量，窗口呈现另见 AOT。\n')
table(['页面 / 后端','原版 Vulkan ms','原版 ANGLE ms','前 ms','后 ms','变化','CPU ms 前→后','WS MiB 前→后','Private MiB 前→后'],[
    [f"{r['scene']} ×{r['scale']} / {b}",n(r['original-vulkan']['meanMs']),n(r['original-angle']['meanMs']),n(r['before-'+b]['meanMs']),n(r[b]['meanMs']),change(r['before-'+b]['meanMs'],r[b]['meanMs']),n(r['before-'+b]['cpuMsPerFrame'])+'→'+n(r[b]['cpuMsPerFrame']),n(r['before-'+b]['sampledMedianWorkingSetMiB'],1)+'→'+n(r[b]['sampledMedianWorkingSetMiB'],1),n(r['before-'+b]['sampledMedianPrivateMiB'],1)+'→'+n(r[b]['sampledMedianPrivateMiB'],1)] for r in pages for b in ['vulkan','direct2d']])
add(f"![GPU 控件页]({asset('gpu-pages')})\n")
add('整页原版画质门槛为 max≤32、RMSE≤2，结果见 quality.json。通过相应门槛的行用于同画质比较，其余行标记诊断。\n')
add('## Native AOT 真实窗口\n')
add(f"![AOT 绘制]({asset('aot-pages')})\n\n![AOT 内存]({asset('aot-memory')})\n")
add('Windows 通用 x64 AOT，Vector128，CPU 光栅与 Win32 软件呈现。每项 3 独立进程、2 s 预热、5 s 测量。1000×720，控件 ×1.5 为 1500×1080。外部每 25 ms 采样，验证 PE 无 CLR 目录、进程未加载 CLR/JIT。下表同时列出原版默认设置与 full-opacity 对照，后者与本库的透明度合同对应。\n')
for case in dict.fromkeys(r['case'] for r in aot):
    group={r['variant']:r for r in aot if r['case']==case}
    add('### '+case+'\n')
    table(['实现','绘制 ms','P95 ms','CPU ms/帧','整机 CPU%','WS MiB','Private MiB','托管 KiB/帧'],[
        [name,n(group[key]['drawMsPerFrame']),n(group[key]['p95Ms']),n(group[key]['cpuMsPerFrame']),n(group[key]['cpuPercentMachine'],2),n(group[key]['sampledMedianWorkingSetMiB'],1),n(group[key]['sampledMedianPrivateMiB'],1),n(group[key]['allocatedBytesPerFrame']/1024,2)] for name,key in [('原版默认','original-default'),('原版 full','original-full'),('修改前','before'),('修改后','simd')]])
add('绘制耗时排除限帧等待。CPU/分配包括 UI 让出线程后的呈现与调度。吞吐测试中整机 CPU% 接近一个逻辑核上限是正常统计口径，同时使用 CPU ms/帧评估工作量。内存为完整进程，WS 衡量驻留内存，Private 衡量私有提交，两个维度分别列出。\n')
add('## 验证与剩余差距\n')
counts={status:sum(r['status']==status for r in tests) for status in dict.fromkeys(r['status'] for r in tests)}
add(f'最终广泛回归实际执行 {len(tests)} 个命令，状态统计为 `{json.dumps(counts,ensure_ascii=False)}`。聚合入口含重复子套件。原版差异及预期 API 缺口保持非零退出码。\n')
if excluded:add('通用扫描曾误调用一次需要专用参数的冻结回放工具，usage 日志单独保留。扫描器已分离该入口，后续使用完整参数执行冻结回放。本表统计有效测试命令，工具调用错误另列于原始记录。\n')
add(f"冻结旧引擎回放 {len(replay)} 个相关套件，{sum(len(r['files']) for r in replay)} 个输出文件逐字节或 JSON 值相同。API 仍匹配 3230/3235，5 项原生句柄/版本合同按约定保留，0 额外签名。Native AOT 的采样、runtime/mesh/PDF 综合场景及编解码另有实际执行记录。\n")
add('- JPEG 严格编码门槛、部分字体/曲线/图像采样、滤镜、序列化及整页原版差异仍存在，逐项记录在验证证据。\n- 运行时效果、图集、网格和纹理 PNG 解码仍是下一批热点。表中同时列出收益较小及回退的项目。\n- 本轮硬件实测为 Windows x64。Linux、macOS、ARM64、Android GPU、iOS 和 Metal 实机复测待补。关闭 AVX2/全部硬件指令的正确性测试通过。\n- 测试使用本机当前电源计划和 GPU 时钟策略，各功能门槛在表中列出。三进程样本含 OS/驱动波动，原始批次、单帧延迟和资源采样保留。\n')
add('## 复现\n')
add('使用 SDK 10.0.403，先构建 Release 并冻结修改前工作程序及全部 SimdSkia 模块。编码器与 AOT 示例通过 `FrozenEngineDirectory` 引用冻结模块，正常源码构建保持该属性为空。\n')
add('```powershell\ndotnet build SimdSkia.sln -c Release -p:UsePackedSimdSkia=false\npython tools/run-performance-validation.py --output artifacts/validation-new\npython tools/run-cpu-optimization.py --output artifacts/cpu-new --before <冻结的CPU工作程序.dll>\npython tools/run-final-api-benchmarks.py --output artifacts/api-new --before <冻结的API工作程序.dll>\npython tools/run-codec-skia-comparison.py --output artifacts/codec-new --before <统一工作负载的旧引擎Managed.exe>\npython tools/run-gpu-slow-cases.py --output artifacts/gpu-new --before <冻结的GPU工作程序.dll>\npython tools/run-gpu-parity.py --output artifacts/pages-new --before-managed <冻结的控件页Managed.exe>\npython tools/run-avalonia-aot.py --output artifacts/aot-new --publish artifacts/aot-publish-new --before-simd <冻结的AOT程序.exe>\n```\n')
add('完整命令、源码/二进制 SHA256、每进程结果、原版差异与压缩原始帧见 [本轮证据](evidence/performance-2026-11-10/)。文档生成命令 `python tools/report-comprehensive-performance.py --input artifacts/performance-2026-11-10/final --report docs/PERFORMANCE-2026-11-10.md --assets docs/assets/performance-2026-11-10`。\n')
args.report.write_text('\n'.join(text)+'\n',encoding='utf-8')
print('Generated',args.report)
