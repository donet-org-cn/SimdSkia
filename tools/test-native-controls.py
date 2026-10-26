"""Verify real glow-controls AOT window without adding test code to its EXE."""
import ctypes as C
from ctypes import wintypes as W
import importlib.util, subprocess, time, json, hashlib, io, zipfile, shutil
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('helper',ROOT/'tools/test-native-window-minimum.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
m.u.SetProcessDpiAwarenessContext(W.HANDLE(-4))
m.u.SendMessageW.argtypes=[W.HWND,W.UINT,W.WPARAM,W.LPARAM];m.u.SendMessageW.restype=W.LPARAM
out=ROOT/'artifacts/native-controls-aot';dest=out/'portable';dest.mkdir(exist_ok=True)
files=[]
for path in (out/'final').rglob('*'):
 if path.is_file() and path.suffix.lower()!='.pdb':
  target=dest/path.relative_to(out/'final');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target)
  files.append({'path':target.relative_to(dest).as_posix(),'bytes':target.stat().st_size,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
exe=dest/'NativeControls.exe';m.checks.verify_pe(exe)
def find(pid):
 found=[]
 @m.callback
 def visit(w,_):
  owner=W.DWORD();m.u.GetWindowThreadProcessId(w,C.byref(owner));name=C.create_unicode_buffer(256);m.u.GetClassNameW(w,name,256)
  if owner.value==pid and name.value=='SimdSkiaGlowControls':found.append(w)
  return True
 m.u.EnumWindows(visit,0);return found[0] if found else None
process=subprocess.Popen([str(exe)],creationflags=subprocess.CREATE_NO_WINDOW)
try:
 window=None
 for i in range(300):
  window=find(process.pid)
  if window:break
  assert process.poll() is None,'Application exited early';time.sleep(.02)
 assert window
 time.sleep(.7)
 def capture(name):
  image=Image.open(io.BytesIO(m.capture(window))).convert('RGB');image.save(out/(name+'.png'));return image
 def send(msg,value=0,param=0):return m.u.SendMessageW(window,msg,value,param)
 def click(x,y):
  send(0x200,0,x|(y<<16));send(0x201,1,x|(y<<16));send(0x202,0,x|(y<<16));time.sleep(.15)
 def key(value):send(0x100,value);send(0x101,value)
 first=capture('initial');time.sleep(.8);second=capture('animated')
 region=(36,270,first.width-36,292)
 assert first.crop(region).tobytes()!=second.crop(region).tobytes(),'Progress did not animate'
 click(100,374);paused=capture('paused');time.sleep(.6);paused2=capture('paused-stable')
 assert paused.crop(region).tobytes()==paused2.crop(region).tobytes(),'Pause did not freeze the progress/shimmer'
 def enter(text):
  click(80,160);send(0x102,1)
  for c in text:send(0x102,ord(c))
 def text_image(name):
  click(500,415);return capture(name).crop((48,144,400,177)).tobytes()
 enter('AB');ab=text_image('text-ab')
 enter('ACB');key(37);send(0x102,8);edited=text_image('text-delete')
 assert ab==edited,'Left/backspace editing result differs from AB'
 enter('中文输入');chinese=text_image('text-chinese');assert chinese!=ab
 click(100,374);a=capture('resumed');time.sleep(.5);b=capture('resumed-moving');assert a.crop(region).tobytes()!=b.crop(region).tobytes()
 # Button down is visually different; releasing outside cancels the activation.
 send(0x200,0,300|(374<<16));time.sleep(.1);hover=capture('hover')
 send(0x201,1,300|(374<<16));time.sleep(.15);down=capture('pressed');assert hover.crop((248,350,408,398)).tobytes()!=down.crop((248,350,408,398)).tobytes()
 send(0x202,0,500|(415<<16));time.sleep(.1)
 click(300,374);reset=capture('reset');assert reset.crop(region).tobytes()!=b.crop(region).tobytes()
 assert m.u.SetWindowPos(window,W.HWND(-1),80,80,900,620,0x40);time.sleep(.5);resized=capture('resized');assert resized.width>first.width
 loaded=m.modules(process)
 send(0x10);assert process.wait(timeout=10)==0
 result={'status':'PASS','exeBytes':exe.stat().st_size,'runtimeBytes':sum(f['bytes'] for f in files),'files':files,'loadedModules':loaded,
 'checks':['native GUI PE','no CLR/Skia/HarfBuzz/Avalonia','animated progress','pause freezes shimmer and progress','resume','reset','English editing left/backspace','Chinese character input','hover and pressed appearance','release outside cancels','resize','normal close'],
 'sourceSha256':{str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (ROOT/'examples/NativeControls').glob('*.cs')}}
 archive=out/'native-controls-aot.zip'
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
  for f in files:z.write(dest/f['path'],f['path'])
 with zipfile.ZipFile(archive) as z:assert z.testzip() is None
 result['zipBytes']=archive.stat().st_size
 (out/'results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print(json.dumps({k:result[k] for k in ['status','exeBytes','runtimeBytes','zipBytes','checks']},ensure_ascii=False))
finally:
 if process.poll() is None:process.kill();process.wait()
