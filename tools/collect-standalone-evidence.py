"""Collect already-run checks; never substitutes for running the tests."""
import hashlib
import json
import shutil
import struct
import subprocess
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
data = root / "artifacts/compat-expansion"
out = root / "docs/evidence/standalone-compatibility-2026-10-27"
out.mkdir(parents=True, exist_ok=True)
def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def executable(path):
    b = path.read_bytes()
    pe = struct.unpack_from("<I", b, 0x3c)[0]
    optional = pe + 24
    assert b[pe:pe+4] == b"PE\0\0" and struct.unpack_from("<H", b, optional)[0] == 0x20b
    clr = struct.unpack_from("<II", b, optional + 112 + 14*8)
    assert clr == (0, 0), "AOT executable unexpectedly has a CLR header"
    assert not list(path.parent.glob("*.dll")), "Unexpected dependency DLL in AOT output"
    return dict(bytes=len(b), sha256=digest(path), clrDirectory=list(clr), dlls=[])

package = data / "package-smoke/SimdSkia.Pdf.0.1.0-preview.8.nupkg"
with zipfile.ZipFile(package) as z:
    names = z.namelist()
    for name in ("README.md", "LICENSE", "CHANGELOG.md", "licenses/Apache-2.0.txt",
                 "licenses/PdfPig-NOTICE.txt", "licenses/PdfPig-UPSTREAM-NOTICES.txt", "lib/net8.0/SimdSkia.Pdf.dll"):
        assert name in names, name
    assert not any(n.startswith("runtimes/") or n.endswith((".so", ".dylib", ".ttf", ".ttc")) for n in names)

engine = root / "benchmarks/PixelAccess/Managed/bin/Release/net8.0"
files = [dict(name=p.name, bytes=p.stat().st_size, sha256=digest(p)) for p in sorted(engine.glob("*.dll")) if p.name != "Managed.dll"]
for group in ("managed", "original", "compat-aot-results", "pdf", "pdf-aot-results", "pdf-independent"):
    shutil.copyfile(data / group / "result.json", out / (group + ".json"))
for p in (data / "pixel-benchmark").glob("*.json"):
    shutil.copyfile(p, out / ("pixel-" + p.name))
shutil.copyfile(data / "quality.json", out / "quality.json")
shutil.copyfile(root / "artifacts/pdf-viewer/presented.png", out / "pdf-viewer.png")
source = {str(p.relative_to(root)).replace("\\", "/"): digest(p)
          for p in root.joinpath("src").rglob("*")
          if p.suffix in (".cs", ".csproj") and "obj" not in p.parts and "bin" not in p.parts}
fixtures = {str(p.relative_to(root)).replace("\\", "/"): digest(p)
            for folder in ("tests/Pdf/Fixtures", "tests/CompatibilityExpansion/Fixtures")
            for p in root.joinpath(folder).glob("*") if p.is_file()}
result = dict(
    scope="Unreleased working tree, not the public preview.8 package", date="2026-10-27",
    gitHead=subprocess.check_output(["git", "-c", "safe.directory=F:/simdSkia", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
    baseline="SkiaSharp 3.119.4", hardware="Windows 10.0.26300 / i7-8700K / 12 logical processors / GTX 1050 Ti",
    build=dict(configuration="Release", warnings=0, errors=0),
    checks=dict(original=read(data / "original/result.json")["checks"], managed=read(data / "managed/result.json")["checks"],
                pdf=read(data / "pdf/result.json")["checks"],
                compatibilityAot=read(data / "compat-aot-results/result.json"), pdfAot=read(data / "pdf-aot-results/result.json"),
                existing=dict(basic=36, features=13, imaging=484, optimization=471569, avaloniaText=595, avaloniaControls=9741,
                              vulkanSampling=590, vulkanKernel=540, gpuText=599, direct2d=344, gpuIntegration=12174)),
    api=dict(types=63, matched=892, baselineEntries=3235, missing=2343, addedOrDifferent=0,
             assembly=read(root / "docs/api/implemented.json")),
    quality=read(data / "quality.json"), independentPdf=read(data / "pdf-independent/result.json"),
    pixelBenchmark=read(data / "pixel-benchmark/summary.json"),
    preliminaryPixelSummary=dict(originalMs=.582275994566272, managedMs=.603902576489533, ratio=1.037141462339299,
                                 note="Earlier observed summary; per-fork files were replaced by the complete final serial rerun. Not pooled with final measurements."),
    aot=dict(compatibility=executable(data / "compat-aot/Managed.exe"), pdf=executable(data / "pdf-aot/Pdf.exe")),
    engineDlls=files, engineDllBytes=sum(p["bytes"] for p in files),
    localPackage=dict(path=str(package.relative_to(root)), sha256=digest(package), uploaded=False, note="Local packaging smoke only; not a newly published preview.8"),
    viewer=(root / "artifacts/pdf-viewer/result.txt").read_text(encoding="utf-8"),
    sourceHashes=source, fixtureHashes=fixtures)
(out / "results.json").write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("Collected standalone evidence:", out)
