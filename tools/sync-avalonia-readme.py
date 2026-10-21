"""Keep the NuGet README in sync with the repository's complete usage example."""
from pathlib import Path
import argparse
import re
parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
text=(root/'README.md').read_text(encoding='utf-8')
text=text.replace('# SimdSkia\n','# SimdSkia.Avalonia\n',1)
def absolute(match):
    label,target=match.groups()
    if target.startswith(('#','/')) or re.match(r'^[a-zA-Z][a-zA-Z0-9+.-]*:',target):return match.group(0)
    return label+'(https://github.com/hnlyf168/SimdSkia/blob/main/'+target+')'
text=re.sub(r'(!?\[[^\]\n]*\])\(([^)\n]+)\)',absolute,text)
target=root/'src/SimdSkia.Avalonia/README.md'
if args.check:
    if target.read_text(encoding='utf-8')!=text:raise SystemExit('Avalonia package README is stale. Run python tools/sync-avalonia-readme.py.')
    print('PASS repository and Avalonia/Desktop/GPU package README are synchronized')
else:
    target.write_text(text,encoding='utf-8')
    print('Updated the Avalonia/Desktop/GPU package README from README.md')
