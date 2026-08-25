"""Render eight deterministic review angles from a saved Mother proof blend."""

import argparse
import json
import math
import os
import sys

import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--prefix", default="mother-proof4")
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = os.path.abspath(ARGS.output)
os.makedirs(OUTPUT, exist_ok=True)

scene = bpy.context.scene
camera = bpy.data.objects.get("ReviewCamera")
if camera is None or camera.type != "CAMERA":
    raise RuntimeError("MotherProof4 blend must contain ReviewCamera")

scene.camera = camera
scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False
scene.render.use_file_extension = True
scene.render.use_freestyle = False
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.18

target = Vector((0.0, 0.0, 0.88))
radius = 4.25
camera_z = 1.00
outputs = []
for yaw in range(0, 360, 45):
    radians = math.radians(yaw)
    camera.location = (
        math.sin(radians) * radius,
        -math.cos(radians) * radius,
        camera_z,
    )
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(OUTPUT, f"{ARGS.prefix}-yaw{yaw:03d}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    outputs.append(os.path.basename(path))

receipt = {
    "sourceBlend": bpy.data.filepath,
    "resolution": [900, 900],
    "yawConvention": "0=front, 90=character left side, 180=rear, 270=character right side",
    "angles": list(range(0, 360, 45)),
    "frames": outputs,
    "renderPassOnly": True,
    "test3OrSakurakoUsed": False,
    "unityModified": False,
}
with open(os.path.join(OUTPUT, f"{ARGS.prefix}-turntable8-receipt.json"), "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print("MOTHER_TURNTABLE8_RENDERED: " + ARGS.prefix)
