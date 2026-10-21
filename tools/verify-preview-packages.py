"""Validate the actual nupkg graph and contents before any public upload."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile

parser=argparse.ArgumentParser()
parser.add_argument('--directory',required=True,type=Path)
parser.add_argument('--version',required=True)
args=parser.parse_args()
if not re.fullmatch(r'\d+\.\d+\.\d+-preview\.\d+',args.version):
    raise SystemExit('Only preview versions are accepted.')
ids=['SimdSkia.Core','SimdSkia.Cpu','SimdSkia.Text','SimdSkia.Imaging','SimdSkia.Gpu','SimdSkia.Compatibility','SimdSkia.Avalonia','SimdSkia']
rows=[]
for package_id in ids:
    path=args.directory/f'{package_id}.{args.version}.nupkg'
    with zipfile.ZipFile(path) as package:
        assert package.testzip() is None,path
        names=package.namelist()
        assert 'README.md' in names and 'LICENSE' in names,path
        assert not any(name.lower().endswith(('.ttf','.ttc','.otf','.woff','.woff2','.so','.dylib')) or name.lower().startswith('runtimes/') for name in names),path
        assert not any('skiasharp.dll' in name.lower() or 'harfbuzz' in name.lower() for name in names),path
        root=ET.fromstring(package.read(next(name for name in names if name.endswith('.nuspec'))))
        # Namespace versions are allowed to differ between SDK releases.
        prefix=root.tag.split('}')[0]+'}' if root.tag.startswith('{') else ''
        metadata=root.find(prefix+'metadata')
        assert metadata.findtext(prefix+'id')==package_id,path
        assert metadata.findtext(prefix+'version')==args.version,path
        assert metadata.findtext(prefix+'license')=='MIT',path
        dependencies={element.attrib['id']:element.attrib['version'] for element in metadata.iter(prefix+'dependency')}
        for dependency,version in dependencies.items():
            if dependency.startswith('SimdSkia'):
                assert version.strip('[]')==args.version,(path,dependency,version)
        if package_id!='SimdSkia':
            assert f'lib/net8.0/{package_id}.dll' in names,path
            with zipfile.ZipFile(args.directory/f'{package_id}.{args.version}.snupkg') as symbols:
                assert any(name.endswith('.pdb') for name in symbols.namelist()),path
        if package_id=='SimdSkia.Gpu':
            assert not any(name.endswith('vulkan-1.dll') for name in names),path
        if package_id=='SimdSkia.Avalonia':
            assert dependencies.get('Avalonia')=='[12.1.4]',dependencies
            readme=package.read('README.md').decode('utf-8')
            for step in ('Demo.csproj','Program.cs','dotnet restore','dotnet run','dotnet publish','UseSimdSkia()','PackageDownload','12.1.4'):
                assert step in readme,(path,'Missing README instructions',step)
            assert '\u7cfb\u7edf' in readme and '\u9884\u89c8' in readme,path
        rows.append({'id':package_id,'version':args.version,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'dependencies':dependencies})
(args.directory/'manifest.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
print(f'PASS {len(rows)} preview packages: dependency versions, managed assemblies, README, MIT, symbols and no font/native drawing binaries')
