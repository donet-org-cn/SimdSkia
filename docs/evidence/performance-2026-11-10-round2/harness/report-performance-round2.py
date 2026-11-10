"""Generate the second optimization report from completed serial measurements."""
import argparse
import json
from pathlib import Path
import sys

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--input', type=Path, required=True)
parser.add_argument('--report', type=Path, required=True)
parser.add_argument('--validation', type=Path, required=True)
parser.add_argument('--assets', type=Path, required=True)
args = parser.parse_args()
sys.path.insert(0, str(Path('artifacts/report-python').resolve()))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np


def load(stage, filename='summary.json'):
    return json.loads((args.input / stage / filename).read_text(encoding='utf-8-sig'))


def number(value):
    return f'{value:.3f}'


def change(before, after):
    return f'{(after / before - 1) * 100:+.1f}%'


def table(headers, rows):
    return '\n'.join(['| ' + ' | '.join(headers) + ' |',
                      '| ' + ' | '.join('---' for _ in headers) + ' |',
                      *['| ' + ' | '.join(map(str, row)) + ' |' for row in rows]])


def chart(filename, labels, series, title, unit, *, logarithmic=False, note=''):
    fig, axis = plt.subplots(figsize=(11, max(3.8, len(labels) * .62 + 1.9)))
    positions = np.arange(len(labels))
    height = .76 / len(series)
    palette = ['#4073aa', '#a1a6ac', '#159b79', '#d78829', '#9466ac']
    for index, (label, values) in enumerate(series.items()):
        axis.barh(positions + (index - (len(series) - 1) / 2) * height,
                  values, height=height, label=label, color=palette[index])
    axis.set_yticks(positions, labels)
    axis.invert_yaxis()
    axis.set_xlabel(unit)
    axis.set_title(title, loc='left', pad=15)
    if logarithmic:
        axis.set_xscale('log')
    axis.grid(axis='x', alpha=.2)
    axis.set_axisbelow(True)
    axis.legend(loc='best')
    fig.text(.1, .015, note, fontsize=8, color='#555555')
    fig.tight_layout(rect=(0, .055, 1, 1))
    fig.savefig(args.assets / filename, dpi=160)
    plt.close(fig)


cpu = load('cpu-formal')
api = load('api-formal')
codec = load('codec-formal')
micro = load('gpu-micro-formal')
pages = load('gpu-pages-formal')
aot = load('aot-pages-formal')
retention = load('retention-verified')
tests = load('validation-final', 'results.json')
audit = load('validation-final', 'api-comparison.json')
replay = load('old-engine-replay-final')
numeric = load('aot-tests/numeric/results', 'result.json')
args.assets.mkdir(parents=True, exist_ok=True)
plt.rcParams.update({'font.family': 'Microsoft YaHei', 'axes.unicode_minus': False, 'font.size': 10})

cases = list(dict.fromkeys(row['case'] for row in aot))
aot_index = {(row['case'], row['variant']): row for row in aot}
chart('cpu-api.png', [row['scene'] for row in api],
      {label: [row[key]['msPerFrame'] for row in api]
       for label, key in [('原版', 'Original'), ('上一轮源码', 'Before'), ('本轮源码', 'Managed')]},
      'CPU API 热点 · 三进程中位数', '毫秒 / 帧，对数坐标', logarithmic=True,
      note='bilingual 未通过原版画质门槛，作为诊断。各场景保持各自相同输入。')
chart('aot-time.png', cases,
      {label: [aot_index[case, key]['drawMsPerFrame'] for case in cases]
       for label, key in [('原版默认', 'original-default'), ('上一轮源码', 'before'), ('本轮源码', 'simd'), ('原版完整组', 'original-full')]},
      '真实 Win32 Native AOT 窗口 · 整页绘制', '毫秒 / 帧',
      note='portable x64 / Vector128。throughput 为持续绘制，paced 为目标 60 FPS。整页严格画质未通过。')
