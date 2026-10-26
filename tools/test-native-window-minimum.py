"""Externally verify the actual tiny shipping EXE: no embedded test harness needed.

Requires a visible Windows desktop. Captures the client DC, compares all RGB
pixels with the earlier validated images, sends resize/click/close, inspects
loaded modules, and packages the smallest successful candidate for each variant.
"""
import argparse
import ctypes as C
from ctypes import wintypes as W
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import shutil
import struct
import subprocess
import time
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("window_checks", ROOT / "tools/test-native-window-aot.py")
checks = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checks)
u = C.WinDLL("user32", use_last_error=True)
g = C.WinDLL("gdi32", use_last_error=True)
ps = C.WinDLL("psapi", use_last_error=True)
callback = C.WINFUNCTYPE(W.BOOL, W.HWND, W.LPARAM)
u.EnumWindows.argtypes = [callback, W.LPARAM]
u.GetWindowThreadProcessId.argtypes = [W.HWND, C.POINTER(W.DWORD)]
u.GetClassNameW.argtypes = [W.HWND, W.LPWSTR, C.c_int]
u.GetWindowTextW.argtypes = [W.HWND, W.LPWSTR, C.c_int]
u.EnumChildWindows.argtypes = [W.HWND, callback, W.LPARAM]
u.IsWindowVisible.argtypes = [W.HWND]
u.GetClientRect.argtypes = [W.HWND, C.POINTER(W.RECT)]
u.GetDC.argtypes = [W.HWND]; u.GetDC.restype = W.HDC
u.ReleaseDC.argtypes = [W.HWND, W.HDC]
u.SetWindowPos.argtypes = [W.HWND, W.HWND, C.c_int, C.c_int, C.c_int, C.c_int, W.UINT]
u.PostMessageW.argtypes = [W.HWND, W.UINT, W.WPARAM, W.LPARAM]
u.SetProcessDpiAwarenessContext.argtypes = [W.HANDLE]
g.CreateCompatibleDC.argtypes = [W.HDC]; g.CreateCompatibleDC.restype = W.HDC
g.CreateDIBSection.argtypes = [W.HDC, C.c_void_p, W.UINT, C.POINTER(C.c_void_p), W.HANDLE, W.DWORD]
g.CreateDIBSection.restype = W.HANDLE
g.SelectObject.argtypes = [W.HDC, W.HANDLE]; g.SelectObject.restype = W.HANDLE
g.BitBlt.argtypes = [W.HDC, C.c_int, C.c_int, C.c_int, C.c_int, W.HDC, C.c_int, C.c_int, W.DWORD]
g.DeleteObject.argtypes = [W.HANDLE]; g.DeleteDC.argtypes = [W.HDC]
ps.EnumProcessModulesEx.argtypes = [W.HANDLE, C.POINTER(W.HMODULE), W.DWORD, C.POINTER(W.DWORD), W.DWORD]
ps.GetModuleBaseNameW.argtypes = [W.HANDLE, W.HMODULE, W.LPWSTR, W.DWORD]


def find_window(pid):
    found = []
    errors = []
    @callback
    def visit(window, _):
        owner = W.DWORD()
        u.GetWindowThreadProcessId(window, C.byref(owner))
        name = C.create_unicode_buffer(256)
        u.GetClassNameW(window, name, len(name))
        if owner.value == pid and u.IsWindowVisible(window) and name.value == "SimdSkiaStandaloneWindow":
            found.append(window)
        elif owner.value == pid and u.IsWindowVisible(window) and name.value == "#32770":
            @callback
            def child(control, _):
                text = C.create_unicode_buffer(4096)
                u.GetWindowTextW(control, text, len(text))
                if text.value: errors.append(text.value)
                return True
            u.EnumChildWindows(window, child, 0)
        return True
    u.EnumWindows(visit, 0)
    if errors: raise RuntimeError("Application error dialog: " + " | ".join(errors))
    return found[0] if found else None


def capture(window):
    rect = W.RECT()
    assert u.GetClientRect(window, C.byref(rect))
    width, height = rect.right, rect.bottom
    assert 0 < width < 4096 and 0 < height < 4096
    dc = u.GetDC(window)
    memory = g.CreateCompatibleDC(dc)
    header = struct.pack("<IiiHHIIiiII", 40, width, -height, 1, 32, 0, width * height * 4, 0, 0, 0, 0)
    info = C.create_string_buffer(header)
    bits = C.c_void_p()
    bitmap = g.CreateDIBSection(dc, info, 0, C.byref(bits), None, 0)
    assert dc and memory and bitmap
    previous = g.SelectObject(memory, bitmap)
    try:
        assert g.BitBlt(memory, 0, 0, width, height, dc, 0, 0, 0x00CC0020)
        assert g.GdiFlush()
        pixels = bytearray(C.string_at(bits, width * height * 4))
        # GDI's 32-bit destination has an undefined X byte, unlike our opaque
        # raster alpha. Compare all RGB exactly and normalize only that X byte.
        pixels[3::4] = b"\xff" * (width * height)
        return struct.pack("<2sIII", b"BM", 54 + len(pixels), 0, 54) + header + pixels
    finally:
        g.SelectObject(memory, previous)
        g.DeleteObject(bitmap); g.DeleteDC(memory); u.ReleaseDC(window, dc)


