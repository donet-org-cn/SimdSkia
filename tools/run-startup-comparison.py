"""Observe real on-screen pixels without sending WM_PAINT/PrintWindow to workers.

The small blue border is identical in every engine and proves the client frame
has reached the desktop. Short-lived visible windows are required for this test.
"""
import argparse,ctypes,hashlib,json,os,statistics,subprocess,time
from ctypes import wintypes as W
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--output',required=True,type=Path)
p.add_argument('--managed',type=Path,default=Path('tests/Startup/Managed/bin/Release/net10.0/win-x64/Startup.Managed.exe'))
p.add_argument('--reference',type=Path,default=Path('tests/Startup/Reference/bin/Release/net10.0/win-x64/Startup.Reference.exe'))
p.add_argument('--before',type=Path)
p.add_argument('--font',type=Path)
p.add_argument('--include-preparation-disabled',action='store_true',help='Add the same managed executable with background preparation disabled; all other settings remain identical.')
p.add_argument('--runs',type=int,default=3)
p.add_argument('--backends',nargs='+',default=['cpu'],choices=['cpu','direct2d','vulkan'])
args=p.parse_args()
if args.runs<1:p.error('--runs must be positive')
if os.name!='nt':raise SystemExit('Actual Windows desktop required.')
if args.output.exists():raise SystemExit('Choose a fresh output directory.')
args.output.mkdir(parents=True)
user=ctypes.WinDLL('user32',use_last_error=True);gdi=ctypes.WinDLL('gdi32',use_last_error=True)
psapi=ctypes.WinDLL('psapi',use_last_error=True)
psapi.EnumProcessModulesEx.argtypes=[W.HANDLE,ctypes.POINTER(W.HMODULE),W.DWORD,ctypes.POINTER(W.DWORD),W.DWORD]
psapi.EnumProcessModulesEx.restype=W.BOOL
psapi.GetModuleBaseNameW.argtypes=[W.HANDLE,W.HMODULE,W.LPWSTR,W.DWORD]
psapi.GetModuleBaseNameW.restype=W.DWORD
def loaded_modules(process):
    # Query only after the first content timestamp. RuntimeFeature=false alone
    # also occurs when an AOT project's intermediate IL is run under dotnet.
    modules=(W.HMODULE*1024)();needed=W.DWORD();handle=W.HANDLE(int(process._handle))
    if not psapi.EnumProcessModulesEx(handle,modules,ctypes.sizeof(modules),ctypes.byref(needed),3):raise ctypes.WinError(ctypes.get_last_error())
    if needed.value>ctypes.sizeof(modules):raise RuntimeError('Module snapshot exceeds its buffer.')
    names=[]
    for module in modules[:needed.value//ctypes.sizeof(W.HMODULE)]:
        name=ctypes.create_unicode_buffer(1024)
        if not psapi.GetModuleBaseNameW(handle,module,name,len(name)):raise ctypes.WinError(ctypes.get_last_error())
        names.append(name.value)
    return names
user.SetProcessDPIAware()
user.GetDC.argtypes=[W.HWND];user.GetDC.restype=W.HDC
user.ReleaseDC.argtypes=[W.HWND,W.HDC]
user.IsWindowVisible.argtypes=[W.HWND];user.IsWindowVisible.restype=W.BOOL
user.GetWindowThreadProcessId.argtypes=[W.HWND,ctypes.POINTER(W.DWORD)]
user.GetClientRect.argtypes=[W.HWND,ctypes.POINTER(W.RECT)]
user.ClientToScreen.argtypes=[W.HWND,ctypes.POINTER(W.POINT)]
class BitmapInfoHeader(ctypes.Structure):
    _fields_=[('size',W.DWORD),('width',W.LONG),('height',W.LONG),('planes',W.WORD),('bits',W.WORD),('compression',W.DWORD),('imageSize',W.DWORD),('xppm',W.LONG),('yppm',W.LONG),('used',W.DWORD),('important',W.DWORD)]
gdi.CreateCompatibleDC.argtypes=[W.HDC];gdi.CreateCompatibleDC.restype=W.HDC
gdi.CreateDIBSection.argtypes=[W.HDC,ctypes.POINTER(BitmapInfoHeader),W.UINT,ctypes.POINTER(ctypes.c_void_p),W.HANDLE,W.DWORD];gdi.CreateDIBSection.restype=W.HANDLE
gdi.SelectObject.argtypes=[W.HDC,W.HANDLE];gdi.SelectObject.restype=W.HANDLE
gdi.DeleteObject.argtypes=[W.HANDLE];gdi.DeleteDC.argtypes=[W.HDC]
gdi.BitBlt.argtypes=[W.HDC,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_int,W.HDC,ctypes.c_int,ctypes.c_int,W.DWORD];gdi.BitBlt.restype=W.BOOL
class ScreenReader:
    # One desktop read per sample. Four GetPixel calls serialized four compositor
    # readbacks (~125 ms here), hiding shorter intervals and exaggerating durations.
    def __init__(self,screen,width,height):
        self.width=width;self.height=height;self.dc=gdi.CreateCompatibleDC(screen)
        header=BitmapInfoHeader(ctypes.sizeof(BitmapInfoHeader),width,-height,1,32,0,0,0,0,0,0)
        bits=ctypes.c_void_p();self.bitmap=gdi.CreateDIBSection(screen,ctypes.byref(header),0,ctypes.byref(bits),None,0)
        if not self.dc or not self.bitmap:raise ctypes.WinError(ctypes.get_last_error())
        self.previous=gdi.SelectObject(self.dc,self.bitmap);self.data=ctypes.cast(bits,ctypes.POINTER(ctypes.c_uint32))
    def read(self,screen,x,y):
        if not gdi.BitBlt(self.dc,0,0,self.width,self.height,screen,x,y,0x00cc0020):raise ctypes.WinError(ctypes.get_last_error())
        gdi.GdiFlush()
        return [((v>>16)&255,(v>>8)&255,v&255) for v in [self.data[y*self.width+x] for x,y in [(3,3),(self.width-4,3),(3,self.height-4),(self.width-4,self.height-4)]]]
    def close(self):
        gdi.SelectObject(self.dc,self.previous);gdi.DeleteObject(self.bitmap);gdi.DeleteDC(self.dc)
callback=ctypes.WINFUNCTYPE(W.BOOL,W.HWND,W.LPARAM)
user.EnumWindows.argtypes=[callback,W.LPARAM]
def find_window(pid):
    found=[]
    @callback
    def visit(hwnd,_):
        owner=W.DWORD();user.GetWindowThreadProcessId(hwnd,ctypes.byref(owner))
        if owner.value==pid and user.IsWindowVisible(hwnd):found.append(hwnd);return False
        return True
    user.EnumWindows(visit,0)
    return found[0] if found else None

variants=[('after-'+b,args.managed,b) for b in args.backends]+[('reference',args.reference,'cpu')]
if args.before:variants=[('before-'+b,args.before,b) for b in args.backends]+variants
if args.include_preparation_disabled:variants += [('disabled-'+b,args.managed,b) for b in args.backends]
files=sorted({path.resolve() for _,exe,_ in variants for path in [exe,*exe.parent.glob('*.dll'),*exe.parent.glob('*.runtimeconfig.json'),*exe.parent.glob('*.deps.json')]})
hashes={str(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in files}
(args.output/'manifest.json').write_text(json.dumps({'runtimeFileSha256':hashes,'includePreparationDisabled':args.include_preparation_disabled,'font':str(args.font) if args.font else 'Microsoft YaHei UI','runtimeEnvironment':{key:os.environ.get(key) for key in ['DOTNET_TieredCompilation','DOTNET_TieredPGO','DOTNET_EnableHWIntrinsic']}},indent=2)+'\n',encoding='utf-8')
rows=[]
for run in range(args.runs):
    for name,exe,backend in variants[run%len(variants):]+variants[:run%len(variants)]:
        dest=args.output/f'{name}-{run+1}';dest.mkdir()
        cmd=[str(exe.resolve()),'--output',str(dest.resolve()),'--backend',backend]
        if name.startswith('disabled-'):cmd+=['--no-preparation']
        if args.font:cmd+=['--font',str(args.font.resolve())]
        with (dest/'process.log').open('wb') as log:
            start=time.perf_counter();process=subprocess.Popen(cmd,stdout=log,stderr=subprocess.STDOUT,creationflags=0x08000000)
            hwnd=None;visible=None;content=None;samples=[];screen=user.GetDC(None);reader=None
            try:
                while process.poll() is None and time.perf_counter()-start<30:
                    now=time.perf_counter()
                    if hwnd is None:hwnd=find_window(process.pid)
                    if hwnd and user.IsWindowVisible(hwnd):
                        if visible is None:visible=now
                        rect=W.RECT();user.GetClientRect(hwnd,ctypes.byref(rect));origin=W.POINT(0,0);user.ClientToScreen(hwnd,ctypes.byref(origin))
                        if reader is None:reader=ScreenReader(screen,rect.right,rect.bottom)
                        if (rect.right,rect.bottom)!=(reader.width,reader.height):raise RuntimeError('Window resized during startup sampling.')
                        rgb=reader.read(screen,origin.x,origin.y)
                        captured=time.perf_counter()
                        match=all(all(abs(a-b)<=2 for a,b in zip(c,(18,52,86))) for c in rgb)
                        white=all(all(v>=245 for v in c) for c in rgb)
                        samples.append({'ms':(captured-start)*1000,'captureStartedMs':(now-start)*1000,'rgb':rgb,'content':match,'white':white})
                        if match and content is None:content=captured;modules=loaded_modules(process)
                    time.sleep(.001)
                if process.poll() is None:process.kill();raise RuntimeError('Startup timed out.')
                if process.returncode:raise RuntimeError(f'Worker failed: {dest}/process.log')
                if content is None:raise RuntimeError(f'No fully presented marker in {dest}; inspect desktop visibility and samples.')
            finally:
                if reader:reader.close()
                user.ReleaseDC(None,screen)
                (dest/'screen-samples.json').write_text(json.dumps(samples),encoding='utf-8')
        metrics=json.loads((dest/'metrics.json').read_text(encoding='utf-8'))
        coreclr=any(name.lower() in {'coreclr.dll','clrjit.dll','clr.dll','mscorwks.dll','mscorsvr.dll'} for name in modules)
        if metrics['nativeAot']==coreclr:raise RuntimeError('Runtime flag and loaded CLR modules disagree; do not label intermediate IL as Native AOT.')
        white_ms=sum(b['ms']-a['ms'] for a,b in zip(samples,samples[1:]) if a['white'] and not a['content'])
        row={'variant':name,'run':run+1,'visibleMs':(visible-start)*1000,'firstContentMs':(content-start)*1000,
             'visibleToContentMs':(content-visible)*1000,'observedWhiteMs':white_ms,'whiteSamples':sum(s['white'] for s in samples),
             'medianSampleGapMs':statistics.median(b['ms']-a['ms'] for a,b in zip(samples,samples[1:])),
             'maxSampleGapMs':max(b['ms']-a['ms'] for a,b in zip(samples,samples[1:])),'loadedModules':modules,'worker':metrics}
        rows.append(row);print(f"{name} #{run+1}: visible -> content {row['visibleToContentMs']:.1f}ms; white {white_ms:.1f}ms; launch -> content {row['firstContentMs']:.1f}ms",flush=True)
summary=[]
for name,_,_ in variants:
    group=[r for r in rows if r['variant']==name]
    summary.append({'variant':name,'runs':len(group),**{key:statistics.median(r[key] for r in group) for key in ['visibleMs','firstContentMs','visibleToContentMs','observedWhiteMs','whiteSamples','medianSampleGapMs','maxSampleGapMs']}})
for path,digest in hashes.items():
    if hashlib.sha256(Path(path).read_bytes()).hexdigest()!=digest:raise SystemExit('Runtime file changed during measurement.')
(args.output/'results.json').write_text(json.dumps({'scope':'Visible Windows desktop sampling; startup disk cache is not flushed; not a cold boot benchmark. Four corner markers detect a presented frame. White duration is a sampled estimate, not a continuous trace; readback and DWM animation affect latency. Screen sampling overhead is identical for all variants.','summary':summary,'runs':rows},indent=2)+'\n',encoding='utf-8')
