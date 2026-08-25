"""Render a compact 360-degree review turntable from the approved static Mother gate."""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--frames", type=int, default=24)
    return parser.parse_args(argv)


args = parse_args()
output = Path(args.output).resolve()
frame_dir = output / "turntable-frames"
frame_dir.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
camera = bpy.data.objects.get("MotherTF_ReviewCamera") or scene.camera
if camera is None:
    raise RuntimeError("Mother turntable requires the review camera")

scene.camera = camera
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.10

target = Vector((0.0, 0.0, 0.92))
radius = 4.25
for index in range(args.frames):
    angle = 2.0 * math.pi * index / args.frames
    camera.location = (
        radius * math.sin(angle),
        -radius * math.cos(angle),
        0.99,
    )
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(frame_dir / f"mother-turntable-{index:03d}.png")
    bpy.ops.render.render(write_still=True)

print(f"MOTHER_PROOF11_TURNTABLE_RENDERED:{args.frames}")
