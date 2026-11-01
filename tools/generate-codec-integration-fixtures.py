"""Independent test-only Pillow/OpenJPEG/libwebp outputs from first-party pixels."""
import hashlib
import json
from pathlib import Path
import struct
import zlib
from PIL import Image,features

root=Path(__file__).resolve().parent.parent
out=root/'tests/CodecIntegration/Fixtures';out.mkdir(parents=True,exist_ok=True)
w,h=13,9
images=[]
for frame in range(3):
    image=Image.new('RGBA',(w,h))
    image.putdata([((x*19+frame*37)%256,(y*27+frame*61)%256,(x*7+y*11+frame*43)%256,255) for y in range(h) for x in range(w)])
    images.append(image)
images[0].save(out/'still.webp',lossless=True)
images[0].save(out/'animation.webp',save_all=True,append_images=images[1:],duration=[70,90,110],loop=2,lossless=True)
images[0].save(out/'pages.tiff',save_all=True,append_images=images[1:],compression='tiff_deflate')
if not features.check('jpg_2000'):raise SystemExit('Independent OpenJPEG codec unavailable; do not count this as a pass')
images[0].convert('RGB').save(out/'lossless.jp2',irreversible=False)
# ISO/ITU T.88 sequential standalone stream: page information, end of page, EOF.
# A blank page needs no region or dictionary segment; tests the standalone dispatch.
def segment(number,kind,page,payload):return struct.pack('>IBBBI',number,kind,0,page,len(payload))+payload
page=struct.pack('>IIIIBH',w,h,0,0,0,0)
jbig=b'\x97JB2\r\n\x1a\n'+b'\x01'+struct.pack('>I',1)+segment(1,48,1,page)+segment(2,49,1,b'')+segment(3,51,0,b'')
(out/'blank.jb2').write_bytes(jbig)
def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
# Linear 0.5 encoded with gAMA=1.0 must display near sRGB 188, not code-value 128.
gamma=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',1,1,8,6,0,0,0))+chunk(b'gAMA',struct.pack('>I',100000))+chunk(b'IDAT',zlib.compress(bytes([0,128,128,128,255])))+chunk(b'IEND',b'')
(out/'gamma.png').write_bytes(gamma)
fixtures=[]
for name in ('still.webp','animation.webp','pages.tiff','lossless.jp2'):
    with Image.open(out/name) as decoded:
        frames=[]
        for index in range(getattr(decoded,'n_frames',1)):
            decoded.seek(index)
            frames.append(dict(rgba=decoded.convert('RGBA').tobytes().hex(),duration=decoded.info.get('duration')))
    fixtures.append(dict(file=name,width=w,height=h,frames=frames))
fixtures.append(dict(file='gamma.png',width=1,height=1,frames=[dict(rgba='bcbcbcff',duration=None)]))
(out/'oracle.json').write_text(json.dumps(dict(generator='Pillow '+Image.__version__+'; native libwebp/OpenJPEG/TIFF used only as isolated black boxes',fixtures=fixtures),indent=2)+'\n',encoding='utf-8')
(out/'SHA256.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in out.iterdir() if p.suffix in ('.webp','.tiff','.jp2','.jb2','.png')},indent=2)+'\n',encoding='utf-8')
print('Generated independent WebP/animation/TIFF/JP2 fixtures and normative blank JBIG2 page')
