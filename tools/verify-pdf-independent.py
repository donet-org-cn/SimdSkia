"""Development-only independent validation. Requires pypdf, PyMuPDF, Pillow.
These native/reference tools are not product or C# test dependencies.
"""
import json
import math
from pathlib import Path
import pymupdf
from pypdf import PdfReader
from PIL import Image, ImageChops, ImageEnhance

root = Path(__file__).resolve().parents[1]
artifacts = root / "artifacts/compat-expansion"
out = artifacts / "pdf-independent"
out.mkdir(parents=True, exist_ok=True)
export = PdfReader(artifacts / "managed/pages.pdf", strict=True)
assert len(export.pages) == 2 and export.metadata.title == "SimdSkia \u4e2d\u6587"
rows = []
for name in ("external", "alpha"):
    with pymupdf.open(root / f"tests/Pdf/Fixtures/{name}.pdf") as document:
        for index, page in enumerate(document):
            pixmap = page.get_pixmap(matrix=pymupdf.Matrix(2, 2), alpha=False)
            expected = Image.frombytes("RGB", (pixmap.width, pixmap.height), pixmap.samples)
            actual = Image.open(artifacts / f"pdf/{name}-{index + 1}.png").convert("RGB")
            assert actual.size == expected.size
            a, b = actual.tobytes(), expected.tobytes()
            errors = [abs(x - y) for x, y in zip(a, b)]
            rows.append(dict(scene=f"{name}-{index+1}", maxChannelError=max(errors), rmse=math.sqrt(sum(e*e for e in errors)/len(errors)),
                             differentPixels=sum(a[i:i+3] != b[i:i+3] for i in range(0, len(a), 3))))
            expected.save(out / f"{name}-{index+1}-reference.png")
            ImageEnhance.Brightness(ImageChops.difference(actual, expected)).enhance(8).save(out / f"{name}-{index+1}-diff-x8.png")
result = dict(exportPages=len(export.pages), exportStructurePassed=True,
              note="Diagnostic differences against an independent rasterizer; not a universal PDF quality pass.", rows=rows)
(out / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result, indent=2))
