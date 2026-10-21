"""Independent reference process only. The product never calls Python/zlib."""
import base64,json,pathlib,sys,zlib
path=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else 'artifacts/completion/managed-zlib-encoded.json')
cases=json.loads(path.read_text(encoding='utf-8-sig'))
for case in cases:
    expected=base64.b64decode(case['raw']);actual=zlib.decompress(base64.b64decode(case['encoded']))
    if actual!=expected:raise SystemExit(f"FAIL {case['length']}/{case['pattern']}")
print(f'PASS: {len(cases)} managed LZ77/fixed-Huffman/stored streams decoded by independent Python zlib; zero differences.')
