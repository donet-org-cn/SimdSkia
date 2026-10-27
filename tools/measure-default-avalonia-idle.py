"""Measure the unchanged default AOT applications while their real windows idle.

Run separately from build, plotting, and rendering benchmarks. Windows x64 only.
"""
import argparse
import ctypes as C
from ctypes import wintypes as W
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import statistics
import subprocess
import time
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--publish', type=Path, default=ROOT/'artifacts/default-avalonia-aot-2026-10-26/publish')
    parser.add_argument('--size-evidence', type=Path, default=ROOT/'docs/evidence/default-avalonia-aot-2026-10-26.json')
    parser.add_argument('--output', type=Path, default=ROOT/'artifacts/cnblogs-performance-2026-10-26/idle')
    parser.add_argument('--forks', type=int, default=3)
    parser.add_argument('--warmup', type=float, default=5)
    parser.add_argument('--seconds', type=float, default=10)
    args = parser.parse_args()
    assert args.forks > 0 and args.warmup >= 0 and args.seconds >= 5
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    m = load('window_checks', ROOT/'tools/test-native-window-minimum.py')
    runner = load('aot_resources', ROOT/'tools/run-avalonia-aot.py')
    m.u.SetProcessDpiAwarenessContext(W.HANDLE(-4))
    kernel = C.WinDLL('kernel32', use_last_error=True)
    kernel.GetProcessTimes.argtypes = [W.HANDLE]+[C.POINTER(W.FILETIME)]*4
    memory_info = C.WinDLL('psapi', use_last_error=True).GetProcessMemoryInfo
    memory_info.argtypes = [W.HANDLE, C.POINTER(runner.ProcessMemory), W.DWORD]
    size_data = json.loads(args.size_evidence.read_text(encoding='utf-8'))
    rows = []

    def cpu(handle):
        values = [W.FILETIME() for _ in range(4)]
        assert kernel.GetProcessTimes(handle, *[C.byref(v) for v in values])
        return sum((v.dwHighDateTime << 32)+v.dwLowDateTime for v in values[2:])/10_000

    for fork in range(args.forks):
        order = ['original','simdskia'] if fork%2 == 0 else ['simdskia','original']
        for label in order:
            executable = (args.publish/label/'DefaultAvalonia.exe').resolve()
            expected = next(r['sha256'] for r in size_data['variants'][label]['files'] if r['category']=='exe')
            actual = hashlib.sha256(executable.read_bytes()).hexdigest()
            assert actual == expected, 'Not the unchanged default-template size executable'
            m.checks.verify_pe(executable)
            destination = output/f'{label}-{fork+1}'
            destination.mkdir()
            with (destination/'stdout.txt').open('w') as stdout, (destination/'stderr.txt').open('w') as stderr:
                process = subprocess.Popen([str(executable)], cwd=executable.parent, stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
                handle = W.HANDLE(int(process._handle))
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
                    assert found, (label,process.poll())
                    window = found[0]
                    assert m.u.SetWindowPos(window,W.HWND(-1),100,100,900,600,0x0040)
                    time.sleep(args.warmup)
                    samples = []
                    cpu_begin = cpu(handle)
                    begin = time.perf_counter()
                    while time.perf_counter()-begin < args.seconds:
                        assert process.poll() is None
                        info = runner.ProcessMemory(); info.cb = C.sizeof(info)
                        assert memory_info(handle,C.byref(info),info.cb)
                        samples.append(dict(t=time.perf_counter()-begin,workingSetMiB=info.workingSet/1048576,privateMiB=info.private/1048576))
                        time.sleep(.05)
                    wall_ms = (time.perf_counter()-begin)*1000
                    cpu_ms = cpu(handle)-cpu_begin
                    module_buffer = (W.HMODULE*1024)(); needed = W.DWORD()
                    assert m.ps.EnumProcessModulesEx(handle,module_buffer,C.sizeof(module_buffer),C.byref(needed),3)
                    assert needed.value <= C.sizeof(module_buffer)
                    modules = []
                    for module in module_buffer[:needed.value//C.sizeof(W.HMODULE)]:
                        name = C.create_unicode_buffer(1024)
                        assert m.ps.GetModuleBaseNameW(handle,module,name,len(name))
                        modules.append(name.value)
                    lower = {name.lower() for name in modules}
                    assert not lower.intersection({'coreclr.dll','clrjit.dll','clr.dll','mscorwks.dll','mscorsvr.dll'})
                    if label=='simdskia':
                        assert not any('skiasharp' in n or 'harfbuzz' in n or 'av_libgles' in n for n in lower)
                    else:
                        assert 'libskiasharp.dll' in lower and 'libharfbuzzsharp.dll' in lower
                    row = dict(variant=label,fork=fork+1,exeSha256=actual,wallMs=wall_ms,cpuMs=cpu_ms,cpuPercentMachine=100*cpu_ms/wall_ms/os.cpu_count(),cpuPercentOneCore=100*cpu_ms/wall_ms,
                               medianWorkingSetMiB=statistics.median(s['workingSetMiB'] for s in samples),medianPrivateMiB=statistics.median(s['privateMiB'] for s in samples),peakSampledWorkingSetMiB=max(s['workingSetMiB'] for s in samples),loadedModules=modules)
                    (destination/'samples.json').write_text(json.dumps(samples,indent=2)+'\n',encoding='utf-8')
                    rows.append(row)
                    (output/'runs.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
                    assert m.u.PostMessageW(window,0x0010,0,0)
                    assert process.wait(timeout=15)==0
                    print(f"{label} fork={fork+1}: CPU={row['cpuPercentMachine']:.4f}% WS={row['medianWorkingSetMiB']:.2f} Private={row['medianPrivateMiB']:.2f} MiB",flush=True)
                finally:
                    if process.poll() is None:
                        process.kill();process.wait()
    summary = []
    for label in ['original','simdskia']:
        group = [r for r in rows if r['variant']==label]
        item = dict(variant=label,forks=len(group))
        for key in ['cpuPercentMachine','cpuPercentOneCore','cpuMs','medianWorkingSetMiB','medianPrivateMiB','peakSampledWorkingSetMiB']:
            values = [r[key] for r in group]
            item[key] = statistics.median(values)
            item[key+'Min'] = min(values);item[key+'Max'] = max(values)
        summary.append(item)
    result = dict(checkedUtc=datetime.now(timezone.utc).isoformat(),processors=os.cpu_count(),warmupSeconds=args.warmup,measurementSeconds=args.seconds,sampleIntervalMs=50,scope='Visible unchanged default-template Native AOT idle windows; no synthetic animation, no forced GC; default original GPU vs SimdSkia CPU.',runs=rows,summary=summary)
    (output/'results.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print('PASS default AOT idle process CPU/memory and module validation',flush=True)


if __name__ == '__main__':
    main()
