"""Render consistent hand closeups while keeping the complete rigged character.

The script never saves or mutates the input file.  It uses a temporary
Workbench camera/light setup and frames the evaluated hand-weighted vertices.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re

import bpy
from mathutils import Vector


def args():
    argv = list(__import__("sys").argv)
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--kind", choices=("blend", "fbx"), required=True)
    parser.add_argument("--label", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--side", choices=("L", "R"), default="L")
    return parser.parse_args(argv)


ARGS = args()
INPUT = os.path.abspath(ARGS.input)
OUTPUT = os.path.abspath(ARGS.output_dir)


def load():
    if ARGS.kind == "blend":
        bpy.ops.wm.open_mainfile(filepath=INPUT)
    else:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        bpy.ops.import_scene.fbx(filepath=INPUT)


def choose_armature():
    return max((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), key=lambda o: len(o.data.bones))


def choose_body(armature):
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bound = [o for o in meshes if any(m.type == "ARMATURE" and m.object == armature for m in o.modifiers)]
    return max(bound or meshes, key=lambda o: len(o.data.vertices))


def side_of(name):
    patterns = {
        "L": re.compile(r"(?:^|[ ._])L(?:eft)?(?:[ ._]|$)", re.I),
        "R": re.compile(r"(?:^|[ ._])R(?:ight)?(?:[ ._]|$)", re.I),
    }
    for side, pattern in patterns.items():
        if pattern.search(name):
            return side
    return None


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


load()
scene = bpy.context.scene
scene.frame_set(scene.frame_current)
depsgraph = bpy.context.evaluated_depsgraph_get()
depsgraph.update()
armature = choose_armature()
body = choose_body(armature)
evaluated = body.evaluated_get(depsgraph)
eval_mesh = evaluated.to_mesh()

group_indices = {
    group.index
    for group in body.vertex_groups
    if side_of(group.name) == ARGS.side and ("hand" in group.name.lower() or "finger" in group.name.lower() or "thumb" in group.name.lower())
}
indices = []
for vertex in body.data.vertices:
    if any(member.group in group_indices and member.weight > 1e-6 for member in vertex.groups):
        indices.append(vertex.index)
if not indices:
    raise RuntimeError("No hand-weighted vertices found")
points = [evaluated.matrix_world @ eval_mesh.vertices[i].co for i in indices]
lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
center = (lo + hi) * 0.5
dims = hi - lo

# The rig and complete character stay in the scene.  Workbench gives a clean,
# source-independent topology read without texture alpha or shader differences.
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 768
scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.display.shading.light = "STUDIO"
scene.display.shading.studio_light = "paint.sl"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "BOTH"
scene.display.shading.curvature_ridge_factor = 1.8
scene.display.shading.curvature_valley_factor = 1.2
scene.display.shading.background_type = "VIEWPORT"
scene.display.shading.background_color = (0.035, 0.045, 0.060)

camera_data = bpy.data.cameras.new("HandAuditCameraData")
camera = bpy.data.objects.new("HandAuditCamera", camera_data)
scene.collection.objects.link(camera)
camera_data.type = "ORTHO"
camera_data.lens = 55
camera_data.clip_start = 0.0001
camera_data.clip_end = 10000.0
scene.camera = camera

# Include some wrist context but keep fingers large enough to count.
scale = max(float(dims.x), float(dims.y), float(dims.z)) * 1.65
camera_data.ortho_scale = max(scale, 1e-5)
distance = max(scale * 5.0, 1.0)
views = {
    "front": Vector((center.x, center.y - distance, center.z)),
    "palm": Vector((center.x, center.y + distance, center.z)),
    "side": Vector((center.x + (distance if ARGS.side == "L" else -distance), center.y, center.z)),
}

os.makedirs(OUTPUT, exist_ok=True)
rendered = []
for view, location in views.items():
    camera.location = location
    look_at(camera, center)
    path = os.path.join(OUTPUT, f"{ARGS.label}-{ARGS.side}-{view}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    rendered.append(path)

modifier_targets = [m.object.name if m.object else None for m in body.modifiers if m.type == "ARMATURE"]
receipt = {
    "schema": "family-company.hand-closeup-render.v1",
    "label": ARGS.label,
    "input": INPUT,
    "side": ARGS.side,
    "body": body.name,
    "armature": armature.name,
    "fullArmatureBoneCount": len(armature.data.bones),
    "armatureStillPresent": armature.name in scene.objects,
    "bodyArmatureModifierTargets": modifier_targets,
    "handWeightedVertexCount": len(indices),
    "handBoundsWorld": {
        "min": list(lo),
        "max": list(hi),
        "dimensions": list(dims),
    },
    "views": rendered,
    "note": "Complete character and full armature remained in scene; only the camera was cropped around the evaluated hand.",
}
with open(os.path.join(OUTPUT, f"{ARGS.label}-{ARGS.side}-render-receipt.json"), "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)
evaluated.to_mesh_clear()
print("HAND_CLOSEUPS=" + OUTPUT)
