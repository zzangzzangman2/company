"""Internal front/three-quarter highlight audit for selected Yuuka components."""

from __future__ import annotations

import argparse
import sys
from collections import defaultdict
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--ids", required=True)
    parser.add_argument("--isolate", action="store_true")
    parser.add_argument("--body-only", action="store_true")
    parser.add_argument("--size", type=int, default=320)
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
TARGETS = [int(value) for value in ARGS.ids.split(",") if value.strip()]

body = bpy.data.objects.get("Yuuka_Original_Body")
camera = bpy.data.objects.get("SisterProof11Camera")
mouth = bpy.data.objects.get("SisterProof11SurfaceMouth")
if body is None or camera is None:
    raise RuntimeError("Expected Sister Proof11 body and camera")
if mouth is not None:
    mouth.hide_render = True
if ARGS.body_only:
    for scene_object in bpy.context.scene.objects:
        if scene_object.type == "MESH" and scene_object != body:
            scene_object.hide_render = True


def material(name, color):
    result = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    result.use_nodes = True
    principled = result.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Roughness"].default_value = 0.80
    return result


gray = material("YuukaComponentAuditGray", (0.16, 0.18, 0.22))
red = material("YuukaComponentAuditRed", (1.0, 0.025, 0.008))
hidden = bpy.data.materials.get("YuukaComponentAuditHidden") or bpy.data.materials.new("YuukaComponentAuditHidden")
hidden.use_nodes = True
hidden.node_tree.nodes.clear()
hidden_output = hidden.node_tree.nodes.new("ShaderNodeOutputMaterial")
hidden_transparent = hidden.node_tree.nodes.new("ShaderNodeBsdfTransparent")
hidden.node_tree.links.new(hidden_transparent.outputs["BSDF"], hidden_output.inputs["Surface"])
if hasattr(hidden, "surface_render_method"):
    hidden.surface_render_method = "DITHERED"

parent = list(range(len(body.data.vertices)))


def find(value):
    while parent[value] != value:
        parent[value] = parent[parent[value]]
        value = parent[value]
    return value


def union(left, right):
    left_root, right_root = find(left), find(right)
    if left_root != right_root:
        parent[right_root] = left_root


for edge in body.data.edges:
    union(edge.vertices[0], edge.vertices[1])
roots = defaultdict(set)
for vertex in body.data.vertices:
    roots[find(vertex.index)].add(vertex.index)
ordered = sorted(roots, key=lambda root: min(roots[root]))
by_vertex = {}
for component_id, root in enumerate(ordered):
    for vertex_index in roots[root]:
        by_vertex[vertex_index] = component_id
by_component = defaultdict(list)
for polygon in body.data.polygons:
    component_ids = {by_vertex[index] for index in polygon.vertices}
    if len(component_ids) != 1:
        raise RuntimeError("Polygon crosses components")
    by_component[component_ids.pop()].append(polygon.index)

body.data.materials.clear()
body.data.materials.append(hidden if ARGS.isolate else gray)
body.data.materials.append(red)

points = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
center = (lo + hi) * 0.5
distance = max((hi - lo).z, 1.0) * 4.0
views = {
    "front": Vector((0.0, -distance, 0.0)),
    "three-quarter": Vector((distance * 0.66, -distance * 0.76, 0.0)),
}

scene = bpy.context.scene
scene.render.resolution_x = ARGS.size
scene.render.resolution_y = ARGS.size
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"

for component_id in TARGETS:
    for polygon in body.data.polygons:
        polygon.material_index = 1 if polygon.index in by_component[component_id] else 0
    for view_name, offset in views.items():
        camera.location = center + offset
        camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(OUTPUT / f"component-{component_id:03d}-{view_name}.png")
        bpy.ops.render.render(write_still=True)

print("YUUKA_OUTFIT_COMPONENT_HIGHLIGHTS_RENDERED")
