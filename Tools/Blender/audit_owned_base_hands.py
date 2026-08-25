"""Audit hand topology/rig/pose in owned-base character proofs.

This script is intentionally read-only with respect to its input.  It opens a
blend or imports an FBX, inspects the largest armature-bound body mesh, and
writes a JSON receipt.  It does not save the loaded scene.

Run with Blender, for example:
  blender --background --python Tools/Blender/audit_owned_base_hands.py -- \
    --input path/to/file.blend --label SisterProof3 --output audit.json
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re
from collections import Counter, defaultdict, deque

import bpy
from mathutils import Matrix, Vector


def parse_args() -> argparse.Namespace:
    argv = list(__import__("sys").argv)
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--label", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--kind", choices=("auto", "blend", "fbx"), default="auto")
    return parser.parse_args(argv)


ARGS = parse_args()
INPUT = os.path.abspath(ARGS.input)
OUTPUT = os.path.abspath(ARGS.output)


def rounded(value, digits=7):
    if isinstance(value, Vector):
        return [round(float(v), digits) for v in value]
    return round(float(value), digits)


def matrix_rows(matrix: Matrix):
    return [[round(float(v), 8) for v in row] for row in matrix]


def bounds(points):
    if not points:
        return None
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    center = (lo + hi) * 0.5
    return {
        "min": rounded(lo),
        "max": rounded(hi),
        "center": rounded(center),
        "dimensions": rounded(hi - lo),
    }


def reset_and_load():
    kind = ARGS.kind
    if kind == "auto":
        kind = "blend" if INPUT.lower().endswith(".blend") else "fbx"
    if kind == "blend":
        bpy.ops.wm.open_mainfile(filepath=INPUT)
    else:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        bpy.ops.import_scene.fbx(filepath=INPUT)
    return kind


def choose_armature():
    armatures = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("No armature found")
    return max(armatures, key=lambda o: len(o.data.bones))


def choose_body(armature):
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bound = []
    for obj in meshes:
        mods = [m for m in obj.modifiers if m.type == "ARMATURE" and m.object == armature]
        if mods:
            bound.append(obj)
    pool = bound or meshes
    if not pool:
        raise RuntimeError("No mesh found")
    return max(pool, key=lambda o: len(o.data.vertices))


SIDE_PATTERNS = {
    "L": re.compile(r"(?:^|[ ._])L(?:eft)?(?:[ ._]|$)", re.IGNORECASE),
    "R": re.compile(r"(?:^|[ ._])R(?:ight)?(?:[ ._]|$)", re.IGNORECASE),
}


def side_of(name):
    for side, pattern in SIDE_PATTERNS.items():
        if pattern.search(name):
            return side
    return None


def is_hand_name(name):
    low = name.lower()
    return "hand" in low or "finger" in low or "thumb" in low


def finger_root_chains(armature, side):
    finger_bones = [b for b in armature.data.bones if side_of(b.name) == side and ("finger" in b.name.lower() or "thumb" in b.name.lower())]
    finger_names = {b.name for b in finger_bones}
    roots = [b for b in finger_bones if b.parent is None or b.parent.name not in finger_names]
    chains = []
    for root in sorted(roots, key=lambda b: b.name):
        chain = []
        queue = deque([root])
        seen = set()
        while queue:
            bone = queue.popleft()
            if bone.name in seen or bone.name not in finger_names:
                continue
            seen.add(bone.name)
            chain.append(bone.name)
            queue.extend(sorted(bone.children, key=lambda b: b.name))
        chains.append(chain)
    return chains


def pose_delta(pose_bone):
    basis = pose_bone.matrix_basis.copy()
    loc, rot, scale = basis.decompose()
    angle = math.degrees(rot.angle)
    return {
        "locationLength": round(float(loc.length), 9),
        "rotationDegrees": round(float(angle), 7),
        "scale": rounded(scale, 8),
        "isIdentity": loc.length < 1e-7 and abs(angle) < 1e-5 and max(abs(s - 1.0) for s in scale) < 1e-7,
        "constraints": [c.type for c in pose_bone.constraints if not c.mute],
    }


def material_record(mat):
    if mat is None:
        return {"name": None}
    return {
        "name": mat.name,
        "diffuseAlpha": round(float(mat.diffuse_color[3]), 6),
        "surfaceRenderMethod": getattr(mat, "surface_render_method", None),
        "useNodes": bool(mat.use_nodes),
    }


def connected_face_components(mesh, selected_faces):
    selected = set(selected_faces)
    vertex_faces = defaultdict(list)
    for poly_index in selected:
        for vertex_index in mesh.polygons[poly_index].vertices:
            vertex_faces[vertex_index].append(poly_index)
    adjacency = defaultdict(set)
    for faces in vertex_faces.values():
        for face in faces:
            adjacency[face].update(f for f in faces if f != face)
    components = []
    unseen = set(selected)
    while unseen:
        seed = unseen.pop()
        component = {seed}
        queue = deque([seed])
        while queue:
            current = queue.popleft()
            for other in adjacency[current]:
                if other in unseen:
                    unseen.remove(other)
                    component.add(other)
                    queue.append(other)
        components.append(sorted(component))
    return sorted(components, key=len, reverse=True)


def audit():
    kind = reset_and_load()
    scene = bpy.context.scene
    scene.frame_set(scene.frame_current)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    depsgraph.update()
    armature = choose_armature()
    body = choose_body(armature)
    evaluated = body.evaluated_get(depsgraph)
    eval_mesh = evaluated.to_mesh()

    modifiers = []
    for modifier in body.modifiers:
        if modifier.type == "ARMATURE":
            modifiers.append({
                "name": modifier.name,
                "target": modifier.object.name if modifier.object else None,
                "showViewport": bool(modifier.show_viewport),
                "showRender": bool(modifier.show_render),
                "useVertexGroups": bool(modifier.use_vertex_groups),
                "useBoneEnvelopes": bool(modifier.use_bone_envelopes),
            })

    group_index_to_name = {g.index: g.name for g in body.vertex_groups}
    vertex_group_weights = defaultdict(dict)
    for vertex in body.data.vertices:
        for membership in vertex.groups:
            vertex_group_weights[vertex.index][membership.group] = float(membership.weight)

    result = {
        "schema": "family-company.hand-topology-audit.v1",
        "label": ARGS.label,
        "input": INPUT,
        "inputKind": kind,
        "sceneFrame": int(scene.frame_current),
        "armature": {
            "name": armature.name,
            "boneCount": len(armature.data.bones),
            "objectScale": rounded(armature.scale, 9),
            "matrixWorldDeterminant": round(float(armature.matrix_world.to_3x3().determinant()), 10),
            "matrixWorld": matrix_rows(armature.matrix_world),
        },
        "body": {
            "name": body.name,
            "vertexCount": len(body.data.vertices),
            "polygonCount": len(body.data.polygons),
            "vertexGroupCount": len(body.vertex_groups),
            "objectScale": rounded(body.scale, 9),
            "matrixWorldDeterminant": round(float(body.matrix_world.to_3x3().determinant()), 10),
            "armatureModifiers": modifiers,
        },
        "sides": {},
    }

    for side in ("L", "R"):
        relevant_groups = [g for g in body.vertex_groups if side_of(g.name) == side and is_hand_name(g.name)]
        relevant_indices = {g.index for g in relevant_groups}
        finger_groups = [g for g in relevant_groups if "finger" in g.name.lower() or "thumb" in g.name.lower()]
        hand_groups = [g for g in relevant_groups if "hand" in g.name.lower()]

        union_vertices = []
        dominant_vertices = []
        group_records = []
        for group in relevant_groups:
            weighted = [(v.index, vertex_group_weights[v.index].get(group.index, 0.0)) for v in body.data.vertices]
            weighted = [(index, weight) for index, weight in weighted if weight > 1e-6]
            points_rest = [body.matrix_world @ body.data.vertices[index].co for index, _ in weighted]
            points_eval = [evaluated.matrix_world @ eval_mesh.vertices[index].co for index, _ in weighted if index < len(eval_mesh.vertices)]
            group_records.append({
                "name": group.name,
                "weightedVertexCount": len(weighted),
                "weightSum": round(sum(weight for _, weight in weighted), 6),
                "maxWeight": round(max((weight for _, weight in weighted), default=0.0), 6),
                "restBoundsWorld": bounds(points_rest),
                "evaluatedBoundsWorld": bounds(points_eval),
            })

        for vertex in body.data.vertices:
            weights = vertex_group_weights[vertex.index]
            hand_weight = sum(weights.get(index, 0.0) for index in relevant_indices)
            other_weight = sum(weight for index, weight in weights.items() if index not in relevant_indices)
            if hand_weight > 1e-6:
                union_vertices.append(vertex.index)
            if hand_weight > other_weight and hand_weight > 0.05:
                dominant_vertices.append(vertex.index)

        union_set = set(union_vertices)
        dominant_set = set(dominant_vertices)
        selected_faces_any = []
        selected_faces_dominant = []
        material_any = Counter()
        material_dominant = Counter()
        for poly in body.data.polygons:
            verts = set(poly.vertices)
            if verts & union_set:
                selected_faces_any.append(poly.index)
                material_any[poly.material_index] += 1
            if verts and len(verts & dominant_set) >= max(1, math.ceil(len(verts) * 0.5)):
                selected_faces_dominant.append(poly.index)
                material_dominant[poly.material_index] += 1

        components = connected_face_components(body.data, selected_faces_dominant)
        component_records = []
        for component in components:
            verts = sorted({vi for pi in component for vi in body.data.polygons[pi].vertices})
            rest_points = [body.matrix_world @ body.data.vertices[i].co for i in verts]
            eval_points = [evaluated.matrix_world @ eval_mesh.vertices[i].co for i in verts if i < len(eval_mesh.vertices)]
            component_records.append({
                "polygonCount": len(component),
                "vertexCount": len(verts),
                "restBoundsWorld": bounds(rest_points),
                "evaluatedBoundsWorld": bounds(eval_points),
            })

        def material_counts(counter):
            records = []
            for index, count in sorted(counter.items()):
                mat = body.data.materials[index] if 0 <= index < len(body.data.materials) else None
                records.append({"slot": index, "polygonCount": count, "material": material_record(mat)})
            return records

        bone_names = [b.name for b in armature.data.bones if side_of(b.name) == side and is_hand_name(b.name)]
        bone_records = []
        for name in sorted(bone_names):
            bone = armature.data.bones[name]
            pose = armature.pose.bones.get(name)
            rest_head = armature.matrix_world @ bone.head_local
            rest_tail = armature.matrix_world @ bone.tail_local
            pose_head = armature.matrix_world @ pose.head if pose else rest_head
            pose_tail = armature.matrix_world @ pose.tail if pose else rest_tail
            bone_records.append({
                "name": name,
                "parent": bone.parent.name if bone.parent else None,
                "useDeform": bool(bone.use_deform),
                "restHeadWorld": rounded(rest_head),
                "restTailWorld": rounded(rest_tail),
                "poseHeadWorld": rounded(pose_head),
                "poseTailWorld": rounded(pose_tail),
                "lengthWorld": round(float((rest_tail - rest_head).length), 7),
                "poseDelta": pose_delta(pose) if pose else None,
            })

        union_rest_points = [body.matrix_world @ body.data.vertices[i].co for i in union_vertices]
        union_eval_points = [evaluated.matrix_world @ eval_mesh.vertices[i].co for i in union_vertices if i < len(eval_mesh.vertices)]
        dominant_eval_points = [evaluated.matrix_world @ eval_mesh.vertices[i].co for i in dominant_vertices if i < len(eval_mesh.vertices)]
        zero_area = sum(1 for index in selected_faces_dominant if body.data.polygons[index].area < 1e-12)
        chains = finger_root_chains(armature, side)
        result["sides"][side] = {
            "handGroupNames": [g.name for g in hand_groups],
            "fingerGroupNames": [g.name for g in finger_groups],
            "groupCount": len(relevant_groups),
            "fingerBoneChains": chains,
            "fingerChainCount": len(chains),
            "digitCapacityFromRig": len(chains),
            "weightedUnionVertexCount": len(union_vertices),
            "dominantHandVertexCount": len(dominant_vertices),
            "weightedUnionRestBoundsWorld": bounds(union_rest_points),
            "weightedUnionEvaluatedBoundsWorld": bounds(union_eval_points),
            "dominantEvaluatedBoundsWorld": bounds(dominant_eval_points),
            "dominantFaceCount": len(selected_faces_dominant),
            "anyWeightedFaceCount": len(selected_faces_any),
            "dominantConnectedComponents": component_records,
            "zeroAreaDominantFaces": zero_area,
            "materialDistributionAnyWeighted": material_counts(material_any),
            "materialDistributionDominant": material_counts(material_dominant),
            "vertexGroups": group_records,
            "bones": bone_records,
            "nonIdentityHandFingerPoseBones": [record["name"] for record in bone_records if record["poseDelta"] and not record["poseDelta"]["isIdentity"]],
        }

    evaluated.to_mesh_clear()
    return result


record = audit()
os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
with open(OUTPUT, "w", encoding="utf-8") as handle:
    json.dump(record, handle, ensure_ascii=False, indent=2)
print("HAND_AUDIT_JSON=" + OUTPUT)

