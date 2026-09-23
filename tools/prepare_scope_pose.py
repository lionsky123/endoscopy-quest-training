"""Offline source-preserving pose experiment; never edits the imported FBX."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from audit_scope_fbx import Reader, named


# Source-space centreline controls measured against scope-pose-coordinates.png.
# These are a draft deformation cage, not replacement pipe geometry.
INSERTION = np.array([[-.403, -.102], [-.372, -.183], [-.302, -.254],
                      [-.207, -.307], [-.09, -.35], [.04, -.392],
                      [.18, -.419], [.30, -.436], [.365, -.44]])
CORD = np.array([[-.113, -.153], [.015, -.18], [.16, -.194], [.31, -.166],
                 [.427, -.092], [.477, -.008], [.444, .072], [.347, .121],
                 [.212, .136], [.062, .129], [-.061, .142], [-.145, .201],
                 [-.154, .29], [-.1, .373], [-.002, .428], [.098, .433],
                 [.181, .398], [.239, .361], [.281, .335]])


def smooth_curve(controls):
    padded = np.vstack((2 * controls[0] - controls[1], controls, 2 * controls[-1] - controls[-2]))
    sections = []
    for i in range(1, len(padded) - 2):
        a, b, c, d = padded[i - 1:i + 3]
        t = np.linspace(0, 1, 30, endpoint=False)[:, None]
        sections.append(.5 * (2*b + (-a+c)*t + (2*a-5*b+4*c-d)*t*t + (-a+3*b-3*c+d)*t*t*t))
    return np.vstack((*sections, controls[-1:]))


def rotate(points, angle):
    c, s = np.cos(angle), np.sin(angle)
    return points @ np.array([[c, s], [-s, c]])


def curve_mapping(points, controls, body_angle):
    source = smooth_curve(controls)
    delta = np.diff(source, axis=0)
    lengths = np.linalg.norm(delta, axis=1)
    tangent = delta / lengths[:, None]
    arc = np.r_[0, np.cumsum(lengths)]
    initial = np.arctan2(tangent[0, 1], tangent[0, 0]) + body_angle
    # Continuous bend at the attachment; after the first section, hang vertically.
    change = ((-np.pi/2 - initial + np.pi) % (2*np.pi)) - np.pi
    u = np.clip(arc[:-1] / .18, 0, 1)
    angle = initial + change * (u*u*(3-2*u))
    target_tangent = np.column_stack((np.cos(angle), np.sin(angle)))
    target = np.vstack((rotate(source[0:1], body_angle),
                        rotate(source[0:1], body_angle) + np.cumsum(target_tangent * lengths[:, None], axis=0)))
    result = np.empty_like(points)
    distances = np.empty(len(points))
    for start in range(0, len(points), 512):
        p = points[start:start+512]
        offset = p[:, None, :] - source[:-1]
        along = np.clip(np.sum(offset * tangent, axis=2) / lengths, 0, 1)
        residual = offset - along[:, :, None] * delta
        squared = np.sum(residual*residual, axis=2)
        index = np.argmin(squared, axis=1)
        row = np.arange(len(p))
        distance = residual[row, index]
        normal = np.column_stack((-tangent[index, 1], tangent[index, 0]))
        radius = np.sum(distance * normal, axis=1)
        target_normal = np.column_stack((-target_tangent[index, 1], target_tangent[index, 0]))
        result[start:start+len(p)] = target[index] + along[row, index, None] * lengths[index, None] * target_tangent[index] + radius[:, None] * target_normal
        distances[start:start+len(p)] = np.sqrt(squared[row, index])
    return result, distances, target[-1], np.arctan2(target_tangent[-1, 1], target_tangent[-1, 0]) - np.arctan2(tangent[-1, 1], tangent[-1, 0])


def pose(points, label=False):
    body_angle = -1.13
    out = points.copy()
    out[:, :2] = rotate(points[:, :2], body_angle)
    if label:
        return out
    x, y = points[:, 0], points[:, 1]
    insertion, _, _, _ = curve_mapping(points[:, :2], INSERTION, body_angle)
    cord, _, end, end_angle = curve_mapping(points[:, :2], CORD, body_angle)
    # Explicit source-region chart masks keep the control head and its label rigid.
    insertion_mask = (y < -.14) & (y < -.24 - .30*x)
    cord_mask = ((x > -.11) | (y > .075)) & ~insertion_mask
    connector_mask = (x > .268) & (y > .18)
    out[insertion_mask, :2] = insertion[insertion_mask]
    out[cord_mask, :2] = cord[cord_mask]
    out[connector_mask, :2] = rotate(points[connector_mask, :2] - CORD[-1], end_angle) + end
    return out


def draft(meshes, output):
    arrays = {}
    original, derived = [], []
    for i, mesh in enumerate(meshes):
        points = named(mesh, "Vertices")[0]["props"][0].reshape((-1, 3))
        changed = pose(points, label=i != 0)
        arrays[f"vertices_{i}"] = changed
        # Vertex ordering is retained: source polygon indices and UVs remain usable.
        arrays[f"source_vertices_{i}"] = points
        arrays[f"polygons_{i}"] = named(mesh, "PolygonVertexIndex")[0]["props"][0]
        original.append(points)
        derived.append(changed)
    np.savez_compressed(str(output) + ".npz", **arrays)
    image = Image.new("RGB", (1000, 1200), "#202830")
    draw = ImageDraw.Draw(image)
    draw.text((20, 12), "EXPERIMENTAL SOURCE-MESH REPOSE / NOT APPROVED FOR RUNTIME", fill="white")
    bounds = np.vstack(derived)
    lower, upper = bounds.min(axis=0), bounds.max(axis=0)
    scale = min(900/(upper[0]-lower[0]), 1080/(upper[1]-lower[1]))
    for i, points in enumerate(derived):
        for p in points[np.argsort(points[:, 2])]:
            shade = int(np.clip(100 + p[2]*600, 80, 250))
            draw.point((50+(p[0]-lower[0])*scale, 1140-(p[1]-lower[1])*scale), fill=(shade, shade, shade) if i == 0 else "orange")
    image.save(str(output) + ".png")
    checks = []
    for i, (before, after) in enumerate(zip(original, derived)):
        checks.append(deformation_metrics(before, after, arrays[f"polygons_{i}"]))
    return {"status": "experimental_not_runtime_approved", "source_unchanged": True,
            "vertex_order_preserved": True, "source_uv_and_materials_not_modified": True,
            "note": "NPZ stores draft positions and original polygon indices only; not a published Unity model. Geometry gates do not establish clinical fidelity.",
            "meshes": checks}


def deformation_metrics(before, after, polygons):
    edges, triangles, face = [], [], []
    for raw in polygons:
        face.append(-int(raw)-1 if raw < 0 else int(raw))
        if raw < 0:
            edges.extend(zip(face, face[1:]+face[:1]))
            triangles.extend((face[0], face[j], face[j+1]) for j in range(1, len(face)-1))
            face = []
    if face:
        raise ValueError("Unterminated polygon")
    edges = np.unique(np.sort(np.array(edges), axis=1), axis=0)
    lengths = np.linalg.norm(before[edges[:, 1]]-before[edges[:, 0]], axis=1)
    changed = np.linalg.norm(after[edges[:, 1]]-after[edges[:, 0]], axis=1)
    usable = lengths > 1e-9
    ratio = changed[usable]/lengths[usable]
    tri = np.array(triangles)
    def area(points):
        return np.linalg.norm(np.cross(points[tri[:, 1]]-points[tri[:, 0]], points[tri[:, 2]]-points[tri[:, 0]]), axis=1)*.5
    old_area, new_area = area(before), area(after)
    newly_degenerate = (old_area > 1e-12) & (new_area < old_area*1e-3)
    stretched = int(np.sum(ratio > 2))
    collapsed = int(np.sum(ratio < .5))
    worst = np.argsort(ratio)[-8:][::-1]
    source_edges = edges[usable]
    return {"vertices": len(before), "triangles": len(tri), "edges": len(edges),
            "finite": bool(np.isfinite(after).all()), "edge_length_ratio_max": float(ratio.max()),
            "edge_length_ratio_p99": float(np.quantile(ratio, .99)),
            "edges_stretched_over_2x": stretched, "edges_compressed_below_half": collapsed,
            "newly_degenerate_triangles": int(newly_degenerate.sum()),
            "largest_stretch_locations": [{"ratio": float(ratio[j]), "source_midpoint": before[source_edges[j]].mean(axis=0).tolist()} for j in worst],
            "geometry_gate_passed": bool(np.isfinite(after).all() and stretched == 0 and collapsed == 0 and not newly_degenerate.any())}


def draft_geometry_valid(report):
    meshes = report.get("meshes", [])
    return bool(meshes) and all(item.get("geometry_gate_passed") is True for item in meshes)


def load_meshes(path):
    reader = Reader(Path(path).read_bytes())
    roots = []
    while True:
        node = reader.node()
        if node is None:
            break
        roots.append(node)
    return [n for n in next(n for n in roots if n["name"] == "Objects")["children"]
            if n["name"] == "Geometry" and n["props"][2] == "Mesh"]


def overview(meshes, output):
    image = Image.new("RGB", (1200, 1200), "#202830")
    draw = ImageDraw.Draw(image)
    for t in np.arange(-.5, .51, .1):
        x, y = 600 + t * 1050, 600 - t * 1050
        draw.line((x, 30, x, 1170), fill="#39434d")
        draw.line((30, y, 1170, y), fill="#39434d")
        draw.text((x, 12), f"x={t:.1f}", fill="white")
        draw.text((4, y), f"y={t:.1f}", fill="white")
    for i, node in enumerate(meshes):
        points = named(node, "Vertices")[0]["props"][0].reshape((-1, 3))
        for p in points[np.argsort(points[:, 2])]:
            tone = int(np.clip(100 + p[2] * 600, 80, 250))
            draw.point((600 + p[0] * 1050, 600 - p[1] * 1050), fill=(tone, tone, tone) if i == 0 else "orange")
    image.save(output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source")
    parser.add_argument("output")
    parser.add_argument("--draft", action="store_true")
    parser.add_argument("--require-valid", action="store_true", help="Exit 2 when any draft geometry gate fails; does not approve clinical or runtime fidelity.")
    args = parser.parse_args()
    if args.require_valid and not args.draft:
        parser.error("--require-valid requires --draft")
    if args.draft:
        report = draft(load_meshes(args.source), args.output)
        report["source_sha256"] = hashlib.sha256(Path(args.source).read_bytes()).hexdigest()
        print(json.dumps(report, indent=2))
        if args.require_valid and not draft_geometry_valid(report):
            print("REJECTED: draft geometry is not publishable", file=sys.stderr)
            sys.exit(2)
    else:
        overview(load_meshes(args.source), args.output)
