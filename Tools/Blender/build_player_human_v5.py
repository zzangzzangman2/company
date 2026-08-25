"""Build the Player Human V5 proof from the user-supplied Yuuka base mesh.

This is deliberately isolated from Unity candidate/production assets.  It keeps
the original game-grade face, EyeMouth topology, hand topology, humanoid rig,
and most of the body mesh, then directly edits materials/geometry into the
Player identity.  test3/Sakurako is never opened.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import sys
from collections import defaultdict, deque
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=r"C:\Users\godho\Documents\Codex\family_company_unity")
    parser.add_argument("--output", default="Artifacts/Family3DPlayerHumanV5/Proof8")
    parser.add_argument("--proof-tag", default="proof8")
    return parser.parse_args(argv)


ARGS = parse_args()
REPO = Path(ARGS.repo).resolve()
OUTPUT = (REPO / ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
PROOF_TAG = ARGS.proof_tag
SOURCE_DIR = (
    REPO
    / "Artifacts"
    / "ExternalReferenceStudy"
    / "UserProvided_BlueArchive_OriginalMeshes_2026-08-24"
    / "Yuuka"
    / "Yuuka_Original_Mesh"
)
SOURCE_FBX = SOURCE_DIR / "Yuuka_Original_Mesh.fbx"
BODY_TEXTURE = SOURCE_DIR / "Yuuka_Original_Body.png"
FACE_TEXTURE = SOURCE_DIR / "Yuuka_Original_Face.png"
HAIR_TEXTURE = SOURCE_DIR / "Yuuka_Original_Hair.png"
EYE_TEXTURE = SOURCE_DIR / "Yuuka_Original_EyeMouth.png"

for required in (SOURCE_FBX, BODY_TEXTURE, FACE_TEXTURE, HAIR_TEXTURE, EYE_TEXTURE):
    if not required.is_file():
        raise FileNotFoundError(required)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def clean_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def principled_material(name: str, color, roughness: float = 0.62, metallic: float = 0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if "IOR Level" in bsdf.inputs:
        bsdf.inputs["IOR Level"].default_value = 0.26
    mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def textured_face_material(name: str, path: Path):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(str(path), check_existing=True)
    tex.interpolation = "Linear"
    hue = nodes.new("ShaderNodeHueSaturation")
    hue.inputs["Hue"].default_value = 0.50
    hue.inputs["Saturation"].default_value = 0.91
    hue.inputs["Value"].default_value = 1.04
    bsdf.inputs["Roughness"].default_value = 0.66
    if "IOR Level" in bsdf.inputs:
        bsdf.inputs["IOR Level"].default_value = 0.20
    mat.node_tree.links.new(tex.outputs["Color"], hue.inputs["Color"])
    mat.node_tree.links.new(hue.outputs["Color"], bsdf.inputs["Base Color"])
    mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def brown_hair_material(name: str, path: Path):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(str(path), check_existing=True)
    bw = nodes.new("ShaderNodeRGBToBW")
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.08
    ramp.color_ramp.elements[0].color = (0.018, 0.005, 0.002, 1.0)
    ramp.color_ramp.elements[1].position = 0.78
    ramp.color_ramp.elements[1].color = (0.22, 0.055, 0.018, 1.0)
    ramp.color_ramp.elements.new(0.46).color = (0.075, 0.015, 0.005, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.48
    if "IOR Level" in bsdf.inputs:
        bsdf.inputs["IOR Level"].default_value = 0.36
    mat.node_tree.links.new(tex.outputs["Color"], bw.inputs["Color"])
    mat.node_tree.links.new(bw.outputs["Val"], ramp.inputs["Fac"])
    mat.node_tree.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def keyed_brown_eye_material(name: str, path: Path):
    """Restore the game's black-key EyeMouth decal behavior.

    The source PNG alpha is fully opaque.  Alpha therefore comes from the
    maximum RGB channel with a small black threshold.  This removes the opaque
    black nose/mouth polygon patch visible under a stock Principled import.
    """
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(str(path), check_existing=True)
    tex.interpolation = "Closest"
    hue = nodes.new("ShaderNodeHueSaturation")
    # Blue-violet donor iris -> warm brown while keeping white highlights white.
    hue.inputs["Hue"].default_value = 0.86
    hue.inputs["Saturation"].default_value = 0.78
    hue.inputs["Value"].default_value = 1.06
    sep = nodes.new("ShaderNodeSeparateColor")
    sep.mode = "RGB"
    max_rg = nodes.new("ShaderNodeMath")
    max_rg.operation = "MAXIMUM"
    max_rgb = nodes.new("ShaderNodeMath")
    max_rgb.operation = "MAXIMUM"
    subtract = nodes.new("ShaderNodeMath")
    subtract.operation = "SUBTRACT"
    subtract.inputs[1].default_value = 0.022
    multiply = nodes.new("ShaderNodeMath")
    multiply.operation = "MULTIPLY"
    multiply.inputs[1].default_value = 7.0
    multiply.use_clamp = True
    bsdf.inputs["Roughness"].default_value = 0.40
    if "IOR Level" in bsdf.inputs:
        bsdf.inputs["IOR Level"].default_value = 0.42
    mat.node_tree.links.new(tex.outputs["Color"], hue.inputs["Color"])
    mat.node_tree.links.new(hue.outputs["Color"], bsdf.inputs["Base Color"])
    mat.node_tree.links.new(tex.outputs["Color"], sep.inputs["Color"])
    mat.node_tree.links.new(sep.outputs["Red"], max_rg.inputs[0])
    mat.node_tree.links.new(sep.outputs["Green"], max_rg.inputs[1])
    mat.node_tree.links.new(max_rg.outputs[0], max_rgb.inputs[0])
    mat.node_tree.links.new(sep.outputs["Blue"], max_rgb.inputs[1])
    mat.node_tree.links.new(max_rgb.outputs[0], subtract.inputs[0])
    mat.node_tree.links.new(subtract.outputs[0], multiply.inputs[0])
    mat.node_tree.links.new(multiply.outputs[0], bsdf.inputs["Alpha"])
    mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    try:
        mat.surface_render_method = "DITHERED"
    except (AttributeError, TypeError):
        if hasattr(mat, "blend_method"):
            mat.blend_method = "HASHED"
    if hasattr(mat, "use_transparency_overlap"):
        mat.use_transparency_overlap = False
    return mat


def horizontal_stripe_material(name: str, navy, yellow):
    """Continuous world-Z stripes; avoids triangle-by-triangle color breakup."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
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
    mix.blend_type = "MIX"
    mix.inputs[1].default_value = navy
    mix.inputs[2].default_value = yellow
    bsdf.inputs["Roughness"].default_value = 0.58
    mat.node_tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    mat.node_tree.links.new(separate.outputs["Z"], multiply.inputs[0])
    mat.node_tree.links.new(multiply.outputs[0], sine.inputs[0])
    mat.node_tree.links.new(sine.outputs[0], greater.inputs[0])
    mat.node_tree.links.new(greater.outputs[0], mix.inputs[0])
    mat.node_tree.links.new(mix.outputs[0], bsdf.inputs["Base Color"])
    mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def assign_original_slot(body, old_name: str, new_material) -> None:
    for index, mat in enumerate(body.data.materials):
        if mat and mat.name == old_name:
            body.data.materials[index] = new_material
            return
    raise RuntimeError(f"Missing material slot: {old_name}")


