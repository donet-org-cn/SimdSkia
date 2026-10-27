"""Rebuild the preview.8 cnblogs article from frozen, portable evidence.

Requires matplotlib/numpy for reporting only. Preserves the preview.7 charts
and measurements; never changes raw gates or implies that quality passed.
"""
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/blog'
ASSETS=OUT/'2026-10-27-simdskia-preview8-assets'
DATA=ASSETS/'data'
DATA.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'docs/evidence/parity-2026-10-27'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
evidence=read(SOURCE/'results.json')
sizes=read(ROOT/'docs/evidence/default-avalonia-aot-2026-10-26.json')
cpu,gpu=evidence['cpuSummary'],evidence['gpuSummary']
assert len(evidence['cpuRuns'])==75 and len(evidence['gpuRuns'])==36
assert evidence['audit']['speedPassed'] and not evidence['audit']['qualityPassed']
assert all(q['unchangedFromPreview7'] for q in evidence['cpuQuality'])

def table(headers,rows):
    return '\n'.join(['| '+' | '.join(headers)+' |','|'+'|'.join(' --- ' for _ in headers)+'|']+
        ['| '+' | '.join(map(str,row))+' |' for row in rows])
scene=lambda r:('控件页' if r['scene']=='controls' else '滚动列表')+f" {r['scale']:g}×"
case=lambda r:scene(r)+(' / 吞吐' if r['mode']=='throughput' else ' / 60 FPS')
names={'original':'原版','preview7':'preview.7','candidate':'preview.8 实现',
       'original-vulkan':'原版 Vulkan','original-angle':'原版 ANGLE/D3D11','vulkan':'SimdSkia Vulkan','direct2d':'SimdSkia Direct2D'}
values={}
values['CPU_TIME_TABLE']=table(['场景','原版 ms/帧','preview.7','本轮','本轮/原版'],[
    [case(r),f"{r['original']['drawMsPerFrame']:.4f}",f"{r['preview7']['drawMsPerFrame']:.4f}",
     f"{r['candidate']['drawMsPerFrame']:.4f}",f"{r['ratio']:.3f}×"] for r in cpu])
values['CPU_RESOURCE_TABLE']=table(['场景','实现','CPU ms/帧','整机 CPU%','WS MiB','Private MiB','托管分配 KiB/帧'],[
    [case(r),names[v],f"{r[v]['cpuMsPerFrame']:.3f}",f"{r[v]['cpuPercentMachine']:.2f}",
     f"{r[v]['workingSetMiB']:.2f}",f"{r[v]['privateMiB']:.2f}",f"{r[v]['allocatedBytesPerFrame']/1024:.2f}"]
    for r in cpu for v in ['original','preview7','candidate']])
variants=['original-vulkan','original-angle','vulkan','direct2d']
values['GPU_TIME_TABLE']=table(['场景']+[names[v]+' ms/完成帧' for v in variants],[
    [scene(r)]+[f"{r[v]['meanMs']:.4f}" for v in variants] for r in gpu])
values['GPU_RESOURCE_TABLE']=table(['场景','后端','CPU ms/帧','整机 CPU%','WS MiB','Private MiB','托管分配 KiB/帧'],[
    [scene(r),names[v],f"{r[v]['cpuMsPerFrame']:.3f}",f"{r[v]['cpuPercentMachine']:.2f}",
     f"{r[v]['sampledMedianWorkingSetMiB']:.2f}",f"{r[v]['sampledMedianPrivateMiB']:.2f}",f"{r[v]['allocatedBytesPerFrame']/1024:.2f}"]
    for r in gpu for v in variants])
values['QUALITY_TABLE']=table(['CPU 场景','最大通道误差','RMSE','原版整页门槛'],[
    [case(q),q['max'],f"{q['rmse']:.4f}",'通过' if q['passed'] else '未通过'] for q in evidence['cpuQuality']])
values['SIZE_TABLE']=table(['默认空白项目，preview.7 历史','原版 MiB','SimdSkia MiB'],[
    [label]+[f"{sizes['variants'][v]['totals'][key]/1048576:.2f}" for v in ['original','simdskia']]
    for label,key in [('运行文件，不含符号','runtime'),('EXE','exe'),('全部 DLL','dll'),('原始 publish，含 PDB','allPublished')]])
values['REMOVED_TABLE']=table(['原版原生 DLL','字节','MiB'],[
    [row['path'],f"{row['bytes']:,}",f"{row['bytes']/1048576:.2f}"]
    for row in sizes['variants']['original']['files'] if row['category']=='dll'])
