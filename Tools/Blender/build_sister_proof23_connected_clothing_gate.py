"""Build Sister Proof23 with visually continuous skin, shorts, legs and feet.

The owned Yuuka face, eyes, hair, rig, weights and native three-digit hands are
retained from the rejected Proof22 blend.  Rejected bridge objects and donor
shoe/pelvis surfaces are hidden.  The replacement upper skin is a voxel-unified
torso/arm surface beneath the original fitted tank; the shorts are one closed
connected surface with piping assigned on the same mesh; each bare leg and foot
is one continuous closed surface.  Static fail-closed QA only.
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
armature = bpy.data.objects.get("Yuuka_Original_Armature") or bpy.data.objects.get("Armature")
camera = bpy.data.objects.get("SisterProof11Camera") or scene.camera
if body is None or armature is None or camera is None:
    raise RuntimeError("Expected Proof22 Yuuka body, 118-bone armature and camera")
if len(armature.data.bones) != 118:
    raise RuntimeError(f"Expected exact 118-bone Yuuka rig, got {len(armature.data.bones)}")


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def coordinate_hash(obj, vertex_indices=None):
    digest = hashlib.sha256()
    indices = vertex_indices if vertex_indices is not None else range(len(obj.data.vertices))
    for index in indices:
        digest.update(("%.9f,%.9f,%.9f;" % tuple(obj.data.vertices[index].co)).encode("ascii"))
    return digest.hexdigest().upper()


def weight_hash(obj, vertex_indices=None):
    digest = hashlib.sha256()
    indices = vertex_indices if vertex_indices is not None else range(len(obj.data.vertices))
    for index in indices:
        memberships = sorted(
            (obj.vertex_groups[item.group].name, round(float(item.weight), 8))
            for item in obj.data.vertices[index].groups
        )
        digest.update((str(index) + repr(memberships)).encode("utf-8"))
    return digest.hexdigest().upper()


def connected_components(mesh):
    adjacency = defaultdict(set)
    for edge in mesh.edges:
        left, right = edge.vertices
        adjacency[left].add(right)
        adjacency[right].add(left)
    unseen = set(range(len(mesh.vertices)))
    components = []
    while unseen:
        start = min(unseen)
        queue = deque([start])
        unseen.remove(start)
        component = {start}
        while queue:
            current = queue.popleft()
            for neighbor in adjacency[current]:
                if neighbor in unseen:
                    unseen.remove(neighbor)
                    component.add(neighbor)
                    queue.append(neighbor)
        components.append(component)
    return components


def boundary_edge_count(mesh):
    counts = defaultdict(int)
    for polygon in mesh.polygons:
        vertices = list(polygon.vertices)
        for index, left in enumerate(vertices):
            right = vertices[(index + 1) % len(vertices)]
            counts[tuple(sorted((left, right)))] += 1
    return sum(1 for count in counts.values() if count == 1)


def material(name):
    result = bpy.data.materials.get(name)
    if result is None:
        raise RuntimeError(f"Expected retained Proof22 material {name}")
    return result


MAT_SKIN = material("SisterProof13SmoothSkin")
MAT_SHORTS = material("SisterProof13SolidNavyShorts")
MAT_PIPING = material("SisterProof13SolidPalePiping")
MAT_HIDDEN = material("SisterProof11WholeComponentHidden")
MAT_GRAY = bpy.data.materials.get("SisterProof11QAGray")
if MAT_GRAY is None:
    MAT_GRAY = bpy.data.materials.new("SisterProof11QAGray")
    MAT_GRAY.diffuse_color = (0.34, 0.36, 0.40, 1.0)
    MAT_GRAY.use_nodes = True
    gray_bsdf = MAT_GRAY.node_tree.nodes.get("Principled BSDF")
    if gray_bsdf is not None:
        gray_bsdf.inputs["Base Color"].default_value = (0.34, 0.36, 0.40, 1.0)
        gray_bsdf.inputs["Roughness"].default_value = 0.78

body_coordinate_before = coordinate_hash(body)
body_weight_before = weight_hash(body)
bone_names_before = sorted(bone.name for bone in armature.data.bones)

# Resolve exact native hand islands before changing any material routing.
component_parent = list(range(len(body.data.vertices)))


def component_find(value):
    while component_parent[value] != value:
        component_parent[value] = component_parent[component_parent[value]]
        value = component_parent[value]
    return value


def component_union(left, right):
    left_root, right_root = component_find(left), component_find(right)
    if left_root != right_root:
        component_parent[right_root] = left_root


for edge in body.data.edges:
    component_union(edge.vertices[0], edge.vertices[1])
roots = defaultdict(set)
for vertex in body.data.vertices:
    roots[component_find(vertex.index)].add(vertex.index)
ordered_roots = sorted(roots, key=lambda root: min(roots[root]))
if len(ordered_roots) != 355:
    raise RuntimeError(f"Expected 355 retained Yuuka components, got {len(ordered_roots)}")
native_hand_components = (60, 61, 62, 96, 97, 98)
native_hand_vertices = sorted(
    vertex_index
    for component_id in native_hand_components
    for vertex_index in roots[ordered_roots[component_id]]
)
native_hand_coordinate_before = coordinate_hash(body, native_hand_vertices)
native_hand_weight_before = weight_hash(body, native_hand_vertices)

# Retire every rejected generated bridge from Proof22 without deleting audit data.
rejected_objects = (
    "SisterProof13LeftBareArm",
    "SisterProof13RightBareArm",
    "SisterProof13LeftShortLeg",
    "SisterProof13RightShortLeg",
    "SisterProof13LeftBareLeg",
    "SisterProof13RightBareLeg",
    "SisterProof14NecklineSkinPatch",
)
for object_name in rejected_objects:
    obj = bpy.data.objects.get(object_name)
    if obj is not None:
        obj.hide_render = True
        obj.hide_viewport = True
        obj["rejectedBy"] = "USER_VISUAL_REJECTED_CONNECTIONS"

# Hide the donor pelvis/gusset and low shoes as whole material-routed surfaces.
hidden_polygon_indices = set()
for polygon in body.data.polygons:
    current = body.data.materials[polygon.material_index]
    current_name = current.name if current is not None else ""
    if current_name in {
        "SisterProof13SolidNavyShorts",
        "SisterProof13LitNavyGusset",
        "SisterProof11DarkShoe_SourceUV",
    }:
        polygon.material_index = body.data.materials.find(MAT_HIDDEN.name)
        hidden_polygon_indices.add(polygon.index)


def new_mesh_object(name, vertices, faces):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj


def make_vertical_solid(name, profiles, radial_segments=48):
    vertices = []
    faces = []
    for z, center_x, center_y, radius_x, radius_y in profiles:
        for segment in range(radial_segments):
            angle = 2.0 * math.pi * segment / radial_segments
            vertices.append(
                (
                    center_x + radius_x * math.cos(angle),
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
    faces.append(tuple(reversed(range(radial_segments))))
    last = (len(profiles) - 1) * radial_segments
    faces.append(tuple(last + segment for segment in range(radial_segments)))
    return new_mesh_object(name, vertices, faces)


def make_curve_solid(name, sign):
    shoulder = Vector((0.120 * sign, 0.000, 0.584))
    control = Vector((0.190 * sign, -0.002, 0.525))
    wrist = Vector((0.262 * sign, -0.004, 0.443))
    ring_count = 17
    radial_segments = 40
    vertices = []
    faces = []
    axis_y = Vector((0.0, 1.0, 0.0))
    for ring in range(ring_count):
        t = ring / (ring_count - 1)
        one_minus = 1.0 - t
        centerline = shoulder * (one_minus * one_minus) + control * (2.0 * one_minus * t) + wrist * (t * t)
        tangent = (control - shoulder) * (2.0 * one_minus) + (wrist - control) * (2.0 * t)
        tangent.normalize()
        plane_axis = tangent.cross(axis_y).normalized()
        smooth_t = t * t * (3.0 - 2.0 * t)
        radius = 0.039 * (1.0 - smooth_t) + 0.027 * smooth_t
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
    smooth.iterations = 3
    return result


def copy_body_transform(obj):
    obj.parent = body.parent
    obj.matrix_parent_inverse = body.matrix_parent_inverse.copy()
    obj.matrix_basis = body.matrix_basis.copy()


def add_armature(obj):
    modifier = obj.modifiers.new("OwnedYuukaArmature", "ARMATURE")
    modifier.object = armature


# One continuous under-body skin surface. The original fitted tank is retained
# above it; its neckline and armholes now reveal skin instead of empty space.
torso = make_ellipsoid("SisterProof23TorsoUnionSource", (0.0, -0.012, 0.510), (0.106, 0.066, 0.112))
left_arm = make_curve_solid("SisterProof23LeftArmUnionSource", 1.0)
right_arm = make_curve_solid("SisterProof23RightArmUnionSource", -1.0)
upper_skin = voxel_union(
    [torso, left_arm, right_arm],
    "SisterProof23ConnectedUpperSkin",
    0.0048,
)
upper_skin.data.materials.append(MAT_SKIN)
upper_groups = {
    "spine": upper_skin.vertex_groups.new(name="Bip001 Spine1"),
    "pelvis": upper_skin.vertex_groups.new(name="Bip001 Spine"),
    "l_clavicle": upper_skin.vertex_groups.new(name="Bip001 L Clavicle"),
    "r_clavicle": upper_skin.vertex_groups.new(name="Bip001 R Clavicle"),
    "l_upper": upper_skin.vertex_groups.new(name="Bip001 L UpperArm"),
    "r_upper": upper_skin.vertex_groups.new(name="Bip001 R UpperArm"),
    "l_fore": upper_skin.vertex_groups.new(name="Bip001 L Forearm"),
    "r_fore": upper_skin.vertex_groups.new(name="Bip001 R Forearm"),
    "l_hand": upper_skin.vertex_groups.new(name="Bip001 L Hand"),
    "r_hand": upper_skin.vertex_groups.new(name="Bip001 R Hand"),
}
for vertex in upper_skin.data.vertices:
    x = float(vertex.co.x)
    absolute_x = abs(x)
    side = "l" if x > 0.0 else "r"
    if absolute_x < 0.100:
        upper_groups["spine"].add([vertex.index], 0.86, "REPLACE")
        upper_groups["pelvis"].add([vertex.index], 0.14, "REPLACE")
    elif absolute_x < 0.145:
        blend = (absolute_x - 0.100) / 0.045
        upper_groups["spine"].add([vertex.index], 1.0 - 0.60 * blend, "REPLACE")
        upper_groups[f"{side}_clavicle"].add([vertex.index], 0.60 * blend, "REPLACE")
    elif absolute_x < 0.205:
        blend = (absolute_x - 0.145) / 0.060
        upper_groups[f"{side}_clavicle"].add([vertex.index], 1.0 - blend, "REPLACE")
        upper_groups[f"{side}_upper"].add([vertex.index], blend, "REPLACE")
    elif absolute_x < 0.245:
        blend = (absolute_x - 0.205) / 0.040
        upper_groups[f"{side}_upper"].add([vertex.index], 1.0 - blend, "REPLACE")
        upper_groups[f"{side}_fore"].add([vertex.index], blend, "REPLACE")
    else:
        blend = min(1.0, (absolute_x - 0.245) / 0.025)
        upper_groups[f"{side}_fore"].add([vertex.index], 1.0 - 0.55 * blend, "REPLACE")
        upper_groups[f"{side}_hand"].add([vertex.index], 0.55 * blend, "REPLACE")
copy_body_transform(upper_skin)
add_armature(upper_skin)

# One connected closed hot-pants surface. Both leg solids overlap and are fused
# with the pelvis volume before remesh; white piping is a material region on
# this same mesh, never a second strip object.
short_profiles_left = (
    (0.430, 0.055, -0.036, 0.069, 0.076),
    (0.410, 0.057, -0.038, 0.072, 0.079),
    (0.385, 0.059, -0.040, 0.071, 0.078),
    (0.360, 0.061, -0.041, 0.067, 0.073),
    (0.350, 0.062, -0.041, 0.064, 0.070),
)
short_profiles_right = tuple((z, -x, y, rx, ry) for z, x, y, rx, ry in short_profiles_left)
left_short = make_vertical_solid("SisterProof23LeftShortUnionSource", short_profiles_left)
right_short = make_vertical_solid("SisterProof23RightShortUnionSource", short_profiles_right)
pelvis_short = make_ellipsoid("SisterProof23PelvisShortUnionSource", (0.0, -0.037, 0.406), (0.118, 0.079, 0.037))
shorts = voxel_union(
    [left_short, right_short, pelvis_short],
    "SisterProof23ConnectedShorts",
    0.0038,
)
shorts.data.materials.append(MAT_SHORTS)
shorts.data.materials.append(MAT_PIPING)
for polygon in shorts.data.polygons:
    polygon.material_index = 1 if polygon.center.z <= 0.366 else 0
pelvis_group = shorts.vertex_groups.new(name="Bip001 Pelvis")
left_thigh_group = shorts.vertex_groups.new(name="Bip001 L Thigh")
right_thigh_group = shorts.vertex_groups.new(name="Bip001 R Thigh")
for vertex in shorts.data.vertices:
    if vertex.co.z >= 0.405 or abs(vertex.co.x) < 0.026:
        pelvis_group.add([vertex.index], 1.0, "REPLACE")
    else:
        pelvis_weight = max(0.25, min(0.82, (vertex.co.z - 0.350) / 0.055))
        pelvis_group.add([vertex.index], pelvis_weight, "REPLACE")
        (left_thigh_group if vertex.co.x > 0.0 else right_thigh_group).add(
            [vertex.index], 1.0 - pelvis_weight, "REPLACE"
        )
copy_body_transform(shorts)
add_armature(shorts)


def make_leg_and_foot(name, sign):
    profiles = (
        (0.408, 0.064 * sign, -0.026, 0.055, 0.061),
        (0.380, 0.067 * sign, -0.025, 0.058, 0.066),
        (0.345, 0.071 * sign, -0.024, 0.057, 0.065),
        (0.305, 0.075 * sign, -0.023, 0.053, 0.060),
        (0.265, 0.078 * sign, -0.022, 0.049, 0.056),
        (0.225, 0.081 * sign, -0.021, 0.045, 0.052),
        (0.190, 0.083 * sign, -0.020, 0.044, 0.050),
        (0.155, 0.085 * sign, -0.019, 0.045, 0.051),
        (0.122, 0.086 * sign, -0.018, 0.041, 0.047),
        (0.090, 0.087 * sign, -0.018, 0.035, 0.041),
        (0.060, 0.087 * sign, -0.019, 0.030, 0.035),
        (0.038, 0.087 * sign, -0.024, 0.030, 0.036),
        (0.022, 0.087 * sign, -0.046, 0.034, 0.057),
        (0.010, 0.087 * sign, -0.066, 0.039, 0.076),
        (0.002, 0.087 * sign, -0.070, 0.040, 0.080),
    )
    obj = make_vertical_solid(name, profiles, radial_segments=44)
    obj.data.materials.append(MAT_SKIN)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    side = "L" if sign > 0.0 else "R"
    pelvis = obj.vertex_groups.new(name="Bip001 Pelvis")
    thigh = obj.vertex_groups.new(name=f"Bip001 {side} Thigh")
    calf = obj.vertex_groups.new(name=f"Bip001 {side} Calf")
    foot = obj.vertex_groups.new(name=f"Bip001 {side} Foot")
    for vertex in obj.data.vertices:
        z = float(vertex.co.z)
        if z >= 0.375:
            blend = min(1.0, max(0.0, (0.408 - z) / 0.033))
            pelvis.add([vertex.index], 0.30 * (1.0 - blend), "REPLACE")
            thigh.add([vertex.index], 0.70 + 0.30 * blend, "REPLACE")
        elif z >= 0.205:
            thigh.add([vertex.index], 1.0, "REPLACE")
        elif z >= 0.155:
            blend = (0.205 - z) / 0.050
            thigh.add([vertex.index], 1.0 - blend, "REPLACE")
            calf.add([vertex.index], blend, "REPLACE")
        elif z >= 0.050:
            calf.add([vertex.index], 1.0, "REPLACE")
        else:
            blend = min(1.0, max(0.0, (0.050 - z) / 0.040))
            calf.add([vertex.index], 1.0 - blend, "REPLACE")
            foot.add([vertex.index], blend, "REPLACE")
    copy_body_transform(obj)
    add_armature(obj)
    return obj


left_leg = make_leg_and_foot("SisterProof23ConnectedLeftBareLegFoot", 1.0)
right_leg = make_leg_and_foot("SisterProof23ConnectedRightBareLegFoot", -1.0)
new_surfaces = [upper_skin, shorts, left_leg, right_leg]

for obj in new_surfaces:
    obj["candidateClaim"] = False
    obj["surfaceGate"] = "connected clothing static QA only"
upper_skin["surfaceMethod"] = "one voxel-unified torso plus both arms beneath retained original tank"
shorts["surfaceMethod"] = "one voxel-unified waist, left leg, right leg and crotch; piping on same mesh"
left_leg["surfaceMethod"] = "one continuous closed leg-to-bare-foot surface"
right_leg["surfaceMethod"] = "one continuous closed leg-to-bare-foot surface"

# Deterministic studio render.
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
if scene.world is not None:
    scene.world.color = (0.018, 0.020, 0.025)

points = [body.matrix_world @ Vector(corner) for corner in body.bound_box]
lo = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
hi = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
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


color_paths = render_set("sister-proof23-connected-clothing-color")

# Gray/wire QA with hidden donor surfaces kept transparent.
body_material_backup = list(body.data.materials)
body_index_backup = [polygon.material_index for polygon in body.data.polygons]
new_material_backups = {obj.name: list(obj.data.materials) for obj in new_surfaces}
new_index_backups = {obj.name: [polygon.material_index for polygon in obj.data.polygons] for obj in new_surfaces}
body.data.materials.clear()
body.data.materials.append(MAT_GRAY)
body.data.materials.append(MAT_HIDDEN)
for polygon, previous_index in zip(body.data.polygons, body_index_backup):
    previous_material = body_material_backup[previous_index]
    polygon.material_index = 1 if previous_material == MAT_HIDDEN else 0
for obj in new_surfaces:
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
    for polygon in obj.data.polygons:
        polygon.material_index = 0

MAT_WIRE = bpy.data.materials.get("SisterProof23ReadableWire") or bpy.data.materials.new("SisterProof23ReadableWire")
MAT_WIRE.use_nodes = True
wire_bsdf = MAT_WIRE.node_tree.nodes.get("Principled BSDF")
wire_bsdf.inputs["Base Color"].default_value = (0.006, 0.008, 0.012, 1.0)
wire_bsdf.inputs["Roughness"].default_value = 0.92
wire_objects = []
for source in new_surfaces:
    wire = source.copy()
    wire.data = source.data.copy()
    wire.name = "QA_Wire_" + source.name
    scene.collection.objects.link(wire)
    wire.data.materials.clear()
    wire.data.materials.append(MAT_WIRE)
    modifier = wire.modifiers.new("QA_ActualTopology", "WIREFRAME")
    modifier.thickness = 0.000020
    modifier.use_replace = True
    modifier.use_even_offset = True
    wire_objects.append(wire)

gray_paths = render_set("sister-proof23-connected-clothing-gray-wire")
for wire in wire_objects:
    bpy.data.objects.remove(wire, do_unlink=True)
body.data.materials.clear()
for retained_material in body_material_backup:
    body.data.materials.append(retained_material)
for polygon, material_index in zip(body.data.polygons, body_index_backup):
    polygon.material_index = material_index
for obj in new_surfaces:
    obj.data.materials.clear()
    for retained_material in new_material_backups[obj.name]:
        obj.data.materials.append(retained_material)
    for polygon, material_index in zip(obj.data.polygons, new_index_backups[obj.name]):
        polygon.material_index = material_index

body_coordinate_after = coordinate_hash(body)
body_weight_after = weight_hash(body)
native_hand_coordinate_after = coordinate_hash(body, native_hand_vertices)
native_hand_weight_after = weight_hash(body, native_hand_vertices)
bone_names_after = sorted(bone.name for bone in armature.data.bones)
if body_coordinate_before != body_coordinate_after:
    raise RuntimeError("Proof23 changed retained Yuuka body coordinates")
if body_weight_before != body_weight_after:
    raise RuntimeError("Proof23 changed retained Yuuka body weights")
if bone_names_before != bone_names_after:
    raise RuntimeError("Proof23 changed retained Yuuka bone names")
if native_hand_coordinate_before != native_hand_coordinate_after:
    raise RuntimeError("Proof23 changed native three-digit hand coordinates")
if native_hand_weight_before != native_hand_weight_after:
    raise RuntimeError("Proof23 changed native three-digit hand weights")

blend_path = OUTPUT / "sister-proof23-connected-clothing-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

surface_receipts = []
for obj in new_surfaces:
    components = connected_components(obj.data)
    surface_receipts.append(
        {
            "object": obj.name,
            "vertices": len(obj.data.vertices),
            "polygons": len(obj.data.polygons),
            "connectedComponents": len(components),
            "boundaryEdges": boundary_edge_count(obj.data),
            "surfaceMethod": obj.get("surfaceMethod"),
        }
    )

receipt = {
    "schema": "family-company.sister-proof23-connected-clothing-gate.v1",
    "status": "STATIC_INTERNAL_QA_PENDING_ROOT_REVIEW",
    "candidateClaim": False,
    "sourceBasis": "rejected Proof22 blend derived only from user-owned test2 Yuuka",
    "test3SakurakoExcluded": True,
    "rejectedInputStatus": "USER_VISUAL_REJECTED_CONNECTIONS",
    "retainedOwnedSurface": {
        "bodyObject": body.name,
        "vertices": len(body.data.vertices),
        "polygons": len(body.data.polygons),
        "connectedComponents": len(ordered_roots),
        "coordinatesUnchangedWithinProof23": body_coordinate_before == body_coordinate_after,
        "weightsUnchangedWithinProof23": body_weight_before == body_weight_after,
        "rigBoneCount": len(armature.data.bones),
        "rigBoneNamesExact": bone_names_before == bone_names_after,
        "nativeHands": "original 3-digit stylized hand retained",
        "nativeHandComponents": list(native_hand_components),
        "nativeHandCoordinatesExact": native_hand_coordinate_before == native_hand_coordinate_after,
        "nativeHandWeightsExact": native_hand_weight_before == native_hand_weight_after,
    },
    "connectionPolicy": {
        "upperSkin": "one connected torso+both-arms under-surface beneath the retained original tank",
        "shorts": "one connected waist+left-leg+right-leg+crotch mesh",
        "piping": "material region on the same connected shorts mesh; no strip object",
        "legsFeet": "one continuous closed leg-to-bare-foot mesh per side",
        "hiddenRejectedObjects": list(rejected_objects),
    },
    "newSurfaceReceipts": surface_receipts,
    "renders": {"color": color_paths, "grayWire": gray_paths},
    "blend": str(blend_path),
    "blendSha256": sha256(blend_path),
    "knownLimitations": [
        "static internal gate only; no GIF, Unity, motion or production claim",
        "bare feet are simplified chibi forms and require visual approval before toe-detail work",
    ],
}
receipt_path = OUTPUT / "sister-proof23-connected-clothing-gate-receipt.json"
receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8")
print("SISTER_PROOF23_CONNECTED_CLOTHING_GATE_RENDERED")