def modules(process):
    buffer = (W.HMODULE * 1024)(); needed = W.DWORD()
    handle = W.HANDLE(int(process._handle))
    assert ps.EnumProcessModulesEx(handle, buffer, C.sizeof(buffer), C.byref(needed), 3)
    assert needed.value <= C.sizeof(buffer)
    names = []
    for module in buffer[:needed.value // C.sizeof(W.HMODULE)]:
        name = C.create_unicode_buffer(1024)
        assert ps.GetModuleBaseNameW(handle, module, name, len(name))
        names.append(name.value)
    assert not any(any(bad in name.lower() for bad in ("coreclr", "clrjit", "skiasharp", "harfbuzz", "avalonia")) for name in names)
    return names


def verify(executable, destination, baseline, include_text):
    checks.verify_pe(executable)
    destination.mkdir(parents=True)
    process = subprocess.Popen([str(executable)], cwd=destination, creationflags=subprocess.CREATE_NO_WINDOW)
    window = None
    try:
        deadline = time.monotonic() + 30
        while window is None and process.poll() is None and time.monotonic() < deadline:
            window = find_window(process.pid)
            time.sleep(.02)
        assert window, f"No visible application window; exit={process.poll()}"
        u.SetWindowPos(window, W.HWND(-1), 80, 80, 640, 420, 0x0040)
        # Client DC pixels can match while the initial DWM show transition is
        # still in progress. Do not start a second resize inside that transition.
        # This harness measures size/quality, never startup latency.
        time.sleep(.5)
        shots = []
        for index in range(4):
            # First three frames duplicate the earlier independent baseline.
            # Fourth exercises a second click, restoring the blue rectangle.
            baseline_index = index if index < 3 else 1
            expected = (baseline / f"frame-{baseline_index}.bmp").read_bytes()
            if index == 1:
                assert u.SetWindowPos(window, W.HWND(-1), 80, 80, 780, 500, 0x0040)
                time.sleep(.3)
            if index in (2, 3):
                assert u.PostMessageW(window, 0x0201, 1, 48 | (48 << 16))
                assert u.PostMessageW(window, 0x0202, 0, 48 | (48 << 16))
            deadline = time.monotonic() + 10
            actual = b""
            while time.monotonic() < deadline and process.poll() is None:
                actual = capture(window)
                if actual[18:30] == expected[18:30] and actual[54:] == expected[54:]:
                    break
                time.sleep(.03)
            path = destination / f"frame-{index}.bmp"
            path.write_bytes(actual)
            assert actual[18:30] == expected[18:30] and actual[54:] == expected[54:], f"Full client RGB mismatch: {path}"
            shot = checks.check_bitmap(path, index == 2, include_text)
            shot["allRgbPixelsMatchBaseline"] = True
            shots.append(shot)
        loaded = modules(process)
        assert u.PostMessageW(window, 0x0010, 0, 0)
        assert process.wait(timeout=10) == 0
        return {"frames": shots, "loadedModules": loaded, "normalClose": True, "nativeGuiPe": True}
    finally:
        if process.poll() is None:
            if window: u.PostMessageW(window, 0x0010, 0, 0)
            try: process.wait(timeout=3)
            except subprocess.TimeoutExpired: process.kill(); process.wait()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/native-window-minimum/release")
    parser.add_argument("--baseline", type=Path, default=ROOT / "artifacts/native-window-aot")
    options = parser.parse_args()
    output = options.output.resolve(); baseline = options.baseline.resolve()
    if output.exists(): raise RuntimeError("Choose a fresh output directory.")
    output.mkdir(parents=True)
    u.SetProcessDpiAwarenessContext(W.HANDLE(-4))
    old = json.loads((baseline / "results.json").read_text(encoding="utf-8"))
    for variant in ("shapes", "text"):
        for shot in old["variants"][variant]["bitmaps"]:
            path = ROOT / shot["path"]
            assert checks.sha(path) == shot["sha256"], "Baseline bitmap changed"
    # Test documented switches first, then isolated toolchain-specific metadata
    # and direct-import experiments. Failed candidates cannot become winners.
    profiles = [
        ("size-only", ["-p:MinimumSize=false", "-p:IncludeVerification=false"]),
        ("minimum", ["-p:MinimumSize=true"]),
        ("metadata", ["-p:MinimumSize=true", "-p:MinimalMetadata=true"]),
        ("direct", ["-p:PublishProfile=MinimumSize"]),
    ]
    result = {"measuredAtUtc": datetime.now(timezone.utc).isoformat(), "sdk": subprocess.check_output(["dotnet", "--version"], text=True).strip(),
              "baseline": str(baseline), "baselineEvidenceSha256": checks.sha(baseline / "results.json"),
              "candidates": [], "winners": {}, "sourceFiles": {}}
    for variant in ("text", "shapes"):
        for profile, settings in profiles:
            label = variant + "-" + profile
            directory = output / label
            command = ["dotnet", "publish", str(checks.PROJECT), "-c", "Release", "-r", "win-x64",
                       f"-p:IncludeText={'true' if variant == 'text' else 'false'}", *settings, "-o", str(directory)]
            row = {"label": label, "variant": variant, "profile": profile, "command": command, "passed": False}
            print("Publish/test " + label, flush=True)
            with (output / f"{label}.log").open("w", encoding="utf-8") as log:
                build = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=600)
            if build.returncode:
                row["failure"] = "Build failed; see log"
            else:
                executable = directory / "NativeWindow.exe"
                row["exeBytes"] = executable.stat().st_size
                rsp = ROOT / "examples/NativeWindow/obj/Release/net10.0/win-x64/native/NativeWindow.ilc.rsp"
                shutil.copy2(rsp, output / f"{label}.ilc.rsp")
                args = rsp.read_text(encoding="utf-8-sig").splitlines()
                assert "--Os" in args and "--dehydrate" in args, "Size optimization not active"
                if profile != "size-only":
                    assert "--feature:System.Globalization.Invariant=true" in args
                    assert "--feature:System.Resources.UseSystemResourceKeys=true" in args
                    assert "--stacktracedata" not in args
                    assert "--methodbodyfolding:all" in args
                if profile == "direct":
                    assert "--directpinvoke:user32" in args and "--directpinvoke:gdi32" in args
                row["compilerOptions"] = [a for a in args if any(key in a for key in ["--Os", "dehydrate", "methodbodyfolding", "stacktracedata", "reflectiondata", "directpinvoke", "--feature:System.Globalization", "--feature:System.Resources", "--feature:System.Diagnostics"]) ]
                try:
                    row["verification"] = verify(executable, output / ("verify-" + label), baseline / ("verify-" + variant), variant == "text")
                    row["passed"] = True
                except Exception as error:
                    row["failure"] = str(error)
            result["candidates"].append(row)
            print(f"{label}: {'PASS' if row['passed'] else 'FAIL'} {row.get('exeBytes')} {row.get('failure', '')}", flush=True)
            (output / "results.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        passed = [r for r in result["candidates"] if r["variant"] == variant and r["passed"]]
        assert passed, "No valid deployment"
        winner = min(passed, key=lambda r: r["exeBytes"])
        deployment = output / ("portable-" + variant)
        deployment.mkdir()
        files = []
        for path in sorted((output / winner["label"]).rglob("*")):
            if not path.is_file() or path.suffix.lower() == ".pdb": continue
            assert path.suffix.lower() not in {".dll", ".ttf", ".ttc", ".otf"}
            relative = path.relative_to(output / winner["label"])
            dest = deployment / relative; dest.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(path, dest)
            files.append({"path": relative.as_posix(), "bytes": dest.stat().st_size, "sha256": checks.sha(dest)})
        final_check = verify(deployment / "NativeWindow.exe", output / ("verify-final-" + variant), baseline / ("verify-" + variant), variant == "text")
        archive_path = output / ("native-window-minimum-" + variant + ".zip")
        with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for f in files: archive.write(deployment / f["path"], f["path"])
        with zipfile.ZipFile(archive_path) as archive:
            assert archive.testzip() is None
            for f in files: assert hashlib.sha256(archive.read(f["path"])).hexdigest() == f["sha256"]
        result["winners"][variant] = {"label": winner["label"], "exeBytes": winner["exeBytes"],
            "runtimeBytes": sum(f["bytes"] for f in files), "zipBytes": archive_path.stat().st_size,
            "zipSha256": checks.sha(archive_path), "files": files, "verification": final_check}
    for path in [checks.PROJECT, checks.PROJECT.with_name("Program.cs"), checks.PROJECT.parent / "Properties/PublishProfiles/MinimumSize.pubxml", Path(__file__), ROOT / "tools/test-native-window-aot.py"]:
        result["sourceFiles"][path.relative_to(ROOT).as_posix()] = checks.sha(path)
    (output / "results.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("PASS: " + str(output / "results.json"), flush=True)


if __name__ == "__main__":
    locks = {p: p.read_bytes() for p in (ROOT / "src").glob("*/packages.lock.json")}
    try: main()
    finally:
        for path, data in locks.items():
            if path.read_bytes() != data: path.write_bytes(data)
