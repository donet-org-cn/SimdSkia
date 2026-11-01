"""Summarize complete serial runs without hiding colour/compatibility failures."""
import argparse
import hashlib
import json
from pathlib import Path
import statistics
import re
import xml.etree.ElementTree as ET

parser=argparse.ArgumentParser()
parser.add_argument('--directory',type=Path,default=Path('artifacts/simdcodecs-evaluation-2026-11-01'))
parser.add_argument('--rounds',type=int,default=3)
args=parser.parse_args();root=args.directory
project=Path(__file__).resolve().parent
assets=json.loads((project/'obj/project.assets.json').read_text(encoding='utf-8-sig'))
cache=Path(next(iter(assets['packageFolders'])))
packages=[]
for name in ('sdcb.simdcodecs.jpeg','sdcb.simdcodecs.png','sdcb.simdcodecs.abstractions','sdcb.simdcodecs.color.icc'):
    folder=cache/name/'1.2.0'
    nuspec=ET.parse(next(folder.glob('*.nuspec'))).getroot()
    ns={'n':nuspec.tag.split('}')[0][1:]};metadata=nuspec.find('n:metadata',ns)
    repository=metadata.find('n:repository',ns)
    packages.append(dict(id=name,version='1.2.0',license=metadata.findtext('n:license',None,ns),
                         repository=repository.attrib if repository is not None else {},
                         dlls=[dict(path=str(p.relative_to(folder)),bytes=p.stat().st_size) for p in folder.rglob('*.dll')],
                         dependencies=[e.attrib for e in metadata.findall('.//n:dependency',ns)]))
(root/'packages.json').write_text(json.dumps(packages,indent=2)+'\n',encoding='utf-8')
host=json.loads((root/'host.json').read_text(encoding='utf-8-sig'))
raw=[json.loads(p.read_text(encoding='utf-8-sig')) for p in sorted((root/'runs').glob('*.json'))]
assert len(raw)==args.rounds*20,(len(raw),'Incomplete matrix')
fields=['millisecondsPerImage','cpuMillisecondsPerImage','allocatedBytesPerImage','workingSetMiB','privateMiB','peakWorkingSetMiB']
summary=[]
for scene in ('ui','texture'):
    for operation in ('decode-jpeg444','decode-jpeg420','decode-png','encode-jpeg444','encode-png'):
        medians={}
        for backend in ('existing','sdcb'):
            rows=[r for r in raw if (r['scene'],r['operation'],r['backend'])==(scene,operation,backend)]
            assert len(rows)==args.rounds
            medians[backend]={key:statistics.median(r[key] for r in rows) for key in fields}
        summary.append(dict(scene=scene,operation=operation,**medians,speedup=medians['existing']['millisecondsPerImage']/medians['sdcb']['millisecondsPerImage']))
validation=json.loads((root/'validation.json').read_text(encoding='utf-8'))
scalar=json.loads((root/'validation-scalar.json').read_text(encoding='utf-8'))
aot=json.loads((root/'validation-aot.json').read_text(encoding='utf-8'))
jit_hashes={r['name']:r.get('sha256') for r in validation['rows'] if r['engine']=='sdcb'}
assert len(jit_hashes)==41
assert all(jit_hashes[r['name']]==r['sha256'] for r in aot['rows'])
assert all(jit_hashes[r['name']]==r.get('sha256') for r in scalar['rows'] if r['engine']=='sdcb')
binary_root=Path(__file__).resolve().parent/'bin/Release/net10.0'
digests={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in binary_root.glob('*.dll')}
date=re.search(r'\d{4}-\d{2}-\d{2}',root.name)
result=dict(date=date[0] if date else 'not specified',version='Sdcb.SimdCodecs 1.2.0',cpu=host['cpu'],
            cpuLogical=12,runtime=raw[0]['runtime'],resolution=[1920,1080],rounds=args.rounds,
            measurement=f"Separate serial processes; {host['warmupSeconds']}s warmup, at least {host['seconds']}s measurement, {args.rounds}-process medians; no disk IO in timing",
            fixtures='Deterministic first-party opaque UI and texture patterns; not a photographic corpus',
            aot='41 Sdcb fixture hashes equal across JIT SIMD, JIT scalar and actual Native AOT; AOT speed not measured',
            packages=json.loads((root/'packages.json').read_text(encoding='utf-8')),
            summary=summary,raw=raw,validation=validation,scalarValidation=scalar,aotValidation=aot,
            quality=json.loads((root/'quality.json').read_text(encoding='utf-8')),
            binarySha256=digests,
            nativeCompressionObserved=any(any(n=='System.IO.Compression.Native.dll' for n in r['nativeModules']) for r in raw if r['backend']=='sdcb' and r['operation'].endswith('png')),
            productionDependenciesChanged=False)
(root/'results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for row in summary:
    print(f"{row['scene']:8} {row['operation']:15} {row['existing']['millisecondsPerImage']:9.3f} -> {row['sdcb']['millisecondsPerImage']:8.3f} ms {row['speedup']:6.2f}x")
