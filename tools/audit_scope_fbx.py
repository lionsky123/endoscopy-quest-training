"""Read-only source topology audit; never imports/executes embedded FBX content.

Binary encoding reference (public-domain description):
https://code.blender.org/2013/08/fbx-binary-file-format-specification/
This bounded reader supports only pre-7500 binary FBX, not general FBX semantics.
"""
import argparse
import hashlib
import json
import re
import struct
import zlib
import zipfile
from collections import Counter
from pathlib import Path

import numpy as np


class Reader:
    def __init__(self, data, metadata_only=False):
        self.data = data
        self.metadata_only = metadata_only
        if data[:23] != b"Kaydara FBX Binary  \x00\x1a\x00":
            raise ValueError("Not binary FBX")
        self.version = struct.unpack_from("<I", data, 23)[0]
        if not 7000 <= self.version < 7500:
            raise ValueError("Unsupported FBX version")
        self.pos = 27

    def take(self, count):
        if count < 0 or self.pos + count > len(self.data):
            raise ValueError("Out-of-range record")
        result = self.data[self.pos:self.pos + count]
        self.pos += count
        return result

    def unpack(self, fmt):
        return struct.unpack(fmt, self.take(struct.calcsize(fmt)))

    def prop(self):
        kind = self.take(1).decode("ascii")
        scalar = {"Y": "h", "C": "?", "I": "i", "F": "f", "D": "d", "L": "q"}
        if kind in scalar:
            return self.unpack("<" + scalar[kind])[0]
        if kind in "SR":
            count, = self.unpack("<I")
            raw = self.take(count)
            return raw.decode("utf-8", errors="replace") if kind == "S" else {"blob_bytes": count}
        dtype = {"f": "<f4", "d": "<f8", "i": "<i4", "l": "<i8", "b": "u1", "c": "u1"}[kind]
        count, encoding, size = self.unpack("<III")
        expected = count * np.dtype(dtype).itemsize
        if expected > 256 * 1024 * 1024:
            raise ValueError("Oversized array")
        raw = self.take(size)
        if self.metadata_only:
            return {"array_type": kind, "count": count, "encoding": encoding}
        if encoding == 1:
            decoder = zlib.decompressobj()
            raw = decoder.decompress(raw, expected + 1)
            if not decoder.eof:
                raise ValueError("Incomplete or oversized compressed array")
        elif encoding != 0:
            raise ValueError("Unsupported array encoding")
        if len(raw) != expected:
            raise ValueError("Array length mismatch")
        return np.frombuffer(raw, dtype=dtype)

    def node(self, depth=0):
        if depth > 64:
            raise ValueError("Excessive nesting")
        end, count, length, name_len = self.unpack("<IIIB")
        if end == count == length == name_len == 0:
            return None
        if not self.pos <= end <= len(self.data) or count > 1000000:
            raise ValueError("Invalid node boundary")
        name = self.take(name_len).decode("utf-8")
        props_start = self.pos
        props = [self.prop() for _ in range(count)]
        if self.pos - props_start != length:
            raise ValueError("Property byte count mismatch")
        children = []
        while self.pos < end:
            child = self.node(depth + 1)
            if child is None:
                break
            children.append(child)
        if self.pos != end:
            raise ValueError("Node end mismatch")
        return {"name": name, "props": props, "children": children}


def named(node, name):
    return [n for n in node["children"] if n["name"] == name]


def topology(vertices, polygons, weld=False):
    tolerance = max(float(np.ptp(vertices, axis=0).max()) * 1e-7, 1e-10)
    if weld:
        _, indices = np.unique(np.rint(vertices / tolerance).astype(np.int64), axis=0, return_inverse=True)
    else:
        indices = np.arange(len(vertices))
    parent = list(range(int(indices.max()) + 1))

    def root(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    first = None
    faces = 0
    polygon_sizes = Counter()
    current_size = 0
    for raw in polygons:
        vertex = int(raw) if raw >= 0 else -int(raw) - 1
        if not 0 <= vertex < len(vertices):
            raise ValueError("Invalid polygon index")
        node = int(indices[vertex])
        current_size += 1
        if first is None:
            first = node
        else:
            parent[root(node)] = root(first)
        if raw < 0:
            faces += 1
            polygon_sizes[current_size] += 1
            current_size = 0
            first = None
    if current_size:
        raise ValueError("Unterminated polygon")
    labels = np.array([root(int(i)) for i in indices])
    counts = Counter(labels.tolist())
    largest = []
    for label, count in counts.most_common(8):
        points = vertices[labels == label]
        largest.append({"vertices": count, "min": points.min(axis=0).tolist(), "max": points.max(axis=0).tolist()})
    return {"components": len(counts), "faces": faces, "polygon_sizes": dict(polygon_sizes),
            "position_weld_tolerance": tolerance if weld else 0, "largest_components": largest}


def audit(path, inventory_only=False, zip_entry=None):
    if zip_entry:
        with zipfile.ZipFile(path, metadata_encoding="gbk") as archive:
            if archive.getinfo(zip_entry).file_size > 256 * 1024 * 1024:
                raise ValueError("Oversized archived model")
            data = archive.read(zip_entry)
    else:
        data = Path(path).read_bytes()
    reader = Reader(data, inventory_only)
    nodes = []
    while True:
        node = reader.node()
        if node is None:
            break
        nodes.append(node)
    objects = next(n for n in nodes if n["name"] == "Objects")["children"]
    report = {"source": str(Path(path).resolve()), "sha256": hashlib.sha256(data).hexdigest(),
              "fbx_version": reader.version, "object_types": dict(Counter(n["name"] for n in objects)),
              "models": [{"name": n["props"][1], "type": n["props"][2]} for n in objects if n["name"] == "Model"],
              "deformers": [{"name": n["props"][1], "type": n["props"][2]} for n in objects if n["name"] == "Deformer"],
              "geometry": []}
    if zip_entry:
        report["zip_entry"] = zip_entry
    if inventory_only:
        report["model_count"] = len(report["models"])
        report["candidate_names_only"] = [m for m in report["models"] if re.search(r"内镜|胃镜|储|镜|柜|endo|scope", m["name"], re.I)]
        report["name_samples"] = report.pop("models")[:12]
        report["note"] = "Name candidates are not identification or proof of an upright endoscope. No geometry was decompressed or modified."
        return report
    for node in objects:
        if node["name"] != "Geometry" or node["props"][2] != "Mesh":
            continue
        vertices = named(node, "Vertices")[0]["props"][0].reshape((-1, 3))
        polygons = named(node, "PolygonVertexIndex")[0]["props"][0]
        report["geometry"].append({"name": node["props"][1], "vertices": len(vertices),
            "source_bounds": {"min": vertices.min(axis=0).tolist(), "max": vertices.max(axis=0).tolist()},
            "uv_layers": len(named(node, "LayerElementUV")), "normal_layers": len(named(node, "LayerElementNormal")),
            "indexed": topology(vertices, polygons), "position_welded_audit_only": topology(vertices, polygons, True)})
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source")
    parser.add_argument("--inventory-only", action="store_true")
    parser.add_argument("--zip-entry")
    args = parser.parse_args()
    print(json.dumps(audit(args.source, args.inventory_only, args.zip_entry), ensure_ascii=True, indent=2))
