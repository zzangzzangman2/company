"""Build the Older Sister Proof5 direct-donor-surface gate.

This diagnostic starts from Sister Proof3 only to retain its approved Yuuka
face, eyes, long hair, original three-digit stylized hands, mouth repair and
118-bone rig.  Every rejected procedural torso/limb/foot object is removed.
Fresh, user-owned Yuuka garment/limb islands are then isolated from the source
FBX and reshaped in place.  No primitive cage, voxel remesh, GIF, Unity export,
or production-candidate claim is made here.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import sys
from collections import defaultdict
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
SOURCE_BLEND = Path(bpy.data.filepath).resolve()
REPO = Path(__file__).resolve().parents[2]
SOURCE_FBX = (
    REPO
    / "Artifacts"
    / "ExternalReferenceStudy"
    / "UserProvided_BlueArchive_OriginalMeshes_2026-08-24"
    / "Yuuka"
    / "Yuuka_Original_Mesh"
    / "Yuuka_Original_Mesh.fbx"
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def coordinate_hash(obj) -> str:
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        digest.update(("%.9f,%.9f,%.9f;" % tuple(vertex.co)).encode("ascii"))
    return digest.hexdigest().upper()


armature = bpy.data.objects.get("Armature")
body = bpy.data.objects.get("Yuuka_Original_Body")
camera = bpy.data.objects.get("SisterProofCamera")
mouth = bpy.data.objects.get("NormalizedMouthCurve")
if armature is None or body is None or camera is None or mouth is None:
    raise RuntimeError("Expected Sister Proof3 armature/body/camera/mouth")
if len(armature.data.bones) != 118:
    raise RuntimeError("Owned Yuuka rig must remain exactly 118 bones")
if not SOURCE_FBX.is_file():
    raise RuntimeError(f"Missing user-owned Yuuka source FBX: {SOURCE_FBX}")

body_hash_before = coordinate_hash(body)
bone_names_before = sorted(bone.name for bone in armature.data.bones)


# Remove every rejected Proof3/Proof4 procedural body part, but never touch the
# retained body object that carries face, eyes, hair and native hands.
remove_prefixes = (
    "SisterTankTorso",
    "SisterTankStrap",
    "SisterContinuousArm",
    "SisterShorts",
    "SisterContinuousLeg",
    "SisterBareFoot",
    "SisterToe",
    "SisterProof3Floor",
    "SisterProof4Floor",
)
for obj in list(bpy.data.objects):
    if obj.name.startswith(remove_prefixes):
        bpy.data.objects.remove(obj, do_unlink=True)


def material(name, color, roughness=0.68):
    result = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    result.use_nodes = True
    bsdf = result.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return result


MAT_SKIN = material("SisterProof5Skin", (0.91, 0.70, 0.62), 0.72)
MAT_TANK = material("SisterProof5BlackTank", (0.028, 0.026, 0.038), 0.78)
MAT_SHORTS = material("SisterProof5NavyShorts", (0.030, 0.090, 0.215), 0.76)
MAT_PIPING = material("SisterProof5WhitePiping", (0.90, 0.93, 0.97), 0.74)
MAT_GRAY = material("SisterProof5QAGray", (0.56, 0.59, 0.64), 0.84)


def delete_vertices(obj, indices):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    mesh.verts.ensure_lookup_table()
    targets = [mesh.verts[index] for index in sorted(indices) if index < len(mesh.verts)]
    bmesh.ops.delete(mesh, geom=targets, context="VERTS")
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def recalc_normals(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def connected_components(mesh):
    parent = list(range(len(mesh.vertices)))

    def find(value):
        while parent[value] != value:
            parent[value] = parent[parent[value]]
            value = parent[value]
        return value

    def union(left, right):
        left_root, right_root = find(left), find(right)
        if left_root != right_root:
            parent[right_root] = left_root

    for edge in mesh.edges:
        union(edge.vertices[0], edge.vertices[1])
    groups = defaultdict(set)
    for vertex in mesh.vertices:
        groups[find(vertex.index)].add(vertex.index)
    roots = sorted(groups, key=lambda root: min(groups[root]))
    return [{"id": index, "vertices": groups[root]} for index, root in enumerate(roots)]


def replace_materials(obj, materials):
    obj.data.materials.clear()
    for item in materials:
        obj.data.materials.append(item)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = True


def reshape_world(obj, transform):
    inverse = obj.matrix_world.inverted()
    for vertex in obj.data.vertices:
        vertex.co = inverse @ Vector(transform(obj.matrix_world @ vertex.co))
    obj.data.update()
    recalc_normals(obj)


# Import a clean copy solely to recover the authored donor islands deleted from
# Proof3.  Scale its armature exactly as Proof3 did, then retarget duplicates to
# the already-preserved 118-bone armature.
objects_before_import = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX))
imported = [obj for obj in bpy.data.objects if obj not in objects_before_import]
donor_armatures = [obj for obj in imported if obj.type == "ARMATURE"]
donor_bodies = [
    obj
    for obj in imported
    if obj.type == "MESH" and obj.name.startswith("Yuuka_Original_Body") and len(obj.data.vertices) == 6126
]
if len(donor_armatures) != 1 or len(donor_bodies) != 1:
    raise RuntimeError(
        f"Fresh Yuuka import was ambiguous: armatures={len(donor_armatures)} bodies={len(donor_bodies)}"
    )
donor_armature = donor_armatures[0]
donor_source = donor_bodies[0]
donor_armature.scale = (1.0, 1.0, 1.0)
bpy.context.view_layer.update()
components = connected_components(donor_source.data)
if len(components) != 355:
    raise RuntimeError(f"Unexpected Yuuka component count: {len(components)}")


def donor_component_object(name, component_ids, materials):
    obj = donor_source.copy()
    obj.data = donor_source.data.copy()
    obj.name = name
    bpy.context.collection.objects.link(obj)
    keep = set().union(*(components[index]["vertices"] for index in component_ids))
    delete_vertices(obj, set(range(len(obj.data.vertices))) - keep)
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.matrix_parent_inverse = armature.matrix_world.inverted()
    obj.matrix_world = world
    for modifier in obj.modifiers:
        if modifier.type == "ARMATURE":
            modifier.object = armature
    if not any(modifier.type == "ARMATURE" for modifier in obj.modifiers):
        modifier = obj.modifiers.new("OwnedYuuka118BoneRig", "ARMATURE")
        modifier.object = armature
    replace_materials(obj, materials)
    obj.hide_render = False
    obj.hide_set(False)
    obj["proofRevision"] = "SisterProof5DonorSurfaceGate"
    obj["candidateClaim"] = False
    return obj


# c157 is the donor-fitted inner torso. c141/c181 are its fitted arm envelopes;
# c063/c099/c218 are the authored pelvis/leg shells.  Feet use only the rounded
# upper islands c000/c001 so shoe sole plates cannot survive as separate parts.
tank = donor_component_object("SisterProof5_Tank_DonorC157", {157}, [MAT_TANK])
arm_positive = donor_component_object("SisterProof5_BareArm_PositiveX_DonorC141", {141}, [MAT_SKIN])
arm_negative = donor_component_object("SisterProof5_BareArm_NegativeX_DonorC181", {181}, [MAT_SKIN])
lower = donor_component_object(
    "SisterProof5_ShortsAndBareLegs_DonorC063_C099_C218",
    {63, 99, 218},
    [MAT_SKIN, MAT_SHORTS, MAT_PIPING],
)
foot_positive = donor_component_object("SisterProof5_BareFoot_PositiveX_DonorC000", {0}, [MAT_SKIN])
foot_negative = donor_component_object("SisterProof5_BareFoot_NegativeX_DonorC001", {1}, [MAT_SKIN])


def reshape_tank(point):
    # Preserve the donor seam flow while taking fantasy-shirt volume out of the
    # silhouette.  The component is already sleeveless when c141/c181 are split.
    vertical = max(0.0, min(1.0, (point.z - 0.397) / (0.635 - 0.397)))
    point.x *= 0.94 - 0.05 * vertical
    point.y = -0.012 + (point.y + 0.012) * 0.72
    return point


reshape_world(tank, reshape_tank)


def reshape_arm(obj, sign):
    source_shoulder = Vector((0.108 * sign, -0.002, 0.585))
    source_wrist = Vector((0.270 * sign, -0.002, 0.442))
    target_shoulder = Vector((0.114 * sign, -0.004, 0.582))
    target_wrist = Vector((0.278 * sign, -0.004, 0.430))
    source_axis = source_wrist - source_shoulder
    source_axis_len_sq = source_axis.length_squared

    def transform(point):
        point = Vector(point)
        t = max(0.0, min(1.0, (point - source_shoulder).dot(source_axis) / source_axis_len_sq))
        source_center = source_shoulder.lerp(source_wrist, t)
        target_center = target_shoulder.lerp(target_wrist, t)
        radial = point - source_center
        # Retain donor loop placement, but remove sleeve puff. Slight shoulder
        # fullness and a clean wrist taper keep the age-20 silhouette readable.
        scale = 0.78 - 0.13 * t
        radial.x *= scale
        radial.y *= scale * 0.92
        radial.z *= scale
        return target_center + radial

    reshape_world(obj, transform)


reshape_arm(arm_positive, 1.0)
reshape_arm(arm_negative, -1.0)


def reshape_lower(point):
    point = Vector(point)
    if abs(point.x) < 0.115 and point.z >= 0.326:
        # Donor pelvis/shorts bridge: fitted, slightly narrower, never rebuilt.
        point.x *= 0.90
        point.y = -0.012 + (point.y + 0.012) * 0.76
        return point
    sign = -1.0 if point.x < 0.0 else 1.0
    source_axis = 0.069 * sign
    target_axis = 0.061 * sign
    point.x = target_axis + (point.x - source_axis) * 0.80
    point.y = -0.010 + (point.y + 0.010) * 0.76
    t = max(0.0, min(1.0, (point.z - 0.142) / (0.409 - 0.142)))
    point.z = 0.066 + t * (0.409 - 0.066)
    return point


reshape_world(lower, reshape_lower)


# The same authored lower surface changes material across the shorts hem;
# piping is a face-material band, never a floating strip or geometry ring.
for polygon in lower.data.polygons:
    center = lower.matrix_world @ polygon.center
    if center.z >= 0.334:
        polygon.material_index = 1
    elif center.z >= 0.313:
        polygon.material_index = 2
    else:
        polygon.material_index = 0


def reshape_foot(obj, sign):
    source_axis = 0.0865 * sign
    target_axis = 0.061 * sign
    zs = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    z_min = min(point.z for point in zs)
    z_max = max(point.z for point in zs)

    def transform(point):
        point = Vector(point)
        point.x = target_axis + (point.x - source_axis) * 0.86
        point.y = -0.022 + (point.y + 0.027) * 1.07
        zt = (point.z - z_min) / max(z_max - z_min, 1.0e-6)
        point.z = 0.002 + zt * 0.073
        # A slight forefoot spread reads as a soft anime bare foot while the
        # donor upper's connected topology remains untouched.
        front = max(0.0, min(1.0, (-point.y - 0.006) / 0.065))
        point.x = target_axis + (point.x - target_axis) * (1.0 + 0.10 * front)
        return point

    reshape_world(obj, transform)


reshape_foot(foot_positive, 1.0)
reshape_foot(foot_negative, -1.0)


# Fresh import was only a geometry source.  All visible duplicates now point at
# the preserved rig, so imported source/weapon/calculator/armature can leave the
# output scene without affecting source files on disk.
visible_surfaces = [tank, arm_positive, arm_negative, lower, foot_positive, foot_negative]
for obj in imported:
    if obj in visible_surfaces:
        continue
    if obj.name in bpy.data.objects:
        bpy.data.objects.remove(obj, do_unlink=True)


# Proof3's surface-attached mouth was visually too wide; narrow only that curve.
mouth.scale.x *= 0.58
mouth["proof5MouthAdjustment"] = "existing surface-attached curve width reduced only"


def topology_stats(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    components_count = 0
    unseen = set(bm.verts)
    while unseen:
        components_count += 1
        stack = [unseen.pop()]
        while stack:
            vertex = stack.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in unseen:
                    unseen.remove(other)
                    stack.append(other)
    result = {
        "vertices": len(bm.verts),
        "edges": len(bm.edges),
        "polygons": len(bm.faces),
        "connectedComponents": components_count,
        "boundaryEdges": sum(1 for edge in bm.edges if len(edge.link_faces) == 1),
        "nonManifoldEdges": sum(1 for edge in bm.edges if len(edge.link_faces) != 2),
        "nonQuadPolygons": sum(1 for face in bm.faces if len(face.verts) != 4),
    }
    bm.free()
    evaluated.to_mesh_clear()
    return result


stats = {obj.name: topology_stats(obj) for obj in visible_surfaces}


# Review floor and static views only.
bpy.ops.mesh.primitive_plane_add(size=10.0, location=(0.0, 0.0, -0.001))
floor = bpy.context.object
floor.name = "SisterProof5Floor"
floor.data.materials.append(material("SisterProof5FloorMaterial", (0.040, 0.052, 0.072), 0.92))

scene = bpy.context.scene
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.camera = camera
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.10
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False
scene.render.use_file_extension = True
scene.render.resolution_x = 1200
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100


def point_camera(yaw_degrees, target_z=0.500, radius=3.20):
    radians = math.radians(yaw_degrees)
    target = Vector((0.0, 0.0, target_z))
    camera.location = (math.sin(radians) * radius, -math.cos(radians) * radius, target_z + 0.02)
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


views = (("front", 0), ("three-quarter", 45), ("side", 90), ("back", 180))
color_outputs = []
for label, yaw in views:
    point_camera(yaw)
    path = OUTPUT / f"sister-proof5-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    color_outputs.append(path)


material_backup = {obj.name: list(obj.data.materials) for obj in visible_surfaces}
material_indices = {obj.name: [polygon.material_index for polygon in obj.data.polygons] for obj in visible_surfaces}
for obj in visible_surfaces:
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
gray_outputs = []
for label, yaw in views:
    point_camera(yaw)
    path = OUTPUT / f"sister-proof5-{label}-gray-silhouette.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    gray_outputs.append(path)
for obj in visible_surfaces:
    obj.data.materials.clear()
    for item in material_backup[obj.name]:
        obj.data.materials.append(item)
    for polygon, material_index in zip(obj.data.polygons, material_indices[obj.name]):
        polygon.material_index = material_index


body_hash_after = coordinate_hash(body)
if body_hash_after != body_hash_before:
    raise RuntimeError("Owned Yuuka face/hair/hand body coordinates changed")
if sorted(bone.name for bone in armature.data.bones) != bone_names_before:
    raise RuntimeError("Owned Yuuka rig bone names changed")

blend_path = OUTPUT / "sister-proof5-donor-surface-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

receipt = {
    "schema": "family-company.sister-proof5-donor-surface-gate.v1",
    "status": "AWAITING_ROOT_DONOR_SURFACE_GATE",
    "candidateClaim": False,
    "sourceBlend": str(SOURCE_BLEND),
    "sourceBlendSha256": sha256(SOURCE_BLEND),
    "freshOwnedDonorFbx": str(SOURCE_FBX),
    "freshOwnedDonorFbxSha256": sha256(SOURCE_FBX),
    "test3OrSakurakoUsed": False,
    "unityModified": False,
    "preservation": {
        "originalFaceEyesHairHandsCoordinateHashBefore": body_hash_before,
        "originalFaceEyesHairHandsCoordinateHashAfter": body_hash_after,
        "originalDonorBodyCoordinatesUnchanged": body_hash_before == body_hash_after,
        "handPolicy": "original 3-digit stylized hand retained",
        "boneCount": len(armature.data.bones),
        "boneNamesUnchanged": sorted(bone.name for bone in armature.data.bones) == bone_names_before,
    },
    "directDonorComponents": {
        "tank": ["c157"],
        "positiveXArm": ["c141"],
        "negativeXArm": ["c181"],
        "shortsAndBareLegs": ["c063", "c099", "c218"],
        "positiveXFoot": ["c000"],
        "negativeXFoot": ["c001"],
    },
    "surfacePolicy": {
        "primitiveCages": 0,
        "voxelRemeshes": 0,
        "floatingPipingGeometry": 0,
        "shoeSolePlateComponentsUsed": False,
        "shortsSkinPipingBoundary": "material assignment on the same retained donor lower surfaces",
    },
    "topology": stats,
    "colorStaticViews": [path.name for path in color_outputs],
    "graySilhouetteViews": [path.name for path in gray_outputs],
    "gifCreated": False,
    "blend": blend_path.name,
    "limitations": [
        "internal static donor-surface QA only",
        "root visual approval is required before color polish, animation, GIF or Unity",
        "animation deformation has not been tested",
    ],
}
with (OUTPUT / "sister-proof5-donor-surface-gate-receipt.json").open("w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print("SISTER_PROOF5_DONOR_SURFACE_GATE_RENDERED")
