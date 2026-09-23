"""Geometry-only contact sheets for inspecting unlabelled source meshes.

These are audit plots, never replacement room textures or production assets.
"""
import argparse
import json
import math
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from audit_scope_fbx import Reader, named


def render(source, entry, output):
    with zipfile.ZipFile(source, metadata_encoding="gbk") as archive:
        if archive.getinfo(entry).file_size > 256 * 1024 * 1024:
            raise ValueError("Oversized source")
        data = archive.read(entry)
    reader = Reader(data)
    roots = []
    while True:
        node = reader.node()
        if node is None:
            break
        roots.append(node)
    objects = next(n for n in roots if n["name"] == "Objects")["children"]
    models = {n["props"][0]: n["props"][1].split("\x00")[0] for n in objects if n["name"] == "Model"}
    links = next(n for n in roots if n["name"] == "Connections")["children"]
    parents = {n["props"][1]: n["props"][2] for n in links if n["name"] == "C" and n["props"][0] == "OO"}
    geometries = [n for n in objects if n["name"] == "Geometry" and n["props"][2] == "Mesh"]
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 12)
    target = Path(output)
    target.mkdir(parents=True, exist_ok=True)
    rows = []
    for page in range(math.ceil(len(geometries) / 48)):
        sheet = Image.new("RGB", (1200, 1780), (24, 30, 35))
        draw = ImageDraw.Draw(sheet)
        draw.text((12, 8), "SOURCE MESH AUDIT / geometry only / local coordinates / NOT material or clinical identification", font=font, fill="white")
        for slot, node in enumerate(geometries[page * 48:(page + 1) * 48]):
            index = page * 48 + slot
            vertices = named(node, "Vertices")[0]["props"][0].reshape((-1, 3))
            source_vertex_count = len(vertices)
            all_vertices = vertices
            polygon_indices = named(node, "PolygonVertexIndex")[0]["props"][0].astype(np.int64)
            referenced = np.unique(np.where(polygon_indices < 0, -polygon_indices - 1, polygon_indices))
            if not len(referenced) or referenced.min() < 0 or referenced.max() >= len(vertices):
                raise ValueError("Invalid or empty polygon references")
            vertices = vertices[referenced]
            minimum, maximum = vertices.min(axis=0), vertices.max(axis=0)
            size = maximum - minimum
            name = models.get(parents.get(node["props"][0]), node["props"][1].split("\x00")[0])
            rows.append({"index": index, "geometry_id": node["props"][0], "model": name,
                         "source_vertices": source_vertex_count, "referenced_vertices": len(vertices), "source_bounds_min": minimum.tolist(), "source_bounds_max": maximum.tolist()})
            # Fixed deterministic sample and orthographic isometric projection.
            sample = vertices[::max(1, len(vertices) // 8000)] - (minimum + maximum) / 2
            sample /= max(float(size.max()), 1e-9)
            projection = sample @ np.array([[.866, -.25, .433], [-.5, -.433, .75], [0, .866, .5]])
            order = np.argsort(projection[:, 2])
            ox, oy = (slot % 6) * 200, (slot // 6) * 220 + 28
            draw.rectangle((ox + 2, oy + 2, ox + 197, oy + 216), outline=(65, 75, 80))
            # Sparse architectural meshes need edges, not just corner dots.
            if len(polygon_indices) < 30000:
                projected = ((all_vertices - (minimum + maximum) / 2) / max(float(size.max()), 1e-9)) @ np.array([[.866, -.25, .433], [-.5, -.433, .75], [0, .866, .5]])
                face = []
                for raw in polygon_indices:
                    p = projected[-int(raw) - 1 if raw < 0 else int(raw)]
                    face.append((ox + 100 + p[0] * 155, oy + 104 - p[1] * 155))
                    if raw < 0:
                        draw.line(face + face[:1], fill=(95, 128, 145), width=1)
                        face = []
            for point in projection[order]:
                x, y = ox + 100 + point[0] * 155, oy + 104 - point[1] * 155
                tone = int(np.clip(160 + point[2] * 65, 90, 230))
                draw.ellipse((x - .7, y - .7, x + .7, y + .7), fill=(tone - 30, tone, min(255, tone + 20)))
            draw.text((ox + 8, oy + 176), f"{index:03d} {name[:24]}", font=font, fill="white")
            draw.text((ox + 8, oy + 196), "dims " + "/".join(f"{value:.3g}" for value in size), font=font, fill=(190, 190, 190))
        sheet.save(target / f"source-meshes-{page + 1}.png")
    return {"source_entry": entry, "mesh_count": len(geometries), "note": "Geometry-only plots; no source geometry, material, or runtime changed.", "meshes": rows}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source")
    parser.add_argument("entry")
    parser.add_argument("output")
    args = parser.parse_args()
    print(json.dumps(render(args.source, args.entry, args.output), ensure_ascii=True, indent=2))
