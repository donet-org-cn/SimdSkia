"""Independent test-only fixtures: Python zlib/PNG assembly + Pillow libjpeg/lcms.
No Python, Pillow or native codec is used by the engine or normal tests.
"""
import base64, io, json, pathlib, struct, zlib
from PIL import Image, ImageCms

def b64(data): return base64.b64encode(data).decode()
def chunk(tag, data): return struct.pack('>I',len(data))+tag+data+struct.pack('>I',zlib.crc32(tag+data))
cases=[]
width,height=11,9
for color,channels in [(0,1),(2,3),(4,2),(6,4)]:
    for depth in [8,16]:
        for interlace in [0,1]:
            values=[[(x*7331+y*3719+c*12131)%65536 for c in range(channels)] for y in range(height) for x in range(width)]
            if depth==8: values=[[v>>8 for v in pixel] for pixel in values]
            raw=bytearray()
            passes=[(0,0,1,1)] if not interlace else [(0,0,8,8),(4,0,8,8),(0,4,4,8),(2,0,4,4),(0,2,2,4),(1,0,2,2),(0,1,1,2)]
            for ox,oy,dx,dy in passes:
                previous=None
                for py,y in enumerate(range(oy,height,dy)):
                    row=bytearray()
                    for x in range(ox,width,dx):
                        for v in values[y*width+x]: row.extend(struct.pack('>H',v) if depth==16 else bytes([v]))
                    if row:
                        filter=py%5;bpp=channels*(depth//8);filtered=bytearray()
                        for i,v in enumerate(row):
                            left=row[i-bpp] if i>=bpp else 0;above=previous[i] if previous is not None else 0;corner=previous[i-bpp] if previous is not None and i>=bpp else 0
                            p=left+above-corner;distances=[abs(p-left),abs(p-above),abs(p-corner)];paeth=[left,above,corner][distances.index(min(distances))]
                            prediction=[0,left,above,(left+above)//2,paeth][filter];filtered.append((v-prediction)&255)
                        raw.extend(bytes([filter])+filtered);previous=row
            expected=bytearray()
            for pixel in values:
                p=[v>>8 for v in pixel] if depth==16 else pixel
                rgb=p[:3] if color in (2,6) else [p[0]]*3
                expected.extend(rgb+[p[-1] if color in (4,6) else 255])
            header=struct.pack('>IIBBBBB',width,height,depth,color,0,0,interlace)
            encoded=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',header)+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b'')
            cases.append(dict(name=f'png-{color}-{depth}-adam{interlace}',encoded=b64(encoded),rgba=b64(expected),width=width,height=height,max=0,rmse=0))
for color in [0,3]:
    for depth in [1,2,4]:
        for interlace in [0,1]:
            levels=1<<depth;values=[(x*7+y*3)%levels for y in range(height) for x in range(width)];raw=bytearray();expected=bytearray()
            palette=bytes(v for i in range(levels) for v in [(i*37)%256,(i*71)%256,(i*113)%256]);alpha=bytes([0]+[128 if i%3==0 else 255 for i in range(1,levels)])
            passes=[(0,0,1,1)] if not interlace else [(0,0,8,8),(4,0,8,8),(0,4,4,8),(2,0,4,4),(0,2,2,4),(1,0,2,2),(0,1,1,2)]
            for ox,oy,dx,dy in passes:
                for y in range(oy,height,dy):
                    samples=[values[y*width+x] for x in range(ox,width,dx)];line=bytearray((len(samples)*depth+7)//8)
                    for x,v in enumerate(samples):line[x*depth//8]|=v<<(8-depth-(x*depth%8))
                    if line:raw.extend(b'\0'+line)
            for v in values:expected.extend(palette[v*3:v*3+3]+bytes([alpha[v]]) if color==3 else bytes([v*255//(levels-1)]*3+[0 if v==1 else 255]))
            header=struct.pack('>IIBBBBB',width,height,depth,color,0,0,interlace);extra=chunk(b'PLTE',palette)+chunk(b'tRNS',alpha) if color==3 else chunk(b'tRNS',b'\0\1')
            encoded=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',header)+extra+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b'')
            cases.append(dict(name=f'png-{color}-{depth}-adam{interlace}',encoded=b64(encoded),rgba=b64(expected),width=width,height=height,max=0,rmse=0))
image=Image.new('RGB',(31,23));image.putdata([((x*5+y*2)%256,(x*2+y*7)%256,(x*3+y*5)%256) for y in range(23) for x in range(31)])
profile=bytearray(ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes());profile[24:36]=struct.pack('>6H',2026,10,21,0,0,0);profile=bytes(profile)
for mode in ['RGB','L','CMYK']:
    for progressive in [False,True]:
        for sampling in ([0,1,2] if mode=='RGB' else [0]):
            output=io.BytesIO();image.convert(mode).save(output,format='JPEG',quality=92,progressive=progressive,subsampling=sampling)
            encoded=output.getvalue();decoded=Image.open(io.BytesIO(encoded)).convert('RGBA')
            cases.append(dict(name=f'jpeg-{mode}-progressive{progressive}-sampling{sampling}',encoded=b64(encoded),rgba=b64(decoded.tobytes()),width=31,height=23,max=5,rmse=1.5,unsupported=mode=='CMYK' and progressive))
for format in ['PNG','JPEG']:
    output=io.BytesIO();image.save(output,format=format,icc_profile=profile,quality=95,subsampling=0)
    encoded=output.getvalue();decoded=Image.open(io.BytesIO(encoded)).convert('RGBA')
    cases.append(dict(name=f'{format.lower()}-srgb-icc',encoded=b64(encoded),rgba=b64(decoded.tobytes()),width=31,height=23,max=5 if format=='JPEG' else 1,rmse=1 if format=='JPEG' else .5))
p3=bytearray(profile)
colorants={b'rXYZ':(.515102,.241182,-.001049),b'gXYZ':(.291965,.692236,.041881),b'bXYZ':(.157153,.066581,.784378)}
for i in range(struct.unpack_from('>I',p3,128)[0]):
    tag,offset,length=struct.unpack_from('>4sII',p3,132+12*i)
    if tag in colorants:
        for j,v in enumerate(colorants[tag]):struct.pack_into('>i',p3,offset+8+j*4,round(v*65536))
p3=bytes(p3);output=io.BytesIO();image.save(output,format='PNG',icc_profile=p3)
converted=ImageCms.profileToProfile(image,ImageCms.ImageCmsProfile(io.BytesIO(p3)),ImageCms.ImageCmsProfile(io.BytesIO(profile)),renderingIntent=1,outputMode='RGBA')
cases.append(dict(name='png-display-p3-to-srgb-icc',encoded=b64(output.getvalue()),rgba=b64(converted.tobytes()),width=31,height=23,max=2,rmse=.6))
target=pathlib.Path(__file__).resolve().parents[1]/'tests/Imaging/fixtures.json'
target.parent.mkdir(parents=True,exist_ok=True)
target.write_text(json.dumps(dict(generator='Python zlib + Pillow 12.2.0 libjpeg/lcms; test only',cases=cases),indent=2)+'\n',encoding='utf-8')
print(f'Generated {len(cases)} independent fixtures: {target}')
