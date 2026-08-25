"""Render the untouched user-owned Yuuka FBX as a static routing reference."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


ARGS = args()
SOURCE = Path(ARGS.input).resolve()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(SOURCE), use_anim=False)

meshes = sorted((obj for obj in bpy.context.scene.objects if obj.type == "MESH"), key=lambda obj: obj.name)
armatures = sorted((obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), key=lambda obj: obj.name)
if not meshes or not armatures:
    raise RuntimeError("Expected Yuuka mesh and armature")

body = max(meshes, key=lambda obj: len(obj.data.vertices))
for obj in meshes:
    obj.hide_render = obj is not body
for obj in armatures:
    obj.scale = (1.0, 1.0, 1.0)
bpy.context.view_layer.update()

points = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
center = (lo + hi) * 0.5
extent = hi - lo

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.world.color = (0.008, 0.012, 0.020)

camera_data = bpy.data.cameras.new("YuukaOwnedReferenceCameraData")
camera = bpy.data.objects.new("YuukaOwnedReferenceCamera", camera_data)
scene.collection.objects.link(camera)
camera_data.type = "ORTHO"
camera_data.ortho_scale = max(extent.z * 1.14, extent.x * 1.35)
scene.camera = camera

for name, location, energy, size in (
    ("YuukaOwnedKey", (-3.5, -4.0, 4.2), 900.0, 3.2),
    ("YuukaOwnedFill", (3.8, -2.0, 2.6), 560.0, 3.0),
    ("YuukaOwnedRim", (0.0, 3.8, 3.0), 720.0, 2.8),
):
    data = bpy.data.lights.new(name + "Data", "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    scene.collection.objects.link(light)
    light.location = Vector(location) * max(extent.z, 1.0) + center
    light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()

distance = max(extent.z, 1.0) * 4.0
views = {
    "front": Vector((0.0, -distance, 0.0)),
    "three-quarter": Vector((distance * 0.66, -distance * 0.76, 0.0)),
    "side": Vector((distance, 0.0, 0.0)),
    "back": Vector((0.0, distance, 0.0)),
}
for label, offset in views.items():
    camera.location = center + offset
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(OUTPUT / f"yuuka-owned-original-{label}.png")
    bpy.ops.render.render(write_still=True)

receipt = {
    "status": "DIAGNOSTIC_ONLY_UNTOUCHED_OWNED_SURFACE_REFERENCE",
    "source": str(SOURCE),
    "bodyObject": body.name,
    "bodyVertices": len(body.data.vertices),
    "bodyPolygons": len(body.data.polygons),
    "armatures": [{"name": obj.name, "bones": len(obj.data.bones)} for obj in armatures],
    "meshObjects": [{"name": obj.name, "vertices": len(obj.data.vertices)} for obj in meshes],
    "bounds": {"min": list(lo), "max": list(hi)},
    "note": "No source mesh/material/rig edits; static internal routing reference only.",
}
(OUTPUT / "yuuka-owned-original-reference-receipt.json").write_text(
    json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8"
)
print("YUUKA_OWNED_ORIGINAL_REFERENCE_RENDERED")
