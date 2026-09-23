"""Trace the original cord surface adjacency; diagnostic only, not new tubing."""
import argparse
import heapq
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from audit_scope_fbx import named
from prepare_scope_pose import load_meshes


def trace(source, output, exclude_right_arc=False):
    mesh = load_meshes(source)[0]
    original = named(mesh, "Vertices")[0]["props"][0].reshape((-1, 3))
    faces = named(mesh, "PolygonVertexIndex")[0]["props"][0].reshape((-1, 3))
    faces = np.where(faces < 0, -faces-1, faces)
    _, first, inverse = np.unique(np.rint(original/1e-7).astype(np.int64), axis=0, return_index=True, return_inverse=True)
    p = original[first]
    f = inverse[faces]
    edges = np.unique(np.sort(np.vstack((f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]])), axis=1), axis=0)
    mask = ((p[:, 0] > -.12) | (p[:, 1] > .075)) & (p[:, 1] > -.24-.30*p[:, 0])
    if exclude_right_arc:
        # Analysis graph only: test whether the other side of the loop still
        # connects the attachment to the connector. Never remove source faces.
        mask &= ~((p[:, 0] > .13) & (p[:, 0] < .25) & (p[:, 1] > .20) & (p[:, 1] < .32))
    edges = edges[mask[edges].all(axis=1)]
    graph = [[] for _ in p]
    for (a, b), length in zip(edges, np.linalg.norm(p[edges[:, 0]]-p[edges[:, 1]], axis=1)):
        graph[a].append((b, length))
        graph[b].append((a, length))
    root = mask & (p[:, 0] < -.09) & (p[:, 1] < -.12)
    distance = np.full(len(p), np.inf)
    parent = np.full(len(p), -1)
    queue = []
    for i in np.flatnonzero(root):
        distance[i] = 0
        heapq.heappush(queue, (0., int(i)))
    while queue:
        value, i = heapq.heappop(queue)
        if value != distance[i]:
            continue
        for j, length in graph[i]:
            candidate = value+length
            if candidate < distance[j]:
                distance[j] = candidate
                parent[j] = i
                heapq.heappush(queue, (candidate, int(j)))
    reachable = mask & np.isfinite(distance)
    connector = np.flatnonzero(reachable & (p[:, 0] > .39) & (p[:, 1] > .19))
    if not len(connector):
        raise ValueError("Connector not reachable from source cord attachment")
    endpoint = connector[np.argmin(distance[connector])]
    path = [endpoint]
    while parent[path[-1]] >= 0:
        path.append(parent[path[-1]])
    path = np.array(path[::-1])
    image = Image.new("RGB", (1200, 1200), "#202830")
    draw = ImageDraw.Draw(image)
    draw.text((20, 12), "ORIGINAL SURFACE GEODESIC / cyan-yellow distance / red shortest surface path / not a centreline", fill="white")
    for i in np.argsort(p[:, 2]):
        if reachable[i]:
            t = distance[i]/max(distance[reachable].max(), 1e-9)
            color = (int(40+210*t), int(200-30*t), int(240-210*t))
        else:
            color = (90, 90, 90)
        draw.point((600+p[i, 0]*1050, 600-p[i, 1]*1050), fill=color)
    draw.line([(600+p[i, 0]*1050, 600-p[i, 1]*1050) for i in path], fill="red", width=3)
    image.save(str(output)+".png")
    np.savez_compressed(str(output)+".npz", source_vertices=p, original_to_welded=inverse,
                        cord_mask=mask, geodesic_distance=distance, surface_path=path)
    return {"cord_vertices": int(mask.sum()), "reachable_vertices": int(reachable.sum()),
            "exclude_right_arc_analysis_only": exclude_right_arc,
            "unreachable_vertices": int((mask & ~reachable).sum()), "path_vertices": len(path),
            "path_length_source_units": float(distance[endpoint]),
            "note": "Surface shortest path, not centreline. Welding is analysis-only; original model unchanged."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source")
    parser.add_argument("output")
    parser.add_argument("--exclude-right-arc", action="store_true")
    args = parser.parse_args()
    print(json.dumps(trace(args.source, args.output, args.exclude_right_arc), indent=2))