legacy=(ROOT/'tools/templates/cnblogs-simdskia-avalonia.md').read_text(encoding='utf-8-sig')
feature=legacy[legacy.index('| 功能 | 原版'):legacy.index('\n\n“C# 实现”')]
feature=feature.replace('SimdSkia preview.7','SimdSkia preview.8').replace('不在本次 CPU 速度排名中','本次另测 JIT 离屏完成帧')
values['FEATURE_TABLE']=feature

for name in ['cpu-time','cpu-memory','gpu-time','gpu-memory']:
    for ext in ['png','svg']:shutil.copyfile(SOURCE/f'{name}.{ext}',ASSETS/f'{name}.{ext}')
for name in ['results.json','sampling-aot.json','font-instruction-controls.json']:
    shutil.copyfile(SOURCE/name,DATA/name)
shutil.copyfile(ROOT/'docs/evidence/default-avalonia-aot-2026-10-26.json',DATA/'default-aot-preview7.json')

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
plt.rcParams.update({'font.family':'Microsoft YaHei','axes.unicode_minus':False,'font.size':10})
def chart(name,title,labels,series,ylabel):
    fig,ax=plt.subplots(figsize=(10,5)); x=np.arange(len(labels)); width=.8/len(series)
    colors=['#747d8c','#ed9d39','#167a91']
    for i,(label,ys) in enumerate(series):
        bars=ax.bar(x+(i-(len(series)-1)/2)*width,ys,width,label=label,color=colors[i]);ax.bar_label(bars,fmt='%.2f',padding=3,fontsize=9)
    ax.set_xticks(x,labels);ax.set_title(title,loc='left',pad=15);ax.set_ylabel(ylabel)
    ax.set_ylim(0,ax.get_ylim()[1]*1.16);ax.legend();ax.grid(axis='y',alpha=.2);ax.set_axisbelow(True)
    fig.tight_layout()
    for ext in ['png','svg']:fig.savefig(ASSETS/f'{name}.{ext}',dpi=160)
    plt.close(fig)
paced=[r for r in cpu if r['mode']=='paced']
assert len(paced)==2
for name,field,divisor,title,ylabel in [
    ('cpu-paced','cpuPercentMachine',1,'60 FPS 节拍：进程 CPU 占整机比例','CPU%，越低越好'),
    ('cpu-allocation','allocatedBytesPerFrame',1024,'60 FPS 节拍：每帧托管分配','KiB / 帧，越低越好')]:
    chart(name,title,[scene(r) for r in paced],[(names[v],[r[v][field]/divisor for r in paced]) for v in ['original','preview7','candidate']],ylabel)
chart('aot-size-preview7','默认 Avalonia 空白模板 AOT 体积（preview.7 历史测量）',
      ['运行文件','EXE','全部原生 DLL'],[(name,[sizes['variants'][v]['totals'][k]/1048576 for k in ['runtime','exe','dll']])
      for v,name in [('original','原版默认'),('simdskia','SimdSkia preview.7 CPU')]],'MiB，越低越好')
article=(ROOT/'tools/templates/cnblogs-simdskia-preview8.md').read_text(encoding='utf-8')
for key,value in values.items():article=article.replace('{{'+key+'}}',value)
assert '{{' not in article,'Unresolved article field'
path=OUT/'2026-10-27-simdskia-avalonia-aot-cnblogs.md'
path.write_text(article,encoding='utf-8')
manifest={'version':'0.1.0-preview.8','performanceSource':'docs/evidence/parity-2026-10-27/results.json',
          'aotSizeVersion':'0.1.0-preview.7','qualityPassed':False,
          'files':{p.relative_to(OUT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(ASSETS.rglob('*')) if p.is_file() and p.name!='manifest.json'}}
(ASSETS/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
bundle=OUT/'2026-10-27-simdskia-cnblogs.zip'
note=OUT/'博客园发布说明.txt'
note.write_text('文章为 2026-10-27-simdskia-avalonia-aot-cnblogs.md，标题已写好。\n配图在 2026-10-27-simdskia-preview8-assets，包含 7 张 PNG、7 张 SVG 和原始 JSON 数据。\n本地 Markdown 可直接预览；博客园发布时将 PNG 上传到博客园图片服务，再替换正文相对图片地址。\n保留 2026-10-27-simdskia-assets 作为 preview.7 历史证据；新版文章不使用旧性能结论。\n没有自动发布到博客园，也没有虚构图床 URL。\n',encoding='utf-8')
with zipfile.ZipFile(bundle,'w',zipfile.ZIP_DEFLATED) as zipped:
    zipped.write(path,path.name)
    zipped.write(note,note.name)
    for p in sorted(ASSETS.rglob('*')):
        if p.is_file():zipped.write(p,p.relative_to(OUT).as_posix())
print(f'Wrote {path}; 7 PNG + 7 SVG, portable evidence and {bundle.name}.')
