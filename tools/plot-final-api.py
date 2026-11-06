"""Generate standalone, exportable plots from frozen measurements only."""
import gzip,hashlib,json,sys
from pathlib import Path
sys.path.insert(0,str(Path('artifacts/report-python').resolve()))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
root=Path('docs/evidence/api-final-2026-11-07')
out=Path('docs/assets/api-final-2026-11-07');out.mkdir(parents=True,exist_ok=True)
load=lambda name:json.loads((root/name).read_text(encoding='utf-8-sig'))
objects=load('frame-objects.json')
plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False,'figure.facecolor':'white','savefig.facecolor':'white'})
def save(fig,name):
    for suffix in ['png','svg']:fig.savefig(out/(name+'.'+suffix),dpi=170,bbox_inches='tight')
    plt.close(fig)
ui=load('ui-final-timing-1/summary.json')
keys=['original-cpu-default','original-cpu','cpu','original-vulkan','original-angle','vulkan','direct2d']
labels=['Skia CPU default','Skia CPU gray','SimdSkia CPU','Skia Vulkan','Skia ANGLE','SimdSkia Vulkan','SimdSkia Direct2D']
colors=['#697787','#9aa6b2','#2384c6','#697787','#9aa6b2','#2384c6','#37a38a']
fig,axes=plt.subplots(1,3,figsize=(15,4.8))
for ax,row in zip(axes,ui):
    values=[row[k]['meanMs'] for k in keys];ax.barh(labels,values,color=colors);ax.invert_yaxis()
    ax.set_title(f"{row['scene']} / scale {row['scale']}");ax.set_xlabel('Completed frame, ms (lower is faster)');ax.grid(axis='x',alpha=.2);ax.set_axisbelow(True)
    ax.set_xlim(0,max(values)*1.24)
    for i,v in enumerate(values):ax.text(v+max(values)*.015,i,f'{v:.3f}',va='center',fontsize=9)
fig.suptitle('Avalonia 12.1.5 / JIT / 3 process medians',fontsize=15)
fig.text(.5,-.02,'Strict whole-page pixel gate: FAIL. GPU includes completion and readback. Not presentation FPS.',ha='center',color='#a02d36')
fig.tight_layout();save(fig,'ui-time')
fig,axes=plt.subplots(1,2,figsize=(12,4.7))
for ax,metric,title in zip(axes,['sampledMedianWorkingSetMiB','sampledMedianPrivateMiB'],['Working set','Private memory']):
    values=[ui[0][k][metric] for k in keys];ax.barh(labels,values,color=colors);ax.invert_yaxis();ax.set_title(title+' / controls 1x');ax.set_xlabel('Process MiB (lower is smaller)');ax.set_xlim(0,max(values)*1.22)
    for i,v in enumerate(values):ax.text(v+max(values)*.012,i,f'{v:.1f}',va='center',fontsize=9)
fig.suptitle('Actual process sampling, 25 ms; not managed heap or VRAM',fontsize=14);fig.tight_layout();save(fig,'ui-memory')
fig,axes=plt.subplots(1,3,figsize=(17,4.7))
for ax,name,title in zip(axes,['gl-timing-3','vk-timing-2','d3d-timing-1'],['System OpenGL Core','Vulkan external target','D3D12 / D2D-on-12']):
    rows=load(name+'/summary.json');y=np.arange(len(rows));o=[r['Original']['msPerFrame'] for r in rows];m=[r['Managed']['msPerFrame'] for r in rows]
    ax.barh(y-.18,o,.34,label='SkiaSharp 3.119.4',color='#78889a');bars=ax.barh(y+.18,m,.34,label='SimdSkia',color='#2384c6')
    ax.set_yticks(y,[r['scene']+(' [quality FAIL]' if not r['quality']['passed'] else '') for r in rows]);ax.invert_yaxis();ax.set_title(title);ax.set_xlabel('Completed frame, ms (lower is faster)');ax.set_xlim(0,max(o+m)*1.27)
    for i,r in enumerate(rows):
        if not r['quality']['passed']:bars[i].set_hatch('///')
        ax.text(o[i]+max(o+m)*.012,i-.18,f'{o[i]:.3f}',va='center',fontsize=9);ax.text(m[i]+max(o+m)*.012,i+.18,f'{m[i]:.3f}',va='center',fontsize=9)
