"""Same-GPU Vulkan completed draw + fence + host-readback slow-case regression.

JIT, original SkiaSharp 3.119.4, ordered batches on/off in the same SimdSkia
worker. Changing glyph/path timings are diagnostic when native pixels fail.
"""
import argparse,hashlib,json,os,pathlib,statistics,subprocess
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',type=pathlib.Path,required=True);p.add_argument('--forks',type=int,default=3);p.add_argument('--before',type=pathlib.Path);a=p.parse_args()
root=pathlib.Path(__file__).resolve().parents[1];a.output=a.output.resolve();a.output.mkdir(parents=True,exist_ok=False)
def save(p,x):p.write_text(json.dumps(x,indent=2)+'\n',encoding='utf-8')
workers={'original':root/'benchmarks/GpuReference/bin/Release/net10.0/SimdSkia.Benchmark.GpuReference.dll','disabled':root/'benchmarks/GpuManaged/bin/Release/net10.0/SimdSkia.Benchmark.GpuManaged.dll','ordered':root/'benchmarks/GpuManaged/bin/Release/net10.0/SimdSkia.Benchmark.GpuManaged.dll'}
if a.before:workers['before']=a.before.resolve()
hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for w in workers.values() for f in w.parent.glob('*') if f.is_file() and f.suffix in ['.dll','.json']}
env=os.environ.copy();env.update(DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0')
dotnet=str(root/'artifacts/dotnet-sdk-10.0.403/dotnet.exe')
manifest=dict(method=__doc__,forks=a.forks,hashes=hashes,complete=False,settings=dict(samples=5,sampleMs=150,warmupMs=500,scenes=['blend','curve-changing','chinese-changing'],sizes=['256x256','1024x768'],tieredCompilation=False,readyToRun=False))
save(a.output/'manifest.json',manifest);rows=[]
for fork in range(a.forks):
    order=list(workers);order=order[fork%len(order):]+order[:fork%len(order)]
    for version in order:
        dest=a.output/f'{version}-{fork}';dest.mkdir()
        cmd=[dotnet,str(workers[version]),'--engine','original' if version=='original' else 'simd','--fork',str(fork),'--out',str(dest),'--font',str(root/'baseline/fonts/ZCOOLXiaoWei-Regular.ttf'),'--scenes','blend,curve-changing,chinese-changing','--sizes','256x256,1024x768','--samples','5','--sample-ms','150','--warmup-ms','500']
        if version=='disabled':cmd+=['--disable-ordered']
        with (dest/'stdout.txt').open('w',encoding='utf-8') as log:subprocess.run(cmd,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=240)
        result=json.loads((dest/'results.json').read_text(encoding='utf-8-sig'))
        for row in result['Workloads']:
            samples=row['Samples'];ops=sum(s['Operations'] for s in samples);ms=sum(s['TotalMilliseconds'] for s in samples);cpu=sum(s['CpuMilliseconds'] for s in samples)
            rows.append(dict(version=version,fork=fork,scene=row['Scene'],width=row['Width'],height=row['Height'],milliseconds=ms/ops,cpuMilliseconds=cpu/ops,allocation=sum(s['AllocatedBytes'] for s in samples)/ops,workingSetMiB=row['AfterGc']['WorkingSetBytes']/1048576,privateMiB=row['AfterGc']['PrivateBytes']/1048576,allowedError=row['AllowedPixelError']))
        save(a.output/'runs.json',rows);print(version,fork,'done',flush=True)
summary=[]
for scene in ['blend','curve-changing','chinese-changing']:
    for w,h in [(256,256),(1024,768)]:
        entry=dict(scene=scene,width=w,height=h)
        for v in workers:
            group=[r for r in rows if (r['version'],r['scene'],r['width'],r['height'])==(v,scene,w,h)]
            entry[v]={k:statistics.median(r[k] for r in group) for k in ['milliseconds','cpuMilliseconds','allocation','workingSetMiB','privateMiB']}
        stem=f'{scene}-{w}x{h}.rgba';blobs={v:[(a.output/f'{v}-{f}'/stem).read_bytes() for f in range(a.forks)] for v in workers}
        if any(any(b!=bs[0] for b in bs) for bs in blobs.values()):raise ValueError('Unstable validation pixels')
        if blobs['disabled'][0]!=blobs['ordered'][0]:raise ValueError('Ordered batch changed pixels')
        if a.before and blobs['before'][0]!=blobs['ordered'][0]:raise ValueError('Product optimization changed fixed-pose pixels')
        maximum=max(abs(x-y) for x,y in zip(blobs['original'][0],blobs['ordered'][0]));limit=next(r['allowedError'] for r in rows if r['scene']==scene)
        entry.update(maximumError=maximum,allowedError=limit,nativeQualityPassed=maximum<=limit,orderedOverDisabled=entry['ordered']['milliseconds']/entry['disabled']['milliseconds'],orderedOverOriginal=entry['ordered']['milliseconds']/entry['original']['milliseconds']);summary.append(entry)
for name,digest in hashes.items():
    if hashlib.sha256(pathlib.Path(name).read_bytes()).hexdigest()!=digest:raise ValueError('Measured binary changed')
save(a.output/'summary.json',summary);manifest['complete']=True;save(a.output/'manifest.json',manifest);print(json.dumps(summary,indent=2),flush=True)
