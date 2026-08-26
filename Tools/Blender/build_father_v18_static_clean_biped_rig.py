import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


DEFORM_BONES = (
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
)


def parse_args():
    separator = sys.argv.index("--") if "--" in sys.argv else len(sys.argv)
    parser = argparse.ArgumentParser()
    parser.add_argument("--input-fbx", required=True)
    parser.add_argument("--output-fbx", required=True)
    parser.add_argument("--receipt", required=True)
    return parser.parse_args(sys.argv[separator + 1 :])


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def world_vertex_hash(mesh):
    digest = hashlib.sha256()
    for vertex in mesh.data.vertices:
        point = mesh.matrix_world @ vertex.co
        digest.update(struct.pack("<3f", *point))
    return digest.hexdigest().upper()


def topology_uv_hash(mesh):
    digest = hashlib.sha256()
    for polygon in mesh.data.polygons:
        digest.update(struct.pack("<I", len(polygon.vertices)))
        for vertex in polygon.vertices:
            digest.update(struct.pack("<I", vertex))
    if mesh.data.uv_layers.active is not None:
        for value in mesh.data.uv_layers.active.data:
            digest.update(struct.pack("<2f", *value.uv))
    return digest.hexdigest().upper()


def create_bone(edit_bones, name, head, tail, parent=None, deform=True):
    bone = edit_bones.new(name)
    bone.head = Vector(head)
    bone.tail = Vector(tail)
    bone.roll = 0.0
    bone.use_deform = deform
    if parent is not None:
        bone.parent = edit_bones[parent]
    return bone


def build_armature(t_pose=False):
    suffix = "TPose" if t_pose else "SourcePose"
    data = bpy.data.armatures.new("FatherV18CleanBipedV2_" + suffix + "_Skeleton")
    armature = bpy.data.objects.new("FatherV18CleanBipedV2_" + suffix + "_Armature", data)
    bpy.context.scene.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bones = data.edit_bones

    create_bone(bones, "Hips", (0.0, 0.0, 0.47), (0.0, 0.0, 0.535))
    create_bone(bones, "Spine", (0.0, 0.0, 0.535), (0.0, 0.0, 0.605), "Hips")
    create_bone(bones, "Spine01", (0.0, 0.0, 0.605), (0.0, 0.0, 0.675), "Spine")
    create_bone(bones, "Spine02", (0.0, 0.0, 0.675), (0.0, 0.0, 0.725), "Spine01")
    create_bone(bones, "neck", (0.0, 0.0, 0.725), (0.0, 0.0, 0.770), "Spine02")
    create_bone(bones, "Head", (0.0, 0.0, 0.770), (0.0, 0.0, 0.920), "neck")
    create_bone(bones, "head_end", (0.0, 0.0, 0.920), (0.0, 0.0, 1.010), "Head", False)
    create_bone(bones, "headfront", (0.0, 0.0, 0.855), (0.100, 0.0, 0.855), "Head", False)

    upper_arm_length = math.sqrt(0.033 * 0.033 + 0.135 * 0.135)
    forearm_length = math.sqrt(0.015 * 0.015 + 0.130 * 0.130)
    hand_length = math.sqrt(0.012 * 0.012 + 0.095 * 0.095)
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        create_bone(
            bones,
            side + "Shoulder",
            (0.0, sign * 0.020, 0.700),
            (0.0, sign * 0.112, 0.700),
            "Spine02",
        )
        if t_pose:
            arm_head = Vector((0.0, sign * 0.112, 0.700))
            arm_tail = arm_head + Vector((0.0, sign * upper_arm_length, 0.0))
            forearm_tail = arm_tail + Vector((0.0, sign * forearm_length, 0.0))
            hand_tail = forearm_tail + Vector((0.0, sign * hand_length, 0.0))
        else:
            arm_head = Vector((0.0, sign * 0.112, 0.700))
            arm_tail = Vector((0.0, sign * 0.145, 0.565))
            forearm_tail = Vector((0.0, sign * 0.160, 0.435))
            hand_tail = Vector((0.012, sign * 0.160, 0.340))
        create_bone(bones, side + "Arm", arm_head, arm_tail, side + "Shoulder")
        create_bone(
            bones,
            side + "ForeArm",
            arm_tail,
            forearm_tail,
            side + "Arm",
        )
        create_bone(
            bones,
            side + "Hand",
            forearm_tail,
            hand_tail,
            side + "ForeArm",
        )
        create_bone(
            bones,
            side + "UpLeg",
            (0.0, sign * 0.064, 0.480),
            (0.0, sign * 0.068, 0.285),
            "Hips",
        )
        create_bone(
            bones,
            side + "Leg",
            (0.0, sign * 0.068, 0.285),
            (0.0, sign * 0.070, 0.082),
            side + "UpLeg",
        )
        create_bone(
            bones,
            side + "Foot",
            (0.0, sign * 0.070, 0.082),
            (0.095, sign * 0.070, 0.040),
            side + "Leg",
        )
        create_bone(
            bones,
            side + "ToeBase",
            (0.095, sign * 0.070, 0.040),
            (0.165, sign * 0.070, 0.035),
            side + "Foot",
        )

    bpy.ops.object.mode_set(mode="OBJECT")
    armature.show_in_front = True
    return armature


