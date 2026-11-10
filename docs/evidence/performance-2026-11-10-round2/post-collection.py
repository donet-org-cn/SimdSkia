import json,pathlib,subprocess,sys,time,os
root=pathlib.Path.cwd();base=root/'artifacts/performance-2026-11-10-round2/final';dotnet=root/'artifacts/dotnet-sdk-10.0.403/dotnet.exe'
os.environ['PATH']=str(dotnet.parent)+os.pathsep+os.environ['PATH'];os.environ['DOTNET_ROOT']=str(dotnet.parent);os.environ['PYTHONIOENCODING']='utf-8';os.environ['DOTNET_CLI_UI_LANGUAGE']='en'
rows=[]
def run(name,cmd,accepted=(0,)):
 print('START',name,flush=True);start=time.time()
 with (base/(name+'.log')).open('wb') as log:p=subprocess.run([str(x) for x in cmd],cwd=root,stdout=log,stderr=subprocess.STDOUT)
 rows.append(dict(name=name,command=[str(x) for x in cmd],exitCode=p.returncode,seconds=time.time()-start));(base/'final-validation-status.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
 print('END',name,p.returncode,flush=True)
 if p.returncode not in accepted:raise RuntimeError(name)
run('build-solution-final',[dotnet,'build','SimdSkia.sln','-c','Release','-m:1','-p:UsePackedSimdSkia=false'])
run('validation-final',[sys.executable,'tools/run-performance-validation.py','--output',base/'validation-final'],(0,2))
run('old-engine-replay-final',[sys.executable,'tools/check-optimization-regressions.py','--validation',base/'validation-final','--before-engine',root/'artifacts/performance-2026-11-10-round2/before/Managed','--output',base/'old-engine-replay-final','--include-passing-shaders'])
print('FINAL VALIDATION COMPLETE',flush=True)
