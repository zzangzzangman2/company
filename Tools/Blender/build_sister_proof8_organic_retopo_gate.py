"""Build Sister Proof8 as one organic donor-guided skin retopology.

Approved user-owned Yuuka face, eyes, long hair, native three-digit hands and
118-bone rig remain unchanged.  Rejected procedural body parts are removed.
Temporary anatomical construction volumes are voxel-joined into one smooth
skin surface; fitted tank and shorts shells are extracted from that same skin
surface.  Construction volumes are deleted and never rendered.  Static QA only.
"""

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
from mathutils.kdtree import KDTree


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--version", choices=("8", "9", "10"), default="8")
    return parser.parse_args(argv)


ARGS = parse_args()
IS_PROOF10 = ARGS.version == "10"
IS_PROOF9 = ARGS.version in {"9", "10"}
PROOF_NUMBER = ARGS.version
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
donor_body = bpy.data.objects.get("Yuuka_Original_Body")
camera = bpy.data.objects.get("SisterProofCamera")
mouth = bpy.data.objects.get("NormalizedMouthCurve")
if any(obj is None for obj in (armature, donor_body, camera, mouth)):
    raise RuntimeError("Expected Sister Proof3 armature/body/camera/mouth")
if len(armature.data.bones) != 118:
    raise RuntimeError("Owned Yuuka rig must remain exactly 118 bones")

body_hash_before = coordinate_hash(donor_body)
bone_names_before = sorted(bone.name for bone in armature.data.bones)


# Remove rejected Proof3/4 pieces and any later proof objects if the script is
# rerun from a diagnostic blend.  The retained donor body is explicitly exempt.
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
    "SisterProof5_",
    "SisterProof6Floor",
    "SisterProof7Floor",
    "SisterProof8_",
)
for obj in list(bpy.data.objects):
    if obj is donor_body:
        continue
    if obj.name.startswith(remove_prefixes):
        bpy.data.objects.remove(obj, do_unlink=True)


def make_material(name, color, roughness=0.72):
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


MAT_SKIN = make_material("SisterProof8Skin", (0.91, 0.70, 0.62), 0.74)
MAT_TANK = make_material("SisterProof8BlackTank", (0.022, 0.021, 0.031), 0.82)
MAT_GRAY = make_material("SisterProof8QAGray", (0.56, 0.59, 0.64), 0.84)


def make_shorts_material():
    result = bpy.data.materials.get("SisterProof8NavyShortsWithPiping") or bpy.data.materials.new(
        "SisterProof8NavyShortsWithPiping"
    )
    result.use_nodes = True
    nodes = result.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.78
    geometry = nodes.new("ShaderNodeNewGeometry")
    separate = nodes.new("ShaderNodeSeparateXYZ")
    threshold = nodes.new("ShaderNodeMath")
    threshold.operation = "GREATER_THAN"
    threshold.inputs[1].default_value = 0.318
    mix = nodes.new("ShaderNodeMixRGB")
    mix.inputs[1].default_value = (0.90, 0.93, 0.97, 1.0)
    mix.inputs[2].default_value = (0.030, 0.090, 0.215, 1.0)
    result.node_tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    result.node_tree.links.new(separate.outputs["Z"], threshold.inputs[0])
    result.node_tree.links.new(threshold.outputs[0], mix.inputs[0])
    result.node_tree.links.new(mix.outputs[0], shader.inputs["Base Color"])
    result.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return result


MAT_SHORTS = make_shorts_material()