def poly_group_score(obj, poly, predicate) -> float:
    score = 0.0
    for vertex_index in poly.vertices:
        for assignment in obj.data.vertices[vertex_index].groups:
            name = obj.vertex_groups[assignment.group].name
            if predicate(name):
                score += assignment.weight
    return score


def poly_total_weight(obj, poly) -> float:
    return sum(assignment.weight for vi in poly.vertices for assignment in obj.data.vertices[vi].groups)


def material_face_components(mesh, material_index):
    polygons = [polygon for polygon in mesh.polygons if polygon.material_index == material_index]
    by_vertex = defaultdict(list)
    polygon_by_index = {polygon.index: polygon for polygon in polygons}
    for polygon in polygons:
        for vertex_index in polygon.vertices:
            by_vertex[vertex_index].append(polygon.index)
    remaining = set(polygon_by_index)
    components = []
    while remaining:
        first = remaining.pop()
        queue = deque([first])
        component = {first}
        while queue:
            polygon = polygon_by_index[queue.popleft()]
            for vertex_index in polygon.vertices:
                for adjacent in by_vertex[vertex_index]:
                    if adjacent in remaining:
                        remaining.remove(adjacent)
                        component.add(adjacent)
                        queue.append(adjacent)
        components.append(component)
    return components


def delete_vertices(obj, vertex_indices) -> None:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in sorted(vertex_indices)], context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def delete_selected_faces(obj) -> int:
    selected_indices = {p.index for p in obj.data.polygons if p.select}
    count = len(selected_indices)
    if not count:
        return 0
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in selected_indices], context="FACES")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    return count


def add_uv(name, location, scale, material, segments=64, rings=40):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for p in obj.data.polygons:
        p.use_smooth = True
    obj.data.materials.append(material)
    return obj


