"""Serial three-fork comparison with sixteen simultaneously live RGBA images.

Includes awkward pool sizes and 1080p. GC heap, working/private set and release
snapshots are distinct measurements. Native allocations are included in process
memory, while the managed allocation counter excludes them.
"""
import argparse,hashlib,json,statistics,subprocess
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output',type=Path,required=True)
parser.add_argument('--before',type=Path,required=True)
parser.add_argument('--current',type=Path,required=True)
parser.add_argument('--original',type=Path,required=True)
parser.add_argument('--fixtures',type=Path,required=True)
args=parser.parse_args()
args.output.mkdir(parents=True,exist_ok=False)
workers={'original':args.original.resolve(),'before':args.before.resolve(),'current':args.current.resolve()}
files={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for directory in {p.parent for p in workers.values()} for p in directory.rglob('*') if p.is_file() and p.suffix in ('.dll','.json','.exe')}
def save(name,value):(args.output/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
save('manifest.json',dict(method=__doc__,workerSha256=files,complete=False))
rows=[]
cases=[(256,256,False),(1024,768,False),(1025,1025,False),(1920,1080,False),(1024,768,True)]
for fork in range(3):
    order=list(workers);order=order[fork:]+order[:fork]
    for width,height,mixed in cases:
        for variant in order:
            destination=args.output/f'{variant}-{width}x{height}{"-mixed" if mixed else ""}-{fork}.json'
            command=['dotnet',str(workers[variant]),str(args.fixtures/f'input-{width}x{height}.png'),str(width),str(height),'16',str(destination.resolve())]
            if mixed:command.extend([str(args.fixtures/'input-1025x1025.png'),'1025','1025'])
            subprocess.run(command,check=True)
            row=json.loads(destination.read_text(encoding='utf-8'));row.update(variant=variant,fork=fork)
            rows.append(row);save('runs.json',rows)
            print(variant,width,height,fork,'complete',flush=True)
summary=[]
for width,height,mixed in cases:
    row=dict(width=width,height=height,count=16,mixedSize=mixed)
    group=[r for r in rows if (r['width'],r['height'],r['mixedSize'])==(width,height,mixed)]
    assert len({r['pixelSha256'] for r in group})==1,'Different decoded pixels'
    for variant in workers:
        samples=[r for r in group if r['variant']==variant]
        row[variant]={phase:{key.removesuffix('Bytes')+'MiB':statistics.median(r[phase][key] for r in samples)/1048576 for key in ['managedLiveBytes','workingSetBytes','privateBytes']} for phase in ['before','held','released']}
        row[variant]['allocatedMiB']=statistics.median(r['allocationBytes'] for r in samples)/1048576
        row[variant]['decodeMilliseconds']=statistics.median(r['decodeMilliseconds'] for r in samples)
    summary.append(row)
for name,digest in files.items():assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==digest,'Measured worker changed'
save('summary.json',summary);save('manifest.json',dict(method=__doc__,workerSha256=files,complete=True))
