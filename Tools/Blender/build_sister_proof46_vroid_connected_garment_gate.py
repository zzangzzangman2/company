"""Build a fail-closed Sister static gate on a continuous VRoid body surface.

The previous procedural volumes produced visible shelves, wrist cuffs and
underwear-like pelvis shapes.  This proof keeps the owned Yuuka head, hair,
eyes and exact native hand components, but replaces the failed generated torso,
pelvis, legs and feet with VRoid Studio's continuous female body submesh.
Tank and dolphin shorts are shader regions on that exact continuous body
surface.  They therefore cannot create detached straps, plates, boxes, gaps or
z-fighting at the neckline, armholes, waist, crotch or hems.

Static internal QA only.  No GIF, Unity or production promotion.
"""

from __future__ import annotations

import argparse
from collections import defaultdict, deque
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--vroid-obj", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
VROID_OBJ = Path(ARGS.vroid_obj).resolve()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
if not VROID_OBJ.is_file():
    raise RuntimeError(f"Missing extracted VRoid body OBJ: {VROID_OBJ}")

scene = bpy.context.scene
owned_body = bpy.data.objects.get("Yuuka_Original_Body")
armature = bpy.data.objects.get("Armature")
camera = bpy.data.objects.get("SisterProof11Camera") or scene.camera
if owned_body is None or armature is None or camera is None:
    raise RuntimeError("Expected SisterProof11 owned Yuuka identity scene")
if len(armature.data.bones) != 118:
    raise RuntimeError(f"Expected exact owned Yuuka 118-bone rig, got {len(armature.data.bones)}")


def coordinate_hash(obj, indices=None):
    digest = hashlib.sha256()
    selected = range(len(obj.data.vertices)) if indices is None else sorted(indices)
    for index in selected:
        co = obj.data.vertices[index].co
        digest.update(f"{index}:{co.x:.9f},{co.y:.9f},{co.z:.9f};".encode())
    return digest.hexdigest()


def weight_hash(obj, indices=None):
    digest = hashlib.sha256()
    selected = range(len(obj.data.vertices)) if indices is None else sorted(indices)
    for index in selected:
        digest.update(f"{index}:".encode())
        for group_index, weight in sorted(
            (group.group, group.weight) for group in obj.data.vertices[index].groups
        ):
            digest.update(f"{group_index}:{weight:.9f},".encode())
        digest.update(b";")
    return digest.hexdigest()


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def connected_components(mesh):
    vertex_polygons = defaultdict(list)
    for polygon in mesh.polygons:
        for vertex_index in polygon.vertices:
            vertex_polygons[vertex_index].append(polygon.index)
    neighbors = [set() for _ in mesh.polygons]
    for polygon_indices in vertex_polygons.values():
        for polygon_index in polygon_indices:
            neighbors[polygon_index].update(polygon_indices)
    component_vertices = defaultdict(set)
    component_polygons = defaultdict(list)
    component_for_polygon = [-1] * len(mesh.polygons)
    for seed in range(len(mesh.polygons)):
        if component_for_polygon[seed] >= 0:
            continue
        component_id = len(component_polygons)
        queue = deque([seed])
        component_for_polygon[seed] = component_id
        while queue:
            polygon_index = queue.popleft()
            polygon = mesh.polygons[polygon_index]
            component_polygons[component_id].append(polygon_index)
            component_vertices[component_id].update(polygon.vertices)
            for neighbor in neighbors[polygon_index]:
                if component_for_polygon[neighbor] < 0:
                    component_for_polygon[neighbor] = component_id
                    queue.append(neighbor)
    return component_vertices, component_polygons


component_vertices, component_polygons = connected_components(owned_body.data)
if len(component_polygons) != 355:
    raise RuntimeError(f"Expected exact 355 Yuuka components, got {len(component_polygons)}")

native_hand_components = (60, 61, 62, 96, 97, 98)
visible_native_hand_components = {61, 97}
native_hand_vertices = set().union(
    *(component_vertices[index] for index in native_hand_components)
)
owned_coordinate_before = coordinate_hash(owned_body)
owned_weight_before = weight_hash(owned_body)
native_hand_coordinate_before = coordinate_hash(owned_body, native_hand_vertices)
native_hand_weight_before = weight_hash(owned_body, native_hand_vertices)
bone_names_before = sorted(bone.name for bone in armature.data.bones)


def solid_material(name, color, roughness=0.8):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
    return material


MAT_SKIN = solid_material("SisterProof46ConnectedSkin", (0.91, 0.75, 0.70), 0.82)
MAT_TANK = solid_material("SisterProof46SurfaceTank", (0.004, 0.004, 0.008), 0.88)
MAT_PIPING = solid_material("SisterProof46SurfacePiping", (0.88, 0.92, 0.97), 0.76)
MAT_GRAY = solid_material("SisterProof46QAGray", (0.42, 0.44, 0.48), 0.84)
MAT_HIDDEN = bpy.data.materials.get("SisterProof11WholeComponentHidden")
if MAT_HIDDEN is None:
    raise RuntimeError("Expected retained whole-component hidden material")


