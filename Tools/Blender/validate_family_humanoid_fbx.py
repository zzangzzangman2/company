"""Fail-closed Blender round-trip validation for a generated family FBX."""

import argparse
import hashlib
import json
import os
import sys

import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--fbx", required=True)
    parser.add_argument("--receipt", required=True)
    return parser.parse_args(argv)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def fail(message):
    print("FAMILY_HUMANOID_FBX_ROUNDTRIP: FAIL | " + message, file=sys.stderr)
    raise SystemExit(2)


ARGS = parse_args()
FBX = os.path.abspath(ARGS.fbx)
RECEIPT = os.path.abspath(ARGS.receipt)
if not os.path.isfile(FBX):
    fail("FBX does not exist: " + FBX)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=FBX)

meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
if len(meshes) != 1:
    fail("Expected exactly one mesh object; found %d" % len(meshes))
if len(armatures) != 1:
    fail("Expected exactly one armature object; found %d" % len(armatures))

mesh_object = meshes[0]
armature = armatures[0]
armature_modifiers = [modifier for modifier in mesh_object.modifiers if modifier.type == "ARMATURE"]
if len(armature_modifiers) != 1 or armature_modifiers[0].object != armature:
    fail("Mesh does not have exactly one modifier targeting the imported armature.")
if len(mesh_object.data.materials) != 1:
    fail("Expected one atlas material; found %d" % len(mesh_object.data.materials))
uv_layers = mesh_object.data.uv_layers
if len(uv_layers) != 1:
    fail("Expected exactly one atlas UV layer; found %d" % len(uv_layers))
active_uv_layer = uv_layers.active
if active_uv_layer is None:
    fail("The single atlas UV layer is not active as UV0.")

bone_profiles = {
    "canonical": {
        "Root",
        "Hips",
        "Spine",
        "Chest",
        "UpperChest",
        "Neck",
        "Head",
        "LeftShoulder",
        "LeftUpperArm",
        "LeftLowerArm",
        "LeftHand",
        "RightShoulder",
        "RightUpperArm",
        "RightLowerArm",
        "RightHand",
        "LeftUpperLeg",
        "LeftLowerLeg",
        "LeftFoot",
        "LeftToes",
        "RightUpperLeg",
        "RightLowerLeg",
        "RightFoot",
        "RightToes",
    },
    "meshy-one-package": {
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
    },
}
bone_names = {bone.name for bone in armature.data.bones}
matching_profiles = [
    name for name, required in bone_profiles.items() if required.issubset(bone_names)
]
if not matching_profiles:
    closest_profile = min(
        bone_profiles,
        key=lambda name: len(bone_profiles[name] - bone_names),
    )
    missing_bones = sorted(bone_profiles[closest_profile] - bone_names)
    fail(
        "Required bones missing after FBX round trip for closest profile "
        + closest_profile
        + ": "
        + ", ".join(missing_bones)
    )
bone_naming_profile = matching_profiles[0]
missing_bones = []

group_names = {group.index: group.name for group in mesh_object.vertex_groups}
unweighted = 0
missing_group_references = 0
maximum_influences = 0
for vertex in mesh_object.data.vertices:
    influences = [assignment for assignment in vertex.groups if assignment.weight > 0.0001]
    maximum_influences = max(maximum_influences, len(influences))
    if not influences:
        unweighted += 1
    for assignment in influences:
        if group_names.get(assignment.group) not in bone_names:
            missing_group_references += 1
if unweighted or missing_group_references:
    fail(
        "Skin weights failed after FBX round trip: unweighted=%d invalidBoneReferences=%d"
        % (unweighted, missing_group_references)
    )

world_bounds = [mesh_object.matrix_world @ Vector(corner) for corner in mesh_object.bound_box]
bounds_min = [min(point[axis] for point in world_bounds) for axis in range(3)]
bounds_max = [max(point[axis] for point in world_bounds) for axis in range(3)]
receipt = {
    "contract": "FC-FAMILY-HUMANOID-FBX-ROUNDTRIP-V1",
    "status": "PASS",
    "fbx": FBX,
    "sha256": sha256(FBX),
    "blenderVersion": bpy.app.version_string,
    "meshObjectCount": len(meshes),
    "armatureObjectCount": len(armatures),
    "armatureModifierCount": len(armature_modifiers),
    "materialCount": len(mesh_object.data.materials),
    "uvLayerCount": len(uv_layers),
    "activeUvLayer": active_uv_layer.name,
    "vertexCount": len(mesh_object.data.vertices),
    "polygonCount": len(mesh_object.data.polygons),
    "boneCount": len(armature.data.bones),
    "boneNamingProfile": bone_naming_profile,
    "missingRequiredBones": missing_bones,
    "unweightedVertexCount": unweighted,
    "invalidBoneWeightReferenceCount": missing_group_references,
    "maximumInfluencesPerVertex": maximum_influences,
    "boundsMin": bounds_min,
    "boundsMax": bounds_max,
    "productionEligible": False,
}
os.makedirs(os.path.dirname(RECEIPT), exist_ok=True)
with open(RECEIPT, "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print("FAMILY_HUMANOID_FBX_ROUNDTRIP: PASS")
print(json.dumps(receipt, ensure_ascii=False, indent=2))
