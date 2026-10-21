"""Independent stdlib zlib/PNG fixtures; this generator is test tooling only."""
import base64, json, random, struct, zlib
from pathlib import Path

def b64(data): return base64.b64encode(data).decode('ascii')
def pattern(name, length):
    if name == 'zero': return bytes(length)
    if name == 'cycle': return bytes((i * 17 + i // 7) % 256 for i in range(length))
    if name == 'runs': return bytes((i // 1000) % 256 for i in range(length))
    if name == 'high': return bytes([255]) * length
    state = 20261012
    out = bytearray()
    for i in range(length):
        state = (state * 1664525 + 1013904223) & 0xffffffff
        out.append(state >> 24)
    return bytes(out)

def chunk(name, data):
    return struct.pack('>I', len(data)) + name + data + struct.pack('>I', zlib.crc32(name + data))

def paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p-a), abs(p-b), abs(p-c)
    return a if pa <= pb and pa <= pc else b if pb <= pc else c

fixtures = []
for length in [0, 1, 7, 31, 32, 33, 258, 4096, 5551, 5552, 5553, 32767, 32768, 32769, 65535, 65536, 150000]:
    for name in ['zero', 'cycle', 'runs', 'high']:
        for strategy in [zlib.Z_DEFAULT_STRATEGY, zlib.Z_FIXED, zlib.Z_HUFFMAN_ONLY]:
            raw = pattern(name, length)
            compressor = zlib.compressobj(6, zlib.DEFLATED, 15, 8, strategy)
            encoded = compressor.compress(raw) + compressor.flush()
            fixtures.append(dict(Name=f'{name}-{length}-{strategy}', Pattern=name, Length=length, Encoded=b64(encoded)))
for length in [31, 30001, 65536]:
    raw = pattern('noise', length)
    for level in [0, 1, 9]:
        fixtures.append(dict(Name=f'noise-{length}-{level}', Pattern='noise', Length=length, Encoded=b64(zlib.compress(raw, level))))

pngs = []
for kind, channels in [(0,1),(2,3),(4,2),(6,4)]:
    for filt in range(5):
        width, height, depth = 11, 7, 8
        rows = [bytes((x*23 + y*31 + channel*71) % 256 for x in range(width) for channel in range(channels)) for y in range(height)]
        filtered = bytearray()
        for y, row in enumerate(rows):
            filtered.append(filt)
            for i, value in enumerate(row):
                left = row[i-channels] if i >= channels else 0
                up = rows[y-1][i] if y else 0
                ul = rows[y-1][i-channels] if y and i >= channels else 0
                predicted = [0,left,up,(left+up)//2,paeth(left,up,ul)][filt]
                filtered.append((value-predicted) & 255)
        rgba = bytearray()
        for row in rows:
            for x in range(width):
                pixel = row[x*channels:(x+1)*channels]
                if kind == 0: rgba.extend([pixel[0]]*3 + [255])
                elif kind == 2: rgba.extend(pixel + bytes([255]))
                elif kind == 4: rgba.extend([pixel[0]]*3 + [pixel[1]])
                else: rgba.extend(pixel)
        png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB',width,height,depth,kind,0,0,0)) + chunk(b'IDAT',zlib.compress(filtered)) + chunk(b'IEND',b'')
        pngs.append(dict(Name=f'png-type{kind}-filter{filt}', Encoded=b64(png), Expected=b64(rgba)))
# Exercise SIMD block boundaries and every predictor with independent random pixels.
for width in [1,2,3,4,5,7,8,9,11,15,16,17,31,32,33,63,64,65,127,128,129,257]:
    for kind, channels in [(0,1),(2,3),(4,2),(6,4)]:
        for filt in range(5):
            height=5
            rng=random.Random(20261021+width*100+kind*10+filt)
            rows=[bytes(rng.randrange(256) for _ in range(width*channels)) for _ in range(height)]
            filtered=bytearray()
            expected=bytearray()
            for y,row in enumerate(rows):
                filtered.append(filt)
                for i,value in enumerate(row):
                    left=row[i-channels] if i>=channels else 0
                    up=rows[y-1][i] if y else 0
                    corner=rows[y-1][i-channels] if y and i>=channels else 0
                    filtered.append((value-[0,left,up,(left+up)//2,paeth(left,up,corner)][filt])&255)
                for x in range(width):
                    pixel=row[x*channels:(x+1)*channels]
                    if kind==0: expected.extend([pixel[0]]*3+[255])
                    elif kind==2: expected.extend(pixel+bytes([255]))
                    elif kind==4: expected.extend([pixel[0]]*3+[pixel[1]])
                    else: expected.extend(pixel)
            header=chunk(b'IHDR',struct.pack('>IIBBBBB',width,height,8,kind,0,0,0))
            encoded=zlib.compress(filtered)
            # Multiple and zero-length consecutive IDAT chunks also test pooled gathering.
            at=len(encoded)//2
            data=chunk(b'IDAT',encoded[:at])+chunk(b'IDAT',b'')+chunk(b'IDAT',encoded[at:])
            png=b'\x89PNG\r\n\x1a\n'+header+data+chunk(b'IEND',b'')
            pngs.append(dict(Name=f'png-simd-{width}-{kind}-{filt}',Encoded=b64(png),Expected=b64(expected)))

for depth in [1,2,4,8]:
    for kind in [0,3]:
        width, height = 13,3
        rows, expected = [],bytearray()
        palette = bytes((i*71+c*31)%256 for i in range(1<<depth) for c in range(3))
        for y in range(height):
            row = bytearray((width*depth+7)//8)
            for x in range(width):
                sample=(x+y)% (1<<depth)
                row[x*depth//8] |= sample << (8-depth-x*depth%8)
                if kind == 0: expected.extend([sample*255//((1<<depth)-1)]*3+[255])
                else: expected.extend(palette[sample*3:(sample+1)*3] + bytes([255]))
            rows.append(bytes([0])+row)
        png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',width,height,depth,kind,0,0,0))
        if kind == 3: png+=chunk(b'PLTE',palette)
        png+=chunk(b'IDAT',zlib.compress(b''.join(rows)))+chunk(b'IEND',b'')
        pngs.append(dict(Name=f'png-type{kind}-depth{depth}',Encoded=b64(png),Expected=b64(expected)))
# Gamma lookup must retain the old channel transform; sRGB metadata overrides gAMA.
for gamma, srgb in [(100000,False),(45455,False),(100000,True)]:
    raw=bytes([0])+bytes(range(256))*4
    expected=bytearray()
    def convert(value):
        linear=(value/255)**(100000/gamma)
        value=linear*12.92 if linear<=.0031308 else 1.055*linear**(1/2.4)-.055
        return min(255,max(0,round(value*255)))
    for i in range(0,1024,4):
        expected.extend([(v if srgb else convert(v)) for v in raw[1+i:4+i]]+[raw[4+i]])
    png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',256,1,8,6,0,0,0))+chunk(b'gAMA',struct.pack('>I',gamma))
    if srgb: png+=chunk(b'sRGB',bytes([0]))
    png+=chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b'')
    pngs.append(dict(Name=f'png-gamma{gamma}-srgb{srgb}',Encoded=b64(png),Expected=b64(expected)))
path=Path('tests/Optimization/Fixtures/codecs.json')
invalid_pngs=[]
for name,kind,raw,extra in [
    ('invalid-filter',6,bytes([5])+bytes(16)+bytes([0])+bytes(16),b''),
    ('invalid-palette-index',3,bytes([0,0,1,2,0,0,1,0,0,1]),chunk(b'PLTE',bytes([10,20,30,40,50,60])))]:
    png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',4,2,8,kind,0,0,0))+extra+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b'')
    invalid_pngs.append(dict(Name=name,Encoded=b64(png)))
path.parent.mkdir(parents=True,exist_ok=True)
path.write_text(json.dumps(dict(Generator='Python standard library zlib; independent of SimdSkia',Deflate=fixtures,Png=pngs,InvalidPng=invalid_pngs),separators=(',',':')),encoding='utf-8')
print(f'{len(fixtures)} zlib + {len(pngs)} PNG fixtures, {path.stat().st_size} bytes')
