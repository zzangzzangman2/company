"""Mother AdultMorph4: edit only retained TF3 donor surfaces.

This gate deliberately creates no character mesh, primitive, curve, plate or
garment object.  It changes coordinates on the retained Mika/TF3 mesh only:
the connected crown/rear hair component is continuously compressed into a bob,
the head/eyes/cheeks receive restrained adult proportions, the body is lengthened
above the ankle, and the existing four donor skirt panels are extended toward
the knee.  Topology, UVs, vertex-group weights, native hands and the 151-bone
source rig remain present and auditable.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
from collections import defaultdict, deque

import bpy
from mathutils import Vector


def parse_args():
    argv = list(__import__("sys").argv)
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output-dir", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
INPUT = os.path.abspath(ARGS.input)
OUTPUT = os.path.abspath(ARGS.output_dir)
os.makedirs(OUTPUT, exist_ok=True)
STEM = "mother-adult-morph4-surface-edit-gate"


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def hash_json(value):
    return hashlib.sha256(json.dumps(value, separators=(",", ":"), sort_keys=True).encode("utf-8")).hexdigest().upper()


def coordinate_hash(obj, indices):
    points = [obj.matrix_world @ obj.data.vertices[index].co for index in indices]
    return hash_json(sorted(tuple(round(float(value), 7) for value in point) for point in points))


def relative_coordinate_hash(obj, indices):
    points = [obj.matrix_world @ obj.data.vertices[index].co for index in indices]
    center = sum(points, Vector()) / max(len(points), 1)
    return hash_json(sorted(tuple(round(float(value), 7) for value in (point - center)) for point in points))


def weight_hash(obj, indices):
    names = {group.index: group.name for group in obj.vertex_groups}
    records = []
    for index in indices:
        vertex = obj.data.vertices[index]
        records.append((index, sorted((names[item.group], round(float(item.weight), 7)) for item in vertex.groups)))
    return hash_json(records)


def uv_hash(obj):
    layer = obj.data.uv_layers.active.data
    rows = []
    for polygon in obj.data.polygons:
        rows.append((polygon.index, tuple(
            (obj.data.loops[loop].vertex_index, tuple(round(float(value), 7) for value in layer[loop].uv))
            for loop in polygon.loop_indices
        )))
    return hash_json(rows)


def topology_hash(obj):
    return hash_json({
        "vertices": len(obj.data.vertices),
        "edges": sorted(tuple(sorted(edge.vertices)) for edge in obj.data.edges),
        "polygons": sorted(tuple(polygon.vertices) for polygon in obj.data.polygons),
    })


def rig_hash(armature):
    return hash_json(sorted((
        bone.name,
        tuple(round(float(value), 8) for value in bone.head_local),
        tuple(round(float(value), 8) for value in bone.tail_local),
        bone.parent.name if bone.parent else None,
        bool(bone.use_connect),
    ) for bone in armature.data.bones))


def components(mesh, material_index):
    polygons = [polygon for polygon in mesh.polygons if polygon.material_index == material_index]
    polygon_map = {polygon.index: polygon for polygon in polygons}
    by_vertex = defaultdict(list)
    for polygon in polygons:
        for vertex in polygon.vertices:
            by_vertex[vertex].append(polygon.index)
    remaining = set(polygon_map)
    found = []
    while remaining:
        seed = remaining.pop()
        queue = deque([seed])
        component = {seed}
        while queue:
            polygon = polygon_map[queue.popleft()]
            for vertex in polygon.vertices:
                for neighbor in by_vertex[vertex]:
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        component.add(neighbor)
                        queue.append(neighbor)
        found.append(sorted(component))
    return sorted(found, key=len, reverse=True)


def component_vertices(mesh, polygon_indices):
    return sorted({vertex for polygon in polygon_indices for vertex in mesh.polygons[polygon].vertices})


def component_boundary_edges(mesh, polygon_indices):
    counts = defaultdict(int)
    for index in polygon_indices:
        for edge in mesh.polygons[index].edge_keys:
            counts[tuple(sorted(edge))] += 1
    return sorted(edge for edge, count in counts.items() if count == 1)


def material_index(obj, prefix):
    return next(index for index, material in enumerate(obj.data.materials) if material and material.name.startswith(prefix))


def material_vertices(obj, index):
    return sorted({vertex for polygon in obj.data.polygons if polygon.material_index == index for vertex in polygon.vertices})


def bounds(obj, indices):
    points = [obj.matrix_world @ obj.data.vertices[index].co for index in indices]
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return {
        "min": [round(float(value), 6) for value in lo],
        "max": [round(float(value), 6) for value in hi],
        "dimensions": [round(float(value), 6) for value in (hi - lo)],
        "center": [round(float(value), 6) for value in ((lo + hi) * 0.5)],
    }


def smoothstep(value):
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def world_point(obj, index):
    return obj.matrix_world @ obj.data.vertices[index].co


def set_world_point(obj, index, point):
    obj.data.vertices[index].co = obj.matrix_world.inverted() @ point


bpy.ops.wm.open_mainfile(filepath=INPUT)
scene = bpy.context.scene
body = bpy.data.objects["CH0069_Body"]
armature = bpy.data.objects["Armature"]

source_mesh_objects = sorted(obj.name for obj in scene.objects if obj.type == "MESH")
source_character_mesh_count = len(source_mesh_objects)
source_vertex_count = len(body.data.vertices)
source_edge_count = len(body.data.edges)
source_polygon_count = len(body.data.polygons)
source_material_names = [material.name if material else None for material in body.data.materials]
source_group_names = [group.name for group in body.vertex_groups]
source_topology_hash = topology_hash(body)
source_uv_hash = uv_hash(body)
source_weight_hash = weight_hash(body, range(source_vertex_count))
source_rig_hash = rig_hash(armature)

hair_slot = material_index(body, "MotherTF_ChestnutHair_SourceUV")
face_slot = material_index(body, "MotherTF2_SourceFacePolished")
brow_slot = material_index(body, "MotherTF2_SourceBrowMatureContrast")
eye_slot = material_index(body, "MotherTF_EyeMouthAlpha")
peach_slot = material_index(body, "MotherTF_PeachCardigan_SourceUV")
cream_slot = material_index(body, "MotherTF_CreamBlouse_SourceUV")
teal_slot = material_index(body, "MotherTF_TealSkirt_SourceUV")
hidden_slot = material_index(body, "MotherTF_WholeComponentHidden")

hair_components = components(body.data, hair_slot)
if not hair_components or len(hair_components[0]) != 651:
    raise RuntimeError("TF3 retained hair component 0 audit changed unexpectedly")
hair_component0_polygons = hair_components[0]
hair_component0_vertices = component_vertices(body.data, hair_component0_polygons)
hair_boundary_before = component_boundary_edges(body.data, hair_component0_polygons)

teal_components = components(body.data, teal_slot)
skirt_components = []
skirt_component_records = []
for item in teal_components:
    vertices = component_vertices(body.data, item)
    item_bounds = bounds(body, vertices)
    dimensions = item_bounds["dimensions"]
    selected = len(item) >= 60 and dimensions[0] > 0.35 and item_bounds["min"][2] < 0.45
    if selected:
        skirt_components.append(item)
    skirt_component_records.append({
        "polygons": len(item),
        "vertices": len(vertices),
        "boundsBefore": item_bounds,
        "selectedForContinuousExtension": selected,
    })
if sorted(len(item) for item in skirt_components) != [65, 65, 77, 77]:
    raise RuntimeError("Expected the audited TF3 four main skirt panels (65,65,77,77 polygons)")
skirt_vertices = sorted({vertex for item in skirt_components for vertex in component_vertices(body.data, item)})

face_vertices = material_vertices(body, face_slot)
eye_vertices = material_vertices(body, eye_slot)
peach_vertices = material_vertices(body, peach_slot)
cream_vertices = material_vertices(body, cream_slot)
visible_head_material_vertices = set(
    material_vertices(body, hair_slot)
    + face_vertices
    + material_vertices(body, brow_slot)
    + eye_vertices
)
group_names = {group.index: group.name for group in body.vertex_groups}
head_vertices = set(visible_head_material_vertices)
hand_vertices = set()
for vertex in body.data.vertices:
    memberships = [group_names[item.group].lower() for item in vertex.groups]
    if any("head" in name or "hair" in name or "dango" in name for name in memberships):
        head_vertices.add(vertex.index)
    if any("hand" in name or "finger" in name for name in memberships):
        hand_vertices.add(vertex.index)

hand_indices = sorted(hand_vertices)
hand_shape_hash_before = relative_coordinate_hash(body, hand_indices)
hand_weight_hash_before = weight_hash(body, hand_indices)
hair_bounds_before = bounds(body, hair_component0_vertices)
face_bounds_before = bounds(body, face_vertices)
eye_bounds_before = bounds(body, eye_vertices)
skirt_bounds_before = bounds(body, skirt_vertices)
upper_garment_vertices = sorted(set(peach_vertices + cream_vertices))
upper_garment_bounds_before = bounds(body, upper_garment_vertices)
body_bounds_before = bounds(body, range(source_vertex_count))
coordinates_before = [world_point(body, index).copy() for index in range(source_vertex_count)]

# Adult proportion constants requested by the gate.
ANKLE_Z = 0.19
OLD_NECK_Z = 1.14
BODY_LENGTH_SCALE = 1.10
HEAD_SCALE = 0.90
EYE_VERTICAL_SCALE = 0.90
CHEEK_WIDTH_SCALE = 0.96
OLD_HEAD_PIVOT = Vector((0.0, -0.02, OLD_NECK_Z))


def stretched_z(value):
    return value if value <= ANKLE_Z else ANKLE_Z + (value - ANKLE_Z) * BODY_LENGTH_SCALE


NEW_NECK_Z = stretched_z(OLD_NECK_Z)
NEW_HEAD_PIVOT = Vector((0.0, -0.02, NEW_NECK_Z))

# Elongate the visible body above the ankle.  Native hand vertices move rigidly
# by the wrist-height delta so finger shape/topology stays exact.
left_hands = sorted(index for index in hand_vertices if coordinates_before[index].x >= 0.0)
right_hands = sorted(index for index in hand_vertices if coordinates_before[index].x < 0.0)
hand_rigid_delta = {}
for label, indices in (("left", left_hands), ("right", right_hands)):
    center_z = sum(coordinates_before[index].z for index in indices) / len(indices)
    delta = stretched_z(center_z) - center_z
    hand_rigid_delta[label] = delta
    for index in indices:
        point = coordinates_before[index].copy()
        point.z += delta
        set_world_point(body, index, point)

for index in range(source_vertex_count):
    if index in head_vertices or index in hand_vertices:
        continue
    point = coordinates_before[index].copy()
    point.z = stretched_z(point.z)
    set_world_point(body, index, point)

# Uniform 0.90 head scale about the translated neck axis.
for index in head_vertices:
    point = coordinates_before[index]
    result = NEW_HEAD_PIVOT + (point - OLD_HEAD_PIVOT) * HEAD_SCALE
    set_world_point(body, index, result)

# Additional cheek/jaw width restraint.  It is strongest below the eyes and
# fades out through the upper cheek so the forehead/eye spacing stays stable.
cheek_vertices = []
for index in face_vertices:
    point = world_point(body, index)
    low = NEW_NECK_Z + 0.03
    high = NEW_NECK_Z + 0.36
    if point.z <= high:
        weight = 1.0 - smoothstep((point.z - low) / max(high - low, 1e-9))
        point.x *= 1.0 - (1.0 - CHEEK_WIDTH_SCALE) * weight
        set_world_point(body, index, point)
        if weight > 0.01:
            cheek_vertices.append(index)

# Compress the source eye/lash surfaces vertically per side around their own
# centers.  Alpha, texture, topology and material assignments are untouched.
eye_side_centers = {}
for label, indices in (
    ("left", [index for index in eye_vertices if world_point(body, index).x >= 0.0]),
    ("right", [index for index in eye_vertices if world_point(body, index).x < 0.0]),
):
    center_z = sum(world_point(body, index).z for index in indices) / len(indices)
    eye_side_centers[label] = center_z
    for index in indices:
        point = world_point(body, index)
        point.z = center_z + (point.z - center_z) * EYE_VERTICAL_SCALE
        set_world_point(body, index, point)

# Component 0 remains one uncut surface.  Its lower interval is monotonically
# compressed into a shoulder-length bob and narrowed toward the nape.  Because
# no faces are cut, there is no new boundary to bridge and the source component
# stays topologically closed exactly as before.
hair_after_head = {index: world_point(body, index).copy() for index in hair_component0_vertices}
hair_low = min(point.z for point in hair_after_head.values())
BOB_BOUNDARY_Z = NEW_NECK_Z + 0.18
BOB_HEM_Z = NEW_NECK_Z - 0.13
hair_bob_vertices = []
for index, point in hair_after_head.items():
    if point.z >= BOB_BOUNDARY_Z:
        continue
    normalized = (point.z - hair_low) / max(BOB_BOUNDARY_Z - hair_low, 1e-9)
    normalized = max(0.0, min(1.0, normalized))
    remapped = normalized ** 0.72
    depth = 1.0 - normalized
    stagger = 0.035 * max(0.0, 1.0 - abs(point.x) / 0.36)
    point.z = BOB_HEM_Z + stagger + (BOB_BOUNDARY_Z - BOB_HEM_Z - stagger) * remapped
    influence = 0.24 + 0.76 * smoothstep(depth)
    point.x *= 1.0 - 0.66 * influence
    point.y = NEW_HEAD_PIVOT.y + (point.y - NEW_HEAD_PIVOT.y) * (1.0 - 0.64 * influence)
    x_limit = 0.27 + 0.07 * normalized
    if abs(point.x) > x_limit:
        point.x = math.copysign(x_limit + (abs(point.x) - x_limit) * 0.08, point.x)
    back_limit = 0.28 + 0.07 * normalized
    if point.y > back_limit:
        point.y = back_limit + (point.y - back_limit) * 0.08
    set_world_point(body, index, point)
    hair_bob_vertices.append(index)

# Extend the four existing donor skirt panels without adding panels or faces.
# Waist vertices stay fixed; the deformation grows smoothly toward the hem.
skirt_pre_extension = {index: world_point(body, index).copy() for index in skirt_vertices}
skirt_min = min(point.z for point in skirt_pre_extension.values())
skirt_max = max(point.z for point in skirt_pre_extension.values())
SKIRT_HEM_EXTENSION = 0.170
for index, point in skirt_pre_extension.items():
    vertical = (skirt_max - point.z) / max(skirt_max - skirt_min, 1e-9)
    amount = smoothstep(vertical)
    point.z -= SKIRT_HEM_EXTENSION * amount
    flare = 1.0 + 0.045 * amount
    point.x *= flare
    point.y = -0.03 + (point.y + 0.03) * flare
    set_world_point(body, index, point)

# Extend only the existing central peach/cream donor top into the skirt waist.
# Sleeves outside the center band are excluded; no fill plate or face is added.
skirt_top_after_body = max(point.z for point in skirt_pre_extension.values())
TORSO_TARGET_BOTTOM = skirt_top_after_body - 0.055
TORSO_TOP_ANCHOR = NEW_NECK_Z - 0.045
torso_extension_vertices = []
for indices in (peach_vertices, cream_vertices):
    candidates = [
        index for index in indices
        if abs(world_point(body, index).x) < 0.235 and world_point(body, index).z < TORSO_TOP_ANCHOR
    ]
    if not candidates:
        continue
    source_bottom = min(world_point(body, index).z for index in candidates)
    for index in candidates:
        point = world_point(body, index)
        normalized = (point.z - source_bottom) / max(TORSO_TOP_ANCHOR - source_bottom, 1e-9)
        normalized = max(0.0, min(1.0, normalized))
        point.z = TORSO_TARGET_BOTTOM + (TORSO_TOP_ANCHOR - TORSO_TARGET_BOTTOM) * normalized
        set_world_point(body, index, point)
        torso_extension_vertices.append(index)

# The TF3 smile is shader-only on the original face surface.  Re-anchor its
# node constants after the head translation/scale; no carrier plate is exposed.
mouth_node_changes = []
face_material = body.data.materials[face_slot]
for node in face_material.node_tree.nodes:
    if node.bl_idname == "ShaderNodeMath" and node.operation == "MULTIPLY_ADD":
        if abs(float(node.inputs[1].default_value) - 6.0) < 1e-5:
            old = [float(node.inputs[1].default_value), float(node.inputs[2].default_value)]
            node.inputs[1].default_value = 8.0
            node.inputs[2].default_value = NEW_NECK_Z + HEAD_SCALE * (1.235 - OLD_NECK_Z)
            mouth_node_changes.append({"node": node.name, "old": old, "new": [8.0, float(node.inputs[2].default_value)]})
    elif node.bl_idname == "ShaderNodeMapRange":
        from_min = float(node.inputs["From Min"].default_value)
        from_max = float(node.inputs["From Max"].default_value)
        if abs(from_min - 0.034) < 1e-5 and abs(from_max - 0.043) < 1e-5:
            node.inputs["From Min"].default_value = 0.029
            node.inputs["From Max"].default_value = 0.037
            mouth_node_changes.append({"node": node.name, "old": [from_min, from_max], "new": [0.029, 0.037]})
    elif node.bl_idname == "ShaderNodeMath" and node.operation == "LESS_THAN":
        threshold = float(node.inputs[1].default_value)
        if abs(threshold - (-0.145)) < 1e-5:
            node.inputs[1].default_value = -0.132
            mouth_node_changes.append({"node": node.name, "old": threshold, "new": -0.132})

body.data.update()
for polygon in body.data.polygons:
    polygon.use_smooth = True
bpy.context.view_layer.update()

hair_bounds_after = bounds(body, hair_component0_vertices)
face_bounds_after = bounds(body, face_vertices)
eye_bounds_after = bounds(body, eye_vertices)
skirt_bounds_after = bounds(body, skirt_vertices)
upper_garment_bounds_after = bounds(body, upper_garment_vertices)
body_bounds_after = bounds(body, range(source_vertex_count))
hand_shape_hash_after = relative_coordinate_hash(body, hand_indices)
hand_weight_hash_after = weight_hash(body, hand_indices)
hair_boundary_after = component_boundary_edges(body.data, hair_component0_polygons)

hand_rigid_max_error = 0.0
for label, indices in (("left", left_hands), ("right", right_hands)):
    expected = Vector((0.0, 0.0, hand_rigid_delta[label]))
    for index in indices:
        error = (world_point(body, index) - coordinates_before[index] - expected).length
        hand_rigid_max_error = max(hand_rigid_max_error, error)

moved = []
for index, before in enumerate(coordinates_before):
    displacement = (world_point(body, index) - before).length
    if displacement > 1e-8:
        moved.append((index, displacement))

# Static four-view render.  Existing TF3 camera/lights are reused.
camera = bpy.data.objects.get("MotherTF_ReviewCamera")
if camera is None:
    raise RuntimeError("Expected TF3 review camera")
scene.camera = camera
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.10
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
views = {
    "front": ((0.0, -4.25, 0.95), (0.0, 0.0, 0.91)),
    "three-quarter": ((2.95, -3.50, 1.01), (0.0, 0.0, 0.92)),
    "side": ((4.25, 0.0, 0.98), (0.0, 0.0, 0.92)),
    "back": ((0.0, 4.25, 0.95), (0.0, 0.0, 0.91)),
}


def position_camera(location, target):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


def render(prefix):
    paths = []
    for label, (location, target) in views.items():
        position_camera(location, target)
        path = os.path.join(OUTPUT, f"{prefix}-{label}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    return paths


def gray_material():
    material = bpy.data.materials.new("MotherAdultMorph4_GrayDiagnostic")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.inputs["Base Color"].default_value = (0.48, 0.51, 0.55, 1.0)
    principled.inputs["Roughness"].default_value = 0.78
    material.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def wire_material():
    material = bpy.data.materials.new("MotherAdultMorph4_WireDiagnostic")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    wire = nodes.new("ShaderNodeWireframe")
    wire.use_pixel_size = True
    wire.inputs["Size"].default_value = 0.72
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (0.82, 0.94, 1.0, 1.0)
    emission.inputs["Strength"].default_value = 1.0
    mix = nodes.new("ShaderNodeMixShader")
    links.new(wire.outputs["Fac"], mix.inputs[0])
    links.new(transparent.outputs["BSDF"], mix.inputs[1])
    links.new(emission.outputs["Emission"], mix.inputs[2])
    links.new(mix.outputs["Shader"], output.inputs["Surface"])
    if hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"
    return material


color_views = render(STEM)
original_materials = list(body.data.materials)
hidden_material = original_materials[hidden_slot]
gray = gray_material()
wire = wire_material()

for index in range(len(body.data.materials)):
    if index != hidden_slot:
        body.data.materials[index] = gray
gray_views = render(f"{STEM}-gray")
for index in range(len(body.data.materials)):
    if index != hidden_slot:
        body.data.materials[index] = wire
wire_views = render(f"{STEM}-wire")
for index, material in enumerate(original_materials):
    body.data.materials[index] = material
body.data.materials[hidden_slot] = hidden_material

blend_path = os.path.join(OUTPUT, f"{STEM}.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)

after_mesh_objects = sorted(obj.name for obj in scene.objects if obj.type == "MESH")
checks = {
    "oneExistingCharacterMeshOnly": after_mesh_objects == source_mesh_objects and len(after_mesh_objects) == source_character_mesh_count,
    "topologyExact": topology_hash(body) == source_topology_hash,
    "vertexEdgePolygonCountsExact": (
        len(body.data.vertices) == source_vertex_count
        and len(body.data.edges) == source_edge_count
        and len(body.data.polygons) == source_polygon_count
    ),
    "uvExact": uv_hash(body) == source_uv_hash,
    "weightsExact": weight_hash(body, range(source_vertex_count)) == source_weight_hash,
    "handWeightsExact": hand_weight_hash_after == hand_weight_hash_before,
    "nativeThreeDigitHandShapeExactUpToRigidTranslation": hand_rigid_max_error <= 0.000002,
    "rig151RestTopologyExact": len(armature.data.bones) == 151 and rig_hash(armature) == source_rig_hash,
    "materialSlotNamesExact": [material.name if material else None for material in body.data.materials] == source_material_names,
    "vertexGroupNamesExact": [group.name for group in body.vertex_groups] == source_group_names,
    "hairComponent0PolygonCountExact": len(hair_component0_polygons) == 651,
    "hairComponent0BoundaryExact": hair_boundary_before == hair_boundary_after,
    "noCutOrNewBoundary": len(hair_boundary_before) == len(hair_boundary_after),
}
if not all(checks.values()):
    raise RuntimeError("Mother AdultMorph4 preservation gate failed: " + json.dumps(checks))

receipt = {
    "schema": "family-company.mother-adult-morph4-surface-edit-gate.v1",
    "status": "AWAITING_ROOT_SURFACE_EDIT_GATE",
    "candidateClaim": False,
    "passClaim": False,
    "source": {
        "tf3Blend": INPUT,
        "sha256": sha256(INPUT),
        "ownedBase": "user-attested Mika/test source through MotherTextureFirst3AdultCleanGate",
        "test3OrSakurakoUsed": False,
    },
    "hardConstraints": {
        "newCharacterMeshObjects": [],
        "newCharacterGeometryCount": 0,
        "newPrimitives": [],
        "newCurves": [],
        "newPlates": [],
        "newGarmentObjects": [],
        "meshObjectsBefore": source_mesh_objects,
        "meshObjectsAfter": after_mesh_objects,
        "gifCreated": False,
        "unityModified": False,
        "docsModified": False,
    },
    "preservation": {
        "automaticChecks": checks,
        "vertexCount": source_vertex_count,
        "edgeCount": source_edge_count,
        "polygonCount": source_polygon_count,
        "topologyHashBefore": source_topology_hash,
        "topologyHashAfter": topology_hash(body),
        "uvHashBefore": source_uv_hash,
        "uvHashAfter": uv_hash(body),
        "weightHashBefore": source_weight_hash,
        "weightHashAfter": weight_hash(body, range(source_vertex_count)),
        "rigHashBefore": source_rig_hash,
        "rigHashAfter": rig_hash(armature),
        "rigBoneCount": len(armature.data.bones),
        "hands": "original 3-digit stylized hand retained",
        "handShapePolicy": "each hand translated rigidly in Z; native relative coordinates, topology, three digits and weights exact",
        "handRigidTranslationWorldZ": {key: round(float(value), 6) for key, value in hand_rigid_delta.items()},
        "handRigidMaximumWorldError": round(float(hand_rigid_max_error), 9),
        "handRelativeCoordinateHashBefore": hand_shape_hash_before,
        "handRelativeCoordinateHashAfter": hand_shape_hash_after,
    },
    "surfaceEdits": {
        "movedVertexCount": len(moved),
        "maximumWorldDisplacement": round(float(max(value for _, value in moved)), 6),
        "bodyLength": {
            "method": "piecewise continuous Z stretch above ankle; shoe/foot region at or below ankle held fixed",
            "ankleWorldZ": ANKLE_Z,
            "oldNeckWorldZ": OLD_NECK_Z,
            "newNeckWorldZ": round(float(NEW_NECK_Z), 6),
            "scale": BODY_LENGTH_SCALE,
            "requestedRange": "8–12%",
            "rigNote": "151-bone rest rig and weight values remain exact; this is a static surface gate and requires later bind/pose validation before animation use",
        },
        "head": {
            "uniformScale": HEAD_SCALE,
            "pivotOldWorld": [round(float(value), 6) for value in OLD_HEAD_PIVOT],
            "pivotNewWorld": [round(float(value), 6) for value in NEW_HEAD_PIVOT],
            "vertexCount": len(head_vertices),
            "faceBoundsBefore": face_bounds_before,
            "faceBoundsAfter": face_bounds_after,
        },
        "eyes": {
            "verticalScale": EYE_VERTICAL_SCALE,
            "perSideCentersWorldZ": {key: round(float(value), 6) for key, value in eye_side_centers.items()},
            "vertexCount": len(eye_vertices),
            "boundsBefore": eye_bounds_before,
            "boundsAfter": eye_bounds_after,
            "alphaTextureAndMaterialUnchanged": True,
        },
        "cheeks": {
            "maximumWidthScale": CHEEK_WIDTH_SCALE,
            "falloffVertexCount": len(cheek_vertices),
        },
        "hairComponent0": {
            "polygonCount": len(hair_component0_polygons),
            "vertexCount": len(hair_component0_vertices),
            "movedLowerVertexCount": len(hair_bob_vertices),
            "bobBoundaryWorldZ": round(float(BOB_BOUNDARY_Z), 6),
            "bobHemWorldZ": round(float(BOB_HEM_Z), 6),
            "boundsBefore": hair_bounds_before,
            "boundsAfter": hair_bounds_after,
            "cutFaces": 0,
            "bridgeFaces": 0,
            "reasonNoBridgeRequired": "monotonic continuous coordinate compression retained every source face/edge, so no cut boundary was introduced",
            "boundaryEdgeCountBefore": len(hair_boundary_before),
            "boundaryEdgeCountAfter": len(hair_boundary_after),
        },
        "skirt": {
            "sourcePanelComponents": 4,
            "sourcePanelPolygonCounts": sorted(len(item) for item in skirt_components),
            "editedVertexCount": len(skirt_vertices),
            "hemExtensionWorld": SKIRT_HEM_EXTENSION,
            "maximumHemFlareScale": 1.045,
            "boundsBefore": skirt_bounds_before,
            "boundsAfter": skirt_bounds_after,
            "newSeparatedGarmentObjects": 0,
            "componentAudit": skirt_component_records,
        },
        "upperGarmentContinuity": {
            "method": "existing central peach/cream donor vertices extended downward with a continuous Z remap; no added faces/object",
            "editedVertexCount": len(set(torso_extension_vertices)),
            "targetBottomWorldZ": round(float(TORSO_TARGET_BOTTOM), 6),
            "topAnchorWorldZ": round(float(TORSO_TOP_ANCHOR), 6),
            "boundsBefore": upper_garment_bounds_before,
            "boundsAfter": upper_garment_bounds_after,
        },
        "mouthShaderReanchor": {
            "nodeOnly": True,
            "newGeometry": 0,
            "changes": mouth_node_changes,
        },
        "bodyBoundsBefore": body_bounds_before,
        "bodyBoundsAfter": body_bounds_after,
    },
    "visualGate": {
        "result": "NO_PASS_CLAIM_ROOT_REVIEW_REQUIRED",
        "reviewQuestions": [
            "Does the compressed connected hair surface read as a coherent shoulder bob without a ring, curtain or self-intersection?",
            "Does the smaller head/eye and 10% longer body read materially older while retaining a cute SD identity?",
            "Does the continuously stretched donor skirt reach near the knee without looking like a rigid cone or separated panels?",
            "Are the original face, eye alpha, mouth, three-digit hands and foot contact still visually intact?",
        ],
        "knownLimit": "No face texture aging or new adult garment topology is claimed; static surface proportions only.",
        "candidateClaim": False,
        "passClaim": False,
    },
    "views": {
        "color": color_views,
        "gray": gray_views,
        "wire": wire_views,
    },
    "blend": blend_path,
}
receipt_path = os.path.join(OUTPUT, f"{STEM}-receipt.json")
with open(receipt_path, "w", encoding="utf-8") as handle:
    json.dump(receipt, handle, ensure_ascii=False, indent=2)
print("MOTHER_ADULT_MORPH4_BLEND=" + blend_path)
print("MOTHER_ADULT_MORPH4_RECEIPT=" + receipt_path)
