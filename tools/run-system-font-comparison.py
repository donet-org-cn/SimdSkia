"""CPU comparison with the same installed family and audited font tables."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

p=argparse.ArgumentParser()
p.add_argument('--family',required=True,help='Exact installed family available to both engines.')
p.add_argument('--output',type=Path,required=True)
p.add_argument('--frames',type=int,default=600)
p.add_argument('--runs',type=int,default=3)
p.add_argument('--before-directory',type=Path,help='Frozen previous AvaloniaControls build for a measured before/after comparison.')
args=p.parse_args()
if args.runs<1 or args.frames<100:raise SystemExit('Use positive runs and at least 100 frames.')
if (args.output/'manifest.json').exists():raise SystemExit('Choose a fresh output directory.')
args.output.mkdir(parents=True,exist_ok=True)
engines={
    'after':(Path('tests/AvaloniaControls/bin/Release/net8.0/SimdSkia.Avalonia.Controls.Tests.dll'),[]),
    'reference':(Path('tests/AvaloniaControls.Reference/bin/Release/net8.0/AvaloniaControls.Reference.dll'),[]),
    'default':(Path('tests/AvaloniaControls.Reference/bin/Release/net8.0/AvaloniaControls.Reference.dll'),['--native-default-opacity']),
}
if args.before_directory:engines={'before':(args.before_directory/'SimdSkia.Avalonia.Controls.Tests.dll',[]),**engines}
files={path.resolve() for binary,_ in engines.values() for path in binary.parent.glob('*.dll')}
hashes={str(path):hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(files)}
manifest={'scope':'CPU RenderTargetBitmap + real Fluent controls; no GPU or desktop presentation timing',
          'family':args.family,'fontMode':'system','runs':args.runs,'frames':args.frames,
          'runtimeFileSha256':hashes,'fontAudit':'Actual selected normal face glyf/cmap/head/hmtx/OS2 tables, checked after measurement.'}
(args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
audit=None
variants=list(engines)
for run in range(args.runs):
    for name in variants[run%len(variants):]+variants[:run%len(variants)]:
        binary,extra=engines[name]
        dest=args.output/f'{name}-controls-dpi1-fork{run}';dest.mkdir()
        command=['dotnet',str(binary),'--bench','--system-font-family',args.family,'--frames',str(args.frames),'--output',str(dest),*extra]
        with (dest/'process.log').open('wb') as log:
            result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,timeout=90)
        if result.returncode:raise SystemExit(f'Worker failed: {dest}/process.log')
        actual=json.loads((dest/'font-tables.json').read_text(encoding='utf-8'))
        if len(actual)!=5:raise SystemExit('Font audit is missing required outline/metric tables.')
        if audit is None:audit=actual
        elif actual!=audit:raise SystemExit('The native and managed engines selected different font tables.')
        row=json.loads((dest/'metrics.json').read_text(encoding='utf-8'))
        print(f"{name} #{run+1}: {row['millisecondsPerFrame']:.3f}ms; WS={row['workingSetMiB']:.1f}MiB; private={row['privateMiB']:.1f}MiB",flush=True)
for path,digest in hashes.items():
    if hashlib.sha256(Path(path).read_bytes()).hexdigest()!=digest:raise SystemExit('A runtime file changed during measurement.')
(args.output/'font-tables.json').write_text(json.dumps(audit,indent=2)+'\n',encoding='utf-8')
raise SystemExit(subprocess.run([sys.executable,'tools/report-avalonia-controls.py',str(args.output)]).returncode)
