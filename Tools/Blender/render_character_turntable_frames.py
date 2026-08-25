"""Render a deterministic 360-degree turntable from an approved static-gate blend."""

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
    parser.add_argument("--frames", type=int, default=32)
    parser.add_argument("--size", type=int, default=512)
    parser.add_argument("--prefix", default="turntable")
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
camera = scene.camera or bpy.data.objects.get("SisterProof11Camera")
body = bpy.data.objects.get("Yuuka_Original_Body")
if camera is None or body is None:
    raise RuntimeError("Expected an active camera and Yuuka_Original_Body")

# The authored body bounds are the stable framing authority. Hidden donor
# islands remain inside this same object, so this does not depend on temporary
# generated bridge names or on camera state left by the static render script.
points = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
extent = hi - lo
center = (lo + hi) * 0.5
distance = max(extent.z, 1.0) * 4.0

camera.data.type = "ORTHO"
camera.data.ortho_scale = max(extent.z * 1.16, extent.x * 1.42)
scene.render.resolution_x = ARGS.size
scene.render.resolution_y = ARGS.size
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False

for frame_index in range(ARGS.frames):
    angle = 2.0 * math.pi * frame_index / ARGS.frames
    offset = Vector((math.sin(angle) * distance, -math.cos(angle) * distance, 0.0))
    camera.location = center + offset
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(OUTPUT / f"{ARGS.prefix}-{frame_index:03d}.png")
    bpy.ops.render.render(write_still=True)

print(f"TURNTABLE_FRAMES_RENDERED {ARGS.frames} {OUTPUT}")
