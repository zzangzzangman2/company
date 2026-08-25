"""Repair the Sister Proof5 direct-donor gate without replacing its surfaces.

Proof6 closes the donor tank's inherited garment cuts, smooths the retained
donor arm/leg/foot topology, overlaps shoulder and ankle contacts, and moves the
shorts/piping cutoff from per-face colors to a continuous position shader.  It
does not add primitive character geometry, create a GIF, or touch Unity.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import sys
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
tank = bpy.data.objects.get("SisterProof5_Tank_DonorC157")
arm_positive = bpy.data.objects.get("SisterProof5_BareArm_PositiveX_DonorC141")
arm_negative = bpy.data.objects.get("SisterProof5_BareArm_NegativeX_DonorC181")
lower = bpy.data.objects.get("SisterProof5_ShortsAndBareLegs_DonorC063_C099_C218")
foot_positive = bpy.data.objects.get("SisterProof5_BareFoot_PositiveX_DonorC000")
foot_negative = bpy.data.objects.get("SisterProof5_BareFoot_NegativeX_DonorC001")
surfaces = [tank, arm_positive, arm_negative, lower, foot_positive, foot_negative]
if any(obj is None for obj in [armature, body, camera, mouth, *surfaces]):
    raise RuntimeError("Expected Sister Proof5 donor-surface scene")
if len(armature.data.bones) != 118:
    raise RuntimeError("Owned Yuuka rig must remain exactly 118 bones")

body_hash_before = coordinate_hash(body)
bone_names_before = sorted(bone.name for bone in armature.data.bones)


def material(name, color, roughness=0.72):
    result = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    result.use_nodes = True
    nodes = result.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    result.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return result


MAT_SKIN = material("SisterProof6Skin", (0.91, 0.70, 0.62), 0.72)
MAT_TANK = material("SisterProof6BlackTank", (0.022, 0.021, 0.031), 0.80)
MAT_GRAY = material("SisterProof6QAGray", (0.56, 0.59, 0.64), 0.84)


def lower_position_material():
    result = bpy.data.materials.get("SisterProof6LowerPositionColor") or bpy.data.materials.new(
        "SisterProof6LowerPositionColor"
    )
    result.use_nodes = True
    nodes = result.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.76
    geometry = nodes.new("ShaderNodeNewGeometry")
    separate = nodes.new("ShaderNodeSeparateXYZ")
    mapping = nodes.new("ShaderNodeMapRange")
    mapping.inputs["From Min"].default_value = 0.066
    mapping.inputs["From Max"].default_value = 0.409
    mapping.inputs["To Min"].default_value = 0.0
    mapping.inputs["To Max"].default_value = 1.0
    mapping.clamp = True
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    skin = (0.91, 0.70, 0.62, 1.0)
    white = (0.90, 0.93, 0.97, 1.0)
    navy = (0.030, 0.090, 0.215, 1.0)
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = skin
    ramp.color_ramp.elements[1].position = 0.730
    ramp.color_ramp.elements[1].color = white
    top = ramp.color_ramp.elements.new(0.792)
    top.color = navy
    result.node_tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    result.node_tree.links.new(separate.outputs["Z"], mapping.inputs["Value"])
    result.node_tree.links.new(mapping.outputs["Result"], ramp.inputs["Fac"])
    result.node_tree.links.new(ramp.outputs["Color"], shader.inputs["Base Color"])
    result.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return result


MAT_LOWER = lower_position_material()


def replace_material(obj, item):
    obj.data.materials.clear()
    obj.data.materials.append(item)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = True


def recalc_normals(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def fill_boundary_holes(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    boundary = [edge for edge in mesh.edges if len(edge.link_faces) == 1]
    before_faces = len(mesh.faces)
    if boundary:
        bmesh.ops.holes_fill(mesh, edges=boundary, sides=0)
        bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    added = len(mesh.faces) - before_faces
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()
    return {"boundaryEdgesSubmitted": len(boundary), "facesAdded": added}


def reshape_world(obj, transform):
    inverse = obj.matrix_world.inverted()
    for vertex in obj.data.vertices:
        vertex.co = inverse @ Vector(transform(obj.matrix_world @ vertex.co))
    obj.data.update()
    recalc_normals(obj)


def add_subdivision(obj, levels):
    for modifier in list(obj.modifiers):
        if modifier.type == "SUBSURF" and modifier.name.startswith("SisterProof6"):
            obj.modifiers.remove(modifier)
    modifier = obj.modifiers.new("SisterProof6_DonorSurfaceSubdivision", "SUBSURF")
    modifier.subdivision_type = "CATMULL_CLARK"
    modifier.levels = levels
    modifier.render_levels = levels
    modifier.show_only_control_edges = True


# Close only c157's inherited fantasy-shirt cut lines.  The donor surface and
# all source loops remain; bmesh fills their own boundary cycles without cages.
tank_hole_repair = fill_boundary_holes(tank)


def reshape_tank(point):
    point = Vector(point)
    t = max(0.0, min(1.0, (point.z - 0.397) / (0.635 - 0.397)))
    # Remove the donor flare at the waist, retain a modest chest/strap width.
    point.x *= 0.82 + 0.13 * t
    point.y = -0.010 + (point.y + 0.010) * 0.88
    return point


reshape_world(tank, reshape_tank)


def repair_shoulder_and_limb(obj, sign):
    def transform(point):
        point = Vector(point)
        # Existing Proof5 arm runs shoulder -> wrist as |x| increases.  Pull the
        # shoulder boundary underneath the tank and soften its square donor cuff.
        shoulder = max(0.0, min(1.0, (0.175 - abs(point.x)) / 0.070))
        point.x -= sign * 0.030 * shoulder
        point.z -= 0.004 * shoulder
        point.y = -0.004 + (point.y + 0.004) * (0.94 + 0.10 * shoulder)
        return point

    reshape_world(obj, transform)


repair_shoulder_and_limb(arm_positive, 1.0)
repair_shoulder_and_limb(arm_negative, -1.0)


def round_foot(obj, sign):
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    source_axis = sum(point.x for point in points) / len(points)
    source_y = sum(point.y for point in points) / len(points)
    source_z = sum(point.z for point in points) / len(points)
    half_x = max(abs(point.x - source_axis) for point in points)
    half_y = max(abs(point.y - source_y) for point in points)
    half_z = max(abs(point.z - source_z) for point in points)
    target_axis = 0.061 * sign

    def transform(point):
        point = Vector(point)
        nx = (point.x - source_axis) / max(half_x, 1.0e-6)
        ny = (point.y - source_y) / max(half_y, 1.0e-6)
        nz = (point.z - source_z) / max(half_z, 1.0e-6)
        # Preserve the donor vertex directions but refit them to a rounded,
        # asymmetric bare-foot envelope with a flat contact underside.
        front = max(0.0, -ny)
        x = target_axis + 0.039 * nx * (1.0 + 0.08 * front)
        y = -0.012 + (0.080 if ny < 0.0 else 0.046) * ny
        z_radius = 0.032 + 0.012 * max(0.0, ny)
        z = 0.034 + z_radius * nz
        z = max(0.002, z)
        return Vector((x, y, z))

    reshape_world(obj, transform)


round_foot(foot_positive, 1.0)
round_foot(foot_negative, -1.0)

replace_material(tank, MAT_TANK)
replace_material(arm_positive, MAT_SKIN)
replace_material(arm_negative, MAT_SKIN)
replace_material(lower, MAT_LOWER)
replace_material(foot_positive, MAT_SKIN)
replace_material(foot_negative, MAT_SKIN)

add_subdivision(tank, 1)
add_subdivision(arm_positive, 2)
add_subdivision(arm_negative, 2)
add_subdivision(lower, 1)
add_subdivision(foot_positive, 2)
add_subdivision(foot_negative, 2)


def topology_stats(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    components = 0
    unseen = set(bm.verts)
    while unseen:
        components += 1
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
        "connectedComponents": components,
        "boundaryEdges": sum(1 for edge in bm.edges if len(edge.link_faces) == 1),
        "nonManifoldEdges": sum(1 for edge in bm.edges if len(edge.link_faces) != 2),
        "nonQuadPolygons": sum(1 for face in bm.faces if len(face.verts) != 4),
    }
    bm.free()
    evaluated.to_mesh_clear()
    return result


stats = {obj.name: topology_stats(obj) for obj in surfaces}


old_floor = bpy.data.objects.get("SisterProof5Floor")
if old_floor is not None:
    bpy.data.objects.remove(old_floor, do_unlink=True)
bpy.ops.mesh.primitive_plane_add(size=10.0, location=(0.0, 0.0, -0.001))
floor = bpy.context.object
floor.name = "SisterProof6Floor"
floor.data.materials.append(material("SisterProof6FloorMaterial", (0.040, 0.052, 0.072), 0.92))

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
    path = OUTPUT / f"sister-proof6-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    color_outputs.append(path)

material_backup = {obj.name: list(obj.data.materials) for obj in surfaces}
for obj in surfaces:
    replace_material(obj, MAT_GRAY)
gray_outputs = []
for label, yaw in views:
    point_camera(yaw)
    path = OUTPUT / f"sister-proof6-{label}-gray-silhouette.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    gray_outputs.append(path)
for obj in surfaces:
    obj.data.materials.clear()
    for item in material_backup[obj.name]:
        obj.data.materials.append(item)
    for polygon in obj.data.polygons:
        polygon.material_index = 0

body_hash_after = coordinate_hash(body)
if body_hash_after != body_hash_before:
    raise RuntimeError("Owned Yuuka face/hair/hand body coordinates changed")
if sorted(bone.name for bone in armature.data.bones) != bone_names_before:
    raise RuntimeError("Owned Yuuka rig bone names changed")

blend_path = OUTPUT / "sister-proof6-surface-repair.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

receipt = {
    "schema": "family-company.sister-proof6-surface-repair.v1",
    "status": "AWAITING_ROOT_SURFACE_REPAIR_GATE",
    "candidateClaim": False,
    "sourceBlend": str(SOURCE_BLEND),
    "sourceBlendSha256": sha256(SOURCE_BLEND),
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
    "repair": {
        "tankBoundaryRepair": tank_hole_repair,
        "primitiveCharacterGeometryAdded": 0,
        "voxelRemeshes": 0,
        "shortsPipingGeometry": 0,
        "shortsPipingMethod": "continuous position shader on the same donor lower surfaces",
        "surfaceSmoothing": "Catmull-Clark modifiers on retained donor topology",
        "shoulderContact": "donor arm boundary overlapped underneath donor tank surface",
        "footMethod": "retained c000/c001 vertex directions refit to rounded bare-foot envelopes",
    },
    "topology": stats,
    "colorStaticViews": [path.name for path in color_outputs],
    "graySilhouetteViews": [path.name for path in gray_outputs],
    "gifCreated": False,
    "blend": blend_path.name,
    "limitations": [
        "internal static surface-repair QA only",
        "root visual approval is required before animation, GIF or Unity",
        "animation deformation has not been tested",
    ],
}
with (OUTPUT / "sister-proof6-surface-repair-receipt.json").open("w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print("SISTER_PROOF6_SURFACE_REPAIR_RENDERED")
