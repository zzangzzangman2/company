"""Locate cross-family skin weights in a generated humanoid GLB.

This diagnostic is intentionally read-only.  It complements
``validate_generated_biped_skin_glb.py`` by recording the exact vertices and
weight families behind a fail-closed arm/leg overlap so visual QA can decide
whether the provider attached a hand, garment, or other surface to a leg.
"""

import argparse
import json
import os
import sys

import bpy


LEFT_LEG = {"LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase"}
RIGHT_LEG = {"RightUpLeg", "RightLeg", "RightFoot", "RightToeBase"}
LEFT_ARM = {"LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand"}
RIGHT_ARM = {"RightShoulder", "RightArm", "RightForeArm", "RightHand"}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--glb", required=True)
    parser.add_argument("--receipt", required=True)
    return parser.parse_args(argv)


def family_sum(weights, names):
    return sum(weights.get(name, 0.0) for name in names)


args = parse_args()
glb_path = os.path.abspath(args.glb)
receipt_path = os.path.abspath(args.receipt)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb_path)
armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if len(armatures) != 1:
    raise RuntimeError(f"Expected one armature, found {len(armatures)}")
armature = armatures[0]
skinned = [
    obj
    for obj in meshes
    if obj.parent == armature
    or any(mod.type == "ARMATURE" and mod.object == armature for mod in obj.modifiers)
]
if not skinned:
    raise RuntimeError("No skinned mesh")
mesh = max(skinned, key=lambda obj: len(obj.data.vertices))
group_names = {group.index: group.name for group in mesh.vertex_groups}

coordinates = [vertex.co for vertex in mesh.data.vertices]
minimum = [min(point[axis] for point in coordinates) for axis in range(3)]
maximum = [max(point[axis] for point in coordinates) for axis in range(3)]
extent = [max(maximum[axis] - minimum[axis], 1.0e-9) for axis in range(3)]

flagged = []
flagged_indices = set()
for vertex in mesh.data.vertices:
    weights = {
        group_names[item.group]: float(item.weight)
        for item in vertex.groups
        if item.group in group_names and item.weight > 1.0e-6
    }
    families = {
        "leftLeg": family_sum(weights, LEFT_LEG),
        "rightLeg": family_sum(weights, RIGHT_LEG),
        "leftArm": family_sum(weights, LEFT_ARM),
        "rightArm": family_sum(weights, RIGHT_ARM),
    }
    leg_total = families["leftLeg"] + families["rightLeg"]
    arm_total = families["leftArm"] + families["rightArm"]
    if arm_total <= 0.10 or leg_total <= 0.10:
        continue
    flagged_indices.add(vertex.index)
    world = mesh.matrix_world @ vertex.co
    flagged.append(
        {
            "index": vertex.index,
            "local": [float(value) for value in vertex.co],
            "world": [float(value) for value in world],
            "normalizedLocal": [
                float((vertex.co[axis] - minimum[axis]) / extent[axis])
                for axis in range(3)
            ],
            "families": families,
            "weights": dict(sorted(weights.items(), key=lambda item: -item[1])),
        }
    )

adjacency = {index: set() for index in flagged_indices}
for edge in mesh.data.edges:
    left, right = edge.vertices
    if left in flagged_indices and right in flagged_indices:
        adjacency[left].add(right)
        adjacency[right].add(left)

clusters = []
remaining = set(flagged_indices)
while remaining:
    seed = remaining.pop()
    component = {seed}
    stack = [seed]
    while stack:
        linked = adjacency[stack.pop()] & remaining
        remaining.difference_update(linked)
        component.update(linked)
        stack.extend(linked)
    clusters.append(sorted(component))

receipt = {
    "contract": "FC-GENERATED-BIPED-SKIN-OVERLAP-AUDIT-V1",
    "glb": glb_path,
    "mesh": mesh.name,
    "objects": [
        {
            "name": obj.name,
            "type": obj.type,
            "parent": obj.parent.name if obj.parent is not None else None,
        }
        for obj in bpy.context.scene.objects
    ],
    "meshVertexCount": len(mesh.data.vertices),
    "localBoundsMin": minimum,
    "localBoundsMax": maximum,
    "flaggedVertexCount": len(flagged),
    "clusterCount": len(clusters),
    "clusters": sorted(clusters, key=len, reverse=True),
    "vertices": flagged,
    "productionEligible": False,
}
os.makedirs(os.path.dirname(receipt_path), exist_ok=True)
with open(receipt_path, "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)
    handle.write("\n")

print(json.dumps(receipt, ensure_ascii=False, indent=2))
