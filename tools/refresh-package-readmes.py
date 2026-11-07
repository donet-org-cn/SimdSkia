"""Update SimdSkia usage versions without rewriting third-party dependencies."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parents[1]
props=ET.parse(root/'src/Directory.Build.props')
version=props.findtext('.//Version')
assert version and re.fullmatch(r'\d+\.\d+\.\d+(?:-preview\.\d+)?',version)
pdf=ET.parse(root/'src/SimdSkia.Pdf/SimdSkia.Pdf.csproj')
pdf_version=next(p.attrib['Version'].strip('[]') for p in pdf.iter('PackageReference') if p.attrib.get('Include')=='PdfPig')
pattern=r'\d+\.\d+\.\d+(?:-preview\.\d+)?'
for name in ['docs/NUGET-README.md','src/SimdSkia.Pdf/README.md']:
    path=root/name
    content=path.read_text(encoding='utf-8')
    # Only SimdSkia command lines, current headings and aligned-package prose.
    # A substring replacement of 0.1.1 also changes PdfPig 0.1.16.
    content=re.sub(r'(dotnet add package SimdSkia(?:\.[\w.]+)? --version )'+pattern,
                   lambda m:m[1]+version,content)
    content=re.sub(r'(?m)^(#{1,2} (?:SimdSkia(?:\.Pdf)? )?)'+pattern+r'(?= 正式| 更新|$)',
                   lambda m:m[1]+version,content)
    content=re.sub(r'(SimdSkia packages on version `)'+pattern+r'`',
                   lambda m:m[1]+version+'`',content)
    content=re.sub(r'(pinned PdfPig )'+pattern,lambda m:m[1]+pdf_version,content)
    content=re.sub(r'(using PdfPig \*\*)'+pattern+r'\*\*',
                   lambda m:m[1]+pdf_version+'**',content)
    path.write_text(content,encoding='utf-8')
print(f'Updated SimdSkia {version} usage; PdfPig stays pinned to {pdf_version}.')
