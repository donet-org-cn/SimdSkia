"""Check the published sampler's PE and live modules, then run its pixel suite."""
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('aot_runner',ROOT/'tools/run-avalonia-aot.py')
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--exe',type=Path,required=True)
p.add_argument('--output',type=Path,required=True)
args=p.parse_args()
digest=runner.native_pe(args.exe)
child=subprocess.Popen([str(args.exe.resolve()),'--inspect'],cwd=ROOT,
    stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,
    creationflags=subprocess.CREATE_NO_WINDOW)
try:
    first=child.stdout.readline().strip()
    if first!='READY':raise RuntimeError('Sampler did not reach the inspection barrier: '+first)
    script=f"@((Get-Process -Id {child.pid}).Modules | ForEach-Object {{ $_.ModuleName }}) | ConvertTo-Json -Compress"
    modules=json.loads(subprocess.check_output(['powershell','-NoProfile','-Command',script],text=True))
    forbidden={'coreclr.dll','clrjit.dll','clr.dll','libskiasharp.dll','libharfbuzzsharp.dll'}
    assert not forbidden.intersection(m.lower() for m in modules),'JIT/native reference module detected'
    output,_=child.communicate('\n',timeout=180)
    assert child.returncode==0 and 'NativeAot=True' in output and 'PASS ' in output,output
    runner.save_json(args.output,dict(executable=str(args.exe.resolve()),sha256=digest,loadedModules=modules,output=output,passed=True))
    print(output,end='');print('PASS absent CLR header and absent CLR/JIT/reference modules')
finally:
    if child.poll() is None:child.kill();child.wait()
