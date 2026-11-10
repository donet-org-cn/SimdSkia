import hashlib,importlib.util,json,os,pathlib,subprocess,sys,time
root=pathlib.Path.cwd();base=root/'artifacts/performance-2026-11-10/final';base.mkdir(parents=True,exist_ok=True)
dotnet=str(root/'artifacts/dotnet-sdk-10.0.403/dotnet.exe')
os.environ['PATH']=str(pathlib.Path(dotnet).parent)+os.pathsep+os.environ['PATH']
os.environ['DOTNET_ROOT']=str(pathlib.Path(dotnet).parent)
os.environ['DOTNET_CLI_UI_LANGUAGE']='en';os.environ['PYTHONIOENCODING']='utf-8'
status_path=base/'collection-status.json';status=json.loads(status_path.read_text(encoding='utf-8')) if status_path.exists() else []
def run(name,command,accepted=(0,)):
    if any(row['name']==name and row['exitCode'] in accepted for row in status):return
    print('START',name,flush=True);begin=time.time()
    with (base/(name+'.log')).open('wb') as log:r=subprocess.run([str(x) for x in command],cwd=root,stdout=log,stderr=subprocess.STDOUT)
    status.append(dict(name=name,command=[str(x) for x in command],exitCode=r.returncode,seconds=time.time()-begin))
    status_path.write_text(json.dumps(status,indent=2)+'\n',encoding='utf-8');print('END',name,r.returncode,flush=True)
    if r.returncode not in accepted:raise RuntimeError(name)

frozen=root/'artifacts/performance-2026-11-10/before-workers/benchmarks'
run('codec-before-build',[dotnet,'build','benchmarks/CodecSkiaComparison/Managed','-c','Release','-m:1',
    '-p:FrozenEngineDirectory='+str(frozen/'Managed'),'-p:RestoreForce=true','-o',base/'codec-before-worker'])
for project in ['Managed','Reference','Api99/Managed','Api99/Original','CodecSkiaComparison/Managed','CodecSkiaComparison/Original','GpuManaged','GpuReference','AvaloniaGpuParity/Managed','AvaloniaGpuParity/Original']:
    run('build-'+project.replace('/','-'),[dotnet,'build','benchmarks/'+project,'-c','Release','-m:1','-p:FrozenEngineDirectory=','-p:RestoreForce=true'])

for name,project,props in [('before','Managed',['-p:FrozenEngineDirectory='+str(frozen/'Managed')]),('current','Managed',[]),('original','Original',[])]:
    run('retention-build-'+name,[dotnet,'build','benchmarks/DecodeRetention/'+project,'-c','Release','-m:1','-p:RestoreForce=true','-o',base/'retention-workers'/name,*props])
run('retention-fixtures',[dotnet,'benchmarks/Reference/bin/Release/net10.0/SimdSkia.Benchmark.Reference.dll','--make-fixtures','--fixtures',base/'retention-fixtures','--font','baseline/fonts/ZCOOLXiaoWei-Regular.ttf','--sizes','256x256,1024x768,1025x1025,1920x1080'])
run('retention-formal',[sys.executable,'tools/run-decode-retention.py','--output',base/'retention-formal','--fixtures',base/'retention-fixtures','--before',base/'retention-workers/before/Managed.dll','--current',base/'retention-workers/current/Managed.dll','--original',base/'retention-workers/original/Original.dll'])
run('api-formal',[sys.executable,'tools/run-final-api-benchmarks.py','--output',base/'api-formal','--before',frozen/'Api99/Managed/Managed.dll'])
run('cpu-formal',[sys.executable,'tools/run-cpu-optimization.py','--output',base/'cpu-formal','--before',frozen/'Managed/SimdSkia.Benchmark.Managed.dll'])
run('codec-formal',[sys.executable,'tools/run-codec-skia-comparison.py','--output',base/'codec-formal','--before',base/'codec-before-worker/Managed.exe'])
run('gpu-micro-formal',[sys.executable,'tools/run-gpu-slow-cases.py','--output',base/'gpu-micro-formal','--before',frozen/'GpuManaged/SimdSkia.Benchmark.GpuManaged.dll'])
run('gpu-pages-formal',[sys.executable,'tools/run-gpu-parity.py','--output',base/'gpu-pages-formal','--before-managed',frozen/'AvaloniaGpuParity/Managed/Managed.exe'],(0,2))

for name,project,props in [
    ('before','SimdSkia',['-p:FrozenEngineDirectory='+str(frozen/'AvaloniaGpuParity/Managed'),'-p:NuGetLockFilePath='+str(base/'aot-before-lock.json')]),
    ('simd','SimdSkia',[]),('original','Original',[])]:
    run('publish-aot-'+name,[dotnet,'publish',f'examples/AvaloniaAot/{project}/AvaloniaAot.{project}.csproj','-c','Release','-m:1',
        '-p:UsePackedSimdSkia=false','-p:RestoreForce=true','-o',base/'aot-publish'/name,*props])
    native=root/f'examples/AvaloniaAot/{project}/obj/Release/net10.0/win-x64/native'
    if native.exists():
        for rsp in native.glob('*.rsp'):(base/('ilc-'+name+'-'+rsp.name)).write_bytes(rsp.read_bytes())

run('aot-pages-formal',[sys.executable,'tools/run-avalonia-aot.py','--output',base/'aot-pages-formal','--publish',base/'aot-publish','--no-build',
    '--before-simd',base/'aot-publish/before/AvaloniaAot.SimdSkia.exe'],(0,2))
for name,project,props in [
    ('sampling-portable','SamplingAot',[]),('sampling-v3','SamplingAot',['-p:IlcInstructionSet=x86-64-v3']),
    ('completion','CompletionAot',[]),('codec','CodecIntegration',['-p:NativeAotTest=true'])]:
    dest=base/'aot-tests'/name
    run('publish-test-'+name,[dotnet,'publish','tests/'+project,'-c','Release','-r','win-x64','-m:1','-p:RestoreForce=true','-o',dest,*props])
    run('native-test-'+name,[dest/(project+'.exe'),dest/'results'])
    if name=='completion':run('native-test-completion-scalar',[dest/(project+'.exe'),dest/'scalar','--scalar'])
run('build-final-solution',[dotnet,'build','SimdSkia.sln','-c','Release','-m:1','-p:UsePackedSimdSkia=false'])
run('validation-final',[sys.executable,'tools/run-performance-validation.py','--output',base/'validation'],(0,2))
print('ALL COLLECTIONS COMPLETE',flush=True)
