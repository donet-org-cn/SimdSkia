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
ids=['SimdSkia.Core','SimdSkia.Cpu','SimdSkia.Text','SimdSkia.Imaging','SimdSkia.Gpu','SimdSkia.Gpu.Direct2D','SimdSkia.Compatibility','SimdSkia.Avalonia','SimdSkia.Avalonia.Desktop','SimdSkia.Avalonia.Gpu','SimdSkia']
if args.version=='0.1.0-preview.9':
    ids.append('SimdSkia.Pdf')
release_topics={
    '0.1.0-preview.7':(('AVX2','24/24'),'AVX2'),
    '0.1.0-preview.8':(('SSE2','28.05','画质'),'SSE2'),
    '0.1.0-preview.9':(('SKBitmap.GetPixels','PDF','preview.9'),'SKBitmap.GetPixels'),
}
if args.version not in release_topics:
    raise SystemExit('Configure release topics and the package list before publishing a new version.')
topics,release_topic=release_topics[args.version]
assert {path.name for path in args.directory.glob('*.nupkg')}=={f'{name}.{args.version}.nupkg' for name in ids},'Unexpected or missing packages in output directory'
rows=[]
for package_id in ids:
    path=args.directory/f'{package_id}.{args.version}.nupkg'
    with zipfile.ZipFile(path) as package:
        assert package.testzip() is None,path
        names=package.namelist()
        assert 'README.md' in names and 'LICENSE' in names and 'CHANGELOG.md' in names,path
        readme=package.read('README.md').decode('utf-8')
        changelog=package.read('CHANGELOG.md').decode('utf-8')
        assert args.version in readme and args.version in changelog,(path,'Missing current version notes')
        for topic in topics:
            assert topic in readme and topic in changelog,(path,'Missing release topic',topic)
        assert not any(name.lower().endswith(('.ttf','.ttc','.otf','.woff','.woff2','.so','.dylib')) or name.lower().startswith('runtimes/') for name in names),path
        assert not any('skiasharp.dll' in name.lower() or 'harfbuzz' in name.lower() for name in names),path
        root=ET.fromstring(package.read(next(name for name in names if name.endswith('.nuspec'))))
        # Namespace versions are allowed to differ between SDK releases.
        prefix=root.tag.split('}')[0]+'}' if root.tag.startswith('{') else ''
        metadata=root.find(prefix+'metadata')
        assert metadata.findtext(prefix+'id')==package_id,path
        assert metadata.findtext(prefix+'version')==args.version,path
        assert metadata.findtext(prefix+'license')=='MIT',path
        release_notes=metadata.findtext(prefix+'releaseNotes') or ''
        assert release_notes.strip() and release_topic in release_notes and 'CHANGELOG.md' in release_notes,(path,'Missing NuGet release notes')
        dependencies={element.attrib['id']:element.attrib['version'] for element in metadata.iter(prefix+'dependency')}
        if package_id=='SimdSkia.Pdf':
            assert dependencies.get('PdfPig')=='[0.1.16]',dependencies
            for notice in ('Apache-2.0.txt','PdfPig-NOTICE.txt','PdfPig-UPSTREAM-NOTICES.txt'):
                assert f'licenses/{notice}' in names,(path,'Missing PDF license notice',notice)
        else:
            assert 'PdfPig' not in dependencies,(path,'Optional PDF dependency leaked into another package')
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
            for step in ('Demo.csproj','Program.cs','dotnet restore','dotnet run','dotnet publish','UseSimdSkiaDesktop()','SimdSkia.Avalonia.Desktop','12.1.4'):
                assert step in readme,(path,'Missing README instructions',step)
            assert '\u7cfb\u7edf' in readme and '\u9884\u89c8' in readme,path
        if package_id=='SimdSkia.Avalonia.Desktop':
            assert dependencies.get('Avalonia.Native')=='[12.1.4]',dependencies
            for host in ['Avalonia.Win32.dll','Avalonia.Win32.Automation.dll','Avalonia.X11.dll']:
                assert f'lib/net8.0/{host}' in names,(path,host)
        if package_id=='SimdSkia.Gpu.Direct2D':
            assert set(dependencies)=={'SimdSkia.Core'},dependencies
        rows.append({'id':package_id,'version':args.version,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'dependencies':dependencies})
(args.directory/'manifest.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
print(f'PASS {len(rows)} preview packages: dependency versions, managed assemblies, README, CHANGELOG, NuGet release notes, MIT, symbols and no font/native drawing binaries')