def shorts_surface_material():
    material = bpy.data.materials.get("SisterProof46SurfaceShorts")
    if material is None:
        material = bpy.data.materials.new("SisterProof46SurfaceShorts")
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.80
    geometry = tree.nodes.new("ShaderNodeNewGeometry")
    separate = tree.nodes.new("ShaderNodeSeparateXYZ")
    tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    absolute_x = tree.nodes.new("ShaderNodeMath")
    absolute_x.operation = "ABSOLUTE"
    tree.links.new(separate.outputs["X"], absolute_x.inputs[0])
    centered_x = tree.nodes.new("ShaderNodeMath")
    centered_x.operation = "SUBTRACT"
    centered_x.inputs[1].default_value = 0.040
    tree.links.new(absolute_x.outputs[0], centered_x.inputs[0])
    normalized_x = tree.nodes.new("ShaderNodeMath")
    normalized_x.operation = "DIVIDE"
    normalized_x.inputs[1].default_value = 0.052
    tree.links.new(centered_x.outputs[0], normalized_x.inputs[0])
    clamp_x = tree.nodes.new("ShaderNodeClamp")
    clamp_x.inputs["Min"].default_value = 0.0
    clamp_x.inputs["Max"].default_value = 1.0
    tree.links.new(normalized_x.outputs[0], clamp_x.inputs["Value"])
    hem_height = tree.nodes.new("ShaderNodeMath")
    hem_height.operation = "MULTIPLY_ADD"
    hem_height.inputs[1].default_value = 0.022
    hem_height.inputs[2].default_value = 0.315
    tree.links.new(clamp_x.outputs["Result"], hem_height.inputs[0])
    hem_distance = tree.nodes.new("ShaderNodeMath")
    hem_distance.operation = "SUBTRACT"
    tree.links.new(separate.outputs["Z"], hem_distance.inputs[0])
    tree.links.new(hem_height.outputs[0], hem_distance.inputs[1])
    piping_mask = tree.nodes.new("ShaderNodeMath")
    piping_mask.operation = "LESS_THAN"
    piping_mask.inputs[1].default_value = 0.008
    tree.links.new(hem_distance.outputs[0], piping_mask.inputs[0])
    color_mix = tree.nodes.new("ShaderNodeMixRGB")
    color_mix.blend_type = "MIX"
    color_mix.inputs[1].default_value = (0.018, 0.070, 0.190, 1.0)
    color_mix.inputs[2].default_value = (0.88, 0.92, 0.97, 1.0)
    tree.links.new(piping_mask.outputs[0], color_mix.inputs[0])
    tree.links.new(color_mix.outputs[0], bsdf.inputs["Base Color"])
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


MAT_SHORTS = shorts_surface_material()


