import json,re,subprocess,sys,time
from pathlib import Path
root=Path('artifacts/android-issue-1/final-device');adb=str(Path('artifacts/android-sdk/platform-tools/adb.exe').resolve());base=[adb,'-s',sys.argv[1]]
package='org.simdskia.androidsample';remote=f'/sdcard/Android/data/{package}/files'
def run(*tail):return subprocess.run(base+list(tail),capture_output=True,check=True,timeout=45).stdout
run('shell','am','start','-W','-n',json.loads((root/'result.json').read_text())['activity']);time.sleep(1)
run('shell','input','tap','720','955');time.sleep(0.5)
run('shell','input','tap','720','730');time.sleep(1)
run('shell','input','keyevent','KEYCODE_MOVE_END')
run('shell','input','text','USB123')
time.sleep(0.5)
log=run('shell','cat',remote+'/smoke.log').decode('utf-8');assert 'Click 1' in log and 'USB123' in log,log
run('shell','input','keyevent','KEYCODE_HOME');time.sleep(2)
log=run('shell','cat',remote+'/smoke.log').decode('utf-8');assert 'Activity paused' in log
paused=int(re.findall(r'Activity paused frames=(\d+)',log)[-1])
component=json.loads((root/'result.json').read_text())['activity']
run('shell','am','start','-W','-n',component);time.sleep(4)
log=run('shell','cat',remote+'/smoke.log').decode('utf-8')
(root/'smoke.log').write_text(log,encoding='utf-8')
diag=run('shell','cat',remote+'/diagnostics.txt');(root/'diagnostics-after-resume.txt').write_bytes(diag)
frames=int(dict(line.split('=',1) for line in diag.decode().splitlines())['Frames']);assert frames>paused
(root/'after-resume.png').write_bytes(run('exec-out','screencap','-p'));time.sleep(1)
(root/'animation-next.png').write_bytes(run('exec-out','screencap','-p'))
sys.path.insert(0,str(Path('artifacts/report-python').resolve()))
from PIL import Image,ImageChops
with Image.open(root/'after-resume.png') as a,Image.open(root/'animation-next.png') as b:
    assert a.size==b.size==(1440,3200)
    # This ROI was observed on the 24122RKC7C screenshot. It contains only the progress bar.
    difference=ImageChops.difference(a.crop((90,1220,1350,1270)),b.crop((90,1220,1350,1270)))
    changed=sum(any(value for value in pixel) for pixel in difference.convert('RGB').getdata())
    assert changed>100,changed
pid=run('shell','pidof',package).decode().strip();logs=run('logcat','--pid='+pid,'-d','-t','1000');(root/'app-logcat-after.txt').write_bytes(logs)
assert b'FATAL EXCEPTION' not in logs and b'UNHANDLED EXCEPTION' not in logs
result=dict(buttonClick=True,inputText=re.findall(r'TextChanged (.+) frames=',log)[-1],backgroundResume=True,
            pausedFrames=paused,resumedFrames=frames,animatedProgressChangedPixels=changed,
            screenshotSize=[1440,3200],rendererErrors=False)
(root/'interaction.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))