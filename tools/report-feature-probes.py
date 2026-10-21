"""Feature-specific pinned quality gates. Exit 2 means a real compatibility failure."""
import argparse,json,math,struct,zlib
from pathlib import Path
parser=argparse.ArgumentParser();parser.add_argument('managed',type=Path);parser.add_argument('reference',type=Path);parser.add_argument('--output',required=True,type=Path);args=parser.parse_args()
a,b,out=args.managed,args.reference,args.output
cases=json.loads((a/'features.json').read_text(encoding='utf-8-sig'))
if cases!=json.loads((b/'features.json').read_text(encoding='utf-8-sig')):raise SystemExit('Scene manifests differ.')
results=[]
def diff_png(path,width,height,d):
 def chunk(tag,data):return struct.pack('>I',len(data))+tag+data+struct.pack('>I',zlib.crc32(tag+data))
 rgba=bytes(v for i in range(0,len(d),4) for v in [min(255,d[i]*4),min(255,d[i+1]*4),min(255,d[i+2]*4),255])
 raw=b''.join(b'\0'+rgba[y*width*4:(y+1)*width*4] for y in range(height))
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',width,height,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
diffs=out.parent/(out.stem+'-diff');diffs.mkdir(parents=True,exist_ok=True)
for case in cases:
 x=(a/(case['name']+'.rgba')).read_bytes();y=(b/(case['name']+'.rgba')).read_bytes()
 if len(x)!=len(y):raise SystemExit('Frame dimensions differ.')
 d=[abs(i-j) for i,j in zip(x,y)];maximum=max(d);rmse=math.sqrt(sum(v*v for v in d)/len(d))
 results.append({**case,'actualMaximum':maximum,'actualRmse':rmse,'passed':maximum<=case['maximum'] and rmse<=case['rmse']})
 if not results[-1]['passed']:diff_png(diffs/(case['name']+'.png'),case['width'],case['height'],d)
groups={}
for r in results:groups.setdefault(r['feature'],[]).append(r)
for name,rows in groups.items():print(name,sum(r['passed'] for r in rows),'/',len(rows),'max',max(r['actualMaximum'] for r in rows),'RMSE',round(max(r['actualRmse'] for r in rows),3))
out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps({'cases':results,'passed':sum(r['passed'] for r in results),'total':len(results)},indent=2),encoding='utf-8')
if any(not r['passed'] for r in results):raise SystemExit(2)
