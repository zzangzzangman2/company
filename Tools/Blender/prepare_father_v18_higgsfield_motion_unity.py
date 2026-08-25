import argparse
import hashlib
import json
import math
import struct
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


def parse_args():
    separator = sys.argv.index("--") if "--" in sys.argv else len(sys.argv)
    parser = argparse.ArgumentParser()
    parser.add_argument("--idle-glb", required=True)
    parser.add_argument("--run-glb", required=True)
    parser.add_argument("--output-idle-fbx", required=True)
    parser.add_argument("--output-run-fbx", required=True)
    parser.add_argument("--output-texture", required=True)
    parser.add_argument("--receipt", required=True)
    return parser.parse_args(sys.argv[separator + 1 :])


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def hash_mesh(mesh):
    digest = hashlib.sha256()
    for vertex in mesh.data.vertices:
        digest.update(struct.pack("<3f", *vertex.co))
        for group in sorted(vertex.groups, key=lambda value: value.group):
            digest.update(struct.pack("<If", group.group, group.weight))
    for polygon in mesh.data.polygons:
        digest.update(struct.pack("<I", len(polygon.vertices)))
        for index in polygon.vertices:
            digest.update(struct.pack("<I", index))
    if mesh.data.uv_layers.active is not None:
        for value in mesh.data.uv_layers.active.data:
            digest.update(struct.pack("<2f", *value.uv))
    return digest.hexdigest().upper()


def hash_mesh_components(mesh):
    geometry = hashlib.sha256()
    topology = hashlib.sha256()
    weights = hashlib.sha256()
    uvs = hashlib.sha256()
    group_names = [group.name for group in mesh.vertex_groups]
    for vertex in mesh.data.vertices:
        geometry.update(struct.pack("<3f", *vertex.co))
        for group in sorted(vertex.groups, key=lambda value: group_names[value.group]):
            weights.update(group_names[group.group].encode("utf-8"))
            weights.update(b"\0")
            weights.update(struct.pack("<f", group.weight))
    for polygon in mesh.data.polygons:
        topology.update(struct.pack("<I", len(polygon.vertices)))
        for index in polygon.vertices:
            topology.update(struct.pack("<I", index))
    if mesh.data.uv_layers.active is not None:
        for value in mesh.data.uv_layers.active.data:
            uvs.update(struct.pack("<2f", *value.uv))
    return {
        "geometry": geometry.hexdigest().upper(),
        "topology": topology.hexdigest().upper(),
        "weights": weights.hexdigest().upper(),
        "uvs": uvs.hexdigest().upper(),
    }


def hash_armature(armature):
    digest = hashlib.sha256()
    for bone in armature.data.bones:
        digest.update(bone.name.encode("utf-8"))
        digest.update(b"\0")
        digest.update((bone.parent.name if bone.parent else "").encode("utf-8"))
        digest.update(b"\0")
        digest.update(struct.pack("<3f", *bone.head_local))
        digest.update(struct.pack("<3f", *bone.tail_local))
    return digest.hexdigest().upper()


def packed_image_hash(image):
    if image.packed_file is None:
        return ""
    return hashlib.sha256(bytes(image.packed_file.data)).hexdigest().upper()


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