def add_torus(name, location, major_radius, minor_radius, scale, material, rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_radius,
        minor_radius=minor_radius,
        major_segments=72,
        minor_segments=16,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for p in obj.data.polygons:
        p.use_smooth = True
    obj.data.materials.append(material)
    return obj


def add_curve(name, points, material, bevel=0.012):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 16
    curve.bevel_depth = bevel
    curve.bevel_resolution = 4
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, coordinate in zip(spline.bezier_points, points):
        point.co = coordinate
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def add_loft(name, rings, material, segments=56):
    vertices = []
    for z, radius_x, radius_y, center_y in rings:
        for index in range(segments):
            angle = math.tau * index / segments
            vertices.append((radius_x * math.cos(angle), center_y + radius_y * math.sin(angle), z))
    faces = []
    for ring in range(len(rings) - 1):
        for index in range(segments):
            nxt = (index + 1) % segments
            a = ring * segments + index
            b = ring * segments + nxt
            c = (ring + 1) * segments + nxt
            d = (ring + 1) * segments + index
            faces.append((a, b, c, d))
    faces.append(tuple(reversed(tuple(range(segments)))))
    top_start = (len(rings) - 1) * segments
    faces.append(tuple(top_start + index for index in range(segments)))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    bevel = obj.modifiers.new(name + "SoftEdge", "BEVEL")
    bevel.width = 0.018
    bevel.segments = 3
    return obj


def add_pointed_hair_card(name, location, width, height, depth, rotation_z, material, skew=0.0):
    """Create one shallow, pointed hair clump instead of a spherical shell."""
    half_w = width * 0.5
    half_h = height * 0.5
    shoulder_z = -height * 0.12
    outline = (
        (-half_w, half_h),
        (half_w, half_h),
        (half_w * 0.88, shoulder_z),
        (skew, -half_h),
        (-half_w * 0.88, shoulder_z),
    )
    vertices = []
    for local_y in (-depth * 0.5, depth * 0.5):
        vertices.extend((x, local_y, z) for x, z in outline)
    faces = [tuple(reversed(range(5))), tuple(range(5, 10))]
    for index in range(5):
        nxt = (index + 1) % 5
        faces.append((index, nxt, 5 + nxt, 5 + index))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler.z = rotation_z
    obj.data.materials.append(material)
    bevel = obj.modifiers.new(name + "SoftEdge", "BEVEL")
    bevel.width = min(depth * 0.28, 0.012)
    bevel.segments = 3
    return obj


def add_curved_back_panel(name, rings, material, depth=0.032):
    """A shallow curved hoodie back panel that covers the inner shirt at rear."""
    columns = (-1.0, -0.5, 0.0, 0.5, 1.0)
    vertices = []
    for inward in (0.0, -depth):
        for z, half_width, base_y in rings:
            for column in columns:
                y = base_y + 0.026 * (1.0 - column * column) + inward
                vertices.append((half_width * column, y, z))
    row_width = len(columns)
    layer_width = len(rings) * row_width
    faces = []
    for layer in range(2):
        base = layer * layer_width
        for row in range(len(rings) - 1):
            for column in range(row_width - 1):
                a = base + row * row_width + column
                b = a + 1
                c = b + row_width
                d = a + row_width
                faces.append((a, b, c, d) if layer == 0 else (d, c, b, a))
    for row in range(len(rings) - 1):
        for column in (0, row_width - 1):
            outer_a = row * row_width + column
            outer_b = (row + 1) * row_width + column
            inner_a = layer_width + outer_a
            inner_b = layer_width + outer_b
            faces.append((outer_a, inner_a, inner_b, outer_b))
    for row in (0, len(rings) - 1):
        for column in range(row_width - 1):
            outer_a = row * row_width + column
            outer_b = outer_a + 1
            inner_a = layer_width + outer_a
            inner_b = inner_a + 1
            faces.append((outer_a, outer_b, inner_b, inner_a))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    bevel = obj.modifiers.new(name + "SoftEdge", "BEVEL")
    bevel.width = 0.018
    bevel.segments = 3
    return obj


def add_beveled_box(name, location, dimensions, material, bevel_width=0.012):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    bevel = obj.modifiers.new(name + "SoftEdge", "BEVEL")
    bevel.width = bevel_width
    bevel.segments = 3
    return obj


def parent_to_bone_keep_world(obj, armature, bone_name: str) -> None:
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world


clean_scene()
bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX))

armature = bpy.data.objects.get("Armature")
body = bpy.data.objects.get("Yuuka_Original_Body")
if armature is None or body is None:
    raise RuntimeError("Yuuka body/armature import failed")

# Imported FBX uses centimeter-like authoring units.  Scale the retained rig,
# not the child mesh, so skinning and original weights remain intact.
armature.name = "PlayerHumanV5_Rig_YuukaBase"
armature.scale = tuple(component * 400.0 for component in armature.scale)
body.name = "PlayerHumanV5_Body_YuukaTopology"
for removable in ("Yuuka_Original_Calculator", "Yuuka_Original_Weapon"):
    obj = bpy.data.objects.get(removable)
    if obj is not None:
        bpy.data.objects.remove(obj, do_unlink=True)
bpy.context.view_layer.update()

