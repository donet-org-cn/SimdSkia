"""Two separate oracles: SkiaSharp public behavior and FontTools EBDT pixels.
The Windows reference renderer may choose outlines despite EmbeddedBitmaps=true;
that difference is retained, not silently called pixel compatibility.
"""
import argparse,hashlib,json,math,os,subprocess,sys
from pathlib import Path
sys.path.insert(0,str(Path('artifacts/report-python').resolve()))
import fontTools
from fontTools.ttLib import TTFont
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh evidence directory.')
a.output.mkdir(parents=True)
for variant in ['Original','Managed']:
 with (a.output/(variant+'.log')).open('w',encoding='utf-8') as log:subprocess.run(['dotnet','run','--project',f'tests/EmbeddedBitmaps/{variant}','-c','Release','--',str(a.output/variant)],stdout=log,stderr=subprocess.STDOUT,check=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));o=load(a.output/'Original/values.json');m=load(a.output/'Managed/values.json');diff=[dict(key=k,original=v,managed=m.get(k)) for k,v in o.items() if v!=m.get(k)];left=(a.output/'Original/pixels.bin').read_bytes();right=(a.output/'Managed/pixels.bin').read_bytes();oc=load(a.output/'Original/cases.json');mc=load(a.output/'Managed/cases.json');assert len(oc)==len(mc);cases=[]
for c,d in zip(oc,mc):
 assert (c['scene'],c['length'])==(d['scene'],d['length']);n=c['length'];delta=[abs(x-y) for x,y in zip(left[c['offset']:c['offset']+n],right[d['offset']:d['offset']+n])];maximum=max(delta);rmse=math.sqrt(sum(x*x for x in delta)/n);cases.append(dict(**c,maximum=maximum,rmse=rmse,passed=maximum<=3 and rmse<=.75))
file=Path(os.environ.get('WINDIR','C:/Windows'))/'Fonts/simsun.ttc';font=TTFont(file,fontNumber=0,lazy=True);bitmapCases=[];outlineFallback=[]
for size in [12,14,16]:
 strike=next(i for i,s in enumerate(font['EBLC'].strikes) if s.bitmapSizeTable.ppemX==size and s.bitmapSizeTable.ppemY==size)
 for char in '中文宋A':
  name=font.getBestCmap()[ord(char)];glyph=font['EBDT'].strikeData[strike].get(name)
  if glyph is None:outlineFallback.append(dict(size=size,glyph=name,reason='The source font has no embedded bitmap for this glyph; an outline fallback is required.'));continue
  metrics=glyph.metrics;w=metrics.width;h=metrics.height;bits=glyph.imageData;stride=(w+7)//8;expected=bytearray([255]*(64*64*4));row=dict(size=size,glyph=name,format=type(glyph).__name__)
  assert row['format']=='ebdt_bitmap_format_1','The independent fixture expects byte-aligned monochrome data.'
  for y in range(h):
   for x in range(w):
    if bits[y*stride+x//8]&(128>>(x%8)):
     at=((40-metrics.BearingY+y)*64+16+metrics.BearingX+x)*4;expected[at:at+3]=bytes(3)
  for variant in ['Original','Managed']:
   actual=(a.output/variant/f'glyph-{size}-{ord(char):04X}.rgba').read_bytes();row[variant]=dict(exact=actual==expected,differentBytes=sum(x!=y for x,y in zip(actual,expected)))
  bitmapCases.append(row)
result=dict(metadata=len(o),differences=diff,cases=cases,gate=dict(maximum=3,rmse=.75),bitmapReference=dict(tool='FontTools',version=fontTools.__version__,fontSha256=hashlib.sha256(file.read_bytes()).hexdigest(),cases=bitmapCases,outlineFallback=outlineFallback),independentBitmapPassed=all(c['Managed']['exact'] for c in bitmapCases),originalPixelGatePassed=all(c['passed'] for c in cases));(a.output/'comparison.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps(dict(metadata=len(o),differences=len(diff),nativeQualityPassed=sum(c['passed'] for c in cases),nativeTotal=len(cases),independentBitmapPassed=sum(c['Managed']['exact'] for c in bitmapCases),independentTotal=len(bitmapCases))));raise SystemExit(0 if not diff and result['independentBitmapPassed'] and result['originalPixelGatePassed'] else 2)
