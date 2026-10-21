"""CPU-only real-control benchmark summary; native differences remain visible as FAIL."""
import hashlib,json,math,pathlib,statistics,struct,sys,zlib

root=pathlib.Path(sys.argv[1])
rows=[]
for file in sorted(root.glob('*/metrics.json')):
    row=json.loads(file.read_text(encoding='utf-8-sig'));row['run']=file.parent.name;row['variant']=file.parent.name.split('-')[0];rows.append(row)
if not rows: raise SystemExit('No measurements.')
groups={}
for row in rows: groups.setdefault((row['scenario'],row['scale'],row['variant']),[]).append(row)
fields=['millisecondsPerFrame','cpuMillisecondsPerFrame','allocatedBytesPerFrame','workingSetMiB','privateMiB','peakProcessWorkingSetMiB','managedRetainedAfterGcMiB','firstRenderMs']
fields+=['framesPerSecond','p50Ms','p95Ms','p99Ms','maximumFrameMs','gc0','gc1','gc2']
summary=[]
for key,samples in sorted(groups.items()):
    item={'scenario':key[0],'scale':key[1],'variant':key[2],'forks':len(samples)}
    for field in fields:
        if all(field in s for s in samples):item[field]=statistics.median(s[field] for s in samples)
    item['frameRangeMs']=[min(s['millisecondsPerFrame'] for s in samples),max(s['millisecondsPerFrame'] for s in samples)];summary.append(item)

def png(path,width,height,rgba):
    def chunk(tag,data):return struct.pack('>I',len(data))+tag+data+struct.pack('>I',zlib.crc32(tag+data)&0xffffffff)
    raw=b''.join(b'\0'+rgba[y*width*4:(y+1)*width*4] for y in range(height))
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',width,height,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))

quality=[]
for scene,scale in sorted({(r['scenario'],r['scale']) for r in rows}):
    selected={variant:next((r for r in rows if r['scenario']==scene and r['scale']==scale and r['variant']==variant),None) for variant in ('before','after','reference','default')}
    after=selected['after'];reference=selected['reference']
    if not after or not reference:continue
    a=(root/after['run']/'scene.rgba').read_bytes();b=(root/reference['run']/'scene.rgba').read_bytes()
    if len(a)!=len(b):raise SystemExit('Pixel dimensions differ.')
    differences=[abs(x-y) for x,y in zip(a,b)]
    max_error=max(differences);rmse=math.sqrt(sum(d*d for d in differences)/len(differences))
    before=selected['before'];same=None
    if before:same=a==(root/before['run']/'scene.rgba').read_bytes()
    pixels=len(a)//4;bad=sum(any(differences[i+c]>32 for c in range(4)) for i in range(0,len(a),4))
    item={'scenario':scene,'scale':scale,'maximumChannelError':max_error,'rmse':rmse,'pixelsOver32':bad,'pixels':pixels,'threshold':{'maximumChannelError':32,'rmse':2.0},'passed':max_error<=32 and rmse<=2,'beforeAfterExactlyEqual':same,'afterSha256':hashlib.sha256(a).hexdigest(),'referenceSha256':hashlib.sha256(b).hexdigest()}
    if selected['default']:
        native=(root/selected['default']['run']/'scene.rgba').read_bytes()
        if len(native)!=len(a):raise SystemExit('Native default pixel dimensions differ.')
        delta=[abs(x-y) for x,y in zip(a,native)]
        native_max=max(delta);native_rmse=math.sqrt(sum(d*d for d in delta)/len(delta))
        item.update(nativeDefaultMaximumChannelError=native_max,nativeDefaultRmse=native_rmse,nativeDefaultPassed=native_max<=32 and native_rmse<=2,nativeDefaultSha256=hashlib.sha256(native).hexdigest())
    quality.append(item)
    diff=bytearray(len(a))
    for i in range(0,len(a),4):diff[i:i+4]=bytes([min(255,differences[i]*4),min(255,differences[i+1]*4),min(255,differences[i+2]*4),255])
    png(root/f'diff-{scene}-dpi{scale}.png',after['width'],after['height'],diff)
    png(root/f'managed-{scene}-dpi{scale}.png',after['width'],after['height'],a)
    png(root/f'reference-{scene}-dpi{scale}.png',after['width'],after['height'],b)
result={'summary':summary,'quality':quality,'runs':rows}
(root/'results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['| Scene | DPI | Engine | ms/frame | CPU ms/frame | KiB/frame | WS MiB | Private MiB | Retained managed MiB |','|---|---:|---|---:|---:|---:|---:|---:|---:|']
for s in summary:lines.append(f"| {s['scenario']} | {s['scale']} | {s['variant']} | {s['millisecondsPerFrame']:.3f} | {s['cpuMillisecondsPerFrame']:.3f} | {s['allocatedBytesPerFrame']/1024:.2f} | {s['workingSetMiB']:.1f} | {s['privateMiB']:.1f} | {s['managedRetainedAfterGcMiB']:.1f} |")
lines+=['','Pixel quality vs official renderer:']+[f"- {q['scenario']} DPI {q['scale']}: {'PASS' if q['passed'] else 'FAIL'}, max {q['maximumChannelError']}, RMSE {q['rmse']:.3f}; before/after exact: {q['beforeAfterExactlyEqual'] if q['beforeAfterExactlyEqual'] is not None else 'not supplied'}" for q in quality]
(root/'summary.md').write_text('\n'.join(lines)+'\n',encoding='utf-8');print('\n'.join(lines))
if any(not q['passed'] for q in quality):raise SystemExit(2)
