"""Pinned native oracle, byte-exact strided erase, and unchanged 32/2 image gate."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess
import zlib

p=argparse.ArgumentParser()
p.add_argument('--output',type=Path,required=True)
a=p.parse_args()
if a.output.exists():raise SystemExit('Choose a fresh directory to avoid stale evidence.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:
    subprocess.run(['dotnet','run','--project',f'tests/ShaderCompatibility/{variant}','-c','Release','--',str(a.output/variant.lower())],check=True)
ref=json.loads((a.output/'original/results.json').read_text(encoding='utf-8'))
got=json.loads((a.output/'managed/results.json').read_text(encoding='utf-8'))
failures=[]
if ref!=got:failures.append(dict(kind='behavior',reference=ref,replacement=got))
def png(path,data):
    # Raw fixtures are BGRA premul over an opaque background.
    rgba=b''.join(bytes((data[i+2],data[i+1],data[i],data[i+3])) for i in range(0,len(data),4))
    def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d))
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>2I5B',128,128,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(b'\0'+rgba[y*512:(y+1)*512] for y in range(128))))+chunk(b'IEND',b''))
images=[];erase=[]
for file in sorted((a.output/'original').iterdir()):
    if file.suffix not in ['.bgra','.bytes']:continue
    x=file.read_bytes();y=(a.output/'managed'/file.name).read_bytes()
    if len(x)!=len(y):failures.append(file.name+': length mismatch');continue
    errors=[abs(u-v) for u,v in zip(x,y)]
    if file.suffix=='.bytes':
        ok=x==y;erase.append(dict(scene=file.stem,bytes=len(x),exact=ok))
    else:
        assert len(x)==128*128*4
        maximum=max(errors);rmse=math.sqrt(sum(v*v for v in errors)/len(errors));ok=maximum<=32 and rmse<=2
        images.append(dict(scene=file.stem,maxError=maximum,rmse=rmse,passed=ok))
        png(file.with_suffix('.png'),x);png((a.output/'managed'/file.name).with_suffix('.png'),y)
        diff=bytes(min(255,v*8) if i%4!=3 else 255 for i,v in enumerate(errors))
        png(a.output/(file.stem+'-diff-x8.png'),diff)
    if not ok:failures.append(file.name+': quality/byte-exact gate failed')
checks=json.loads((a.output/'managed/managed-checks.json').read_text(encoding='utf-8'))
report=dict(baseline='SkiaSharp 3.119.4 CPU',behaviorCases=len(ref),behaviorPassed=ref==got,managedChecks=checks,pixelThreshold=dict(maximum=32,rmse=2),images=images,erase=erase,failures=failures,passed=not failures,scope='SKColor gradients and Erase; not a claim of complete SKShader/GPU/API compatibility')
(a.output/'comparison.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
sources={str(path):hashlib.sha256(path.read_bytes()).hexdigest() for root in ['src/SimdSkia.Compatibility','src/SimdSkia.Cpu','tests/ShaderCompatibility'] for path in Path(root).rglob('*.cs') if not {'bin','obj'}&set(path.parts)}
(a.output/'source-hashes.json').write_text(json.dumps(sources,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(passed=report['passed'],behaviorCases=len(ref),images=len(images),erase=len(erase),managedChecks=checks,failures=failures),indent=2))
raise SystemExit(2 if failures else 0)
