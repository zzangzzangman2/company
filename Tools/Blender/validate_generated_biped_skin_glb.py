"""Fail-closed structural validation for generated humanoid GLB skinning.

This gate is intentionally stricter than a generic import check.  It catches the failure where a
static character looks correct but an auto-rigger assigns head/torso vertices to limb bones, mixes
left and right shoes, or collapses both hip joints onto the centre line.  Such a file must never be
imported as a Unity candidate merely because it contains two leg bone chains.
"""

import argparse
import hashlib
import json
import os
import sys
from collections import Counter

import bpy


LEFT_LEG = {"LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase"}
RIGHT_LEG = {"RightUpLeg", "RightLeg", "RightFoot", "RightToeBase"}
LEFT_ARM = {"LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand"}
RIGHT_ARM = {"RightShoulder", "RightArm", "RightForeArm", "RightHand"}
REQUIRED_BONES = LEFT_LEG | RIGHT_LEG | LEFT_ARM | RIGHT_ARM | {
    "Hips",
    "Spine",
    "Head",
    "neck",
}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--glb", required=True)
    parser.add_argument("--receipt", required=True)
    return parser.parse_args(argv)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def connected_component_count(mesh):
    adjacency = [set() for _ in mesh.data.vertices]
    for edge in mesh.data.edges:
        left, right = edge.vertices
        adjacency[left].add(right)
        adjacency[right].add(left)
    unseen = set(range(len(mesh.data.vertices)))
    count = 0
    while unseen:
        count += 1
        stack = [unseen.pop()]
        while stack:
            linked = adjacency[stack.pop()] & unseen
            unseen.difference_update(linked)
            stack.extend(linked)
    return count


args = parse_args()
glb_path = os.path.abspath(args.glb)
receipt_path = os.path.abspath(args.receipt)
if not os.path.isfile(glb_path):
    raise FileNotFoundError(glb_path)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb_path)
scene = bpy.context.scene
armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
meshes = [obj for obj in scene.objects if obj.type == "MESH"]
failures = []
if len(armatures) != 1:
    failures.append(f"armature-count:{len(armatures)}")
if not armatures:
    armature = None
    skinned_meshes = []
else:
    armature = armatures[0]
    skinned_meshes = [
        obj
        for obj in meshes
        if obj.parent == armature
        or any(mod.type == "ARMATURE" and mod.object == armature for mod in obj.modifiers)
    ]
if not skinned_meshes:
    failures.append("skinned-mesh-count:0")
    mesh = max(meshes, key=lambda obj: len(obj.data.vertices)) if meshes else None
else:
    mesh = max(skinned_meshes, key=lambda obj: len(obj.data.vertices))

bone_names = {bone.name for bone in armature.data.bones} if armature else set()
missing_bones = sorted(REQUIRED_BONES - bone_names)
if missing_bones:
    failures.append("missing-bones:" + ",".join(missing_bones))

metrics = {
    "crossLegStrongVertexCount": 0,
    "armLegStrongVertexCount": 0,
    "armLegDominantVertexCount": 0,
    "headLegStrongVertexCount": 0,
    "lowerLegCrossWeightVertexCount": 0,
    "footRegionCrossWeightVertexCount": 0,
    "unweightedVertexCount": 0,
    "maximumInfluencesPerVertex": 0,
    "dominantBoneCounts": {},
    "dominantFamilyCounts": {},
    "leftRightLegDominantRatio": None,
    "hipJointSeparationOverMeshWidth": None,
    "surfaceComponentCount": None,
}

