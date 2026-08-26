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


def build_armature():
    data = bpy.data.armatures.new("FatherV18CleanBiped_Skeleton")
    armature = bpy.data.objects.new("FatherV18CleanBiped_Armature", data)
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

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        create_bone(
            bones,
            side + "Shoulder",
            (0.0, sign * 0.020, 0.700),
            (0.0, sign * 0.112, 0.700),
            "Spine02",
        )
        create_bone(
            bones,
            side + "Arm",
            (0.0, sign * 0.112, 0.700),
            (0.0, sign * 0.145, 0.565),
            side + "Shoulder",
        )
        create_bone(
            bones,
            side + "ForeArm",
            (0.0, sign * 0.145, 0.565),
            (0.0, sign * 0.160, 0.435),
            side + "Arm",
        )
        create_bone(
            bones,
            side + "Hand",
            (0.0, sign * 0.160, 0.435),
            (0.012, sign * 0.160, 0.340),
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


def smooth_pair(lower_name, upper_name, value, lower, upper):
    if upper <= lower:
        return {lower_name: 1.0}
    t = max(0.0, min(1.0, (value - lower) / (upper - lower)))
    t = t * t * (3.0 - 2.0 * t)
    return {lower_name: 1.0 - t, upper_name: t}


def classify_vertex(point):
    x, y, z = point
    side = "Left" if y >= 0.0 else "Right"
    lateral = abs(y)

    # The paid detailed source is a single static surface with arms resting beside the body.
    # Separate the outer arm/hand columns from the trouser columns before assigning any weights;
    # no vertex is ever allowed to blend between a hand and a leg or between left and right legs.
    if z < 0.28:
        arm_vertex = False
    elif z < 0.53:
        arm_vertex = lateral > 0.116
    elif z < 0.72:
        shoulder_threshold = 0.108 + (z - 0.53) * 0.050
        arm_vertex = lateral > shoulder_threshold
    else:
        arm_vertex = False

    if arm_vertex:
        if z < 0.425:
            return {side + "Hand": 1.0}
        if z < 0.475:
            return smooth_pair(side + "Hand", side + "ForeArm", z, 0.425, 0.475)
        if z < 0.555:
            return {side + "ForeArm": 1.0}
        if z < 0.610:
            return smooth_pair(side + "ForeArm", side + "Arm", z, 0.555, 0.610)
        if z < 0.685:
            return {side + "Arm": 1.0}
        return smooth_pair(side + "Arm", side + "Shoulder", z, 0.685, 0.725)

    if z >= 0.755:
        return {"Head": 1.0}
    if z >= 0.710:
        return smooth_pair("Spine02", "neck", z, 0.710, 0.755)
    if z >= 0.650:
        return smooth_pair("Spine01", "Spine02", z, 0.650, 0.710)
    if z >= 0.580:
        return smooth_pair("Spine", "Spine01", z, 0.580, 0.650)
    if z >= 0.505:
        return smooth_pair("Hips", "Spine", z, 0.505, 0.580)

    # Belt, fly, crotch bridge, and the top trouser band must remain one rigid pelvis surface.
    # Splitting these vertices by Y makes the zipper and belt open into a false third limb.
    if z >= 0.435:
        return {"Hips": 1.0}
    if z >= 0.255 and lateral < 0.036:
        centre_leg = side + ("UpLeg" if z >= 0.285 else "Leg")
        return smooth_pair("Hips", centre_leg, lateral, 0.008, 0.036)
    if z >= 0.385:
        return smooth_pair(side + "UpLeg", "Hips", z, 0.385, 0.435)
    if z >= 0.315:
        return {side + "UpLeg": 1.0}
    if z >= 0.255:
        return smooth_pair(side + "Leg", side + "UpLeg", z, 0.255, 0.315)
    if z >= 0.145:
        return {side + "Leg": 1.0}
    if z >= 0.095:
        return smooth_pair(side + "Foot", side + "Leg", z, 0.095, 0.145)
    # The detailed shoes are assembled from many overlapping shell components. Keep every shell,
    # lace, sole, and trouser cuff rigidly on its anatomical Foot bone; toe blending tears those
    # disconnected shells into the dangling flaps visible in the rejected generated rig.
    return {side + "Foot": 1.0}


def rig_mesh(mesh, armature):
    for group in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(group)
    groups = {name: mesh.vertex_groups.new(name=name) for name in DEFORM_BONES}
    counts = {name: 0 for name in DEFORM_BONES}
    maximum_influences = 0
    for vertex in mesh.data.vertices:
        weights = classify_vertex(vertex.co)
        total = sum(weights.values())
        if not math.isclose(total, 1.0, rel_tol=0.0, abs_tol=1e-5):
            raise RuntimeError(f"Vertex {vertex.index} weights sum to {total}")
        maximum_influences = max(maximum_influences, len(weights))
        for name, weight in weights.items():
            if weight <= 0.000001:
                continue
            groups[name].add([vertex.index], float(weight), "REPLACE")
            counts[name] += 1
    mesh.parent = armature
    modifier = mesh.modifiers.new("FatherV18CleanBiped", "ARMATURE")
    modifier.object = armature
    return counts, maximum_influences


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

    mesh.name = "FatherV18CleanBiped"
    mesh.data.name = "FatherV18CleanBiped_Mesh"
    armature = build_armature()
    counts, maximum_influences = rig_mesh(mesh, armature)
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
        "contract": "FC-FATHER-V18-STATIC-APPEARANCE-CLEAN-BIPED-RIG-V1",
        "sourceFbx": input_fbx.as_posix(),
        "sourceFbxSha256": sha256(input_fbx),
        "outputFbx": output_fbx.as_posix(),
        "outputFbxSha256": sha256(output_fbx),
        "sourceWorldVertexHash": before_world_hash,
        "postApplyWorldVertexHash": after_apply_world_hash,
        "riggedWorldVertexHashAtRest": world_vertex_hash(mesh),
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
        "weightVertexCounts": counts,
        "appearancePolicy": "Exact paid Father V18 static vertices/topology/UV/material slots retained; only a new biped armature, armature modifier, and deterministic skin weights were added",
        "motionPolicy": "No generated motion clip is embedded; Unity QA drives a handcrafted two-contact SD biped cycle on this clean Humanoid rig",
        "rejectedSourcePolicy": "CasualWalk613 moving FBX skeleton/weights are not reused",
        "productionEligible": False,
    }
    receipt_path.write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print("FATHER_V18_CLEAN_BIPED_RIG=PASS", flush=True)
    print("RECEIPT=" + str(receipt_path), flush=True)


if __name__ == "__main__":
    main()