# Replace only shaders first; original face/EyeMouth polygons and UV topology
# stay in the skinned donor body object.
mat_skin = principled_material("PlayerV5_Skin", (0.82, 0.43, 0.31, 1.0), 0.68)
mat_face = textured_face_material("PlayerV5_Face_FromYuukaTopology", FACE_TEXTURE)
mat_hair = brown_hair_material("PlayerV5_Hair_Brown", HAIR_TEXTURE)
mat_brow = principled_material("PlayerV5_Brow_Brown", (0.045, 0.010, 0.004, 1.0), 0.58)
mat_eye = keyed_brown_eye_material("PlayerV5_EyeMouth_Brown_KeyAlpha", EYE_TEXTURE)
mat_hoodie = principled_material("PlayerV5_Hoodie_White", (0.78, 0.82, 0.88, 1.0), 0.58)
mat_hoodie_shadow = principled_material("PlayerV5_Hoodie_Shadow", (0.44, 0.52, 0.64, 1.0), 0.66)
mat_navy = principled_material("PlayerV5_Navy", (0.018, 0.050, 0.125, 1.0), 0.62)
mat_yellow = principled_material("PlayerV5_Shirt_Yellow", (0.95, 0.49, 0.045, 1.0), 0.55)
mat_striped = horizontal_stripe_material(
    "PlayerV5_Shirt_HorizontalStripes",
    (0.018, 0.050, 0.125, 1.0),
    (0.95, 0.49, 0.045, 1.0),
)
mat_shoe = principled_material("PlayerV5_Sneaker_White", (0.88, 0.90, 0.94, 1.0), 0.50)
mat_red = principled_material("PlayerV5_Cap_Red", (0.70, 0.025, 0.012, 1.0), 0.46)
mat_red_dark = principled_material("PlayerV5_Cap_Seam", (0.28, 0.004, 0.002, 1.0), 0.57)
mat_coral = principled_material("PlayerV5_BackAccent_Coral", (0.72, 0.055, 0.030, 1.0), 0.52)
mat_gold = principled_material("PlayerV5_Cap_Pin", (1.0, 0.45, 0.035, 1.0), 0.35, 0.10)

assign_original_slot(body, "Yuuka_Original_Face", mat_face)
assign_original_slot(body, "Yuuka_Original_Hair", mat_hair)
assign_original_slot(body, "Yuuka_Original_Eyebrow", mat_brow)
assign_original_slot(body, "Yuuka_Original_EyeMouth", mat_eye)

# The EyeMouth atlas has one disconnected 32-polygon mouth plate.  Its UVs map
# the atlas' opaque white blob across the entire lower face.  Delete only that
# loose component; both eye components, face shell, hands, weights and rig stay.
eye_slot_index = next(index for index, material in enumerate(body.data.materials) if material == mat_eye)
eye_components = material_face_components(body.data, eye_slot_index)
mouth_components = [component for component in eye_components if len(component) == 32]
if len(mouth_components) != 1:
    raise RuntimeError(f"Expected one 32-polygon mouth plate, found {len(mouth_components)}")
mouth_polygon_indices = mouth_components[0]
mouth_vertex_indices = {
    vertex_index
    for polygon_index in mouth_polygon_indices
    for vertex_index in body.data.polygons[polygon_index].vertices
}
delete_vertices(body, mouth_vertex_indices)
removed_mouth_polygon_count = len(mouth_polygon_indices)
removed_mouth_vertex_count = len(mouth_vertex_indices)

# Remove small disconnected torso tech trinkets while explicitly preserving
# hands/fingers, all limb surfaces, feet and neck.  The three large jacket/body
# shells remain and are recolored as the hoodie.
small_tech_face_indices = set()
for component in material_face_components(body.data, 0):
    if len(component) >= 70:
        continue
    polygons = [body.data.polygons[index] for index in component]
    total = max(sum(poly_total_weight(body, polygon) for polygon in polygons), 1.0e-6)
    anatomical = sum(
        poly_group_score(
            body,
            polygon,
            lambda n: any(token in n for token in (" Hand", " Finger", " Foot", " Toe", " Neck")),
        )
        for polygon in polygons
    )
    coordinates = [body.matrix_world @ body.data.vertices[vertex_index].co for polygon in polygons for vertex_index in polygon.vertices]
    min_z = min(coordinate.z for coordinate in coordinates)
    max_z = max((body.matrix_world @ polygon.center).z for polygon in polygons)
    max_abs_x = max(abs(coordinate.x) for coordinate in coordinates)
    wrist_trinket = max_abs_x > 0.84 and min_z > 1.48 and max_z < 2.02
    if (anatomical / total < 0.32 or wrist_trinket) and max_z > 0.65:
        small_tech_face_indices.update(component)

# Keep the donor's largest, head-weighted scalp.  Then retain only the upper
# faces of donor bone_hair_b_* locks as a short crop; bone_hair_l/r_* twin-tail
# chains are removed.  No procedural sphere or cards are added.
hair_components = material_face_components(body.data, 2)
scalp_component = max(hair_components, key=len)
if len(scalp_component) != 216:
    raise RuntimeError(f"Expected 216-polygon Yuuka scalp component, found {len(scalp_component)}")
scalp_vertices = {
    vertex_index
    for polygon_index in scalp_component
    for vertex_index in body.data.polygons[polygon_index].vertices
}
body_world_inverse = body.matrix_world.inverted()
for vertex_index in scalp_vertices:
    world_coordinate = body.matrix_world @ body.data.vertices[vertex_index].co
    if world_coordinate.y > 0.22:
        world_coordinate.y = 0.22 + (world_coordinate.y - 0.22) * 0.14
        body.data.vertices[vertex_index].co = body_world_inverse @ world_coordinate

