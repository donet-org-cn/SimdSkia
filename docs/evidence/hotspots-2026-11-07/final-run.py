import hashlib,json,os,pathlib,subprocess,sys
root=pathlib.Path.cwd();base=root/'artifacts/scroll-opt-2026-11-07';dotnet=str(root/'artifacts/dotnet-sdk-10.0.403/dotnet.exe');os.environ['DOTNET_CLI_UI_LANGUAGE']='en'
status=json.loads((base/'validation-final.json').read_text()) if (base/'validation-final.json').exists() else []
def run(name,cmd,accepted=(0,)):
    if any(s['name']==name and s['exitCode'] in accepted for s in status):print('RESUME',name,flush=True);return
    print('START',name,flush=True)
    with (base/(name+'.log')).open('w',encoding='utf-8') as log:r=subprocess.run(cmd,cwd=root,stdout=log,stderr=subprocess.STDOUT)
    status.append(dict(name=name,command=cmd,exitCode=r.returncode));(base/'validation-final.json').write_text(json.dumps(status,indent=2)+'\n')
    print('END',name,r.returncode,flush=True)
    if r.returncode not in accepted:raise RuntimeError(name)
run('build-final-verified',[dotnet,'build','SimdSkia.sln','-c','Release','-m:1'])
for name,project in [('micro','benchmarks/GpuManaged'),('gpu-page','benchmarks/AvaloniaGpuParity/Managed'),('gpu-reference-page','benchmarks/AvaloniaGpuParity/Original'),('churn','benchmarks/GlyphRunChurn')]:run('build-verified-'+name,[dotnet,'build',project,'-c','Release','--no-restore','-m:1'])
for name,dll,args in [
    ('core','tests/SimdSkia.Tests/bin/Release/net10.0/SimdSkia.Tests.dll',[]),
    ('optimization','tests/Optimization/bin/Release/net10.0/Optimization.dll',['baseline/fonts/ZCOOLXiaoWei-Regular.ttf']),
    ('controls','tests/AvaloniaControls/bin/Release/net10.0/SimdSkia.Avalonia.Controls.Tests.dll',['--frames','5','--output',str(base/'controls-verified')]),
    ('gpu','tests/GpuOptimization/bin/Release/net10.0/SimdSkia.GpuOptimization.Tests.dll',['baseline/fonts/ZCOOLXiaoWei-Regular.ttf']),
    ('gpu-integration','tests/AvaloniaGpu/bin/Release/net10.0/AvaloniaGpu.dll',[]),
    ('gpu-preference','tests/GpuPreferred/bin/Release/net10.0/SimdSkia.GpuPreferred.Tests.dll',['--hardware'])]:run('verify-'+name,[dotnet,dll,*args])
sources={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'src').rglob('*') if p.is_file() and not {'obj','bin'}.intersection(p.parts) and p.suffix in ['.cs','.csproj','.comp','.spv']}
(base/'source-final.json').write_text(json.dumps(sources,indent=2)+'\n')
run('formal-scroll',[sys.executable,'tools/run-scroll-regression.py','--output',str(base/'scroll-final'),'--before',str(base/'source-before-aot/AvaloniaAot.SimdSkia.exe'),'--after',str(base/'source-after-aot/AvaloniaAot.SimdSkia.exe'),'--stable',str(root/'artifacts/blog-preview8-to-012-2026-11-07/aot-publish/simd/AvaloniaAot.SimdSkia.exe'),'--original',str(root/'artifacts/blog-preview8-to-012-2026-11-07/aot-publish/original/AvaloniaAot.Original.exe')])
run('formal-micro',[sys.executable,'tools/run-gpu-slow-cases.py','--output',str(base/'slow-verified'),'--before',str(base/'gpu-micro-before-binary/SimdSkia.Benchmark.GpuManaged.dll')])
run('formal-pages',[sys.executable,'tools/run-gpu-parity.py','--output',str(base/'gpu-pages-final'),'--forks','3','--seconds','5','--warmup','3'],accepted=(0,2))
for fork in range(3):
    for version in (['before','after'] if fork%2==0 else ['after','before']):
        dll=base/'churn-before/GlyphRunChurn.dll' if version=='before' else root/'benchmarks/GlyphRunChurn/bin/Release/net10.0/GlyphRunChurn.dll'
        run(f'churn-verified-{version}-{fork}',[dotnet,str(dll),str(base/f'churn-verified-{version}-{fork}.json')])
for name,digest in sources.items():
    if hashlib.sha256((root/name).read_bytes()).hexdigest()!=digest:raise RuntimeError('Measured source changed: '+name)
print('ALL FINAL COLLECTIONS COMPLETE',flush=True)
