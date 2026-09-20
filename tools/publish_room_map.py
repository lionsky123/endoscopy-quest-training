"""Publish the room's shared six-station layout, diagram and clearance report. No Unity/build launch."""
from pathlib import Path
import hashlib
import html
import json
import math
import struct
import uuid
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ROOM = ROOT / 'app/Assets/EndoscopyTheme/Resources/EndoscopyRoom'
SOURCE = ROOT / 'app/Tools/Authoring/visitor-map.json'
PUBLISHED = ROOT / 'app/Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json'
DRAWING = ROOT / 'docs/room-map'


def load_meshes():
    data = (ROOM / 'geometry.bytes').read_bytes()
    assert data[:4] == b'ECR1'
    offset = 4

    def integer():
        nonlocal offset
        value = struct.unpack_from('<i', data, offset)[0]
        offset += 4
        return value

    meshes = []
    for _ in range(integer()):
        length = integer()
        name = data[offset:offset + length].decode('utf-8')
        offset += length
        count = integer()
        xyz = np.frombuffer(data, dtype='<f4', count=count * 8, offset=offset).reshape(-1, 8)[:, :3].copy()
        xyz[:, [0, 2]] *= -1  # Same normalization as VirtualRoomEnvironment.
        offset += count * 32
        for _ in range(integer()):
            integer()
            indices = integer()
            offset += indices * 4
        meshes.append(dict(name=name, lo=xyz.min(axis=0), hi=xyz.max(axis=0), xyz=xyz))
    assert offset == len(data)
    return meshes


def p(x, z):
    return dict(x=x, y=0, z=z)


def sample_route(corners):
    # Round each right-angle bend within a 0.22 m envelope, then sample at <= 5 cm.
    knots = [corners[0]]
    for i in range(1, len(corners) - 1):
        a, b, c = (np.array(corners[n], dtype=float) for n in (i - 1, i, i + 1))
        before = b + (a - b) / np.linalg.norm(a - b) * .22
        after = b + (c - b) / np.linalg.norm(c - b) * .22
        knots.append(before.tolist())
        for t in np.linspace(0, 1, 13)[1:]:
            knots.append(((1-t)**2 * before + 2*(1-t)*t*b + t*t*after).tolist())
    knots.append(corners[-1])
    samples = [p(*knots[0])]
    for a, b in zip(knots, knots[1:]):
        length = math.dist(a, b)
        for i in range(1, max(1, math.ceil(length / .05)) + 1):
            t = i / max(1, math.ceil(length / .05))
            samples.append(p(round(a[0] + (b[0]-a[0])*t, 5), round(a[1] + (b[1]-a[1])*t, 5)))
    return samples