short_back_hair_polygons = set()
short_back_hair_vertices = set()
for polygon in body.data.polygons:
    if polygon.material_index != 2 or polygon.index in scalp_component:
        continue
    center = body.matrix_world @ polygon.center
    total = max(poly_total_weight(body, polygon), 1.0e-6)
    back_chain = poly_group_score(body, polygon, lambda name: name.startswith("bone_hair_b_"))
    twin_tail = poly_group_score(
        body,
        polygon,
        lambda name: name.startswith("bone_hair_l_") or name.startswith("bone_hair_r_"),
    )
    if back_chain / total > 0.22 and twin_tail / total < 0.08 and center.z >= 2.70 and abs(center.x) < 0.72:
        short_back_hair_polygons.add(polygon.index)
        short_back_hair_vertices.update(polygon.vertices)

for vertex_index in short_back_hair_vertices:
    world_coordinate = body.matrix_world @ body.data.vertices[vertex_index].co
    if world_coordinate.y > 0.18:
        world_coordinate.y = 0.20 + (world_coordinate.y - 0.18) * 0.14
        body.data.vertices[vertex_index].co = body_world_inverse @ world_coordinate
body.data.update()
retained_rear_hair_polygons = scalp_component | short_back_hair_polygons

# Delete donor identity accessories and crop the remaining hair topology into a
# short, cap-compatible silhouette.  Face/EyeMouth faces are never selected.
deleted_hair_faces = 0
deleted_accessory_faces = 0
for polygon in body.data.polygons:
    polygon.select = False
    center = body.matrix_world @ polygon.center
    if polygon.material_index == 2:
        total = max(poly_total_weight(body, polygon), 1.0e-6)
        tail = poly_group_score(
            body,
            polygon,
            lambda n: n.startswith("bone_hair_l_") or n.startswith("bone_hair_r_"),
        )
        if polygon.index not in retained_rear_hair_polygons and (
            center.z < 2.80 or abs(center.x) > 0.66 or center.y > 0.20 or tail / total > 0.08
        ):
            polygon.select = True
            deleted_hair_faces += 1
    elif polygon.material_index == 0:
        total = max(poly_total_weight(body, polygon), 1.0e-6)
        accessory = poly_group_score(
            body,
            polygon,
            lambda n: "bone_skirt" in n or "bone_Nameplate" in n or "bone_Pocket" in n,
        )
        torso = poly_group_score(body, polygon, lambda n: " Spine" in n or " Pelvis" in n)
        limbs = poly_group_score(
            body,
            polygon,
            lambda n: any(token in n for token in (" UpperArm", " Forearm", " Hand", " Finger", " Thigh", " Calf", " Foot", " Toe", " Neck")),
        )
        hide_tech_torso = 1.24 < center.z < 2.63 and abs(center.x) < 0.84 and torso > limbs * 0.82
        # Headset/tail hardware is body-atlas geometry, not hair material.
        if polygon.index in small_tech_face_indices or accessory / total > 0.01 or hide_tech_torso or center.z > 3.20:
            polygon.select = True
            deleted_accessory_faces += 1
deleted_total = delete_selected_faces(body)


def interpolate_anchors(value, anchors):
    if value <= anchors[0][0]:
        return anchors[0][1]
    if value >= anchors[-1][0]:
        return anchors[-1][1]
    for (left_x, left_y), (right_x, right_y) in zip(anchors, anchors[1:]):
        if left_x <= value <= right_x:
            ratio = (value - left_x) / max(right_x - left_x, 1.0e-8)
            return left_y + (right_y - left_y) * ratio
    raise RuntimeError("anchor interpolation failed")


# Proof7 neutral-leg correction.  This edits the retained, skinned donor mesh in
# rest space; vertex groups and the 118-bone armature stay intact.  Both sides
# use the same signed centerline and radial factors, guaranteeing symmetry.
leg_group_indices = {
    side: {
        token: body.vertex_groups[f"Bip001 {side} {token}"].index
        for token in ("Thigh", "Calf", "Foot", "Toe0")
    }
    for side in ("L", "R")
}
source_x_anchors = (
    (0.38, 0.3535),
    (0.54, 0.3535),
    (0.82, 0.3248),
    (1.19, 0.2362),
    (1.44, 0.2966),
    (1.55, 0.3050),
)
source_y_anchors = (
    (0.38, -0.030),
    (0.54, -0.030),
    (0.82, -0.113),
    (1.19, -0.176),
    (1.44, -0.197),
    (1.55, -0.175),
)
source_radius_x_anchors = (
    (0.38, 0.162),
    (0.54, 0.162),
    (0.82, 0.165),
    (1.19, 0.225),
    (1.44, 0.180),
    (1.55, 0.160),
)
target_radius_x_anchors = (
    (0.38, 0.180),
    (0.54, 0.190),
    (0.82, 0.200),
    (1.19, 0.220),
    (1.44, 0.200),
    (1.55, 0.170),
)
source_radius_y_anchors = (
    (0.38, 0.210),
    (0.54, 0.220),
    (0.82, 0.220),
    (1.19, 0.238),
    (1.44, 0.255),
    (1.55, 0.240),
)
target_radius_y_anchors = (
    (0.38, 0.238),
    (0.54, 0.248),
    (0.82, 0.255),
    (1.19, 0.270),
    (1.44, 0.275),
    (1.55, 0.255),
)
target_axis_x = 0.300
target_axis_y = -0.065
rest_inverse = body.matrix_world.inverted()
leg_vertices_edited = 0
foot_vertices_edited = 0
leg_vertex_indices = set()

