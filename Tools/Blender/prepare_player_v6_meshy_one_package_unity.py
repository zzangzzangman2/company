"""Preserve the Player V6 Meshy mesh/rig/weights/action as one Unity FBX package."""

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


REQUIRED_BONES = {
    "Hips",
    "Spine",
    "Spine01",
    "Spine02",
    "neck",
    "Head",
    "LeftShoulder",
    "LeftArm",
    "LeftForeArm",
    "LeftHand",
    "RightShoulder",
    "RightArm",
    "RightForeArm",
    "RightHand",
    "LeftUpLeg",
    "LeftLeg",
    "LeftFoot",
    "LeftToeBase",
    "RightUpLeg",
    "RightLeg",
    "RightFoot",
    "RightToeBase",
}
KNOWN_RIG_HELPERS = {"Icosphere"}


def parse_args():
    separator = sys.argv.index("--") if "--" in sys.argv else len(sys.argv)
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-glb", required=True)
    parser.add_argument("--output-fbx", required=True)
    parser.add_argument("--output-texture", required=True)
    parser.add_argument("--receipt", required=True)
    return parser.parse_args(sys.argv[separator + 1 :])


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def world_bounds(objects):
    corners = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    minimum = Vector(tuple(min(point[index] for point in corners) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in corners) for index in range(3)))
    return minimum, maximum


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.actions,
    ):
        for datablock in list(collection):
            collection.remove(datablock)


def main():
    args = parse_args()
    source_glb = Path(args.source_glb).resolve()
    output_fbx = Path(args.output_fbx).resolve()
    output_texture = Path(args.output_texture).resolve()
    receipt_path = Path(args.receipt).resolve()
    for path in (output_fbx, output_texture, receipt_path):
        path.parent.mkdir(parents=True, exist_ok=True)

    clear_scene()
    scene = bpy.context.scene
    # glTF animation time is expressed in seconds.  Set the target sample rate
    # before import so the provider's 4.2 s action becomes frames 1..127 and
    # retains its measured 42-frame / 1.4 s authored gait cycle.
    scene.render.fps = 30
    scene.render.fps_base = 1.0
    bpy.ops.import_scene.gltf(filepath=str(source_glb))
    bpy.context.view_layer.update()

    armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one armature, found {len(armatures)}")
    armature = armatures[0]
    skinned = [obj for obj in meshes if any(mod.type == "ARMATURE" for mod in obj.modifiers)]
    if len(skinned) != 1:
        raise RuntimeError(f"Expected one skinned character mesh, found {len(skinned)}")
    mesh = skinned[0]
    auxiliary = [obj for obj in meshes if obj is not mesh]
    unexpected_auxiliary = sorted(obj.name for obj in auxiliary if obj.name not in KNOWN_RIG_HELPERS)
    if unexpected_auxiliary:
        raise RuntimeError(f"Unexpected auxiliary meshes: {unexpected_auxiliary}")
    if any(len(vertex.groups) == 0 for vertex in mesh.data.vertices):
        raise RuntimeError("Character mesh contains unweighted vertices")

    bone_names = {bone.name for bone in armature.data.bones}
    missing = sorted(REQUIRED_BONES - bone_names)
    if missing:
        raise RuntimeError(f"Missing required Humanoid bones: {missing}")

    actions = list(bpy.data.actions)
    if len(actions) != 1 or "Casual_Walk_inplace" not in actions[0].name:
        raise RuntimeError(
            "Expected exactly one Casual_Walk_inplace action; found "
            + repr([action.name for action in actions])
        )
    action = actions[0]
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = action
    if math.floor(action.frame_range[0]) != 1 or math.ceil(action.frame_range[1]) != 127:
        raise RuntimeError(f"Unexpected action range: {tuple(action.frame_range)}")

    images = [
        image
        for image in bpy.data.images
        if image.name != "Render Result" and image.type == "IMAGE" and image.size[0] > 0
    ]
    if len(images) != 1:
        raise RuntimeError(f"Expected one embedded character texture, found {len(images)}")
    materials = list(
        dict.fromkeys(slot.material for slot in mesh.material_slots if slot.material is not None)
    )
    if len(materials) != 1:
        raise RuntimeError(f"Expected one character material, found {len(materials)}")

    removed_auxiliary = [obj.name for obj in auxiliary]
    for obj in auxiliary:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.update()

    armature.name = "PlayerV6MeshyOnePackage_Armature"
    armature.data.name = "PlayerV6MeshyOnePackage_Skeleton"
    mesh.name = "PlayerV6MeshyOnePackage"
    mesh.data.name = "PlayerV6MeshyOnePackage_Mesh"
    materials[0].name = "PlayerV6MeshyOnePackage_Material"
    images[0].name = "player-v6-meshy-one-package-albedo"
    action.name = "PlayerV6_Casual_Walk_inplace"

    scene.frame_start = math.floor(action.frame_range[0])
    scene.frame_end = math.ceil(action.frame_range[1])
    scene.frame_set(scene.frame_start)
    bpy.context.view_layer.update()
    before_min, before_max = world_bounds([mesh])
    armature.location += Vector(
        (
            -(before_min.x + before_max.x) * 0.5,
            -(before_min.y + before_max.y) * 0.5,
            -before_min.z,
        )
    )
    bpy.context.view_layer.update()
    after_min, after_max = world_bounds([mesh])

    images[0].filepath_raw = str(output_texture)
    images[0].file_format = "PNG"
    images[0].save()

    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=str(output_fbx),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        path_mode="RELATIVE",
        embed_textures=False,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_tspace=True,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
    )

    receipt = {
        "contract": "FC-PLAYER-V6-MESHY-ONE-PACKAGE-UNITY-PREP-V1",
        "sourceGlb": str(source_glb),
        "sourceGlbSha256": sha256(source_glb),
        "outputFbx": str(output_fbx),
        "outputFbxSha256": sha256(output_fbx),
        "outputTexture": str(output_texture),
        "outputTextureSha256": sha256(output_texture),
        "meshVertexCount": len(mesh.data.vertices),
        "meshPolygonCount": len(mesh.data.polygons),
        "boneCount": len(bone_names),
        "boneNames": sorted(bone_names),
        "action": action.name,
        "actionFrameStart": float(action.frame_range[0]),
        "actionFrameEnd": float(action.frame_range[1]),
        "animationFps": 30,
        "authoredCycleFirstFrame": 1,
        "authoredCycleLastFrame": 43,
        "authoredCycleSeconds": 1.4,
        "removedAuxiliaryObjects": removed_auxiliary,
        "normalizationSourceBoundsMin": list(before_min),
        "normalizationSourceBoundsMax": list(before_max),
        "normalizedBoundsMin": list(after_min),
        "normalizedBoundsMax": list(after_max),
        "skinPolicy": "unchanged provider mesh, bind skeleton, vertex weights, and authored action",
        "rootMotionPolicy": "Unity office agent owns translation and yaw; visual root curves reset after sampling",
        "productionEligible": False,
    }
    receipt_path.write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print("PLAYER_V6_MESHY_ONE_PACKAGE_UNITY_PREP=PASS")
    print("RECEIPT=" + str(receipt_path))


if __name__ == "__main__":
    main()
