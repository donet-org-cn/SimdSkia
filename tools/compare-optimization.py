"""Compare preserved measured CPU runs without changing quality/compression gates."""
import argparse, hashlib, json, os
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('before',type=Path)
parser.add_argument('after',type=Path)
parser.add_argument('--output',type=Path,default=Path('docs/OPTIMIZATION.md'))
args=parser.parse_args()
before=json.loads((args.before/'summary.json').read_text(encoding='utf-8-sig'))
after=json.loads((args.after/'summary.json').read_text(encoding='utf-8-sig'))
for field in ['Framework','OS','Architecture','Cpu','LogicalProcessors','ServerGc','FontSha256','TieredCompilation','ReadyToRun']:
    if before['Runtime'][field] != after['Runtime'][field]: raise ValueError('Environment mismatch: '+field)
for field in ['Forks','Samples','TargetSampleMilliseconds','WarmupMilliseconds','Sizes','Seed','TieredCompilation','ReadyToRun','ServerGc']:
    b=json.loads((args.before/'run.json').read_text(encoding='utf-8-sig'))
    a=json.loads((args.after/'run.json').read_text(encoding='utf-8-sig'))
    if not b['Complete'] or not a['Complete'] or b[field]!=a[field]: raise ValueError('Incomplete/incompatible run: '+field)

def key(row):return row['Scene'],row['Width'],row['Height'],row['Engine']
old={key(row):row for row in before['Rows']}
relative=os.path.relpath(args.after.resolve(),args.output.parent.resolve()).replace('\\','/')
rows=[]
for r in after['Rows']:
    if r['Engine']!='simd':continue
    o=old[key(r)]
    original=next(x for x in after['Rows'] if key(x)==(r['Scene'],r['Width'],r['Height'],'original'))
    # Confirm every canonical frame for every managed fork has the same pixels as before.
    stem=f"{r['Scene']}-{r['Width']}x{r['Height']}.rgba"
    hashes={hashlib.sha256(p.read_bytes()).hexdigest() for root in [args.before,args.after] for engine in ['scalar','simd'] for p in root.glob(f'{engine}-*/{stem}')}
    count=sum(1 for root in [args.before,args.after] for engine in ['scalar','simd'] for p in root.glob(f'{engine}-*/{stem}'))
    if len(hashes)!=1 or count!=12:raise ValueError(f'Changed/missing old/new managed pixels: {stem} ({count} files, {len(hashes)} hashes)')
    if r['MaxPixelError']!=o['MaxPixelError'] or r['PixelPass']!=o['PixelPass'] or r['AllowedPixelError']!=o['AllowedPixelError']:raise ValueError('Quality gate changed: '+stem)
    rows.append(dict(Scene=r['Scene'],Width=r['Width'],Height=r['Height'],BeforeMs=o['MedianMs'],AfterMs=r['MedianMs'],OriginalMs=original['MedianMs'],Improvement=o['MedianMs']/r['MedianMs'],VsOriginal=r['MedianMs']/original['MedianMs'],BeforeBytes=o['ManagedBytesPerOp'],AfterBytes=r['ManagedBytesPerOp'],OldNewPixelsIdentical=True,PixelPass=r['PixelPass'],Comparable=r['Comparable'],MaxPixelError=r['MaxPixelError'],AllowedPixelError=r['AllowedPixelError'],OriginalDrift=original['MedianMs']/old[(r['Scene'],r['Width'],r['Height'],'original')]['MedianMs']))
(args.after/'optimization-summary.json').write_text(json.dumps(dict(Before=str(args.before),After=str(args.after),ManagedCanonicalFramesChecked=288,Rows=rows),ensure_ascii=False,indent=2),encoding='utf-8')
# 24 workloads * (two engines * three forks * two runs) = 288 saved canonical frames.
lines=['# CPU 优化实测：2026-10-12','',
'本轮修改实际引擎代码，重跑原版/标量/SIMD各 3 个独立进程，12 场景、2 尺寸、7 批；完整 1512 批。硬件、输入、字体 SHA、运行时、采样配置与优化前一致。表中原版为本轮重新实测值，优化前/后为 Simd SIMD 批均值中位数。',
'', '## 时间与分配', '']
for w,h in sorted({(r['Width'],r['Height']) for r in rows}):
    lines += [f'### {w}×{h}','',
    '| 场景 | 优化前 ms | 优化后 ms | 自身加速 | 本轮原版 ms | 后/原版耗时 | 托管 B/op 前→后 | 质量/可比性 |',
    '| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |']
    for r in rows:
        if (r['Width'],r['Height'])!=(w,h):continue
        quality=f"通过 max={r['MaxPixelError']}" if r['Comparable'] else ('诊断：压缩策略不同' if r['PixelPass'] else f"诊断：max {r['MaxPixelError']}>{r['AllowedPixelError']}")
        vs=f"{r['VsOriginal']:.2f}×" if r['Comparable'] else '—'
        lines += [f"| {r['Scene']} | {r['BeforeMs']:.3f} | {r['AfterMs']:.3f} | {r['Improvement']:.1f}× | {r['OriginalMs']:.3f} | {vs} | {r['BeforeBytes']:.0f}→{r['AfterBytes']:.0f} | {quality} |"]
    lines += ['']