def import_and_validate(path, expected_action_fragment):
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(path))
    bpy.context.view_layer.update()

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(armatures) != 1:
        raise RuntimeError(f"{path.name}: expected one armature, found {len(armatures)}")
    armature = armatures[0]
    skinned = [
        obj
        for obj in meshes
        if any(modifier.type == "ARMATURE" for modifier in obj.modifiers)
    ]
    auxiliary = [obj for obj in meshes if obj not in skinned]
    if len(skinned) != 1:
        raise RuntimeError(f"{path.name}: expected one skinned mesh, found {len(skinned)}")
    if len(auxiliary) != 1 or auxiliary[0].name != "Icosphere":
        raise RuntimeError(
            f"{path.name}: expected only the known Icosphere rigging helper; "
            f"found {[obj.name for obj in auxiliary]}"
        )
    mesh = skinned[0]
    if any(len(vertex.groups) == 0 for vertex in mesh.data.vertices):
        raise RuntimeError(f"{path.name}: the character contains unweighted vertices")

    bone_names = {bone.name for bone in armature.data.bones}
    missing = sorted(REQUIRED_BONES - bone_names)
    if missing:
        raise RuntimeError(f"{path.name}: missing required Humanoid bones: {missing}")

    actions = list(bpy.data.actions)
    if len(actions) != 1 or expected_action_fragment not in actions[0].name:
        raise RuntimeError(
            f"{path.name}: expected one {expected_action_fragment} action; "
            f"found {[action.name for action in actions]}"
        )
    action = actions[0]
    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = action

    images = [
        image
        for image in bpy.data.images
        if image.name != "Render Result" and image.type == "IMAGE" and image.size[0] > 0
    ]
    if len(images) != 1:
        raise RuntimeError(f"{path.name}: expected one texture image, found {len(images)}")
    materials = list(dict.fromkeys(
        slot.material for slot in mesh.material_slots if slot.material is not None
    ))
    if len(materials) != 1:
        raise RuntimeError(f"{path.name}: expected one material, found {len(materials)}")

    for obj in auxiliary:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.update()

    armature.name = "FatherV18HiggsfieldMotion_Armature"
    armature.data.name = "FatherV18HiggsfieldMotion_Skeleton"
    mesh.name = "FatherV18HiggsfieldMotion"
    mesh.data.name = "FatherV18HiggsfieldMotion_Mesh"
    materials[0].name = "FatherV18HiggsfieldMotion_Material"
    images[0].name = "father-v18-higgsfield-motion-albedo"

    # Keep the generated skeleton and skin unchanged. Move only the armature object so frame zero
    # is bottom-centred and grounded; animation/root ownership remains with Unity's office agent.
    bpy.context.scene.frame_set(math.floor(action.frame_range[0]))
    bpy.context.view_layer.update()
    before_min, before_max = world_bounds([mesh])
    armature.location += Vector((
        -(before_min.x + before_max.x) * 0.5,
        -(before_min.y + before_max.y) * 0.5,
        -before_min.z,
    ))
    bpy.context.view_layer.update()
    after_min, after_max = world_bounds([mesh])

    action.name = "FatherV18_" + expected_action_fragment
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start = math.floor(action.frame_range[0])
    scene.frame_end = math.ceil(action.frame_range[1])

    return {
        "armature": armature,
        "mesh": mesh,
        "image": images[0],
        "material": materials[0],
        "action": action,
        "boneNames": sorted(bone_names),
        "boneCount": len(bone_names),
        "meshHash": hash_mesh(mesh),
        "meshComponentHashes": hash_mesh_components(mesh),
        "armatureHash": hash_armature(armature),
        "packedTextureHash": packed_image_hash(images[0]),
        "vertexCount": len(mesh.data.vertices),
        "polygonCount": len(mesh.data.polygons),
        "vertexGroupCount": len(mesh.vertex_groups),
        "weightedVertexCount": sum(1 for vertex in mesh.data.vertices if len(vertex.groups) > 0),
        "actionFrameStart": float(action.frame_range[0]),
        "actionFrameEnd": float(action.frame_range[1]),
        "normalizationSourceBoundsMin": list(before_min),
        "normalizationSourceBoundsMax": list(before_max),
        "normalizedBoundsMin": list(after_min),
        "normalizedBoundsMax": list(after_max),
    }


