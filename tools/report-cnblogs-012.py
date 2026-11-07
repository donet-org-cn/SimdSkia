"""Generate the preview.8 -> 0.1.2 article tables, charts and portable publishing kit.

Only reads frozen measurements. Does not run benchmarks or change quality gates.
Matplotlib is a report-time dependency, never a SimdSkia runtime dependency.
"""
from pathlib import Path
import hashlib
import json
import math
import re
import shutil
import statistics
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / 'docs/evidence/blog-preview8-to-012-2026-11-07'
UI = ROOT / 'docs/evidence/ui-preview3-2026-11-07'
API = ROOT / 'docs/evidence/api-final-2026-11-07'
BLOG = ROOT / 'docs/blog'
ARTICLE = BLOG / '2026-11-07-simdskia-preview8-to-012-cnblogs.md'
ASSETS = BLOG / '2026-11-07-simdskia-012-assets'
ONLINE = BLOG / '2026-11-07-simdskia-012-online.md'
GUIDE = BLOG / '2026-11-07-simdskia-012-publishing.txt'
REPORT = ROOT / 'docs/PERFORMANCE-STABLE-0.1.2-AOT-2026-11-07.md'
ARCHIVE = BLOG / '2026-11-07-simdskia-012-cnblogs.zip'
load = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')


def table(headers, rows):
    return '\n'.join(['| ' + ' | '.join(headers) + ' |',
                      '|' + '|'.join('---' for _ in headers) + '|'] +
                     ['| ' + ' | '.join(map(str, row)) + ' |' for row in rows])


