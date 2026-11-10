"""Archive completed measurements and content-addressed raw outputs.

Run after all timing and validation. Excludes executables, caches and browser
profiles. Binary SHA256 values remain in each measurement manifest.
"""
import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--input',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
args.output.mkdir(parents=True,exist_ok=False)
frames={}
stages=['cpu-formal','api-formal','codec-formal','gpu-micro-formal','gpu-pages-formal',
        'aot-pages-formal','retention-verified','gpu-chinese-confirmation','validation','old-engine-replay']
text_suffixes={'.json','.csv','.log','.txt','.rsp','.py'}
binary_suffixes={'.rgba','.bgra','.bin','.ticks','.png','.jpg','.jpeg','.webp','.skp','.svg','.pdf','.xps'}
for stage in stages:
    directory=args.input/stage
    if not directory.is_dir():raise SystemExit('Missing completed stage: '+stage)
    for parent,names,filenames in os.walk(directory):
        names[:]=[name for name in names if name not in ('browser-profiles','workers','bin','obj')]
        for name in filenames:
            path=Path(parent)/name
            relative=path.relative_to(args.input).as_posix()
            if path.suffix in text_suffixes:
                destination=args.output/relative
                destination.parent.mkdir(parents=True,exist_ok=True)
                shutil.copyfile(path,destination)
            elif path.suffix in binary_suffixes:
                data=path.read_bytes();digest=hashlib.sha256(data).hexdigest()
                target=Path('objects')/(digest+'.gz')
                destination=args.output/target
                if not destination.exists():
                    destination.parent.mkdir(parents=True,exist_ok=True)
                    destination.write_bytes(gzip.compress(data,compresslevel=9,mtime=0))
                frames[relative]=dict(object=target.as_posix(),length=len(data),sha256=digest)

for path in args.input.iterdir():
    if path.is_file() and path.suffix in text_suffixes:
        shutil.copyfile(path,args.output/path.name)
for stage in ['sampling-portable','sampling-v3','completion','codec']:
    folder=args.input/'aot-tests'/stage
    for path in folder.rglob('*'):
        if path.is_file() and path.suffix=='.json' and path.parent.name in ('results','scalar'):
            destination=args.output/'native-tests'/stage/path.parent.name/path.name
            destination.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,destination)

source={}
for parent,names,filenames in os.walk(root/'src'):
    names[:]=[name for name in names if name not in ('obj','bin')]
    for name in filenames:
        path=Path(parent)/name
        if path.suffix in ('.cs','.csproj','.comp','.spv'):
            source[path.relative_to(root).as_posix()]=hashlib.sha256(path.read_bytes()).hexdigest()
(args.output/'source-sha256.json').write_text(json.dumps(source,indent=2)+'\n',encoding='utf-8')
(args.output/'frame-objects.json').write_text(json.dumps(frames,indent=2)+'\n',encoding='utf-8')
for name in ['collect-final.py','post-collection.py']:
    source_path=args.input.parent/name
    shutil.copyfile(source_path,args.output/name)
scripts=args.output/'harness';scripts.mkdir()
for name in ['run-performance-validation.py','check-optimization-regressions.py','run-cpu-optimization.py',
             'run-final-api-benchmarks.py','run-codec-skia-comparison.py','run-gpu-slow-cases.py','run-gpu-parity.py',
             'run-avalonia-aot.py','run-decode-retention.py','report-comprehensive-performance.py','collect-optimization-evidence.py']:
    shutil.copyfile(root/'tools'/name,scripts/name)
metadata=dict(date='2026-11-10',scope='Working-tree source optimization; public NuGet remains 0.1.4',
    baseline='SkiaSharp 3.119.4',avalonia='12.1.5',beforeCommit='1eceed6',
    sourceHead=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),
    rawDirectory=str(args.input.resolve()),
    excludedExperiments=['Initial API and interrupted CPU collection before compact-storage policy',
        'Incomplete frozen module replay','SKP replay without its cross-input','Preliminary four-case retention run'],
    hardwareScope='Windows x64 / i7-8700K / 12 logical processors / GTX 1050 Ti',
    binaryObjects='Lossless gzip, addressed by the uncompressed SHA256; includes pixels and latency ticks')
(args.output/'scope.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
hashes={path.relative_to(args.output).as_posix():hashlib.sha256(path.read_bytes()).hexdigest() for path in args.output.rglob('*') if path.is_file()}
(args.output/'sha256.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8')
print('Archived',len(hashes),'files and',len(frames),'raw-output references')