chart('aot-memory.png', cases,
      {label: [aot_index[case, key]['sampledMedianPrivateMiB'] for case in cases]
       for label, key in [('原版默认', 'original-default'), ('上一轮源码', 'before'), ('本轮源码', 'simd')]},
      '真实 Native AOT 窗口 · 完整进程私有内存', 'MiB，25 ms 外部采样中位数',
      note='包括代码、GC 堆、字体、像素、缓存与运行库。图像、字体及窗口尺寸相同。')
chart('gpu-pages.png', [f"{row['scene']} ×{row['scale']}" for row in pages],
      {label: [row[key]['meanMs'] for row in pages]
       for label, key in [('原版 Vulkan', 'original-vulkan'), ('前 Vulkan', 'before-vulkan'), ('后 Vulkan', 'vulkan'), ('前 Direct2D', 'before-direct2d'), ('后 Direct2D', 'direct2d')]},
      'GPU 整页 · 绘制完成与主机读回', '毫秒 / 帧',
      note='同一物理 GPU。JIT 离屏测试，整页严格画质未通过。ANGLE 数字另列于表格。')

asset_prefix = args.assets.relative_to(args.report.parent).as_posix()


def picture(name, label):
    return f'![{label}]({asset_prefix}/{name})'


lines = [
    '# SimdSkia 第二轮优化与原版对比（2026-11-10）',
    '本轮继续优化上一轮未发布的工作区源码。基线为 SkiaSharp 3.119.4、Avalonia 12.1.5，构建版本基于 0.1.4。Windows x64、i7-8700K、12 逻辑处理器、GTX 1050 Ti、SDK 10.0.403、.NET 10.0.14。修改前完整 DLL 已冻结，AOT 修改前使用上一轮本机 dotnet publish 产物。各实现串行、轮换顺序运行。',
    '本轮完成318个性能工作进程，另执行85个最终验收命令及Native AOT专项测试。完整逐进程结果、压缩原始像素和源码/二进制哈希已归档。[本轮证据](evidence/performance-2026-11-10-round2/)',
    '## 实施内容',
    '- 运行时效果共享有界数值执行计划，在绑定时快照 uniform，移除逐像素 AST 遍历和大 Value 结构复制。可向量化的数值指令按像素批量执行，其余现有语言特性继续使用类型化解释器。更新 uniform 复用计划，保留绑定独立性及并发安全。',
    '- 网格预计算颜色和纹理坐标差分，批量传播坐标及材质采样。最近邻图像着色器在一批样本内复用行访问，并在精确整数区间采用等价边界计算，保留大坐标回退。',
    '- Avalonia 的普通不透明作用域使用不含托管引用的轻量记录，真实图层另存状态。保留错误配对、嵌套、透明度、裁剪及资源回收检查。',
    '- 已注册字体重新以可寻址流打开时，先流式校验再复用字体，减少启动时完整 MemoryStream 大对象缓冲。外部新字体仍保留独立字节快照，非寻址流另行验证。',
    '- 字形/路径覆盖率的黑色文本和全不透明 AVX2 通道减少重复计算。标量、SSE2、AVX2 路径分别回归。',
    '坐标向量收集和仿射分支候选初测出现性能回退，已撤回。PNG 单次解压候选需要访问底包内部行接口，编译验证失败后撤回，严格验证和颜色处理保留在最终管线中。',
    '## CPU 公共 API',
    '256×192，相同 CJK 字体及场景，每实现三个独立进程，每场景 0.5 s 预热、0.8 s 测量。CPU ms/帧便于比较完成同等工作需要的处理器时间。持续单线程满负荷时，机器 CPU 百分比接近 8.3%。',
    picture('cpu-api.png', '公共 API 绘制耗时'),
]
lines.append(table(['场景', '原版 ms', '前 ms', '后 ms', '变化', '后/原版', 'CPU ms 前→后', '原版画质'],
    [[row['scene'], number(row['Original']['msPerFrame']), number(row['Before']['msPerFrame']), number(row['Managed']['msPerFrame']),
      change(row['Before']['msPerFrame'], row['Managed']['msPerFrame']), f"{row['managedOverOriginal']:.2f}×",
      f"{number(row['Before']['cpuMsPerFrame'])}→{number(row['Managed']['cpuMsPerFrame'])}", '通过' if row['quality']['passed'] else '失败，诊断'] for row in api]))
