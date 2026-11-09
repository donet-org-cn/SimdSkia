"""Install and verify the Mobile.Android sample on an explicitly selected USB device."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import time
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--adb', required=True, type=Path)
parser.add_argument('--serial', required=True)
parser.add_argument('--apk', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
package = 'org.simdskia.androidsample'
command = [str(args.adb.resolve()), '-s', args.serial]

def adb(*arguments, timeout=60):
    result = subprocess.run(command + list(arguments), capture_output=True, timeout=timeout)
    if result.returncode:
        raise RuntimeError(result.stdout.decode('utf-8', errors='replace')
                           + result.stderr.decode('utf-8', errors='replace'))
    return result.stdout

with zipfile.ZipFile(args.apk) as archive:
    forbidden = [name for name in archive.namelist()
                 if any(word in name.lower() for word in ['skiasharp', 'harfbuzz', 'avalonia.skia', 'mono.cecil'])]
    assert not forbidden, forbidden
    native = [name for name in archive.namelist() if name.endswith('.so')]

print(adb('install', '--no-incremental', '-r', str(args.apk.resolve()), timeout=180).decode(), flush=True)
activity = adb('shell', 'cmd', 'package', 'resolve-activity', '--brief',
               '-a', 'android.intent.action.MAIN', '-c', 'android.intent.category.LAUNCHER', '-p', package).decode()
component = next(line.strip() for line in activity.splitlines() if line.strip().startswith(package + '/'))
remote = f'/sdcard/Android/data/{package}/files'
adb('shell', 'am', 'force-stop', package)
adb('shell', 'rm', '-f', remote + '/diagnostics.txt', remote + '/smoke.log')
launch = adb('shell', 'am', 'start', '-W', '-n', component)
(args.output / 'launch.txt').write_bytes(launch)
print(launch.decode(), flush=True)

diagnostics = b''
for attempt in range(20):
    time.sleep(1)
    pid = adb('shell', 'pidof', package).decode().strip()
    try:
        diagnostics = adb('shell', 'cat', remote + '/diagnostics.txt')
    except RuntimeError:
        continue
    values = dict(line.split('=', 1) for line in diagnostics.decode().splitlines() if '=' in line)
    if int(values.get('Frames', '0')) > 0:
        break
else:
    raise RuntimeError('The sample did not report a presented SimdSkia frame.')

logs = adb('logcat', '--pid=' + pid, '-d', '-t', '1000')
(args.output / 'app-logcat.txt').write_bytes(logs)
assert b'FATAL EXCEPTION' not in logs and b'UNHANDLED EXCEPTION' not in logs
(args.output / 'diagnostics.txt').write_bytes(diagnostics)
(args.output / 'smoke.log').write_bytes(adb('shell', 'cat', remote + '/smoke.log'))
frame = adb('exec-out', 'screencap', '-p')
assert frame.startswith(b'\x89PNG\r\n\x1a\n')
(args.output / 'screen.png').write_bytes(frame)
properties = {}
for name in ['ro.product.model', 'ro.build.version.release', 'ro.build.version.sdk', 'ro.product.cpu.abilist']:
    properties[name] = adb('shell', 'getprop', name).decode().strip()
summary = dict(passed=True, device=properties, apkBytes=args.apk.stat().st_size,
               apkSha256=hashlib.sha256(args.apk.read_bytes()).hexdigest(), forbiddenAssets=forbidden,
               nativeAssets=native, diagnostics=values, activity=component)
(args.output / 'result.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
print('PASS real Android launch, managed presented frames, ARM capabilities and APK dependency checks', flush=True)