def make_unified_body_style_material():
    """Material-only clothing on one skin surface; no cut cloth boundaries."""
    result = bpy.data.materials.get("SisterProof9UnifiedBodyStyle") or bpy.data.materials.new(
        "SisterProof9UnifiedBodyStyle"
    )
    result.use_nodes = True
    nodes = result.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.76
    geometry = nodes.new("ShaderNodeNewGeometry")
    separate = nodes.new("ShaderNodeSeparateXYZ")
    absolute_x = nodes.new("ShaderNodeMath")
    absolute_x.operation = "ABSOLUTE"

    def compare(operation, threshold):
        node = nodes.new("ShaderNodeMath")
        node.operation = operation
        node.inputs[1].default_value = threshold
        return node

    def multiply(left, right):
        node = nodes.new("ShaderNodeMath")
        node.operation = "MULTIPLY"
        result.node_tree.links.new(left, node.inputs[0])
        result.node_tree.links.new(right, node.inputs[1])
        return node.outputs[0]

    def maximum(left, right):
        node = nodes.new("ShaderNodeMath")
        node.operation = "MAXIMUM"
        result.node_tree.links.new(left, node.inputs[0])
        result.node_tree.links.new(right, node.inputs[1])
        return node.outputs[0]

    x_under_torso = compare("LESS_THAN", 0.136)
    x_under_strap = compare("LESS_THAN", 0.108)
    x_over_neck = compare("GREATER_THAN", 0.050)
    z_tank_bottom = compare("GREATER_THAN", 0.402)
    z_tank_body_top = compare("LESS_THAN", 0.552)
    z_strap_bottom = compare("GREATER_THAN", 0.542)
    z_strap_top = compare("LESS_THAN", 0.596)
    z_shorts_bottom = compare("GREATER_THAN", 0.300)
    z_piping_top = compare("LESS_THAN", 0.316)
    z_shorts_top = compare("LESS_THAN", 0.418)
    x_shorts = compare("LESS_THAN", 0.145)

    tree = result.node_tree
    tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    tree.links.new(separate.outputs["X"], absolute_x.inputs[0])
    for node in (x_under_torso, x_under_strap, x_over_neck, x_shorts):
        tree.links.new(absolute_x.outputs[0], node.inputs[0])
    for node in (
        z_tank_bottom,
        z_tank_body_top,
        z_strap_bottom,
        z_strap_top,
        z_shorts_bottom,
        z_piping_top,
        z_shorts_top,
    ):
        tree.links.new(separate.outputs["Z"], node.inputs[0])

    tank_body = multiply(z_tank_bottom.outputs[0], z_tank_body_top.outputs[0])
    tank_body = multiply(tank_body, x_under_torso.outputs[0])
    tank_straps = multiply(z_strap_bottom.outputs[0], z_strap_top.outputs[0])
    tank_straps = multiply(tank_straps, x_under_strap.outputs[0])
    tank_straps = multiply(tank_straps, x_over_neck.outputs[0])
    tank_mask = maximum(tank_body, tank_straps)

    shorts_mask = multiply(z_shorts_bottom.outputs[0], z_shorts_top.outputs[0])
    shorts_mask = multiply(shorts_mask, x_shorts.outputs[0])
    piping_mask = multiply(z_shorts_bottom.outputs[0], z_piping_top.outputs[0])
    piping_mask = multiply(piping_mask, x_shorts.outputs[0])

    skin_to_tank = nodes.new("ShaderNodeMixRGB")
    skin_to_tank.inputs[1].default_value = (0.91, 0.70, 0.62, 1.0)
    skin_to_tank.inputs[2].default_value = (0.022, 0.021, 0.031, 1.0)
    tree.links.new(tank_mask, skin_to_tank.inputs[0])
    tank_to_shorts = nodes.new("ShaderNodeMixRGB")
    tank_to_shorts.inputs[2].default_value = (0.030, 0.090, 0.215, 1.0)
    tree.links.new(skin_to_tank.outputs[0], tank_to_shorts.inputs[1])
    tree.links.new(shorts_mask, tank_to_shorts.inputs[0])
    piping = nodes.new("ShaderNodeMixRGB")
    piping.inputs[2].default_value = (0.90, 0.93, 0.97, 1.0)
    tree.links.new(tank_to_shorts.outputs[0], piping.inputs[1])
    tree.links.new(piping_mask, piping.inputs[0])
    tree.links.new(piping.outputs[0], shader.inputs["Base Color"])
    tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return result


MAT_UNIFIED_STYLE = make_unified_body_style_material()