lines.append(table(['场景', 'WS MiB 原版 / 前 / 后', 'Private MiB 原版 / 前 / 后', '分配 B/帧 前→后'],
    [[row['scene'], ' / '.join(f"{row[v]['workingSetMiB']:.1f}" for v in ['Original','Before','Managed']),
      ' / '.join(f"{row[v]['privateMiB']:.1f}" for v in ['Original','Before','Managed']),
      f"{row['Before']['allocatedBytesPerFrame']:.1f}→{row['Managed']['allocatedBytesPerFrame']:.1f}"] for row in api]))
lines += ['## 真实 Native AOT 窗口',
          '每例四种实现各三个真实窗口进程，5 s 测量、2 s 预热。普通 x64 / Vector128，不提高 CPU 最低指令集。原版默认和强制完整透明度组分别列出。所有修改前后固定帧逐字节比较，原版使用 max=32、RMSE=2 的既有整页门槛。',
          picture('aot-time.png', '真实 Native AOT 控件页耗时')]
lines.append(table(['页面 / 模式', '原版默认 ms', '原版完整组 ms', '前 ms', '后 ms', '变化', '后/原版默认'],
    [[case, number(aot_index[case, 'original-default']['drawMsPerFrame']), number(aot_index[case, 'original-full']['drawMsPerFrame']),
      number(aot_index[case, 'before']['drawMsPerFrame']), number(aot_index[case, 'simd']['drawMsPerFrame']),
      change(aot_index[case, 'before']['drawMsPerFrame'], aot_index[case, 'simd']['drawMsPerFrame']),
      f"{aot_index[case, 'simd']['drawMsPerFrame']/aot_index[case, 'original-default']['drawMsPerFrame']:.2f}×"] for case in cases]))
lines += [picture('aot-memory.png', '真实 Native AOT 进程私有内存'),
          table(['页面 / 模式', 'CPU% 原版默认 / 前 / 后', 'WS MiB 原版 / 前 / 后', 'Private MiB 原版 / 前 / 后', '分配 B/帧 前→后'],
          [[case, ' / '.join(f"{aot_index[case, v]['cpuPercentMachine']:.2f}" for v in ['original-default', 'before', 'simd']),
            ' / '.join(f"{aot_index[case, v]['sampledMedianWorkingSetMiB']:.1f}" for v in ['original-default', 'before', 'simd']),
            ' / '.join(f"{aot_index[case, v]['sampledMedianPrivateMiB']:.1f}" for v in ['original-default', 'before', 'simd']),
            '→'.join(f"{aot_index[case, v]['allocatedBytesPerFrame']:.0f}" for v in ['before', 'simd'])] for case in cases]),
          '内存为完整进程外部采样。托管分配量不包含原生 malloc。持续绘制和目标 60 FPS 两种负荷分别比较。']
focus = load('aot-scroll-confirmation')
lines += ['### 60 FPS 滚动页加长复测',
          '主矩阵三轮中，本轮滚动页 paced 耗时相对修改前上升。追加每实现六个独立进程，3 s 预热、7.5 s 测量，保留两组完整记录。以下为加长复测，初轮回退原因尚未定位。',
          table(['实现', '绘制 ms/帧', '完整 CPU ms/帧', '机器 CPU%', 'Private MiB'],
          [[row['variant'], number(row['drawMsPerFrame']), number(row['cpuMsPerFrame']), f"{row['cpuPercentMachine']:.2f}",
            f"{row['sampledMedianPrivateMiB']:.1f}"] for row in focus])]
