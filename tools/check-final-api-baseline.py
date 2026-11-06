"""Enforce the unchanged baseline and the five explicitly retained native gaps.

This is a preview scope check. ApiAudit's ordinary compare still exits 2 for
these missing entries; this tool does not turn them into implementations.
"""
import argparse,json
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--comparison',type=Path,required=True)
a=p.parse_args()
report=json.loads(a.comparison.read_text(encoding='utf-8-sig'))
gaps=json.loads(Path('docs/api/intentional-native-gaps.json').read_text(encoding='utf-8'))
assert report['BaselineHash']=='03A164AC1097F84793632DF806DA91E2829F0FCA65D8F2B922E45554AB083296','The fixed baseline changed.'
assert report['BaselineEntries']==3235 and report['MatchingEntries']==3230,report
assert not report['AddedOrDifferent'],'Public replacement signatures differ.'
assert set(report['MissingOrDifferent'])==set(gaps['entries']),'Unexpected missing native or managed API.'
print('PASS preview metadata scope: 3230/3235, exactly 5 retained native-contract gaps, 0 extra. Behavior/binary compatibility not established.')
