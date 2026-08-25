"""Render eight equally spaced review views from the saved Player Human V5 proof.

Run after opening player-human-v5-proof4.blend in background mode.  The script
does not save or modify the source blend; it only writes review images and a
small yaw map into the requested output directory.
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def script_args() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


args = script_args()
if len(args) != 1:
    raise SystemExit("usage: blender <proof.blend> --background --python <this-script> -- <output-dir>")

output_dir = Path(args[0]).resolve()
output_dir.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
camera = bpy.data.objects.get("PlayerV5_ProofCamera")
if camera is None or camera.type != "CAMERA":
    raise RuntimeError("PlayerV5_ProofCamera is missing from the proof blend")

scene.camera = camera
camera.data.type = "ORTHO"
camera.data.ortho_scale = 4.72
scene.render.resolution_x = 1200
scene.render.resolution_y = 1600
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False

radius = 8.4
camera_z = 2.12
target = Vector((0.0, 0.0, 2.08))
frames = []

for index, yaw_degrees in enumerate(range(0, 360, 45)):
    yaw = math.radians(yaw_degrees)
    # yaw 0 is the same front direction as Proof4; positive yaw proceeds to
    # front-right, right profile, rear-right, back, and around to front-left.
    camera.location = (radius * math.sin(yaw), -radius * math.cos(yaw), camera_z)
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    filename = f"player-human-v5-turntable-{index:02d}.png"
    scene.render.filepath = str(output_dir / filename)
    bpy.ops.render.render(write_still=True)
    frames.append(
        {
            "frame": index,
            "yawDegrees": yaw_degrees,
            "file": filename,
            "cameraLocation": [round(value, 6) for value in camera.location],
        }
    )

(output_dir / "player-human-v5-turntable8-map.json").write_text(
    json.dumps(
        {
            "schema": "family-company.player-human-v5-turntable8.v1",
            "sourceBlend": "../" + Path(bpy.data.filepath).name,
            "frameDurationSeconds": 0.5,
            "loop": True,
            "camera": {
                "type": "ORTHO",
                "orthoScale": 4.72,
                "radius": radius,
                "height": camera_z,
                "target": list(target),
            },
            "frames": frames,
        },
        ensure_ascii=False,
        indent=2,
    )
    + "\n",
    encoding="utf-8",
)

print(f"Rendered {len(frames)} turntable views to {output_dir}")
