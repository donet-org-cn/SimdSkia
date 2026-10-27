"""Compare raw captures from the two CompatibilityExpansion executables.
Exit 2 preserves known quality failures; do not interpret behavior tests as pixel parity.
"""
import argparse
import json
import math
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument("--root", type=Path, default=Path("artifacts/compat-expansion"))
args = p.parse_args()
native_metadata = json.loads((args.root / "original/result.json").read_text(encoding="utf-8"))
managed_metadata = json.loads((args.root / "managed/result.json").read_text(encoding="utf-8"))
# The layerPixel's 1-LSB rounding allowance is checked in the pixel comparison.
metadata_errors = [key for key, value in native_metadata.items()
                   if key not in ("checks", "layerPixel") and managed_metadata.get(key) != value]
if metadata_errors:
    raise ValueError(f"Original metadata differs: {metadata_errors}")
rows = []
for native in sorted((args.root / "original").glob("*.bgra")):
    managed = args.root / "managed" / native.name
    a, b = native.read_bytes(), managed.read_bytes()
    if len(a) != len(b):
        raise ValueError(f"Mismatched dimensions: {native.name}")
    # Existing repo gates: exact non-AA geometry, 1 LSB compositing, 32 AA paths/text.
    threshold = 32 if native.stem in ("round-aa", "layer-aa-clip", "text") else 1 if native.stem == "group-alpha" else 0
    errors = [abs(x - y) for x, y in zip(a, b)]
    rows.append(dict(scene=native.stem, maxChannelError=max(errors),
                     rmse=math.sqrt(sum(e * e for e in errors) / len(errors)),
                     differentPixels=sum(a[i:i+4] != b[i:i+4] for i in range(0, len(a), 4)),
                     threshold=threshold, passed=max(errors) <= threshold))
if not rows:
    raise ValueError("Run both original and managed capture executables first.")
result = dict(passed=sum(r["passed"] for r in rows), total=len(rows), metadataMatched=True, rows=rows)
(args.root / "quality.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
for row in rows:
    print(f'{"PASS" if row["passed"] else "FAIL"} {row["scene"]}: max={row["maxChannelError"]}, RMSE={row["rmse"]:.6f}, threshold={row["threshold"]}')
raise SystemExit(0 if result["passed"] == result["total"] else 2)