for vertex in body.data.vertices:
    assignments = {assignment.group: assignment.weight for assignment in vertex.groups}
    side_weights = {}
    for side in ("L", "R"):
        indices = leg_group_indices[side]
        leg_weight = assignments.get(indices["Thigh"], 0.0) + assignments.get(indices["Calf"], 0.0)
        foot_weight = assignments.get(indices["Foot"], 0.0) + assignments.get(indices["Toe0"], 0.0)
        side_weights[side] = (leg_weight, foot_weight)
    side = max(side_weights, key=lambda candidate: sum(side_weights[candidate]))
    leg_weight, foot_weight = side_weights[side]
    if max(leg_weight, foot_weight) <= 0.025:
        continue

    coordinate = body.matrix_world @ vertex.co
    sign = 1.0 if side == "L" else -1.0
    if leg_weight >= foot_weight and coordinate.z >= 0.34:
        source_center_x = sign * interpolate_anchors(coordinate.z, source_x_anchors)
        source_center_y = interpolate_anchors(coordinate.z, source_y_anchors)
        source_radius_x = interpolate_anchors(coordinate.z, source_radius_x_anchors)
        target_radius_x = interpolate_anchors(coordinate.z, target_radius_x_anchors)
        source_radius_y = interpolate_anchors(coordinate.z, source_radius_y_anchors)
        target_radius_y = interpolate_anchors(coordinate.z, target_radius_y_anchors)
        normalized_x = max(-1.06, min(1.06, (coordinate.x - source_center_x) / source_radius_x))
        normalized_y = max(-1.08, min(1.08, (coordinate.y - source_center_y) / source_radius_y))
        corrected_x = sign * target_axis_x + normalized_x * target_radius_x
        corrected_y = target_axis_y + normalized_y * target_radius_y
        influence = 1.0
        coordinate.x += (corrected_x - coordinate.x) * influence
        coordinate.y += (corrected_y - coordinate.y) * influence
        leg_vertices_edited += 1
        leg_vertex_indices.add(vertex.index)
    elif foot_weight > 0.025:
        # Source shoes are already mirrored; translate both equally under the
        # new ankle axis without rotating toes or changing height.
        source_foot_axis_x = sign * 0.3534
        corrected_x = sign * target_axis_x + (coordinate.x - source_foot_axis_x)
        influence = 1.0
        coordinate.x += (corrected_x - coordinate.x) * influence
        foot_vertices_edited += 1
    else:
        continue
    vertex.co = rest_inverse @ coordinate
body.data.update()

# One restrained X/Y-only topology smoothing pass removes the donor garment's
# diamond faceting without changing knee/ankle heights or touching other body
# regions.  Mirrored topology and identical weights keep both legs symmetric.
leg_mesh = bmesh.new()
leg_mesh.from_mesh(body.data)
leg_mesh.verts.ensure_lookup_table()
bmesh.ops.smooth_vert(
    leg_mesh,
    verts=[leg_mesh.verts[index] for index in sorted(leg_vertex_indices)],
    factor=0.16,
    use_axis_x=True,
    use_axis_y=True,
    use_axis_z=False,
)
leg_mesh.to_mesh(body.data)
leg_mesh.free()
body.data.update()

# Add player clothing materials to the same skinned mesh and directly reassign
# surviving donor body polygons by original rig weights and surface position.
material_slots = {}
for material in (mat_skin, mat_hoodie, mat_hoodie_shadow, mat_navy, mat_yellow, mat_striped, mat_shoe, mat_red):
    body.data.materials.append(material)
    material_slots[material.name] = len(body.data.materials) - 1


def score(poly, tokens):
    return poly_group_score(body, poly, lambda name: any(token in name for token in tokens))


for polygon in body.data.polygons:
    if polygon.material_index != 0:
        continue
    center = body.matrix_world @ polygon.center
    hand = score(polygon, (" Hand", " Finger"))
    foot = score(polygon, (" Foot", " Toe"))
    leg = score(polygon, (" Thigh", " Calf"))
    arm = score(polygon, (" UpperArm", " Forearm", "ForeArm Twist", "Deltoid", " Clavicle"))
    neck = score(polygon, (" Neck",))
    pelvis = score(polygon, (" Pelvis",))
    torso = score(polygon, (" Spine",))
    values = {"hand": hand, "foot": foot, "leg": leg, "arm": arm, "neck": neck, "pelvis": pelvis, "torso": torso}
    dominant = max(values, key=values.get)
    if dominant in {"hand", "neck"}:
        material = mat_skin
    elif dominant == "foot":
        if center.z > 0.29:
            material = mat_navy
        elif center.z < 0.075:
            material = mat_navy
        elif center.y < -0.11 and center.z < 0.29:
            material = mat_red
        else:
            material = mat_shoe
    elif dominant in {"leg", "pelvis"} or center.z < 1.73:
        material = mat_navy
    elif dominant == "arm":
        material = mat_hoodie
    elif center.y < -0.15 and abs(center.x) < 0.39 and 1.79 < center.z < 2.43:
        material = mat_striped
    else:
        material = mat_hoodie
    polygon.material_index = material_slots[material.name]

