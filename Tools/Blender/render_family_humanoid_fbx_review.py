"""Render deterministic rest views and the first walk cycle from a family FBX."""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--fbx", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--size", type=int, default=768)
    parser.add_argument("--frame-start", type=int, default=1)
    parser.add_argument("--frame-end", type=int, default=43)
    return parser.parse_args(argv)


def bounds(mesh, depsgraph):
    evaluated = mesh.evaluated_get(depsgraph)
    points = [evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box]
    lo = Vector(tuple(min(point[i] for point in points) for i in range(3)))
    hi = Vector(tuple(max(point[i] for point in points) for i in range(3)))
    return lo, hi


def add_area(name, location, energy, size, target):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(light)
    light.location = location
    light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()


def main():
    options = args()
    output = Path(options.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(Path(options.fbx).resolve()))
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = options.size
    scene.render.resolution_y = options.size
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.fps = 30
    if scene.world is None:
        scene.world = bpy.data.worlds.new("FamilyReviewWorld")
    scene.world.color = (0.055, 0.055, 0.055)

    armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    skinned = [obj for obj in meshes if any(mod.type == "ARMATURE" for mod in obj.modifiers)]
    if len(armatures) != 1 or len(skinned) != 1:
        raise RuntimeError(f"Expected one armature/skinned mesh, found {len(armatures)}/{len(skinned)}")
    armature = armatures[0]
    mesh = skinned[0]
    actions = list(bpy.data.actions)
    if not actions:
        raise RuntimeError("FBX contains no action")
    action = actions[0]
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = action

    depsgraph = bpy.context.evaluated_depsgraph_get()
    armature.data.pose_position = "REST"
    scene.frame_set(options.frame_start)
    bpy.context.view_layer.update()
    lo, hi = bounds(mesh, depsgraph)
    center = (lo + hi) * 0.5
    height = hi.z - lo.z

    camera_data = bpy.data.cameras.new("FamilyReviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = height * 1.12
    camera = bpy.data.objects.new("FamilyReviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    distance = height * 4.0

    add_area("ReviewKey", center + Vector((-2.6, -3.2, 3.8)), 650.0, 4.0, center)
    add_area("ReviewFill", center + Vector((3.0, -1.0, 2.2)), 380.0, 3.0, center)
    add_area("ReviewRim", center + Vector((0.0, 3.0, 3.1)), 420.0, 3.0, center)

    views = {
        "front": Vector((0.0, -1.0, 0.0)),
        "three-quarter": Vector((0.72, -0.72, 0.0)),
        "side": Vector((1.0, 0.0, 0.0)),
        "back": Vector((0.0, 1.0, 0.0)),
    }

    def place_camera(direction):
        camera.location = center + direction.normalized() * distance
        camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()

    for name, direction in views.items():
        place_camera(direction)
        scene.render.filepath = str(output / f"rest-{name}.png")
        bpy.ops.render.render(write_still=True)

    armature.data.pose_position = "POSE"
    for frame in range(options.frame_start, options.frame_end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        for name in ("three-quarter", "side"):
            place_camera(views[name])
            scene.render.filepath = str(output / name / f"frame-{frame:03d}.png")
            Path(scene.render.filepath).parent.mkdir(parents=True, exist_ok=True)
            bpy.ops.render.render(write_still=True)

    print(f"FAMILY_HUMANOID_FBX_REVIEW_RENDERED={output}")


if __name__ == "__main__":
    main()
