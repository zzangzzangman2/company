"""Create a no-new-credit SD repair of the rejected Higgsfield Older Sister V2.

The source remains the single paid Higgsfield/Meshy package.  This script changes
only that package's bind geometry and bind joint positions with one deterministic
continuous proportion map, then exports the original skin, UV and action 613.
It never imports a donor mesh, skeleton, weight set or animation.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


REQUIRED_BONES = {
    "Hips", "Spine", "Spine01", "Spine02", "neck", "Head", "head_end",
    "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
    "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
    "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
}

HEAD_GROUPS = {"neck", "Head", "head_end", "headfront"}
TORSO_GROUPS = {"Hips", "Spine", "Spine01", "Spine02"}
LEFT_ARM_GROUPS = {"LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand"}
RIGHT_ARM_GROUPS = {"RightShoulder", "RightArm", "RightForeArm", "RightHand"}
LEFT_LEG_GROUPS = {"LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase"}
RIGHT_LEG_GROUPS = {"RightUpLeg", "RightLeg", "RightFoot", "RightToeBase"}

TARGET_HEAD_TO_HEIGHT = 0.31
TARGET_LEG_TO_HEIGHT = 0.46
TARGET_HIP_X_SCALE = 1.52
TARGET_SHOULDER_X_SCALE = 1.25
TARGET_HEAD_X_SCALE = 1.48
TARGET_HEAD_DEPTH_SCALE = 1.42


def parse_args():
    separator = sys.argv.index("--") if "--" in sys.argv else len(sys.argv)
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-glb", required=True)
    parser.add_argument("--output-fbx", required=True)
    parser.add_argument("--output-texture", required=True)
    parser.add_argument("--uv-mask-npz", required=True)
    parser.add_argument("--receipt", required=True)
    return parser.parse_args(sys.argv[separator + 1 :])


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


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


def world_bounds(objects):
    corners = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    lo = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
    hi = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
    return lo, hi


def piecewise(value, anchors):
    if value <= anchors[0][0]:
        a, b = anchors[0], anchors[1]
    elif value >= anchors[-1][0]:
        a, b = anchors[-2], anchors[-1]
    else:
        for index in range(len(anchors) - 1):
            a, b = anchors[index], anchors[index + 1]
            if a[0] <= value <= b[0]:
                break
    alpha = (value - a[0]) / max(b[0] - a[0], 1e-8)
    return a[1] + (b[1] - a[1]) * alpha


def dominant_category(group_weights):
    totals = {
        "head": sum(group_weights.get(name, 0.0) for name in HEAD_GROUPS),
        "torso": sum(group_weights.get(name, 0.0) for name in TORSO_GROUPS),
        "left_arm": sum(group_weights.get(name, 0.0) for name in LEFT_ARM_GROUPS),
        "right_arm": sum(group_weights.get(name, 0.0) for name in RIGHT_ARM_GROUPS),
        "left_leg": sum(group_weights.get(name, 0.0) for name in LEFT_LEG_GROUPS),
        "right_leg": sum(group_weights.get(name, 0.0) for name in RIGHT_LEG_GROUPS),
    }
    return max(totals, key=totals.get), totals


def main():
    args = parse_args()
    source_glb = Path(args.source_glb).resolve()
    output_fbx = Path(args.output_fbx).resolve()
    output_texture = Path(args.output_texture).resolve()
    uv_mask_npz = Path(args.uv_mask_npz).resolve()
    receipt_path = Path(args.receipt).resolve()
    for path in (output_fbx, output_texture, uv_mask_npz, receipt_path):
        path.parent.mkdir(parents=True, exist_ok=True)

    clear_scene()
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.render.fps_base = 1.0
    bpy.ops.import_scene.gltf(filepath=str(source_glb))
    bpy.context.view_layer.update()

    armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one armature, found {len(armatures)}")
    armature = armatures[0]
    skinned = [
        obj for obj in meshes if any(mod.type == "ARMATURE" for mod in obj.modifiers)
    ]
    if len(skinned) != 1:
        raise RuntimeError(f"Expected one skinned character mesh, found {len(skinned)}")
    mesh = skinned[0]
    auxiliary = [obj for obj in meshes if obj is not mesh]
    unexpected = [obj.name for obj in auxiliary if obj.name != "Icosphere"]
    if unexpected:
        raise RuntimeError("Unexpected auxiliary objects: " + repr(unexpected))

    bone_names = {bone.name for bone in armature.data.bones}
    missing = sorted(REQUIRED_BONES - bone_names)
    if missing:
        raise RuntimeError("Missing required bones: " + repr(missing))
    if any(len(vertex.groups) == 0 for vertex in mesh.data.vertices):
        raise RuntimeError("Character mesh contains unweighted vertices")

    actions = list(bpy.data.actions)
    if len(actions) != 1 or "Casual_Walk_inplace" not in actions[0].name:
        raise RuntimeError("Expected exactly one action 613; found " + repr([a.name for a in actions]))
    action = actions[0]
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = None
    armature.data.pose_position = "REST"
    scene.frame_set(int(math.floor(action.frame_range[0])))
    bpy.context.view_layer.update()

    source_min, source_max = world_bounds([mesh])

    def joint_z(name):
        return (armature.matrix_world @ armature.data.bones[name].head_local).z

    toe_z = 0.5 * (joint_z("LeftToeBase") + joint_z("RightToeBase"))
    hip_z = joint_z("Hips")
    neck_z = joint_z("neck")
    head_end_z = joint_z("head_end")
    skeleton_height = head_end_z - toe_z
    target_hip_z = toe_z + skeleton_height * TARGET_LEG_TO_HEIGHT
    target_neck_z = head_end_z - skeleton_height * TARGET_HEAD_TO_HEIGHT
    z_anchors = [
        (toe_z, toe_z),
        (hip_z, target_hip_z),
        (neck_z, target_neck_z),
        (head_end_z, head_end_z),
    ]

    center_x = 0.5 * (source_min.x + source_max.x)
    center_y = 0.5 * (source_min.y + source_max.y)
    left_shoulder_x = (
        armature.matrix_world @ armature.data.bones["LeftArm"].head_local
    ).x
    right_shoulder_x = (
        armature.matrix_world @ armature.data.bones["RightArm"].head_local
    ).x

    def transform_for_category(point, category):
        z = piecewise(point.z, z_anchors)
        if category == "head":
            x = center_x + (point.x - center_x) * TARGET_HEAD_X_SCALE
            y = center_y + (point.y - center_y) * TARGET_HEAD_DEPTH_SCALE
        elif category == "torso":
            torso_alpha = max(0.0, min(1.0, (point.z - hip_z) / max(neck_z - hip_z, 1e-8)))
            x_scale = TARGET_HIP_X_SCALE + (TARGET_SHOULDER_X_SCALE - TARGET_HIP_X_SCALE) * torso_alpha
            x = center_x + (point.x - center_x) * x_scale
            y = center_y + (point.y - center_y) * 1.24
        elif category == "left_arm":
            target_shoulder = center_x + (left_shoulder_x - center_x) * TARGET_SHOULDER_X_SCALE
            x = target_shoulder + (point.x - left_shoulder_x) * 0.88
            y = center_y + (point.y - center_y) * 1.18
        elif category == "right_arm":
            target_shoulder = center_x + (right_shoulder_x - center_x) * TARGET_SHOULDER_X_SCALE
            x = target_shoulder + (point.x - right_shoulder_x) * 0.88
            y = center_y + (point.y - center_y) * 1.18
        else:
            x = center_x + (point.x - center_x) * TARGET_HIP_X_SCALE
            y = center_y + (point.y - center_y) * 1.30
        return Vector((x, y, z))

    group_names = {group.index: group.name for group in mesh.vertex_groups}
    vertex_categories = []
    mesh_world = mesh.matrix_world.copy()
    inverse_mesh_world = mesh_world.inverted()
    for vertex in mesh.data.vertices:
        weights = {group_names[g.group]: g.weight for g in vertex.groups}
        _, totals = dominant_category(weights)
        total = sum(totals.values())
        if total <= 1e-8:
            raise RuntimeError(f"Unclassified weighted vertex {vertex.index}")
        source_world = mesh_world @ vertex.co
        transformed = Vector((0.0, 0.0, 0.0))
        for category, weight in totals.items():
            if weight > 0.0:
                transformed += transform_for_category(source_world, category) * (weight / total)
        vertex.co = inverse_mesh_world @ transformed
        vertex_categories.append(max(totals, key=totals.get))
    mesh.data.update(calc_edges=True)

    category_code = {
        "head": 1,
        "torso": 2,
        "left_arm": 3,
        "right_arm": 4,
        "left_leg": 5,
        "right_leg": 6,
    }
    mesh.data.calc_loop_triangles()
    uv_layer = mesh.data.uv_layers.active
    if uv_layer is None:
        raise RuntimeError("Character mesh has no active UV layer")
    triangle_uvs = []
    triangle_categories = []
    for triangle in mesh.data.loop_triangles:
        triangle_uvs.append([list(uv_layer.data[index].uv) for index in triangle.loops])
        counts = {}
        for vertex_index in triangle.vertices:
            code = category_code[vertex_categories[vertex_index]]
            counts[code] = counts.get(code, 0) + 1
        triangle_categories.append(max(counts, key=counts.get))
    np.savez_compressed(
        uv_mask_npz,
        uv=np.asarray(triangle_uvs, dtype=np.float32),
        category=np.asarray(triangle_categories, dtype=np.uint8),
    )

    original_bone_heads = {
        bone.name: armature.matrix_world @ bone.head_local for bone in armature.data.bones
    }
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for edit_bone in armature.data.edit_bones:
        source_head_world = original_bone_heads[edit_bone.name]
        if edit_bone.name in HEAD_GROUPS:
            category = "head"
        elif edit_bone.name in TORSO_GROUPS:
            category = "torso"
        elif edit_bone.name in {"LeftShoulder", "RightShoulder"}:
            category = "torso"
        elif edit_bone.name in LEFT_ARM_GROUPS:
            category = "left_arm"
        elif edit_bone.name in RIGHT_ARM_GROUPS:
            category = "right_arm"
        elif edit_bone.name in LEFT_LEG_GROUPS:
            category = "left_leg"
        else:
            category = "right_leg"
        target_head_world = transform_for_category(source_head_world, category)
        delta_local = armature.matrix_world.inverted().to_3x3() @ (target_head_world - source_head_world)
        edit_bone.head += delta_local
        edit_bone.tail += delta_local
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.data.pose_position = "POSE"
    armature.animation_data.action = action
    bpy.context.view_layer.update()

    removed_auxiliary = [obj.name for obj in auxiliary]
    for obj in auxiliary:
        bpy.data.objects.remove(obj, do_unlink=True)

    images = [
        image for image in bpy.data.images
        if image.name != "Render Result" and image.type == "IMAGE" and image.size[0] > 0
    ]
    materials = list(dict.fromkeys(
        slot.material for slot in mesh.material_slots if slot.material is not None
    ))
    if len(images) != 1 or len(materials) != 1:
        raise RuntimeError(f"Expected one image/material, found {len(images)}/{len(materials)}")

    armature.name = "OlderSisterV3HiggsfieldSdRepair_Armature"
    armature.data.name = "OlderSisterV3HiggsfieldSdRepair_Skeleton"
    mesh.name = "OlderSisterV3HiggsfieldSdRepair"
    mesh.data.name = "OlderSisterV3HiggsfieldSdRepair_Mesh"
    materials[0].name = "OlderSisterV3HiggsfieldSdRepair_Material"
    images[0].name = "older-sister-v3-higgsfield-sd-repair-albedo"
    action.name = "OlderSisterV3_Casual_Walk_inplace"

    scene.frame_start = math.floor(action.frame_range[0])
    scene.frame_end = math.ceil(action.frame_range[1])
    scene.frame_set(scene.frame_start)
    bpy.context.view_layer.update()
    before_normalization_min, before_normalization_max = world_bounds([mesh])
    armature.location += Vector((
        -(before_normalization_min.x + before_normalization_max.x) * 0.5,
        -(before_normalization_min.y + before_normalization_max.y) * 0.5,
        -before_normalization_min.z,
    ))
    bpy.context.view_layer.update()
    output_min, output_max = world_bounds([mesh])

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

    def repaired_joint_z(name):
        return (armature.matrix_world @ armature.data.bones[name].head_local).z

    repaired_toe_z = 0.5 * (repaired_joint_z("LeftToeBase") + repaired_joint_z("RightToeBase"))
    repaired_height = repaired_joint_z("head_end") - repaired_toe_z
    repaired_head_ratio = (repaired_joint_z("head_end") - repaired_joint_z("neck")) / repaired_height
    repaired_leg_ratio = (repaired_joint_z("Hips") - repaired_toe_z) / repaired_height
    left_hip = armature.matrix_world @ armature.data.bones["LeftUpLeg"].head_local
    right_hip = armature.matrix_world @ armature.data.bones["RightUpLeg"].head_local
    repaired_hip_ratio = abs(left_hip.x - right_hip.x) / repaired_height
    left_shoulder = armature.matrix_world @ armature.data.bones["LeftShoulder"].head_local
    right_shoulder = armature.matrix_world @ armature.data.bones["RightShoulder"].head_local
    repaired_shoulder_ratio = abs(left_shoulder.x - right_shoulder.x) / repaired_height

    receipt = {
        "contract": "FC-OLDER-SISTER-V3-HIGGSFIELD-SD-REPAIR-UNITY-PREP-V1",
        "status": "LOCAL_REPAIR_VISUAL_REVIEW_REQUIRED",
        "sourceGlb": str(source_glb),
        "sourceGlbSha256": sha256(source_glb),
        "newProviderCreditCharge": 0,
        "repairPolicy": "continuous bind mesh and bind-joint proportion map applied to the same paid Higgsfield/Meshy package; original skin, UV and action 613 retained; no donor",
        "targetRatios": {
            "headToHeight": TARGET_HEAD_TO_HEIGHT,
            "legToHeight": TARGET_LEG_TO_HEIGHT,
            "hipXScale": TARGET_HIP_X_SCALE,
            "shoulderXScale": TARGET_SHOULDER_X_SCALE,
            "headXScale": TARGET_HEAD_X_SCALE,
        },
        "measuredRatios": {
            "headToHeight": repaired_head_ratio,
            "legToHeight": repaired_leg_ratio,
            "hipWidthToHeight": repaired_hip_ratio,
            "shoulderWidthToHeight": repaired_shoulder_ratio,
        },
        "zAnchors": [[float(a), float(b)] for a, b in z_anchors],
        "outputFbx": str(output_fbx),
        "outputFbxSha256": sha256(output_fbx),
        "outputTexture": str(output_texture),
        "outputTextureSha256BeforePaletteRepair": sha256(output_texture),
        "uvMaskNpz": str(uv_mask_npz),
        "meshVertexCount": len(mesh.data.vertices),
        "meshPolygonCount": len(mesh.data.polygons),
        "boneCount": len(bone_names),
        "action": action.name,
        "actionFrameStart": float(action.frame_range[0]),
        "actionFrameEnd": float(action.frame_range[1]),
        "animationFps": 30,
        "removedAuxiliaryObjects": removed_auxiliary,
        "sourceBoundsMin": list(source_min),
        "sourceBoundsMax": list(source_max),
        "repairedBoundsBeforeNormalizationMin": list(before_normalization_min),
        "repairedBoundsBeforeNormalizationMax": list(before_normalization_max),
        "outputBoundsMin": list(output_min),
        "outputBoundsMax": list(output_max),
        "productionEligible": False,
    }
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print("OLDER_SISTER_V3_HIGGSFIELD_SD_REPAIR_UNITY_PREP=PASS")
    print(json.dumps(receipt["measuredRatios"], indent=2))
    print("RECEIPT=" + str(receipt_path))


if __name__ == "__main__":
    main()
