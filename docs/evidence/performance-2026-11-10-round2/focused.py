import importlib.util,json,hashlib,os,pathlib,shutil,statistics,subprocess,sys
root=pathlib.Path.cwd();base=root/'artifacts/performance-2026-11-10-round2/final';previous=root/'artifacts/performance-2026-11-10/final'
sdk=root/'artifacts/dotnet-sdk-10.0.403';os.environ['PATH']=str(sdk)+os.pathsep+os.environ['PATH'];os.environ['DOTNET_ROOT']=str(sdk);os.environ['PYTHONIOENCODING']='utf-8'
spec=importlib.util.spec_from_file_location('aot_focus',root/'tools/run-avalonia-aot.py');runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
runner.CASES=(('scroll',1,'paced'),)
sys.argv=['run-avalonia-aot.py','--no-build','--forks','6','--seconds','7.5','--warmup','3','--publish',str(base/'aot-publish'),'--before-simd',str(previous/'aot-publish/simd/AvaloniaAot.SimdSkia.exe'),'--output',str(base/'aot-scroll-confirmation')]
code=runner.main()
if code not in (0,2):raise RuntimeError('Focused AOT run failed')
current=root/'tests/AvaloniaControls/bin/Release/net10.0';original=root/'tests/AvaloniaControls.Reference/bin/Release/net10.0';frozen=base/'system-font-before-worker';shutil.copytree(current,frozen)
for folder in ['Managed','AvaloniaGpuParity/Managed']:
 for file in (root/'artifacts/performance-2026-11-10-round2/before'/folder).glob('SimdSkia*.dll'):shutil.copy2(file,frozen/file.name)
programs={'original-default':(original/'AvaloniaControls.Reference.dll',['--native-default-opacity']),'original-full':(original/'AvaloniaControls.Reference.dll',[]),'before':(frozen/'SimdSkia.Avalonia.Controls.Tests.dll',[]),'current':(current/'SimdSkia.Avalonia.Controls.Tests.dll',[])}
output=base/'system-font-controls';output.mkdir();runs=[]
hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for p,_ in programs.values() for f in p.parent.glob('*.dll')}
for fork in range(3):
 order=list(programs);order=order[fork:]+order[:fork]
 for variant in order:
  program,extra=programs[variant];dest=output/f'{variant}-{fork}'
  cmd=[str(sdk/'dotnet.exe'),str(program),'--bench','--system-font-family','Microsoft YaHei UI','--frames','6000','--output',str(dest),*extra]
  with (output/(dest.name+'.log')).open('wb') as log:subprocess.run(cmd,cwd=root,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=120)
  data=json.loads((dest/'metrics.json').read_text(encoding='utf-8-sig'));runs.append(dict(variant=variant,fork=fork,**data));(output/'runs.json').write_text(json.dumps(runs,indent=2)+'\n',encoding='utf-8');print('system font',variant,fork,data['millisecondsPerFrame'],data['privateMiB'],flush=True)
summary=[]
for variant in programs:
 group=[r for r in runs if r['variant']==variant]
 summary.append(dict(variant=variant,**{k:statistics.median(r[k] for r in group) for k in ['millisecondsPerFrame','cpuMillisecondsPerFrame','cpuPercentOneCore','allocatedBytesPerFrame','workingSetMiB','privateMiB','managedRetainedAfterGcMiB']}))
frames={v:(output/f'{v}-0/scene.rgba').read_bytes() for v in programs}
assert frames['before']==frames['current'],'System-font optimization changed pixels'
font_tables=[json.loads((output/f'{v}-{fork}/font-tables.json').read_text(encoding='utf-8-sig')) for v in programs for fork in range(3)]
assert all(t==font_tables[0] for t in font_tables),'Different actual font tables'
for path,digest in hashes.items():assert hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()==digest,'Measured binaries changed'
quality=[]
for variant in ['original-default','original-full']:
 delta=[abs(x-y) for x,y in zip(frames['current'],frames[variant])];maximum=max(delta);rmse=(sum(d*d for d in delta)/len(delta))**.5
 quality.append(dict(reference=variant,maximum=maximum,rmse=rmse,passed=maximum<=32 and rmse<=2))
for filename,data in [('summary.json',summary),('quality.json',quality),('manifest.json',dict(complete=True,scope='Windows JIT CPU offscreen control tree, installed Microsoft YaHei UI, 1000x720, 6000 measured frames after 2.5s warmup, three rotating forks',hashes=hashes,fontTables=font_tables[0],beforeAfterExact=True))]:
 (output/filename).write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
print('FOCUSED COLLECTION COMPLETE',flush=True)