def make_vertex_style_material():
    result = bpy.data.materials.get("SisterProof10UnifiedVertexStyle") or bpy.data.materials.new(
        "SisterProof10UnifiedVertexStyle"
    )
    result.use_nodes = True
    nodes = result.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.76
    vertex_color = nodes.new("ShaderNodeVertexColor")
    vertex_color.layer_name = "SisterProof10StyleColor"
    result.node_tree.links.new(vertex_color.outputs["Color"], shader.inputs["Base Color"])
    result.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return result


MAT_VERTEX_STYLE = make_vertex_style_material()


def recalc_normals(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def finish_raw(name, vertices, faces):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=False)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    recalc_normals(obj)
    return obj


def loft_volume(name, rings, segments=40):
    vertices = []
    for z, cx, cy, rx, ry in rings:
        for segment in range(segments):
            angle = math.tau * segment / segments
            vertices.append((cx + rx * math.cos(angle), cy + ry * math.sin(angle), z))
    faces = []
    for ring_index in range(len(rings) - 1):
        first = ring_index * segments
        second = (ring_index + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
    bottom = len(vertices)
    vertices.append((rings[0][1], rings[0][2], rings[0][0]))
    top = len(vertices)
    vertices.append((rings[-1][1], rings[-1][2], rings[-1][0]))
    last = (len(rings) - 1) * segments
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((bottom, nxt, segment))
        faces.append((top, last + segment, last + nxt))
    return finish_raw(name, vertices, faces)


def path_frame(tangent):
    tangent = tangent.normalized()
    reference = Vector((0.0, 1.0, 0.0))
    if abs(tangent.dot(reference)) > 0.90:
        reference = Vector((0.0, 0.0, 1.0))
    axis_a = tangent.cross(reference).normalized()
    axis_b = tangent.cross(axis_a).normalized()
    return axis_a, axis_b


def tube_volume(name, points, radii, segments=28):
    points = [Vector(point) for point in points]
    vertices = []
    for index, (point, radius) in enumerate(zip(points, radii)):
        if index == 0:
            tangent = points[1] - points[0]
        elif index == len(points) - 1:
            tangent = points[-1] - points[-2]
        else:
            tangent = points[index + 1] - points[index - 1]
        axis_a, axis_b = path_frame(tangent)
        for segment in range(segments):
            angle = math.tau * segment / segments
            vertices.append(
                tuple(
                    point
                    + axis_a * (math.cos(angle) * radius[0])
                    + axis_b * (math.sin(angle) * radius[1])
                )
            )
    faces = []
    for ring_index in range(len(points) - 1):
        first = ring_index * segments
        second = (ring_index + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
    start = len(vertices)
    vertices.append(tuple(points[0]))
    end = len(vertices)
    vertices.append(tuple(points[-1]))
    last = (len(points) - 1) * segments
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((start, nxt, segment))
        faces.append((end, last + segment, last + nxt))
    return finish_raw(name, vertices, faces)


def foot_volume(name, side):
    # A rounded asymmetrical bare foot.  Negative Y is the toe direction.
    segments = 32
    rings = 14
    vertices = []
    for ring in range(1, rings):
        phi = -math.pi / 2.0 + math.pi * ring / rings
        z_sin = math.sin(phi)
        radial = math.cos(phi)
        for segment in range(segments):
            theta = math.tau * segment / segments
            direction_y = math.sin(theta)
            if IS_PROOF10:
                y_radius = 0.073 if direction_y < 0.0 else 0.040
                base_x_radius = 0.037
            elif IS_PROOF9:
                y_radius = 0.074 if direction_y < 0.0 else 0.040
                base_x_radius = 0.035
            else:
                y_radius = 0.082 if direction_y < 0.0 else 0.045
                base_x_radius = 0.043
            x_radius = base_x_radius * (1.0 + 0.08 * max(0.0, -direction_y))
            x = 0.064 * side + x_radius * radial * math.cos(theta)
            y = -0.010 + y_radius * radial * direction_y
            center_z = 0.022 if IS_PROOF10 else (0.028 if IS_PROOF9 else 0.034)
            radius_z = 0.020 if IS_PROOF10 else (0.026 if IS_PROOF9 else 0.034)
            z = center_z + radius_z * z_sin
            z = max(0.002, z)
            vertices.append((x, y, z))
    bottom = len(vertices)
    vertices.append((0.064 * side, -0.010, 0.002))
    top = len(vertices)
    vertices.append(
        (0.064 * side, -0.010, 0.044 if IS_PROOF10 else (0.054 if IS_PROOF9 else 0.068))
    )
    faces = []
    for ring in range(rings - 2):
        first = ring * segments
        second = (ring + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
    first = 0
    last = (rings - 2) * segments
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((bottom, first + nxt, first + segment))
        faces.append((top, last + segment, last + nxt))
    return finish_raw(name, vertices, faces)


# Four-head cute-adult guide: modest shoulders/waist/hips, straight legs with a
# knee break, tapered forearms and small rounded feet.  Every construction part
# overlaps before a single final voxel union.
if IS_PROOF10:
    torso_rings = (
        (0.330, 0.0, -0.002, 0.086, 0.061),
        (0.375, 0.0, -0.004, 0.106, 0.070),
        (0.430, 0.0, -0.006, 0.095, 0.064),
        (0.480, 0.0, -0.006, 0.081, 0.059),
        (0.535, 0.0, -0.005, 0.095, 0.063),
        (0.575, 0.0, -0.003, 0.121, 0.067),
        (0.605, 0.0, -0.001, 0.080, 0.053),
        (0.642, 0.0, 0.000, 0.043, 0.039),
    )
    arm_radii = ((0.042, 0.041), (0.038, 0.037), (0.032, 0.031), (0.029, 0.028), (0.027, 0.027))
    leg_radii = ((0.046, 0.052), (0.043, 0.048), (0.038, 0.042), (0.035, 0.038), (0.038, 0.037), (0.027, 0.029))
elif IS_PROOF9:
    torso_rings = (
        (0.330, 0.0, -0.002, 0.090, 0.064),
        (0.375, 0.0, -0.004, 0.112, 0.074),
        (0.430, 0.0, -0.006, 0.100, 0.067),
        (0.480, 0.0, -0.006, 0.086, 0.062),
        (0.535, 0.0, -0.005, 0.100, 0.066),
        (0.575, 0.0, -0.003, 0.127, 0.070),
        (0.605, 0.0, -0.001, 0.083, 0.055),
        (0.642, 0.0, 0.000, 0.044, 0.040),
    )
    arm_radii = ((0.045, 0.044), (0.041, 0.040), (0.035, 0.034), (0.032, 0.031), (0.029, 0.029))
    leg_radii = ((0.052, 0.058), (0.048, 0.053), (0.043, 0.047), (0.039, 0.043), (0.043, 0.041), (0.031, 0.033))
else:
    torso_rings = (
        (0.330, 0.0, -0.002, 0.104, 0.071),
        (0.375, 0.0, -0.004, 0.127, 0.082),
        (0.430, 0.0, -0.006, 0.112, 0.073),
        (0.480, 0.0, -0.006, 0.098, 0.068),
        (0.535, 0.0, -0.005, 0.112, 0.073),
        (0.575, 0.0, -0.003, 0.143, 0.077),
        (0.605, 0.0, -0.001, 0.094, 0.061),
        (0.642, 0.0, 0.000, 0.047, 0.043),
    )
    arm_radii = ((0.057, 0.053), (0.052, 0.050), (0.043, 0.041), (0.039, 0.038), (0.034, 0.033))
    leg_radii = ((0.064, 0.070), (0.059, 0.064), (0.052, 0.057), (0.046, 0.050), (0.050, 0.048), (0.037, 0.039))

construction = []
construction.append(
    loft_volume(
        "QA_SisterProof8_Torso",
        torso_rings,
        48,
    )
)
for side in (-1.0, 1.0):
    construction.append(
        tube_volume(
            f"QA_SisterProof8_Arm_{side:+.0f}",
            (
                (0.112 * side, -0.002, 0.575),
                (0.145 * side, -0.003, 0.555),
                (0.190 * side, -0.004, 0.515),
                (0.225 * side, -0.004, 0.477),
                (0.273 * side, -0.004, 0.432),
            ),
            arm_radii,
            32,
        )
    )
    construction.append(
        tube_volume(
            f"QA_SisterProof8_Leg_{side:+.0f}",
            (
                (0.070 * side, -0.003, 0.382),
                (0.067 * side, -0.004, 0.320),
                (0.064 * side, -0.004, 0.250),
                (0.063 * side, -0.003, 0.205),
                (0.064 * side, -0.002, 0.135),
                (
                    0.064 * side,
                    -0.002,
                    0.042 if IS_PROOF10 else (0.052 if IS_PROOF9 else 0.066),
                ),
            ),
            leg_radii,
            32,
        )
    )
    construction.append(foot_volume(f"QA_SisterProof8_Foot_{side:+.0f}", side))


# Join and remesh every anatomical construction part into one final skin mesh.
bpy.ops.object.select_all(action="DESELECT")
for obj in construction:
    obj.select_set(True)
bpy.context.view_layer.objects.active = construction[0]
bpy.ops.object.join()
skin = construction[0]
skin.name = "SisterProof8_OrganicSkin_OneConnectedSurface"
skin.data.remesh_voxel_size = 0.0055
skin.data.remesh_voxel_adaptivity = 0.0
bpy.context.view_layer.objects.active = skin
bpy.ops.object.voxel_remesh()
recalc_normals(skin)

smooth = bmesh.new()
smooth.from_mesh(skin.data)
for _ in range(5 if IS_PROOF10 else 3):
    bmesh.ops.smooth_vert(
        smooth,
        verts=list(smooth.verts),
        factor=0.22 if IS_PROOF10 else 0.20,
        use_axis_x=True,
        use_axis_y=True,
        use_axis_z=True,
    )
smooth.to_mesh(skin.data)
smooth.free()
skin.data.update()
recalc_normals(skin)
skin.data.materials.append(MAT_SKIN)
for polygon in skin.data.polygons:
    polygon.material_index = 0
    polygon.use_smooth = True


# Transfer the original Yuuka rest-pose weights from all retained source
# vertices, including vertices whose rejected clothing faces were removed.
source_kd = KDTree(len(donor_body.data.vertices))
for vertex in donor_body.data.vertices:
    source_kd.insert(donor_body.matrix_world @ vertex.co, vertex.index)
source_kd.balance()
for group in donor_body.vertex_groups:
    skin.vertex_groups.new(name=group.name)
group_names = {group.index: group.name for group in donor_body.vertex_groups}
for vertex in skin.data.vertices:
    world = skin.matrix_world @ vertex.co
    _, source_index, _ = source_kd.find(world)
    for membership in donor_body.data.vertices[source_index].groups:
        skin.vertex_groups[group_names[membership.group]].add(
            [vertex.index], membership.weight, "REPLACE"
        )
modifier = skin.modifiers.new("OwnedYuuka118BoneRig", "ARMATURE")
modifier.object = armature
world = skin.matrix_world.copy()
skin.parent = armature
skin.matrix_parent_inverse = armature.matrix_world.inverted()
skin.matrix_world = world
skin["proofRevision"] = f"SisterProof{PROOF_NUMBER}OrganicRetopoGate"
skin["candidateClaim"] = False


def delete_vertices(obj, indices):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    mesh.verts.ensure_lookup_table()
    targets = [mesh.verts[index] for index in sorted(indices) if index < len(mesh.verts)]
    bmesh.ops.delete(mesh, geom=targets, context="VERTS")
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def duplicate_region(name, predicate, material_item, offset, transform=None):
    obj = skin.copy()
    obj.data = skin.data.copy()
    obj.name = name
    bpy.context.collection.objects.link(obj)
    remove = [
        vertex.index
        for vertex in obj.data.vertices
        if not predicate(obj.matrix_world @ vertex.co)
    ]
    delete_vertices(obj, remove)
    if transform is not None:
        inverse = obj.matrix_world.inverted()
        for vertex in obj.data.vertices:
            vertex.co = inverse @ Vector(transform(obj.matrix_world @ vertex.co))
        obj.data.update()
    # Offset along retained organic-skin normals so cloth follows the body.
    recalc_normals(obj)
    bpy.context.view_layer.update()
    for vertex in obj.data.vertices:
        vertex.co += vertex.normal * offset
    obj.data.update()
    recalc_normals(obj)
    obj.data.materials.clear()
    obj.data.materials.append(material_item)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = True
    smooth_modifier = obj.modifiers.new("SisterProof8_ClothBoundarySmooth", "SMOOTH")
    smooth_modifier.factor = 0.16
    smooth_modifier.iterations = 2
    solidify = obj.modifiers.new("SisterProof8_ClothThickness", "SOLIDIFY")
    solidify.thickness = 0.004
    solidify.offset = 0.0
    solidify.use_even_offset = True
    obj["proofRevision"] = f"SisterProof{PROOF_NUMBER}OrganicRetopoGate"
    obj["candidateClaim"] = False
    return obj


def tank_region(point):
    if not (0.405 <= point.z <= 0.592 and abs(point.x) <= 0.130):
        return False
    if point.z <= 0.548:
        return True
    return 0.052 <= abs(point.x) <= 0.115


def clean_tank_fit(point):
    point = Vector(point)
    # Mild waist fit and a curved lower hem; no box slab or separate straps.
    if point.z < 0.445:
        point.x *= 0.96
    return point


tank = None
if not IS_PROOF9:
    tank = duplicate_region(
        "SisterProof8_FittedTank_ExtractedOrganicSurface",
        tank_region,
        MAT_TANK,
        0.0045,
        clean_tank_fit,
    )


def shorts_region(point):
    return 0.302 <= point.z <= 0.418 and abs(point.x) <= 0.142


def shorts_fit(point):
    point = Vector(point)
    lower_t = max(0.0, min(1.0, (0.418 - point.z) / (0.418 - 0.302)))
    point.x *= 1.035 + 0.045 * lower_t
    point.y = -0.004 + (point.y + 0.004) * (1.025 + 0.030 * lower_t)
    return point


shorts = None
if not IS_PROOF9:
    shorts = duplicate_region(
        "SisterProof8_FittedShorts_ExtractedOrganicSurface",
        shorts_region,
        MAT_SHORTS,
        0.006,
        shorts_fit,
    )
else:
    skin.data.materials.clear()
    skin.data.materials.append(MAT_VERTEX_STYLE if IS_PROOF10 else MAT_UNIFIED_STYLE)
    for polygon in skin.data.polygons:
        polygon.material_index = 0
    if IS_PROOF10:
        style = skin.data.color_attributes.get("SisterProof10StyleColor")
        if style is None:
            style = skin.data.color_attributes.new(
                name="SisterProof10StyleColor",
                type="BYTE_COLOR",
                domain="CORNER",
            )
        skin_color = (0.91, 0.70, 0.62, 1.0)
        tank_color = (0.022, 0.021, 0.031, 1.0)
        shorts_color = (0.030, 0.090, 0.215, 1.0)
        piping_color = (0.90, 0.93, 0.97, 1.0)
        for loop in skin.data.loops:
            point = skin.matrix_world @ skin.data.vertices[loop.vertex_index].co
            absolute_x = abs(point.x)
            color = skin_color
            neckline = 0.545 + 4.20 * absolute_x * absolute_x
            if 0.402 <= point.z <= neckline and absolute_x <= 0.130:
                color = tank_color
            shorts_hem = 0.300 + 1.80 * absolute_x * absolute_x
            if shorts_hem <= point.z <= 0.418 and absolute_x <= 0.145:
                color = shorts_color
            if shorts_hem <= point.z <= shorts_hem + 0.009 and absolute_x <= 0.145:
                color = piping_color
            style.data[loop.index].color = color


# Narrow the already surface-attached mouth without changing the donor face.
mouth.scale.x *= 0.58
mouth["proof8MouthAdjustment"] = "existing surface-attached curve width reduced only"


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


visible_retopology = [skin] if IS_PROOF9 else [skin, tank, shorts]
stats = {obj.name: topology_stats(obj) for obj in visible_retopology}
if stats[skin.name]["connectedComponents"] != 1:
    raise RuntimeError(f"Organic skin is not one connected surface: {stats[skin.name]}")
if stats[skin.name]["boundaryEdges"] != 0:
    raise RuntimeError(f"Organic skin is not closed: {stats[skin.name]}")


bpy.ops.mesh.primitive_plane_add(size=10.0, location=(0.0, 0.0, -0.001))
floor = bpy.context.object
floor.name = "SisterProof8Floor"
floor.data.materials.append(make_material("SisterProof8FloorMaterial", (0.040, 0.052, 0.072), 0.92))

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
    path = OUTPUT / f"sister-proof{PROOF_NUMBER}-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    color_outputs.append(path)

material_backup = {obj.name: list(obj.data.materials) for obj in visible_retopology}
for obj in visible_retopology:
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
gray_outputs = []
for label, yaw in views:
    point_camera(yaw)
    path = OUTPUT / f"sister-proof{PROOF_NUMBER}-{label}-gray-silhouette.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    gray_outputs.append(path)
for obj in visible_retopology:
    obj.data.materials.clear()
    for item in material_backup[obj.name]:
        obj.data.materials.append(item)
    for polygon in obj.data.polygons:
        polygon.material_index = 0

body_hash_after = coordinate_hash(donor_body)
if body_hash_after != body_hash_before:
    raise RuntimeError("Owned Yuuka face/hair/hand coordinates changed")
if sorted(bone.name for bone in armature.data.bones) != bone_names_before:
    raise RuntimeError("Owned Yuuka rig bone names changed")

blend_path = OUTPUT / f"sister-proof{PROOF_NUMBER}-organic-retopo-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

receipt = {
    "schema": f"family-company.sister-proof{PROOF_NUMBER}-organic-retopo-gate.v1",
    "status": "AWAITING_ROOT_UNIFIED_STYLE_GATE" if IS_PROOF9 else "AWAITING_ROOT_ORGANIC_RETOPO_GATE",
    "candidateClaim": False,
    "sourceBlend": str(SOURCE_BLEND),
    "sourceBlendSha256": sha256(SOURCE_BLEND),
    "sourceBasis": "user-owned test2 Yuuka Sister Proof3 donor face/hair/hands and rig",
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
    "organicRetopology": {
        "visibleSkinSurfaces": 1,
        "visiblePrimitiveStacking": 0,
        "constructionObjectsRemaining": 0,
        "skinMethod": "one high-resolution voxel union of overlapping anatomical loft/tube guides, smoothed and donor-weighted",
        "tankMethod": (
            "material-only tank mask on the one organic skin surface"
            if IS_PROOF9
            else "surface region extracted from the same organic skin, offset and solidified"
        ),
        "shortsMethod": (
            "material-only shorts mask on the one organic skin surface"
            if IS_PROOF9
            else "surface region extracted from the same organic skin, fitted, offset and solidified"
        ),
        "pipingGeometry": 0,
        "pipingMethod": "shader boundary on the shorts surface",
    },
    "topology": stats,
    "colorStaticViews": [path.name for path in color_outputs],
    "graySilhouetteViews": [path.name for path in gray_outputs],
    "gifCreated": False,
    "blend": blend_path.name,
    "limitations": [
        "internal static organic-retopology QA only",
        "root visual approval is required before animation, GIF or Unity",
        "animation deformation has not been tested",
    ],
}
with (OUTPUT / f"sister-proof{PROOF_NUMBER}-organic-retopo-gate-receipt.json").open("w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)

print(f"SISTER_PROOF{PROOF_NUMBER}_ORGANIC_RETOPO_GATE_RENDERED")