for polygon in body.data.polygons:
    polygon.use_smooth = True

# Clean clothing overlays replace the hidden donor-tech torso while the
# original skinned arms/hands/legs remain underneath and fully rigged.
shirt = add_loft(
    "PlayerV5_CleanStripedShirt",
    (
        (1.72, 0.40, 0.225, 0.015),
        (1.80, 0.45, 0.250, 0.005),
        (2.18, 0.46, 0.268, -0.005),
        (2.28, 0.47, 0.270, -0.003),
        (2.31, 0.475, 0.272, -0.002),
        (2.36, 0.48, 0.273, -0.001),
        (2.39, 0.485, 0.274, 0.000),
        (2.42, 0.49, 0.275, 0.000),
        (2.50, 0.34, 0.220, 0.030),
    ),
    mat_striped,
)
# Use the same curved torso surface for the rear hoodie.  Extra close Z rings
# above make a thin, completely flush navy/coral/navy accent rather than boxes.
shirt.data.materials.append(mat_hoodie)
shirt.data.materials.append(mat_navy)
shirt.data.materials.append(mat_coral)
shirt_hoodie_index = 1
shirt_navy_index = 2
shirt_coral_index = 3
for polygon in shirt.data.polygons:
    center = polygon.center
    if center.y <= 0.018:
        continue
    if 2.28 <= center.z < 2.31 or 2.36 <= center.z < 2.39:
        polygon.material_index = shirt_navy_index
    elif 2.31 <= center.z < 2.36:
        polygon.material_index = shirt_coral_index
    else:
        polygon.material_index = shirt_hoodie_index
left_panel = add_uv("PlayerV5_HoodiePanel_L", (-0.36, -0.015, 2.08), (0.245, 0.235, 0.48), mat_hoodie)
right_panel = add_uv("PlayerV5_HoodiePanel_R", (0.36, -0.015, 2.08), (0.245, 0.235, 0.48), mat_hoodie)
for side in (-1.0, 1.0):
    add_curve(
        f"PlayerV5_HoodieZipper_{'L' if side < 0 else 'R'}",
        ((0.145 * side, -0.267, 1.76), (0.155 * side, -0.276, 2.08), (0.165 * side, -0.258, 2.40)),
        mat_navy,
        0.009,
    )

# Constant-depth hip loft overlaps both retained donor thighs; unlike a sphere,
# its lower cross-section does not collapse into a visible pants gap.
waist = add_loft(
    "PlayerV5_PantsWaist",
    (
        (1.30, 0.48, 0.165, 0.005),
        (1.38, 0.50, 0.175, 0.000),
        (1.50, 0.48, 0.180, 0.000),
        (1.68, 0.45, 0.180, 0.000),
        (1.78, 0.40, 0.165, 0.005),
    ),
    mat_navy,
)

# Remove Proof2's tech cuff shards but bridge the retained donor hands back to
# the large hoodie sleeves with clean rounded cuffs.
for side in (-1.0, 1.0):
    add_uv(
        f"PlayerV5_CleanCuff_{'L' if side < 0 else 'R'}",
        (1.055 * side, -0.010, 1.725),
        (0.145, 0.105, 0.105),
        mat_hoodie,
        44,
        28,
    )

# Hood volume is an actual 3D collar behind the retained jacket topology.
hood = add_torus("PlayerV5_Hood", (0.0, 0.10, 2.52), 0.39, 0.105, (1.0, 0.78, 1.0), mat_hoodie)

# Red newsboy cap: rounded crown, soft band, broad brim, panel seams and pin.
cap_parts = []
cap_parts.append(add_uv("PlayerV5_CapCrown", (0.0, 0.035, 3.84), (0.49, 0.40, 0.20), mat_red))
cap_parts.append(add_uv("PlayerV5_CapBrim", (0.0, -0.390, 3.675), (0.31, 0.18, 0.035), mat_red))
cap_parts[-1].rotation_euler.x = math.radians(-5.0)
cap_parts.append(add_uv("PlayerV5_CapButton", (0.0, 0.010, 4.055), (0.040, 0.040, 0.030), mat_red_dark, 40, 24))

# Replace the deleted white mouth plate with one shallow smile curve that hugs
# the original face surface.  This is volume, not a front-only flat patch.
mouth_curve = add_curve(
    "PlayerV5_RestoredSmile",
    (
        (-0.060, -0.400, 2.704),
        (-0.027, -0.402, 2.684),
        (0.0, -0.403, 2.678),
        (0.027, -0.402, 2.684),
        (0.060, -0.400, 2.704),
    ),
    mat_red_dark,
    0.005,
)

# Review scene.
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
scene.render.image_settings.color_depth = "8"
scene.render.film_transparent = False
scene.render.use_freestyle = True
scene.render.line_thickness = 0.66
freestyle = scene.view_layers[0].freestyle_settings
line_style = freestyle.linesets[0].linestyle
if line_style is not None:
    line_style.color = (0.055, 0.025, 0.020)
    line_style.thickness = 0.66

