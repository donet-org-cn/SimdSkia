"""Isolated SkiaSharp 3.119.4 versus the current full SimdSkia codec adapter.

Build both benchmarks/CodecSkiaComparison projects first. All child processes
run serially; three alternating forks, owned RGBA outputs, no timed file IO.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import statistics
import subprocess

ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--output',type=Path,required=True)
p.add_argument('--fixtures',type=Path,default=ROOT/'artifacts/simdcodecs-evaluation-2026-11-01/fixtures')
p.add_argument('--forks',type=int,default=3)
p.add_argument('--seconds',type=float,default=2)
args=p.parse_args()
if args.forks<1 or not .1<=args.seconds<=60:raise ValueError('Invalid measurement settings')
out=args.output.resolve();out.mkdir(parents=True,exist_ok=False)
fixtures=args.fixtures.resolve()
programs={key:ROOT/f'benchmarks/CodecSkiaComparison/{folder}/bin/Release/net10.0/{folder}.exe'
          for key,folder in [('original','Original'),('simd','Managed')]}
operations=['decode-jpeg444','decode-jpeg420','decode-png','encode-jpeg444','encode-png']
def save(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def invoke(key,command,scene,operation,destination):
    result=subprocess.run([str(programs[key]),command,str(fixtures),scene,operation,str(destination),str(args.seconds)],
                          cwd=ROOT,capture_output=True,timeout=120,creationflags=0x08000000 if os.name=='nt' else 0)
    destination.with_suffix(destination.suffix+'.log').write_bytes(result.stdout+result.stderr)
    if result.returncode:raise RuntimeError(f'{key} failed; see {destination}.log')
    return result.stdout.decode('utf-8',errors='replace').strip()
files={str(f):digest(f) for exe in programs.values() for f in exe.parent.rglob('*')
       if f.is_file() and f.suffix in ('.exe','.dll','.json')}
source={str(f.relative_to(ROOT)):digest(f) for directory in [ROOT/'src',ROOT/'benchmarks/CodecSkiaComparison']
        for f in directory.rglob('*.cs') if not {'bin','obj'}.intersection(f.parts)}
manifest={'method':__doc__,'seconds':args.seconds,'warmupSeconds':1,'forks':args.forks,'complete':False,
          'runtimeSha256':files,'sourceSha256':source,
          'fixtureSha256':{f.name:digest(f) for f in fixtures.iterdir() if f.is_file()},
          'environment':{k:os.environ.get(k) for k in ['DOTNET_TieredCompilation','DOTNET_TieredPGO','DOTNET_EnableHWIntrinsic']},
          'jpeg':'quality 90, 4:4:4 baseline, owned encoded byte array',
          'png':'lossless RGBA; original AllFilters/zlib6, SimdSkia public Optimal/serial adapter; compressed size reported',
          'memory':'working/private set snapshots after timing, peak WS includes warmup; managed allocation excludes native malloc'}
save(out/'manifest.json',manifest)
rows=[]
for fork in range(args.forks):
    for scene in ['ui','texture']:
        for operation in operations:
            for key in (list(programs) if fork%2==0 else list(reversed(programs))):
                dest=out/f'{scene}-{operation}-{key}-{fork}.json'
                print(invoke(key,'run',scene,operation,dest),flush=True)
                row=json.loads(dest.read_text());row.update(variant=key,fork=fork);rows.append(row)
                assert not row['nativeAot'] and row['runtime'].startswith('.NET 10.')
                assert row['outputBytes']>0 and row['wallMs']>=args.seconds*1000
                save(out/'runs.json',rows)
summary=[]
for scene in ['ui','texture']:
    for operation in operations:
        entry={'scene':scene,'operation':operation}
        matching=[r for r in rows if r['scene']==scene and r['operation']==operation]
        assert len({r['inputSha256'] for r in matching})==1,'Different compressed/source input'
        for key in programs:
            group=[r for r in matching if r['variant']==key]
            entry[key]={k:statistics.median(r[k] for r in group) for k in
                ['millisecondsPerImage','cpuMillisecondsPerImage','cpuPercentMachine','allocatedBytesPerImage',
                 'workingSetMiB','privateMiB','peakWorkingSetMiB','outputBytes']}
            entry[key]['rangeMs']=[min(r['millisecondsPerImage'] for r in group),max(r['millisecondsPerImage'] for r in group)]
        entry['simdTimeRatio']=entry['simd']['millisecondsPerImage']/entry['original']['millisecondsPerImage']
        summary.append(entry)
save(out/'summary.json',summary)

# Captures and cross-decoding happen AFTER all timing. One common native decoder
# evaluates both encoders, so encoder differences aren't conflated with decoders.
captures=out/'quality';captures.mkdir();quality=[]
def compare(a,b):
    if len(a)!=len(b):raise ValueError('Different image dimensions')
    maximum=0;squared=0
    for x,y in zip(a,b):d=abs(x-y);maximum=max(maximum,d);squared+=d*d
    rmse=math.sqrt(squared/len(a))
    return {'maximumChannelError':maximum,'rmseRgba':rmse,
            'psnrRgb':None if not rmse else 20*math.log10(255/(rmse*math.sqrt(4/3)))}
def sampling(data):
    pos=2
    while pos<len(data):
        if data[pos]!=255:raise ValueError('Invalid JPEG marker')
        while data[pos]==255:pos+=1
        marker=data[pos];pos+=1
        if marker in (0xD8,0xD9):continue
        length=int.from_bytes(data[pos:pos+2],'big');payload=data[pos+2:pos+length];pos+=length
        if marker in (0xC0,0xC1,0xC2):
            return {'sof':marker,'components':[payload[7+i*3] for i in range(payload[5])]}
    raise ValueError('Missing JPEG SOF')
for scene in ['ui','texture']:
    raw=(fixtures/f'{scene}.rgba').read_bytes()
    for operation in operations:
        images={}
        for key in programs:
            extension='.rgba' if operation.startswith('decode-') else ('.png' if operation.endswith('png') else '.jpg')
            dest=captures/f'{scene}-{operation}-{key}{extension}'
            invoke(key,'capture',scene,operation,dest)
            if extension!='.rgba':
                decoded=dest.with_suffix(extension+'.rgba')
                subprocess.run([str(programs['original']),'decode-file',str(dest),str(decoded)],cwd=ROOT,check=True,
                               capture_output=True,creationflags=0x08000000 if os.name=='nt' else 0)
                images[key]=decoded.read_bytes()
                quality.append({'kind':'encode','scene':scene,'operation':operation,'variant':key,
                                'outputBytes':dest.stat().st_size,**compare(raw,images[key]),
                                'jpegHeader':sampling(dest.read_bytes()) if extension=='.jpg' else None})
            else:images[key]=dest.read_bytes()
        delta=compare(images['original'],images['simd'])
        lossless=operation.endswith('png')
        exact=all(images[key]==raw for key in images) if lossless else None
        if lossless and not exact:raise ValueError('Lossless PNG changed pixels')
        if operation=='encode-jpeg444':
            for key in programs:
                header=sampling((captures/f'{scene}-{operation}-{key}.jpg').read_bytes())
                assert header=={'sof':192,'components':[17,17,17]},'JPEG mode differs'
        quality.append({'kind':'pair','scene':scene,'operation':operation,**delta,'losslessExact':exact,
                        'jpegNote':'No new JPEG acceptance threshold invented; existing 1-LSB joint test remains separate' if not lossless else None})
save(out/'quality.json',quality)
for name,sha in files.items():assert digest(Path(name))==sha,'Measured runtime changed'
for name,sha in source.items():assert digest(ROOT/name)==sha,'Source changed'
manifest['complete']=True;save(out/'manifest.json',manifest)
print('Completed original-Skia comparison; existing strict JPEG gate is not waived.',flush=True)
