"""Build Player Human Proof9 from user-owned Yuuka donor surfaces only.

Model policy:
- no procedural character primitives, lofts, curves, slabs, boxes, or panels;
- preserve the imported Yuuka face, EyeMouth geometry, hands, pose, weights,
  armature modifier, and 118-bone rig;
- recolor and reshape only donor body/hair/clothing surfaces;
- the cap is one connected duplicate of donor scalp component c281, directly
  reshaped and fitted into the retained hair (no generated UV sphere/brim).

Review-only lights, camera, and ground are scene helpers, not character mesh.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from collections import defaultdict
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output",
        default="Artifacts/Family3DPlayerHumanV5/Proof9",
    )
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(argv)


ARGS = parse_args()
REPO = Path(__file__).resolve().parents[2]
OUTPUT = (REPO / ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
SOURCE_DIR = (
    REPO
    / "Artifacts"
    / "ExternalReferenceStudy"
    / "UserProvided_BlueArchive_OriginalMeshes_2026-08-24"
    / "Yuuka"
    / "Yuuka_Original_Mesh"
)
SOURCE_FBX = SOURCE_DIR / "Yuuka_Original_Mesh.fbx"
FACE_TEXTURE = SOURCE_DIR / "Yuuka_Original_Face.png"
HAIR_TEXTURE = SOURCE_DIR / "Yuuka_Original_Hair.png"
EYE_TEXTURE = SOURCE_DIR / "Yuuka_Original_EyeMouth.png"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def principled_material(name: str, color, roughness=0.62, metallic=0.0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if "IOR Level" in shader.inputs:
        shader.inputs["IOR Level"].default_value = 0.25
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def textured_material(name: str, path: Path, roughness=0.62):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(str(path), check_existing=True)
    texture.interpolation = "Linear"
    shader.inputs["Roughness"].default_value = roughness
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def brown_hair_material(name: str, path: Path):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(str(path), check_existing=True)
    gray = nodes.new("ShaderNodeRGBToBW")
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.08
    ramp.color_ramp.elements[0].color = (0.012, 0.003, 0.001, 1.0)
    ramp.color_ramp.elements[1].position = 0.80
    ramp.color_ramp.elements[1].color = (0.22, 0.060, 0.020, 1.0)
    ramp.color_ramp.elements.new(0.46).color = (0.070, 0.016, 0.006, 1.0)
    shader.inputs["Roughness"].default_value = 0.48
    material.node_tree.links.new(texture.outputs["Color"], gray.inputs["Color"])
    material.node_tree.links.new(gray.outputs["Val"], ramp.inputs["Fac"])
    material.node_tree.links.new(ramp.outputs["Color"], shader.inputs["Base Color"])
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def keyed_brown_eye_material(name: str, path: Path):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(str(path), check_existing=True)
    texture.interpolation = "Closest"
    hue = nodes.new("ShaderNodeHueSaturation")
    hue.inputs["Hue"].default_value = 0.86
    hue.inputs["Saturation"].default_value = 0.78
    hue.inputs["Value"].default_value = 1.06
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.mode = "RGB"
    maximum_rg = nodes.new("ShaderNodeMath")
    maximum_rg.operation = "MAXIMUM"
    maximum_rgb = nodes.new("ShaderNodeMath")
    maximum_rgb.operation = "MAXIMUM"
    subtract = nodes.new("ShaderNodeMath")
    subtract.operation = "SUBTRACT"
    subtract.inputs[1].default_value = 0.022
    multiply = nodes.new("ShaderNodeMath")
    multiply.operation = "MULTIPLY"
    multiply.inputs[1].default_value = 7.0
    multiply.use_clamp = True
    shader.inputs["Roughness"].default_value = 0.40
    material.node_tree.links.new(texture.outputs["Color"], hue.inputs["Color"])
    material.node_tree.links.new(hue.outputs["Color"], shader.inputs["Base Color"])
    material.node_tree.links.new(texture.outputs["Color"], separate.inputs["Color"])
    material.node_tree.links.new(separate.outputs["Red"], maximum_rg.inputs[0])
    material.node_tree.links.new(separate.outputs["Green"], maximum_rg.inputs[1])
    material.node_tree.links.new(maximum_rg.outputs[0], maximum_rgb.inputs[0])
    material.node_tree.links.new(separate.outputs["Blue"], maximum_rgb.inputs[1])
    material.node_tree.links.new(maximum_rgb.outputs[0], subtract.inputs[0])
    material.node_tree.links.new(subtract.outputs[0], multiply.inputs[0])
    material.node_tree.links.new(multiply.outputs[0], shader.inputs["Alpha"])
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    try:
        material.surface_render_method = "DITHERED"
    except (AttributeError, TypeError):
        if hasattr(material, "blend_method"):
            material.blend_method = "HASHED"
    return material


def horizontal_stripe_material(name: str, navy, yellow):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    geometry = nodes.new("ShaderNodeNewGeometry")
    separate = nodes.new("ShaderNodeSeparateXYZ")
    multiply = nodes.new("ShaderNodeMath")
    multiply.operation = "MULTIPLY"
    multiply.inputs[1].default_value = 27.3
    sine = nodes.new("ShaderNodeMath")
    sine.operation = "SINE"
    greater = nodes.new("ShaderNodeMath")
    greater.operation = "GREATER_THAN"
    greater.inputs[1].default_value = 0.0
    mix = nodes.new("ShaderNodeMixRGB")
    mix.inputs[1].default_value = navy
    mix.inputs[2].default_value = yellow
    shader.inputs["Roughness"].default_value = 0.58
    material.node_tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    material.node_tree.links.new(separate.outputs["Z"], multiply.inputs[0])
    material.node_tree.links.new(multiply.outputs[0], sine.inputs[0])
    material.node_tree.links.new(sine.outputs[0], greater.inputs[0])
    material.node_tree.links.new(greater.outputs[0], mix.inputs[0])
    material.node_tree.links.new(mix.outputs[0], shader.inputs["Base Color"])
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def assign_original_slot(obj, old_name: str, replacement) -> int:
    for index, material in enumerate(obj.data.materials):
        if material and material.name == old_name:
            obj.data.materials[index] = replacement
            return index
    raise RuntimeError(f"Missing material slot {old_name}")


def connected_components(mesh):
    """Return components ordered by minimum source vertex, matching audit cNNN."""
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
    by_root = defaultdict(set)
    for vertex in mesh.vertices:
        by_root[find(vertex.index)].add(vertex.index)
    roots = sorted(by_root, key=lambda root: min(by_root[root]))
    vertex_to_component = {}
    components = []
    for component_id, root in enumerate(roots):
        vertices = by_root[root]
        components.append({"id": component_id, "vertices": vertices, "polygons": set()})
        for index in vertices:
            vertex_to_component[index] = component_id
    for polygon in mesh.polygons:
        component_id = vertex_to_component[polygon.vertices[0]]
        components[component_id]["polygons"].add(polygon.index)
    return components, vertex_to_component


def polygon_group_weight(obj, polygon, predicate) -> float:
    total = 0.0
    for vertex_index in polygon.vertices:
        for assignment in obj.data.vertices[vertex_index].groups:
            if predicate(obj.vertex_groups[assignment.group].name):
                total += assignment.weight
    return total


def polygon_total_weight(obj, polygon) -> float:
    return sum(assignment.weight for index in polygon.vertices for assignment in obj.data.vertices[index].groups)


def delete_vertices(obj, indices) -> None:
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    mesh.verts.ensure_lookup_table()
    targets = [mesh.verts[index] for index in sorted(indices) if index < len(mesh.verts)]
    bmesh.ops.delete(mesh, geom=targets, context="VERTS")
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def interpolate(value, anchors):
    if value <= anchors[0][0]:
        return anchors[0][1]
    if value >= anchors[-1][0]:
        return anchors[-1][1]
    for (left_x, left_y), (right_x, right_y) in zip(anchors, anchors[1:]):
        if left_x <= value <= right_x:
            ratio = (value - left_x) / max(right_x - left_x, 1.0e-8)
            return left_y + (right_y - left_y) * ratio
    raise RuntimeError("Interpolation anchors do not cover value")


def component_count(mesh) -> int:
    if not mesh.vertices:
        return 0
    return len(connected_components(mesh)[0])


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX))
armature = bpy.data.objects.get("Armature")
body = bpy.data.objects.get("Yuuka_Original_Body")
if armature is None or body is None:
    raise RuntimeError("Yuuka body/armature import failed")
armature.name = "PlayerHumanV9_Rig_YuukaBase"
armature.scale = tuple(component * 400.0 for component in armature.scale)
body.name = "PlayerHumanV9_DonorSurfaceBody"
for removable in ("Yuuka_Original_Calculator", "Yuuka_Original_Weapon"):
    obj = bpy.data.objects.get(removable)
    if obj:
        bpy.data.objects.remove(obj, do_unlink=True)
bpy.context.view_layer.update()

# Snapshot source topology before any deletions. Component numbering matches
# the checked audit JSON because it is ordered by minimum source vertex index.
source_components, source_vertex_component = connected_components(body.data)
source_counts = {
    "vertices": len(body.data.vertices),
    "polygons": len(body.data.polygons),
    "components": len(source_components),
}
if len(source_components) != 355:
    raise RuntimeError(f"Expected 355 Yuuka body components, found {len(source_components)}")

mat_face = textured_material("PlayerV9_Face_UnmodifiedYuukaUV", FACE_TEXTURE, 0.66)
mat_hair = brown_hair_material("PlayerV9_BrownDonorHair", HAIR_TEXTURE)
mat_eye = keyed_brown_eye_material("PlayerV9_BrownEyes_UnmodifiedTopology", EYE_TEXTURE)
mat_brow = principled_material("PlayerV9_BrownBrows", (0.040, 0.010, 0.004, 1.0), 0.58)
mat_skin = principled_material("PlayerV9_Skin", (0.82, 0.54, 0.44, 1.0), 0.68)
mat_white = principled_material("PlayerV9_HoodieWhite", (0.82, 0.86, 0.92, 1.0), 0.58)
mat_navy = principled_material("PlayerV9_Navy", (0.018, 0.052, 0.128, 1.0), 0.62)
mat_shoe_white = principled_material("PlayerV9_ShoeWhite", (0.90, 0.92, 0.96, 1.0), 0.52)
mat_red = principled_material("PlayerV9_Red", (0.72, 0.028, 0.014, 1.0), 0.48)
mat_stripe = horizontal_stripe_material(
    "PlayerV9_DonorShirtStripes",
    (0.018, 0.052, 0.128, 1.0),
    (0.96, 0.54, 0.10, 1.0),
)

body_slot = next(i for i, m in enumerate(body.data.materials) if m.name == "Yuuka_Original_Body")
face_slot = assign_original_slot(body, "Yuuka_Original_Face", mat_face)
hair_slot = assign_original_slot(body, "Yuuka_Original_Hair", mat_hair)
brow_slot = assign_original_slot(body, "Yuuka_Original_Eyebrow", mat_brow)
eye_slot = assign_original_slot(body, "Yuuka_Original_EyeMouth", mat_eye)

# Derive the only new wearable mesh from audited donor scalp c281. It is a
# single connected source component with original Head weights/modifier. No
# primitive, curve, panel, or box is generated.
scalp_component = source_components[281]
if len(scalp_component["polygons"]) != 216:
    raise RuntimeError("Yuuka c281 donor scalp signature changed")
cap = body.copy()
cap.data = body.data.copy()
cap.name = "PlayerHumanV9_Cap_FromConnectedScalpC281"
bpy.context.collection.objects.link(cap)
cap_keep = set(scalp_component["vertices"])
delete_vertices(cap, set(range(len(cap.data.vertices))) - cap_keep)
cap.data.materials.clear()
cap.data.materials.append(mat_red)
for polygon in cap.data.polygons:
    polygon.material_index = 0

# Directly reshape the retained connected shell in world space. The lower
# front quarter becomes a short, still-connected visor; all other vertices
# become the soft newsboy crown. The base overlaps donor hair by 15-25 mm.
cap_world = [cap.matrix_world @ vertex.co for vertex in cap.data.vertices]
minimum = Vector((min(p.x for p in cap_world), min(p.y for p in cap_world), min(p.z for p in cap_world)))
maximum = Vector((max(p.x for p in cap_world), max(p.y for p in cap_world), max(p.z for p in cap_world)))
center = (minimum + maximum) * 0.5
inverse = cap.matrix_world.inverted()
for vertex, point in zip(cap.data.vertices, cap_world):
    nx = (point.x - center.x) / max((maximum.x - minimum.x) * 0.5, 1.0e-8)
    ny = (point.y - center.y) / max((maximum.y - minimum.y) * 0.5, 1.0e-8)
    nz = (point.z - minimum.z) / max(maximum.z - minimum.z, 1.0e-8)
    radial = max(0.0, 1.0 - min(1.0, (nx / 1.12) ** 2 + (ny / 1.12) ** 2))
    target = Vector((0.50 * nx, 0.035 + 0.39 * ny, 3.585 + 0.18 * nz + 0.16 * math.sqrt(radial)))
    front_factor = max(0.0, min(1.0, (-ny - 0.18) / 0.82)) * max(0.0, min(1.0, (0.62 - nz) / 0.62))
    if front_factor > 0.0:
        target.y -= 0.115 * front_factor
        target.z = target.z * (1.0 - 0.72 * front_factor) + (3.59 + 0.018 * (1.0 - nx * nx)) * 0.72 * front_factor
    vertex.co = inverse @ target
cap.data.update()

# Delete only donor identity accessories and long twin-tail surfaces. Original
# face, EyeMouth, eyebrows, hands, and all selected garment surfaces are locked.
protected_components = {60, 61, 62, 63, 96, 97, 98, 99, 132, 141, 146, 157, 181, 218, 249}
protected_components.update({0, 1, 214, 221})
delete_component_ids = set()
for component in source_components:
    component_id = component["id"]
    polygons = [body.data.polygons[index] for index in component["polygons"]]
    material_ids = {polygon.material_index for polygon in polygons}
    if material_ids & {face_slot, eye_slot, brow_slot}:
        continue
    if component_id in protected_components:
        continue
    total_weight = 0.0
    skirt_weight = 0.0
    accessory_weight = 0.0
    anatomy_weight = 0.0
    hair_tail_weight = 0.0
    for polygon in polygons:
        total_weight += max(polygon_total_weight(body, polygon), 1.0e-8)
        skirt_weight += polygon_group_weight(body, polygon, lambda n: "bone_skirt" in n)
        accessory_weight += polygon_group_weight(body, polygon, lambda n: "bone_Pocket" in n or "bone_Nameplate" in n)
        anatomy_weight += polygon_group_weight(
            body,
            polygon,
            lambda n: any(token in n for token in (" Hand", " Finger", " Foot", " Toe", " Thigh", " Calf", " Forearm", " UpperArm", " Neck")),
        )
        hair_tail_weight += polygon_group_weight(body, polygon, lambda n: n.startswith("bone_hair_l_") or n.startswith("bone_hair_r_"))
    centers = [body.matrix_world @ polygon.center for polygon in polygons]
    center = sum(centers, Vector()) / max(len(centers), 1)
    if hair_slot in material_ids:
        # Keep scalp and high front/side hair; remove twin tails and long locks.
        if component_id == 281:
            continue
        tail_ratio = hair_tail_weight / max(total_weight, 1.0e-8)
        if tail_ratio > 0.08 or center.z < 2.68 or center.y > 0.28 or abs(center.x) > 0.72:
            delete_component_ids.add(component_id)
    elif body_slot in material_ids:
        skirt_ratio = skirt_weight / max(total_weight, 1.0e-8)
        accessory_ratio = accessory_weight / max(total_weight, 1.0e-8)
        anatomy_ratio = anatomy_weight / max(total_weight, 1.0e-8)
        if skirt_ratio > 0.18 or accessory_ratio > 0.05:
            delete_component_ids.add(component_id)
        elif len(polygons) < 18 and anatomy_ratio < 0.30 and 0.70 < center.z < 3.25:
            delete_component_ids.add(component_id)
        elif center.z > 3.20 and anatomy_ratio < 0.10:
            delete_component_ids.add(component_id)

delete_vertex_indices = set()
for component_id in delete_component_ids:
    delete_vertex_indices.update(source_components[component_id]["vertices"])
delete_vertices(body, delete_vertex_indices)
bpy.context.view_layer.update()

# Recompute components after deletion, then map surviving surfaces back to
# original cNNN ids through immutable source vertex positions is no longer
# possible after index compaction. Materials therefore use weights/positions;
# the protected audited components above guaranteed the core surfaces survive.
for material in (mat_skin, mat_white, mat_navy, mat_shoe_white, mat_red, mat_stripe):
    body.data.materials.append(material)
slots = {material.name: index for index, material in enumerate(body.data.materials)}


def score(polygon, tokens):
    return polygon_group_weight(body, polygon, lambda name: any(token in name for token in tokens))


for polygon in body.data.polygons:
    if polygon.material_index != body_slot:
        polygon.use_smooth = True
        continue
    world_center = body.matrix_world @ polygon.center
    values = {
        "hand": score(polygon, (" Hand", " Finger")),
        "foot": score(polygon, (" Foot", " Toe")),
        "leg": score(polygon, (" Thigh", " Calf")),
        "arm": score(polygon, (" UpperArm", " Forearm", "ForeArm Twist", "Deltoid", " Clavicle")),
        "neck": score(polygon, (" Neck",)),
        "pelvis": score(polygon, (" Pelvis",)),
        "torso": score(polygon, (" Spine",)),
    }
    dominant = max(values, key=values.get)
    if dominant in {"hand", "neck"}:
        material = mat_skin
    elif dominant == "foot":
        if world_center.z < 0.07:
            material = mat_navy
        elif world_center.y < -0.10 and world_center.z < 0.30:
            material = mat_red
        else:
            material = mat_shoe_white
    elif dominant in {"leg", "pelvis"} or world_center.z < 1.70:
        material = mat_navy
    elif dominant == "arm":
        material = mat_white
    elif world_center.y < -0.12 and abs(world_center.x) < 0.36 and 1.70 < world_center.z < 2.60:
        material = mat_stripe
    else:
        material = mat_white
    polygon.material_index = slots[material.name]
    polygon.use_smooth = True

# Retain Proof8's approved bilateral leg correction, applied only to donor
# skinned lower-body vertices. Hands and face are never included.
leg_group_indices = {
    side: {token: body.vertex_groups[f"Bip001 {side} {token}"].index for token in ("Thigh", "Calf", "Foot", "Toe0")}
    for side in ("L", "R")
}
source_x = ((0.38, 0.3535), (0.54, 0.3535), (0.82, 0.3248), (1.19, 0.2362), (1.44, 0.2966), (1.55, 0.3050))
source_y = ((0.38, -0.030), (0.54, -0.030), (0.82, -0.113), (1.19, -0.176), (1.44, -0.197), (1.55, -0.175))
source_rx = ((0.38, 0.162), (0.54, 0.162), (0.82, 0.165), (1.19, 0.225), (1.44, 0.180), (1.55, 0.160))
target_rx = ((0.38, 0.180), (0.54, 0.190), (0.82, 0.200), (1.19, 0.220), (1.44, 0.200), (1.55, 0.170))
source_ry = ((0.38, 0.210), (0.54, 0.220), (0.82, 0.220), (1.19, 0.238), (1.44, 0.255), (1.55, 0.240))
target_ry = ((0.38, 0.238), (0.54, 0.248), (0.82, 0.255), (1.19, 0.270), (1.44, 0.275), (1.55, 0.255))
body_inverse = body.matrix_world.inverted()
leg_vertex_count = 0
foot_vertex_count = 0
for vertex in body.data.vertices:
    assignments = {membership.group: membership.weight for membership in vertex.groups}
    side_values = {}
    for side in ("L", "R"):
        groups = leg_group_indices[side]
        leg_weight = assignments.get(groups["Thigh"], 0.0) + assignments.get(groups["Calf"], 0.0)
        foot_weight = assignments.get(groups["Foot"], 0.0) + assignments.get(groups["Toe0"], 0.0)
        side_values[side] = (leg_weight, foot_weight)
    side = max(side_values, key=lambda item: sum(side_values[item]))
    leg_weight, foot_weight = side_values[side]
    if max(leg_weight, foot_weight) <= 0.025:
        continue
    point = body.matrix_world @ vertex.co
    sign = 1.0 if side == "L" else -1.0
    if leg_weight >= foot_weight and point.z >= 0.34:
        sx = sign * interpolate(point.z, source_x)
        sy = interpolate(point.z, source_y)
        nx = max(-1.06, min(1.06, (point.x - sx) / interpolate(point.z, source_rx)))
        ny = max(-1.08, min(1.08, (point.y - sy) / interpolate(point.z, source_ry)))
        point.x = sign * 0.300 + nx * interpolate(point.z, target_rx)
        point.y = -0.065 + ny * interpolate(point.z, target_ry)
        leg_vertex_count += 1
    elif foot_weight > 0.025:
        point.x = sign * 0.300 + (point.x - sign * 0.3534)
        foot_vertex_count += 1
    else:
        continue
    vertex.co = body_inverse @ point
body.data.update()

# Directly relax only surviving donor torso/arm garment surfaces. This rounds
# original jacket faceting while leaving face and hand vertices exactly fixed.
garment_vertices = set()
for polygon in body.data.polygons:
    if body.data.materials[polygon.material_index] not in {mat_white, mat_stripe}:
        continue
    center = body.matrix_world @ polygon.center
    if 1.65 < center.z < 2.62:
        hand_weight = score(polygon, (" Hand", " Finger"))
        if hand_weight < 0.02:
            garment_vertices.update(polygon.vertices)
mesh_edit = bmesh.new()
mesh_edit.from_mesh(body.data)
mesh_edit.verts.ensure_lookup_table()
bmesh.ops.smooth_vert(
    mesh_edit,
    verts=[mesh_edit.verts[index] for index in sorted(garment_vertices)],
    factor=0.08,
    use_axis_x=True,
    use_axis_y=True,
    use_axis_z=False,
)
mesh_edit.to_mesh(body.data)
mesh_edit.free()
body.data.update()

# Invariant: face and hand source positions were never passed through a vertex
# transform. Face/EyeMouth topology remains present and modifier/rig retained.
if not any(modifier.type == "ARMATURE" and modifier.object == armature for modifier in body.modifiers):
    raise RuntimeError("Armature modifier was not retained")
if len(armature.data.bones) != 118:
    raise RuntimeError("Yuuka 118-bone rig signature changed")
if component_count(cap.data) != 1:
    raise RuntimeError("Donor-derived cap must remain one connected component")

# Review scene (non-character helpers only).
scene = bpy.context.scene
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 1600
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.render.use_freestyle = True
scene.render.line_thickness = 0.62
scene.world = bpy.data.worlds.new("PlayerV9_ReviewWorld")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.048, 0.068, 0.100, 1.0)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.42
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.20
except TypeError:
    pass


def add_area(name, location, energy, color, size):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.color = color
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    bpy.context.collection.objects.link(light)
    light.location = location
    light.rotation_euler = (Vector((0.0, 0.0, 2.15)) - light.location).to_track_quat("-Z", "Y").to_euler()


add_area("PlayerV9_Key", (-4.2, -5.3, 6.5), 1120.0, (1.0, 0.82, 0.70), 4.8)
add_area("PlayerV9_Fill", (4.5, -3.8, 4.8), 820.0, (0.68, 0.83, 1.0), 4.2)
add_area("PlayerV9_Rim", (0.0, 4.5, 5.8), 960.0, (1.0, 0.72, 0.58), 4.0)

bpy.ops.mesh.primitive_plane_add(size=30.0, location=(0.0, 0.0, -0.018))
ground = bpy.context.object
ground.name = "PlayerV9_ReviewGround_NotCharacter"
ground.data.materials.append(principled_material("PlayerV9_Ground", (0.075, 0.095, 0.135, 1.0), 0.78))

camera_data = bpy.data.cameras.new("PlayerV9_ProofCamera")
camera = bpy.data.objects.new("PlayerV9_ProofCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 4.72
scene.camera = camera


def point_camera(location, target=(0.0, 0.0, 2.08)):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


views = {
    "front": (0.0, -8.4, 2.12),
    "three-quarter": (5.9, -5.9, 2.15),
    "side": (8.4, 0.0, 2.12),
    "back": (0.0, 8.4, 2.12),
}
rendered = []
for view, location in views.items():
    point_camera(location)
    path = OUTPUT / f"player-human-v9-{view}-proof9.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    rendered.append(path)

# Wire/seam proof: duplicate existing final surfaces only and render their real
# topology as thin black wires. QA duplicates are removed before saving blend.
wire_material = principled_material("PlayerV9_QA_Wire", (0.004, 0.004, 0.006, 1.0), 0.85)
wire_objects = []
for source in (body, cap):
    wire = source.copy()
    wire.data = source.data.copy()
    wire.name = f"QA_Wire_{source.name}"
    bpy.context.collection.objects.link(wire)
    wire.data.materials.clear()
    wire.data.materials.append(wire_material)
    for polygon in wire.data.polygons:
        polygon.material_index = 0
    modifier = wire.modifiers.new("QA_RealTopologyWire", "WIREFRAME")
    modifier.thickness = 0.0035
    modifier.use_replace = True
    modifier.use_even_offset = True
    wire_objects.append(wire)

for view, location in {"front": views["front"], "three-quarter": views["three-quarter"], "side": views["side"]}.items():
    point_camera(location)
    path = OUTPUT / f"player-human-v9-{view}-wire-seam-proof9.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    rendered.append(path)
for wire in wire_objects:
    bpy.data.objects.remove(wire, do_unlink=True)
bpy.data.materials.remove(wire_material)

blend_path = OUTPUT / "player-human-v9-proof9.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

face_polygons = sum(1 for polygon in body.data.polygons if body.data.materials[polygon.material_index] == mat_face)
eye_polygons = sum(1 for polygon in body.data.polygons if body.data.materials[polygon.material_index] == mat_eye)
receipt = {
    "schema": "family-company.player-human-proof.v9",
    "status": "DIAGNOSTIC_ONLY_REJECTED_NOT_USER_CANDIDATE",
    "source": {
        "fbx": str(SOURCE_FBX.relative_to(REPO)).replace("\\", "/"),
        "sha256": sha256(SOURCE_FBX),
        "userAttestation": "test2/Yuuka is user-owned for this private prototype",
        "test3Sakurako": "EXCLUDED_NOT_OPENED",
    },
    "policy": {
        "proceduralCharacterPrimitives": 0,
        "floatingProceduralCharacterParts": 0,
        "generatedPanelsBoxesCurvesLofts": 0,
        "characterMeshObjects": [body.name, cap.name],
        "capSource": "audited Yuuka_Original_Body/c281 duplicate; one connected donor shell",
        "capConnectedComponents": component_count(cap.data),
        "reviewHelpersExcludedFromCharacter": [ground.name, camera.name, "three area lights"],
    },
    "retained": {
        "rigBones": len(armature.data.bones),
        "armatureModifier": True,
        "sourceBodyCounts": source_counts,
        "finalBodyVertices": len(body.data.vertices),
        "finalBodyPolygons": len(body.data.polygons),
        "facePolygons": face_polygons,
        "eyeMouthPolygons": eye_polygons,
        "faceGeometryEdited": False,
        "handGeometryEdited": False,
        "originalAPoseRetained": True,
    },
    "directDonorEdits": {
        "hoodie": ["c146 torso", "c141/c181 mirrored sleeve shells", "solid recolor + XY relax only"],
        "shirt": ["c157 central torso shell", "generated-coordinate stripe shader; no overlay surface"],
        "pants": ["c063/c099 leg shells", "c218 pelvis bridge", "approved bilateral rest-space reshape"],
        "shoes": ["c000/c001 foot shells", "c214/c221 sole/toe shells", "material segmentation only"],
        "removedSourceComponents": len(delete_component_ids),
        "removedSourceVertices": len(delete_vertex_indices),
        "legVerticesEdited": leg_vertex_count,
        "footVerticesEdited": foot_vertex_count,
    },
    "proofs": [str(path.relative_to(REPO)).replace("\\", "/") for path in rendered],
    "blend": str(blend_path.relative_to(REPO)).replace("\\", "/"),
}
receipt_path = OUTPUT / "player-human-v9-proof9-receipt.json"
receipt_path.write_text(json.dumps(receipt, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(json.dumps(receipt, indent=2, ensure_ascii=False))
