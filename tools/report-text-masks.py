"""Isolated same-font A8 text comparison; exit 2 preserves a failed quality gate."""
import argparse
import hashlib
import json
import math
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("managed", type=Path)
parser.add_argument("reference", type=Path)
parser.add_argument("--before", type=Path)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
cases = json.loads((args.reference / "text-cases.json").read_text(encoding="utf-8-sig"))
reference = (args.reference / "text.rgba").read_bytes()
width, height = 960, 1008
if len(reference) != width * height * 4:
    raise SystemExit("Unexpected reference dimensions.")

def errors(a, b):
    d = [abs(x-y) for x, y in zip(a, b)]
    maximum = max(d, default=0)
    rmse = math.sqrt(sum(x*x for x in d) / len(d)) if d else 0
    return {"maximumChannelError": maximum, "rmse": rmse,
            "channelsOver32": sum(x > 32 for x in d),
            "passed": maximum <= 32 and rmse <= 2}

results = []
for name, directory in (("after", args.managed), ("before", args.before)):
    if directory is None:
        continue
    if json.loads((directory / "text-cases.json").read_text(encoding="utf-8-sig")) != cases:
        raise SystemExit("Text parameters differ between engines.")
    data = (directory / "text.rgba").read_bytes()
    if len(data) != len(reference):
        raise SystemExit("Pixel dimensions differ.")
    row = {"variant": name, "sha256": hashlib.sha256(data).hexdigest(), **errors(data, reference), "cases": []}
    for case in cases:
        start, end = case["top"] * width * 4, (case["top"] + case["height"]) * width * 4
        row["cases"].append({**case, **errors(data[start:end], reference[start:end])})
    results.append(row)
    print(f"{name}: max {row['maximumChannelError']}, RMSE {row['rmse']:.4f}, channels >32: {row['channelsOver32']}")
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps({"width": width, "height": height,
    "threshold": {"maximumChannelError": 32, "rmse": 2},
    "referenceSha256": hashlib.sha256(reference).hexdigest(), "results": results}, indent=2), encoding="utf-8")
if not results[0]["passed"]:
    raise SystemExit(2)
