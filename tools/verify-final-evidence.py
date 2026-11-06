"""Verify retained evidence hashes and every content-addressed raw frame."""
import argparse,gzip,hashlib,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--directory',type=Path,default=Path('docs/evidence/api-final-2026-11-07'));a=p.parse_args()
root=a.directory;load=lambda name:json.loads((root/name).read_text(encoding='utf-8-sig'))
hashes=load('sha256.json')
for name,digest in hashes.items():
 path=(root/name).resolve();assert path.is_relative_to(root.resolve()),'Invalid evidence path.'
 assert hashlib.sha256(path.read_bytes()).hexdigest()==digest,name
frames=load('frame-objects.json');seen={}
for name,item in frames.items():
 if item['object'] not in seen:seen[item['object']]=gzip.decompress((root/item['object']).read_bytes())
 data=seen[item['object']];assert len(data)==item['length'] and hashlib.sha256(data).hexdigest()==item['sha256'],name
print(f'PASS {len(hashes)} evidence file hashes, {len(frames)} frame references, {len(seen)} unique raw objects. This validates evidence integrity, not failed quality gates.')