ARM_BONES = {
    name
    for name in DEFORM_BONES
    if "Shoulder" in name or "Arm" in name or "Hand" in name
}
LEG_BONES = {
    name
    for name in DEFORM_BONES
    if "Leg" in name or "Foot" in name or "Toe" in name
}
CENTRE_BONES = {"Hips", "Spine", "Spine01", "Spine02", "neck", "Head"}


def point_segment_distance(point, head, tail):
    segment = tail - head
    denominator = segment.length_squared
    if denominator <= 1e-12:
        return (point - head).length
    t = max(0.0, min(1.0, (point - head).dot(segment) / denominator))
    return (point - (head + t * segment)).length


def nearest_anatomical_bone(point, armature):
    candidates = list(CENTRE_BONES)
    if point.y >= -0.006:
        candidates.extend(name for name in DEFORM_BONES if name.startswith("Left"))
    if point.y <= 0.006:
        candidates.extend(name for name in DEFORM_BONES if name.startswith("Right"))
    return min(
        candidates,
        key=lambda name: point_segment_distance(
            point,
            armature.data.bones[name].head_local,
            armature.data.bones[name].tail_local,
        ),
    )


def normalize_weights(weights):
    kept = sorted(
        ((name, value) for name, value in weights.items() if value > 0.000001),
        key=lambda item: item[1],
        reverse=True,
    )[:4]
    total = sum(value for _, value in kept)
    if total <= 1e-12:
        return {}
    return {name: value / total for name, value in kept}


def sanitize_heat_weights(mesh, armature):
    index_to_name = {group.index: group.name for group in mesh.vertex_groups}
    sanitized = []
    automatic_unweighted = 0
    cross_side_removed = 0
    arm_leg_mixes_removed = 0
    for vertex in mesh.data.vertices:
        weights = {
            index_to_name[item.group]: float(item.weight)
            for item in vertex.groups
            if index_to_name.get(item.group) in DEFORM_BONES and item.weight > 0.000001
        }
        if not weights:
            automatic_unweighted += 1

        if vertex.co.y > 0.006:
            forbidden = [name for name in weights if name.startswith("Right")]
        elif vertex.co.y < -0.006:
            forbidden = [name for name in weights if name.startswith("Left")]
        else:
            left_total = sum(
                value for name, value in weights.items() if name.startswith("Left")
            )
            right_total = sum(
                value for name, value in weights.items() if name.startswith("Right")
            )
            if left_total > 0.000001 and right_total > 0.000001:
                if left_total > right_total * 1.05:
                    forbidden = [name for name in weights if name.startswith("Right")]
                elif right_total > left_total * 1.05:
                    forbidden = [name for name in weights if name.startswith("Left")]
                else:
                    # A symmetric centre seam must be owned by the pelvis/spine chain, never split
                    # between both legs: that split is the false third-limb failure this gate blocks.
                    forbidden = [
                        name
                        for name in weights
                        if name.startswith("Left") or name.startswith("Right")
                    ]
            else:
                forbidden = []
        for name in forbidden:
            cross_side_removed += 1
            weights.pop(name, None)

        arm_total = sum(weights.get(name, 0.0) for name in ARM_BONES)
        leg_total = sum(weights.get(name, 0.0) for name in LEG_BONES)
        if arm_total > 0.000001 and leg_total > 0.000001:
            arm_leg_mixes_removed += 1
            remove = LEG_BONES if arm_total >= leg_total else ARM_BONES
            for name in remove:
                weights.pop(name, None)

        weights = normalize_weights(weights)
        if not weights:
            weights = {nearest_anatomical_bone(vertex.co, armature): 1.0}
        sanitized.append(weights)

    for group in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(group)
    groups = {name: mesh.vertex_groups.new(name=name) for name in DEFORM_BONES}
    counts = {name: 0 for name in DEFORM_BONES}
    maximum_influences = 0
    for vertex, weights in zip(mesh.data.vertices, sanitized):
        maximum_influences = max(maximum_influences, len(weights))
        for name, weight in weights.items():
            groups[name].add([vertex.index], weight, "REPLACE")
            counts[name] += 1
    return (
        sanitized,
        counts,
        maximum_influences,
        automatic_unweighted,
        cross_side_removed,
        arm_leg_mixes_removed,
    )


