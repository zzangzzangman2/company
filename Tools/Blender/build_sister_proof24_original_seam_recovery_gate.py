"""Recover the Sister outfit from Yuuka's authored connected surfaces.

Proof23 proved that free procedural volumes still read as assembly parts.  This
gate returns to the owned Yuuka mesh for the face, hair, tank, pelvis, upper
legs, forearms and native hands.  The shorts/piping/skin transition is shaded
on the same original pelvis-to-calf surface, so no detached strip or cut plate
is introduced.  Only the unsuitable donor puff sleeves and boots are replaced:
each arm is one rounded shoulder-to-forearm surface and each lower leg/foot is
one continuous closed barefoot surface.

Static fail-closed QA only.  No GIF or Unity promotion.
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
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)

scene = bpy.context.scene
body = bpy.data.objects.get("Yuuka_Original_Body")
armature = bpy.data.objects.get("Armature")
camera = bpy.data.objects.get("SisterProof11Camera") or scene.camera
if body is None or armature is None or camera is None:
    raise RuntimeError("Expected full original-surface Yuuka body, armature and camera")
if len(armature.data.bones) != 118:
    raise RuntimeError(f"Expected owned Yuuka 118-bone rig, got {len(armature.data.bones)}")


def coordinate_hash(obj, indices=None):
    digest = hashlib.sha256()
    selected = range(len(obj.data.vertices)) if indices is None else sorted(indices)
    for index in selected:
        vertex = obj.data.vertices[index]
        digest.update(f"{index}:{vertex.co.x:.9f},{vertex.co.y:.9f},{vertex.co.z:.9f};".encode())
    return digest.hexdigest()


def weight_hash(obj, indices=None):
    digest = hashlib.sha256()
    selected = range(len(obj.data.vertices)) if indices is None else sorted(indices)
    for index in selected:
        vertex = obj.data.vertices[index]
        groups = sorted((group.group, group.weight) for group in vertex.groups)
        digest.update(f"{index}:".encode())
        for group_index, weight in groups:
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
    component_for_polygon = [-1] * len(mesh.polygons)
    component_vertices = defaultdict(set)
    component_polygons = defaultdict(list)
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
    return component_for_polygon, component_vertices, component_polygons


component_for_polygon, component_vertices, component_polygons = connected_components(body.data)
if len(component_polygons) != 355:
    raise RuntimeError(f"Expected exact 355 Yuuka components, got {len(component_polygons)}")

native_hand_components = (60, 61, 62, 96, 97, 98)
native_hand_vertices = set().union(*(component_vertices[index] for index in native_hand_components))
editable_tank_vertices = set(component_vertices[157])
retained_coordinate_vertices = set(range(len(body.data.vertices))) - editable_tank_vertices
body_coordinate_before = coordinate_hash(body)
body_weight_before = weight_hash(body)
retained_coordinate_before = coordinate_hash(body, retained_coordinate_vertices)
hand_coordinate_before = coordinate_hash(body, native_hand_vertices)
hand_weight_before = weight_hash(body, native_hand_vertices)
bone_names_before = sorted(bone.name for bone in armature.data.bones)


def solid_material(name, color, roughness=0.78):
    result = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    result.diffuse_color = (*color, 1.0)
    result.use_nodes = True
    bsdf = result.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
    return result


MAT_SKIN = solid_material("SisterProof24OriginalSkin", (0.91, 0.75, 0.70), 0.80)
MAT_TANK = solid_material("SisterProof24OriginalBlackTank", (0.004, 0.004, 0.008), 0.88)
MAT_SHORTS = solid_material("SisterProof24OriginalNavyShorts", (0.020, 0.080, 0.205), 0.78)
MAT_PIPING = solid_material("SisterProof24OriginalPalePiping", (0.88, 0.91, 0.95), 0.76)
MAT_GRAY = solid_material("SisterProof24QAGray", (0.40, 0.42, 0.46), 0.84)
MAT_HIDDEN = bpy.data.materials.get("SisterProof11WholeComponentHidden")
if MAT_HIDDEN is None:
    raise RuntimeError("Expected retained whole-component hidden material")


def lower_material():
    result = bpy.data.materials.get("SisterProof24SameSurfaceShortsPipingSkin")
    if result is None:
        result = bpy.data.materials.new("SisterProof24SameSurfaceShortsPipingSkin")
    result.use_nodes = True
    tree = result.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.80
    geometry = tree.nodes.new("ShaderNodeNewGeometry")
    separate = tree.nodes.new("ShaderNodeSeparateXYZ")
    tree.links.new(geometry.outputs["Position"], separate.inputs["Vector"])

    shorts_abs_x = tree.nodes.new("ShaderNodeMath")
    shorts_abs_x.operation = "ABSOLUTE"
    tree.links.new(separate.outputs["X"], shorts_abs_x.inputs[0])
    hem_slope = tree.nodes.new("ShaderNodeMath")
    hem_slope.operation = "MULTIPLY_ADD"
    hem_slope.inputs[1].default_value = 0.28
    hem_slope.inputs[2].default_value = 0.285
    tree.links.new(shorts_abs_x.outputs[0], hem_slope.inputs[0])
    shorts_low = tree.nodes.new("ShaderNodeMath")
    shorts_low.operation = "GREATER_THAN"
    tree.links.new(separate.outputs["Z"], shorts_low.inputs[0])
    tree.links.new(hem_slope.outputs[0], shorts_low.inputs[1])
    shorts_high = tree.nodes.new("ShaderNodeMath")
    shorts_high.operation = "LESS_THAN"
    shorts_high.inputs[1].default_value = 0.430
    tree.links.new(separate.outputs["Z"], shorts_high.inputs[0])
    shorts_test = tree.nodes.new("ShaderNodeMath")
    shorts_test.operation = "MULTIPLY"
    tree.links.new(shorts_low.outputs[0], shorts_test.inputs[0])
    tree.links.new(shorts_high.outputs[0], shorts_test.inputs[1])
    shorts_width = tree.nodes.new("ShaderNodeMath")
    shorts_width.operation = "LESS_THAN"
    shorts_width.inputs[1].default_value = 0.150
    tree.links.new(shorts_abs_x.outputs[0], shorts_width.inputs[0])
    shorts_mask = tree.nodes.new("ShaderNodeMath")
    shorts_mask.operation = "MULTIPLY"
    tree.links.new(shorts_test.outputs[0], shorts_mask.inputs[0])
    tree.links.new(shorts_width.outputs[0], shorts_mask.inputs[1])

    hem_distance = tree.nodes.new("ShaderNodeMath")
    hem_distance.operation = "SUBTRACT"
    tree.links.new(separate.outputs["Z"], hem_distance.inputs[0])
    tree.links.new(hem_slope.outputs[0], hem_distance.inputs[1])
    hem_distance_abs = tree.nodes.new("ShaderNodeMath")
    hem_distance_abs.operation = "ABSOLUTE"
    tree.links.new(hem_distance.outputs[0], hem_distance_abs.inputs[0])
    pipe_mask = tree.nodes.new("ShaderNodeMath")
    pipe_mask.operation = "LESS_THAN"
    pipe_mask.inputs[1].default_value = 0.0065
    tree.links.new(hem_distance_abs.outputs[0], pipe_mask.inputs[0])
    pipe_width_mask = tree.nodes.new("ShaderNodeMath")
    pipe_width_mask.operation = "MULTIPLY"
    tree.links.new(pipe_mask.outputs[0], pipe_width_mask.inputs[0])
    tree.links.new(shorts_width.outputs[0], pipe_width_mask.inputs[1])

    skin_to_shorts = tree.nodes.new("ShaderNodeMixRGB")
    skin_to_shorts.blend_type = "MIX"
    skin_to_shorts.inputs[1].default_value = (0.91, 0.75, 0.70, 1.0)
    skin_to_shorts.inputs[2].default_value = (0.020, 0.080, 0.205, 1.0)
    tree.links.new(shorts_mask.outputs[0], skin_to_shorts.inputs[0])
    piping_mix = tree.nodes.new("ShaderNodeMixRGB")
    piping_mix.blend_type = "MIX"
    piping_mix.inputs[2].default_value = (0.88, 0.91, 0.95, 1.0)
    tree.links.new(skin_to_shorts.outputs[0], piping_mix.inputs[1])
    tree.links.new(pipe_width_mask.outputs[0], piping_mix.inputs[0])

    absolute_x = tree.nodes.new("ShaderNodeMath")
    absolute_x.operation = "ABSOLUTE"
    tree.links.new(separate.outputs["X"], absolute_x.inputs[0])
    torso_width = tree.nodes.new("ShaderNodeMath")
    torso_width.operation = "LESS_THAN"
    torso_width.inputs[1].default_value = 0.110
    tree.links.new(absolute_x.outputs[0], torso_width.inputs[0])
    # A real U-shaped neckline: the black boundary is lowest at centre-front
    # and rises continuously into the two shoulder straps.  This removes the
    # rectangular skin bib left by the earlier binary strap mask.
    x_squared = tree.nodes.new("ShaderNodeMath")
    x_squared.operation = "MULTIPLY"
    tree.links.new(absolute_x.outputs[0], x_squared.inputs[0])
    tree.links.new(absolute_x.outputs[0], x_squared.inputs[1])
    neckline_curve = tree.nodes.new("ShaderNodeMath")
    neckline_curve.operation = "MULTIPLY_ADD"
    neckline_curve.inputs[1].default_value = 7.4
    neckline_curve.inputs[2].default_value = 0.535
    tree.links.new(x_squared.outputs[0], neckline_curve.inputs[0])
    below_neckline = tree.nodes.new("ShaderNodeMath")
    below_neckline.operation = "LESS_THAN"
    tree.links.new(separate.outputs["Z"], below_neckline.inputs[0])
    tree.links.new(neckline_curve.outputs[0], below_neckline.inputs[1])
    tank_low = tree.nodes.new("ShaderNodeMath")
    tank_low.operation = "GREATER_THAN"
    tank_low.inputs[1].default_value = 0.397
    tree.links.new(separate.outputs["Z"], tank_low.inputs[0])
    tank_high = tree.nodes.new("ShaderNodeMath")
    tank_high.operation = "LESS_THAN"
    tank_high.inputs[1].default_value = 0.606
    tree.links.new(separate.outputs["Z"], tank_high.inputs[0])
    tank_height = tree.nodes.new("ShaderNodeMath")
    tank_height.operation = "MULTIPLY"
    tree.links.new(tank_low.outputs[0], tank_height.inputs[0])
    tree.links.new(tank_high.outputs[0], tank_height.inputs[1])
    tank_shape = tree.nodes.new("ShaderNodeMath")
    tank_shape.operation = "MULTIPLY"
    tree.links.new(torso_width.outputs[0], tank_shape.inputs[0])
    tree.links.new(below_neckline.outputs[0], tank_shape.inputs[1])
    tank_mask = tree.nodes.new("ShaderNodeMath")
    tank_mask.operation = "MULTIPLY"
    tree.links.new(tank_height.outputs[0], tank_mask.inputs[0])
    tree.links.new(tank_shape.outputs[0], tank_mask.inputs[1])
    tank_mix = tree.nodes.new("ShaderNodeMixRGB")
    tank_mix.blend_type = "MIX"
    tank_mix.inputs[2].default_value = (0.004, 0.004, 0.008, 1.0)
    tree.links.new(piping_mix.outputs[0], tank_mix.inputs[1])
    tree.links.new(tank_mask.outputs[0], tank_mix.inputs[0])
    tree.links.new(tank_mix.outputs[0], bsdf.inputs["Base Color"])
    tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return result


MAT_LOWER = lower_material()
new_materials = (MAT_SKIN, MAT_TANK, MAT_SHORTS, MAT_PIPING, MAT_LOWER, MAT_GRAY)
slot_by_material = {}
for material in new_materials:
    slot_by_material[material.name] = len(body.data.materials)
    body.data.materials.append(material)
hidden_slot = next(
    index for index, material in enumerate(body.data.materials) if material == MAT_HIDDEN
)

# Body components kept from the owned Yuuka model.  No retained coordinates or
# weights are changed.  c63/c99 remain the one original upper-leg surface whose
# shader carries shorts, piping and skin without a geometry cut.
visible_hand_components = {61, 97}
skin_components = set(visible_hand_components)
tank_components = set()
same_surface_lower_components = set()
shorts_components = set()
visible_outfit_components = skin_components | tank_components | same_surface_lower_components | shorts_components
controlled_surface_materials = {
    "SisterProof11CharcoalTop_SourceUV",
    "SisterProof11NavyLower_SourceUV",
    "SisterProof11Skin_SourceUV",
    "SisterProof11DarkShoe_SourceUV",
    "SisterProof11PaleAccent_SourceUV",
}

role_for_component = {}
for component_id, polygon_indices in component_polygons.items():
    source_material_names = {
        body.data.materials[body.data.polygons[index].material_index].name
        for index in polygon_indices
        if body.data.materials[body.data.polygons[index].material_index] is not None
    }
    is_controlled = bool(source_material_names & controlled_surface_materials)
    if component_id in skin_components:
        role, slot = "skin", slot_by_material[MAT_SKIN.name]
    elif component_id in tank_components:
        role, slot = "tank", slot_by_material[MAT_TANK.name]
    elif component_id in same_surface_lower_components:
        role, slot = "same_surface_lower", slot_by_material[MAT_LOWER.name]
    elif component_id in shorts_components:
        role, slot = "shorts", slot_by_material[MAT_SHORTS.name]
    elif is_controlled:
        role, slot = "hidden", hidden_slot
    else:
        continue
    for polygon_index in polygon_indices:
        body.data.polygons[polygon_index].material_index = slot
        body.data.polygons[polygon_index].use_smooth = True
    role_for_component[component_id] = role

# Exact source hands must never be routed away by a component-ID mistake.
if any(role_for_component.get(index) != "skin" for index in visible_hand_components):
    raise RuntimeError("Visible native hand routing failed")

# Component 157 remains unmodified and hidden.  A closed, smoothed copy is
# authored below so the owned silhouette guides the garment without retaining
# its pointed uniform cut-outs or peplum-like hem.


def new_mesh_object(name, vertices, faces):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj


def extract_component_object(name, component_id, minimum_polygon_z=None):
    polygon_indices = list(component_polygons[component_id])
    if minimum_polygon_z is not None:
        polygon_indices = [
            polygon_index for polygon_index in polygon_indices
            if body.data.polygons[polygon_index].center.z >= minimum_polygon_z
        ]
    source_vertices = sorted({
        vertex_index
        for polygon_index in polygon_indices
        for vertex_index in body.data.polygons[polygon_index].vertices
    })
    remap = {source_index: new_index for new_index, source_index in enumerate(source_vertices)}
    vertices = [tuple(body.data.vertices[index].co) for index in source_vertices]
    faces = [
        tuple(remap[index] for index in body.data.polygons[polygon_index].vertices)
        for polygon_index in polygon_indices
    ]
    return new_mesh_object(name, vertices, faces)


def make_curve_solid(name, sign):
    # Start well inside the torso shell.  The arm tube itself becomes the
    # shoulder; a separate shoulder sphere created a visible ball joint.
    shoulder = Vector((0.073 * sign, -0.014, 0.574))
    control = Vector((0.168 * sign, -0.006, 0.523))
    wrist = Vector((0.278 * sign, -0.008, 0.420))
    ring_count = 18
    radial_segments = 40
    vertices = []
    faces = []
    axis_y = Vector((0.0, 1.0, 0.0))
    for ring in range(ring_count):
        t = ring / (ring_count - 1)
        one_minus = 1.0 - t
        centerline = shoulder * (one_minus * one_minus)
        centerline += control * (2.0 * one_minus * t) + wrist * (t * t)
        tangent = (control - shoulder) * (2.0 * one_minus)
        tangent += (wrist - control) * (2.0 * t)
        tangent.normalize()
        plane_axis = tangent.cross(axis_y).normalized()
        smooth_t = t * t * (3.0 - 2.0 * t)
        radius = 0.0340 * (1.0 - smooth_t) + 0.0215 * smooth_t
        for segment in range(radial_segments):
            angle = 2.0 * math.pi * segment / radial_segments
            radial = axis_y * (math.cos(angle) * radius * 0.92)
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
    faces.append(tuple(reversed(range(radial_segments))))
    last = (ring_count - 1) * radial_segments
    faces.append(tuple(last + segment for segment in range(radial_segments)))
    return new_mesh_object(name, vertices, faces)


def make_vertical_solid(name, profiles, radial_segments=40):
    vertices = []
    faces = []
    for z, center_x, center_y, radius_x, radius_y in profiles:
        for segment in range(radial_segments):
            angle = 2.0 * math.pi * segment / radial_segments
            vertices.append((
                center_x + radius_x * math.cos(angle),
                center_y + radius_y * math.sin(angle),
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
    return new_mesh_object(name, vertices, faces)


def make_ellipsoid(name, center, scale):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=56, ring_count=36, location=center)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def voxel_union(objects, name, voxel_size):
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
    smooth = result.modifiers.new("ContinuousSurfaceRelax", "SMOOTH")
    smooth.factor = 0.42
    smooth.iterations = 4
    return result


def bind_object(obj, material=MAT_SKIN):
    obj.parent = body.parent
    obj.matrix_parent_inverse = body.matrix_parent_inverse.copy()
    obj.matrix_basis = body.matrix_basis.copy()
    modifier = obj.modifiers.new("OwnedYuukaArmature", "ARMATURE")
    modifier.object = armature
    obj.data.materials.append(material)


def make_owned_tank_shell(name):
    radial_segments = 72
    vertical_segments = 12
    vertices = []
    faces = []
    bottom_z = 0.397
    for layer in range(vertical_segments + 1):
        t = layer / vertical_segments
        for segment in range(radial_segments):
            angle = 2.0 * math.pi * segment / radial_segments
            frontness = -math.sin(angle)
            back_bias = (1.0 - frontness) * 0.5
            # The highest cloth lies at the outer shoulders (cos = +/-1),
            # while centre-front/back form the neckline.  Broad fourth-power
            # peaks read as tank straps instead of the prior two-prong halter.
            strap_peak = abs(math.cos(angle)) ** 4
            top_z = 0.535 + 0.014 * back_bias + 0.064 * strap_peak
            z = bottom_z + (top_z - bottom_z) * t
            if t < 0.36:
                blend = t / 0.36
                radius_x = 0.092 * (1.0 - blend) + 0.087 * blend
                radius_y = 0.060 * (1.0 - blend) + 0.061 * blend
            else:
                blend = (t - 0.36) / 0.64
                radius_x = 0.087 * (1.0 - blend) + 0.090 * blend
                radius_y = 0.061 * (1.0 - blend) + 0.063 * blend
            vertices.append((
                radius_x * math.cos(angle),
                -0.030 + radius_y * math.sin(angle),
                z,
            ))
    for layer in range(vertical_segments):
        for segment in range(radial_segments):
            next_segment = (segment + 1) % radial_segments
            a = layer * radial_segments + segment
            b = layer * radial_segments + next_segment
            c = (layer + 1) * radial_segments + next_segment
            d = (layer + 1) * radial_segments + segment
            faces.append((a, b, c, d))
    obj = new_mesh_object(name + "OpenSurface", vertices, faces)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    spine = obj.vertex_groups.new(name="Bip001 Spine1")
    spine.add([vertex.index for vertex in obj.data.vertices], 1.0, "REPLACE")
    obj.name = name
    obj.data.name = name + "Mesh"
    bind_object(obj, MAT_TANK)
    obj["surfaceMethod"] = "one fitted tank shell with continuous U-neck, straps, armholes and hem"
    return obj


def make_integrated_arm(name, sign):
    tube = make_curve_solid(name + "TubeSource", sign)
    obj = tube
    obj.name = name
    obj.data.name = name + "Mesh"
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    side = "L" if sign > 0 else "R"
    upper = obj.vertex_groups.new(name=f"Bip001 {side} UpperArm")
    forearm = obj.vertex_groups.new(name=f"Bip001 {side} Forearm")
    for vertex in obj.data.vertices:
        absolute_x = abs(float(vertex.co.x))
        if absolute_x <= 0.190:
            upper.add([vertex.index], 1.0, "REPLACE")
        elif absolute_x >= 0.225:
            forearm.add([vertex.index], 1.0, "REPLACE")
        else:
            blend = (absolute_x - 0.190) / 0.035
            upper.add([vertex.index], 1.0 - blend, "REPLACE")
            forearm.add([vertex.index], blend, "REPLACE")
    bind_object(obj)
    obj["surfaceMethod"] = "one rounded tube starting inside the under-tank torso and ending inside the native hand"
    return obj


def make_under_tank_torso(name):
    # A single closed skin shell sits under the original garment.  It blocks
    # hair/background from showing through the scoop neckline and overlaps the
    # arm and lower-body shells internally; the black tank hides those joins.
    profiles = (
        (0.365, 0.0, -0.010, 0.056, 0.025),
        (0.400, 0.0, -0.010, 0.062, 0.030),
        (0.455, 0.0, -0.031, 0.075, 0.053),
        (0.515, 0.0, -0.030, 0.074, 0.053),
        (0.565, 0.0, -0.028, 0.076, 0.053),
        (0.592, 0.0, -0.025, 0.047, 0.038),
        (0.615, 0.0, -0.023, 0.035, 0.031),
        (0.655, 0.0, -0.021, 0.032, 0.029),
    )
    obj = make_vertical_solid(name, profiles, radial_segments=64)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    spine = obj.vertex_groups.new(name="Bip001 Spine1")
    pelvis = obj.vertex_groups.new(name="Bip001 Pelvis")
    for vertex in obj.data.vertices:
        z = float(vertex.co.z)
        if z >= 0.410:
            spine.add([vertex.index], 1.0, "REPLACE")
        elif z <= 0.380:
            pelvis.add([vertex.index], 1.0, "REPLACE")
        else:
            blend = (z - 0.380) / 0.030
            pelvis.add([vertex.index], 1.0 - blend, "REPLACE")
            spine.add([vertex.index], blend, "REPLACE")
    bind_object(obj, MAT_SKIN)
    obj["surfaceMethod"] = "single closed under-tank torso and neck shell"
    return obj


def make_integrated_lower_body(name):
    sources = []
    for sign, side_name in ((1.0, "Left"), (-1.0, "Right")):
        profiles = (
            (0.415, 0.069 * sign, -0.029, 0.057, 0.070),
            (0.390, 0.071 * sign, -0.028, 0.058, 0.069),
            (0.355, 0.074 * sign, -0.026, 0.056, 0.066),
            (0.315, 0.077 * sign, -0.024, 0.053, 0.062),
            (0.275, 0.079 * sign, -0.022, 0.050, 0.058),
            (0.235, 0.081 * sign, -0.020, 0.047, 0.054),
            (0.195, 0.082 * sign, -0.018, 0.044, 0.050),
            (0.158, 0.083 * sign, -0.017, 0.043, 0.048),
            (0.125, 0.083 * sign, -0.016, 0.041, 0.045),
            (0.095, 0.083 * sign, -0.015, 0.037, 0.041),
            (0.068, 0.083 * sign, -0.015, 0.033, 0.037),
            (0.045, 0.083 * sign, -0.016, 0.030, 0.034),
            (0.025, 0.083 * sign, -0.020, 0.030, 0.033),
        )
        sources.append(make_vertical_solid(name + side_name + "LegSource", profiles))
        sources.append(make_ellipsoid(
            name + side_name + "FootSource",
            (0.083 * sign, -0.040, 0.024),
            (0.034, 0.064, 0.024),
        ))
    sources.append(make_ellipsoid(
        name + "PelvisSource",
        (0.0, -0.015, 0.375),
        (0.110, 0.045, 0.080),
    ))
    obj = voxel_union(sources, name, 0.0028)
    for vertex in obj.data.vertices:
        if vertex.co.z < 0.004:
            vertex.co.z = 0.004
    pelvis_group = obj.vertex_groups.new(name="Bip001 Pelvis")
    thigh_groups = {
        "L": obj.vertex_groups.new(name="Bip001 L Thigh"),
        "R": obj.vertex_groups.new(name="Bip001 R Thigh"),
    }
    calf_groups = {
        "L": obj.vertex_groups.new(name="Bip001 L Calf"),
        "R": obj.vertex_groups.new(name="Bip001 R Calf"),
    }
    foot_groups = {
        "L": obj.vertex_groups.new(name="Bip001 L Foot"),
        "R": obj.vertex_groups.new(name="Bip001 R Foot"),
    }
    for vertex in obj.data.vertices:
        if abs(vertex.co.x) < 0.035 and vertex.co.z >= 0.330:
            pelvis_group.add([vertex.index], 1.0, "REPLACE")
            continue
        side = "L" if vertex.co.x >= 0.0 else "R"
        thigh_group = thigh_groups[side]
        calf_group = calf_groups[side]
        foot_group = foot_groups[side]
        if vertex.co.z >= 0.350:
            pelvis_group.add([vertex.index], 0.18, "REPLACE")
            thigh_group.add([vertex.index], 0.82, "REPLACE")
        elif vertex.co.z >= 0.185:
            thigh_group.add([vertex.index], 1.0, "REPLACE")
        elif vertex.co.z >= 0.140:
            blend = (0.185 - vertex.co.z) / 0.045
            thigh_group.add([vertex.index], 1.0 - blend, "REPLACE")
            calf_group.add([vertex.index], blend, "REPLACE")
        elif vertex.co.z >= 0.055:
            calf_group.add([vertex.index], 1.0, "REPLACE")
        elif vertex.co.z <= 0.025:
            foot_group.add([vertex.index], 1.0, "REPLACE")
        else:
            blend = (0.055 - vertex.co.z) / 0.030
            calf_group.add([vertex.index], 1.0 - blend, "REPLACE")
            foot_group.add([vertex.index], blend, "REPLACE")
    bind_object(obj, MAT_LOWER)
    obj["surfaceMethod"] = "one voxel-unified pelvis, both legs and both rounded bare feet"
    return obj


def make_integrated_one_piece_body(name):
    torso_profiles = (
        (0.603, 0.0, -0.025, 0.075, 0.055),
        (0.586, 0.0, -0.027, 0.093, 0.064),
        (0.558, 0.0, -0.029, 0.102, 0.070),
        (0.515, 0.0, -0.031, 0.102, 0.073),
        (0.480, 0.0, -0.032, 0.096, 0.070),
        (0.440, 0.0, -0.033, 0.089, 0.066),
        (0.405, 0.0, -0.033, 0.094, 0.066),
        (0.385, 0.0, -0.032, 0.096, 0.067),
        # Continue inside the pelvis so the torso end cap can never become a
        # rendered waist flange after voxel union.
        (0.360, 0.0, -0.030, 0.092, 0.064),
        (0.338, 0.0, -0.028, 0.078, 0.056),
    )
    sources = [make_vertical_solid(name + "TorsoSource", torso_profiles, radial_segments=56)]
    neck_profiles = (
        (0.655, 0.0, -0.021, 0.032, 0.029),
        (0.630, 0.0, -0.022, 0.034, 0.030),
        (0.608, 0.0, -0.024, 0.037, 0.033),
        (0.592, 0.0, -0.026, 0.042, 0.037),
    )
    sources.append(make_vertical_solid(name + "NeckSource", neck_profiles, radial_segments=40))
    for sign, side_name in ((1.0, "Left"), (-1.0, "Right")):
        sources.append(make_curve_solid(name + side_name + "ArmSource", sign))
        sources.append(make_ellipsoid(
            name + side_name + "WristSource",
            (0.276 * sign, -0.008, 0.420),
            (0.023, 0.021, 0.023),
        ))
        leg_profiles = (
            # Extend the leg sources inside the waist.  Their top caps stay
            # internal and cannot print through as a horizontal shelf.
            (0.442, 0.038 * sign, -0.030, 0.050, 0.064),
            (0.418, 0.043 * sign, -0.030, 0.058, 0.070),
            (0.395, 0.046 * sign, -0.029, 0.059, 0.070),
            (0.365, 0.052 * sign, -0.027, 0.059, 0.068),
            (0.330, 0.061 * sign, -0.025, 0.057, 0.064),
            (0.290, 0.075 * sign, -0.023, 0.052, 0.060),
            (0.250, 0.079 * sign, -0.021, 0.049, 0.056),
            (0.210, 0.081 * sign, -0.019, 0.046, 0.052),
            (0.170, 0.082 * sign, -0.017, 0.043, 0.048),
            (0.135, 0.083 * sign, -0.016, 0.041, 0.045),
            (0.102, 0.083 * sign, -0.015, 0.037, 0.041),
            (0.073, 0.083 * sign, -0.015, 0.033, 0.037),
            (0.047, 0.083 * sign, -0.014, 0.030, 0.034),
            (0.027, 0.083 * sign, -0.010, 0.029, 0.032),
        )
        sources.append(make_vertical_solid(
            name + side_name + "LegSource", leg_profiles, radial_segments=48
        ))
        sources.append(make_ellipsoid(
            name + side_name + "HeelSource",
            (0.083 * sign, -0.004, 0.025),
            (0.031, 0.034, 0.024),
        ))
        sources.append(make_ellipsoid(
            name + side_name + "MidFootSource",
            (0.083 * sign, -0.044, 0.019),
            (0.033, 0.052, 0.018),
        ))
        sources.append(make_ellipsoid(
            name + side_name + "ToeSource",
            (0.083 * sign, -0.083, 0.018),
            (0.036, 0.030, 0.017),
        ))
    sources.append(make_ellipsoid(
        name + "PelvisSource",
        (0.0, -0.029, 0.370),
        (0.099, 0.072, 0.058),
    ))
    obj = voxel_union(sources, name, 0.0028)
    for vertex in obj.data.vertices:
        if vertex.co.z < 0.004:
            vertex.co.z = 0.004

    spine = obj.vertex_groups.new(name="Bip001 Spine1")
    pelvis = obj.vertex_groups.new(name="Bip001 Pelvis")
    upper_groups = {
        "L": obj.vertex_groups.new(name="Bip001 L UpperArm"),
        "R": obj.vertex_groups.new(name="Bip001 R UpperArm"),
    }
    fore_groups = {
        "L": obj.vertex_groups.new(name="Bip001 L Forearm"),
        "R": obj.vertex_groups.new(name="Bip001 R Forearm"),
    }
    thigh_groups = {
        "L": obj.vertex_groups.new(name="Bip001 L Thigh"),
        "R": obj.vertex_groups.new(name="Bip001 R Thigh"),
    }
    calf_groups = {
        "L": obj.vertex_groups.new(name="Bip001 L Calf"),
        "R": obj.vertex_groups.new(name="Bip001 R Calf"),
    }
    foot_groups = {
        "L": obj.vertex_groups.new(name="Bip001 L Foot"),
        "R": obj.vertex_groups.new(name="Bip001 R Foot"),
    }
    for vertex in obj.data.vertices:
        x = float(vertex.co.x)
        z = float(vertex.co.z)
        absolute_x = abs(x)
        side = "L" if x >= 0.0 else "R"
        if z >= 0.445 and absolute_x <= 0.115:
            spine.add([vertex.index], 1.0, "REPLACE")
        elif z >= 0.390 and absolute_x <= 0.105:
            blend = min(1.0, max(0.0, (0.445 - z) / 0.055))
            spine.add([vertex.index], 1.0 - blend, "REPLACE")
            pelvis.add([vertex.index], blend, "REPLACE")
        elif z >= 0.350 and absolute_x < 0.045:
            pelvis.add([vertex.index], 1.0, "REPLACE")
        elif z >= 0.380 and (absolute_x >= 0.150 or (z >= 0.430 and absolute_x >= 0.105)):
            if absolute_x <= 0.195:
                upper_groups[side].add([vertex.index], 1.0, "REPLACE")
            elif absolute_x >= 0.245:
                fore_groups[side].add([vertex.index], 1.0, "REPLACE")
            else:
                blend = (absolute_x - 0.195) / 0.050
                upper_groups[side].add([vertex.index], 1.0 - blend, "REPLACE")
                fore_groups[side].add([vertex.index], blend, "REPLACE")
        elif z >= 0.350:
            pelvis.add([vertex.index], 0.25, "REPLACE")
            thigh_groups[side].add([vertex.index], 0.75, "REPLACE")
        elif z >= 0.190:
            thigh_groups[side].add([vertex.index], 1.0, "REPLACE")
        elif z >= 0.145:
            blend = (0.190 - z) / 0.045
            thigh_groups[side].add([vertex.index], 1.0 - blend, "REPLACE")
            calf_groups[side].add([vertex.index], blend, "REPLACE")
        elif z >= 0.052:
            calf_groups[side].add([vertex.index], 1.0, "REPLACE")
        elif z <= 0.025:
            foot_groups[side].add([vertex.index], 1.0, "REPLACE")
        else:
            blend = (0.052 - z) / 0.027
            calf_groups[side].add([vertex.index], 1.0 - blend, "REPLACE")
            foot_groups[side].add([vertex.index], blend, "REPLACE")
    bind_object(obj, MAT_LOWER)
    obj["surfaceMethod"] = "one continuous torso, armholes, waist, pelvis, both legs and both bare feet"
    return obj


integrated_surfaces = [
    make_under_tank_torso("SisterProof36UnderTankTorso"),
    make_owned_tank_shell("SisterProof37OwnedFittedTank"),
    make_integrated_arm("SisterProof36LeftBareArm", 1.0),
    make_integrated_arm("SisterProof36RightBareArm", -1.0),
    make_integrated_lower_body("SisterProof36ConnectedLowerBody"),
]
for obj in integrated_surfaces:
    obj["candidateClaim"] = False
    obj["test3SakurakoExcluded"] = True

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
if scene.world is not None:
    scene.world.color = (0.018, 0.020, 0.025)

points = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
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


color_paths = render_set("sister-proof24-original-seam-recovery-color")

material_backup = list(body.data.materials)
index_backup = [polygon.material_index for polygon in body.data.polygons]
integrated_material_backups = {obj.name: list(obj.data.materials) for obj in integrated_surfaces}
body.data.materials.clear()
body.data.materials.append(MAT_GRAY)
body.data.materials.append(MAT_HIDDEN)
for polygon, previous_index in zip(body.data.polygons, index_backup):
    previous_material = material_backup[previous_index]
    polygon.material_index = 1 if previous_material == MAT_HIDDEN else 0
for obj in integrated_surfaces:
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
gray_paths = render_set("sister-proof24-original-seam-recovery-gray")
body.data.materials.clear()
for material in material_backup:
    body.data.materials.append(material)
for polygon, material_index in zip(body.data.polygons, index_backup):
    polygon.material_index = material_index
for obj in integrated_surfaces:
    obj.data.materials.clear()
    for material in integrated_material_backups[obj.name]:
        obj.data.materials.append(material)

body_coordinate_after = coordinate_hash(body)
body_weight_after = weight_hash(body)
retained_coordinate_after = coordinate_hash(body, retained_coordinate_vertices)
hand_coordinate_after = coordinate_hash(body, native_hand_vertices)
hand_weight_after = weight_hash(body, native_hand_vertices)
bone_names_after = sorted(bone.name for bone in armature.data.bones)
if retained_coordinate_before != retained_coordinate_after or body_weight_before != body_weight_after:
    raise RuntimeError("Proof36 changed retained Yuuka coordinates or any source weights")
if hand_coordinate_before != hand_coordinate_after or hand_weight_before != hand_weight_after:
    raise RuntimeError("Proof24 changed native hand coordinates or weights")
if bone_names_before != bone_names_after:
    raise RuntimeError("Proof24 changed rig bone names")

body["proofRevision"] = "SisterProof24OriginalSeamRecoveryGate"
body["candidateClaim"] = False
body["test3SakurakoExcluded"] = True
blend_path = OUTPUT / "sister-proof24-original-seam-recovery-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

receipt = {
    "schema": "family-company.sister-proof24-original-seam-recovery-gate.v1",
    "status": "STATIC_INTERNAL_QA_PENDING_ROOT_REVIEW",
    "candidateClaim": False,
    "promotionBlocked": True,
    "sourceBasis": "user-owned test2 Yuuka full original-surface blend",
    "test3SakurakoExcluded": True,
    "retained": {
        "bodyVertices": len(body.data.vertices),
        "bodyPolygons": len(body.data.polygons),
        "connectedComponents": len(component_polygons),
        "coordinatesExact": body_coordinate_before == body_coordinate_after,
        "weightsExact": body_weight_before == body_weight_after,
        "rigBoneCount": len(armature.data.bones),
        "rigBoneNamesExact": bone_names_before == bone_names_after,
        "nativeThreeDigitHandsExact": (
            hand_coordinate_before == hand_coordinate_after and hand_weight_before == hand_weight_after
        ),
        "nativeHandComponents": list(native_hand_components),
    },
    "surfacePolicy": {
        "tankAndBindings": sorted(tank_components),
        "armsHandsFeet": sorted(skin_components),
        "sameOriginalLegSurfaceForShortsPipingSkin": sorted(same_surface_lower_components),
        "authoredPelvisClosure": sorted(shorts_components),
        "generatedGarmentObjects": 0,
        "integratedSkinSurfaces": [obj.name for obj in integrated_surfaces],
        "generatedSkinVolumes": len(integrated_surfaces),
        "coordinateChanges": 0,
    },
    "componentRoles": {str(key): value for key, value in sorted(role_for_component.items())},
    "renders": {"color": color_paths, "gray": gray_paths},
    "blend": str(blend_path),
    "blendSha256": sha256(blend_path),
    "knownLimitations": [
        "static internal gate only; no GIF, Unity, motion or production claim",
        "the original face, eyes, hair, tank, shorts-bearing upper legs and native hands remain coordinate exact",
        "visual approval is required for round-shoulder continuity, native-hand overlap, shorts hem and barefoot silhouette",
    ],
}
receipt_path = OUTPUT / "sister-proof24-original-seam-recovery-gate-receipt.json"
receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8")
print("SISTER_PROOF24_ORIGINAL_SEAM_RECOVERY_GATE_RENDERED")
