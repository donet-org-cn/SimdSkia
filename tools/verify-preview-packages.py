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
parser.add_argument('--desktop-only',action='store_true',help='Build-time desktop CI feed, excluding the Android package.')
args=parser.parse_args()
if not re.fullmatch(r'\d+\.\d+\.\d+(?:-preview\.\d+)?',args.version):
    raise SystemExit('Expected a stable or preview SemVer.')
ids=['SimdSkia.Core','SimdSkia.Cpu','SimdSkia.Text','SimdSkia.Imaging','SimdSkia.Gpu','SimdSkia.Gpu.Direct2D','SimdSkia.Compatibility','SimdSkia.Avalonia','SimdSkia.Avalonia.Desktop','SimdSkia.Avalonia.Gpu','SimdSkia']
modern=args.version in ('0.1.0-preview.10','0.1.0','0.1.1','0.1.2','0.1.3','0.1.4','0.1.2-preview.1','0.1.2-preview.2','0.1.2-preview.3')
matrix=args.version in ('0.1.1','0.1.2','0.1.3','0.1.4','0.1.2-preview.1','0.1.2-preview.2','0.1.2-preview.3')
if args.version=='0.1.0-preview.9' or modern:
    ids.append('SimdSkia.Pdf')
if args.version=='0.1.4' and not args.desktop_only:
    ids.append('SimdSkia.Avalonia.Android')
release_topics={
    '0.1.4':(('Android','12.1.5','CPU','iOS','0.1.4'),'0.1.4'),
    '0.1.3':(('GPU','Vulkan','Direct2D','缓存','画质','UseCpu','12.1.0'),'0.1.3'),
    '0.1.2':(('0.1.1','2338','3230','Vulkan','Metal','12.1.0','画质','SaveLayer'),'2338'),
    '0.1.2-preview.3':(('3230','Vulkan','Metal','12.1.0','画质','SaveLayer'),'3230'),
    '0.1.2-preview.2':(('3230','Vulkan','Metal','12.1.0','画质','SaveLayer'),'3230'),
    '0.1.2-preview.1':(('3230','Vulkan','Metal','12.1.0','画质'),'3230'),
    '0.1.1':(('12.1.0','12.1.5','Sdcb.SimdCodecs','画质'),'12.1.0'),
    '0.1.0':(('Sdcb.SimdCodecs','性能','画质'),'Sdcb.SimdCodecs'),
    '0.1.0-preview.7':(('AVX2','24/24'),'AVX2'),
    '0.1.0-preview.8':(('SSE2','28.05','画质'),'SSE2'),
    '0.1.0-preview.9':(('SKBitmap.GetPixels','PDF','preview.9'),'SKBitmap.GetPixels'),
    '0.1.0-preview.10':(('Sdcb.SimdCodecs','123','画质'),'Sdcb.SimdCodecs'),
}
if args.version not in release_topics:
    raise SystemExit('Configure release topics and the package list before publishing a new version.')