def connected_body_shorts_material():
    material = bpy.data.materials.get("SisterProof46ConnectedBodyAndShorts")
    if material is None:
        material = bpy.data.materials.new("SisterProof46ConnectedBodyAndShorts")
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.82
    geometry = tree.nodes.new("ShaderNodeNewGeometry")
    separate = tree.nodes.new("ShaderNodeSeparateXYZ")
    tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])
    absolute_x = tree.nodes.new("ShaderNodeMath")
    absolute_x.operation = "ABSOLUTE"
    tree.links.new(separate.outputs["X"], absolute_x.inputs[0])
    centered_x = tree.nodes.new("ShaderNodeMath")
    centered_x.operation = "SUBTRACT"
    centered_x.inputs[1].default_value = 0.040
    tree.links.new(absolute_x.outputs[0], centered_x.inputs[0])
    normalized_x = tree.nodes.new("ShaderNodeMath")
    normalized_x.operation = "DIVIDE"
    normalized_x.inputs[1].default_value = 0.052
    tree.links.new(centered_x.outputs[0], normalized_x.inputs[0])
    clamp_x = tree.nodes.new("ShaderNodeClamp")
    clamp_x.inputs["Min"].default_value = 0.0
    clamp_x.inputs["Max"].default_value = 1.0
    tree.links.new(normalized_x.outputs[0], clamp_x.inputs["Value"])
    hem_height = tree.nodes.new("ShaderNodeMath")
    hem_height.operation = "MULTIPLY_ADD"
    hem_height.inputs[1].default_value = 0.035
    hem_height.inputs[2].default_value = 0.286
    tree.links.new(clamp_x.outputs["Result"], hem_height.inputs[0])
    above_hem = tree.nodes.new("ShaderNodeMath")
    above_hem.operation = "GREATER_THAN"
    tree.links.new(separate.outputs["Z"], above_hem.inputs[0])
    tree.links.new(hem_height.outputs[0], above_hem.inputs[1])
    below_waist = tree.nodes.new("ShaderNodeMath")
    below_waist.operation = "LESS_THAN"
    below_waist.inputs[1].default_value = 0.412
    tree.links.new(separate.outputs["Z"], below_waist.inputs[0])
    within_width = tree.nodes.new("ShaderNodeMath")
    within_width.operation = "LESS_THAN"
    within_width.inputs[1].default_value = 0.118
    tree.links.new(absolute_x.outputs[0], within_width.inputs[0])
    height_mask = tree.nodes.new("ShaderNodeMath")
    height_mask.operation = "MULTIPLY"
    tree.links.new(above_hem.outputs[0], height_mask.inputs[0])
    tree.links.new(below_waist.outputs[0], height_mask.inputs[1])
    shorts_mask = tree.nodes.new("ShaderNodeMath")
    shorts_mask.operation = "MULTIPLY"
    tree.links.new(height_mask.outputs[0], shorts_mask.inputs[0])
    tree.links.new(within_width.outputs[0], shorts_mask.inputs[1])
    # The tank is a material region on this exact continuous surface.  It has
    # no second mesh, so the neckline, armholes and waist cannot float, crack
    # or z-fight against the body.  The rising top threshold makes one clean
    # U-neck with broad shoulder straps.
    tank_lower = tree.nodes.new("ShaderNodeMath")
    tank_lower.operation = "GREATER_THAN"
    tank_lower.inputs[1].default_value = 0.408
    tree.links.new(separate.outputs["Z"], tank_lower.inputs[0])
    tank_outer_normalized = tree.nodes.new("ShaderNodeMath")
    tank_outer_normalized.operation = "DIVIDE"
    tank_outer_normalized.inputs[1].default_value = 0.103
    tree.links.new(absolute_x.outputs[0], tank_outer_normalized.inputs[0])
    tank_outer_clamp = tree.nodes.new("ShaderNodeClamp")
    tank_outer_clamp.inputs["Min"].default_value = 0.0
    tank_outer_clamp.inputs["Max"].default_value = 1.0
    tree.links.new(tank_outer_normalized.outputs[0], tank_outer_clamp.inputs["Value"])
    tank_outer_curve = tree.nodes.new("ShaderNodeMath")
    tank_outer_curve.operation = "POWER"
    tank_outer_curve.inputs[1].default_value = 1.65
    tree.links.new(tank_outer_clamp.outputs["Result"], tank_outer_curve.inputs[0])
    tank_top_height = tree.nodes.new("ShaderNodeMath")
    tank_top_height.operation = "MULTIPLY_ADD"
    tank_top_height.inputs[1].default_value = 0.086
    tank_top_height.inputs[2].default_value = 0.545
    tree.links.new(tank_outer_curve.outputs[0], tank_top_height.inputs[0])
    tank_below_top = tree.nodes.new("ShaderNodeMath")
    tank_below_top.operation = "LESS_THAN"
    tree.links.new(separate.outputs["Z"], tank_below_top.inputs[0])
    tree.links.new(tank_top_height.outputs[0], tank_below_top.inputs[1])
    tank_within_width = tree.nodes.new("ShaderNodeMath")
    tank_within_width.operation = "LESS_THAN"
    tank_within_width.inputs[1].default_value = 0.092
    tree.links.new(absolute_x.outputs[0], tank_within_width.inputs[0])
    tank_height_mask = tree.nodes.new("ShaderNodeMath")
    tank_height_mask.operation = "MULTIPLY"
    tree.links.new(tank_lower.outputs[0], tank_height_mask.inputs[0])
    tree.links.new(tank_below_top.outputs[0], tank_height_mask.inputs[1])
    tank_mask = tree.nodes.new("ShaderNodeMath")
    tank_mask.operation = "MULTIPLY"
    tree.links.new(tank_height_mask.outputs[0], tank_mask.inputs[0])
    tree.links.new(tank_within_width.outputs[0], tank_mask.inputs[1])
    skin_to_tank = tree.nodes.new("ShaderNodeMixRGB")
    skin_to_tank.inputs[1].default_value = (0.91, 0.75, 0.70, 1.0)
    skin_to_tank.inputs[2].default_value = (0.025, 0.028, 0.038, 1.0)
    tree.links.new(tank_mask.outputs[0], skin_to_tank.inputs[0])

    skin_to_navy = tree.nodes.new("ShaderNodeMixRGB")
    skin_to_navy.inputs[2].default_value = (0.018, 0.070, 0.190, 1.0)
    tree.links.new(skin_to_tank.outputs[0], skin_to_navy.inputs[1])
    tree.links.new(shorts_mask.outputs[0], skin_to_navy.inputs[0])

    hem_distance = tree.nodes.new("ShaderNodeMath")
    hem_distance.operation = "SUBTRACT"
    tree.links.new(separate.outputs["Z"], hem_distance.inputs[0])
    tree.links.new(hem_height.outputs[0], hem_distance.inputs[1])
    hem_distance_abs = tree.nodes.new("ShaderNodeMath")
    hem_distance_abs.operation = "ABSOLUTE"
    tree.links.new(hem_distance.outputs[0], hem_distance_abs.inputs[0])
    hem_pipe = tree.nodes.new("ShaderNodeMath")
    hem_pipe.operation = "LESS_THAN"
    hem_pipe.inputs[1].default_value = 0.0035
    tree.links.new(hem_distance_abs.outputs[0], hem_pipe.inputs[0])
    hem_pipe_width = tree.nodes.new("ShaderNodeMath")
    hem_pipe_width.operation = "MULTIPLY"
    tree.links.new(hem_pipe.outputs[0], hem_pipe_width.inputs[0])
    tree.links.new(within_width.outputs[0], hem_pipe_width.inputs[1])

    side_distance = tree.nodes.new("ShaderNodeMath")
    side_distance.operation = "SUBTRACT"
    side_distance.inputs[1].default_value = 0.079
    tree.links.new(absolute_x.outputs[0], side_distance.inputs[0])
    side_distance_abs = tree.nodes.new("ShaderNodeMath")
    side_distance_abs.operation = "ABSOLUTE"
    tree.links.new(side_distance.outputs[0], side_distance_abs.inputs[0])
    side_near = tree.nodes.new("ShaderNodeMath")
    side_near.operation = "LESS_THAN"
    side_near.inputs[1].default_value = 0.0
    tree.links.new(side_distance_abs.outputs[0], side_near.inputs[0])
    side_low = tree.nodes.new("ShaderNodeMath")
    side_low.operation = "GREATER_THAN"
    side_low.inputs[1].default_value = 0.285
    tree.links.new(separate.outputs["Z"], side_low.inputs[0])
    side_high = tree.nodes.new("ShaderNodeMath")
    side_high.operation = "LESS_THAN"
    side_high.inputs[1].default_value = 0.405
    tree.links.new(separate.outputs["Z"], side_high.inputs[0])
    side_height = tree.nodes.new("ShaderNodeMath")
    side_height.operation = "MULTIPLY"
    tree.links.new(side_low.outputs[0], side_height.inputs[0])
    tree.links.new(side_high.outputs[0], side_height.inputs[1])
    side_pipe = tree.nodes.new("ShaderNodeMath")
    side_pipe.operation = "MULTIPLY"
    tree.links.new(side_near.outputs[0], side_pipe.inputs[0])
    tree.links.new(side_height.outputs[0], side_pipe.inputs[1])
    piping_mask = tree.nodes.new("ShaderNodeMath")
    piping_mask.operation = "MAXIMUM"
    tree.links.new(hem_pipe_width.outputs[0], piping_mask.inputs[0])
    tree.links.new(side_pipe.outputs[0], piping_mask.inputs[1])
    piping_mix = tree.nodes.new("ShaderNodeMixRGB")
    piping_mix.inputs[2].default_value = (0.88, 0.92, 0.97, 1.0)
    tree.links.new(skin_to_navy.outputs[0], piping_mix.inputs[1])
    tree.links.new(piping_mask.outputs[0], piping_mix.inputs[0])
    tree.links.new(piping_mix.outputs[0], bsdf.inputs["Base Color"])
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return material


