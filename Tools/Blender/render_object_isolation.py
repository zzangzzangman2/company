"""Render named mesh objects alone for internal geometry-overlap diagnosis."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--objects", required=True)
    parser.add_argument("--hide", action="store_true")
    parser.add_argument("--size", type=int, default=768)
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
TARGETS = {name.strip() for name in ARGS.objects.split(",") if name.strip()}

body = bpy.data.objects.get("Yuuka_Original_Body")
camera = bpy.data.objects.get("SisterProof11Camera")
if body is None or camera is None:
    raise RuntimeError("Expected Sister Proof body and camera")

for scene_object in bpy.context.scene.objects:
    if scene_object.type == "MESH":
        scene_object.hide_render = (
            scene_object.name in TARGETS if ARGS.hide else scene_object.name not in TARGETS
        )

points = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
center = (lo + hi) * 0.5
distance = max((hi - lo).z, 1.0) * 4.0
views = {
    "front": Vector((0.0, -distance, 0.0)),
    "three-quarter": Vector((distance * 0.66, -distance * 0.76, 0.0)),
    "side": Vector((distance, 0.0, 0.0)),
}

scene = bpy.context.scene
scene.render.resolution_x = ARGS.size
scene.render.resolution_y = ARGS.size
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
slug = "-".join(sorted(TARGETS)).lower()

for view_name, offset in views.items():
    camera.location = center + offset
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(OUTPUT / f"{slug}-{view_name}.png")
    bpy.ops.render.render(write_still=True)

print("OBJECT_ISOLATION_RENDERED")