def rig_mesh_with_heat_map(mesh, armature):
    for group in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(group)
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    result = bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    if "FINISHED" not in result:
        raise RuntimeError("Blender ARMATURE_AUTO heat-map binding did not finish")
    bpy.context.view_layer.update()
    return sanitize_heat_weights(mesh, armature)


def apply_t_pose_bind(mesh, source_armature, weights_by_vertex):
    target_armature = build_armature(t_pose=True)
    transforms = {
        name: target_armature.data.bones[name].matrix_local
        @ source_armature.data.bones[name].matrix_local.inverted()
        for name in DEFORM_BONES
    }
    for vertex, weights in zip(mesh.data.vertices, weights_by_vertex):
        source = vertex.co.copy()
        transformed = Vector((0.0, 0.0, 0.0))
        for name, weight in weights.items():
            transformed += (transforms[name] @ source) * weight
        vertex.co = transformed

    for modifier in list(mesh.modifiers):
        if modifier.type == "ARMATURE":
            mesh.modifiers.remove(modifier)
    mesh.parent = None
    bpy.data.objects.remove(source_armature, do_unlink=True)
    mesh.parent = target_armature
    modifier = mesh.modifiers.new("FatherV18CleanBipedV2", "ARMATURE")
    modifier.object = target_armature
    bpy.context.view_layer.update()
    return target_armature