def main():
    data = load(EVIDENCE / 'overview.json')
    aot = data['aot']
    ui = load(UI / 'summary.json')
    quality = load(EVIDENCE / 'aot/quality.json')
    assert aot['processes'] == 45 and aot['qualityPassed'] == 0 and aot['qualityTotal'] == 10
    assert len(quality) == 10 and not any(r['passed'] for r in quality)
    assert all(r['maximumGate'] == 32 and r['rmseGate'] == 2 for r in quality)
    cases = aot['cases']
    labels = ['控件页 ×1 / 吞吐', '控件页 ×1.5 / 吞吐', '滚动页 ×1 / 吞吐',
              '控件页 ×1 / 60 FPS', '滚动页 ×1 / 60 FPS']
    assert [r['case'] for r in cases] == ['controls-1-throughput', 'controls-1.5-throughput',
        'scroll-1-throughput', 'controls-1-paced', 'scroll-1-paced']
    # Re-select the faster native renderer from the archived medians, not a hand-picked row.
    raw_summary = load(EVIDENCE / 'aot/summary.json')
    for row in cases:
        originals = [r for r in raw_summary if r['case'] == row['case'] and r['variant'].startswith('original-')]
        best = min(originals, key=lambda r: r['drawMsPerFrame'])
        assert row['baseline'] == best['variant']
        assert math.isclose(row['ratio'], row['simd']['drawMsPerFrame'] / best['drawMsPerFrame'])
    size = data['defaultSize']
    assert size['savedBytes'] == size['original']['runtime'] - size['simd']['runtime']
    assert size['simd']['dll'] == 0 and len(size['removed']) == 3
    micro = []
    for backend, directory in [('OpenGL', 'gl-timing-3'), ('Vulkan', 'vk-timing-2'),
                               ('D3D12 / D2D-on-12', 'd3d-timing-1')]:
        for row in load(API / directory / 'summary.json'):
            micro.append(dict(backend=backend, **row))
    assert len(micro) == 12
    sections = {}
    sections['api'] = table(['项目', 'preview.8', '0.1.2'], [
        ['公开类型', data['api']['fromPublicTypes'], data['api']['toPublicTypes']],
        ['匹配公共元数据项', '425 / 3235', '3230 / 3235'],
        ['缺失或签名不一致项', 2810, 5],
        ['额外或不同签名项', 0, 0]])
    sections['size'] = table(['文件类别', '原版', 'SimdSkia 0.1.2'], [
        [title, f"{size['original'][key]/1048576:.2f} MiB", f"{size['simd'][key]/1048576:.2f} MiB"]
        for title, key in [('EXE', 'exe'), ('运行 DLL', 'dll'), ('运行文件合计（不含 PDB）', 'runtime'),
                           ('PDB（单独统计）', 'symbols'), ('全部发布文件（含 PDB）', 'allPublished')]])
    sections['removed'] = table(['文件', '字节', 'MiB'], [
        [f"`{r['path']}`", f"{r['bytes']:,}", f"{r['bytes']/1048576:.2f}"] for r in size['removed']])
    historical = []
    previous_article = BLOG / '2026-10-27-simdskia-avalonia-aot-cnblogs.md'
    for line in previous_article.read_text(encoding='utf-8').splitlines():
        if re.match(r'^\| (控件页|滚动列表).* / (吞吐|60 FPS) \|', line):
            cells = [x.strip() for x in line.strip('|').split('|')]
            if len(cells) == 5:
                historical.append(dict(scene=cells[0], originalMs=float(cells[1]), simdMs=float(cells[3])))
    assert len(historical) == 5
    sections['historical'] = table(['场景', '旧文原版 ms', 'preview.8 ms', '本次原版 ms', '0.1.2 ms'], [
        [label, f"{old['originalMs']:.4f}", f"{old['simdMs']:.4f}",
         f"{row['original']['drawMsPerFrame']:.4f}", f"{row['simd']['drawMsPerFrame']:.4f}"]
        for label, old, row in zip(labels, historical, cases)])
    sections['aot-time'] = table(['场景', '原版 ms', 'SimdSkia ms', '耗时比', '选择的原版配置'], [
        [label, f"{r['original']['drawMsPerFrame']:.4f}", f"{r['simd']['drawMsPerFrame']:.4f}",
         f"{r['ratio']:.3f}", r['baseline']] for label, r in zip(labels, cases)])
    sections['aot-resources'] = table(['场景', '实现', 'CPU ms/帧', '整机 CPU %', 'WS MiB', 'private MiB', '托管 KiB/帧'], [
        [label, title, f"{r[key]['cpuMsPerFrame']:.3f}", f"{r[key]['cpuPercentMachine']:.2f}",
         f"{r[key]['workingSetMiB']:.2f}", f"{r[key]['privateMiB']:.2f}",
         f"{r[key]['allocatedBytesPerFrame']/1024:.2f}"]
        for label, r in zip(labels, cases) for title, key in [('原版', 'original'), ('SimdSkia', 'simd')]])
    jit_keys = ['original-cpu-default', 'original-cpu', 'cpu', 'original-vulkan', 'original-angle', 'vulkan', 'direct2d']
    jit_labels = ['原版 CPU 默认', '原版 CPU 灰度', 'SimdSkia CPU', '原版 Vulkan', '原版 ANGLE', 'SimdSkia Vulkan', 'SimdSkia Direct2D']
    sections['jit-time'] = table(['实现', '控件 ×1 ms', '控件 ×1.5 ms', '滚动 ×1 ms'], [
        [label] + [f"{r[key]['meanMs']:.4f}" for r in ui] for label, key in zip(jit_labels, jit_keys)])
    sections['jit-resources'] = table(['实现', 'CPU ms/帧', '整机 CPU %', '采样 WS MiB', '采样 private MiB', '托管 B/帧'], [
        [label] + [f"{ui[0][key][metric]:.{places}f}" for metric, places in [
            ('cpuMsPerFrame', 3), ('cpuPercentMachine', 2), ('sampledMedianWorkingSetMiB', 2),
            ('sampledMedianPrivateMiB', 2), ('allocatedBytesPerFrame', 1)]] for label, key in zip(jit_labels, jit_keys)])
    sections['gpu-micro'] = table(['后端 / 场景', '原版 ms', 'SimdSkia ms', '耗时比', '画质门槛'], [
        [r['backend'] + ' / ' + r['scene'], f"{r['Original']['msPerFrame']:.4f}", f"{r['Managed']['msPerFrame']:.4f}",
         f"{r['managedOverOriginal']:.3f}", '通过' if r['quality']['passed'] else '**失败**'] for r in micro])
    article = ARTICLE.read_text(encoding='utf-8')
    for name, content in sections.items():
        pattern = rf'<!-- BEGIN GENERATED:{re.escape(name)} -->.*?<!-- END GENERATED:{re.escape(name)} -->'
        article, count = re.subn(pattern, lambda m: f'<!-- BEGIN GENERATED:{name} -->\n{content}\n<!-- END GENERATED:{name} -->', article, flags=re.S)
        assert count == 1, name
    ARTICLE.write_text(article, encoding='utf-8')

    sys.path.insert(0, str(ROOT / 'artifacts/report-python'))
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from matplotlib import font_manager
    from matplotlib.patches import Patch
    import numpy as np
    font = Path('C:/Windows/Fonts/msyh.ttc')
    if font.exists():
        font_manager.fontManager.addfont(str(font))
        plt.rcParams['font.family'] = font_manager.FontProperties(fname=str(font)).get_name()
    plt.rcParams.update({'font.size': 11, 'axes.spines.top': False, 'axes.spines.right': False,
        'figure.facecolor': 'white', 'savefig.facecolor': 'white', 'svg.hashsalt': 'simdskia-012-cnblogs',
        'axes.unicode_minus': False})
    ASSETS.mkdir(parents=True, exist_ok=True)
    native, managed, danger = '#687c93', '#168aab', '#c84942'

    def save(fig, name, subtitle):
        fig.text(.5, .015, subtitle, ha='center', fontsize=10, color='#555555')
        fig.tight_layout(rect=(0, .055, 1, .84 if fig.legends else .94))
        for extension in ('png', 'svg'):
            fig.savefig(ASSETS / (name + '.' + extension), dpi=170, metadata={'Date': None} if extension == 'svg' else None)
        plt.close(fig)

    def pair(ax, names, left, right, xlabel, right_colors=None):
        y = np.arange(len(names))
        ax.barh(y-.18, left, .34, label='原版', color=native)
        ax.barh(y+.18, right, .34, label='SimdSkia', color=right_colors or managed)
        maxval = max(left + right)
        ax.set_yticks(y, names)
        ax.invert_yaxis()
        ax.set_xlim(0, maxval*1.30)
        ax.set_xlabel(xlabel)
        ax.grid(axis='x', alpha=.18)
        ax.set_axisbelow(True)
        for values, offset in [(left, -.18), (right, .18)]:
            for i, value in enumerate(values):
                ax.text(value + maxval*.012, i+offset, f'{value:.3f}' if maxval < 15 else f'{value:.2f}', va='center', fontsize=10)

    def pair_legend(fig, include_slow=False):
        handles = [Patch(color=native, label='原版'), Patch(color=managed, label='SimdSkia')]
        if include_slow:
            handles.append(Patch(color=danger, label='SimdSkia（明显落后）'))
        fig.legend(handles=handles, loc='upper center', bbox_to_anchor=(.5, .925), ncol=len(handles), frameon=False)

    fig, ax = plt.subplots(figsize=(10, 4))
    values = [425, 892, 3230]
    ax.barh(['preview.8', '0.1.1', '0.1.2'], values, color=[native, '#8498ad', managed])
    ax.invert_yaxis(); ax.set_xlim(0, 3800); ax.axvline(3235, color=danger, ls='--', lw=1)
    ax.set_xlabel('匹配的公共元数据项（包含类型、字段、属性、重载）')
    for i, v in enumerate(values): ax.text(v+45, i, str(v), va='center', fontsize=13)
    ax.set_title('从小范围外观，到 3230 / 3235 项公共元数据')
    save(fig, 'api-progress', '固定基线 SkiaSharp 3.119.4；元数据匹配不等于行为兼容或二进制兼容')

    fig, ax = plt.subplots(figsize=(10, 4.4))
    exes = [size[x]['exe']/1048576 for x in ('original', 'simd')]
    dlls = [size[x]['dll']/1048576 for x in ('original', 'simd')]
    ax.barh(['原版 Avalonia', 'SimdSkia 0.1.2'], exes, color=managed, label='EXE')
    ax.barh(['原版 Avalonia', 'SimdSkia 0.1.2'], dlls, left=exes, color=native, label='运行 DLL')
    ax.invert_yaxis(); ax.set_xlim(0, 46); ax.set_xlabel('运行文件 MiB（不含 PDB）')
    for i, (exe, dll) in enumerate(zip(exes, dlls)):
        ax.text(exe/2, i, f'EXE {exe:.2f}', ha='center', va='center', color='white')
        if dll: ax.text(exe+dll/2, i, f'DLL {dll:.2f}', ha='center', va='center', color='white')
        ax.text(exe+dll+.5, i, f'{exe+dll:.2f} MiB', va='center')
    ax.set_title('默认 Avalonia 窗口 AOT：运行文件体积减少 34.27%')
    ax.legend(loc='lower right')
    save(fig, 'aot-size', '公开 NuGet 0.1.2 / Avalonia 12.1.5 / win-x64；双方保留 Fluent + Inter；不含可选 GPU/PDF')

    fig, axes = plt.subplots(1, 2, figsize=(13, 5))
    for ax, start, end, title in [(axes[0], 0, 3, '不限制帧率的吞吐'), (axes[1], 3, 5, '固定 60 FPS（绘制耗时，不含等待）')]:
        part = cases[start:end]
        pair(ax, [s.split(' / ')[0] for s in labels[start:end]], [r['original']['drawMsPerFrame'] for r in part],
             [r['simd']['drawMsPerFrame'] for r in part], 'ms / 次绘制（越低越好）',
             [danger if r['ratio'] > 1.1 else managed for r in part])
        ax.set_title(title)
    fig.suptitle('正式公开包 Native AOT：吞吐接近持平，限帧滚动仍明显落后', fontsize=15)
    pair_legend(fig, include_slow=True)
    save(fig, 'aot-time', '45 进程 / 3 forks / 通用 x64、AVX2=false；每项选择较快原版；严格整页画质 0/10')

    fig, axes = plt.subplots(1, 2, figsize=(13, 6))
    for ax, key, title in [(axes[0], 'workingSetMiB', '工作集 WS'), (axes[1], 'privateMiB', '私有提交 private')]:
        pair(ax, labels, [r['original'][key] for r in cases], [r['simd'][key] for r in cases], '测量终点 MiB')
        ax.set_title(title)
    fig.suptitle('Native AOT 运行内存：部署更小，进程内存仍偏高', fontsize=15)
    pair_legend(fig)
    save(fig, 'aot-memory', '公开 NuGet 0.1.2 / 三进程中位数；WS 与 private 不可相加，不含独立显存统计')

    fig, axes = plt.subplots(1, 2, figsize=(13, 4.8))
    for ax, key, factor, title, xlabel in [(axes[0], 'cpuPercentMachine', 1, '固定帧率 CPU 占用', '整机 CPU %（12 逻辑线程）'),
        (axes[1], 'allocatedBytesPerFrame', 1/1024, '固定帧率托管分配', 'KiB / 次绘制（包含进程窗口工作）')]:
        part = cases[3:]
        pair(ax, ['控件页 ×1', '滚动页 ×1'], [r['original'][key]*factor for r in part],
            [r['simd'][key]*factor for r in part], xlabel, [managed, danger])
        ax.set_title(title)
    fig.suptitle('约 60 FPS 不等于同成本：滚动页暴露 CPU / GC 热点', fontsize=15)
    pair_legend(fig, include_slow=True)
    save(fig, 'aot-paced-resources', '公开 NuGet 0.1.2 / portable Native AOT；托管分配不包含原生堆或显存')

    colors = [native, '#99a8b7', managed, native, '#99a8b7', managed, '#4b9b6c']
    fig, axes = plt.subplots(1, 3, figsize=(16, 5.4))
    for ax, row, title in zip(axes, ui, ['控件页 ×1', '控件页 ×1.5', '滚动页 ×1']):
        values = [row[k]['meanMs'] for k in jit_keys]
        ax.barh(jit_labels, values, color=colors); ax.invert_yaxis(); ax.set_xlim(0, max(values)*1.24)
        for i, value in enumerate(values): ax.text(value+max(values)*.012, i, f'{value:.3f}', va='center', fontsize=10)
        ax.set_title(title); ax.set_xlabel('完成帧 ms（含 GPU 完成读回）')
    fig.suptitle('另一个口径：既有同绘图代码的 JIT / GPU 控件页测量', fontsize=15)
    save(fig, 'jit-ui-time', '不是本次 AOT 跑分；63 进程 / preview.3 最终绘图代码；严格整页画质 0/18')

    fig, ax = plt.subplots(figsize=(11, 7.2))
    names = [r['backend']+' / '+r['scene']+(' *' if not r['quality']['passed'] else '') for r in micro]
    values = [r['managedOverOriginal'] for r in micro]
    ax.barh(names, values, color=[managed if v < 1 else danger for v in values]); ax.invert_yaxis()
    ax.axvline(1, color='#333333', ls='--', lw=1); ax.set_xlim(0, max(values)*1.16)
    for i, value in enumerate(values): ax.text(value+.02, i, f'{value:.3f}×', va='center')
    ax.set_xlabel('SimdSkia / 原版完成耗时（小于 1 更快）')
    ax.set_title('GPU 微场景：OpenGL 更快，Vulkan / D3D12 仍落后')
    save(fig, 'gpu-micro-ratio', '既有 JIT 同后端快照 / 640×480 / 含完成读回；* 表示该路径画质门槛失败')
    shutil.copyfile(ROOT / 'docs/assets/api-final-2026-11-07/ui-quality.png', ASSETS / 'ui-quality.png')
    write_json(ASSETS / 'data.json', dict(newPublicPackageAot=data, previousArticlePublishedAot=historical,
                                        previousSameCodeJit=ui, previousSameCodeGpuMicro=micro))

    report = '\n\n'.join([
        '# SimdSkia 0.1.2 正式公开包：通用 x64 Native AOT、CPU/内存与默认窗口体积复测',
        '测量日期2026-11-07。使用NuGet.org已发布0.1.2；不修改产品绘图代码。'
        '对应[preview.8→0.1.2新文章](blog/2026-11-07-simdskia-preview8-to-012-cnblogs.md)，此前测量保留。',
        '**三个吞吐场景综合耗时比0.968，最差1.049；纳入两个固定60FPS场景后综合1.437。'
        '限帧滚动耗时7.528倍、托管分配约3.21MiB/次，严格画质0/10。整体持平且最差落后不超过10%的目标未达到。**',
        '## 环境与方法',
        'Windows11 x64 / i7-8700K 6核12线程 / 31.93GiB / GTX1050Ti。SDK10.0.403、runtime10.0.14、'
        'Avalonia12.1.5、原版SkiaSharp3.119.4/HarfBuzzSharp8.3.1.3。真正Native AOT，通用x64，AVX2=false、Vector128；'
        '固定同一字体、页面/尺寸/DPI。每项2秒预热5秒测量、3独立进程中位数；5场景×3变体×3fork=45进程，串行运行。',
        '保留原版default和full-group两套透明度合同；每项选择耗时较低的原版作性能分母，不等于两者行为已相同。'
        '吞吐为连续离屏RenderTargetBitmap.Render，paced通过Task.Delay让出UI线程，CPU/分配含窗口呈现/定时器工作；'
        '绘制计时不含显式等待。它不是显示器呈现FPS或光栅内核孤立计时。',
        '## 绘制耗时', sections['aot-time'],
        '![AOT耗时](blog/2026-11-07-simdskia-012-assets/aot-time.png)',
        '## CPU、内存和分配', sections['aot-resources'],
        'CPU%为整机12逻辑线程口径。WS/private是测量终点值（完整25ms外部采样和峰值在证据中）；'
        '二者不可相加。托管分配不计原生堆/显存。限帧滚动本库p95=13.9555ms，原版1.0076ms，'
        '3fork本库49–51次调度截止延迟、原版0–3；不能等同实际丢帧。根因尚未通过调用栈分析确认。',
        '![AOT内存](blog/2026-11-07-simdskia-012-assets/aot-memory.png)',
        '![限帧资源](blog/2026-11-07-simdskia-012-assets/aot-paced-resources.png)',
        '## 默认窗口AOT体积（另一程序，只测大小）',
        '两个默认模板窗口保留同一Fluent/Inter、App/XAML/manifest/AOT属性，无源码项目引用；'
        '原版默认桌面配置、本库CPU，实际启动窗口与PE/CLR/JIT模块检查通过。没有删DLL或UPX，PDB单列；不含可选GPU/PDF。',
        sections['size'], sections['removed'],
        '运行文件减少13,133,352字节（12.52MiB、34.27%）；EXE本身更大。旧文19.51MiB是preview.7历史结果，'
        '本次24.02MiB不能宣传为继续减小。SDK/Avalonia/依赖/功能不同，差值未做模块归因。',
        '## 画质与复现',
        '10幅比较全部保留FAIL，最大通道误差92–109、RMSE约2.31–3.09；门槛最大32/RMSE2。'
        '相同group合同也失败。脚本退出码2表示质量未过，不是把运行成功算作画质通过。'
        '本报告没有重新运行GPU/JIT；既有同绘图代码63进程报告单独列于文章，不能与此处AOT数混合。',
        '在有.NET10 AOT工具链的Windows桌面，从仓库根目录执行（输出须为新目录）：\n\n'
        '```powershell\npython tools/compare-default-avalonia-aot.py --output artifacts/default-012-new --avalonia-version 12.1.5 --simdskia-version 0.1.2\n'
        'python tools/run-avalonia-aot.py --output artifacts/aot-012-new --publish artifacts/aot-012-new-publish --package-version 0.1.2 --isa portable --forks 3 --warmup 2 --seconds 5\n'
        'python tools/verify-final-evidence.py --directory docs/evidence/blog-preview8-to-012-2026-11-07\n```',
        '冻结证据：[目录](evidence/blog-preview8-to-012-2026-11-07/)、[机器可读汇总](evidence/blog-preview8-to-012-2026-11-07/overview.json)、'
        '[逐文件哈希](evidence/blog-preview8-to-012-2026-11-07/sha256.json)。保留45进程metrics/外部采样、'
        '原始像素内容寻址对象、构建和执行记录、默认窗口逐文件SHA与截图、实际preview.8公共API对照。',
        '## 新旧口径',
        '旧文章preview.8：Avalonia12.1.4/SDK10.0.402/runtime10.0.13；本次12.1.5/10.0.403/10.0.14。'
        '新旧吞吐和paced均列出，但不能把差值全部归因于引擎版本。API增量在同一3.119.4基线/工具下从实际包提取，'
        '425→3230、37→201公开类型，剩余5个真实native合同缺口；这不等于完整行为或二进制兼容。']) + '\n'
    REPORT.write_text(report, encoding='utf-8')

    prefix = 'https://raw.githubusercontent.com/donet-org-cn/SimdSkia/main/docs/blog/'
    online = re.sub(r'(!\[[^\]]*\]\()(' + re.escape(ASSETS.name) + r'/[^)]+)(\))',
                    lambda m: m[1]+prefix+m[2]+m[3], article)
    ONLINE.write_text(online, encoding='utf-8')
    GUIDE.write_text(
        'SimdSkia 0.1.2 博客园新文章发布说明\n\n'
        '这是接续preview.8的新文章，不覆盖已发布的旧文章。\n'
        '推荐标题取Markdown第一行；博客园标题框填标题，正文可删除重复的第一行。\n\n'
        '方法A：在线图片版\n'
        '1. 在博客园新建随笔，选择Markdown编辑器。\n'
        f'2. 粘贴{ONLINE.name}内容。图为GitHub原始图片URL；GitHub推送完成后可用。\n'
        '3. 预览八张图。若读者网络无法访问raw.githubusercontent.com，改用方法B。\n\n'
        '方法B：上传图片（更适合博客园读者）\n'
        f'1. 使用{ARTICLE.name}，保持原文章不动。\n'
        f'2. 将{ASSETS.name}目录的八张PNG上传博客园，替换正文同名图片的相对URL。\n'
        '3. SVG用于高清备用，不必同时上传；data.json和manifest.json用于复查，正文无需粘贴。\n'
        '4. 预览表格、代码和图。保持AOT/JIT/GPU口径、0/10与0/18失败和滚动热点说明。\n\n'
        '文章、图、数据、使用示例、优劣势与新旧口径已准备好。没有自动发布到博客园。\n', encoding='utf-8')
    write_json(ASSETS / 'manifest.json', dict(
        generator='tools/report-cnblogs-012.py', generatorSha256=sha(Path(__file__)),
        newAotEvidenceManifestSha256=sha(EVIDENCE/'sha256.json'),
        previousArticleSha256=sha(previous_article),
        previousJitEvidenceManifestSha256=sha(UI/'sha256.json'),
        previousGpuEvidenceManifestSha256=sha(API/'sha256.json'),
        scope=dict(publicVersion='0.1.2', aotNewProcesses=45, earlierSameCodeJitProcesses=63,
                   aotQualityPassed=0, aotQualityTotal=10, jitQualityPassed=0, jitQualityTotal=18,
                   metalHardwareVerified=False),
        files={p.name:sha(p) for p in sorted(ASSETS.iterdir()) if p.name != 'manifest.json' and p.is_file()}))
    # Relative image references must work in the downloadable kit without a repository checkout.
    images = re.findall(r'!\[[^\]]*\]\(([^)]+)\)', article)
    assert len(images) == 8 and all((BLOG / p).is_file() for p in images)
    assert not re.search(r'!\[[^\]]*\]\((?!https://)', online)
    files = [ARTICLE, ONLINE, GUIDE] + sorted(p for p in ASSETS.rglob('*') if p.is_file())
    with zipfile.ZipFile(ARCHIVE, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for path in files:
            entry = zipfile.ZipInfo(path.relative_to(BLOG).as_posix(), date_time=(2026, 11, 7, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            z.writestr(entry, path.read_bytes())
    with zipfile.ZipFile(ARCHIVE) as z:
        assert z.testzip() is None and len(z.namelist()) == len(files)
        for path in files:
            assert z.read(path.relative_to(BLOG).as_posix()) == path.read_bytes()
    print(f'PASS generated 8 article images, {len(sections)} tables, AOT report, online copy and {len(files)}-file ZIP.')
    print(f'Preserved quality FAIL 0/10 and 0/18; all-five AOT ratio={aot["allFiveGeometricMean"]:.3f}.')
    print(ARCHIVE.relative_to(ROOT), ARCHIVE.stat().st_size, 'bytes')


if __name__ == '__main__':
    main()
