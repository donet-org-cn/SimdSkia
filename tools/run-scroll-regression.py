"""Serial portable Native AOT paced-scroll regression against fixed binaries.

Includes actual UI/window work while yielding; draw timing excludes Task.Delay.
The 0.1.2 binary is an unchanged historical control, not a rebuilt substitute.
"""
import argparse,hashlib,importlib.util,json,os,pathlib,statistics

ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('aot_runner',ROOT/'tools/run-avalonia-aot.py')
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--output',type=pathlib.Path,required=True)
p.add_argument('--before',type=pathlib.Path,required=True)
p.add_argument('--after',type=pathlib.Path)
p.add_argument('--stable',type=pathlib.Path,required=True)
p.add_argument('--original',type=pathlib.Path,required=True)
p.add_argument('--forks',type=int,default=3)
p.add_argument('--warmup',type=float,default=2)
p.add_argument('--seconds',type=float,default=5)
a=p.parse_args()
if os.environ.get('SIMD_SKIA_PROFILE')=='1':raise ValueError('Disable profiling during formal timings')
if not 1<=a.forks<=20 or not .1<=a.seconds<=120 or not 0<=a.warmup<=30:raise ValueError('Invalid measurement settings')
a.output.mkdir(parents=True,exist_ok=False)
variants={'original-full':a.original.resolve(),'stable-0.1.2':a.stable.resolve(),'before':a.before.resolve()}
if a.after:variants['after']=a.after.resolve()
hashes={str(path):runner.native_pe(path) for path in variants.values()}
runner.save_json(a.output/'manifest.json',dict(method=__doc__,forks=a.forks,seconds=a.seconds,warmup=a.warmup,binaries=hashes,complete=False))
rows=[];ram=runner.physical_memory()
for fork in range(a.forks):
    order=list(variants);order=order[fork%len(order):]+order[:fork%len(order)]
    for variant in order:
        dest=(a.output/f'{variant}-{fork}').resolve()
        row=runner.measure_process(variants[variant],['--hidden','--scene','scroll','--scale','1','--mode','paced','--warmup',str(a.warmup),'--seconds',str(a.seconds),'--full-opacity','--output',str(dest)],dest,ram)
        if row['avx2'] or row['vectorBits']!=128:raise ValueError('Expected portable x64 AOT')
        if (row['width'],row['height'],row['fontSha256'])!=(1000,720,rows[0]['fontSha256'] if rows else row['fontSha256']):raise ValueError('Frame dimensions/font changed')
        row.update(variant=variant,fork=fork);rows.append(row)
        runner.save_json(a.output/'runs.json',rows)
        print(f"{variant} #{fork}: {row['drawMsPerFrame']:.4f}ms, CPU {row['cpuMsPerFrame']:.3f}ms/frame, alloc {row['allocatedBytesPerFrame']/1024:.2f}KiB",flush=True)
summary={}
for variant in variants:
    group=[r for r in rows if r['variant']==variant]
    summary[variant]={k:statistics.median(r[k] for r in group) for k in ['drawMsPerFrame','p95Ms','cpuMsPerFrame','cpuPercentMachine','allocatedBytesPerFrame','sampledMedianWorkingSetMiB','sampledMedianPrivateMiB','gc0','gc1','gc2','missedDeadlines']}
    captures=[(a.output/f'{variant}-{i}'/'scene.rgba').read_bytes() for i in range(a.forks)]
    if any(c!=captures[0] for c in captures):raise ValueError('Unstable fixed-pose pixels: '+variant)
quality=[]
for variant in variants:
    if variant=='original-full':continue
    quality.append(dict(variant=variant,**runner.quality_compare(a.output/f'{variant}-0/scene.rgba',a.output/'original-full-0/scene.rgba',1000,720)))
if a.after and (a.output/'after-0/scene.rgba').read_bytes()!=(a.output/'before-0/scene.rgba').read_bytes():raise ValueError('Product optimization changed fixed-pose pixels')
for name,digest in hashes.items():
    if hashlib.sha256(pathlib.Path(name).read_bytes()).hexdigest()!=digest:raise ValueError('Measured binary changed')
runner.save_json(a.output/'summary.json',summary);runner.save_json(a.output/'quality.json',quality)
runner.save_json(a.output/'manifest.json',dict(method=__doc__,forks=a.forks,seconds=a.seconds,warmup=a.warmup,binaries=hashes,complete=True))
print(json.dumps(summary,indent=2),flush=True)
