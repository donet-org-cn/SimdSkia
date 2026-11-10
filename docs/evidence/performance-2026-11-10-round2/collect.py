import json,os,pathlib,subprocess,sys,time,shutil
root=pathlib.Path.cwd();base=root/'artifacts/performance-2026-11-10-round2/final';base.mkdir(parents=True,exist_ok=True)
before=root/'artifacts/performance-2026-11-10-round2/before';previous=root/'artifacts/performance-2026-11-10/final'
dotnet=root/'artifacts/dotnet-sdk-10.0.403/dotnet.exe'
os.environ.update(DOTNET_ROOT=str(dotnet.parent),DOTNET_CLI_UI_LANGUAGE='en',PYTHONIOENCODING='utf-8')
os.environ['PATH']=str(dotnet.parent)+os.pathsep+os.environ['PATH']
status=[];status_path=base/'collection-status.json'
def run(name,command,accepted=(0,)):
 print('START',name,flush=True);start=time.time()
 with (base/(name+'.log')).open('wb') as log:r=subprocess.run([str(x) for x in command],cwd=root,stdout=log,stderr=subprocess.STDOUT)
 status.append(dict(name=name,command=[str(x) for x in command],exitCode=r.returncode,seconds=time.time()-start))
 status_path.write_text(json.dumps(status,indent=2)+'\n',encoding='utf-8');print('END',name,r.returncode,flush=True)
 if r.returncode not in accepted:raise RuntimeError(name)
run('validation',[sys.executable,'tools/run-performance-validation.py','--output',base/'validation'],(0,2))
run('replay',[sys.executable,'tools/check-optimization-regressions.py','--validation',base/'validation','--before-engine',before/'Managed','--output',base/'old-engine-replay'])
for name,project,props in [('numeric','RuntimeNumeric',['-p:PublishAot=true']),('sampling-portable','SamplingAot',[]),('sampling-v3','SamplingAot',['-p:IlcInstructionSet=x86-64-v3']),('completion','CompletionAot',[])]:
 dest=base/'aot-tests'/name
 run('publish-test-'+name,[dotnet,'publish','tests/'+project,'-c','Release','-r','win-x64','-m:1','-p:RestoreForce=true','-o',dest,*props])
 exe='SimdSkia.RuntimeNumeric.Tests' if name=='numeric' else project
 run('native-test-'+name,[dest/(exe+'.exe'),dest/'results'])
 if name=='completion':run('native-test-completion-scalar',[dest/(exe+'.exe'),dest/'scalar','--scalar'])
run('publish-aot-simd',[dotnet,'publish','examples/AvaloniaAot/SimdSkia/AvaloniaAot.SimdSkia.csproj','-c','Release','-m:1','-p:UsePackedSimdSkia=false','-p:FrozenEngineDirectory=','-p:RestoreForce=true','-o',base/'aot-publish/simd'])
shutil.copytree(previous/'aot-publish/original',base/'aot-publish/original')
for project in ['Managed','Reference','Api99/Managed','Api99/Original','CodecSkiaComparison/Managed','CodecSkiaComparison/Original','GpuManaged','GpuReference','AvaloniaGpuParity/Managed','AvaloniaGpuParity/Original']:
 run('build-'+project.replace('/','-'),[dotnet,'build','benchmarks/'+project,'-c','Release','-m:1','-p:FrozenEngineDirectory=','-p:RestoreForce=true'])
run('api-formal',[sys.executable,'tools/run-final-api-benchmarks.py','--output',base/'api-formal','--before',before/'Api99/Managed/Managed.dll'])
run('cpu-formal',[sys.executable,'tools/run-cpu-optimization.py','--output',base/'cpu-formal','--before',before/'Managed/SimdSkia.Benchmark.Managed.dll'])
run('codec-formal',[sys.executable,'tools/run-codec-skia-comparison.py','--output',base/'codec-formal','--before',before/'CodecSkiaComparison/Managed/Managed.exe'])
run('gpu-micro-formal',[sys.executable,'tools/run-gpu-slow-cases.py','--output',base/'gpu-micro-formal','--before',before/'GpuManaged/SimdSkia.Benchmark.GpuManaged.dll'])
run('gpu-pages-formal',[sys.executable,'tools/run-gpu-parity.py','--output',base/'gpu-pages-formal','--before-managed',before/'AvaloniaGpuParity/Managed/Managed.exe'],(0,2))
run('aot-pages-formal',[sys.executable,'tools/run-avalonia-aot.py','--output',base/'aot-pages-formal','--publish',base/'aot-publish','--no-build','--before-simd',previous/'aot-publish/simd/AvaloniaAot.SimdSkia.exe'],(0,2))
for name,project,props in [('before','Managed',['-p:FrozenEngineDirectory='+str(before/'Managed')]),('current','Managed',[]),('original','Original',[])]:
 run('retention-build-'+name,[dotnet,'build','benchmarks/DecodeRetention/'+project,'-c','Release','-m:1','-p:RestoreForce=true','-o',base/'retention-workers'/name,*props])
run('retention-verified',[sys.executable,'tools/run-decode-retention.py','--output',base/'retention-verified','--fixtures',previous/'retention-fixtures','--before',base/'retention-workers/before/Managed.dll','--current',base/'retention-workers/current/Managed.dll','--original',base/'retention-workers/original/Original.dll'])
print('ALL COLLECTIONS COMPLETE',flush=True)