system_fonts = load('system-font-controls')
lines += ['### 系统字体补测',
          '上面的显著内存下降来自相同字体同时经 FontFiles 注册、再经 EmbeddedFontCollection 打开的路径。另用系统 Microsoft YaHei UI 运行 JIT CPU 离屏控件页，1000×720，2.5 s 预热、6000 帧测量、每实现三个进程。五个实际字体表的哈希在原版及修改前后逐项核对，固定帧在修改前后精确比较。此处的进程内存为测量结束快照。',
          table(['实现', '绘制 ms/帧', 'CPU ms/帧', '机器 CPU%', 'WS MiB', 'Private MiB', 'GC 后托管 MiB'],
          [[row['variant'], number(row['millisecondsPerFrame']), number(row['cpuMillisecondsPerFrame']), f"{row['cpuPercentOneCore']/12:.2f}",
            f"{row['workingSetMiB']:.1f}", f"{row['privateMiB']:.1f}", f"{row['managedRetainedAfterGcMiB']:.1f}"] for row in system_fonts]),
          '固定字体复用与系统字体的结果分别列出，具体应用的内存收益取决于其字体加载路径。']
lines += ['## Vulkan 与 Direct2D',
          '三进程，每例 3 s 预热、5 s 测量。GPU 完成时间包括绘制、同步等待和 BGRA8888 主机读回。此处为 JIT 离屏场景，与上面的 AOT 软件呈现分别列出。',
          picture('gpu-pages.png', 'GPU 完成帧耗时')]
lines.append(table(['页面', '后端', '原版 Vulkan ms', '原版 ANGLE ms', '前 ms', '后 ms', '变化', 'CPU ms 前→后', 'Private MiB 前→后'],
    [[f"{row['scene']} ×{row['scale']}", backend, number(row['original-vulkan']['meanMs']), number(row['original-angle']['meanMs']),
      number(row['before-'+backend]['meanMs']), number(row[backend]['meanMs']), change(row['before-'+backend]['meanMs'], row[backend]['meanMs']),
      f"{number(row['before-'+backend]['cpuMsPerFrame'])}→{number(row[backend]['cpuMsPerFrame'])}",
      f"{row['before-'+backend]['sampledMedianPrivateMiB']:.1f}→{row[backend]['sampledMedianPrivateMiB']:.1f}"] for row in pages for backend in ['vulkan', 'direct2d']]))
lines.append(table(['页面', '后端', '机器 CPU% 前→后', 'WS MiB 前→后', 'Private MiB 原版 VK / ANGLE / 后', '分配 B/帧 后'],
    [[f"{row['scene']} ×{row['scale']}", backend,
      f"{row['before-'+backend]['cpuPercentMachine']:.2f}→{row[backend]['cpuPercentMachine']:.2f}",
      f"{row['before-'+backend]['sampledMedianWorkingSetMiB']:.1f}→{row[backend]['sampledMedianWorkingSetMiB']:.1f}",
      ' / '.join(f"{row[v]['sampledMedianPrivateMiB']:.1f}" for v in ['original-vulkan','original-angle',backend]),
      f"{row[backend]['allocatedBytesPerFrame']:.0f}"] for row in pages for backend in ['vulkan','direct2d']]))