fig.suptitle('640x480 / same GPU / completed RGBA readback / 3 forks',fontsize=14);fig.legend(*axes[0].get_legend_handles_labels(),loc='lower center',bbox_to_anchor=(.5,-.055),ncol=2,frameon=False);fig.tight_layout();save(fig,'gpu-time')
fig,axes=plt.subplots(1,2,figsize=(11,3.7))
for ax,metric,title in zip(axes,['cpuPercentOfMachine','cpuMsPerFrame'],['Whole-machine CPU, %','Process CPU time, ms/frame']):
    values=[];names=[];barcolors=[]
    for backend,folder in [('GL','gl-paced-2'),('Vulkan','vk-paced-1'),('D3D12','d3d-paced-1')]:
        row=load(folder+'/summary.json')[0]
        for variant,label,color in [('Original','Skia','#78889a'),('Managed','SimdSkia','#2384c6')]:values.append(row[variant][metric]);names.append(backend+' '+label);barcolors.append(color)
    ax.bar(names,values,color=barcolors);ax.set_title(title);ax.tick_params(axis='x',rotation=15);ax.set_ylim(0,max(values)*1.3)
    for i,v in enumerate(values):ax.text(i,v+max(values)*.025,f'{v:.3f}',ha='center',fontsize=10)
fig.suptitle('Same 60 FPS workload: rectangles, including completed GPU readback',fontsize=13);fig.tight_layout();save(fig,'gpu-paced-cpu')
fig,axes=plt.subplots(3,3,figsize=(12,9))
for row_index,(variant,reference,label) in enumerate([('cpu','original-cpu','CPU / grayscale reference'),('vulkan','original-vulkan','Vulkan'),('direct2d','original-angle','Direct2D / ANGLE reference')]):
    def frame(name):
        folder=root/'ui-final-timing-1'/('controls-1-'+name+'-0');meta=json.loads((folder/'metrics.json').read_text(encoding='utf-8-sig'))
        key=str((folder/'scene.rgba').relative_to(root)).replace('\\','/');item=objects[key];data=gzip.decompress((root/item['object']).read_bytes())
        assert len(data)==item['length'] and hashlib.sha256(data).hexdigest()==item['sha256']
        raw=np.frombuffer(data,dtype=np.uint8).reshape(meta['height'],meta['width'],4)
        return raw
    original=frame(reference);managed=frame(variant)
    for ax,data,title in [(axes[row_index,0],original,'SkiaSharp'),(axes[row_index,1],managed,'SimdSkia')]:
        # Captures are physical premultiplied RGBA. Unpremultiply only for
        # display, keeping the raw-byte difference below unchanged.
        a=data[...,3:4].astype(float);rgb=np.divide(data[...,:3].astype(float)*255,a,out=np.zeros_like(data[...,:3],dtype=float),where=a!=0)
        display=np.concatenate((np.clip(rgb,0,255).astype(np.uint8),data[...,3:4]),axis=2)
        ax.imshow(display);ax.set_title(label+' / '+title,fontsize=10);ax.axis('off')
    delta=np.max(np.abs(original.astype(np.int16)-managed.astype(np.int16)),axis=2)
    heat=axes[row_index,2].imshow(delta,cmap='magma',vmin=0,vmax=64);axes[row_index,2].set_title('Error heatmap\nmax channel, clipped at 64',fontsize=10);axes[row_index,2].axis('off')
fig.colorbar(heat,ax=axes[:,2],shrink=.55,label='Channel error')
fig.suptitle('Retained whole-page quality failures, controls 1x / 1000x720',fontsize=14)
save(fig,'ui-quality')
manifest=dict(generator=__file__,generatorSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),evidenceIndexSha256=hashlib.sha256((root/'sha256.json').read_bytes()).hexdigest(),files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in out.iterdir() if p.suffix in ['.png','.svg']})
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('Wrote',len(manifest['files']),'PNG/SVG figures from frozen evidence.')
