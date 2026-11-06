"""Isolated Edge SVG oracle: native Skia exporter vs managed exporter.
The production engine never calls Edge or its native drawing libraries.
"""
import argparse,hashlib,json,os,re,subprocess,sys
from pathlib import Path
sys.path.insert(0,str(Path('artifacts/report-python').resolve()))
from PIL import Image
import numpy as np
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if a.output.exists():raise SystemExit('Use a fresh evidence directory.')
a.output.mkdir(parents=True);browser=Path(os.environ.get('ProgramFiles(x86)','C:/Program Files (x86)'))/'Microsoft/Edge/Application/msedge.exe'
if not browser.exists():raise SystemExit('An independent SVG renderer (Edge headless) is required; no success is reported without rendering.')
creation=getattr(subprocess,'CREATE_NO_WINDOW',0)
for variant in ['Original','Managed']:
 with (a.output/(variant+'.log')).open('w',encoding='utf-8') as f:subprocess.run(['dotnet','run','--project',f'tests/VectorDocuments/{variant}','-c','Release','--',str(a.output/variant)],stdout=f,stderr=subprocess.STDOUT,check=True)
 for file in sorted((a.output/variant).glob('*.svg')):
  markup=re.sub(r'<\?xml.*?\?>','',file.read_text(encoding='utf-8'),flags=re.S);page=file.with_suffix('.html');page.write_text('<!doctype html><html><head><style>html,body{margin:0;padding:0;width:80px;height:64px;overflow:hidden;background:white}</style></head><body>'+markup+'</body></html>',encoding='utf-8');png=file.with_suffix('.png').resolve()
  # Each invocation owns its profile. A previous browser shutdown must not
  # forward this screenshot request to another process through a profile lock.
  profile=a.output/'browser-profiles'/(variant+'-'+file.stem)
  command=[str(browser),'--headless=new','--disable-gpu','--disable-extensions','--disable-sync','--disable-background-mode','--disable-background-networking','--no-first-run','--no-default-browser-check','--hide-scrollbars','--force-color-profile=srgb','--force-device-scale-factor=1','--window-size=80,64','--user-data-dir='+str(profile.resolve()),'--screenshot='+str(png),page.resolve().as_uri()]
  with (file.with_suffix('.browser.log')).open('w',encoding='utf-8') as f:subprocess.run(command,stdout=f,stderr=subprocess.STDOUT,timeout=45,check=True,creationflags=creation)
  if not png.exists():raise RuntimeError('Browser did not produce '+str(png))
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));original=load(a.output/'Original/values.json');managed=load(a.output/'Managed/values.json');diff=[dict(key=k,original=v,managed=managed.get(k)) for k,v in original.items() if v!=managed.get(k)];cases=[]
for file in sorted((a.output/'Original').glob('*.png')):
 left=np.array(Image.open(file).convert('RGBA')).astype(np.int16);right=np.array(Image.open(a.output/'Managed'/file.name).convert('RGBA')).astype(np.int16);assert left.shape==right.shape;delta=np.abs(left-right);maximum=int(delta.max());rmse=float(np.sqrt(np.mean(delta.astype(float)**2)));cases.append(dict(scene=file.stem,maximum=maximum,rmse=rmse,passed=maximum<=3 and rmse<=.75))
result=dict(renderer=str(browser),browserSha256=hashlib.sha256(browser.read_bytes()).hexdigest(),metadata=len(original),differences=diff,cases=cases,gate=dict(maximum=3,rmse=.75),passed=not diff and all(c['passed'] for c in cases));(a.output/'comparison.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps(dict(metadata=len(original),differences=len(diff),passed=sum(c['passed'] for c in cases),total=len(cases))));raise SystemExit(0 if result['passed'] else 2)
