"""Publish and externally verify the default Avalonia template with two backends.

Windows x64 only. No test instrumentation is compiled into either application.
The Inter font and Fluent theme are retained in both; SimdSkia is a public NuGet.
"""
import argparse
from collections import Counter
import ctypes as C
from ctypes import wintypes as W
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import platform
import struct
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
import zipfile
import zlib

ROOT = Path(__file__).resolve().parents[1]
EXAMPLES = ROOT / 'examples/DefaultAvaloniaAot'


def digest(blob):
    return hashlib.sha256(blob).hexdigest()


def png(path, bitmap):
    width, height = struct.unpack_from('<ii', bitmap, 18)
    assert height < 0
    height = -height
    bgra = bitmap[54:]
    rgb = bytearray(width * height * 3)
    rgb[0::3], rgb[1::3], rgb[2::3] = bgra[2::4], bgra[1::4], bgra[0::4]
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
    scanlines = b''.join(b'\0' + rgb[y*width*3:(y+1)*width*3] for y in range(height))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(scanlines, 9)) + chunk(b'IEND', b''))
    colors = Counter(bytes(rgb[i:i+3]) for i in range(0, len(rgb), 3))
    return dict(width=width, height=height, distinctColors=len(colors), nonBackgroundPixels=width*height-colors.most_common(1)[0][1], sha256=digest(path.read_bytes()))


def capture(m, window):
    # Capture the composited screen: a GPU swapchain need not appear in its GDI DC.
    rect = W.RECT()
    assert m.u.GetClientRect(window, C.byref(rect))
    width, height = rect.right, rect.bottom
    origin = W.POINT(0, 0)
    m.u.ClientToScreen.argtypes = [W.HWND, C.POINTER(W.POINT)]
    assert m.u.ClientToScreen(window, C.byref(origin))
    assert 0 < width < 4096 and 0 < height < 4096
    dc = m.u.GetDC(None)
    memory = m.g.CreateCompatibleDC(dc)
    header = struct.pack('<IiiHHIIiiII', 40, width, -height, 1, 32, 0, width*height*4, 0, 0, 0, 0)
    info = C.create_string_buffer(header)
    bits = C.c_void_p()
    bitmap = m.g.CreateDIBSection(dc, info, 0, C.byref(bits), None, 0)
    assert bitmap and bits.value
    previous = m.g.SelectObject(memory, bitmap)
    try:
        assert m.g.BitBlt(memory, 0, 0, width, height, dc, origin.x, origin.y, 0x40CC0020)
        pixels = bytearray(C.string_at(bits, width*height*4))
        pixels[3::4] = b'\xff' * (width*height)
        return struct.pack('<2sIII', b'BM', 54+len(pixels), 0, 54) + header + pixels
    finally:
        m.g.SelectObject(memory, previous)
        m.g.DeleteObject(bitmap)
        m.g.DeleteDC(memory)
        m.u.ReleaseDC(None, dc)


