"""Inspect local trial packages; this script has no publishing capability."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile

p=argparse.ArgumentParser();p.add_argument('--directory',required=True,type=Path);args=p.parse_args()
version='0.1.0-codecs-trial.1';rows=[]
for path in sorted(args.directory.glob('*.nupkg')):
    with zipfile.ZipFile(path) as z:
        names=z.namelist();root=ET.fromstring(z.read(next(n for n in names if n.endswith('.nuspec'))))
        ns={'n':root.tag.split('}')[0][1:]};m=root.find('n:metadata',ns);id=m.findtext('n:id',None,ns)
        assert m.findtext('n:version',None,ns)==version
        assert m.findtext('n:license',None,ns)=='MIT'
        assert all(n in names for n in ('README.md','CHANGELOG.md','LICENSE'))
        assert not any(n.startswith('lib/net8.0/') for n in names),path
        assert not any('JpegLibrary' in n or 'libSkiaSharp' in n or n.startswith('runtimes/') for n in names),path
        if id!='SimdSkia':assert f'lib/net10.0/{id}.dll' in names
        deps={d.attrib['id']:d.attrib['version'] for d in m.findall('.//n:dependency',ns)}
        for name,v in deps.items():
            if name.startswith('SimdSkia'):assert v.strip('[]')==version,(id,name,v)
        if id=='SimdSkia.Imaging':
            assert deps.get('Sdcb.SimdCodecs')=='[1.4.0]',deps
            assert 'licenses/Sdcb.SimdCodecs-MIT.txt' in names
        assert 'JpegLibrary' not in deps
        rows.append(dict(id=id,version=version,bytes=path.stat().st_size,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),dependencies=deps))
assert len(rows)==12 and len({r['id'] for r in rows})==12
(args.directory/'manifest.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
print('PASS 12 local net10.0 trial packages, exact internal versions, complete codec reference, usage docs, license and no legacy JPEG dependency')
