"""Record the unchanged 1-LSB JPEG gate; permit only disclosed preview.10 exceptions when explicitly requested."""
import argparse
import json
import os
from pathlib import Path
import subprocess

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--worker',type=Path,default=Path('tests/Imaging/bin/Release/net10.0/Imaging.dll'))
p.add_argument('--output',type=Path,required=True)
p.add_argument('--allow-known-preview10','--check-documented-differences',dest='allow_known_preview10',action='store_true')
args=p.parse_args();args.output.mkdir(parents=True,exist_ok=True)
actual_path=args.output.resolve()/'actual.json'
subprocess.run(['dotnet',str(args.worker),'--encode-probe',str(actual_path)],check=True)
actual=json.loads(actual_path.read_text(encoding='utf-8'))
expected=json.loads(Path('tests/Imaging/jpeg-encode-native.json').read_text(encoding='utf-8'))
assert len(actual)==len(expected)==24,'Unexpected JPEG probe count'
known={('Opaque',32,'erase'),('Premul',32,'erase'),('Premul',32,'raw')}
rows=[]
for a,e in zip(actual,expected):
    identity=tuple(a[k] for k in ('alpha','a','storage'))
    assert identity==tuple(e[k] for k in ('alpha','a','storage')),'JPEG cases reordered'
    error=max(abs(a[k]-e[k]) for k in ('r','g','b','outAlpha'))
    passed=error<=1
    # A release exception is NOT a changed quality threshold. Store failure,
    # reject new affected cases, RGB changes outside the observed delta, and
    # output-alpha regressions even when preview authorization is supplied.
    approved=not passed and identity in known and all(a[k]==e[k] for k in ('r','g','outAlpha')) and a['b']==e['b']+2
    rows.append({'actual':a,'expected':e,'maximumChannelError':error,'qualityPassed':passed,'knownPreview10Exception':approved})
failures=[r for r in rows if not r['qualityPassed']]
accepted=args.allow_known_preview10 and all(r['knownPreview10Exception'] for r in failures)
result={'threshold':1,'qualityPassed':not failures,'passedCases':24-len(failures),'totalCases':24,
        'releaseExceptionExplicitlyEnabled':args.allow_known_preview10,'knownExceptionsOnly':accepted,
        'releaseCheckPassed':not failures or accepted,'rows':rows}
(args.output/'result.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(f"JPEG QUALITY {'PASS' if not failures else 'FAIL'}: {24-len(failures)}/24, threshold=1; releaseException={accepted}",flush=True)
if failures:
    print('WARNING: disclosed JPEG quality failures remain failures; this release does not claim pixel parity.',flush=True)
summary=os.environ.get('GITHUB_STEP_SUMMARY')
if summary:
    with open(summary,'a',encoding='utf-8') as f:
        f.write(f"\nJPEG strict quality: **{'FAIL' if failures else 'PASS'}**, {24-len(failures)}/24 at the unchanged 1-LSB threshold. "
                f"Documented pixel-difference regression check accepted: **{accepted}**. "
                "See the uploaded JSON; this is not full image quality or rendering parity.\n")
raise SystemExit(0 if result['releaseCheckPassed'] else 2)