def main():
    meshes = load_meshes()
    source = json.loads(SOURCE.read_text(encoding='utf-8'))
    definition = {k: source[k] for k in ('mapId', 'roomResource', 'scale', 'start', 'points', 'speed',
                                        'waitDistance', 'resumeDistance', 'departureRadius', 'fairyJoinRadius')}
    definition['sourceDigest'] = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    definition['modelDigest'] = hashlib.sha256((ROOM / 'geometry.bytes').read_bytes()).hexdigest()
    definition['routes'] = [dict(from_=r['from'], to=r['to'], samples=sample_route(r['corners'])) for r in source['routes']]
    for route in definition['routes']:
        route['from'] = route.pop('from_')
    obstacles = [m for m in meshes if m['hi'][1] > .15 and m['lo'][1] < 1.8]
    closest = 100
    for i, route in enumerate(definition['routes']):
        assert route['samples'][0] == (source['start'] if i == 0 else source['points'][i-1]['position'])
        assert route['samples'][-1] == source['points'][i]['position']
        for s in route['samples']:
            x, z = s['x'], s['z']
            # Conservative walking footprint, using actual exported mesh bounds.
            for mesh in obstacles:
                lo, hi = mesh['lo'], mesh['hi']
                dx = max(float(lo[0])-x, 0, x-float(hi[0]))
                dz = max(float(lo[2])-z, 0, z-float(hi[2]))
                clearance = math.hypot(dx, dz)
                closest = min(closest, clearance)
                assert clearance >= .4, (route['to'], s, mesh['name'], clearance)
    PUBLISHED.write_text(json.dumps(definition, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    DRAWING.mkdir(exist_ok=True)
    draw(meshes, definition, source)
    report = dict(meshes=len(meshes), stations=len(definition['points']), legs=len(definition['routes']),
                  samples=sum(len(r['samples']) for r in definition['routes']),
                  minimumHorizontalClearanceMetres=round(closest, 3),
                  totalRouteMetres=round(sum(math.dist((a['x'], a['z']), (b['x'], b['z']))
                    for r in definition['routes'] for a, b in zip(r['samples'], r['samples'][1:])), 2),
                  modelDigest=definition['modelDigest'], sourceDigest=definition['sourceDigest'],
                  method='Conservative mesh AABB clearance, 0.4m radius, <=0.05m route sampling; not headset acceptance.')
    (DRAWING / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    for path in list(ROOM.rglob('*')) + [ROOM, ROOM.parent, ROOT / 'app/Assets/BotanicalGardenQR/Experience/Bootstrap/VirtualRoomEnvironment.cs']:
        meta = Path(str(path) + '.meta')
        if path.suffix == '.meta' or meta.exists():
            continue
        contents = f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n'
        if path.is_dir(): contents += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
        meta.write_text(contents, encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))


def draw(meshes, definition, source):
    width, height = 1720, 1130
    image = Image.new('RGB', (width, height), '#f5f7fa')
    d = ImageDraw.Draw(image)
    font_path = 'C:/Windows/Fonts/msyh.ttc'
    font = ImageFont.truetype(font_path, 24)
    small = ImageFont.truetype(font_path, 20)
    title = ImageFont.truetype(font_path, 42)
    number = ImageFont.truetype(font_path, 25)
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}"><rect width="100%" height="100%" fill="#f5f7fa"/>']

    def text(x, y, value, size=24, color='#25354a'):
        d.text((x, y), value, font=title if size == 42 else small if size == 20 else font, fill=color)
        svg.append(f'<text x="{x}" y="{y+size}" font-family="Microsoft YaHei,sans-serif" font-size="{size}" fill="{color}">{html.escape(value)}</text>')

    def rect(box, fill):
        d.rectangle(box, fill=fill)
        x,y,x2,y2=box
        svg.append(f'<rect x="{x:.1f}" y="{y:.1f}" width="{x2-x:.1f}" height="{y2-y:.1f}" fill="{fill}"/>')

    def xy(x, z): return 125 + (x+8)*88, 205 + (3.85-z)*88

    text(90, 43, '清洗消毒室 · VR 六站地图', 42)
    text(92, 106, '固定起点与初始面向  /  小精灵全程受同一路线网约束  /  转场沿过道绕行', 24)
    # Actual floor triangles preserve the stepped room boundary.
    for m in meshes:
        if '楼板' in m['name']:
            for tri in m['xyz'].reshape(-1, 3, 3):
                pts = [xy(float(v[0]), float(v[2])) for v in tri]
                d.polygon(pts, fill='#e0e7ed')
                svg.append('<polygon points="'+' '.join(f'{x:.1f},{y:.1f}' for x,y in pts)+'" fill="#e0e7ed"/>')
    for m in meshes:
        if m['lo'][1] >= 1.8 or m['hi'][1] <= .15: continue
        color = '#809198' if '墙' in m['name'] else '#42a8a1' if 'tripo' in m['name'] else '#b9c5d0'
        if '门' in m['name']: color = '#f1b565'
        x1,y1=xy(float(m['lo'][0]), float(m['hi'][2])); x2,y2=xy(float(m['hi'][0]),float(m['lo'][2]))
        rect((x1,y1,x2,y2), color)
    colors = ['#267dc0','#267dc0','#267dc0','#267dc0','#267dc0','#267dc0']
    for i,r in enumerate(definition['routes']):
        pts=[xy(s['x'],s['z']) for s in r['samples']]
        d.line(pts, fill=colors[i], width=5, joint='curve')
        svg.append('<polyline points="'+' '.join(f'{x:.1f},{y:.1f}' for x,y in pts)+f'" fill="none" stroke="{colors[i]}" stroke-width="5"/>')
        mid=len(pts)//2; a,b=pts[max(0,mid-3)],pts[min(len(pts)-1,mid+3)]
        ang=math.atan2(b[1]-a[1],b[0]-a[0]); x,y=pts[mid]
        triangle=[(x+11*math.cos(ang),y+11*math.sin(ang)),(x+10*math.cos(ang+2.5),y+10*math.sin(ang+2.5)),(x+10*math.cos(ang-2.5),y+10*math.sin(ang-2.5))]
        d.polygon(triangle,fill=colors[i])
        svg.append('<polygon points="'+' '.join(f'{x:.1f},{y:.1f}' for x,y in triangle)+f'" fill="{colors[i]}"/>')
    for i,point in enumerate(definition['points']):
        x,y=xy(point['position']['x'],point['position']['z'])
        d.ellipse((x-23,y-23,x+23,y+23),fill='#1f527d',outline='white',width=3)
        d.text((x,y-1),str(i+1),font=number,fill='white',anchor='mm')
        svg.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="23" fill="#1f527d" stroke="white" stroke-width="3"/><text x="{x:.1f}" y="{y+9:.1f}" text-anchor="middle" font-size="25" fill="white">{i+1}</text>')
    x,y=xy(source['start']['x'],source['start']['z'])
    d.ellipse((x-13,y-13,x+13,y+13),fill='#d97932')
    svg.append(f'<circle cx="{x}" cy="{y}" r="13" fill="#d97932"/>')
    text(x-70,y+23,'固定起点 · 面朝 01',20)
    # A fixed model-space heading; the first valid headset sample aligns once.
    ax, ay = xy(source['start']['x'] - 1.0, source['start']['z'])
    d.line([(x-20, y), (ax, ay)], fill='#d97932', width=8)
    arrow = [(ax, ay), (ax+17, ay-10), (ax+17, ay+10)]
    d.polygon(arrow, fill='#d97932')
    svg.append(f'<path d="M {x-20} {y} L {ax} {ay}" stroke="#d97932" stroke-width="8"/><polygon points="'+ ' '.join(f'{px},{py}' for px,py in arrow)+'" fill="#d97932"/>')
    text(106,169,'模型 +Z ↑    （非地理北向）',20)
    text(1240,169,'原模型外轮廓约 15.8 × 7.6 m',20)
    labels=['全景观察 · 放大镜与精灵','工序排序 · 现场处置','器械视频 · 清单核查',
            '毛刷状态 · 水路证据','瓶体抓取 · 产品资料','记录对账 · 附件追溯']
    for i,label in enumerate(labels):
        col=i%3;row=i//3
        text(100+col*525,916+row*52,f'{i+1:02d}  {label}')
    text(100,1040,'蓝线：完整教学路线（审阅图）    青色：原模型槽体    灰色：墙 / 柜    橙色：门 / 起点',20)
    text(100,1077,'开场、带路、对话停留和归线共用模型路线；运行仅显示当前路段，玩家实际步行。',20)
    image.save(DRAWING/'six-station-map.png')
    (DRAWING/'six-station-map.svg').write_text('\n'.join(svg)+'</svg>\n',encoding='utf-8')


if __name__ == '__main__': main()