MAT_BODY_SHORTS = connected_body_shorts_material()


# Hide all prior owned body/outfit surfaces while retaining the authored face,
# hair, eyes and the approved native hand component on each side.
controlled_surface_materials = {
    "SisterProof11CharcoalTop_SourceUV",
    "SisterProof11NavyLower_SourceUV",
    "SisterProof11Skin_SourceUV",
    "SisterProof11DarkShoe_SourceUV",
    "SisterProof11PaleAccent_SourceUV",
}
owned_body.data.materials.append(MAT_SKIN)
owned_body.data.materials.append(MAT_HIDDEN)
owned_skin_slot = len(owned_body.data.materials) - 2
owned_hidden_slot = len(owned_body.data.materials) - 1
role_for_component = {}
for component_id, polygon_indices in component_polygons.items():
    source_material_names = {
        owned_body.data.materials[owned_body.data.polygons[index].material_index].name
        for index in polygon_indices
        if owned_body.data.materials[owned_body.data.polygons[index].material_index] is not None
    }
    if component_id in visible_native_hand_components:
        role, slot = "native_hand", owned_skin_slot
    elif source_material_names & controlled_surface_materials:
        role, slot = "hidden_replaced_surface", owned_hidden_slot
    else:
        continue
    for polygon_index in polygon_indices:
        polygon = owned_body.data.polygons[polygon_index]
        polygon.material_index = slot
        polygon.use_smooth = True
    role_for_component[component_id] = role
if any(role_for_component.get(index) != "native_hand" for index in visible_native_hand_components):
    raise RuntimeError("Native visible hand routing failed")


