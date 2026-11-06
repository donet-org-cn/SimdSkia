"""Verify archived byte hashes against Git index blobs, including ignored logs."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('directories',nargs='+',type=Path)
a=p.parse_args()
index=subprocess.run(['git','ls-files','--stage','-z'],capture_output=True,check=True).stdout
blobs={}
for entry in index.split(b'\0'):
    if not entry:continue
    metadata,path=entry.split(b'\t',1)
    _,oid,stage=metadata.split()
    assert stage==b'0','Resolve unmerged files before checking evidence.'
    blobs[path.decode('utf-8')]=oid.decode('ascii')
requests=[]
for root in a.directories:
    hashes=json.loads((root/'sha256.json').read_text(encoding='utf-8-sig'))
    for name,digest in hashes.items():
        file=root/name
        assert file.resolve().is_relative_to(root.resolve()),'Unsafe evidence path'
        relative=file.as_posix()
        assert relative in blobs,relative+' is not staged in Git'
        assert hashlib.sha256(file.read_bytes()).hexdigest()==digest,relative+' working copy changed'
        requests.append((blobs[relative],relative,digest))
data=subprocess.run(['git','cat-file','--batch'],input=''.join(oid+'\n' for oid,_,_ in requests).encode('ascii'),capture_output=True,check=True).stdout
offset=0
for oid,path,digest in requests:
    end=data.index(b'\n',offset)
    actual_oid,kind,size=data[offset:end].split()
    assert actual_oid.decode('ascii')==oid and kind==b'blob',path
    start=end+1;length=int(size);content=data[start:start+length]
    assert len(content)==length and data[start+length:start+length+1]==b'\n',path
    assert hashlib.sha256(content).hexdigest()==digest,path+' staged bytes differ from the recorded evidence hash'
    offset=start+length+1
assert offset==len(data),'Unexpected batch output'
print(f'PASS {len(requests)} evidence hashes exist in the Git index and match the measured working bytes.')
