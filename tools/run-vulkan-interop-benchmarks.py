"""Serial same-Vulkan-device external-target workloads, completed RGBA readback every frame.

Whole-process CPU comes from the worker; Windows WS/private memory is sampled
every 10ms only during the measurement marker. Native managed allocations are
not native-heap/driver allocations. These are micro-scenes, not Avalonia pages.
"""
import argparse,ctypes,hashlib,json,math,os,platform,statistics,subprocess,time
from pathlib import Path
from ctypes import wintypes
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--paced',action='store_true');a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh evidence directory.')
if os.name!='nt':raise SystemExit('The process memory sampler requires Windows.')
a.output.mkdir(parents=True)
def save(path,data):path.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
class Counters(ctypes.Structure):_fields_=[('cb',wintypes.DWORD),('faults',wintypes.DWORD)]+[(name,ctypes.c_size_t) for name in ['peakWs','ws','peakPaged','paged','peakNonPaged','nonPaged','pagefile','peakPagefile','private']]
kernel=ctypes.WinDLL('kernel32',use_last_error=True);psapi=ctypes.WinDLL('psapi',use_last_error=True);kernel.OpenProcess.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD];kernel.OpenProcess.restype=wintypes.HANDLE;kernel.CloseHandle.argtypes=[wintypes.HANDLE];psapi.GetProcessMemoryInfo.argtypes=[wintypes.HANDLE,ctypes.POINTER(Counters),wintypes.DWORD];psapi.GetProcessMemoryInfo.restype=wintypes.BOOL
workers={v:Path(f'benchmarks/VulkanInterop/{v}/bin/Release/net10.0/SimdSkia.GpuContext.Tests.dll').resolve() for v in ['Original','Managed']};hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for worker in workers.values() for f in worker.parent.glob('*') if f.is_file()};manifest=dict(method=__doc__,platform=platform.platform(),forks=3,warmupSeconds=1,measureSeconds=2,mode='paced60' if a.paced else 'throughput',hashes=hashes,complete=False);save(a.output/'manifest.json',manifest);runs=[]
for fork in range(3):
 for scene in ['rectangles','paths','gradient','image']:
  for variant in (['Original','Managed','Reference'] if fork%2==0 else ['Reference','Managed','Original']):
   dest=a.output/f'{scene}-{variant}-{fork}';dest.mkdir();samples=[];log=(a.output/(dest.name+'.log')).open('w',encoding='utf-8');process=subprocess.Popen(['dotnet',str(workers['Managed' if variant=='Reference' else variant]),str(dest),scene]+(['--paced'] if a.paced else [])+(['--cpu-bridge'] if variant=='Reference' else []),stdout=log,stderr=subprocess.STDOUT);handle=kernel.OpenProcess(0x410,False,process.pid)
   if not handle:process.terminate();raise ctypes.WinError(ctypes.get_last_error())
   start=time.monotonic()
   try:
    while process.poll() is None:
     if (dest/'measuring.marker').exists():
      memory=Counters();memory.cb=ctypes.sizeof(memory)
      if psapi.GetProcessMemoryInfo(handle,ctypes.byref(memory),memory.cb):samples.append(dict(time=time.monotonic()-start,ws=memory.ws,private=memory.private))
     if time.monotonic()-start>90:process.kill();raise TimeoutError('GPU worker timed out.')
     time.sleep(.01)
   finally:kernel.CloseHandle(handle);log.close()
   if process.returncode:raise RuntimeError(f'{dest.name} failed; inspect its log.')
   if len(samples)<10:raise RuntimeError('Insufficient steady-state process samples.')
   row=json.loads((dest/'result.json').read_text());row.update(variant=variant,fork=fork,sampledMedianWorkingSetMiB=statistics.median(r['ws'] for r in samples)/1048576,sampledPeakWorkingSetMiB=max(r['ws'] for r in samples)/1048576,sampledMedianPrivateMiB=statistics.median(r['private'] for r in samples)/1048576,sampledPeakPrivateMiB=max(r['private'] for r in samples)/1048576);save(dest/'memory.json',samples);runs.append(row);save(a.output/'runs.json',runs);print(f"{scene} {variant} #{fork}: {row['msPerFrame']:.4f}ms completed, CPU {row['cpuPercentOfMachine']:.2f}%, WS {row['sampledMedianWorkingSetMiB']:.2f}MiB",flush=True)
summary=[]
for scene in ['rectangles','paths','gradient','image']:
 row=dict(scene=scene)
 for variant in ['Original','Managed','Reference']:
  group=[r for r in runs if r['scene']==scene and r['variant']==variant];row[variant]={key:statistics.median(r[key] for r in group) for key in ['msPerFrame','activeMsPerFrame','framesPerSecond','drawCallsMsPerFrame','completeReadbackMsPerFrame','cpuMsPerFrame','cpuPercentOfMachine','allocatedBytesPerFrame','sampledMedianWorkingSetMiB','sampledPeakWorkingSetMiB','sampledMedianPrivateMiB','sampledPeakPrivateMiB']}
  data=[(a.output/f'{scene}-{variant}-{fork}/pixels.bin').read_bytes() for fork in range(3)]
  if any(x!=data[0] for x in data):raise RuntimeError('Unstable rendering.')
 x=(a.output/f'{scene}-Original-0/pixels.bin').read_bytes();y=(a.output/f'{scene}-Managed-0/pixels.bin').read_bytes();errors=[abs(u-v) for u,v in zip(x,y)];row['quality']=dict(maximum=max(errors),rmse=math.sqrt(sum(d*d for d in errors)/len(errors)));row['quality']['passed']=row['quality']['maximum']<=3 and row['quality']['rmse']<=.75;row['managedOverOriginal']=row['Managed']['msPerFrame']/row['Original']['msPerFrame'];summary.append(row)
for path,digest in hashes.items():
 if hashlib.sha256(Path(path).read_bytes()).hexdigest()!=digest:raise RuntimeError('Frozen worker changed during measurement.')

for scene in ['rectangles','paths','gradient','image']:
 if (a.output/f'{scene}-Managed-0/pixels.bin').read_bytes()!=(a.output/f'{scene}-Reference-0/pixels.bin').read_bytes():raise RuntimeError('Device copy changed pixels versus the reference CPU bridge.')
save(a.output/'summary.json',summary);manifest['complete']=True;save(a.output/'manifest.json',manifest)
