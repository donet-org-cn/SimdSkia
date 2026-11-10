import gzip,hashlib,json,os,pathlib,shutil,subprocess
root=pathlib.Path.cwd();base=root/'artifacts/performance-2026-11-10-round2/final';dest=root/'docs/evidence/performance-2026-11-10-round2';dest.mkdir(parents=True,exist_ok=False)
stages=['cpu-formal','api-formal','codec-formal','gpu-micro-formal','gpu-pages-formal','aot-pages-formal','retention-verified','aot-scroll-confirmation','system-font-controls','validation-final','old-engine-replay-final']
text={'.json','.csv','.log','.txt','.rsp','.py'};binary={'.rgba','.bgra','.bin','.ticks','.png','.jpg','.jpeg','.webp','.skp','.svg','.pdf','.xps'};frames={}
for stage in stages:
 for directory,names,files in os.walk(base/stage):
  names[:]=[name for name in names if name not in ('browser-profiles','workers','bin','obj')]
  for name in files:
   path=pathlib.Path(directory)/name;relative=path.relative_to(base)
   if path.suffix in text:
    out=dest/relative;out.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,out)
   elif path.suffix in binary:
    data=path.read_bytes();digest=hashlib.sha256(data).hexdigest();obj=pathlib.Path('objects')/(digest+'.gz');out=dest/obj
    if not out.exists():out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(gzip.compress(data,compresslevel=6,mtime=0))
    frames[relative.as_posix()]=dict(object=obj.as_posix(),length=len(data),sha256=digest)
for path in base.iterdir():
 if path.is_file() and path.suffix in text:shutil.copyfile(path,dest/path.name)
for test in ['numeric','sampling-portable','sampling-v3','completion']:
 folder=base/'aot-tests'/test
 for path in folder.rglob('*.json'):
  if path.parent.name in ('results','scalar'):
   out=dest/'native-tests'/test/path.relative_to(folder);out.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,out)
for name in ['collect.py','post-collection.py','focused.py','archive.py']:
 path=base.parent/name
 if path.exists():shutil.copyfile(path,dest/name)
harness=dest/'harness';harness.mkdir()
for name in ['run-performance-validation.py','check-optimization-regressions.py','run-cpu-optimization.py','run-final-api-benchmarks.py','run-codec-skia-comparison.py','run-gpu-slow-cases.py','run-gpu-parity.py','run-avalonia-aot.py','run-decode-retention.py','report-performance-round2.py']:
 shutil.copyfile(root/'tools'/name,harness/name)
source={}
for folder,names,files in os.walk(root/'src'):
 names[:]=[name for name in names if name not in ('bin','obj')]
 for name in files:
  path=pathlib.Path(folder)/name
  if path.suffix in ('.cs','.csproj','.comp','.spv'):source[path.relative_to(root).as_posix()]=hashlib.sha256(path.read_bytes()).hexdigest()
(dest/'source-sha256.json').write_text(json.dumps(source,indent=2)+'\n',encoding='utf-8')
(dest/'frame-objects.json').write_text(json.dumps(frames,indent=2)+'\n',encoding='utf-8')
(dest/'working-tree.diff').write_bytes(subprocess.check_output(['git','diff','--binary']))
metadata=dict(scope='Second working-tree optimization after the preceding 2026-11-10 round; source assembly version 0.1.4, SkiaSharp 3.119.4, Avalonia 12.1.5',performanceProcesses=318,regressionCommands=85,passedCommands=59,originalGateOrAuditNonSuccess=26,hardware='Windows x64 / i7-8700K / 12 logical processors / GTX 1050 Ti',rawDirectory=str(base),sourceHead=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),before='Frozen full engine DLLs under artifacts/performance-2026-11-10-round2/before; previous local Native AOT publish',excludedExperiments=['Preliminary API and single-fork profile runs','Coordinate vectorization and affine branching candidates','PNG internal unfilter API candidate','Initial mixed-build investigation validation','Initial replay treating oracle-generated PNGs as worker outputs'],sourceSnapshotNote='All 333 implementation files match timing and final validation. Initial AOT snapshot additionally retained an empty net10.0/win-x64 PDF lock entry, reconstructed with exact SHA256. AOT UI has no PDF dependency.',byteObjects='Lossless gzip, named by raw SHA256')
(dest/'scope.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
hashes={path.relative_to(dest).as_posix():hashlib.sha256(path.read_bytes()).hexdigest() for path in dest.rglob('*') if path.is_file()}
(dest/'sha256.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf-8')
print('Archived',len(hashes),'files,',len(frames),'raw outputs,',sum(x.stat().st_size for x in dest.rglob('*') if x.is_file())/1048576,'MiB')
