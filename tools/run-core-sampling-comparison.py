"""CPU-only before/after/original/scalar sampling comparison; processes run serially."""
import argparse,hashlib,json,math,os,statistics,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output',type=Path,default=ROOT/'artifacts/core-round8/final')
parser.add_argument('--before-directory',type=Path,default=ROOT/'artifacts/core-round8/before-sampling')
parser.add_argument('--allow-pixel-changes',action='store_true',help='Only for a verified behavior correction; after/scalar identity stays mandatory.')
parser.add_argument('--require-native-quality',action='store_true')
options=parser.parse_args()
out=options.output.resolve()
if out.exists():raise RuntimeError('Choose a fresh output directory')
out.mkdir(parents=True)
programs={'before':options.before_directory.resolve()/'Sampling.dll',
          'after':ROOT/'benchmarks/Sampling/bin/Release/net8.0/Sampling.dll',
          'original':ROOT/'benchmarks/Sampling/bin/Release/net8.0/Sampling.dll',
          'scalar':ROOT/'benchmarks/Sampling/bin/Release/net8.0/Sampling.dll'}
files={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for exe in programs.values() for p in exe.parent.glob('*.dll')}
env=dict(os.environ,DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0')
rows=[];engines=list(programs)
for fork in range(3):
 for name in engines[fork:]+engines[:fork]:
  directory=out/f'{name}-{fork+1}'
  with (out/f'{name}-{fork+1}.log').open('wb') as log:
   subprocess.run(['dotnet',str(programs[name]),'--engine',name if name in ('original','scalar') else 'managed','--output',str(directory)],cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=180)
  data=json.loads((directory/'results.json').read_text())
  for row in data['results']:
   row.update(engine=name,fork=fork+1,runtime=data['runtime'],processors=data['processors']);rows.append(row)
  print(f'PASS {name} process {fork+1}/3',flush=True)
quality=[];summary=[]
for kind in ['straight-opaque','straight-alpha','pma-alpha','opaque']:
 for w,h in [(320,240),(1024,768)]:
  for fork in range(1,4):
   pixels={name:(out/f'{name}-{fork}'/f'{kind}-{w}x{h}.rgba').read_bytes() for name in engines}
   assert pixels['after']==pixels['scalar'],(kind,w,h,fork)
   same=pixels['before']==pixels['after']
   if not options.allow_pixel_changes:assert same,(kind,w,h,fork)
   differences=[abs(a-b) for a,b in zip(pixels['after'],pixels['original'])]
   quality.append({'kind':kind,'width':w,'height':h,'fork':fork,'beforeAfterScalarExactlyEqual':same,'beforeAfterExactlyEqual':same,'afterScalarExactlyEqual':True,'nativeMaximum':max(differences),'nativeRmse':math.sqrt(sum(x*x for x in differences)/len(differences)),'maximumThreshold':2,'rmseThreshold':.5,'passed':max(differences)<=2 and sum(x*x for x in differences)/len(differences)<=.25})
  for name in engines:
   group=[r for r in rows if (r['kind'],r['width'],r['height'],r['engine'])==(kind,w,h,name)]
   summary.append({'kind':kind,'width':w,'height':h,'engine':name,'ms':statistics.median(statistics.median(s['millisecondsPerFrame'] for s in r['samples']) for r in group),'cpuMs':statistics.median(statistics.median(s['cpuMillisecondsPerFrame'] for s in r['samples']) for r in group),'bytesPerFrame':statistics.median(statistics.median(s['allocatedBytesPerFrame'] for s in r['samples']) for r in group),'workingSetMiB':statistics.median(r['workingSetBytes']/1048576 for r in group),'privateMiB':statistics.median(r['privateBytes']/1048576 for r in group)})
for path,digest in files.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,path
(out/'results.json').write_text(json.dumps({'scope':'CPU only; linear-filtered 512x384 image; same whole-frame clear; 3 processes and 7 timing batches per variant. After/scalar identity mandatory; before pixel changes allowed only for explicit behavior corrections. Quality gates remain max<=2/RMSE<=0.5. Whole-process memory, not production application memory.','pixelChangesAllowed':options.allow_pixel_changes,'runtimeFiles':files,'runs':rows,'summary':summary,'quality':quality},indent=2)+'\n')
print('PASS exact after/scalar pixels; native gates passed '+str(sum(q['passed'] for q in quality))+'/'+str(len(quality)),flush=True)
if options.require_native_quality and not all(q['passed'] for q in quality):raise SystemExit(2)
