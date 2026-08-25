"""Polish Sister Proof6 contacts and clothing masks on retained donor surfaces."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
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


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def coordinate_hash(obj):
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        digest.update(("%.9f,%.9f,%.9f;" % tuple(vertex.co)).encode("ascii"))
    return digest.hexdigest().upper()


armature = bpy.data.objects.get("Armature")
body = bpy.data.objects.get("Yuuka_Original_Body")
camera = bpy.data.objects.get("SisterProofCamera")
tank = bpy.data.objects.get("SisterProof5_Tank_DonorC157")
arm_positive = bpy.data.objects.get("SisterProof5_BareArm_PositiveX_DonorC141")
arm_negative = bpy.data.objects.get("SisterProof5_BareArm_NegativeX_DonorC181")
lower = bpy.data.objects.get("SisterProof5_ShortsAndBareLegs_DonorC063_C099_C218")
foot_positive = bpy.data.objects.get("SisterProof5_BareFoot_PositiveX_DonorC000")
foot_negative = bpy.data.objects.get("SisterProof5_BareFoot_NegativeX_DonorC001")
surfaces = [tank, arm_positive, arm_negative, lower, foot_positive, foot_negative]
if any(obj is None for obj in [armature, body, camera, *surfaces]):
    raise RuntimeError("Expected Sister Proof6 scene")
if len(armature.data.bones) != 118:
    raise RuntimeError("Owned Yuuka rig must remain exactly 118 bones")

body_hash_before = coordinate_hash(body)
bone_names_before = sorted(bone.name for bone in armature.data.bones)


def make_simple_material(name, color, roughness=0.74):
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


MAT_SKIN = make_simple_material("SisterProof7Skin", (0.91, 0.70, 0.62), 0.72)
MAT_GRAY = make_simple_material("SisterProof7QAGray", (0.56, 0.59, 0.64), 0.84)


def make_tank_mask_material():
    result = bpy.data.materials.get("SisterProof7TankTextureMask") or bpy.data.materials.new(
        "SisterProof7TankTextureMask"
    )
    result.use_nodes = True
    nodes = result.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.80
    geometry = nodes.new("ShaderNodeNewGeometry")
    separate = nodes.new("ShaderNodeSeparateXYZ")
    absolute_x = nodes.new("ShaderNodeMath")
    absolute_x.operation = "ABSOLUTE"
    central_x = nodes.new("ShaderNodeMath")
    central_x.operation = "LESS_THAN"
    central_x.inputs[1].default_value = 0.057
    outer_x = nodes.new("ShaderNodeMath")
    outer_x.operation = "GREATER_THAN"
    outer_x.inputs[1].default_value = 0.087
    upper_center = nodes.new("ShaderNodeMath")
    upper_center.operation = "GREATER_THAN"
    upper_center.inputs[1].default_value = 0.552
    upper_outer = nodes.new("ShaderNodeMath")
    upper_outer.operation = "GREATER_THAN"
    upper_outer.inputs[1].default_value = 0.535
    center_skin = nodes.new("ShaderNodeMath")
    center_skin.operation = "MULTIPLY"
    outer_skin = nodes.new("ShaderNodeMath")
    outer_skin.operation = "MULTIPLY"
    skin_mask = nodes.new("ShaderNodeMath")
    skin_mask.operation = "MAXIMUM"
    mix = nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MIX"
    mix.inputs[1].default_value = (0.022, 0.021, 0.031, 1.0)
    mix.inputs[2].default_value = (0.91, 0.70, 0.62, 1.0)
    tree = result.node_tree
    tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    tree.links.new(separate.outputs["X"], absolute_x.inputs[0])
    tree.links.new(absolute_x.outputs[0], central_x.inputs[0])
    tree.links.new(absolute_x.outputs[0], outer_x.inputs[0])
    tree.links.new(separate.outputs["Z"], upper_center.inputs[0])
    tree.links.new(separate.outputs["Z"], upper_outer.inputs[0])
    tree.links.new(central_x.outputs[0], center_skin.inputs[0])
    tree.links.new(upper_center.outputs[0], center_skin.inputs[1])
    tree.links.new(outer_x.outputs[0], outer_skin.inputs[0])
    tree.links.new(upper_outer.outputs[0], outer_skin.inputs[1])
    tree.links.new(center_skin.outputs[0], skin_mask.inputs[0])
    tree.links.new(outer_skin.outputs[0], skin_mask.inputs[1])
    tree.links.new(skin_mask.outputs[0], mix.inputs[0])
    tree.links.new(mix.outputs[0], shader.inputs["Base Color"])
    tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return result


def make_lower_mask_material():
    result = bpy.data.materials.get("SisterProof7LowerTextureMask") or bpy.data.materials.new(
        "SisterProof7LowerTextureMask"
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
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (0.91, 0.70, 0.62, 1.0)
    ramp.color_ramp.elements[1].position = 0.752
    ramp.color_ramp.elements[1].color = (0.90, 0.93, 0.97, 1.0)
    navy = ramp.color_ramp.elements.new(0.778)
    navy.color = (0.030, 0.090, 0.215, 1.0)
    result.node_tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    result.node_tree.links.new(separate.outputs["Z"], mapping.inputs["Value"])
    result.node_tree.links.new(mapping.outputs["Result"], ramp.inputs["Fac"])
    result.node_tree.links.new(ramp.outputs["Color"], shader.inputs["Base Color"])
    result.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return result


MAT_TANK = make_tank_mask_material()
MAT_LOWER = make_lower_mask_material()


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


def reshape_world(obj, transform):
    inverse = obj.matrix_world.inverted()
    for vertex in obj.data.vertices:
        vertex.co = inverse @ Vector(transform(obj.matrix_world @ vertex.co))
    obj.data.update()
    recalc_normals(obj)


def close_boundaries(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    boundary = [edge for edge in mesh.edges if len(edge.link_faces) == 1]
    before = len(mesh.faces)
    if boundary:
        bmesh.ops.holes_fill(mesh, edges=boundary, sides=0)
        bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    result = {"boundaryEdgesSubmitted": len(boundary), "facesAdded": len(mesh.faces) - before}
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()
    return result


# Close open donor cloth/foot rims before their existing subdivision modifiers.
boundary_repairs = {
    obj.name: close_boundaries(obj)
    for obj in [arm_positive, arm_negative, lower, foot_positive, foot_negative]
}


def clean_tank_hem(point):
    point = Vector(point)
    if point.z < 0.425:
        normalized_x = min(1.0, abs(point.x) / 0.105)
        target = 0.405 + 0.006 * (1.0 - normalized_x * normalized_x)
        point.z = target + (point.z - target) * 0.10
    return point


reshape_world(tank, clean_tank_hem)


def pull_shoulder_under_tank(obj, sign):
    def transform(point):
        point = Vector(point)
        shoulder = max(0.0, min(1.0, (point.z - 0.500) / 0.085))
        point.x -= sign * 0.042 * shoulder
        point.y = -0.004 + (point.y + 0.004) * (1.0 + 0.08 * shoulder)
        return point

    reshape_world(obj, transform)


pull_shoulder_under_tank(arm_positive, 1.0)
pull_shoulder_under_tank(arm_negative, -1.0)


def add_knee_and_hip_cues(point):
    point = Vector(point)
    if abs(point.x) < 0.125 and point.z < 0.330:
        sign = -1.0 if point.x < 0.0 else 1.0
        axis = 0.061 * sign
        local_x = point.x - axis
        knee = math.exp(-((point.z - 0.225) / 0.052) ** 2)
        calf = math.exp(-((point.z - 0.145) / 0.060) ** 2)
        factor = 1.0 - 0.075 * knee + 0.035 * calf
        point.x = axis + local_x * factor
        point.y = -0.010 + (point.y + 0.010) * factor
    elif point.z >= 0.330:
        hip = max(0.0, min(1.0, (point.z - 0.330) / 0.079))
        point.x *= 1.0 + 0.045 * hip
    return point


reshape_world(lower, add_knee_and_hip_cues)


def lower_foot(point):
    point = Vector(point)
    point.z -= 0.008
    return point


reshape_world(foot_positive, lower_foot)
reshape_world(foot_negative, lower_foot)

replace_material(tank, MAT_TANK)
replace_material(arm_positive, MAT_SKIN)
replace_material(arm_negative, MAT_SKIN)
replace_material(lower, MAT_LOWER)
replace_material(foot_positive, MAT_SKIN)
replace_material(foot_negative, MAT_SKIN)


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

old_floor = bpy.data.objects.get("SisterProof6Floor")
if old_floor is not None:
    bpy.data.objects.remove(old_floor, do_unlink=True)
bpy.ops.mesh.primitive_plane_add(size=10.0, location=(0.0, 0.0, -0.001))
floor = bpy.context.object
floor.name = "SisterProof7Floor"
floor.data.materials.append(make_simple_material("SisterProof7FloorMaterial", (0.040, 0.052, 0.072), 0.92))

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
    path = OUTPUT / f"sister-proof7-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    color_outputs.append(path)

material_backup = {obj.name: list(obj.data.materials) for obj in surfaces}
for obj in surfaces:
    replace_material(obj, MAT_GRAY)
gray_outputs = []
for label, yaw in views:
    point_camera(yaw)
    path = OUTPUT / f"sister-proof7-{label}-gray-silhouette.png"
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

blend_path = OUTPUT / "sister-proof7-contact-and-style.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

receipt = {
    "schema": "family-company.sister-proof7-contact-and-style.v1",
    "status": "AWAITING_ROOT_CONTACT_STYLE_GATE",
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
    "contactAndStyleRepair": {
        "boundaryRepairs": boundary_repairs,
        "tankNecklineArmholeMethod": "texture mask on the same closed c157 donor surface",
        "shortsPipingMethod": "narrow position-shader band on retained c063/c099/c218 surfaces",
        "shoulderMethod": "retained c141/c181 upper vertices pulled under the c157 surface",
        "kneeHipMethod": "small coordinate refinement on retained lower donor loops",
        "primitiveCharacterGeometryAdded": 0,
        "voxelRemeshes": 0,
    },
    "topology": stats,
    "colorStaticViews": [path.name for path in color_outputs],
    "graySilhouetteViews": [path.name for path in gray_outputs],
    "gifCreated": False,
    "blend": blend_path.name,
    "limitations": [
        "internal static contact/style QA only",
        "root visual approval is required before animation, GIF or Unity",
        "animation deformation has not been tested",
    ],
}
with (OUTPUT / "sister-proof7-contact-and-style-receipt.json").open("w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print("SISTER_PROOF7_CONTACT_AND_STYLE_RENDERED")