scene.world = bpy.data.worlds.new("PlayerV5World")
scene.world.use_nodes = True
background = scene.world.node_tree.nodes.get("Background")
background.inputs["Color"].default_value = (0.050, 0.070, 0.105, 1.0)
background.inputs["Strength"].default_value = 0.42
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.22
except TypeError:
    pass


def add_area(name, location, energy, color, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0.0, 0.0, 2.15)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


add_area("PlayerV5_Key", (-4.2, -5.3, 6.5), 1120.0, (1.0, 0.82, 0.70), 4.8)
add_area("PlayerV5_Fill", (4.5, -3.8, 4.8), 820.0, (0.68, 0.83, 1.0), 4.2)
add_area("PlayerV5_Rim", (0.0, 4.5, 5.8), 960.0, (1.0, 0.72, 0.58), 4.0)
add_area("PlayerV5_Top", (0.0, 0.0, 7.2), 480.0, (1.0, 0.95, 0.88), 3.5)

bpy.ops.mesh.primitive_plane_add(size=30.0, location=(0.0, 0.0, -0.018))
ground = bpy.context.object
ground.name = "PlayerV5_ReviewGround"
ground.data.materials.append(principled_material("PlayerV5_Ground", (0.075, 0.095, 0.135, 1.0), 0.78))

camera_data = bpy.data.cameras.new("PlayerV5_ProofCamera")
camera = bpy.data.objects.new("PlayerV5_ProofCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 4.72
scene.camera = camera


def point_camera(location, target=(0.0, 0.0, 2.08)):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


view_paths = []
views = {
    "front": (0.0, -8.4, 2.12),
    "three-quarter": (5.9, -5.9, 2.15),
    "side": (8.4, 0.0, 2.12),
    "back": (0.0, 8.4, 2.12),
}
for name, location in views.items():
    point_camera(location)
    path = OUTPUT / f"player-human-v5-{name}-{PROOF_TAG}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    view_paths.append(path)

blend_path = OUTPUT / f"player-human-v5-{PROOF_TAG}.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

face_polygons = sum(1 for p in body.data.polygons if body.data.materials[p.material_index] == mat_face)
eye_polygons = sum(1 for p in body.data.polygons if body.data.materials[p.material_index] == mat_eye)
receipt = {
    "schema": "family-company.player-human-v5-proof.v8",
    "status": "REQUIRES_VISUAL_REVIEW",
    "source": {
        "fbx": str(SOURCE_FBX.relative_to(REPO)).replace("\\", "/"),
        "sha256": sha256(SOURCE_FBX),
        "userAttestation": "test2/Yuuka is user-owned for this private prototype",
        "test3Sakurako": "EXCLUDED_NOT_OPENED",
    },
    "retained": {
        "bodyObject": body.name,
        "armatureObject": armature.name,
        "rigBones": len(armature.data.bones),
        "facePolygons": face_polygons,
        "eyeMouthPolygons": eye_polygons,
        "faceTopologyRetained": face_polygons > 0,
        "eyeMouthEyeTopologyRetained": eye_polygons > 0,
        "removedKnownOpaqueMouthPlateOnly": removed_mouth_polygon_count == 32 and removed_mouth_vertex_count == 25,
        "armatureModifierRetained": any(m.type == "ARMATURE" for m in body.modifiers),
    },
    "edits": {
        "deletedHairFaces": deleted_hair_faces,
        "deletedAccessoryFaces": deleted_accessory_faces,
        "deletedTotalFaces": deleted_total,
        "removedOpaqueMouthPlatePolygons": removed_mouth_polygon_count,
        "removedOpaqueMouthPlateVertices": removed_mouth_vertex_count,
        "eyeMouthAlpha": "RGB max-channel black-key; source PNG alpha ignored because it is fully opaque",
        "eyeColor": "brown hue remap in retained EyeMouth shader",
        "rearHair": "compressed retained donor 216-polygon scalp plus directly cropped bone_hair_b_* locks; twin tails removed; no procedural sphere or cards",
        "rearHoodie": "rear polygons of the curved torso loft; flush white surface with navy/coral/navy horizontal stripe",
        "neutralLegs": {
            "editedSkinnedLegVertices": leg_vertices_edited,
            "editedSkinnedFootVertices": foot_vertices_edited,
            "targetAxisAbsoluteX": target_axis_x,
            "targetAxisY": target_axis_y,
            "targetRadialIncrease": "calf volume retained near requested range; upper thigh tapered after Proof7 QA",
            "correctiveSmooth": "one 0.16 factor X/Y-only pass on leg-group vertices; Z locked",
            "feet": "equal signed translation to the symmetric ankle axes; toe direction and height unchanged",
        },
        "identity": ["short brown donor hair", "red newsboy cap", "white hoodie", "striped navy/yellow shirt", "navy pants", "white/red sneakers"],
    },
    "views": [str(path.relative_to(REPO)).replace("\\", "/") for path in view_paths],
    "blend": str(blend_path.relative_to(REPO)).replace("\\", "/"),
}
receipt_path = OUTPUT / f"player-human-v5-{PROOF_TAG}-receipt.json"
receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("PLAYER_HUMAN_V5_RECEIPT=" + str(receipt_path))