def export_fbx(path, data):
    bpy.ops.object.select_all(action="DESELECT")
    data["armature"].select_set(True)
    data["mesh"].select_set(True)
    bpy.context.view_layer.objects.active = data["armature"]
    bpy.ops.export_scene.fbx(
        filepath=str(path),
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


def main():
    args = parse_args()
    idle_glb = Path(args.idle_glb).resolve()
    run_glb = Path(args.run_glb).resolve()
    output_idle_fbx = Path(args.output_idle_fbx).resolve()
    output_run_fbx = Path(args.output_run_fbx).resolve()
    output_texture = Path(args.output_texture).resolve()
    receipt_path = Path(args.receipt).resolve()
    for path in (output_idle_fbx, output_run_fbx, output_texture, receipt_path):
        path.parent.mkdir(parents=True, exist_ok=True)

    idle = import_and_validate(idle_glb, "Idle")
    idle["image"].filepath_raw = str(output_texture)
    idle["image"].file_format = "PNG"
    idle["image"].save()
    export_fbx(output_idle_fbx, idle)

    run = import_and_validate(run_glb, "Lean_Forward_Sprint_inplace")
    export_fbx(output_run_fbx, run)

    same_geometry = (
        idle["meshComponentHashes"]["geometry"]
        == run["meshComponentHashes"]["geometry"]
    )
    same_topology = (
        idle["meshComponentHashes"]["topology"]
        == run["meshComponentHashes"]["topology"]
    )
    same_uvs = (
        idle["meshComponentHashes"]["uvs"]
        == run["meshComponentHashes"]["uvs"]
    )
    same_weights = (
        idle["meshComponentHashes"]["weights"]
        == run["meshComponentHashes"]["weights"]
    )
    same_bind_skeleton = idle["armatureHash"] == run["armatureHash"]
    if not same_topology or not same_uvs:
        print(
            "MOTION_SOURCE_MESH_DIFFERENCE="
            + json.dumps(
                {
                    "sameGeometry": same_geometry,
                    "sameTopology": same_topology,
                    "sameUvs": same_uvs,
                    "sameWeights": same_weights,
                    "sameBindSkeleton": same_bind_skeleton,
                    "idle": idle["meshComponentHashes"],
                    "run": run["meshComponentHashes"],
                },
                separators=(",", ":"),
            )
        )
        raise RuntimeError("Idle and run jobs changed character topology/UV identity")
    if idle["packedTextureHash"] != run["packedTextureHash"]:
        raise RuntimeError("Idle and run jobs do not contain the same embedded texture")

    receipt = {
        "contract": "FC-FATHER-V18-HIGGSFIELD-IDLE-RUN-UNITY-PREP-V1",
        "sourceIdleGlb": idle_glb.name,
        "sourceIdleGlbSha256": sha256(idle_glb),
        "sourceRunGlb": run_glb.name,
        "sourceRunGlbSha256": sha256(run_glb),
        "outputIdleFbx": output_idle_fbx.as_posix(),
        "outputIdleFbxSha256": sha256(output_idle_fbx),
        "outputRunFbx": output_run_fbx.as_posix(),
        "outputRunFbxSha256": sha256(output_run_fbx),
        "outputTexture": output_texture.as_posix(),
        "outputTextureSha256": sha256(output_texture),
        "sameGeometry": same_geometry,
        "sameTopology": same_topology,
        "sameUvs": same_uvs,
        "sameSkinWeights": same_weights,
        "sameBindSkeleton": same_bind_skeleton,
        "sameEmbeddedTexture": True,
        "idleMeshHash": idle["meshHash"],
        "runMeshHash": run["meshHash"],
        "idleMeshComponentHashes": idle["meshComponentHashes"],
        "runMeshComponentHashes": run["meshComponentHashes"],
        "armatureHash": idle["armatureHash"],
        "packedTextureHash": idle["packedTextureHash"],
        "idle": {key: value for key, value in idle.items() if isinstance(value, (str, int, float, list))},
        "run": {key: value for key, value in run.items() if isinstance(value, (str, int, float, list))},
        "removedAuxiliaryObject": "Icosphere",
        "retargetPolicy": "Render only the idle-0 skinned body; consume run-644 as a Humanoid motion source so independently generated run skin/bind data never replaces or overlaps the visible body",
        "rootMotionPolicy": "OfficeRuntimeAgent owns translation/yaw; imported animation root curves are locked/baked in Unity",
        "productionEligible": False,
    }
    receipt_path.write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print("FATHER_V18_HIGGSFIELD_MOTION_PREP=PASS")
    print("RECEIPT=" + str(receipt_path))


if __name__ == "__main__":
    main()