def inspect_application(label, publish, output):
    spec = importlib.util.spec_from_file_location('native_window_checks', ROOT/'tools/test-native-window-minimum.py')
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    m.u.SetProcessDpiAwarenessContext(W.HANDLE(-4))
    executable = publish/'DefaultAvalonia.exe'
    m.checks.verify_pe(executable)
    # Run exactly the release directory; do not fall back to dotnet-hosted IL.
    with (output/f'{label}.stdout.txt').open('w') as stdout, (output/f'{label}.stderr.txt').open('w') as stderr:
        process = subprocess.Popen([str(executable)], cwd=publish, stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            found = []
            @m.callback
            def visit(window, _):
                owner = W.DWORD()
                m.u.GetWindowThreadProcessId(window, C.byref(owner))
                title = C.create_unicode_buffer(512)
                m.u.GetWindowTextW(window, title, len(title))
                if owner.value == process.pid and m.u.IsWindowVisible(window) and title.value == 'DefaultAvalonia':
                    found.append(window)
                return True
            deadline = time.monotonic()+30
            while not found and process.poll() is None and time.monotonic() < deadline:
                m.u.EnumWindows(visit, 0)
                time.sleep(.05)
            assert found, f'{label}: no visible default window; exit={process.poll()}; see stderr'
            window = found[0]
            assert m.u.SetWindowPos(window, W.HWND(-1), 100, 100, 900, 600, 0x0040)
            time.sleep(2)
            screenshot = png(output/f'{label}.png', capture(m, window))
            assert screenshot['distinctColors'] > 10 and screenshot['nonBackgroundPixels'] > 50, f'{label}: blank client area'
            buffer = (W.HMODULE * 1024)()
            needed = W.DWORD()
            handle = W.HANDLE(int(process._handle))
            assert m.ps.EnumProcessModulesEx(handle, buffer, C.sizeof(buffer), C.byref(needed), 3)
            assert needed.value <= C.sizeof(buffer)
            modules = []
            for module in buffer[:needed.value//C.sizeof(W.HMODULE)]:
                name = C.create_unicode_buffer(1024)
                assert m.ps.GetModuleBaseNameW(handle, module, name, len(name))
                modules.append(name.value)
            lower = {n.lower() for n in modules}
            assert not lower.intersection({'coreclr.dll','clrjit.dll','clr.dll','mscorwks.dll','mscorsvr.dll'}), modules
            if label == 'simdskia':
                assert not any('skia' in n or 'harfbuzz' in n or 'av_libgles' in n for n in lower), modules
            else:
                assert 'libskiasharp.dll' in lower and 'libharfbuzzsharp.dll' in lower, modules
            assert m.u.PostMessageW(window, 0x0010, 0, 0)
            assert process.wait(timeout=15) == 0, f'{label}: close failed'
            return dict(nativePeNoClrHeader=True, guiSubsystem=True, noClrOrJitModules=True, loadedModules=modules, screenshot=screenshot, exitCode=process.returncode)
        finally:
            if process.poll() is None:
                process.kill()
                process.wait()


def inventory(directory):
    rows = []
    for p in sorted(directory.rglob('*')):
        if p.is_file():
            category = 'symbols' if p.suffix.lower() == '.pdb' else 'exe' if p.suffix.lower() == '.exe' else 'dll' if p.suffix.lower() == '.dll' else 'other'
            row = dict(path=p.relative_to(directory).as_posix(), bytes=p.stat().st_size, category=category, sha256=digest(p.read_bytes()))
            if category == 'dll':
                blob = p.read_bytes()
                offset = struct.unpack_from('<I', blob, 0x3c)[0]+24
                row['managed'] = struct.unpack_from('<II', blob, offset+112+14*8) != (0, 0)
            rows.append(row)
    totals = {category:sum(r['bytes'] for r in rows if r['category'] == category) for category in ['exe','dll','other','symbols']}
    totals['runtime'] = totals['exe']+totals['dll']+totals['other']
    totals['allPublished'] = totals['runtime']+totals['symbols']
    return dict(files=rows, totals=totals, dllCount=sum(r['category']=='dll' for r in rows))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/default-avalonia-aot-2026-10-26')
    parser.add_argument('--skip-publish',action='store_true')
    args = parser.parse_args()
    assert sys.platform == 'win32', 'Windows x64 verification only'
    output = args.output.resolve()
    output.mkdir(parents=True,exist_ok=True)
    for name in ['App.axaml','App.axaml.cs','MainWindow.axaml','MainWindow.axaml.cs','app.manifest']:
        assert (EXAMPLES/'Original'/name).read_bytes() == (EXAMPLES/'SimdSkia'/name).read_bytes(), name
    props = []
    for variant in ['Original','SimdSkia']:
        tree = ET.parse(EXAMPLES/variant/'DefaultAvalonia.csproj')
        props.append({x.tag:x.text for g in tree.findall('PropertyGroup') for x in g})
        assert '.WithInterFont()' in (EXAMPLES/variant/'Program.cs').read_text()
    assert props[0] == props[1], 'Mismatching AOT properties'
    report = dict(measuredAtUtc=datetime.now(timezone.utc).isoformat(), platform=platform.platform(), sdk=subprocess.check_output(['dotnet','--version'],cwd=ROOT,text=True).strip(), template=dict(package='Avalonia.Templates',version='12.0.0',shortName='avalonia.app',avaloniaVersionOption='12.1.4',framework='net10.0'), properties=props[0], verificationHarnessSha256=digest(Path(__file__).read_bytes()), globalJsonSha256=digest((ROOT/'global.json').read_bytes()), sourceFiles={}, variants={})
    for p in sorted(EXAMPLES.rglob('*')):
        if p.is_file() and not set(p.relative_to(EXAMPLES).parts).intersection({'bin','obj'}):
            report['sourceFiles'][p.relative_to(ROOT).as_posix()] = digest(p.read_bytes())
    for label,variant in [('original','Original'),('simdskia','SimdSkia')]:
        publish = output/'publish'/label
        project = EXAMPLES/variant/'DefaultAvalonia.csproj'
        command = ['dotnet','publish',str(project),'-c','Release','-r','win-x64','-o',str(publish)]
        if not args.skip_publish:
            assert not publish.exists() or not any(publish.iterdir()), 'Choose a fresh output directory to exclude stale files'
            print('Publishing '+label,flush=True)
            with (output/f'publish-{label}.log').open('w',encoding='utf-8') as log:
                subprocess.run(command,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=900)
        assets = json.loads((project.parent/'obj/project.assets.json').read_text())
        packages = {k:v['type'] for k,v in assets['libraries'].items()}
        origins = {}
        for name in packages:
            if name.startswith(('Avalonia/', 'Avalonia.Desktop/', 'Avalonia.Fonts.Inter/', 'SimdSkia.')):
                metadata = [Path(folder)/name.lower()/'.nupkg.metadata' for folder in assets['packageFolders']]
                found = next((p for p in metadata if p.exists()), None)
                assert found, ('Missing cached NuGet origin', name)
                origins[name] = json.loads(found.read_text())['source']
                assert origins[name].rstrip('/') == 'https://api.nuget.org/v3/index.json', (name,origins[name])
        assert packages.get('Avalonia/12.1.4') == 'package' and packages.get('Avalonia.Fonts.Inter/12.1.4') == 'package'
        if label == 'simdskia':
            assert packages.get('SimdSkia.Avalonia.Desktop/0.1.0-preview.7') == 'package'
            assert not any(v=='project' or 'skiasharp' in k.lower() or 'harfbuzz' in k.lower() for k,v in packages.items()), packages
        data = inventory(publish)
        data['command'] = command
        data['packages'] = packages
        data['packageOrigins'] = origins
        data['validation'] = inspect_application(label,publish,output)
        archive = output/f'{label}-runtime.zip'
        with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
            for row in data['files']:
                if row['category'] != 'symbols':
                    z.write(publish/row['path'],row['path'])
        with zipfile.ZipFile(archive) as z:
            expected={r['path']:r for r in data['files'] if r['category']!='symbols'}
            assert set(z.namelist()) == set(expected) and z.testzip() is None
            for name,row in expected.items():
                assert digest(z.read(name)) == row['sha256']
        data['runtimeZip'] = dict(bytes=archive.stat().st_size,sha256=digest(archive.read_bytes()))
        report['variants'][label] = data
        print(label,json.dumps(data['totals']),flush=True)
    a = {r['path']:r for r in report['variants']['original']['files'] if r['category']!='symbols'}
    b = {r['path']:r for r in report['variants']['simdskia']['files'] if r['category']!='symbols'}
    report['removedRuntimeFiles'] = [a[name] for name in sorted(a.keys()-b.keys())]
    report['addedRuntimeFiles'] = [b[name] for name in sorted(b.keys()-a.keys())]
    report['changedRuntimeFiles'] = [dict(path=name,originalBytes=a[name]['bytes'],simdskiaBytes=b[name]['bytes']) for name in sorted(a.keys()&b.keys()) if a[name]['sha256'] != b[name]['sha256']]
    report['runtimeSavedBytes'] = report['variants']['original']['totals']['runtime']-report['variants']['simdskia']['totals']['runtime']
    (output/'results.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('PASS actual default-template Native AOT windows, module isolation, file inventories and complete runtime ZIPs',flush=True)


if __name__ == '__main__':
    main()
