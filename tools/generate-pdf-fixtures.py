"""Development only: pip install reportlab==5.1.0 pillow==13.0.0.
Run after tools/fetch-avalonia-font.ps1. No Python dependency at test runtime.
The generated PDF contains an OFL font subset; retain the fixture license.
"""
from pathlib import Path
from io import BytesIO
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.utils import ImageReader
from PIL import Image

root = Path(__file__).resolve().parents[1]
out = root / "tests/Pdf/Fixtures"
out.mkdir(parents=True, exist_ok=True)
pdfmetrics.registerFont(TTFont("Fixture", str(root / "baseline/fonts/ZCOOLXiaoWei-Regular.ttf")))
image = Image.new("RGB", (32, 16))
image.putdata([(x * 8, y * 16, 80) for y in range(16) for x in range(32)])
doc = canvas.Canvas(str(out / "external.pdf"), pagesize=(240, 160), pageCompression=1, invariant=1)
doc.setTitle("Independent CJK / vector / image PDF fixture")
doc.setFillColorRGB(.1, .3, .8)
doc.roundRect(12, 12, 216, 136, 12, fill=1, stroke=0)
doc.setFillColorRGB(1, 1, 1)
doc.setFont("Fixture", 20)
doc.drawString(24, 112, "PDF Hello \u4e2d\u6587")
doc.setStrokeColorRGB(1, 0, 0)
doc.setLineWidth(2)
doc.line(24, 96, 210, 96)
doc.drawImage(ImageReader(image), 24, 24, width=96, height=48)
doc.showPage()
doc.setPageRotation(90)
doc.setFillColorRGB(.2, .8, .4)
doc.rect(10, 20, 80, 50, fill=1, stroke=0)
doc.showPage()
doc.save()

rgba = Image.new("RGBA", (16, 16))
rgba.putdata([(255, 0, 0, x * 17) for y in range(16) for x in range(16)])
doc = canvas.Canvas(str(out / "alpha.pdf"), pagesize=(100, 80), pageCompression=1, invariant=1)
doc.setFillColorRGB(0, 0, 1)
doc.rect(0, 0, 100, 80, fill=1, stroke=0)
doc.drawImage(ImageReader(rgba), 10, 10, width=80, height=60, mask="auto")
doc.showPage()
doc.save()
print("Generated independent PDF fixtures:", out)