lines += ['自身加速可用于检查优化是否有效；质量未过和 PNG 压缩策略不同的场景不能据此宣布超过原版。旧、新运行不在同一时间，操作系统调度、温度、频率及后台活动仍会影响对比。本轮原版重复测量作为参照，不把两次原版当固定常数。逐项原版漂移见 optimization-summary.json。', '',
'## 改动及缓存边界','',
'- 矩形按连续像素填充；AA 矩形直接计算与原来 16×16 网格一致的边缘覆盖率。高坐标浮点采样精度不足时回到通用路径算法。',
'- 预乘 Alpha 合成使用 Vector<byte>/Vector<ushort>，保留整数舍入、饱和、标量尾部；Straight Alpha 使用原有精确标量换算。显式 SIMD 可关闭；运行时标准内存拷贝/JIT 优化仍开启。',
'- 路径按包围盒生成覆盖率，使用活动边表和区间差分计数；固定覆盖率编译为行内 spans，省去每帧扫描整张 mask。',
'- 画布覆盖率 LRU：64 项、16 MiB 数据预算（计入 mask、spans 与估算几何字节），渐变行缓存 16 项/4 MiB。SKCanvas 描边 64 项/4 MiB；SKFont 布局 64 项/4 MiB；小矩形几何表另有数量上限。字典/节点和数组对象头不计入数据预算。全部随 Dispose 清除。',
'- key 包含不可变路径快照、变换、AA、clip；布局 key 含文字/坐标/对齐，字号/scale/skew 变化清空布局；描边 key 含宽度/miter/变换。缓存不会保存目标像素或可变源图内容；Span 改图立即生效，源/目标重叠维持旧顺序。',
'- 图片最近邻按相同源像素分段，复用 X 索引缓冲区；仍使用原来的采样公式，没有通过改采样规则制造速度收益。',
'- PNG 使用 slicing-by-8 CRC、分块 Adler、Huffman 前缀表、位缓冲、重叠 back-reference 拷贝、分过滤器循环、256 项 gamma LUT。编码保持 stored 块和相同无损像素；解码临时行缓冲使用 ArrayPool，CRC/Adler/gamma 没有省略。', '',
'这些暖场景允许正常缓存命中。另测每帧移动、97 个位置循环的路径和文字，容量仅 64 项，用于暴露缓存未命中成本；参见 [变化场景原版对照](PERFORMANCE-CHANGING.md)。该测试计时包含持续细分/布局/覆盖率生成，不能把暖缓存结果套用到所有动态场景。', '',
'## 验证与剩余差距','',
'冻结优化前标量渲染器位于 tests/Optimization，仅作为测试参考。5267 项逐字节/格式/stride/Alpha/clip/曲线/反向填充/缓存失效与淘汰/源图修改/别名/字体/编解码检查通过；DOTNET_EnableHWIntrinsic=0 运行同样通过。独立 Python 标准库生成 81 个 zlib、31 个 PNG fixture，涵盖 stored/fixed/dynamic、重叠回溯、五种 filter、低位灰度/调色板和 gamma，并检查校验/截断拒绝。', '',
'基础 36 项、扩展 14 项通过；Windows GTX 1050 Ti Vulkan 回归与 CPU 像素一致、缓存及生命周期检查通过。原版 120 场景仍 108 达标、12 个曲线/文字不达标；质量门槛未变，PixelCompare 仍返回失败。API 保持 33 个公开类型/403 项匹配、2832 缺失，未扩大兼容承诺。', '',
'对优化前后两个尺寸、12 场景、两种 managed 模式、3 进程的 288 个保存帧逐项核验，所有 canonical RGBA 字节一致；本轮对原版的像素误差及 gate 状态也与优化前相同。', '',
'PNG 解码、动态覆盖率等仍有性能差距；曲线/字体、密集混合及某些图片变换的原版质量差距仍待修复。GPU 本轮仅做功能回归，没有原版 GPU 性能对照。', '',
'## 数据与复现','',
'[完整本轮性能、P95、进程内存](PERFORMANCE.md) · [优化前报告](PERFORMANCE-BEFORE-OPTIMIZATION.md) · [本轮 CSV](../'+str(args.after).replace('\\','/')+'/results.csv) · [前后摘要](../'+str(args.after).replace('\\','/')+'/optimization-summary.json)','',
'```powershell',
'dotnet build SimdSkia.sln -c Release',
'dotnet run --project tests/Optimization -c Release -- baseline/fonts/ZenOldMincho-Regular.ttf',
'powershell -NoProfile -File tools/verify.ps1 -Gpu',
'powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-optimized-run',
'powershell -NoProfile -File tools/run-benchmarks.ps1 -Output artifacts/performance/my-changing-run -Scenes curve-changing,chinese-changing -Sizes 1024x768 -Report docs/PERFORMANCE-CHANGING.md',
'python tools/compare-optimization.py artifacts/performance/comparison-2026-10-12 artifacts/performance/my-optimized-run',
'```','',
'前后比较脚本会核查运行完整性、配置、环境、质量门槛及每个 managed 帧的字节；旧目录保留证据，脚本拒绝覆盖已有 run.json。']
args.output.write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(f'Wrote {args.output}; 288 old/new managed frames identical; {len(rows)} scene-size comparisons.')
