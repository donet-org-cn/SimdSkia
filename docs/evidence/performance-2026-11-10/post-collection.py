import hashlib,json,os,pathlib,statistics,subprocess,sys
root=pathlib.Path.cwd();base=root/'artifacts/performance-2026-11-10/final'
dotnet=str(root/'artifacts/dotnet-sdk-10.0.403/dotnet.exe')
os.environ['PATH']=str(pathlib.Path(dotnet).parent)+os.pathsep+os.environ['PATH'];os.environ['DOTNET_ROOT']=str(pathlib.Path(dotnet).parent)
os.environ['DOTNET_CLI_UI_LANGUAGE']='en';os.environ['PYTHONIOENCODING']='utf-8'
status_path=base/'post-status.json';status=json.loads(status_path.read_text(encoding='utf-8')) if status_path.exists() else []
def run(name,command,accepted=(0,)):
    if any(row['name']==name and row['exitCode'] in accepted for row in status):return
    print('START',name,flush=True)
    with (base/(name+'.log')).open('wb') as log:r=subprocess.run([str(x) for x in command],stdout=log,stderr=subprocess.STDOUT)
    status.append(dict(name=name,command=[str(x) for x in command],exitCode=r.returncode));status_path.write_text(json.dumps(status,indent=2)+'\n',encoding='utf-8')
    print('END',name,r.returncode,flush=True)
    if r.returncode not in accepted:raise RuntimeError(name)
frozen=root/'artifacts/performance-2026-11-10/before-workers/benchmarks'
for name,project,props in [('before','Managed',['-p:FrozenEngineDirectory='+str(frozen/'Managed')]),('current','Managed',[]),('original','Original',[])]:
    run('retention-mixed-build-'+name,[dotnet,'build','benchmarks/DecodeRetention/'+project,'-c','Release','-m:1','-p:RestoreForce=true','-o',base/'retention-mixed-workers'/name,*props])
run('retention-verified',[sys.executable,'tools/run-decode-retention.py','--output',base/'retention-verified','--fixtures',base/'retention-fixtures','--before',base/'retention-mixed-workers/before/Managed.dll','--current',base/'retention-mixed-workers/current/Managed.dll','--original',base/'retention-mixed-workers/original/Original.dll'])
run('old-engine-replay-verified',[sys.executable,'tools/check-optimization-regressions.py','--validation',base/'validation','--before-engine',frozen/'Managed','--output',base/'old-engine-replay'],(0,2))

out=base/'gpu-chinese-confirmation';out.mkdir(exist_ok=False)
workers={'before':frozen/'GpuManaged/SimdSkia.Benchmark.GpuManaged.dll','current':root/'benchmarks/GpuManaged/bin/Release/net10.0/SimdSkia.Benchmark.GpuManaged.dll'}
files={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for directory in {p.parent for p in workers.values()} for p in directory.glob('*') if p.is_file() and p.suffix in ['.dll','.json']}
env=dict(os.environ,DOTNET_TieredCompilation='0',DOTNET_ReadyToRun='0',DOTNET_gcServer='0')
rows=[]
for fork in range(6):
    for variant in (['before','current'] if fork%2==0 else ['current','before']):
        dest=out/f'{variant}-{fork}';dest.mkdir()
        with (out/(dest.name+'.log')).open('wb') as log:subprocess.run([dotnet,str(workers[variant]),'--engine','simd','--fork',str(fork),'--out',str(dest),'--font','baseline/fonts/ZCOOLXiaoWei-Regular.ttf','--scenes','chinese-changing','--sizes','1024x768','--samples','7','--sample-ms','500','--warmup-ms','1500'],env=env,stdout=log,stderr=subprocess.STDOUT,check=True)
        data=json.loads((dest/'results.json').read_text(encoding='utf-8-sig'))['Workloads'][0];samples=data['Samples'];ops=sum(s['Operations'] for s in samples)
        rows.append(dict(variant=variant,fork=fork,milliseconds=sum(s['TotalMilliseconds'] for s in samples)/ops,cpuMilliseconds=sum(s['CpuMilliseconds'] for s in samples)/ops,allocation=sum(s['AllocatedBytes'] for s in samples)/ops,workingSetMiB=data['AfterGc']['WorkingSetBytes']/1048576,privateMiB=data['AfterGc']['PrivateBytes']/1048576))
        (out/'runs.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8');print(variant,fork,'done',flush=True)
summary={variant:{key:statistics.median(r[key] for r in rows if r['variant']==variant) for key in ['milliseconds','cpuMilliseconds','allocation','workingSetMiB','privateMiB']} for variant in workers}
summary['afterOverBefore']=summary['current']['milliseconds']/summary['before']['milliseconds']
frames=[p.read_bytes() for p in out.glob('*-*/*.rgba')];assert all(p==frames[0] for p in frames)
for name,digest in files.items():assert hashlib.sha256(pathlib.Path(name).read_bytes()).hexdigest()==digest
(out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
(out/'manifest.json').write_text(json.dumps(dict(hashes=files,forks=6,warmupMilliseconds=1500,samples=7,sampleMilliseconds=500,complete=True),indent=2)+'\n',encoding='utf-8')
print('POST COLLECTION COMPLETE',flush=True)
