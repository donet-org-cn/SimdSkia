"""Generate the cnblogs Markdown article, numeric charts and portable evidence bundle.

Requires matplotlib/numpy for reporting only; never imported in timed workers.
"""
from pathlib import Path
import argparse
import csv
import hashlib
import io
import json
import re
import shutil
import statistics
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MIB = 1048576
SCENES = [('controls-1-throughput','控件 1000×720'),('controls-1.5-throughput','控件 1500×1080'),('scroll-1-throughput','滚动列表 1000×720')]
PACED = [('controls-1-paced','控件页'),('scroll-1-paced','滚动列表')]


def read(path):
    blob = path.read_bytes()
    return json.loads(blob.decode('utf-16') if blob.startswith((b'\xff\xfe',b'\xfe\xff')) else blob.decode('utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def table(headers, rows):
    return '\n'.join(['| '+' | '.join(headers)+' |','|'+'|'.join(' --- ' for _ in headers)+'|']+['| '+' | '.join(str(x) for x in row)+' |' for row in rows])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input',type=Path,default=ROOT/'artifacts/cnblogs-performance-2026-10-26')
    parser.add_argument('--output',type=Path,default=ROOT/'docs/blog')
    args = parser.parse_args()
    root = args.input.resolve(); out = args.output.resolve()
    out.mkdir(parents=True,exist_ok=True)
    assets = out/'2026-10-27-simdskia-assets'; data = assets/'data'
    data.mkdir(parents=True,exist_ok=True)
    sizes = read(ROOT/'docs/evidence/default-avalonia-aot-2026-10-26.json')
    idle = read(root/'idle/results.json')
    status = read(root/'exit-status.json')
    assert set(status)=={'portable','v3'} and all(v in (0,2) for v in status.values())
    runs = {}; summaries = {}; quality = {}; manifests = {}
    for isa in ['portable','v3']:
        source = root/(isa+'-measured')
        runs[isa] = read(source/'results.json'); summaries[isa] = read(source/'summary.json')
        quality[isa] = read(source/'quality.json'); manifests[isa] = read(source/'manifest.json')
        assert len(runs[isa])==45 and len(summaries[isa])==15 and len(quality[isa])==10
        assert manifests[isa]['forks']==3 and manifests[isa]['seconds']==5 and manifests[isa]['warmupSeconds']==2
        for row in runs[isa]:
            assert row['nativeAot'] and row['imageBrushProbePassed'] and row['avx2']==(isa=='v3')
            assert not {m.lower() for m in row['loadedModules']}.intersection({'coreclr.dll','clrjit.dll','clr.dll'})
        for item in manifests[isa]['binaries'].values():
            assert sha(Path(item['path']))==item['sha256']
        published_sizes=read(source/'sizes.json')
        for engine,item in manifests[isa]['binaries'].items():
            directory=Path(item['path']).parent
            for f in published_sizes[engine]['files']:
                assert sha(directory/f['path'])==f['sha256'],f['path']
        for name,digest in read(source/'source-files.json').items():
            assert sha(ROOT/name)==digest, ('Measured source changed',name)
        for name in ['manifest.json','results.json','summary.json','summary.csv','quality.json','source-files.json','run-harness.py.txt']:
            shutil.copyfile(source/name,data/(isa+'-'+name))
    assert len(idle['runs'])==6 and len(idle['summary'])==2
    original_dlls=[]
    for isa in ['portable','v3']:
        published=read(root/(isa+'-measured')/'sizes.json')['original']['files']
        original_dlls.append({r['path']:r['sha256'] for r in published if r['category']=='nativeLibrary'})
    assert original_dlls[0]==original_dlls[1] and len(original_dlls[0])==2
    font_hashes={r['fontSha256'] for group in runs.values() for r in group}
    assert len(font_hashes)==1
    capture_hashes={}
    for isa in ['portable','v3']:
        for r in runs[isa]:
            p=root/(isa+'-measured')/r['case']/f"{r['variant']}-{r['fork']}"/'scene.rgba'
            capture_hashes[f"{isa}/{r['case']}/{r['variant']}/{r['fork']}"]=sha(p)
    cross_isa=[capture_hashes[f"portable/{case}/{variant}/1"]==capture_hashes[f"v3/{case}/{variant}/1"] for case in {r['case'] for r in runs['portable']} for variant in ['simd','original-default','original-full']]
    stability={f"{isa}/{case}/{variant}":len({capture_hashes[f"{isa}/{case}/{variant}/{fork}"] for fork in (1,2,3)})==1 for isa in ['portable','v3'] for case in {r['case'] for r in runs[isa]} for variant in ['simd','original-default','original-full']}
    (data/'capture-integrity.json').write_text(json.dumps(dict(originalNativeDllSha256=original_dlls[0],fontSha256=next(iter(font_hashes)),rgbaSha256=capture_hashes,crossIsaEqual=sum(cross_isa),crossIsaTotal=len(cross_isa),forkStability=stability),indent=2)+'\n',encoding='utf-8')
    for row in idle['runs']:
        expected = next(r['sha256'] for r in sizes['variants'][row['variant']]['files'] if r['category']=='exe')
        assert row['exeSha256']==expected
    shutil.copyfile(root/'idle/results.json',data/'default-idle.json')
    shutil.copyfile(ROOT/'docs/evidence/default-avalonia-aot-2026-10-26.json',data/'default-aot-size.json')
    shutil.copyfile(root/'preflight-excluded.json',data/'preflight-excluded.json')
    preflight=root/'portable/results.json'
    if preflight.exists():shutil.copyfile(preflight,data/'preflight-excluded-partial-runs.json')
    assets_json = read(ROOT/'examples/AvaloniaAot/SimdSkia/obj/project.assets.json')
    assert assets_json['libraries']['SimdSkia.Avalonia/0.1.0-preview.7']['type']=='package'
    assert not any(v['type']=='project' or 'skiasharp' in k.lower() or 'harfbuzz' in k.lower() for k,v in assets_json['libraries'].items())
    def row(isa,case,variant):
        return next(r for r in summaries[isa] if r['case']==case and r['variant']==variant)
    def label(isa):return '通用 x64' if isa=='portable' else 'x86-64-v3'
    # Imports and font cache initialization happen only after all timed processes.
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from matplotlib import font_manager
    import numpy as np
    font = Path('C:/Windows/Fonts/msyh.ttc')
    if font.exists():font_manager.fontManager.addfont(str(font));plt.rcParams['font.family']='Microsoft YaHei'
    plt.rcParams.update({'axes.unicode_minus':False,'font.size':12,'axes.titlesize':14,'axes.labelsize':12,'svg.fonttype':'path'})
    gray,blue = '#64748b','#2563eb'
    def save(fig,name):
        fig.tight_layout(pad=1.5)
        fig.savefig(assets/(name+'.png'),dpi=170,bbox_inches='tight',facecolor='white')
        fig.savefig(assets/(name+'.svg'),bbox_inches='tight',facecolor='white')
        plt.close(fig)
    def beautify(ax):
        ax.spines[['top','right']].set_visible(False)
        ax.grid(axis='y',alpha=.2);ax.set_axisbelow(True);ax.set_ylim(bottom=0)
    def bars(ax,x,values,width,color,name,errors=None,fmt='.2f'):
        obj = ax.bar(x,values,width,color=color,label=name,yerr=errors,capsize=3,error_kw={'elinewidth':1})
        ax.bar_label(obj,labels=[format(v,fmt) for v in values],padding=5,fontsize=10)
        return obj
    a,b = sizes['variants']['original'],sizes['variants']['simdskia']
    fig,ax=plt.subplots(figsize=(9,4.8))
    values=[a['totals']['exe']/MIB,b['totals']['exe']/MIB];dll=[a['totals']['dll']/MIB,b['totals']['dll']/MIB]
    ax.bar([0,1],values,.5,label='EXE',color=blue)
    ax.bar([0,1],dll,.5,bottom=values,label='外置原生 DLL',color=gray)
    for i,v in enumerate([a['totals']['runtime']/MIB,b['totals']['runtime']/MIB]):ax.text(i,v+.6,f'{v:.2f} MiB',ha='center',weight='bold')
    ax.set_xticks([0,1],['原版默认后端','SimdSkia 默认 CPU']);ax.set_ylabel('运行文件 MiB（排除 PDB）');ax.set_title('默认 Avalonia 空白应用：AOT 发布体积')
    ax.set_ylim(0,43);beautify(ax);ax.legend();save(fig,'aot-size')
    fig,axes=plt.subplots(1,2,figsize=(12,5),sharey=True)
    x=np.arange(3)
    for ax,isa in zip(axes,['portable','v3']):
        for delta,variant,color,name in [(-.19,'original-default',gray,'原版默认'),(.19,'simd',blue,'SimdSkia')]:
            selected=[row(isa,c,variant) for c,_ in SCENES]
            v=[r['drawMsPerFrame'] for r in selected]
            err=np.array([[r['drawMsPerFrame']-r['drawMsPerFrameMin'] for r in selected],[r['drawMsPerFrameMax']-r['drawMsPerFrame'] for r in selected]])
            bars(ax,x+delta,v,.35,color,name,err)
        ax.set_xticks(x,['控件 100%','控件 150%','滚动列表']);ax.set_title(label(isa));beautify(ax);ax.legend(fontsize=10)
    axes[0].set_ylabel('离屏绘制 ms/帧（越低越好）');fig.suptitle('相同 CPU 负载 / Native AOT / 3 进程中位数',y=1.03);save(fig,'cpu-draw')
    fig,axes=plt.subplots(1,2,figsize=(12,4.8),sharey=True);x=np.arange(2)
    for ax,isa in zip(axes,['portable','v3']):
        for delta,variant,color,name in [(-.19,'original-default',gray,'原版默认'),(.19,'simd',blue,'SimdSkia')]:
            selected=[row(isa,c,variant) for c,_ in PACED];v=[r['cpuPercentMachine'] for r in selected]
            err=np.array([[r['cpuPercentMachine']-r['cpuPercentMachineMin'] for r in selected],[r['cpuPercentMachineMax']-r['cpuPercentMachine'] for r in selected]])
            bars(ax,x+delta,v,.35,color,name,err)
        ax.set_xticks(x,['控件页','滚动列表']);ax.set_title(label(isa));beautify(ax);ax.legend(fontsize=10)
    axes[0].set_ylabel('整机口径 CPU%（越低越好）');fig.suptitle('60 FPS 节拍 / CPU 渲染 / 所有进程线程',y=1.03);save(fig,'paced-cpu')
    fig,axes=plt.subplots(1,2,figsize=(12,5))
    for ax,key,title in zip(axes,['sampledMedianWorkingSetMiB','sampledMedianPrivateMiB'],['工作集 Working Set','私有提交 Private Bytes']):
        selected=[row(isa,'controls-1-paced',v) for isa in ['portable','v3'] for v in ['original-default','simd']]
        values=[r[key] for r in selected];errors=np.array([[r[key]-r[key+'Min'] for r in selected],[r[key+'Max']-r[key] for r in selected]])
        bars(ax,np.arange(4),values,.6,[gray,blue,gray,blue],'',errors,fmt='.1f')
        ax.set_xticks(np.arange(4),['原版\n通用','SimdSkia\n通用','原版\nv3','SimdSkia\nv3']);ax.set_title(title);ax.set_ylabel('MiB');beautify(ax)
    fig.suptitle('60 FPS 控件页：独立采样的进程内存',y=1.03);save(fig,'paced-memory')
    fig,ax=plt.subplots(figsize=(9,4.8));x=np.arange(2)
    for delta,variant,color,name in [(-.19,'original',gray,'原版默认'),(.19,'simdskia',blue,'SimdSkia')]:
        r=next(r for r in idle['summary'] if r['variant']==variant);keys=['medianWorkingSetMiB','medianPrivateMiB'];values=[r[k] for k in keys]
        err=np.array([[r[k]-r[k+'Min'] for k in keys],[r[k+'Max']-r[k] for k in keys]])
        bars(ax,x+delta,values,.35,color,name,err,fmt='.1f')
    ax.set_xticks(x,['工作集 Working Set','私有提交 Private Bytes']);ax.set_ylabel('MiB');ax.set_title('默认 AOT 空白窗口：可见空闲状态，3 进程中位数');beautify(ax);ax.legend();save(fig,'idle-memory')
    qroot=root/'v3-measured/controls-1-throughput'
    original=np.frombuffer((qroot/'original-default-1/scene.rgba').read_bytes(),dtype=np.uint8).reshape(720,1000,4)
    managed=np.frombuffer((qroot/'simd-1/scene.rgba').read_bytes(),dtype=np.uint8).reshape(720,1000,4)
    diff=np.clip(np.abs(original[:,:,:3].astype(np.int16)-managed[:,:,:3].astype(np.int16))*4,0,255).astype(np.uint8)
    fig,axes=plt.subplots(1,3,figsize=(15,4.2))
    for ax,pixels,name in zip(axes,[original,managed,diff],['原版默认','SimdSkia','通道差异 ×4（黑色表示无差异）']):ax.imshow(pixels);ax.set_title(name);ax.axis('off')
    save(fig,'quality')
    # Generate tables from exactly the same summary records as the charts.
    size_rows=[]
    for title,key in [('EXE','exe'),('DLL 合计','dll'),('运行文件合计，不含 PDB','runtime'),('PDB 调试符号','symbols'),('原始 publish，包含 PDB','allPublished')]:size_rows.append([title,f"{a['totals'][key]/MIB:.2f}",f"{b['totals'][key]/MIB:.2f}"])
    size_rows.insert(1,['DLL 数量',a['dllCount'],b['dllCount']]);size_rows.append(['完整运行 ZIP',f"{a['runtimeZip']['bytes']/MIB:.2f}",f"{b['runtimeZip']['bytes']/MIB:.2f}"])
    removed=[r for r in a['files'] if r['path'] not in {x['path'] for x in b['files']}]
    removed_rows=[[f"`{r['path']}`",f"{r['bytes']:,}",f"{r['bytes']/MIB:.2f}",'调试符号' if r['category']=='symbols' else '原生 DLL'] for r in removed]
    idle_rows=[]
    for r in idle['summary']:
        idle_rows.append(['原版默认' if r['variant']=='original' else 'SimdSkia',f"{r['cpuPercentMachine']:.4f}",f"{r['cpuPercentMachineMin']:.4f}–{r['cpuPercentMachineMax']:.4f}",f"{r['medianWorkingSetMiB']:.2f}",f"{r['medianPrivateMiB']:.2f}"])
    speed_rows=[];opacity_rows=[];paced_rows=[];memory_rows=[]
    for isa in ['portable','v3']:
        for case,title in SCENES:
            o,s=row(isa,case,'original-default'),row(isa,case,'simd')
            speed_rows.append([label(isa),title,f"{o['drawMsPerFrame']:.3f}",f"{s['drawMsPerFrame']:.3f}",f"{s['drawMsPerFrame']/o['drawMsPerFrame']:.2f}×"])
        for v,title in [('original-default','原版默认'),('original-full','原版完整透明组'),('simd','SimdSkia 完整组')]:
            r=row(isa,'controls-1-throughput',v);opacity_rows.append([label(isa),title,f"{r['drawMsPerFrame']:.3f}",f"{r['p95Ms']:.3f}"])
        for case,title in PACED:
            for v,name in [('original-default','原版默认'),('simd','SimdSkia')]:
                r=row(isa,case,v)
                paced_rows.append([label(isa),title,name,f"{r['framesPerSecond']:.2f}",f"{r['cpuPercentMachine']:.2f}",f"{r['cpuMsPerFrame']:.3f}",f"{r['missedDeadlines']:.0f}"])
                memory_rows.append([label(isa),title,name,f"{r['sampledMedianWorkingSetMiB']:.1f}",f"{r['sampledMedianPrivateMiB']:.1f}",f"{r['allocatedBytesPerFrame']/1024:.2f}"])
    quality_rows=[]
    for isa in ['portable','v3']:
        for q in quality[isa]:
            if q['case'].endswith('throughput'):
                name=next(n for c,n in SCENES if c==q['case'])
                quality_rows.append([label(isa),name,'原版默认' if q['reference']=='original-default' else '原版完整组',q['maximumChannelError'],f"{q['rmse']:.3f}",'通过' if q['passed'] else '**未通过**'])
    all_quality=[q for group in quality.values() for q in group];passed=sum(q['passed'] for q in all_quality)
    po,ps=row('portable','controls-1-throughput','original-default'),row('portable','controls-1-throughput','simd')
    vo,vs=row('v3','controls-1-throughput','original-default'),row('v3','controls-1-throughput','simd')
    io_,is_=idle['summary']
    cpu=read(root/'hardware.json');cpu=cpu[0] if isinstance(cpu,list) else cpu
    def direction(s,o):return f"耗时为原版的 **{s/o:.2f} 倍**"+("，更慢" if s>o else "，更快")
    tokens={
        'CPU_NAME':cpu['Name'].strip(),'PROCESSORS':str(manifests['portable']['processors']),'RAM_GIB':f"{manifests['portable']['physicalMemoryBytes']/1024**3:.2f}",'SDK':manifests['portable']['sdk'],'ONE_CORE_PERCENT':f"{100/manifests['portable']['processors']:.2f}",
        'SUMMARY':f"- **默认应用运行体积：36.53 → 19.51 MiB，减少 46.59%。**\n- **通用 AOT 控件页：{po['drawMsPerFrame']:.3f} → {ps['drawMsPerFrame']:.3f} ms/帧（原版 → SimdSkia）。**\n- **v3 AOT 同一控件页：{vo['drawMsPerFrame']:.3f} → {vs['drawMsPerFrame']:.3f} ms/帧。**\n- **体积下降不保证内存更低或绘制更快；完整 API/页面画质兼容仍未通过。**",
        'SIZE_TABLE':table(['项目','原版 MiB','SimdSkia MiB'],size_rows),'REMOVED_TABLE':table(['文件','字节','MiB','类型'],removed_rows),
        'IDLE_TABLE':table(['实现','整机 CPU%','3 进程 CPU% 范围','工作集 MiB','Private MiB'],idle_rows),
        'IDLE_DISCUSSION':f"这组默认应用的工作集中位数为原版 **{io_['medianWorkingSetMiB']:.2f} MiB**、SimdSkia **{is_['medianWorkingSetMiB']:.2f} MiB**；Private 分别为 **{io_['medianPrivateMiB']:.2f} / {is_['medianPrivateMiB']:.2f} MiB**。这是默认 GPU 平台配置与默认 CPU 配置的实际应用资源对照，不用于解释两种 CPU 光栅算法的效率。",
        'SPEED_TABLE':table(['AOT','CPU 场景','原版 ms/帧','SimdSkia ms/帧','SimdSkia/原版耗时'],speed_rows),
        'SPEED_DISCUSSION':f"100% DPI 控件页中，通用 AOT 的 SimdSkia {direction(ps['drawMsPerFrame'],po['drawMsPerFrame'])}；v3 的结果是{direction(vs['drawMsPerFrame'],vo['drawMsPerFrame'])}。其他场景按表逐项看，不能只选最好的一行代表全部 UI。",
        'OPACITY_TABLE':table(['AOT','透明度路径','平均绘制 ms/帧','P95 ms'],opacity_rows),
        'PACED_TABLE':table(['AOT','场景','实现','实测帧/s','整机 CPU%','CPU ms/帧','5 秒内错过时限次数'],paced_rows),
        'MEMORY_TABLE':table(['AOT','场景','实现','工作集 MiB','Private MiB','托管分配 KiB/帧'],memory_rows),
        'MEMORY_DISCUSSION': f"100% DPI 控件页的通用 AOT 组，原版/SimdSkia 工作集为 **{row('portable','controls-1-paced','original-default')['sampledMedianWorkingSetMiB']:.1f} / {row('portable','controls-1-paced','simd')['sampledMedianWorkingSetMiB']:.1f} MiB**；v3 对应 **{row('v3','controls-1-paced','original-default')['sampledMedianWorkingSetMiB']:.1f} / {row('v3','controls-1-paced','simd')['sampledMedianWorkingSetMiB']:.1f} MiB**。默认空窗和固定中文字体控件页是不同负载，不能用其中一组代替另一组。\n\n滚动场景的托管分配尤其值得继续处理：通用组原版 **{row('portable','scroll-1-paced','original-default')['allocatedBytesPerFrame']/1024:.2f} KiB/帧**，SimdSkia **{row('portable','scroll-1-paced','simd')['allocatedBytesPerFrame']/1024:.2f} KiB/帧**。这一轮只定位到场景级别，尚未据此确认具体分配调用栈；不能因为 v3 降低了绘制耗时就忽略 GC 压力。\n\n体积和内存使用不同口径：删除原生 DLL 会减少发布文件，但运行时还会有字体数据、像素缓冲、路径、图层与缓存。这里保留所有场景的采样结果，不把发布目录少了多少直接换算成内存节省。",
        'QUALITY_TABLE':f"两种 ISA、5 个场景/节拍、两种原版透明度路径共 **{len(all_quality)} 组对照，{passed} 组通过、{len(all_quality)-passed} 组未通过**。以下列出 3 个独立吞吐场景，节拍重复场景的完整结果在原始记录中。\n\n"+table(['AOT','场景','对照','最大通道误差','RMSE','既定门槛'],quality_rows),
        'ISA_QUALITY':f"跨 ISA 的首进程捕获有 **{sum(cross_isa)}/{len(cross_isa)}** 对逐字节一致；同一 ISA/场景/实现的三次捕获有 **{sum(stability.values())}/{len(stability)}** 组一致。像素哈希与失败记录均附在原始数据中，未将它们替换为全通过。",
        'CONCLUSION':f"默认应用的外置 DLL 和分发体积已经明显减少。通用 AOT 的控件页绘制从原版 {po['drawMsPerFrame']:.3f} ms/帧变为 SimdSkia {ps['drawMsPerFrame']:.3f} ms/帧；v3 对应 {vo['drawMsPerFrame']:.3f} / {vs['drawMsPerFrame']:.3f} ms/帧。速度需要按发布指令集和具体场景评估，CPU、内存也要按上表分别看。\n\n完整页面质量仍有 {len(all_quality)-passed} 组未过门槛，公共 API 也没有覆盖原版全部表面。我还不能据此宣称它已经全面追平 SkiaSharp 或可以无条件替换所有 Avalonia 应用。",
    }
    article=(ROOT/'tools/templates/cnblogs-simdskia-avalonia.md').read_text(encoding='utf-8')
    for key,value in tokens.items():article=article.replace('{{'+key+'}}',value)
    assert not re.search(r'\{\{[A-Z_]+\}\}',article),'Unexpanded article marker'
    md=out/'2026-10-27-simdskia-preview7-history.md';md.write_text(article,encoding='utf-8')
    for target in re.findall(r'!\[[^\]]*\]\(([^)]+)\)',article):assert (out/target).is_file(),target
    manifest=dict(articleSha256=sha(md),chartFiles={p.name:sha(p) for p in assets.glob('*.png')},toolSha256=sha(Path(__file__)),matplotlibVersion=matplotlib.__version__,formalProcesses=sum(len(v) for v in runs.values()),idleProcesses=len(idle['runs']),qualityPassed=passed,qualityTotal=len(all_quality),measuredExitStatus=status,publicPackage='SimdSkia.Avalonia 0.1.0-preview.7',hardware=cpu)
    (data/'article-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    note=out/'preview7-history-publishing.txt'
    note.write_text('此文件为 preview.7 历史测量说明，文章为 2026-10-27-simdskia-preview7-history.md。\n配图在 2026-10-27-simdskia-assets，包含 PNG、SVG 与原始数据。\n当前 preview.8 文章请使用 tools/report-preview8-cnblogs.py 生成，不要用本历史数据覆盖新结果。\n没有自动发布到博客园，也没有虚构图床 URL。\n',encoding='utf-8')
    archive=out/'2026-10-27-simdskia-preview7-history.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for p in [md,note,*sorted(assets.rglob('*'))]:
            if p.is_file():z.write(p,p.relative_to(out))
    with zipfile.ZipFile(archive) as z:assert z.testzip() is None
    print(json.dumps(manifest,ensure_ascii=False,indent=2));print('Article:',md);print('Bundle:',archive)


if __name__=='__main__':main()