lines += ['GPU 整页严格画质结果在测试报告中列出，失败场景的速度作为诊断。实际原生 GPU 窗口、资源失效、软件回退和线程约束由硬件集成测试覆盖。',
          '### Vulkan 基础工作负荷',
          table(['场景 / 尺寸', '原版 ms', '前 ms', '后 ms', '变化', '原版画质'],
          [[f"{row['scene']} / {row['width']}×{row['height']}", number(row['original']['milliseconds']), number(row['before']['milliseconds']),
            number(row['ordered']['milliseconds']), change(row['before']['milliseconds'], row['ordered']['milliseconds']),
            '通过' if row['nativeQualityPassed'] else f"失败，max {row['maximumError']} / {row['allowedError']}"] for row in micro]),
          '## CPU 基础绘制与编解码',
          'CPU 基础绘制每实现三个独立进程、每场景 0.5 s 预热、五批各目标 0.1 s，TieredCompilation 与 ReadyToRun 关闭。当前标量与 SIMD 另行检查。编解码为同一 1920×1080 输入，统一显式 RGBA8888/Unpremul 输出。',
          table(['场景 / 尺寸', '原版 ms', '前 ms', '后 ms', '变化', '后/原版', '原版画质'],
          [[f"{row['scene']} / {row['width']}×{row['height']}", number(row['original']['milliseconds']), number(row['before']['milliseconds']),
            number(row['simd']['milliseconds']), change(row['before']['milliseconds'], row['simd']['milliseconds']), f"{row['afterOverOriginal']:.2f}×",
            '通过' if row['nativeQualityPassed'] else f"失败，max {row['maximumError']} / {row['allowedError']}"] for row in cpu]),
          table(['内容 / 操作', '原版 ms', '前 ms', '后 ms', '变化', '后/原版', '分配 KiB 后'],
          [[row['scene']+' / '+row['operation'], number(row['original']['millisecondsPerImage']), number(row['before']['millisecondsPerImage']),
            number(row['simd']['millisecondsPerImage']), change(row['before']['millisecondsPerImage'], row['simd']['millisecondsPerImage']),
            f"{row['simdTimeRatio']:.2f}×", f"{row['simd']['allocatedBytesPerImage']/1024:.2f}"] for row in codec]),
          'PNG/JPEG 编码策略与输出大小详见原始记录。JPEG 严格编码误差和纹理 PNG 解码属于剩余差距。',
          '## 16 张同时存活图片',
          table(['尺寸', '原版 Private MiB', '前 MiB', '后 MiB', '后托管存活 MiB'],
          [[f"{row['width']}×{row['height']}"+('，先暖大图' if row['mixedSize'] else ''), f"{row['original']['held']['privateMiB']:.1f}",
            f"{row['before']['held']['privateMiB']:.1f}", f"{row['current']['held']['privateMiB']:.1f}", f"{row['current']['held']['managedLiveMiB']:.1f}"] for row in retention]),
          '## 构建与复测',
          '```powershell\ndotnet build SimdSkia.sln -c Release -m:1\ndotnet run --project tests/RuntimeNumeric -c Release -- artifacts/runtime-numeric\npython tools/run-performance-validation.py --output artifacts/validation-new\npython tools/run-final-api-benchmarks.py --before <冻结目录>/Api99/Managed/Managed.dll --output artifacts/api-new\npython tools/run-gpu-parity.py --before-managed <冻结目录>/AvaloniaGpuParity/Managed/Managed.exe --output artifacts/gpu-new\n```',
          '完整命令、输入和二进制哈希、逐进程 CPU/内存、原始像素及差异结果见本轮证据目录。该报告覆盖 Windows x64 实机，其他平台需要各自硬件复测。']
sizes=load('aot-pages-formal','sizes.json')
lines += ['## 同一示例的 AOT 发布体积',
          table(['实现', 'EXE MiB', '原生 DLL MiB', '字体 MiB', '运行所需 MiB', 'ZIP MiB'],
          [[variant, *[f"{sizes[variant][key]/1048576:.2f}" for key in ['executableBytes','nativeLibraryBytes','fontBytes','runtimeBytes','zipBytes']]]
           for variant in ['original','before','simd']]),
          '运行所需文件按 runtimeBytes 统计，调试符号单独记录于原始 sizes.json。使用相同字体及示例。',
          '## 尚未达到的目标',
          'CPU网格、图集、运行时效果耗时仍约为原版4.26×、6.13×、2.63×。系统字体补测的私有内存约49.8MiB，原版默认约41.4MiB、完整组约31.0MiB。固定字体AOT示例的显著内存下降有明确适用路径，整页画质和部分JPEG、图像采样差异仍待解决。整体持平且最多落后10%的目标尚未达成。']
args.report.write_text('\n\n'.join(lines)+'\n', encoding='utf-8')

