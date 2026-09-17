"""Inspect the generated runtime binary without opening Unity or changing original geometry."""
from pathlib import Path
import struct,json
ROOT=Path(__file__).resolve().parents[1]
data=(ROOT/'app/Assets/Endoscopy/Resources/WashingRoom/geometry.bytes').read_bytes()
offset=4
def integer():
    global offset
    v=struct.unpack_from('<i',data,offset)[0];offset+=4;return v
objects=[]
for _ in range(integer()):
    length=integer();name=data[offset:offset+length].decode('utf-8');offset+=length
    count=integer();lo=[1e9]*3;hi=[-1e9]*3
    for v in struct.iter_unpack('<8f',data[offset:offset+count*32]):
        pos=(-v[0],v[1],-v[2])
        for a in range(3):lo[a]=min(lo[a],pos[a]);hi[a]=max(hi[a],pos[a])
    offset+=count*32
    for _ in range(integer()):
        material=integer();indices=integer();offset+=indices*4
    objects.append(dict(name=name,min=lo,max=hi,triangles=count//3))
(ROOT/'artifacts/model/runtime-bounds.json').write_text(json.dumps(objects,ensure_ascii=False,indent=2),encoding='utf-8')
for o in objects:
    print(o['name'], 'min',*[round(x,2) for x in o['min']], 'max',*[round(x,2) for x in o['max']])