topics,release_topic=release_topics[args.version]
framework='net10.0' if modern else 'net8.0'
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
        allowed_bridges={f'hosts/12.1.{i}.0/native/libAvaloniaNative.dylib' for i in range(6)} if matrix and package_id=='SimdSkia.Avalonia.Desktop' else set()
        assert not any((name.lower().endswith(('.ttf','.ttc','.otf','.woff','.woff2','.so','.dylib')) or name.lower().startswith('runtimes/')) and name not in allowed_bridges for name in names),path
        assert allowed_bridges.issubset(names),path
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
        if modern:
            assert not any(name.startswith('lib/net8.0/') for name in names),path
            assert 'JpegLibrary' not in dependencies,path
            if package_id=='SimdSkia.Imaging':
                assert dependencies.get('Sdcb.SimdCodecs')=='[1.4.0]',dependencies
                assert 'licenses/Sdcb.SimdCodecs-MIT.txt' in names,path
        if args.version in ('0.1.2-preview.3','0.1.2','0.1.3','0.1.4') and package_id!='SimdSkia.Avalonia.Android':
            pdf_readme_versions=re.findall(r'PdfPig\s+(?:\*\*)?([0-9A-Za-z.+-]+)',readme)
            assert pdf_readme_versions and set(pdf_readme_versions)=={'0.1.16'},(path,'Incorrect PDF dependency version in README',pdf_readme_versions)
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
            assembly_framework='net10.0-android36.0' if package_id=='SimdSkia.Avalonia.Android' else framework
            assert f'lib/{assembly_framework}/{package_id}.dll' in names,path
            with zipfile.ZipFile(args.directory/f'{package_id}.{args.version}.snupkg') as symbols:
                assert any(name.endswith('.pdb') for name in symbols.namelist()),path
        if package_id=='SimdSkia.Gpu':
            assert not any(name.endswith('vulkan-1.dll') for name in names),path
        if package_id=='SimdSkia.Avalonia':
            assert dependencies.get('Avalonia')==('[12.1.0, 13.0.0)' if matrix else '[12.1.5]'),dependencies
            readme=package.read('README.md').decode('utf-8')
            for step in ('Demo.csproj','Program.cs','dotnet restore','dotnet run','dotnet publish','UseSimdSkiaDesktop()','SimdSkia.Avalonia.Desktop','12.1.5'):
                assert step in readme,(path,'Missing README instructions',step)
            label='预览' if '-preview.' in args.version else '正式'
            assert '系统' in readme and label in readme,path
        if package_id=='SimdSkia.Avalonia.Desktop':
            if matrix:
                assert dependencies.get('Tmds.DBus.Protocol')=='[0.94.1]',dependencies
                assert not any(name in dependencies for name in ('Avalonia.Native','Avalonia.FreeDesktop','Avalonia.FreeDesktop.AtSpi')),dependencies
            else:
                assert dependencies.get('Avalonia.Native')=='[12.1.5]',dependencies
            hosts=['Avalonia.Win32.dll','Avalonia.Win32.Automation.dll','Avalonia.X11.dll']
            if matrix:hosts+=['Avalonia.FreeDesktop.dll','Avalonia.FreeDesktop.AtSpi.dll','Avalonia.Native.dll']
            for host in hosts:
                if matrix:
                    for patch in range(6):
                        assert f'hosts/12.1.{patch}.0/{host}' in names,(path,host,patch)
                    assert f'lib/{framework}/{host}' not in names,(path,'Unconditional mismatched host')
                    assert 'buildTransitive/SimdSkia.Avalonia.Desktop.targets' in names,path
                else:
                    assert f'lib/{framework}/{host}' in names,(path,host)
        if package_id=='SimdSkia.Gpu.Direct2D':
            assert set(dependencies)==({'SimdSkia.Core','SimdSkia.Cpu','SimdSkia.Gpu'} if args.version in ('0.1.2','0.1.3','0.1.4','0.1.2-preview.1','0.1.2-preview.2','0.1.2-preview.3') else {'SimdSkia.Core'}),dependencies
        if args.version in ('0.1.3','0.1.4') and package_id=='SimdSkia.Avalonia.Desktop':
            assert dependencies.get('SimdSkia.Avalonia.Gpu','').strip('[]')==args.version,(path,'GPU-preferred desktop dependency missing')
        if package_id=='SimdSkia.Avalonia.Android':
            assert dependencies.get('Avalonia')=='[12.1.5]',dependencies
            assert 'Avalonia.Android' not in dependencies,dependencies
            assert 'lib/net10.0-android36.0/Avalonia.Android.dll' in names,path
            assert 'licenses/Avalonia-MIT.txt' in names and 'licenses/AndroidX-Apache-2.0.txt' in names,path
            assert not any('Mono.Cecil' in name for name in names),path
            for step in ('UseSimdSkiaAndroid','CreateAppBuilder','Theme.AppCompat','dotnet add package','Mono AOT'):
                assert step in readme,(path,'Missing Android usage instructions',step)
        rows.append({'id':package_id,'version':args.version,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'dependencies':dependencies})
(args.directory/'manifest.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
print(f'PASS {len(rows)} {args.version} packages: dependency versions, managed assemblies, README, CHANGELOG, NuGet release notes, MIT, symbols and no font/native drawing binaries')