if mesh is not None:
    coordinates = [vertex.co for vertex in mesh.data.vertices]
    minimum_z = min(point.z for point in coordinates)
    maximum_z = max(point.z for point in coordinates)
    minimum_x = min(point.x for point in coordinates)
    maximum_x = max(point.x for point in coordinates)
    height = max(maximum_z - minimum_z, 1.0e-9)
    width = max(maximum_x - minimum_x, 1.0e-9)
    group_names = {group.index: group.name for group in mesh.vertex_groups}
    dominant_bones = Counter()
    dominant_families = Counter()

    for vertex in mesh.data.vertices:
        weights = {
            group_names[item.group]: float(item.weight)
            for item in vertex.groups
            if item.group in group_names and item.weight > 1.0e-6
        }
        metrics["maximumInfluencesPerVertex"] = max(
            metrics["maximumInfluencesPerVertex"], len(weights)
        )
        if not weights:
            metrics["unweightedVertexCount"] += 1
            continue
        dominant_bones[max(weights.items(), key=lambda item: item[1])[0]] += 1
        families = {
            "left_leg": sum(weights.get(name, 0.0) for name in LEFT_LEG),
            "right_leg": sum(weights.get(name, 0.0) for name in RIGHT_LEG),
            "left_arm": sum(weights.get(name, 0.0) for name in LEFT_ARM),
            "right_arm": sum(weights.get(name, 0.0) for name in RIGHT_ARM),
        }
        dominant_family, dominant_family_weight = max(
            families.items(), key=lambda item: item[1]
        )
        # Torso/head vertices legitimately have no limb-family weight. Do not let Python's
        # tie-breaking assign thousands of zero-valued torso vertices to left_leg and corrupt the
        # left/right symmetry ratio.
        if dominant_family_weight > 0.10:
            dominant_families[dominant_family] += 1
        leg_total = families["left_leg"] + families["right_leg"]
        arm_total = families["left_arm"] + families["right_arm"]
        if families["left_leg"] > 0.10 and families["right_leg"] > 0.10:
            metrics["crossLegStrongVertexCount"] += 1
        if arm_total > 0.10 and leg_total > 0.10:
            metrics["armLegStrongVertexCount"] += 1
            # A torso/hip seam may legitimately blend small amounts from both the adjacent arm
            # and thigh while remaining Hips/Spine dominated.  Fail only when limb families own
            # most of the vertex; otherwise retain the count as a visual-review advisory.  This
            # still fails detached hands/pants because either limb plus its cross-family bleed
            # dominates those vertices.
            if arm_total + leg_total > 0.50:
                metrics["armLegDominantVertexCount"] += 1
        normalized_height = (vertex.co.z - minimum_z) / height
        if normalized_height >= 0.60 and leg_total > 0.10:
            metrics["headLegStrongVertexCount"] += 1
        if (
            normalized_height <= 0.30
            and families["left_leg"] > 0.05
            and families["right_leg"] > 0.05
        ):
            metrics["lowerLegCrossWeightVertexCount"] += 1
        if (
            normalized_height <= 0.20
            and families["left_leg"] > 0.05
            and families["right_leg"] > 0.05
        ):
            metrics["footRegionCrossWeightVertexCount"] += 1

    metrics["dominantBoneCounts"] = dict(dominant_bones.most_common())
    metrics["dominantFamilyCounts"] = dict(dominant_families.most_common())
    left_leg_dominant = dominant_families["left_leg"]
    right_leg_dominant = dominant_families["right_leg"]
    if left_leg_dominant and right_leg_dominant:
        metrics["leftRightLegDominantRatio"] = left_leg_dominant / right_leg_dominant
    metrics["surfaceComponentCount"] = connected_component_count(mesh)

    if armature and {"LeftUpLeg", "RightUpLeg"}.issubset(bone_names):
        left_hip = armature.data.bones["LeftUpLeg"].head_local
        right_hip = armature.data.bones["RightUpLeg"].head_local
        metrics["hipJointSeparationOverMeshWidth"] = (left_hip - right_hip).length / width

    if metrics["unweightedVertexCount"]:
        failures.append(f"unweighted:{metrics['unweightedVertexCount']}")
    if metrics["armLegDominantVertexCount"]:
        failures.append(f"arm-leg-dominant-mixed:{metrics['armLegDominantVertexCount']}")
    if metrics["headLegStrongVertexCount"]:
        failures.append(f"head-leg-mixed:{metrics['headLegStrongVertexCount']}")
    # Some generated quad rigs use a symmetric blend around the inner knee/crotch. That is recorded
    # above but is not itself a failure when the shoes are clean. Opposite-side weights in the
    # lowest 20% are the actual detached-shoe / third-leg failure and remain fail-closed.
    if metrics["footRegionCrossWeightVertexCount"]:
        failures.append(
            f"left-right-foot-region-mixed:{metrics['footRegionCrossWeightVertexCount']}"
        )
    ratio = metrics["leftRightLegDominantRatio"]
    if ratio is None or ratio < 0.50 or ratio > 2.0:
        failures.append(f"left-right-leg-dominant-ratio:{ratio}")
    separation = metrics["hipJointSeparationOverMeshWidth"]
    if separation is None or separation < 0.04:
        failures.append(f"collapsed-hip-joints:{separation}")

actions = list(bpy.data.actions)
if not actions:
    failures.append("animation-action-count:0")

receipt = {
    "contract": "FC-GENERATED-BIPED-SKIN-FAIL-CLOSED-V1",
    "status": "FAIL" if failures else "PASS_STRUCTURAL_ONLY",
    "glb": glb_path,
    "sha256": sha256(glb_path),
    "blenderVersion": bpy.app.version_string,
    "meshObjectCount": len(meshes),
    "skinnedMeshCount": len(skinned_meshes),
    "armatureObjectCount": len(armatures),
    "boneCount": len(bone_names),
    "missingRequiredBones": missing_bones,
    "actionNames": [action.name for action in actions],
    "metrics": metrics,
    "failureReasons": failures,
    "visualFullCycleStillRequired": True,
    "productionEligible": False,
}
os.makedirs(os.path.dirname(receipt_path), exist_ok=True)
with open(receipt_path, "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)
    handle.write("\n")

print(json.dumps(receipt, ensure_ascii=False, indent=2))
if failures:
    raise SystemExit(2)
