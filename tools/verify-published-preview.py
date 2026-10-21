"""Wait for public NuGet availability and compare DLLs/README with CI artifacts."""
import argparse
import concurrent.futures
import hashlib
import io
import json
from pathlib import Path
import re
import time
import urllib.error
import urllib.request
import zipfile

parser=argparse.ArgumentParser()
parser.add_argument('--manifest',required=True,type=Path)
parser.add_argument('--wait-seconds',type=int,default=1200)
args=parser.parse_args()
rows=json.loads(args.manifest.read_text(encoding='utf-8'))
assert len(rows)>=8 and len({row['id'] for row in rows})==len(rows)
for row in rows:
    assert re.fullmatch(r'\d+\.\d+\.\d+-preview\.\d+',row['version'])
pending={row['id']:row for row in rows}
verified=[]
deadline=time.monotonic()+args.wait_seconds

def check(row):
    package_id=row['id'].lower();version=row['version'].lower()
    url=f'https://api.nuget.org/v3-flatcontainer/{package_id}/{version}/{package_id}.{version}.nupkg'
    try:
        request=urllib.request.Request(url,headers={'Cache-Control':'no-cache','User-Agent':'SimdSkia-release-verification'})
        with urllib.request.urlopen(request,timeout=25) as response:blob=response.read()
    except urllib.error.HTTPError as error:
        if error.code in (404,429,500,502,503,504):return None
        raise
    except (urllib.error.URLError,TimeoutError):return None
    expected=args.manifest.parent/f"{row['id']}.{row['version']}.nupkg"
    with zipfile.ZipFile(io.BytesIO(blob)) as public,zipfile.ZipFile(expected) as built:
        assert public.testzip() is None,row['id']
        # NuGet repository-signs the container, so the whole ZIP hash can differ.
        # Its managed assemblies and README must remain exactly the CI artifacts.
        files=[name for name in built.namelist() if name.endswith('.dll') or name=='README.md']
        for name in files:assert public.read(name)==built.read(name),(row['id'],name)
    return {'id':row['id'],'version':row['version'],'publicDownload':url,'contentVerified':True,'downloadSha256':hashlib.sha256(blob).hexdigest()}

while pending:
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        results=list(pool.map(check,list(pending.values())))
    for result in results:
        if result:
            verified.append(result);pending.pop(result['id']);print('PASS public NuGet download and content:',result['id'],flush=True)
    if not pending:break
    if time.monotonic()>=deadline:raise SystemExit('NuGet public availability timed out: '+', '.join(pending))
    print('Waiting for NuGet validation/indexing:',', '.join(pending),flush=True)
    time.sleep(min(30,max(0,deadline-time.monotonic())))
(args.manifest.parent/'published-verification.json').write_text(json.dumps(verified,indent=2)+'\n',encoding='utf-8')
print(f'PASS all {len(rows)} preview packages are publicly downloadable, with the exact CI DLLs and README',flush=True)
