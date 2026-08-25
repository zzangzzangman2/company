"""Rig the user-approved Father v14 static proof and export an isolated Unity FBX.

The owned Yuuka face/hand weights remain byte-for-byte unchanged before mesh assembly.
New shirt, trousers, shoes and arms receive nearest-surface interpolated weights from the
owned body; rigid head accessories keep their existing bone-parent contract as 1.0 weights.
The export remains an Experimental/private QA candidate and is not a production promotion.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--receipt", required=True)
    parser.add_argument("--blend-output", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = Path(ARGS.output).resolve()
RECEIPT = Path(ARGS.receipt).resolve()
BLEND_OUTPUT = Path(ARGS.blend_output).resolve()
for path in (OUTPUT, RECEIPT, BLEND_OUTPUT):
    path.parent.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
SOURCE_BLEND = Path(bpy.data.filepath).resolve()
armature = bpy.data.objects.get("Armature")
owned_body = bpy.data.objects.get("Yuuka_Original_Body")
if armature is None or owned_body is None:
    raise RuntimeError("Approved Father blend must contain Armature and Yuuka_Original_Body")
if armature.type != "ARMATURE" or len(armature.data.bones) != 118:
    raise RuntimeError("Approved Father rig must retain the exact 118-bone armature")

REQUIRED_BONES = {
    "Bip001 Pelvis",
    "Bip001 Spine",
    "Bip001 Spine1",
    "Bip001 Neck",
    "Bip001 Head",
    "Bip001 L Clavicle",
    "Bip001 L UpperArm",
    "Bip001 L Forearm",
    "Bip001 L Hand",
    "Bip001 R Clavicle",
    "Bip001 R UpperArm",
    "Bip001 R Forearm",
    "Bip001 R Hand",
    "Bip001 L Thigh",
    "Bip001 L Calf",
    "Bip001 L Foot",
    "Bip001 L Toe0",
    "Bip001 R Thigh",
    "Bip001 R Calf",
    "Bip001 R Foot",
    "Bip001 R Toe0",
}
missing_bones = sorted(REQUIRED_BONES - {bone.name for bone in armature.data.bones})
if missing_bones:
    raise RuntimeError(f"Approved Father rig is missing required bones: {missing_bones}")


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def weight_hash(obj):
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        digest.update(f"{vertex.index}:".encode())
        for membership in sorted(vertex.groups, key=lambda item: item.group):
            digest.update(f"{membership.group}:{membership.weight:.9f},".encode())
        digest.update(b";")
    return digest.hexdigest()


def coordinate_hash(obj):
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        point = vertex.co
        digest.update(f"{vertex.index}:{point.x:.9f},{point.y:.9f},{point.z:.9f};".encode())
    return digest.hexdigest()


owned_coordinate_before = coordinate_hash(owned_body)
owned_weight_before = weight_hash(owned_body)


def make_active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def unparent_keep_world(obj):
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = world


def ensure_armature_modifier(obj):
    modifiers = [modifier for modifier in obj.modifiers if modifier.type == "ARMATURE"]
    for modifier in modifiers[1:]:
        obj.modifiers.remove(modifier)
    modifier = modifiers[0] if modifiers else obj.modifiers.new("FatherApprovedV14Armature", "ARMATURE")
    modifier.object = armature
    modifier.use_vertex_groups = True
    return modifier


def clear_vertex_groups(obj):
    while obj.vertex_groups:
        obj.vertex_groups.remove(obj.vertex_groups[-1])


def rigid_weight(obj, bone_name):
    unparent_keep_world(obj)
    clear_vertex_groups(obj)
    group = obj.vertex_groups.new(name=bone_name)
    if obj.data.vertices:
        group.add(range(len(obj.data.vertices)), 1.0, "REPLACE")
    ensure_armature_modifier(obj)


def transfer_weights(source, target):
    unparent_keep_world(target)
    clear_vertex_groups(target)
    for source_group in source.vertex_groups:
        target.vertex_groups.new(name=source_group.name)
    modifier = target.modifiers.new("FatherApprovedV14WeightTransfer", "DATA_TRANSFER")
    modifier.object = source
    modifier.use_vert_data = True
    modifier.data_types_verts = {"VGROUP_WEIGHTS"}
    modifier.vert_mapping = "POLYINTERP_NEAREST"
    modifier.layers_vgroup_select_src = "ALL"
    modifier.layers_vgroup_select_dst = "NAME"
    modifier.mix_mode = "REPLACE"
    modifier.mix_factor = 1.0
    make_active(target)
    bpy.ops.object.modifier_move_to_index(modifier=modifier.name, index=0)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    ensure_armature_modifier(target)
    if any(not vertex.groups for vertex in target.data.vertices):
        raise RuntimeError(f"Weight transfer left unweighted vertices on {target.name}")


def sanitize_trouser_weights(target):
    """Keep trousers on the pelvis/leg chain even when an A-pose hand is the nearest surface."""
    allowed = {
        "Bip001 Pelvis",
        "Bip001 Spine",
        "Bip001 L Thigh",
        "Bip001 L Calf",
        "Bip001 R Thigh",
        "Bip001 R Calf",
    }
    forbidden_tokens = ("UpperArm", "Forearm", "Hand", "Finger", "Clavicle")
    groups_by_index = {group.index: group for group in target.vertex_groups}
    left_thigh_x = armature.data.bones["Bip001 L Thigh"].head_local.x
    left_is_positive = left_thigh_x >= 0.0
    removed_memberships = 0
    fallback_vertices = 0

    for vertex in target.data.vertices:
        retained = []
        for membership in tuple(vertex.groups):
            group = groups_by_index[membership.group]
            if group.name in allowed:
                retained.append((group, membership.weight))
            else:
                if any(token in group.name for token in forbidden_tokens):
                    removed_memberships += 1
                group.remove([vertex.index])

        total = sum(weight for _, weight in retained)
        if total > 1.0e-8:
            for group, weight in retained:
                group.add([vertex.index], weight / total, "REPLACE")
            continue

        fallback_vertices += 1
        point = target.matrix_world @ vertex.co
        is_left = (point.x >= 0.0) == left_is_positive
        if point.z >= 0.40:
            fallback_name = "Bip001 Pelvis"
        elif point.z >= 0.20:
            fallback_name = "Bip001 L Thigh" if is_left else "Bip001 R Thigh"
        else:
            fallback_name = "Bip001 L Calf" if is_left else "Bip001 R Calf"
        target.vertex_groups[fallback_name].add([vertex.index], 1.0, "REPLACE")

    forbidden_after = 0
    for vertex in target.data.vertices:
        for membership in vertex.groups:
            name = groups_by_index[membership.group].name
            if any(token in name for token in forbidden_tokens) and membership.weight > 1.0e-7:
                forbidden_after += 1
    if forbidden_after:
        raise RuntimeError(
            f"Trouser weight sanitation left {forbidden_after} arm/hand memberships")
    return {
        "removedArmHandMemberships": removed_memberships,
        "fallbackVertices": fallback_vertices,
        "forbiddenArmHandMembershipsAfter": forbidden_after,
        "allowedBones": sorted(allowed),
    }


def sanitize_shoe_weights(target):
    """Keep each approved oxford rigidly on its anatomical foot, never on nearby A-pose hands."""
    left_foot_name = "Bip001 L Foot"
    right_foot_name = "Bip001 R Foot"
    left_foot_x = armature.data.bones[left_foot_name].head_local.x
    left_is_positive = left_foot_x >= 0.0
    groups_by_index = {group.index: group for group in target.vertex_groups}
    removed_memberships = 0

    for vertex in target.data.vertices:
        for membership in tuple(vertex.groups):
            groups_by_index[membership.group].remove([vertex.index])
            removed_memberships += 1
        point = target.matrix_world @ vertex.co
        is_left = (point.x >= 0.0) == left_is_positive
        foot_name = left_foot_name if is_left else right_foot_name
        target.vertex_groups[foot_name].add([vertex.index], 1.0, "REPLACE")

    unexpected_memberships = 0
    for vertex in target.data.vertices:
        memberships = [
            groups_by_index[item.group].name
            for item in vertex.groups
            if item.weight > 1.0e-7
        ]
        if len(memberships) != 1 or memberships[0] not in {left_foot_name, right_foot_name}:
            unexpected_memberships += 1
    if unexpected_memberships:
        raise RuntimeError(
            f"Shoe weight sanitation left {unexpected_memberships} non-rigid vertices")
    return {
        "removedTransferredMemberships": removed_memberships,
        "unexpectedMembershipsAfter": unexpected_memberships,
        "allowedBones": [left_foot_name, right_foot_name],
        "staticGeometryChanged": False,
    }


def convert_visible_curves():
    converted = []
    for obj in tuple(scene.objects):
        if obj.hide_render or obj.type != "CURVE":
            continue
        parent_bone = obj.parent_bone if obj.parent_type == "BONE" else None
        make_active(obj)
        bpy.ops.object.convert(target="MESH")
        mesh_obj = bpy.context.object
        if parent_bone:
            rigid_weight(mesh_obj, parent_bone)
        converted.append(mesh_obj.name)
    return converted


converted_curves = convert_visible_curves()

rigid_bones = {
    "FatherSurface15CleanRearCranium": "Bip001 Head",
    "FatherNeck": "Bip001 Neck",
    "FatherWatchBand": "Bip001 L Forearm",
    "FatherWatchFace": "Bip001 L Forearm",
    "NormalizedMouthCurve": "Bip001 Head",
}
for obj in tuple(scene.objects):
    if obj.hide_render or obj.type != "MESH" or obj == owned_body:
        continue
    if obj.parent_type == "BONE" and obj.parent_bone:
        rigid_bones[obj.name] = obj.parent_bone

for name, bone_name in sorted(rigid_bones.items()):
    obj = bpy.data.objects.get(name)
    if obj is not None and not obj.hide_render and obj.type == "MESH":
        rigid_weight(obj, bone_name)

deforming_names = (
    "FatherSurface11CleanArmL",
    "FatherSurface11CleanArmR",
    "FatherSurface7AuthoredBrownShoes",
    "FatherSurface7AuthoredMaleTrousers",
    "FatherSurface7OnePieceBodyShoulderSleeveShirt",
)
for name in deforming_names:
    target = bpy.data.objects.get(name)
    if target is None:
        raise RuntimeError(f"Approved Father deforming surface missing: {name}")
    transfer_weights(owned_body, target)

shoe_weight_sanitation = sanitize_shoe_weights(
    bpy.data.objects["FatherSurface7AuthoredBrownShoes"]
)
trouser_weight_sanitation = sanitize_trouser_weights(
    bpy.data.objects["FatherSurface7AuthoredMaleTrousers"]
)

shirt = bpy.data.objects["FatherSurface7OnePieceBodyShoulderSleeveShirt"]
for name in (
    "FatherSurface7FittedButton1",
    "FatherSurface7FittedButton2",
    "FatherSurface7FittedButton3",
    "FatherSurface7FittedButton4",
    "FatherSurface7FittedCollarSeam",
    "FatherSurface7FittedPlacketSeam",
    "FatherSurface7FittedPocketStitch",
):
    target = bpy.data.objects.get(name)
    if target is None:
        raise RuntimeError(f"Approved Father fitted detail missing: {name}")
    transfer_weights(shirt, target)

if owned_coordinate_before != coordinate_hash(owned_body):
    raise RuntimeError("Rig transfer changed approved owned-body coordinates")
if owned_weight_before != weight_hash(owned_body):
    raise RuntimeError("Rig transfer changed approved owned-body weights")

# Remove empty provenance meshes and join every visible mesh into one skinned body.
for obj in tuple(scene.objects):
    if obj.type == "MESH" and len(obj.data.vertices) == 0:
        bpy.data.objects.remove(obj, do_unlink=True)

unparent_keep_world(owned_body)
visible_meshes = [
    obj for obj in scene.objects
    if obj.type == "MESH" and not obj.hide_render
]
for obj in visible_meshes:
    ensure_armature_modifier(obj)

bpy.ops.object.select_all(action="DESELECT")
for obj in visible_meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = owned_body
bpy.ops.object.join()
body = bpy.context.object
body.name = "FatherApprovedV14_Body"
body.data.name = "FatherApprovedV14_BodyMesh"
ensure_armature_modifier(body)
body.parent = armature
body.matrix_parent_inverse = armature.matrix_world.inverted()

weighted_vertices = sum(1 for vertex in body.data.vertices if vertex.groups)
max_influences = max((len(vertex.groups) for vertex in body.data.vertices), default=0)
if weighted_vertices != len(body.data.vertices):
    raise RuntimeError(
        f"Approved Father combined body has unweighted vertices: {weighted_vertices}/{len(body.data.vertices)}"
    )
if max_influences > 4:
    raise RuntimeError(f"Approved Father combined body exceeds four influences: {max_influences}")

armature.name = "FatherApprovedV14_Armature"
armature.data.name = "FatherApprovedV14_ArmatureData"
armature["familyCompanyHumanoid"] = True
armature["familyCompanyCandidate"] = "FatherApprovedV14"
body["userVisualApproval"] = "USER_VISUAL_APPROVED_STATIC"
body["productionEligible"] = False
body["test3SakurakoExcluded"] = True

# The approved source blend contains valid image datablocks, but their historical relative
# paths point at the old proof location. Resolve the three retained Yuuka texture images to
# the private project evidence folder before saving/exporting so Unity receives real pixels.
project_root = next(
    (
        parent
        for parent in OUTPUT.parents
        if (parent / "Assets" / "FamilyCompany").is_dir()
    ),
    None,
)
if project_root is None:
    raise RuntimeError("Could not resolve Family Company project root from output FBX path")
texture_root = (
    project_root
    / "Artifacts"
    / "ExternalReferenceStudy"
    / "UserProvided_BlueArchive_OriginalMeshes_2026-08-24"
    / "Yuuka"
    / "Yuuka_Original_Mesh"
)
required_texture_files = {
    "Yuuka_Original_Face.png",
    "Yuuka_Original_Hair.png",
    "Yuuka_Original_EyeMouth.png",
}
resolved_texture_files = []
for image in bpy.data.images:
    filename = Path(image.filepath).name
    if filename not in required_texture_files:
        continue
    resolved = texture_root / filename
    if not resolved.is_file():
        raise RuntimeError(f"Required approved texture is missing: {resolved}")
    image.filepath = str(resolved)
    image.filepath_raw = str(resolved)
    image.reload()
    resolved_texture_files.append(str(resolved))
if {Path(path).name for path in resolved_texture_files} != required_texture_files:
    raise RuntimeError(
        "Approved blend did not expose all required Yuuka image datablocks: "
        f"{sorted(Path(path).name for path in resolved_texture_files)}"
    )

bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUTPUT), check_existing=False)

bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.export_scene.fbx(
    filepath=str(OUTPUT),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=False,
    bake_anim=False,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    use_space_transform=True,
    path_mode="COPY",
    embed_textures=True,
)

receipt = {
    "schema": "family-company.father-approved-v14-rig-transfer.v1",
    "status": "RIG_TRANSFER_BLENDER_PASS_UNITY_HUMANOID_PENDING",
    "productionEligible": False,
    "userVisualApproval": "USER_VISUAL_APPROVED_STATIC",
    "sourceBlend": str(SOURCE_BLEND),
    "sourceBlendSha256": sha256(SOURCE_BLEND),
    "outputFbx": str(OUTPUT),
    "outputFbxSha256": sha256(OUTPUT),
    "rigBlend": str(BLEND_OUTPUT),
    "rigBlendSha256": sha256(BLEND_OUTPUT),
    "armature": armature.name,
    "boneCount": len(armature.data.bones),
    "requiredBonesPresent": not missing_bones,
    "combinedBody": body.name,
    "meshCount": 1,
    "vertices": len(body.data.vertices),
    "polygons": len(body.data.polygons),
    "materials": [material.name if material else None for material in body.data.materials],
    "weightedVertices": weighted_vertices,
    "maxInfluences": max_influences,
    "ownedBodyCoordinateHashBeforeAssembly": owned_coordinate_before,
    "ownedBodyWeightHashBeforeAssembly": owned_weight_before,
    "ownedBodyUnchangedBeforeAssembly": True,
    "convertedCurves": converted_curves,
    "deformingWeightMethod": "nearest face interpolated from approved owned Yuuka body; limit 4 and normalize",
    "trouserWeightSanitation": trouser_weight_sanitation,
    "shoeWeightSanitation": shoe_weight_sanitation,
    "rigidWeightMethod": "existing bone-parent contract converted to one 1.0 vertex group",
    "resolvedTextureFiles": resolved_texture_files,
    "test3SakurakoExcluded": True,
    "nextGate": "Unity Humanoid import, shared walk deformation, actual StarterOffice D3D11 QA",
}
RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8")
print(RECEIPT)