failed = [row for row in tests if row['status'] != 'passed']
quality_aot = load('aot-pages-formal', 'quality.json')
quality_gpu = load('gpu-pages-formal', 'quality.json')
validation_lines = [
    '# 第二轮优化测试报告（2026-11-10）',
    f"最终源码重建后执行 {len(tests)} 个独立验收命令，{len(tests)-len(failed)} 个返回成功，{len(failed)} 个原版门槛或 API 审计返回非成功。返回码和原始日志完整保留。初轮调查与优化候选不计入最终通过数。",
    f"公共 API 元数据匹配 {audit['MatchingEntries']}/{audit['BaselineEntries']}。保留用户指定的五项原生句柄/版本合同缺口。元数据匹配与行为、画质及二进制兼容分别评估。",
    '## 新增回归',
    f"- Native AOT 数值、采样、并发、绑定和解释器回退检查 {numeric['checks']:,} 项，每次变化 uniform 的绑定分配 {numeric['allocatedBytesPerBind']:.0f} B。JIT、关闭 AVX2、关闭全部硬件指令的相同测试另行执行。",
    '- 字体流验证覆盖已注册字体复用、大对象分配上限、非零流位置、非寻址流、不同字体的快照及调用方流生命周期。',
    '- 图层验证覆盖轻量不透明作用域嵌套真实半透明组、错误 Pop 的异常及后续恢复。完整 Avalonia 控件、几何、文字、SIMD、生命周期和效果用例由完整验收记录计数。',
    '- Native AOT portable / x86-64-v3 各实际执行 460,050 项采样检查。运行时、网格、路径、滤镜、PDF 的 38 项 AOT 检查各执行 SIMD 与标量。',
    '## 画质与剩余差距',
    f"AOT 整页通过 {sum(row['passed'] for row in quality_aot)}/{len(quality_aot)}，GPU 整页通过 {sum(row['passed'] for row in quality_gpu)}/{len(quality_gpu)}，保持既有 max=32、RMSE=2 门槛。所有性能场景中的修改前后固定帧由采集脚本逐字节检查。",
    f"冻结旧引擎回放覆盖原有失败套件及四个通过的着色器套件，合计 {len(replay)} 个套件、{sum(len(row['files']) for row in replay)} 个输出，全部相同的前提由回放脚本强制断言。该项只检查新增回归，原版差异另列。",
    '额外纳入已通过原版门槛的网格/图集、运行时效果、浮点着色器及梯度兼容测试。梯度测试中的50张预览PNG由Python对照工具生成，最初回放将其视为C#输出导致文件缺失，修正后对其对应完整BGRA源逐字节验证。初次记录单独保留。',
    table(['验收命令', '返回码', '状态'], [[row['name'], row['exitCode'], row['status']] for row in failed]),
    '## 原版整页差异',
    table(['场景', '后端/参考', 'max', 'RMSE', '通过'],
          [[row.get('case', f"{row.get('scene')} ×{row.get('scale')}"), str(row.get('variant','SimdSkia'))+' / '+str(row.get('reference')),
            row.get('maximumChannelError', row.get('maximum')), number(row['rmse']), row['passed']] for row in [*quality_gpu, *quality_aot]]),
    '## 测试边界',
    'Windows x64 CPU、Vulkan、Direct2D 和原生窗口通过本机执行。macOS Metal GPU、Linux GPU、Android 及 iOS 未在本轮硬件矩阵执行。API 元数据覆盖不代表全部方法、平台和参数组合的行为兼容。',
    '额外24个AOT进程复测60 FPS滚动页，追加12个JIT进程检查系统Microsoft YaHei UI字体；五个实际字体表哈希及各轮修改前后固定帧均一致。系统字体整页原版严格画质为0/2，六轮AOT滚动为0/2。',
    '333份实现源码与正式测量和最终验收的哈希一致。初次AOT快照的PDF锁文件多一个空的net10.0/win-x64条目，已按原哈希精确还原并记录；后续恢复移除了空条目，包版本和实现代码一致。AOT窗口示例的引用图不包含PDF模块。',
    '各场景仍存在相对原版的耗时、内存和画质差距，整体持平目标尚未达成。详细性能、完整失败记录和可复跑命令见配套报告与证据目录。[完整归档](evidence/performance-2026-11-10-round2/)']
args.validation.write_text('\n\n'.join(validation_lines)+'\n', encoding='utf-8')
print('Generated performance and validation reports.')
