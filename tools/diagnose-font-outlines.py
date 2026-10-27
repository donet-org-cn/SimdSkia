"""Four controlled font variants for the isolated native/managed outline probe.

Requires fontTools, only as a development tool. Does not replace the benchmark
font or install fonts. Native Skia is loaded only by tests/FontOutlineProbe.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from fontTools.ttLib import TTFont

ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__)
p.add_argument("--font",type=Path,default=ROOT/"baseline/fonts/ZCOOLXiaoWei-Regular.ttf")
p.add_argument("--output",type=Path,required=True)
args=p.parse_args();output=args.output.resolve();source=args.font.resolve()
if output.exists():raise SystemExit("A fresh output directory is required.")
output.mkdir(parents=True)
subprocess.run(["dotnet","build","tests/FontOutlineProbe","-c","Release"],cwd=ROOT,check=True)
records=[]
for strip in [False,True]:
    for recalc in [False,True]:
        font=TTFont(source,recalcBBoxes=recalc,recalcTimestamp=False)
        if strip:
            for name in font.getGlyphOrder():font['glyf'][name].removeHinting()
            for tag in ['fpgm','prep','cvt ']:
                if tag in font:del font[tag]
        name=f"strip-{int(strip)}-recalculate-bounds-{int(recalc)}"
        path=output/(name+'.ttf');font.save(path);font.close()
        result=output/name
        subprocess.run(["dotnet","run","--project","tests/FontOutlineProbe","-c","Release","--no-build","--",str(path),str(result)],cwd=ROOT,check=True)
        cases=json.loads((result/'summary.json').read_text(encoding='utf-8'))
        records.append(dict(stripInstructions=strip,recalculateBounds=recalc,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                            output=name,cases=len(cases),exact=sum(c['different']==0 for c in cases),maximum=max(c['maximum'] for c in cases)))
report=dict(sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),variants=records,
            scope="Diagnostic font copies only; production and performance use the unchanged original font.")
(output/'diagnostic.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
