"""Build Older Sister Proof11 from the untouched user-owned Yuuka surface.

The authored body/hair/face topology, UVs, weights, 118-bone rig and native
three-digit hands remain coordinate-identical.  Only whole non-body objects
(weapon/calculator), materials, and per-component material roles change.
Static internal QA only; no GIF, Unity import or production claim.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from collections import defaultdict, deque
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--texture-dir", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument(
        "--style",
        choices=("full", "clean", "casual", "casual_polished", "hybrid", "minimal", "player_original15"),
        default="full",
    )
    return parser.parse_args(argv)


ARGS = parse_args()
SOURCE = Path(ARGS.input).resolve()
TEXTURES = Path(ARGS.texture_dir).resolve()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
IS_PLAYER15 = ARGS.style == "player_original15"
FILE_PREFIX = (
    "player-original-surface15-polished-gate"
    if IS_PLAYER15
    else f"sister-proof11-{ARGS.style}-original-surface-style"
)


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


def load_image(name, colorspace="sRGB"):
    image = bpy.data.images.load(str(TEXTURES / name), check_existing=False)
    image.colorspace_settings.name = colorspace
    return image


def set_principled_defaults(node, roughness=0.66, specular=0.10):
    node.inputs["Roughness"].default_value = roughness
    if "Specular IOR Level" in node.inputs:
        node.inputs["Specular IOR Level"].default_value = specular


def faithful_material(name, image, mask=None, roughness=0.66):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    set_principled_defaults(principled, roughness)
    color = nodes.new("ShaderNodeTexImage")
    color.image = image
    color.interpolation = "Linear"
    links.new(color.outputs["Color"], principled.inputs["Base Color"])
    if mask is not None:
        mask_node = nodes.new("ShaderNodeTexImage")
        mask_node.image = mask
        mask_node.interpolation = "Linear"
        separate = nodes.new("ShaderNodeSeparateColor")
        rough = nodes.new("ShaderNodeMapRange")
        rough.inputs["From Min"].default_value = 0.0
        rough.inputs["From Max"].default_value = 1.0
        rough.inputs["To Min"].default_value = 0.80
        rough.inputs["To Max"].default_value = 0.48
        links.new(mask_node.outputs["Color"], separate.inputs["Color"])
        links.new(separate.outputs["Green"], rough.inputs["Value"])
        links.new(rough.outputs["Result"], principled.inputs["Roughness"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def palette_material(name, image, mask, dark, mid, light, roughness=0.70):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    set_principled_defaults(principled, roughness, 0.07)
    color = nodes.new("ShaderNodeTexImage")
    color.image = image
    color.interpolation = "Linear"
    luminance = nodes.new("ShaderNodeRGBToBW")
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
    low = ramp.color_ramp.elements[0]
    low.position = 0.06
    low.color = (*dark, 1.0)
    middle = ramp.color_ramp.elements.new(0.54)
    middle.color = (*mid, 1.0)
    high = ramp.color_ramp.elements.new(0.94)
    high.color = (*light, 1.0)
    links.new(color.outputs["Color"], luminance.inputs["Color"])
    links.new(luminance.outputs["Val"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], principled.inputs["Base Color"])
    if mask is not None:
        mask_node = nodes.new("ShaderNodeTexImage")
        mask_node.image = mask
        mask_node.interpolation = "Linear"
        separate = nodes.new("ShaderNodeSeparateColor")
        rough = nodes.new("ShaderNodeMapRange")
        rough.inputs["From Min"].default_value = 0.0
        rough.inputs["From Max"].default_value = 1.0
        rough.inputs["To Min"].default_value = 0.82
        rough.inputs["To Max"].default_value = 0.50
        links.new(mask_node.outputs["Color"], separate.inputs["Color"])
        links.new(separate.outputs["Green"], rough.inputs["Value"])
        links.new(rough.outputs["Result"], principled.inputs["Roughness"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (*mid, 1.0)
    return material


def build_alpha_image(source, path):
    width, height = source.size
    pixels = list(source.pixels[:])
    result = [0.0] * len(pixels)
    for offset in range(0, len(pixels), 4):
        red, green, blue = pixels[offset : offset + 3]
        maximum = max(red, green, blue)
        alpha = max(0.0, min(1.0, (maximum - 0.018) / 0.080))
        result[offset + 0] = red
        result[offset + 1] = green
        result[offset + 2] = blue
        result[offset + 3] = alpha
    image = bpy.data.images.new("SisterProof11EyeMouthAlpha", width=width, height=height, alpha=True)
    image.pixels.foreach_set(result)
    image.file_format = "PNG"
    image.filepath_raw = str(path)
    image.save()
    return image


def eye_material(name, image, hue_value=0.30, saturation=1.06, value=1.02):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    set_principled_defaults(principled, 0.52, 0.14)
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Linear"
    hue = nodes.new("ShaderNodeHueSaturation")
    hue.inputs["Hue"].default_value = hue_value
    hue.inputs["Saturation"].default_value = saturation
    hue.inputs["Value"].default_value = value
    links.new(texture.outputs["Color"], hue.inputs["Color"])
    links.new(hue.outputs["Color"], principled.inputs["Base Color"])
    mix = nodes.new("ShaderNodeMixShader")
    links.new(texture.outputs["Alpha"], mix.inputs[0])
    links.new(transparent.outputs["BSDF"], mix.inputs[1])
    links.new(principled.outputs["BSDF"], mix.inputs[2])
    links.new(mix.outputs["Shader"], output.inputs["Surface"])
    if hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"
    return material


def solid_material(name, color, roughness=0.76):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    set_principled_defaults(principled, roughness, 0.05)
    material.diffuse_color = (*color, 1.0)
    return material


def transparent_material(name):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    links.new(transparent.outputs["BSDF"], output.inputs["Surface"])
    if hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"
    material.diffuse_color = (0.0, 0.0, 0.0, 0.0)
    return material


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
    roots = defaultdict(set)
    for vertex in mesh.vertices:
        roots[find(vertex.index)].add(vertex.index)
    ordered = sorted(roots, key=lambda root: min(roots[root]))
    by_vertex = {}
    for component_id, root in enumerate(ordered):
        for vertex_index in roots[root]:
            by_vertex[vertex_index] = component_id
    by_polygon = {}
    for polygon in mesh.polygons:
        component_ids = {by_vertex[index] for index in polygon.vertices}
        if len(component_ids) != 1:
            raise RuntimeError("Polygon crosses connected-component roots")
        by_polygon[polygon.index] = component_ids.pop()
    return by_polygon, len(ordered)


def boundary_edge_count(mesh):
    uses = defaultdict(int)
    for polygon in mesh.polygons:
        for edge_key in polygon.edge_keys:
            uses[tuple(sorted(edge_key))] += 1
    return sum(1 for count in uses.values() if count != 2)


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(SOURCE), use_anim=False)

armature = bpy.data.objects.get("Armature")
body = bpy.data.objects.get("Yuuka_Original_Body")
if armature is None or body is None or len(armature.data.bones) != 118:
    raise RuntimeError("Expected original Yuuka body and exact 118-bone rig")

for name in ("Yuuka_Original_Weapon", "Yuuka_Original_Calculator", "Cube", "Camera", "Light"):
    obj = bpy.data.objects.get(name)
    if obj is not None:
        bpy.data.objects.remove(obj, do_unlink=True)

coordinate_before = coordinate_hash(body)
bone_names_before = sorted(bone.name for bone in armature.data.bones)
original_material_indices = [polygon.material_index for polygon in body.data.polygons]
original_coordinates = [tuple(vertex.co) for vertex in body.data.vertices]
original_vertex_groups = {
    vertex.index: sorted((body.vertex_groups[item.group].name, round(float(item.weight), 8)) for item in vertex.groups)
    for vertex in body.data.vertices
}

body_image = load_image("Yuuka_Original_Body.png")
body_mask = load_image("Yuuka_Original_Body_Mask.png", "Non-Color")
hair_image = load_image("Yuuka_Original_Hair.png")
hair_mask = load_image("Yuuka_Original_Hair_Mask.png", "Non-Color")
face_image = load_image("Yuuka_Original_Face.png")
eye_source = load_image("Yuuka_Original_EyeMouth.png")
eye_alpha_path = OUTPUT / f"{FILE_PREFIX}-eyemouth-alpha.png"
eye_alpha = build_alpha_image(eye_source, eye_alpha_path)

MAT_BODY_CHARCOAL = palette_material(
    "SisterProof11CharcoalTop_SourceUV", body_image, body_mask,
    (0.004, 0.004, 0.008), (0.030, 0.032, 0.047), (0.16, 0.17, 0.21), 0.72,
)
MAT_NAVY = palette_material(
    "SisterProof11NavyLower_SourceUV", body_image, body_mask,
    (0.004, 0.018, 0.050), (0.020, 0.090, 0.22), (0.18, 0.40, 0.69), 0.74,
)
MAT_SKIN = palette_material(
    "SisterProof11Skin_SourceUV", body_image, body_mask,
    (0.58, 0.31, 0.25), (0.86, 0.62, 0.54), (1.00, 0.89, 0.84), 0.76,
)
MAT_SHOE = palette_material(
    "SisterProof11DarkShoe_SourceUV", body_image, body_mask,
    (0.002, 0.003, 0.006), (0.018, 0.024, 0.040), (0.12, 0.16, 0.23), 0.62,
)
MAT_ACCENT = palette_material(
    "SisterProof11PaleAccent_SourceUV", body_image, body_mask,
    (0.12, 0.16, 0.22), (0.65, 0.75, 0.84), (0.98, 0.99, 1.00), 0.70,
)
MAT_HAIR = palette_material(
    "SisterProof11CharcoalHair_SourceUV", hair_image, hair_mask,
    (0.001, 0.001, 0.003), (0.014, 0.012, 0.020), (0.11, 0.09, 0.13), 0.62,
)
MAT_FACE = faithful_material("SisterProof11OriginalFace", face_image, None, 0.73)
MAT_BROW = palette_material(
    "SisterProof11DarkBrow_SourceUV", face_image, None,
    (0.006, 0.003, 0.004), (0.040, 0.016, 0.018), (0.18, 0.07, 0.06), 0.72,
)
MAT_EYE = eye_material("SisterProof11TealEyeMouth_SourceUV", eye_alpha)
MAT_GRAY = solid_material("SisterProof11QAGray", (0.56, 0.59, 0.64), 0.84)
MAT_HIDDEN = transparent_material("SisterProof11WholeComponentHidden")
MAT_MOUTH = solid_material("SisterProof11SoftRoseMouth", (0.42, 0.055, 0.060), 0.78)
MAT_SKIN_FLAT = solid_material("SisterProof13SmoothSkin", (0.76, 0.56, 0.50), 0.76)
MAT_SHORTS_FLAT = solid_material("SisterProof13SolidNavyShorts", (0.012, 0.055, 0.16), 0.75)
MAT_SHORTS_GUSSET = solid_material("SisterProof13LitNavyGusset", (0.026, 0.105, 0.29), 0.78)
MAT_PIPING_FLAT = solid_material("SisterProof13SolidPalePiping", (0.72, 0.82, 0.91), 0.72)
MAT_NECKLINE_INLAY = solid_material("SisterProof20IntegratedNeckline", (0.006, 0.007, 0.012), 0.78)

# Player OriginalSurface15 uses only the retained donor surfaces and material
# routing.  Source luminance remains on hair/eyes/skin for authored polish;
# clothing becomes deliberately quiet solids so donor technology graphics do
# not masquerade as floating outfit parts.
MAT_PLAYER_HAIR = palette_material(
    "PlayerOriginal15DeepBrownHair_SourceUV", hair_image, hair_mask,
    (0.008, 0.002, 0.001), (0.055, 0.014, 0.006), (0.24, 0.085, 0.035), 0.58,
)
MAT_PLAYER_BROW = solid_material("PlayerOriginal15SoftBrownBrow", (0.050, 0.014, 0.008), 0.66)
MAT_PLAYER_EYE = eye_material(
    "PlayerOriginal15BrownEye_OriginalHighlights_LowSaturationLash",
    eye_alpha,
    hue_value=0.86,
    saturation=0.72,
    value=1.02,
)
MAT_PLAYER_HOODIE = solid_material("PlayerOriginal15HoodieOffWhite", (0.87, 0.90, 0.94), 0.78)
MAT_PLAYER_PANTS = solid_material("PlayerOriginal15PantsNavy", (0.018, 0.050, 0.125), 0.76)
MAT_PLAYER_SHOE = solid_material("PlayerOriginal15ShoesWarmWhite", (0.78, 0.82, 0.88), 0.69)
MAT_PLAYER_SKIN = MAT_SKIN
MAT_PLAYER_CAP = solid_material("PlayerOriginal15CapMutedRed", (0.64, 0.030, 0.018), 0.70)
# The original closed face shell must remain a two-sided rear occluder.  Only
# the front-authored EyeMouth decal is culled from behind.
MAT_FACE.use_backface_culling = False
MAT_PLAYER_EYE.use_backface_culling = True

slot_by_name = {material.name: index for index, material in enumerate(body.data.materials) if material}
required_slots = {
    "Yuuka_Original_Body", "Yuuka_Original_Face", "Yuuka_Original_Hair",
    "Yuuka_Original_Eyebrow", "Yuuka_Original_EyeMouth",
}
if not required_slots.issubset(slot_by_name):
    raise RuntimeError(f"Unexpected original material slots: {slot_by_name}")

body.data.materials[slot_by_name["Yuuka_Original_Body"]] = MAT_PLAYER_HOODIE if IS_PLAYER15 else MAT_BODY_CHARCOAL
body.data.materials[slot_by_name["Yuuka_Original_Face"]] = MAT_FACE
body.data.materials[slot_by_name["Yuuka_Original_Hair"]] = MAT_PLAYER_HAIR if IS_PLAYER15 else MAT_HAIR
body.data.materials[slot_by_name["Yuuka_Original_Eyebrow"]] = MAT_PLAYER_BROW if IS_PLAYER15 else MAT_BROW
body.data.materials[slot_by_name["Yuuka_Original_EyeMouth"]] = MAT_PLAYER_EYE if IS_PLAYER15 else MAT_EYE

role_materials = {
    "charcoal": MAT_BODY_CHARCOAL,
    "navy": MAT_NAVY,
    "shorts_flat": MAT_SHORTS_FLAT,
    "shorts_gusset": MAT_SHORTS_GUSSET,
    "skin": MAT_SKIN,
    "skin_flat": MAT_SKIN_FLAT,
    "shoe": MAT_SHOE,
    "accent": MAT_ACCENT,
    "player_hoodie": MAT_PLAYER_HOODIE,
    "player_pants": MAT_PLAYER_PANTS,
    "player_shoe": MAT_PLAYER_SHOE,
    "player_skin": MAT_PLAYER_SKIN,
    "hidden": MAT_HIDDEN,
}
role_slots = {}
for role, material in role_materials.items():
    role_slots[role] = len(body.data.materials)
    body.data.materials.append(material)

component_for_polygon, component_count = connected_components(body.data)
if component_count != 355:
    raise RuntimeError(f"Expected exact 355 Yuuka body components, got {component_count}")

body_slot_original = slot_by_name["Yuuka_Original_Body"]
component_vertices = defaultdict(set)
component_polygons = defaultdict(list)
for polygon in body.data.polygons:
    component_id = component_for_polygon[polygon.index]
    component_vertices[component_id].update(polygon.vertices)
    component_polygons[component_id].append(polygon.index)

explicit_skin = {60, 61, 62, 63, 96, 97, 98, 99}
explicit_shoe = {0, 1, 65, 66, 67, 74, 75, 76, 101, 102, 103, 110, 111, 112, 214, 215, 221, 222}
explicit_navy = {57, 58, 59, 79, 115, 142, 143, 146, 151, 209, 217, 218}
explicit_accent = {132, 159, 160, 170, 208, 225, 226}

minimal_keep = {0, 1, 60, 61, 62, 63, 96, 97, 98, 99, 141, 157, 181, 218}
hybrid_keep = minimal_keep | {132, 140, 160, 170, 180, 208, 217}
casual_keep = {
    0, 1,
    60, 61, 62, 63, 65, 66, 67, 74, 75, 76,
    96, 97, 98, 99, 101, 102, 103, 110, 111, 112,
    141, 157, 181,
    214, 215, 217, 218, 221, 222,
}
casual_polished_keep = {
    0, 1,
    60, 61, 62, 74, 75, 76,
    96, 97, 98, 110, 111, 112,
    157, 214, 215, 218, 221, 222,
}
player15_hands = {60, 61, 62, 96, 97, 98}
player15_hoodie = {141, 157, 181}
player15_pants = {63, 64, 65, 66, 99, 100, 101, 102, 217, 218}
player15_shoes = {
    0, 1, 67, 70, 71, 72, 73, 74, 75, 76, 77, 78,
    103, 106, 107, 108, 109, 110, 111, 112, 113, 114,
    214, 215, 221, 222,
}
player15_keep = player15_hands | player15_hoodie | player15_pants | player15_shoes
style_keep = None if ARGS.style == "full" else (hybrid_keep if ARGS.style == "hybrid" else minimal_keep)
if ARGS.style == "clean":
    style_keep = None
elif ARGS.style == "casual":
    style_keep = casual_keep
elif ARGS.style == "casual_polished":
    style_keep = casual_polished_keep
elif IS_PLAYER15:
    style_keep = player15_keep
clean_hide = {
    2, 3, 52, 53, 54, 55, 56, 57, 58, 59, 79, 115,
    133, 134, 135, 136, 137, 138, 139,
    211, 224, 225, 226, 229, 230, 231,
}
transparent_polygons = set()

component_receipt = []
role_counts = defaultdict(int)
for component_id, polygon_indices in sorted(component_polygons.items()):
    if original_material_indices[polygon_indices[0]] != body_slot_original:
        continue
    vertices = component_vertices[component_id]
    bone_weight = defaultdict(float)
    for vertex_index in vertices:
        for group_name, weight in original_vertex_groups[vertex_index]:
            bone_weight[group_name.lower()] += weight
    total = max(sum(bone_weight.values()), 1.0e-9)
    skirt_fraction = sum(value for name, value in bone_weight.items() if "skirt" in name) / total
    hand_fraction = sum(value for name, value in bone_weight.items() if " hand" in name or "finger" in name) / total
    leg_fraction = sum(value for name, value in bone_weight.items() if any(token in name for token in ("thigh", "calf", " foot", " toe"))) / total
    points = [body.matrix_world @ body.data.vertices[index].co for index in vertices]
    center_z = sum(point.z for point in points) / max(len(points), 1)

    role = "charcoal"
    reason = "authored outer outfit retained as coherent charcoal surface"
    if IS_PLAYER15 and component_id not in player15_keep:
        role, reason = "hidden", "whole donor technology/outer-skirt/accessory component excluded from Player OriginalSurface15"
    elif IS_PLAYER15 and component_id in player15_hands:
        role, reason = "player_skin", "exact original three-digit hand surface; coordinates and weights untouched"
    elif IS_PLAYER15 and component_id in player15_hoodie:
        role, reason = "player_hoodie", "simplest body-fitted original torso/sleeve surface routed to off-white hoodie palette"
    elif IS_PLAYER15 and component_id in player15_pants:
        role, reason = "player_pants", "original pelvis-to-calf surface routed to continuous navy lower palette"
    elif IS_PLAYER15 and component_id in player15_shoes:
        role, reason = "player_shoe", "original fitted shoe/sole surface routed to quiet warm-white palette"
    elif ARGS.style == "clean" and component_id in clean_hide:
        role, reason = "hidden", "whole disconnected technology/nameplate/pouch component hidden by clean routing"
    elif style_keep is not None and component_id not in style_keep:
        role, reason = "hidden", f"whole original component hidden by {ARGS.style} casual routing"
    elif ARGS.style == "casual_polished" and component_id in {
        60, 61, 62,
        96, 97, 98,
    }:
        role, reason = "skin_flat", "native limb/three-digit hand surface with uniform skin material"
    elif ARGS.style == "casual_polished" and component_id == 217:
        role, reason = "shorts_flat", "original pelvis surface unified to the athletic-short material"
    elif ARGS.style == "casual_polished" and component_id == 218:
        role, reason = "shorts_flat", "compressed original pelvis gusset unified to the athletic-short material"
    elif component_id in explicit_skin or hand_fraction > 0.52 or (
        ARGS.style in {"casual", "hybrid", "minimal"} and component_id in {141, 181}
    ):
        role, reason = "skin", "original authored bare limb/hand surface"
    elif component_id in explicit_shoe:
        role, reason = "shoe", "original authored foot/shoe surface"
    elif component_id in explicit_navy or skirt_fraction > 0.08:
        role, reason = "navy", "original authored skirt/lower surface"
    elif component_id in explicit_accent:
        role, reason = "accent", "small original connected collar/trim surface"
    elif leg_fraction > 0.56:
        role, reason = "skin", "original authored leg surface"

    for polygon_index in polygon_indices:
        if ARGS.style in {"casual", "hybrid", "minimal"} and component_id in {63, 99}:
            polygon = body.data.polygons[polygon_index]
            center_world = body.matrix_world @ polygon.center
            polygon.material_index = role_slots["navy" if center_world.z >= 0.00334 else "skin"]
        elif ARGS.style == "casual_polished" and component_id == 217:
            body.data.polygons[polygon_index].material_index = role_slots["shorts_flat"]
        elif ARGS.style == "casual_polished" and component_id == 218:
            body.data.polygons[polygon_index].material_index = role_slots["shorts_flat"]
        elif ARGS.style in {"casual", "hybrid", "minimal"} and component_id in {217, 218}:
            body.data.polygons[polygon_index].material_index = role_slots["navy"]
        else:
            body.data.polygons[polygon_index].material_index = role_slots[role]
        if role == "hidden":
            transparent_polygons.add(polygon_index)
        body.data.polygons[polygon_index].use_smooth = True
    role_counts[role] += len(polygon_indices)
    component_receipt.append({
        "component": component_id,
        "polygons": len(polygon_indices),
        "role": role,
        "reason": reason,
        "centerZ": round(float(center_z), 8),
        "skirtFraction": round(float(skirt_fraction), 5),
        "handFraction": round(float(hand_fraction), 5),
        "legFraction": round(float(leg_fraction), 5),
    })

# The disconnected 32-polygon EyeMouth component is the opaque atlas backdrop,
# not a usable mouth.  Hide that whole authored island without cutting topology.
for polygon_index in component_polygons.get(346, []):
    body.data.polygons[polygon_index].material_index = role_slots["hidden"]
    transparent_polygons.add(polygon_index)

# The Player baseline keeps only the authored short-hair envelope: scalp,
# nape/socket cover, front/middle bangs and the two side-bang islands.  Twin
# tails, long rear locks, bows and tech clips are removed as whole disconnected
# components.  No retained hair coordinate is moved.
player15_hair_keep = {281, 282, 329, 330, 337, 338, 339, 343}
player15_hidden_hair_components = []
if IS_PLAYER15:
    hair_slot_original = slot_by_name["Yuuka_Original_Hair"]
    for component_id, polygon_indices in sorted(component_polygons.items()):
        if original_material_indices[polygon_indices[0]] != hair_slot_original:
            continue
        if component_id in player15_hair_keep:
            for polygon_index in polygon_indices:
                body.data.polygons[polygon_index].use_smooth = True
            continue
        player15_hidden_hair_components.append(component_id)
        for polygon_index in polygon_indices:
            body.data.polygons[polygon_index].material_index = role_slots["hidden"]
            transparent_polygons.add(polygon_index)


def reshape_casual_arm(component_id, sign):
    """Taper the retained donor arm envelope without moving either joint axis."""
    # FBX geometry is authored in centimetre-like local coordinates below an
    # armature scaled to 0.01.  Work in those local coordinates so the taper is
    # centred on the actual shoulder/wrist line rather than the world-scale line.
    shoulder = Vector((0.108 * sign, -0.002, 0.585))
    wrist = Vector((0.270 * sign, -0.002, 0.442))
    axis = wrist - shoulder
    axis_length_sq = axis.length_squared
    for vertex_index in component_vertices[component_id]:
        vertex = body.data.vertices[vertex_index]
        point = Vector(vertex.co)
        t = max(0.0, min(1.0, (point - shoulder).dot(axis) / axis_length_sq))
        centerline = shoulder.lerp(wrist, t)
        radial = point - centerline
        smooth_t = t * t * (3.0 - 2.0 * t)
        radial_scale = 0.68 - 0.18 * smooth_t
        radial.y *= radial_scale * 0.90
        radial.x *= radial_scale
        radial.z *= radial_scale
        vertex.co = centerline + radial


allowed_coordinate_changes = set()
if ARGS.style == "casual":
    reshape_casual_arm(141, 1.0)
    reshape_casual_arm(181, -1.0)
    allowed_coordinate_changes.update(component_vertices[141])
    allowed_coordinate_changes.update(component_vertices[181])
    body.data.update()
elif ARGS.style == "casual_polished":
    # Compress the donor gusset fully beneath the overlapping short shells.  It
    # closes the centre but no top corner or underwear point may break the
    # athletic-short silhouette in front, rear, or oblique views.
    component_218_min_z = min(body.data.vertices[index].co.z for index in component_vertices[218])
    component_218_max_z = max(body.data.vertices[index].co.z for index in component_vertices[218])
    component_218_span_z = max(component_218_max_z - component_218_min_z, 1.0e-9)
    for vertex_index in component_vertices[218]:
        vertex = body.data.vertices[vertex_index]
        normalized = (vertex.co.z - component_218_min_z) / component_218_span_z
        vertex.co.x *= 0.72
        vertex.co.z = 0.363 + 0.041 * normalized
    allowed_coordinate_changes.update(component_vertices[218])
    body.data.update()


generated_surface_objects = []


def copy_body_transform(obj):
    """Place authored-local geometry in the same armature space as the donor body."""
    obj.parent = body.parent
    obj.matrix_parent_inverse = body.matrix_parent_inverse.copy()
    obj.matrix_basis = body.matrix_basis.copy()


def make_polished_arm(name, sign):
    """Create a smooth bare arm between the untouched shoulder and native hand."""
    # Keep the shoulder ring outside the tank's scoop-neck aperture.  The
    # earlier, more medial centreline let the arm tube peek through the chest
    # opening in three-quarter views even though it looked fine head-on.
    shoulder = Vector((0.135 * sign, 0.020, 0.585))
    control = Vector((0.198 * sign, 0.006, 0.525))
    wrist = Vector((0.260 * sign, -0.004, 0.443))
    axis_y = Vector((0.0, 1.0, 0.0))
    ring_count = 15
    radial_segments = 32
    vertices = []
    faces = []

    for ring in range(ring_count):
        t = ring / (ring_count - 1)
        one_minus = 1.0 - t
        centerline = shoulder * (one_minus * one_minus) + control * (2.0 * one_minus * t) + wrist * (t * t)
        tangent = (control - shoulder) * (2.0 * one_minus) + (wrist - control) * (2.0 * t)
        tangent.normalize()
        plane_axis = tangent.cross(axis_y).normalized()
        smooth_t = t * t * (3.0 - 2.0 * t)
        radius = 0.0355 * (1.0 - smooth_t) + 0.0255 * smooth_t
        elbow_relief = 1.0 - 0.085 * math.exp(-((t - 0.58) / 0.16) ** 2)
        radius *= elbow_relief
        for segment in range(radial_segments):
            angle = 2.0 * math.pi * segment / radial_segments
            radial = axis_y * (math.cos(angle) * radius * 0.90)
            radial += plane_axis * (math.sin(angle) * radius)
            vertices.append(tuple(centerline + radial))

    for ring in range(ring_count - 1):
        for segment in range(radial_segments):
            next_segment = (segment + 1) % radial_segments
            a = ring * radial_segments + segment
            b = ring * radial_segments + next_segment
            c = (ring + 1) * radial_segments + next_segment
            d = (ring + 1) * radial_segments + segment
            faces.append((a, b, c, d))
    # Close both tube ends so the shoulder cannot expose a hollow/back-facing
    # wedge through the tank armhole at three-quarter camera angles.
    faces.append(tuple(reversed(range(radial_segments))))
    last_ring_start = (ring_count - 1) * radial_segments
    faces.append(tuple(last_ring_start + segment for segment in range(radial_segments)))

    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    copy_body_transform(obj)
    obj.data.materials.append(MAT_SKIN_FLAT)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True

    side = "L" if sign > 0 else "R"
    groups = {
        "upper": obj.vertex_groups.new(name=f"Bip001 {side} UpperArm"),
        "forearm": obj.vertex_groups.new(name=f"Bip001 {side} Forearm"),
        "hand": obj.vertex_groups.new(name=f"Bip001 {side} Hand"),
    }
    for ring in range(ring_count):
        t = ring / (ring_count - 1)
        indices = list(range(ring * radial_segments, (ring + 1) * radial_segments))
        if t <= 0.42:
            weights = (1.0, 0.0, 0.0)
        elif t <= 0.72:
            blend = (t - 0.42) / 0.30
            weights = (1.0 - blend, blend, 0.0)
        elif t <= 0.90:
            weights = (0.0, 1.0, 0.0)
        else:
            blend = (t - 0.90) / 0.10
            weights = (0.0, 1.0 - blend, blend)
        for key, weight in zip(("upper", "forearm", "hand"), weights):
            if weight > 0.0:
                groups[key].add(indices, weight, "REPLACE")

    modifier = obj.modifiers.new("OwnedYuukaArmature", "ARMATURE")
    modifier.object = armature
    obj["surfaceMethod"] = "smooth authored-scale arm bridge"
    obj["nativeHandReplacement"] = False
    generated_surface_objects.append(obj)
    return obj


def make_polished_short_leg(name, sign):
    """Create one smooth athletic-short shell; the original pelvis surface closes the crotch."""
    profiles = (
        (0.416, 0.053, 0.064, 0.073),
        (0.409, 0.055, 0.069, 0.076),
        (0.399, 0.057, 0.072, 0.079),
        (0.378, 0.060, 0.071, 0.078),
        (0.365, 0.062, 0.068, 0.075),
        (0.357, 0.063, 0.066, 0.073),
    )
    radial_segments = 48
    center_y = -0.043
    vertices = []
    faces = []
    face_materials = []
    for ring, (z, center_x, radius_x, radius_y) in enumerate(profiles):
        for segment in range(radial_segments):
            angle = 2.0 * math.pi * segment / radial_segments
            x = sign * (center_x + radius_x * math.cos(angle))
            y = center_y + radius_y * math.sin(angle)
            # A raised dolphin hem read as a thin detached spike at oblique
            # angles.  Keep the hem continuous and let the white piping carry
            # the athletic-short silhouette instead.
            vertices.append((x, y, z))
    for ring in range(len(profiles) - 1):
        for segment in range(radial_segments):
            next_segment = (segment + 1) % radial_segments
            a = ring * radial_segments + segment
            b = ring * radial_segments + next_segment
            c = (ring + 1) * radial_segments + next_segment
            d = (ring + 1) * radial_segments + segment
            faces.append((a, b, c, d))
            face_materials.append(1 if ring >= len(profiles) - 2 else 0)

    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    copy_body_transform(obj)
    obj.data.materials.append(MAT_SHORTS_FLAT)
    obj.data.materials.append(MAT_PIPING_FLAT)
    for polygon, material_index in zip(obj.data.polygons, face_materials):
        polygon.material_index = material_index
        polygon.use_smooth = True

    side = "L" if sign > 0 else "R"
    pelvis_group = obj.vertex_groups.new(name="Bip001 Pelvis")
    thigh_group = obj.vertex_groups.new(name=f"Bip001 {side} Thigh")
    for ring in range(len(profiles)):
        t = ring / (len(profiles) - 1)
        pelvis_weight = 0.88 * (1.0 - t) + 0.28 * t
        indices = list(range(ring * radial_segments, (ring + 1) * radial_segments))
        pelvis_group.add(indices, pelvis_weight, "REPLACE")
        thigh_group.add(indices, 1.0 - pelvis_weight, "REPLACE")

    solidify = obj.modifiers.new("SoftClothThickness", "SOLIDIFY")
    solidify.thickness = 0.0045
    solidify.offset = 0.0
    solidify.use_even_offset = True
    armature_modifier = obj.modifiers.new("OwnedYuukaArmature", "ARMATURE")
    armature_modifier.object = armature
    obj["surfaceMethod"] = "smooth fitted athletic-short shell"
    generated_surface_objects.append(obj)
    return obj


def make_polished_leg(name, sign):
    """Create one continuous, softly tapered chibi leg from shorts to low shoe."""
    profiles = (
        (0.405, 0.066, -0.026, 0.045, 0.055),
        (0.388, 0.069, -0.025, 0.052, 0.064),
        (0.360, 0.073, -0.024, 0.058, 0.069),
        (0.325, 0.075, -0.023, 0.056, 0.066),
        (0.290, 0.077, -0.022, 0.052, 0.061),
        (0.255, 0.079, -0.021, 0.048, 0.056),
        (0.220, 0.081, -0.020, 0.044, 0.051),
        (0.190, 0.083, -0.019, 0.043, 0.049),
        (0.160, 0.085, -0.018, 0.045, 0.050),
        (0.132, 0.086, -0.017, 0.042, 0.047),
        (0.105, 0.087, -0.016, 0.037, 0.042),
        (0.080, 0.087, -0.016, 0.032, 0.037),
        (0.059, 0.087, -0.016, 0.029, 0.033),
        (0.042, 0.087, -0.016, 0.027, 0.030),
    )
    radial_segments = 32
    vertices = []
    faces = []
    for z, center_x, center_y, radius_x, radius_y in profiles:
        for segment in range(radial_segments):
            angle = 2.0 * math.pi * segment / radial_segments
            vertices.append(
                (
                    sign * (center_x + radius_x * math.cos(angle)),
                    center_y + radius_y * math.sin(angle),
                    z,
                )
            )
    for ring in range(len(profiles) - 1):
        for segment in range(radial_segments):
            next_segment = (segment + 1) % radial_segments
            a = ring * radial_segments + segment
            b = ring * radial_segments + next_segment
            c = (ring + 1) * radial_segments + next_segment
            d = (ring + 1) * radial_segments + segment
            faces.append((a, b, c, d))

    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    copy_body_transform(obj)
    obj.data.materials.append(MAT_SKIN_FLAT)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True

    side = "L" if sign > 0 else "R"
    pelvis_group = obj.vertex_groups.new(name="Bip001 Pelvis")
    thigh_group = obj.vertex_groups.new(name=f"Bip001 {side} Thigh")
    calf_group = obj.vertex_groups.new(name=f"Bip001 {side} Calf")
    foot_group = obj.vertex_groups.new(name=f"Bip001 {side} Foot")
    for ring in range(len(profiles)):
        t = ring / (len(profiles) - 1)
        indices = list(range(ring * radial_segments, (ring + 1) * radial_segments))
        if t <= 0.12:
            pelvis_weight = 0.28 * (1.0 - t / 0.12)
            thigh_weight = 1.0 - pelvis_weight
            weights = (pelvis_weight, thigh_weight, 0.0, 0.0)
        elif t <= 0.54:
            weights = (0.0, 1.0, 0.0, 0.0)
        elif t <= 0.68:
            blend = (t - 0.54) / 0.14
            weights = (0.0, 1.0 - blend, blend, 0.0)
        elif t <= 0.90:
            weights = (0.0, 0.0, 1.0, 0.0)
        else:
            blend = (t - 0.90) / 0.10
            weights = (0.0, 0.0, 1.0 - 0.45 * blend, 0.45 * blend)
        for group, weight in zip((pelvis_group, thigh_group, calf_group, foot_group), weights):
            if weight > 0.0:
                group.add(indices, weight, "REPLACE")
    modifier = obj.modifiers.new("OwnedYuukaArmature", "ARMATURE")
    modifier.object = armature
    obj["surfaceMethod"] = "continuous smooth hip-to-low-shoe chibi leg"
    generated_surface_objects.append(obj)
    return obj


def make_polished_neckline_patch():
    """Close the donor neckline aperture with a dark, tank-integrated underlay."""
    radial_segments = 32
    radial_rings = 4
    # This lies behind the authored tank front but in front of the arm end caps,
    # so only the aperture reveals the same-dark cloth and no shoulder tube can
    # appear as a detached skin wedge.
    vertices = [(0.0, -0.020, 0.606)]
    faces = []
    for ring in range(1, radial_rings + 1):
        radius = ring / radial_rings
        for segment in range(radial_segments):
            angle = 2.0 * math.pi * segment / radial_segments
            x = 0.045 * radius * math.cos(angle)
            z = 0.606 + 0.023 * radius * math.sin(angle)
            y = -0.020 + 0.006 * radius * radius
            vertices.append((x, y, z))
    for segment in range(radial_segments):
        next_segment = (segment + 1) % radial_segments
        faces.append((0, 1 + segment, 1 + next_segment))
    for ring in range(1, radial_rings):
        inner_start = 1 + (ring - 1) * radial_segments
        outer_start = 1 + ring * radial_segments
        for segment in range(radial_segments):
            next_segment = (segment + 1) % radial_segments
            faces.append(
                (
                    inner_start + segment,
                    outer_start + segment,
                    outer_start + next_segment,
                    inner_start + next_segment,
                )
            )
    mesh = bpy.data.meshes.new("SisterProof14NecklineSkinPatchMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new("SisterProof14NecklineSkinPatch", mesh)
    bpy.context.scene.collection.objects.link(obj)
    copy_body_transform(obj)
    obj.data.materials.append(MAT_NECKLINE_INLAY)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    spine_group = obj.vertex_groups.new(name="Bip001 Spine1")
    spine_group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    modifier = obj.modifiers.new("OwnedYuukaArmature", "ARMATURE")
    modifier.object = armature
    obj["surfaceMethod"] = "shallow same-dark neckline underlay hidden beneath tank boundary"
    generated_surface_objects.append(obj)
    return obj


if ARGS.style == "casual_polished":
    make_polished_arm("SisterProof13LeftBareArm", 1.0)
    make_polished_arm("SisterProof13RightBareArm", -1.0)
    make_polished_short_leg("SisterProof13LeftShortLeg", 1.0)
    make_polished_short_leg("SisterProof13RightShortLeg", -1.0)
    make_polished_leg("SisterProof13LeftBareLeg", 1.0)
    make_polished_leg("SisterProof13RightBareLeg", -1.0)

# The source atlas has no usable mouth pixels.  Add one very small, curved,
# surface-attached ribbon at the original mouth island location.  It is weighted
# only to the donor head bone and never forms a plate across the face.
mouth_points = (
    (-0.00013, 0.006785),
    (-0.000065, 0.006755),
    (0.0, 0.006744),
    (0.000065, 0.006755),
    (0.00013, 0.006785),
)
mouth_vertices = []
half_thickness = 0.000008
for x, z in mouth_points:
    mouth_scale = 100.0 if IS_PLAYER15 else 1.0
    mouth_vertices.append((x * mouth_scale, -0.001205 * mouth_scale, (z + half_thickness) * mouth_scale))
    mouth_vertices.append((x * mouth_scale, -0.001207 * mouth_scale, (z - half_thickness) * mouth_scale))
mouth_faces = [(index * 2, index * 2 + 2, index * 2 + 3, index * 2 + 1) for index in range(4)]
mouth_mesh = bpy.data.meshes.new("SisterProof11MouthRibbonMesh")
mouth_mesh.from_pydata(mouth_vertices, [], mouth_faces)
mouth_mesh.update(calc_edges=True)
mouth = bpy.data.objects.new("SisterProof11SurfaceMouth", mouth_mesh)
bpy.context.scene.collection.objects.link(mouth)
mouth.data.materials.append(MAT_MOUTH)
mouth_group = mouth.vertex_groups.new(name="Bip001 Head")
mouth_group.add(list(range(len(mouth_vertices))), 1.0, "REPLACE")
mouth_modifier = mouth.modifiers.new("OwnedYuukaHeadRig", "ARMATURE")
mouth_modifier.object = armature
mouth["surfaceAttachedFacialFeature"] = True
mouth["candidateClaim"] = False
generated_surface_objects.append(mouth)


def make_player15_cap():
    """One closed scalp-hugging shell with an integrated short visor.

    This is a fresh direct surface for Proof15.  Crown and visor share the same
    radial topology; there is no reused Proof14 mesh, stacked brim, band, box,
    plate or floating trim object.
    """
    local_scale = 100.0
    radial_rings = 6
    segments = 48
    upper_vertices = [(0.0, -0.000035 * local_scale, 0.01030 * local_scale)]
    lower_vertices = [(0.0, 0.000010 * local_scale, 0.00897 * local_scale)]

    def smoothstep(value):
        value = max(0.0, min(1.0, value))
        return value * value * (3.0 - 2.0 * value)

    for ring in range(1, radial_rings + 1):
        radius = ring / radial_rings
        dome = max(0.0, 1.0 - radius * radius) ** 0.55
        brim_ring = smoothstep((radius - 0.64) / 0.36)
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            cosine = math.cos(angle)
            sine = math.sin(angle)
            front = max(0.0, -sine) ** 1.75
            brim = brim_ring * front
            x = 0.00163 * radius * cosine * (1.0 - 0.055 * brim)
            y = -0.000030 + 0.00117 * radius * sine - 0.00042 * brim
            crown_z = 0.00920 + 0.00110 * dome * (1.0 - 0.035 * sine)
            visor_z = 0.00920 - 0.000075 * front + 0.000030 * cosine * cosine
            upper_z = crown_z * (1.0 - brim) + visor_z * brim
            underside_z = 0.00878 + 0.00019 * max(0.0, 1.0 - radius * radius)
            visor_under_z = visor_z - 0.000075
            lower_z = underside_z * (1.0 - brim) + visor_under_z * brim
            upper_vertices.append((x * local_scale, y * local_scale, upper_z * local_scale))
            lower_vertices.append((x * local_scale, y * local_scale, lower_z * local_scale))

    vertices = upper_vertices + lower_vertices
    lower_offset = len(upper_vertices)
    faces = []

    def ring_start(ring, offset=0):
        return offset + 1 + (ring - 1) * segments

    for segment in range(segments):
        next_segment = (segment + 1) % segments
        faces.append((0, ring_start(1) + segment, ring_start(1) + next_segment))
        faces.append(
            (
                lower_offset,
                ring_start(1, lower_offset) + next_segment,
                ring_start(1, lower_offset) + segment,
            )
        )
    for ring in range(1, radial_rings):
        upper_inner = ring_start(ring)
        upper_outer = ring_start(ring + 1)
        lower_inner = ring_start(ring, lower_offset)
        lower_outer = ring_start(ring + 1, lower_offset)
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            faces.append(
                (
                    upper_inner + segment,
                    upper_outer + segment,
                    upper_outer + next_segment,
                    upper_inner + next_segment,
                )
            )
            faces.append(
                (
                    lower_inner + segment,
                    lower_inner + next_segment,
                    lower_outer + next_segment,
                    lower_outer + segment,
                )
            )
    upper_outer = ring_start(radial_rings)
    lower_outer = ring_start(radial_rings, lower_offset)
    for segment in range(segments):
        next_segment = (segment + 1) % segments
        faces.append(
            (
                upper_outer + segment,
                lower_outer + segment,
                lower_outer + next_segment,
                upper_outer + next_segment,
            )
        )

    mesh = bpy.data.meshes.new("PlayerOriginalSurface15CapSingleShellMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new("PlayerOriginalSurface15_Cap_SingleScalpHuggingShell", mesh)
    bpy.context.scene.collection.objects.link(obj)
    copy_body_transform(obj)
    obj.data.materials.append(MAT_PLAYER_CAP)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    group = obj.vertex_groups.new(name="Bip001 Head")
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    modifier = obj.modifiers.new("OwnedYuukaHeadRig", "ARMATURE")
    modifier.object = armature
    subdivision = obj.modifiers.new("SingleShellPolish", "SUBSURF")
    subdivision.subdivision_type = "CATMULL_CLARK"
    subdivision.levels = 2
    subdivision.render_levels = 2
    obj["surfacePolicy"] = "fresh one-connected closed shell; crown and visor share radial topology"
    obj["candidateClaim"] = False
    generated_surface_objects.append(obj)
    return obj


player15_cap = make_player15_cap() if IS_PLAYER15 else None

armature.scale = (1.0, 1.0, 1.0)
bpy.context.view_layer.update()
points = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
lo = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
hi = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
center = (lo + hi) * 0.5
extent = hi - lo

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200 if IS_PLAYER15 else 1000
scene.render.resolution_y = 1600 if IS_PLAYER15 else 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.world.color = (0.008, 0.012, 0.020)

camera_data = bpy.data.cameras.new("SisterProof11CameraData")
camera = bpy.data.objects.new("SisterProof11Camera", camera_data)
scene.collection.objects.link(camera)
camera_data.type = "ORTHO"
camera_data.ortho_scale = max(extent.z * 1.15, extent.x * 1.35)
scene.camera = camera

for name, offset, energy, size in (
    ("SisterProof11Key", Vector((-3.5, -4.0, 4.2)), 900.0, 3.2),
    ("SisterProof11Fill", Vector((3.8, -2.0, 2.6)), 560.0, 3.0),
    ("SisterProof11Rim", Vector((0.0, 3.8, 3.0)), 720.0, 2.8),
):
    data = bpy.data.lights.new(name + "Data", "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    scene.collection.objects.link(light)
    light.location = center + offset * max(extent.z, 1.0)
    light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()

distance = max(extent.z, 1.0) * 4.0
views = {
    "front": Vector((0.0, -distance, 0.0)),
    "three-quarter": Vector((distance * 0.66, -distance * 0.76, 0.0)),
    "side": Vector((distance, 0.0, 0.0)),
    "back": Vector((0.0, distance, 0.0)),
}


def render_set(prefix):
    paths = []
    for label, offset in views.items():
        camera.location = center + offset
        camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
        path = OUTPUT / f"{prefix}-{label}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(str(path))
    return paths


if IS_PLAYER15:
    player15_cap.hide_render = False
    color_with_cap_paths = render_set(f"{FILE_PREFIX}-with-cap-color")
    player15_cap.hide_render = True
    color_no_cap_paths = render_set(f"{FILE_PREFIX}-no-cap-color")
    player15_cap.hide_render = False
    color_paths = color_with_cap_paths + color_no_cap_paths
else:
    color_paths = render_set(FILE_PREFIX)

material_backup = list(body.data.materials)
index_backup = [polygon.material_index for polygon in body.data.polygons]
generated_material_backups = {obj.name: list(obj.data.materials) for obj in generated_surface_objects}
generated_index_backups = {
    obj.name: [polygon.material_index for polygon in obj.data.polygons]
    for obj in generated_surface_objects
}
body.data.materials.clear()
body.data.materials.append(MAT_GRAY)
body.data.materials.append(MAT_HIDDEN)
for polygon in body.data.polygons:
    polygon.material_index = 1 if polygon.index in transparent_polygons else 0
for obj in generated_surface_objects:
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
    for polygon in obj.data.polygons:
        polygon.material_index = 0

wire_objects = []
if IS_PLAYER15:
    MAT_WIRE = solid_material("PlayerOriginal15ActualTopologyWire", (0.008, 0.010, 0.014), 0.90)

    def make_wire_overlay(source, preserve_hidden=False):
        wire = source.copy()
        wire.data = source.data.copy()
        wire.name = "QA_Wire_" + source.name
        bpy.context.scene.collection.objects.link(wire)
        wire.data.materials.clear()
        wire.data.materials.append(MAT_WIRE)
        if preserve_hidden:
            wire.data.materials.append(MAT_HIDDEN)
        for polygon in wire.data.polygons:
            if not preserve_hidden or polygon.index not in transparent_polygons:
                polygon.material_index = 0
            else:
                polygon.material_index = 1
        modifier = wire.modifiers.new("QA_ActualSourceTopology", "WIREFRAME")
        modifier.thickness = 0.000008
        modifier.use_replace = True
        modifier.use_even_offset = True
        wire_objects.append(wire)
        return wire

    body_wire = make_wire_overlay(body, preserve_hidden=True)
    cap_wire = make_wire_overlay(player15_cap, preserve_hidden=False)
    player15_cap.hide_render = False
    cap_wire.hide_render = False
    gray_with_cap_paths = render_set(f"{FILE_PREFIX}-with-cap-gray-wire")
    player15_cap.hide_render = True
    cap_wire.hide_render = True
    gray_no_cap_paths = render_set(f"{FILE_PREFIX}-no-cap-gray-wire")
    player15_cap.hide_render = False
    cap_wire.hide_render = False
    gray_paths = gray_with_cap_paths + gray_no_cap_paths
    for wire in wire_objects:
        bpy.data.objects.remove(wire, do_unlink=True)
    bpy.data.materials.remove(MAT_WIRE)
else:
    gray_paths = render_set(f"sister-proof11-{ARGS.style}-original-surface-gray")
body.data.materials.clear()
for material in material_backup:
    body.data.materials.append(material)
for polygon, material_index in zip(body.data.polygons, index_backup):
    polygon.material_index = material_index
for obj in generated_surface_objects:
    obj.data.materials.clear()
    for material in generated_material_backups[obj.name]:
        obj.data.materials.append(material)
    for polygon, material_index in zip(obj.data.polygons, generated_index_backups[obj.name]):
        polygon.material_index = material_index

coordinate_after = coordinate_hash(body)
bone_names_after = sorted(bone.name for bone in armature.data.bones)
groups_after = {
    vertex.index: sorted((body.vertex_groups[item.group].name, round(float(item.weight), 8)) for item in vertex.groups)
    for vertex in body.data.vertices
}
unexpected_coordinate_changes = [
    vertex.index
    for vertex in body.data.vertices
    if tuple(vertex.co) != original_coordinates[vertex.index]
    and vertex.index not in allowed_coordinate_changes
]
if unexpected_coordinate_changes:
    raise RuntimeError(f"Unexpected Yuuka coordinate changes: {unexpected_coordinate_changes[:12]}")
if ARGS.style not in {"casual", "casual_polished"} and coordinate_before != coordinate_after:
    raise RuntimeError("Original Yuuka coordinates changed")
if bone_names_before != bone_names_after or groups_after != original_vertex_groups:
    raise RuntimeError("Original Yuuka rig or weights changed")

body["proofRevision"] = "PlayerOriginalSurface15PolishedGate" if IS_PLAYER15 else f"SisterProof11OriginalSurfaceStyle_{ARGS.style}"
body["candidateClaim"] = False
body["nativeHandPolicy"] = "original 3-digit stylized hand retained"
body["test3Excluded"] = True

blend_path = OUTPUT / f"{FILE_PREFIX}.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

receipt = {
    "schema": "family-company.player-original-surface15-polished-gate.v1" if IS_PLAYER15 else "family-company.sister-proof11-original-surface-style.v1",
    "status": (
        "STATIC_DIAGNOSTIC_ONLY_AWAITING_ROOT_REVIEW"
        if IS_PLAYER15
        else "DIRECTION_ONLY_PENDING_USER_APPROVAL"
        if ARGS.style == "casual_polished"
        else "AWAITING_ROOT_ORIGINAL_SURFACE_STYLE_GATE"
    ),
    "candidateClaim": False,
    "sourceBasis": "user-owned test2 Yuuka original FBX only",
    "source": str(SOURCE),
    "sourceSha256": sha256(SOURCE),
    "test3SakurakoExcluded": True,
    "structuralProof": {
        "bodyObject": body.name,
        "vertices": len(body.data.vertices),
        "polygons": len(body.data.polygons),
        "connectedComponents": component_count,
        "originalCoordinatesExact": coordinate_before == coordinate_after,
        "coordinateChangesRestrictedToDeclaredComponents": not unexpected_coordinate_changes,
        "reshapedDonorComponents": (
            [141, 181]
            if ARGS.style == "casual"
            else [218]
            if ARGS.style == "casual_polished"
            else []
        ),
        "originalWeightsExact": groups_after == original_vertex_groups,
        "originalRigBoneCount": len(armature.data.bones),
        "originalRigBoneNamesExact": bone_names_before == bone_names_after,
        "nativeHands": "original 3-digit stylized hand retained",
        "newCharacterGeometryObjects": len(generated_surface_objects),
        "newCharacterGeometryPurpose": (
            "six smooth rigged casual surface bridges plus one tiny head-weighted mouth ribbon; native hands untouched"
            if ARGS.style == "casual_polished"
            else "one fresh closed single-shell cap plus one tiny surface mouth ribbon; no new clothing, body, panel or box geometry"
            if IS_PLAYER15
            else "one tiny head-weighted mouth ribbon; no clothing/accessory/body geometry"
        ),
        "removedWholeNonBodyObjects": ["Yuuka_Original_Weapon", "Yuuka_Original_Calculator"],
    },
    "styleMethod": {
        "styleVariant": ARGS.style,
        "geometry": (
            "untouched authored Yuuka face/hair/torso/hands plus six smooth authored-scale casual bridges"
            if ARGS.style == "casual_polished"
            else "fresh Yuuka original body with coordinate-exact face, selected hair, torso, legs, shoes and native hands; whole-component visibility/material routing only"
            if IS_PLAYER15
            else "untouched authored Yuuka surface; no morph/remesh/cut/new parts"
        ),
        "hair": (
            "original c281/c282/c329/c330/c337/c338/c339/c343 scalp, nape and bangs retained coordinate-exact; source highlights remapped deep brown; twin tails/long rear locks/clips hidden whole-component"
            if IS_PLAYER15
            else "source UV luminance/mask remapped to authority charcoal"
        ),
        "eyes": (
            "original eye topology, alpha and highlight polish retained; brown hue remap with saturation reduced to 0.72 for quieter lashes"
            if IS_PLAYER15
            else "source alpha/highlights retained; hue shifted toward authority teal"
        ),
        "outfit": (
            "original fitted tank/pelvis retained; smooth weighted arm and athletic-short shells bridge missing casual surfaces"
            if ARGS.style == "casual_polished"
            else "only original fitted c141/c157/c181 torso-sleeve, c063-c066/c099-c102/c217-c218 lower, and audited shoe surfaces; off-white/navy palette; no added garment surface"
            if IS_PLAYER15
            else "whole authored connected surfaces retained; component material roles only"
        ),
        "cap": (
            {
                "policy": "fresh one-connected closed smooth shell; crown and short visor share the same radial topology; Proof14 geometry not reused",
                "vertices": len(player15_cap.data.vertices),
                "polygons": len(player15_cap.data.polygons),
                "connectedComponents": connected_components(player15_cap.data)[1],
                "boundaryEdges": boundary_edge_count(player15_cap.data),
                "noCapBaselineRendered": True,
            }
            if IS_PLAYER15
            else None
        ),
        "mouth": "small surface-attached head-weighted ribbon because source atlas contains no usable mouth pixels",
        "rolePolygonCounts": dict(sorted(role_counts.items())),
    },
    "componentRoles": component_receipt,
    "renders": (
        {
            "withCap": {"color": color_with_cap_paths, "grayWire": gray_with_cap_paths},
            "noCap": {"color": color_no_cap_paths, "grayWire": gray_no_cap_paths},
        }
        if IS_PLAYER15
        else {"color": color_paths, "gray": gray_paths}
    ),
    "blend": str(blend_path),
    "visualGate": (
        {
            "verdict": "FAIL_CLOSED",
            "candidateClaim": False,
            "promotionBlocked": True,
            "criticalFailures": [
                "cap silhouette reads as an oversized smooth helmet/dome; the short visor is not legible",
                "rear view exposes the back sides of the authored face and both eye apertures because the legal short-hair component set does not close the donor rear head",
                "minimal original torso islands leave a large dark V aperture and open shoulder seams, so the surface does not read as the authority hoodie",
                "wire overlay thickness is sub-pixel in the current static render and does not provide a readable topology proof",
            ],
        }
        if IS_PLAYER15
        else None
    ),
    "knownLimitations": [
        (
            "authored Yuuka ankle/footwear silhouette remains pending a separate barefoot gate"
            if ARGS.style == "casual_polished"
            else "minimal coordinate-exact Yuuka inner surfaces expose source discontinuities and are diagnostic only"
            if IS_PLAYER15
            else "authored Yuuka jacket/skirt silhouette remains and is not an exact tank/shorts match"
        ),
        (
            "rear face/eye backs, hoodie gaps, cap shape and unreadable wire overlay block promotion"
            if IS_PLAYER15
            else "age-20 role is carried mainly by authority palette and hair/eye identity"
        ),
        "static gate only; GIF and Unity validation intentionally not generated",
    ],
}
(OUTPUT / f"{FILE_PREFIX}-receipt.json").write_text(
    json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8"
)
print("PLAYER_ORIGINAL_SURFACE15_POLISHED_GATE_RENDERED" if IS_PLAYER15 else "SISTER_PROOF11_ORIGINAL_SURFACE_STYLE_RENDERED")