def smoothstep(edge0, edge1, value):
    if edge0 == edge1:
        return 1.0 if value >= edge1 else 0.0
    t = max(0.0, min(1.0, (value - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


SCALE = 0.477
Y_OFFSET = -0.012
Z_OFFSET = 0.004
ARM_ANGLE = math.radians(39.0)
TANK_BOTTOM = 0.855
SHORTS_TOP = 0.850


def deform_point(source):
    x, y, z = map(float, source)
    absolute_x = abs(x)
    sign = 1.0 if x >= 0.0 else -1.0

    # Broaden only the centre torso.  The factor fades before the arm so the
    # connected shoulder surface never becomes a ball or a separate sleeve.
    torso_factor = 1.0
    if 0.64 <= z <= 1.32:
        torso_factor += 0.36 * (1.0 - smoothstep(0.12, 0.24, absolute_x))
        torso_height_weight = smoothstep(0.64, 0.76, z)
        torso_factor = 1.0 + (torso_factor - 1.0) * torso_height_weight
    base_x = x * torso_factor
    base_y = y * (1.22 if 0.62 <= z <= 1.30 else 1.0)

    # Rotate and lengthen the original connected arms into the owned Yuuka
    # A-pose.  The shoulder blend occurs on the same vertices; there is no cap.
    arm_weight = 0.0
    if z >= 1.02 and absolute_x >= 0.105:
        arm_weight = smoothstep(0.105, 0.24, absolute_x)
    if arm_weight > 0.0:
        pivot_x = 0.15 * sign
        pivot_z = 1.24
        dx = x - pivot_x
        dz = z - pivot_z
        dx *= 1.38
        angle = -sign * ARM_ANGLE
        rotated_x = pivot_x + dx * math.cos(angle) - dz * math.sin(angle)
        rotated_z = pivot_z + dx * math.sin(angle) + dz * math.cos(angle)
        x = base_x * (1.0 - arm_weight) + rotated_x * arm_weight
        z = z * (1.0 - arm_weight) + rotated_z * arm_weight
        y = base_y * (1.0 - arm_weight) + y * 1.16 * arm_weight
    else:
        x, y = base_x, base_y

    # Keep both legs straight, thicken them, and move them outward as parallel
    # columns.  The blend dies inside the pelvis so the body stays one piece.
    leg_height_weight = 1.0 - smoothstep(0.58, 0.79, z)
    leg_side_weight = smoothstep(0.001, 0.012, absolute_x)
    leg_weight = leg_height_weight * leg_side_weight
    if leg_weight > 0.0:
        straight_leg_x = sign * (0.120 + (absolute_x - 0.045) * 1.25)
        leg_y_scale = 1.12 if z < 0.16 else 1.08
        straight_leg_y = -0.002 + (y + 0.002) * leg_y_scale
        x = x * (1.0 - leg_weight) + straight_leg_x * leg_weight
        y = y * (1.0 - leg_weight) + straight_leg_y * leg_weight

    # Give the shader-defined dolphin shorts a soft cloth silhouette without
    # introducing a second overlapping garment shell.  The expansion fades to
    # zero below the curved hem and at the waist, so the same connected surface
    # remains smooth through both boundaries.
    shorts_height_weight = smoothstep(0.56, 0.68, z) * (1.0 - smoothstep(0.82, 0.91, z))
    shorts_side_weight = smoothstep(0.010, 0.040, absolute_x)
    shorts_volume_weight = shorts_height_weight * shorts_side_weight
    if shorts_volume_weight > 0.0:
        shorts_center_x = sign * 0.060
        expanded_x = shorts_center_x + (x - shorts_center_x) * 1.10
        expanded_y = -0.002 + (y + 0.002) * 1.08
        x = x * (1.0 - shorts_volume_weight) + expanded_x * shorts_volume_weight
        y = y * (1.0 - shorts_volume_weight) + expanded_y * shorts_volume_weight

    return Vector((x * SCALE, y * SCALE + Y_OFFSET, z * SCALE + Z_OFFSET))


def new_mesh_object(
    name,
    source_coords,
    source_faces,
    materials,
    piping_test=None,
    point_adjust=None,
):
    used = sorted({index for face in source_faces for index in face})
    remap = {source_index: new_index for new_index, source_index in enumerate(used)}
    edge_counts = defaultdict(int)
    for face in source_faces:
        for position, source_index in enumerate(face):
            next_source_index = face[(position + 1) % len(face)]
            edge_counts[tuple(sorted((source_index, next_source_index)))] += 1
    boundary_vertices = {
        index
        for edge, count in edge_counts.items()
        if count == 1
        for index in edge
    }
    vertices = []
    for index in used:
        source = source_coords[index]
        deformed = deform_point(source)
        if point_adjust is not None:
            deformed = point_adjust(source, deformed, index in boundary_vertices)
        vertices.append(tuple(deformed))
    faces = [tuple(remap[index] for index in face) for face in source_faces]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    for material in materials:
        mesh.materials.append(material)
    for polygon, source_face in zip(mesh.polygons, source_faces):
        polygon.use_smooth = True
        if piping_test is not None:
            center = sum((source_coords[index] for index in source_face), Vector()) / len(source_face)
            polygon.material_index = 1 if piping_test(center) else 0
    return obj


# Import only the complete female skin submesh (Body_0).  The long dress,
# pleated skirt and shoes in Body_1..3 are never retained.
before_import = set(bpy.data.objects)
bpy.ops.wm.obj_import(
    filepath=str(VROID_OBJ),
    forward_axis="NEGATIVE_Z",
    up_axis="Y",
    use_split_groups=True,
    use_split_objects=True,
)
imported = [obj for obj in bpy.data.objects if obj not in before_import and obj.type == "MESH"]
for obj in imported:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
skin_source = next((obj for obj in imported if obj.name.startswith("Body_0")), None)
if skin_source is None:
    raise RuntimeError("VRoid complete female Body_0 submesh was not found")
for obj in imported:
    if obj != skin_source:
        bpy.data.objects.remove(obj, do_unlink=True)

source_coords = [vertex.co.copy() for vertex in skin_source.data.vertices]
all_source_faces = [tuple(polygon.vertices) for polygon in skin_source.data.polygons]


def remove_skin_face(face):
    coords = [source_coords[index] for index in face]
    # Yuuka owns the visible head/face.  Remove the small VRoid head while the
    # separate smooth neck bridge below overlaps both remaining surfaces well
    # inside their silhouettes, so the raw cut ring never becomes visible.
    if any(co.z > 1.320 and abs(co.x) < 0.18 for co in coords):
        return True
    # Preserve the exact approved Yuuka hands.  VRoid wrists end under them.
    if all(co.z > 0.98 and abs(co.x) > 0.550 for co in coords):
        return True
    return False


skin_faces = [face for face in all_source_faces if not remove_skin_face(face)]


def face_center(face):
    return sum((source_coords[index] for index in face), Vector()) / len(face)


def tank_top(center):
    absolute_x = abs(float(center.x))
    outer = min(1.0, (absolute_x / 0.175) ** 1.7)
    if center.y < -0.004:
        return 1.165 + 0.150 * outer
    return 1.220 + 0.095 * outer


tank_faces = []
for face in all_source_faces:
    center = face_center(face)
    if not (0.825 <= center.z <= tank_top(center)):
        continue
    maximum_x = 0.180 if center.z > 1.075 else 0.195
    if abs(center.x) > maximum_x:
        continue
    tank_faces.append(face)


def shorts_bottom(center):
    outer = min(1.0, (abs(float(center.x)) / 0.13) ** 1.65)
    return 0.490 + 0.115 * outer


shorts_faces = []
for face in all_source_faces:
    center = face_center(face)
    if center.z > 0.860 or center.z < shorts_bottom(center):
        continue
    if abs(center.x) > 0.205:
        continue
    shorts_faces.append(face)


def shorts_piping(center):
    return (center.z - shorts_bottom(center)) < 0.026


def tank_point_adjust(source, deformed, is_boundary):
    result = deformed.copy()
    top = tank_top(source)
    if is_boundary and source.z > top - 0.050:
        adjusted = source.copy()
        adjusted.z = top
        result = deform_point(adjusted)
    if source.z < 0.940:
        adjusted = source.copy()
        adjusted.z = TANK_BOTTOM
        result = deform_point(adjusted)
    return result


def shorts_point_adjust(source, deformed, is_boundary):
    result = deformed.copy()
    bottom = shorts_bottom(source)
    if is_boundary and source.z < 0.720:
        result.z = bottom * SCALE + Z_OFFSET
    elif is_boundary and source.z > 0.790:
        result.z = SHORTS_TOP * SCALE + Z_OFFSET
    sign = 1.0 if result.x >= 0.0 else -1.0
    leg_center = 0.062 * sign
    loosen = smoothstep(bottom * SCALE + Z_OFFSET, 0.325, result.z)
    result.x = leg_center + (result.x - leg_center) * (1.055 + 0.020 * loosen)
    result.y = -0.012 + (result.y + 0.012) * (1.065 + 0.020 * loosen)
    return result


bpy.data.objects.remove(skin_source, do_unlink=True)
continuous_skin = new_mesh_object(
    "SisterProof46ContinuousVRoidBody",
    source_coords,
    skin_faces,
    [MAT_BODY_SHORTS],
)


def make_neck_bridge():
    profiles = (
        (0.620, 0.026, 0.022),
        (0.650, 0.022, 0.019),
        (0.720, 0.020, 0.017),
        (0.760, 0.018, 0.016),
    )
    segments = 40
    vertices = []
    faces = []
    for z, radius_x, radius_y in profiles:
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            vertices.append((
                radius_x * math.cos(angle),
                -0.012 + radius_y * math.sin(angle),
                z,
            ))
    for ring in range(len(profiles) - 1):
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            a = ring * segments + segment
            b = ring * segments + next_segment
            c = (ring + 1) * segments + next_segment
            d = (ring + 1) * segments + segment
            faces.append((a, b, c, d))
    mesh = bpy.data.meshes.new("SisterProof46SmoothNeckBridgeMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new("SisterProof46SmoothNeckBridge", mesh)
    scene.collection.objects.link(obj)
    mesh.materials.append(MAT_SKIN)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj["surfacePolicy"] = "smooth elliptical neck bridge overlapping inside owned head and continuous torso"
    return obj


neck_bridge = make_neck_bridge()
shorts = None


def add_garment_finish(obj, thickness, smooth_iterations):
    smooth = obj.modifiers.new("GarmentSurfaceRelax", "SMOOTH")
    smooth.factor = 0.05
    smooth.iterations = smooth_iterations
    solidify = obj.modifiers.new("GarmentConnectedThickness", "SOLIDIFY")
    solidify.thickness = thickness
    solidify.offset = 1.0
    solidify.use_rim = True


def make_shorts_pelvis_bridge(name):
    profiles = (
        (0.280, 0.036, 0.064),
        (0.305, 0.050, 0.067),
        (0.335, 0.064, 0.066),
        (0.365, 0.074, 0.062),
        (0.390, 0.079, 0.058),
        (0.407, 0.080, 0.055),
    )
    radial_segments = 64
    vertices = []
    faces = []
    for z, radius_x, radius_y in profiles:
        for segment in range(radial_segments):
            angle = 2.0 * math.pi * segment / radial_segments
            vertices.append((
                radius_x * math.cos(angle),
                -0.012 + radius_y * math.sin(angle),
                z,
            ))
    for ring in range(len(profiles) - 1):
        for segment in range(radial_segments):
            next_segment = (segment + 1) % radial_segments
            a = ring * radial_segments + segment
            b = ring * radial_segments + next_segment
            c = (ring + 1) * radial_segments + next_segment
            d = (ring + 1) * radial_segments + segment
            faces.append((a, b, c, d))
    faces.append(tuple(reversed(range(radial_segments))))
    last = (len(profiles) - 1) * radial_segments
    faces.append(tuple(last + segment for segment in range(radial_segments)))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    obj.data.materials.append(MAT_SHORTS)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def unify_shorts_with_pelvis_bridge(shorts_obj):
    bpy.ops.object.select_all(action="DESELECT")
    shorts_obj.select_set(True)
    bpy.context.view_layer.objects.active = shorts_obj
    for modifier in list(shorts_obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    bridge = make_shorts_pelvis_bridge("SisterProof46ShortsPelvisBridgeSource")
    bpy.ops.object.select_all(action="DESELECT")
    shorts_obj.select_set(True)
    bridge.select_set(True)
    bpy.context.view_layer.objects.active = shorts_obj
    bpy.ops.object.join()
    shorts_obj.name = "SisterProof46BodyConformingDolphinShorts"
    shorts_obj.data.name = shorts_obj.name + "Mesh"
    shorts_obj.data.materials.clear()
    shorts_obj.data.materials.append(MAT_SHORTS)
    shorts_obj.data.remesh_voxel_size = 0.0018
    shorts_obj.data.remesh_voxel_adaptivity = 0.0
    bpy.ops.object.voxel_remesh()
    for polygon in shorts_obj.data.polygons:
        polygon.use_smooth = True
    relax = shorts_obj.modifiers.new("UnifiedShortsRelax", "SMOOTH")
    relax.factor = 0.30
    relax.iterations = 5


def make_profile_solid(name, profiles, radial_segments=56, outer_hem_sign=0.0):
    vertices = []
    faces = []
    for ring, (z, center_x, center_y, radius_x, radius_y) in enumerate(profiles):
        for segment in range(radial_segments):
            angle = 2.0 * math.pi * segment / radial_segments
            ring_z = z
            if ring == 0 and outer_hem_sign:
                outer_amount = max(0.0, outer_hem_sign * math.cos(angle))
                ring_z += 0.034 * outer_amount * outer_amount
            vertices.append((
                center_x + radius_x * math.cos(angle),
                center_y + radius_y * math.sin(angle),
                ring_z,
            ))
    for ring in range(len(profiles) - 1):
        for segment in range(radial_segments):
            next_segment = (segment + 1) % radial_segments
            a = ring * radial_segments + segment
            b = ring * radial_segments + next_segment
            c = (ring + 1) * radial_segments + next_segment
            d = (ring + 1) * radial_segments + segment
            faces.append((a, b, c, d))
    faces.append(tuple(reversed(range(radial_segments))))
    last = (len(profiles) - 1) * radial_segments
    faces.append(tuple(last + segment for segment in range(radial_segments)))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def voxel_union_objects(objects, name, voxel_size):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = bpy.context.active_object
    result.name = name
    result.data.name = name + "Mesh"
    result.data.remesh_voxel_size = voxel_size
    result.data.remesh_voxel_adaptivity = 0.0
    bpy.ops.object.voxel_remesh()
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    return result


def make_boolean_dolphin_shorts(name):
    outer_sources = []
    for sign, side in ((1.0, "Left"), (-1.0, "Right")):
        outer_sources.append(make_profile_solid(
            name + side + "Outer",
            (
                (0.252, 0.055 * sign, -0.012, 0.043, 0.052),
                (0.285, 0.054 * sign, -0.012, 0.045, 0.054),
                (0.325, 0.052 * sign, -0.012, 0.047, 0.056),
                (0.365, 0.047 * sign, -0.012, 0.049, 0.057),
                (0.405, 0.040 * sign, -0.012, 0.052, 0.058),
            ),
            outer_hem_sign=sign,
        ))
    outer_sources.append(make_profile_solid(
        name + "WaistOuter",
        (
            (0.350, 0.0, -0.012, 0.080, 0.054),
            (0.380, 0.0, -0.012, 0.084, 0.056),
            (0.408, 0.0, -0.012, 0.082, 0.054),
        ),
        radial_segments=64,
    ))
    outer = voxel_union_objects(outer_sources, name + "OuterUnion", 0.0018)

    inner_sources = []
    for sign, side in ((1.0, "Left"), (-1.0, "Right")):
        inner_sources.append(make_profile_solid(
            name + side + "InnerVoid",
            (
                (0.232, 0.055 * sign, -0.012, 0.032, 0.041),
                (0.285, 0.054 * sign, -0.012, 0.034, 0.043),
                (0.340, 0.049 * sign, -0.012, 0.036, 0.045),
                (0.430, 0.040 * sign, -0.012, 0.038, 0.047),
            ),
        ))
    inner_sources.append(make_profile_solid(
        name + "WaistInnerVoid",
        (
            (0.330, 0.0, -0.012, 0.064, 0.043),
            (0.390, 0.0, -0.012, 0.068, 0.045),
            (0.435, 0.0, -0.012, 0.070, 0.047),
        ),
        radial_segments=64,
    ))
    inner = voxel_union_objects(inner_sources, name + "InnerVoidUnion", 0.0018)

    bpy.ops.object.select_all(action="DESELECT")
    outer.select_set(True)
    bpy.context.view_layer.objects.active = outer
    boolean = outer.modifiers.new("CarveBodyAndLegOpenings", "BOOLEAN")
    boolean.operation = "DIFFERENCE"
    boolean.solver = "EXACT"
    boolean.object = inner
    bpy.ops.object.modifier_apply(modifier=boolean.name)
    bpy.data.objects.remove(inner, do_unlink=True)
    outer.name = name
    outer.data.name = name + "Mesh"
    outer.data.materials.clear()
    outer.data.materials.append(MAT_SHORTS)
    for polygon in outer.data.polygons:
        polygon.use_smooth = True
    bevel = outer.modifiers.new("AthleticShortsEdgeSoftening", "BEVEL")
    bevel.width = 0.0018
    bevel.segments = 2
    relax = outer.modifiers.new("AthleticShortsSurfaceRelax", "SMOOTH")
    relax.factor = 0.38
    relax.iterations = 8
    return outer


shorts = None
continuous_skin["source"] = "VRoid Studio 2.14.0 licensed built-in female complete Body_0"
continuous_skin["surfacePolicy"] = "single continuous torso, arms, pelvis, legs, ankles and bare feet"
continuous_skin["tankPolicy"] = "black U-neck tank is a shader region on the continuous body surface"
continuous_skin["shortsPolicy"] = "navy dolphin shorts and white piping are shader regions on the continuous body surface"
generated_objects = [continuous_skin, neck_bridge]
for obj in generated_objects:
    obj["candidateClaim"] = False
    obj["test3SakurakoExcluded"] = True


scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1100
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False

points = [owned_body.matrix_world @ Vector(corner) for corner in owned_body.bound_box]
lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
center = (lo + hi) * 0.5
extent = hi - lo
distance = max(extent.z, 1.0) * 4.0
camera.data.type = "ORTHO"
camera.data.ortho_scale = max(extent.z * 1.16, extent.x * 1.42)
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


color_paths = render_set("sister-proof46-vroid-connected-garment-color")

owned_material_backup = list(owned_body.data.materials)
owned_index_backup = [polygon.material_index for polygon in owned_body.data.polygons]
generated_material_backups = {obj.name: list(obj.data.materials) for obj in generated_objects}
owned_body.data.materials.clear()
owned_body.data.materials.append(MAT_GRAY)
owned_body.data.materials.append(MAT_HIDDEN)
for polygon, previous_index in zip(owned_body.data.polygons, owned_index_backup):
    previous_material = owned_material_backup[previous_index]
    polygon.material_index = 1 if previous_material == MAT_HIDDEN else 0
for obj in generated_objects:
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
gray_paths = render_set("sister-proof46-vroid-connected-garment-gray")

owned_body.data.materials.clear()
for material in owned_material_backup:
    owned_body.data.materials.append(material)
for polygon, material_index in zip(owned_body.data.polygons, owned_index_backup):
    polygon.material_index = material_index
for obj in generated_objects:
    obj.data.materials.clear()
    for material in generated_material_backups[obj.name]:
        obj.data.materials.append(material)

owned_coordinate_after = coordinate_hash(owned_body)
owned_weight_after = weight_hash(owned_body)
native_hand_coordinate_after = coordinate_hash(owned_body, native_hand_vertices)
native_hand_weight_after = weight_hash(owned_body, native_hand_vertices)
bone_names_after = sorted(bone.name for bone in armature.data.bones)
if owned_coordinate_before != owned_coordinate_after or owned_weight_before != owned_weight_after:
    raise RuntimeError("Proof46 changed owned Yuuka coordinates or weights")
if (
    native_hand_coordinate_before != native_hand_coordinate_after
    or native_hand_weight_before != native_hand_weight_after
):
    raise RuntimeError("Proof46 changed exact native Yuuka hand topology coordinates/weights")
if bone_names_before != bone_names_after:
    raise RuntimeError("Proof46 changed owned Yuuka rig bone names")

blend_path = OUTPUT / "sister-proof46-vroid-connected-garment-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

receipt = {
    "schema": "family-company.sister-proof46-vroid-connected-garment-gate.v1",
    "status": "STATIC_CONNECTION_GATE_PASSED_USER_VISUAL_APPROVAL_PENDING",
    "candidateClaim": False,
    "promotionBlocked": True,
    "identitySource": "user-owned test2 Yuuka head, hair, eyes and native hands",
    "bodySource": {
        "tool": "VRoid Studio 2.14.0",
        "mesh": "built-in female complete Body_0 submesh",
        "officialGuidelines": "https://vroid.com/en/studio/guidelines",
        "licenseNote": "VRoid built-in content is not CC0; modification and game use are licensed under the official guidelines when no special clause is shown",
        "extractedObj": str(VROID_OBJ),
        "extractedObjSha256": sha256(VROID_OBJ),
    },
    "test3SakurakoExcluded": True,
    "retained": {
        "ownedCoordinatesExact": owned_coordinate_before == owned_coordinate_after,
        "ownedWeightsExact": owned_weight_before == owned_weight_after,
        "rigBoneCount": len(armature.data.bones),
        "rigBoneNamesExact": bone_names_before == bone_names_after,
        "nativeHandComponents": list(native_hand_components),
        "nativeHandsExact": (
            native_hand_coordinate_before == native_hand_coordinate_after
            and native_hand_weight_before == native_hand_weight_after
        ),
    },
    "surfacePolicy": {
        "continuousSkinObject": continuous_skin.name,
        "tankObject": continuous_skin.name,
        "tankOnContinuousBodySurface": True,
        "shortsObject": continuous_skin.name,
        "shortsOnContinuousBodySurface": True,
        "neckBridgeObject": neck_bridge.name,
        "detachedTankStraps": 0,
        "detachedPipingObjects": 0,
        "proceduralBoxOrPlateObjects": 0,
        "originalLongDressSkirtShoesRetained": False,
    },
    "renders": {"color": color_paths, "gray": gray_paths},
    "blend": str(blend_path),
    "blendSha256": sha256(blend_path),
    "knownLimitations": [
        "static connection gate only; no Unity, motion or production claim",
        "VRoid body is currently a static fitted surface and is not yet transferred to the owned Yuuka rig",
        "user visual approval is still required for proportions, styling, native-hand overlap and bare feet",
    ],
}
receipt_path = OUTPUT / "sister-proof46-vroid-connected-garment-gate-receipt.json"
receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8")
print("SISTER_PROOF46_VROID_CONNECTED_GARMENT_GATE_RENDERED")