def main():
    args = parse_args()
    input_fbx = Path(args.input_fbx).resolve()
    output_fbx = Path(args.output_fbx).resolve()
    receipt_path = Path(args.receipt).resolve()
    output_fbx.parent.mkdir(parents=True, exist_ok=True)
    receipt_path.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(input_fbx), use_anim=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one detailed static mesh, found {len(meshes)}")
    mesh = meshes[0]
    if len(mesh.data.vertices) != 28895 or len(mesh.data.polygons) != 49192:
        raise RuntimeError(
            "Detailed source identity changed: "
            f"vertices={len(mesh.data.vertices)} polygons={len(mesh.data.polygons)}"
        )

    before_world_hash = world_vertex_hash(mesh)
    before_world_vertices = [mesh.matrix_world @ vertex.co.copy() for vertex in mesh.data.vertices]
    topology_hash = topology_uv_hash(mesh)
    original_matrix = [list(row) for row in mesh.matrix_world]
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()
    after_apply_world_hash = world_vertex_hash(mesh)
    maximum_rest_vertex_delta = max(
        (before_world_vertices[index] - vertex.co).length
        for index, vertex in enumerate(mesh.data.vertices)
    )
    if maximum_rest_vertex_delta > 0.000001:
        raise RuntimeError(
            "Applying the source transform moved static world-space vertices by "
            f"{maximum_rest_vertex_delta}"
        )

    mesh.name = "FatherV18CleanBipedV2"
    mesh.data.name = "FatherV18CleanBipedV2_Mesh"
    source_armature = build_armature(t_pose=False)
    (
        weights_by_vertex,
        counts,
        maximum_influences,
        automatic_unweighted_vertices,
        cross_side_memberships_removed,
        arm_leg_mixes_removed,
    ) = rig_mesh_with_heat_map(mesh, source_armature)
    armature = apply_t_pose_bind(mesh, source_armature, weights_by_vertex)
    bpy.context.view_layer.update()

    group_names = {group.index: group.name for group in mesh.vertex_groups}
    cross_side_vertices = 0
    arm_leg_vertices = 0
    unweighted_vertices = 0
    for vertex in mesh.data.vertices:
        names = {
            group_names[item.group]
            for item in vertex.groups
            if item.weight > 0.000001
        }
        if not names:
            unweighted_vertices += 1
        if any(name.startswith("Left") for name in names) and any(
            name.startswith("Right") for name in names
        ):
            cross_side_vertices += 1
        has_arm = any("Arm" in name or "Hand" in name or "Shoulder" in name for name in names)
        has_leg = any("Leg" in name or "Foot" in name or "Toe" in name for name in names)
        if has_arm and has_leg:
            arm_leg_vertices += 1
    if cross_side_vertices or arm_leg_vertices or unweighted_vertices:
        raise RuntimeError(
            "Clean skin validation failed: "
            f"cross={cross_side_vertices} armLeg={arm_leg_vertices} "
            f"unweighted={unweighted_vertices}"
        )

    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    armature.select_set(True)
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
        bake_anim=False,
    )

    receipt = {
        "contract": "FC-FATHER-V18-STATIC-APPEARANCE-CLEAN-BIPED-RIG-V2",
        "sourceFbx": input_fbx.as_posix(),
        "sourceFbxSha256": sha256(input_fbx),
        "outputFbx": output_fbx.as_posix(),
        "outputFbxSha256": sha256(output_fbx),
        "sourceWorldVertexHash": before_world_hash,
        "postApplyWorldVertexHash": after_apply_world_hash,
        "riggedWorldVertexHashAtTPoseBind": world_vertex_hash(mesh),
        "maximumRestVertexDelta": maximum_rest_vertex_delta,
        "topologyUvHash": topology_hash,
        "sourceObjectMatrix": original_matrix,
        "vertexCount": len(mesh.data.vertices),
        "polygonCount": len(mesh.data.polygons),
        "boneCount": len(armature.data.bones),
        "deformBoneCount": len(DEFORM_BONES),
        "maximumInfluencesPerVertex": maximum_influences,
        "weightedVertexCount": len(mesh.data.vertices) - unweighted_vertices,
        "crossSideVertexCount": cross_side_vertices,
        "armLegMixedVertexCount": arm_leg_vertices,
        "automaticUnweightedVertexCountBeforeFallback": automatic_unweighted_vertices,
        "crossSideMembershipsRemoved": cross_side_memberships_removed,
        "armLegMixedVerticesSanitized": arm_leg_mixes_removed,
        "weightVertexCounts": counts,
        "weightingPolicy": "Blender ARMATURE_AUTO bone-heat weights computed against the anatomical source-pose skeleton, capped at four influences, with opposite-side and arm-leg contamination removed",
        "bindPosePolicy": "The heat-weighted source surface and arm chain were transformed together into a horizontal T-pose; the exported armature rest pose is that T-pose",
        "appearancePolicy": "Exact paid Father V18 topology, UV, material slots, texture, and body proportions retained; only the arm vertices move to the required T-pose bind coordinates",
        "motionPolicy": "No motion is embedded; Unity must validate this rig with the paid Casual_Walk_inplace action 613 at poseStrength 1.0",
        "rejectedSourcePolicy": "The action-613 moving mesh, skeleton, and skin weights are not reused; only its separate Humanoid AnimationClip is allowed in Unity QA",
        "productionEligible": False,
    }
    receipt_path.write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print("FATHER_V18_CLEAN_BIPED_RIG=PASS", flush=True)
    print("RECEIPT=" + str(receipt_path), flush=True)


if __name__ == "__main__":
    main()
