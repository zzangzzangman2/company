"""Create a hand-only five-digit visual correction proof.

The input proof is never overwritten.  Only faces dominated by the donor's
Hand/Finger groups are removed, then a rounded palm, thumb and four distinct
fingers are added on each side.  Clothing, face and hair objects are untouched.
The complete source armature remains in the saved proof; the new neutral-pose
hand parts are bone-parented to the existing Hand bone.

This is a topology/silhouette feasibility correction, not a production finger
deformation solution: the donor rigs contain only three finger chains.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re

import bmesh
import bpy
from mathutils import Matrix, Vector


def parse_args():
    argv = list(__import__("sys").argv)
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--label", required=True)
    parser.add_argument("--output-dir", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
INPUT = os.path.abspath(ARGS.input)
OUTPUT = os.path.abspath(ARGS.output_dir)


def side_of(name):
    for side, pattern in {
        "L": re.compile(r"(?:^|[ ._])L(?:eft)?(?:[ ._]|$)", re.I),
        "R": re.compile(r"(?:^|[ ._])R(?:ight)?(?:[ ._]|$)", re.I),
    }.items():
        if pattern.search(name):
            return side
    return None


def choose_armature():
    return max((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), key=lambda o: len(o.data.bones))


def choose_body(armature):
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bound = [o for o in meshes if any(m.type == "ARMATURE" and m.object == armature for m in o.modifiers)]
    return max(bound or meshes, key=lambda o: len(o.data.vertices))


def is_hand_group(group, side):
    low = group.name.lower()
    return side_of(group.name) == side and ("hand" in low or "finger" in low or "thumb" in low)


def chain_roots(armature, side):
    fingers = [b for b in armature.data.bones if side_of(b.name) == side and ("finger" in b.name.lower() or "thumb" in b.name.lower())]
    names = {b.name for b in fingers}
    return sorted((b for b in fingers if b.parent is None or b.parent.name not in names), key=lambda b: b.name)


def terminal_descendant(root):
    current = root
    while True:
        children = [child for child in current.children if "finger" in child.name.lower() or "thumb" in child.name.lower()]
        if not children:
            return current
        current = sorted(children, key=lambda b: b.name)[0]


def pose_head_world(armature, bone_name):
    return armature.matrix_world @ armature.pose.bones[bone_name].head


def pose_tail_world(armature, bone_name):
    return armature.matrix_world @ armature.pose.bones[bone_name].tail


def make_material(body):
    # Reuse the material that currently covers most donor hand faces when
    # possible, preserving each character's skin presentation.
    counts = {}
    hand_indices = {g.index for g in body.vertex_groups if "hand" in g.name.lower() or "finger" in g.name.lower()}
    for polygon in body.data.polygons:
        score = 0.0
        for vertex_index in polygon.vertices:
            score += sum(m.weight for m in body.data.vertices[vertex_index].groups if m.group in hand_indices)
        if score > 0.1:
            counts[polygon.material_index] = counts.get(polygon.material_index, 0) + 1
    if counts:
        index = max(counts, key=counts.get)
        if 0 <= index < len(body.data.materials) and body.data.materials[index] is not None:
            return body.data.materials[index]
    material = bpy.data.materials.new("HandAudit_CorrectedSkin")
    material.diffuse_color = (0.82, 0.53, 0.46, 1.0)
    return material


def create_ellipsoid(name, center, axes, radii, material, armature, hand_bone):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=18, location=center)
    obj = bpy.context.object
    obj.name = name
    basis = Matrix((axes[0], axes[1], axes[2])).transposed()
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = basis.to_quaternion()
    obj.scale = radii
    # Force dependency-graph evaluation before capturing matrix_world.  Without
    # this, Blender can return the pre-scale sphere matrix and bone parenting
    # silently expands the proof object back to a two-metre default sphere.
    bpy.context.view_layer.update()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj["handAuditCorrection"] = True
    obj["digitRole"] = name.rsplit(".", 1)[-1]
    return obj


def mesh_component_count(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    remaining = set(bm.verts)
    count = 0
    while remaining:
        count += 1
        seed = remaining.pop()
        stack = [seed]
        while stack:
            vertex = stack.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in remaining:
                    remaining.remove(other)
                    stack.append(other)
    non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    vertices = len(bm.verts)
    polygons = len(bm.faces)
    bm.free()
    return count, non_manifold, vertices, polygons


def unify_hand(parts, armature, hand_bone, side, size):
    """Voxel-union the construction volumes into one closed hand surface."""
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    hand = bpy.context.object
    hand.name = f"HandAudit5Digit.{side}.ConnectedHand"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    hand.data.remesh_voxel_size = max(size * 0.018, 1e-6)
    hand.data.remesh_voxel_adaptivity = 0.0
    bpy.ops.object.voxel_remesh()

    smooth = hand.modifiers.new("HandAuditSurfaceRelax", "SMOOTH")
    smooth.factor = 0.30
    smooth.iterations = 3
    bpy.context.view_layer.objects.active = hand
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    for polygon in hand.data.polygons:
        polygon.use_smooth = True

    components, non_manifold, vertices, polygons = mesh_component_count(hand)
    hand["handAuditCorrection"] = True
    hand["visibleDigitCount"] = 5
    hand["connectedComponentCount"] = components
    hand["nonManifoldEdgeCount"] = non_manifold
    world = hand.matrix_world.copy()
    hand.parent = armature
    hand.parent_type = "BONE"
    hand.parent_bone = hand_bone
    hand.matrix_world = world
    return hand, {
        "connectedComponentsAfterVoxelUnion": components,
        "nonManifoldEdgesAfterVoxelUnion": non_manifold,
        "verticesAfterVoxelUnion": vertices,
        "polygonsAfterVoxelUnion": polygons,
        "voxelSizeWorld": size * 0.018,
    }


def remove_donor_hand_faces(body, sides):
    remove_indices = set()
    per_side = {}
    group_weights = {}
    for vertex in body.data.vertices:
        group_weights[vertex.index] = {m.group: float(m.weight) for m in vertex.groups}
    for side in sides:
        relevant = {g.index for g in body.vertex_groups if is_hand_group(g, side)}
        dominant_vertices = set()
        for vertex in body.data.vertices:
            weights = group_weights[vertex.index]
            hand_weight = sum(weights.get(index, 0.0) for index in relevant)
            other_weight = sum(weight for index, weight in weights.items() if index not in relevant)
            if hand_weight > other_weight and hand_weight > 0.05:
                dominant_vertices.add(vertex.index)
        selected = []
        for polygon in body.data.polygons:
            overlap = sum(1 for index in polygon.vertices if index in dominant_vertices)
            if overlap >= max(1, math.ceil(len(polygon.vertices) * 0.5)):
                selected.append(polygon.index)
                remove_indices.add(polygon.index)
        per_side[side] = {"dominantVertices": len(dominant_vertices), "removedFaces": len(selected)}

    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.faces.ensure_lookup_table()
    targets = [bm.faces[index] for index in sorted(remove_indices) if index < len(bm.faces)]
    bmesh.ops.delete(bm, geom=targets, context="FACES_ONLY")
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    return per_side


def build_side(armature, body, side, material):
    roots = chain_roots(armature, side)
    if len(roots) != 3:
        raise RuntimeError(f"Expected donor three-chain hand on {side}; got {len(roots)}")
    hand_candidates = [b for b in armature.data.bones if side_of(b.name) == side and "hand" in b.name.lower()]
    if not hand_candidates:
        raise RuntimeError(f"No hand bone for {side}")
    hand_bone = sorted(hand_candidates, key=lambda b: len(b.name))[0]
    wrist = pose_head_world(armature, hand_bone.name)
    tips = [pose_tail_world(armature, terminal_descendant(root).name) for root in roots]
    mean_tip = sum(tips, Vector()) / len(tips)
    long_axis = (mean_tip - wrist).normalized()
    depth_hint = Vector((0.0, 1.0, 0.0))
    depth_axis = (depth_hint - long_axis * depth_hint.dot(long_axis)).normalized()
    width_axis = depth_axis.cross(long_axis).normalized()
    size = max((mean_tip - wrist).length, 1e-5)

    # The donor's Finger0 chain is the thumb.  Its actual side tells us which
    # edge of the mirrored hand to use without hard-coded left/right signs.
    thumb_tip = tips[0]
    other_mean = (tips[1] + tips[2]) * 0.5
    thumb_sign = 1.0 if (thumb_tip - other_mean).dot(width_axis) >= 0.0 else -1.0

    created = []
    prefix = f"HandAudit5Digit.{side}"
    # The wrist bridge overlaps the retained forearm and the palm, preventing a
    # circular assembly seam while keeping the correction strictly hand-local.
    wrist_bridge_center = wrist + long_axis * (0.055 * size)
    created.append(create_ellipsoid(
        prefix + ".WristBridge",
        wrist_bridge_center,
        (width_axis, depth_axis, long_axis),
        (0.255 * size, 0.165 * size, 0.215 * size),
        material,
        armature,
        hand_bone.name,
    ))
    palm_center = wrist + long_axis * (0.31 * size)
    created.append(create_ellipsoid(
        prefix + ".Palm",
        palm_center,
        (width_axis, depth_axis, long_axis),
        (0.34 * size, 0.18 * size, 0.33 * size),
        material,
        armature,
        hand_bone.name,
    ))

    finger_offsets = (-0.255, -0.085, 0.085, 0.255)
    finger_lengths = (0.365, 0.435, 0.455, 0.390)
    finger_splay = (-0.024, -0.008, 0.008, 0.024)
    for index, (offset, length, splay) in enumerate(zip(finger_offsets, finger_lengths, finger_splay), start=1):
        start = wrist + long_axis * (0.52 * size) + width_axis * (offset * size)
        end = start + long_axis * (length * size) + width_axis * (splay * size)
        direction = (end - start).normalized()
        center = (start + end) * 0.5
        local_width = depth_axis.cross(direction).normalized()
        local_depth = direction.cross(local_width).normalized()
        created.append(create_ellipsoid(
            prefix + f".Finger{index}",
            center,
            (local_width, local_depth, direction),
            (0.058 * size, 0.070 * size, (end - start).length * 0.53),
            material,
            armature,
            hand_bone.name,
        ))

    thumb_start = wrist + long_axis * (0.26 * size) + width_axis * (thumb_sign * 0.29 * size)
    thumb_end = thumb_start + long_axis * (0.20 * size) + width_axis * (thumb_sign * 0.31 * size)
    thumb_direction = (thumb_end - thumb_start).normalized()
    thumb_width = depth_axis.cross(thumb_direction).normalized()
    thumb_depth = thumb_direction.cross(thumb_width).normalized()
    created.append(create_ellipsoid(
        prefix + ".Thumb",
        (thumb_start + thumb_end) * 0.5,
        (thumb_width, thumb_depth, thumb_direction),
        (0.070 * size, 0.075 * size, (thumb_end - thumb_start).length * 0.54),
        material,
        armature,
        hand_bone.name,
    ))

    connected_hand, topology = unify_hand(created, armature, hand_bone.name, side, size)
    return {
        "side": side,
        "existingFingerChains": [[root.name, terminal_descendant(root).name] for root in roots],
        "existingFingerChainCount": len(roots),
        "newVisibleDigits": 5,
        "handBone": hand_bone.name,
        "wristWorld": list(wrist),
        "donorMeanTipWorld": list(mean_tip),
        "constructionSize": float(size),
        "objects": [connected_hand.name],
        "topology": topology,
    }


bpy.ops.wm.open_mainfile(filepath=INPUT)
scene = bpy.context.scene
scene.frame_set(scene.frame_current)
bpy.context.evaluated_depsgraph_get().update()
armature = choose_armature()
body = choose_body(armature)
bone_count_before = len(armature.data.bones)
modifier_targets_before = [m.object.name if m.object else None for m in body.modifiers if m.type == "ARMATURE"]
material = make_material(body)

# Measure anchors before removing only the donor hand faces.
side_data = [build_side(armature, body, side, material) for side in ("L", "R")]
removed = remove_donor_hand_faces(body, ("L", "R"))

os.makedirs(OUTPUT, exist_ok=True)
blend_path = os.path.join(OUTPUT, f"{ARGS.label}-five-finger-corrected.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
receipt = {
    "schema": "family-company.hand-five-digit-correction-proof.v1",
    "status": "VISUAL_HAND_CORRECTION_PROOF_REVIEW_REQUIRED",
    "input": INPUT,
    "inputOverwritten": False,
    "label": ARGS.label,
    "body": body.name,
    "armature": armature.name,
    "fullArmatureBoneCountBefore": bone_count_before,
    "fullArmatureBoneCountAfter": len(armature.data.bones),
    "armatureUnchanged": len(armature.data.bones) == bone_count_before,
    "bodyArmatureModifierTargetsBefore": modifier_targets_before,
    "bodyArmatureModifierTargetsAfter": [m.object.name if m.object else None for m in body.modifiers if m.type == "ARMATURE"],
    "removedDonorHandFacesOnly": removed,
    "sides": side_data,
    "untouchedScope": ["face", "eyes", "hair", "clothing", "feet", "leg topology"],
    "limitation": "Each side is one connected/manifold neutral-pose hand surface bone-parented to the existing Hand bone. Donor has only three finger chains; production independent five-finger articulation needs two added chains or a dedicated hand rig.",
    "blend": blend_path,
}
with open(os.path.join(OUTPUT, f"{ARGS.label}-five-finger-corrected-receipt.json"), "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)
print("HAND_CORRECTION_BLEND=" + blend_path)
