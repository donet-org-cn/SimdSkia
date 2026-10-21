import json,pathlib,statistics,sys
root=pathlib.Path(sys.argv[1]);groups={};runs=[]
for path in sorted(root.glob('*.json')):
    if path.name in ['manifest.json','results.json']:continue
    run=json.loads(path.read_text(encoding='utf-8-sig'));runs.append(run)
    for result in run['results']:groups.setdefault((result['width'],result['height'],result['sigma'],run['mode']),[]).append(result)
rows=[]
fields=['ms','cpuMs','allocatedBytes','workingSetMiB','privateMiB','retainedMiB','gc0','gc1','gc2']
for (width,height,sigma,mode),samples in sorted(groups.items()):
    row=dict(width=width,height=height,sigma=sigma,mode=mode,forks=len(samples))
    row.update({field:statistics.median(s[field] for s in samples) for field in fields});rows.append(row)
lines=['| Size | Sigma | Mode | ms | CPU ms | B/op | WS MiB | Private MiB |','|---|---:|---|---:|---:|---:|---:|---:|']
for row in rows:lines.append(f"| {row['width']}x{row['height']} | {row['sigma']} | {row['mode']} | {row['ms']:.3f} | {row['cpuMs']:.3f} | {row['allocatedBytes']:.0f} | {row['workingSetMiB']:.1f} | {row['privateMiB']:.1f} |")
(root/'results.json').write_text(json.dumps(dict(summary=rows,runs=runs),indent=2)+'\n',encoding='utf-8')
(root/'summary.md').write_text('\n'.join(lines)+'\n',encoding='utf-8');print('\n'.join(lines))
