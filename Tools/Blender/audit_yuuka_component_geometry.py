"""Write geometry and skinning metadata for Yuuka's disconnected body islands."""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

import bpy


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--object", default="Yuuka_Original_Body")
    parser.add_argument("--component", action="append", type=int, default=[])
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.parent.mkdir(parents=True, exist_ok=True)

body = bpy.data.objects.get(ARGS.object)
if body is None:
    raise RuntimeError(f"Expected {ARGS.object} in the open blend")

mesh = body.data
parent = list(range(len(mesh.vertices)))


def find(value):
    while parent[value] != value:
        parent[value] = parent[parent[value]]
        value = parent[value]
    return value


def union(left, right):
    left_root = find(left)
    right_root = find(right)
    if left_root != right_root:
        parent[right_root] = left_root


for edge in mesh.edges:
    union(edge.vertices[0], edge.vertices[1])

roots = defaultdict(set)
for vertex in mesh.vertices:
    roots[find(vertex.index)].add(vertex.index)
ordered_roots = sorted(roots, key=lambda root: min(roots[root]))
component_by_vertex = {}
for component_id, root in enumerate(ordered_roots):
    for vertex_index in roots[root]:
        component_by_vertex[vertex_index] = component_id

polygons_by_component = defaultdict(list)
for polygon in mesh.polygons:
    component_ids = {component_by_vertex[index] for index in polygon.vertices}
    if len(component_ids) != 1:
        raise RuntimeError("Polygon crosses component roots")
    polygons_by_component[component_ids.pop()].append(polygon)

records = []
for component_id, root in enumerate(ordered_roots):
    vertex_indices = sorted(roots[root])
    points = [mesh.vertices[index].co for index in vertex_indices]
    lo = [min(point[axis] for point in points) for axis in range(3)]
    hi = [max(point[axis] for point in points) for axis in range(3)]
    bone_weights = Counter()
    for vertex_index in vertex_indices:
        for membership in mesh.vertices[vertex_index].groups:
            bone_weights[body.vertex_groups[membership.group].name] += float(membership.weight)
    polygons = polygons_by_component[component_id]
    material_counts = Counter(
        body.material_slots[polygon.material_index].material.name
        if polygon.material_index < len(body.material_slots)
        and body.material_slots[polygon.material_index].material is not None
        else "<none>"
        for polygon in polygons
    )
    record = {
            "component": component_id,
            "vertices": len(vertex_indices),
            "polygons": len(polygons),
            "minVertex": vertex_indices[0],
            "maxVertex": vertex_indices[-1],
            "bboxMin": [round(float(value), 8) for value in lo],
            "bboxMax": [round(float(value), 8) for value in hi],
            "extent": [round(float(hi[axis] - lo[axis]), 8) for axis in range(3)],
            "materials": dict(material_counts.most_common()),
            "topBones": [
                {"name": name, "weight": round(weight, 4)}
                for name, weight in bone_weights.most_common(8)
            ],
        }
    if component_id in ARGS.component:
        record["vertexCoordinates"] = {
            str(index): [round(float(value), 8) for value in mesh.vertices[index].co]
            for index in vertex_indices
        }
        record["faces"] = [list(polygon.vertices) for polygon in polygons]
    records.append(record)

OUTPUT.write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"WROTE_YUUKA_COMPONENT_AUDIT {OUTPUT}")
