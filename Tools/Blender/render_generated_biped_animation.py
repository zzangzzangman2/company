"""Render every authored animation frame of a generated humanoid GLB.

The script is designed for hidden/background QA.  It imports the untouched GLB,
keeps its material, skeleton, weights and action, and renders deterministic
front-three-quarter and side views for visual inspection.
"""

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
    parser.add_argument("--glb", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--size", type=int, default=512)
    parser.add_argument("--frame-start", type=int)
    parser.add_argument("--frame-end", type=int)
    return parser.parse_args(argv)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def evaluated_world_bounds(meshes):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    corners = []
    for source in meshes:
        obj = source.evaluated_get(depsgraph)
        corners.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    minimum = Vector(tuple(min(point[axis] for point in corners) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in corners) for axis in range(3)))
    return minimum, maximum


def add_area_light(name, location, energy, size):
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(light)
    light.location = location
    light.rotation_euler = (Vector((0.0, 0.0, 0.9)) - light.location).to_track_quat(
        "-Z", "Y"
    ).to_euler()
    return light


args = parse_args()
glb_path = Path(args.glb).resolve()
output_root = Path(args.output).resolve()
output_root.mkdir(parents=True, exist_ok=True)

clear_scene()
scene = bpy.context.scene
scene.render.fps = 30
scene.render.fps_base = 1.0
bpy.ops.import_scene.gltf(filepath=str(glb_path))
armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
meshes = [obj for obj in scene.objects if obj.type == "MESH"]
skinned = [
    obj
    for obj in meshes
    if any(mod.type == "ARMATURE" for mod in obj.modifiers)
    or (obj.parent is not None and obj.parent.type == "ARMATURE")
]
if len(armatures) != 1 or not skinned:
    raise RuntimeError(
        f"Expected one armature and a skinned mesh, found {len(armatures)} / {len(skinned)}"
    )
armature = armatures[0]
actions = list(bpy.data.actions)
if len(actions) != 1:
    raise RuntimeError(f"Expected one action, found {[action.name for action in actions]}")
if armature.animation_data is None:
    armature.animation_data_create()
armature.animation_data.action = actions[0]

frame_start = (
    args.frame_start
    if args.frame_start is not None
    else int(math.floor(actions[0].frame_range[0]))
)
frame_end = (
    args.frame_end
    if args.frame_end is not None
    else int(math.ceil(actions[0].frame_range[1]))
)
scene.frame_start = frame_start
scene.frame_end = frame_end
scene.frame_set(frame_start)
bpy.context.view_layer.update()

minimum, maximum = evaluated_world_bounds(skinned)
center = (minimum + maximum) * 0.5
extent = maximum - minimum
height = max(extent.z, 1.0e-6)
ground_z = minimum.z

camera_data = bpy.data.cameras.new("GeneratedBipedQaCamera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = max(height * 1.20, extent.x * 1.55, extent.y * 1.55)
camera = bpy.data.objects.new("GeneratedBipedQaCamera", camera_data)
bpy.context.collection.objects.link(camera)
scene.camera = camera

floor_size = max(height * 2.2, 2.0)
bpy.ops.mesh.primitive_plane_add(size=floor_size, location=(center.x, center.y, ground_z))
floor = bpy.context.object
floor.name = "GeneratedBipedQaFloor"
floor_material = bpy.data.materials.new("GeneratedBipedQaFloorMaterial")
floor_material.diffuse_color = (0.18, 0.19, 0.22, 1.0)
floor.data.materials.append(floor_material)

add_area_light(
    "GeneratedBipedQaKey",
    center + Vector((-height * 2.0, -height * 2.2, height * 2.8)),
    1250.0,
    height * 2.2,
)
add_area_light(
    "GeneratedBipedQaFill",
    center + Vector((height * 2.0, height * 0.8, height * 1.6)),
    750.0,
    height * 1.8,
)

scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x = args.size
scene.render.resolution_y = args.size
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.use_file_extension = True
scene.render.image_settings.color_depth = "8"
if scene.world is None:
    scene.world = bpy.data.worlds.new("GeneratedBipedQaWorld")
scene.world.color = (0.055, 0.055, 0.07)
scene.view_settings.look = "AgX - Medium High Contrast"

distance = height * 4.0
views = {
    "three-quarter": Vector((1.0, -1.55, 0.72)).normalized(),
    "side": Vector((1.0, 0.0, 0.42)).normalized(),
}
look_at = Vector((center.x, center.y, ground_z + height * 0.52))

for view_name, direction in views.items():
    view_dir = output_root / view_name
    view_dir.mkdir(parents=True, exist_ok=True)
    camera.location = look_at + direction * distance
    camera.rotation_euler = (look_at - camera.location).to_track_quat("-Z", "Y").to_euler()
    for frame in range(frame_start, frame_end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        scene.render.filepath = str(view_dir / f"frame-{frame:04d}.png")
        bpy.ops.render.render(write_still=True)

print(
    "GENERATED_BIPED_ANIMATION_RENDERED "
    f"{frame_start}..{frame_end} views={','.join(views)} output={output_root}"
)
