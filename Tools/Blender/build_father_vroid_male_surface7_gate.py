"""Build a static Father quality gate on a genuine VRoid male body surface.

The owned FatherProof3 Yuuka identity, short hair, glasses, expression and all
six native three-digit hand islands remain coordinate/weight exact.  A male
VRoid skin surface supplies one organic shoulder/torso/hip/limb silhouette.
The shirt is copied from that same connected male surface, so torso, shoulder
and rolled sleeve are one shell instead of assembled primitives.  VRoid pants
and shoes keep their authored topology.  Flat fitted seams describe the collar,
placket and pocket without adding floating plates.

Static visual review only: no rig transfer, walk, Unity import or production
promotion is performed here.
"""

from __future__ import annotations

import argparse
from collections import defaultdict, deque
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--vroid-obj", required=True)
    parser.add_argument("--vroid-source", required=True)
    parser.add_argument("--reference", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(argv)


ARGS = parse_args()
VROID_OBJ = Path(ARGS.vroid_obj).resolve()
VROID_SOURCE = Path(ARGS.vroid_source).resolve()
REFERENCE = Path(ARGS.reference).resolve()
OUTPUT = Path(ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
for required in (VROID_OBJ, VROID_SOURCE, REFERENCE):
    if not required.is_file():
        raise RuntimeError(f"Missing Father surface7 input: {required}")

scene = bpy.context.scene
owned_body = bpy.data.objects.get("Yuuka_Original_Body")
armature = bpy.data.objects.get("Armature")
camera = bpy.data.objects.get("FatherProofCamera") or scene.camera
if owned_body is None or armature is None or camera is None:
    raise RuntimeError("Expected the owned FatherProof3 Yuuka identity scene")
if len(armature.data.bones) != 118:
    raise RuntimeError(f"Expected the exact 118-bone Yuuka rig, got {len(armature.data.bones)}")


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def coordinate_hash(obj, indices=None):
    digest = hashlib.sha256()
    selected = range(len(obj.data.vertices)) if indices is None else sorted(indices)
    for index in selected:
        point = obj.data.vertices[index].co
        digest.update(f"{index}:{point.x:.9f},{point.y:.9f},{point.z:.9f};".encode())
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


def connected_components(mesh):
    vertex_polygons = defaultdict(list)
    for polygon in mesh.polygons:
        for vertex_index in polygon.vertices:
            vertex_polygons[vertex_index].append(polygon.index)
    neighbors = [set() for _ in mesh.polygons]
    for polygon_indices in vertex_polygons.values():
        for polygon_index in polygon_indices:
            neighbors[polygon_index].update(polygon_indices)
    result = []
    visited = set()
    for seed in range(len(mesh.polygons)):
        if seed in visited:
            continue
        queue = deque([seed])
        visited.add(seed)
        polygons = []
        vertices = set()
        while queue:
            polygon_index = queue.popleft()
            polygons.append(polygon_index)
            vertices.update(mesh.polygons[polygon_index].vertices)
            for neighbor in neighbors[polygon_index]:
                if neighbor not in visited:
                    visited.add(neighbor)
                    queue.append(neighbor)
        result.append((vertices, polygons))
    return result


components = connected_components(owned_body.data)
native_hand_components = []
native_hand_vertices = set()
for component_id, (vertices, polygons) in enumerate(components):
    points = [owned_body.matrix_world @ owned_body.data.vertices[index].co for index in vertices]
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    if (
        max(abs(lo.x), abs(hi.x)) > 0.225
        and lo.z > 0.375
        and hi.z < 0.490
        and len(polygons) >= 20
    ):
        native_hand_components.append(component_id)
        native_hand_vertices.update(vertices)
if len(native_hand_components) != 6:
    raise RuntimeError(
        f"Expected all six native Yuuka hand islands, got {native_hand_components}"
    )
if not any((owned_body.matrix_world @ owned_body.data.vertices[i].co).x > 0 for i in native_hand_vertices):
    raise RuntimeError("Positive-X native hand was not retained")
if not any((owned_body.matrix_world @ owned_body.data.vertices[i].co).x < 0 for i in native_hand_vertices):
    raise RuntimeError("Negative-X native hand was not retained")

# The approved Father identity was inherited from a very wide chibi Yuuka
# shell.  Correct only the head region requested by the user: narrow it a
# little, restore vertical height and reduce front/back flattening.  Hand
# islands stay outside this Z range and therefore remain coordinate-exact.
HEAD_PIVOT = Vector((0.0, 0.0, 0.785))
HEAD_SCALE = Vector((0.91, 0.96, 1.10))
head_transform = (
    Matrix.Translation(HEAD_PIVOT)
    @ Matrix.Diagonal((HEAD_SCALE.x, HEAD_SCALE.y, HEAD_SCALE.z, 1.0))
    @ Matrix.Translation(-HEAD_PIVOT)
)
owned_inverse = owned_body.matrix_world.inverted()
for vertex in owned_body.data.vertices:
    world = owned_body.matrix_world @ vertex.co
    if world.z > 0.600:
        vertex.co = owned_inverse @ (head_transform @ world)
owned_body.data.update()

# Keep glasses, brows, mouth, cranium coverage and all hair synchronized with
# the corrected face shell.  The short neck is intentionally excluded so the
# chin remains overlapped instead of creating another long neck stalk.
for obj in tuple(scene.objects):
    if obj == owned_body or obj.name == "FatherNeck" or obj.type not in {"MESH", "CURVE"}:
        continue
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    if corners and min(point.z for point in corners) > 0.600:
        obj.matrix_world = head_transform @ obj.matrix_world

# Replace FatherProof3's low rear-cranium patch.  Its lower edge projected
# beneath the front jaw and read as a second chin.  The replacement ellipsoid
# stays behind the face and stops at the true jaw height.  It is an under-hair
# scalp volume, so it must use the same UV-independent charcoal material as the
# short-hair cap; using the UV-mapped face material makes the back of the head
# render as a white skin patch between the hair locks.
rear_cranium = bpy.data.objects.get("FatherRearCraniumCoverage")
if rear_cranium is None:
    raise RuntimeError("Expected FatherRearCraniumCoverage")
rear_cranium.hide_render = True
rear_hair_material = bpy.data.materials.get("FatherCharcoalHair")
if rear_hair_material is None:
    raise RuntimeError("Expected FatherCharcoalHair material")
bpy.ops.mesh.primitive_uv_sphere_add(
    segments=64,
    ring_count=32,
    location=(0.0, 0.030, 0.795),
)
clean_cranium = bpy.context.object
clean_cranium.name = "FatherSurface15CleanRearCranium"
clean_cranium.scale = (0.130, 0.085, 0.145)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
clean_cranium.data.materials.append(rear_hair_material)
for polygon in clean_cranium.data.polygons:
    polygon.use_smooth = True
clean_cranium["surfaceContinuity"] = "rounded charcoal under-hair coverage ending above the true jaw line"

owned_coordinate_before = coordinate_hash(owned_body)
owned_weight_before = weight_hash(owned_body)
hand_coordinate_before = coordinate_hash(owned_body, native_hand_vertices)
hand_weight_before = weight_hash(owned_body, native_hand_vertices)
bone_names_before = sorted(bone.name for bone in armature.data.bones)


failed_objects = {
    "FatherBareForearmL",
    "FatherBareForearmR",
    "FatherBeltBuckle",
    "FatherBrownBelt",
    "FatherBrownShoeL",
    "FatherBrownShoeR",
    "FatherChestPocket",
    "FatherCollarL",
    "FatherCollarR",
    "FatherFittedShirtTorso",
    "FatherRolledCuffL",
    "FatherRolledCuffR",
    "FatherRolledSleeveL",
    "FatherRolledSleeveR",
    "FatherShirtPlacket",
    "FatherShoeSoleL",
    "FatherShoeSoleR",
    "FatherStraightTrouserLegL",
    "FatherStraightTrouserLegR",
    "FatherTailoredShoulderL",
    "FatherTailoredShoulderR",
    "FatherTrouserWaist",
}
failed_objects.update(
    obj.name for obj in bpy.data.objects if obj.name.startswith("FatherShirtButton")
)
removed_failed_objects = []
for name in sorted(failed_objects):
    obj = bpy.data.objects.get(name)
    if obj is not None:
        removed_failed_objects.append(name)
        bpy.data.objects.remove(obj, do_unlink=True)

# FatherProof3's Yuuka-style neck is kept, but shortened into the overlap
# between head and collar so it cannot read as a tall rectangular stalk.
owned_neck = bpy.data.objects.get("FatherNeck")
if owned_neck is None:
    raise RuntimeError("Expected FatherProof3 Yuuka-style neck")
neck_matrix = owned_neck.matrix_world.copy()
neck_inverse = neck_matrix.inverted()
for vertex in owned_neck.data.vertices:
    world = neck_matrix @ vertex.co
    world.x *= 0.85
    world.y *= 0.85
    world.z = 0.655 + (world.z - 0.655) * 0.38
    vertex.co = neck_inverse @ world
owned_neck.data.update()


def solid_material(name, color, roughness=0.78, metallic=0.0):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.08
    return material


MAT_SKIN = bpy.data.materials.get("FatherSkin") or solid_material(
    "FatherSurface7Skin", (0.91, 0.75, 0.70), 0.82
)
MAT_SHIRT = solid_material("FatherSurface7MutedTealShirt", (0.070, 0.205, 0.255), 0.86)
MAT_TAILOR = solid_material("FatherSurface7TealTailoring", (0.028, 0.105, 0.140), 0.78)
MAT_PANTS = solid_material("FatherSurface7CharcoalTrousers", (0.055, 0.064, 0.075), 0.88)
MAT_BELT = solid_material("FatherSurface7BrownBeltRegion", (0.155, 0.070, 0.035), 0.74)
MAT_SHOE = solid_material("FatherSurface7BrownOxford", (0.135, 0.055, 0.026), 0.70)
MAT_SOLE = solid_material("FatherSurface7DarkSole", (0.025, 0.022, 0.021), 0.88)
MAT_SILVER = solid_material("FatherSurface7Silver", (0.72, 0.76, 0.78), 0.38, 0.22)
MAT_GRAY = solid_material("FatherSurface7QAGray", (0.43, 0.45, 0.49), 0.86)

# Preserve the authored VRoid trouser pelvis instead of cutting the garment at
# source Z 0.940.  That old cut transformed to Father Z ~0.400 and ended below
# the butt, leaving the blue shirt surface as the only visible glute volume.
# The real authored top reaches source Z 1.1207 (Father Z ~0.476), which gives
# the trousers proper waist/seat coverage and lets the shirt remain tucked in.
PANTS_SOURCE_TOP_Z = 1.125
BELT_REGION_BOTTOM_Z = 0.452
BELT_REGION_TOP_Z = 0.474
SHIRT_SOURCE_HEM_Z = 1.040
PANTS_WAIST_TAPER_BEGIN_Z = 0.425
PANTS_WAIST_TAPER_END_Z = 0.476
PANTS_WAIST_HALF_WIDTH_BOTTOM = 0.109
PANTS_WAIST_HALF_WIDTH_TOP = 0.108
SHIRT_TUCK_BEGIN_Z = 0.440
SHIRT_TUCK_END_Z = 0.490


def add_smooth_belt_region(material):
    """Color one mathematically flat waist strip; no belt geometry is added."""
    tree = material.node_tree
    bsdf = tree.nodes.get("Principled BSDF")
    geometry = tree.nodes.new("ShaderNodeNewGeometry")
    separate = tree.nodes.new("ShaderNodeSeparateXYZ")
    above = tree.nodes.new("ShaderNodeMath")
    above.operation = "GREATER_THAN"
    above.inputs[1].default_value = BELT_REGION_BOTTOM_Z
    below = tree.nodes.new("ShaderNodeMath")
    below.operation = "LESS_THAN"
    below.inputs[1].default_value = BELT_REGION_TOP_Z
    band = tree.nodes.new("ShaderNodeMath")
    band.operation = "MULTIPLY"
    mix = tree.nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MIX"
    mix.inputs[1].default_value = (0.055, 0.064, 0.075, 1.0)
    mix.inputs[2].default_value = (0.155, 0.070, 0.035, 1.0)
    tree.links.new(geometry.outputs["Position"], separate.inputs[0])
    tree.links.new(separate.outputs["Z"], above.inputs[0])
    tree.links.new(separate.outputs["Z"], below.inputs[0])
    tree.links.new(above.outputs[0], band.inputs[0])
    tree.links.new(below.outputs[0], band.inputs[1])
    tree.links.new(band.outputs[0], mix.inputs[0])
    tree.links.new(mix.outputs[0], bsdf.inputs["Base Color"])


add_smooth_belt_region(MAT_PANTS)




def smoothstep(edge0, edge1, value):
    t = max(0.0, min(1.0, (value - edge0) / max(edge1 - edge0, 1e-9)))
    return t * t * (3.0 - 2.0 * t)


ARM_ANGLE = math.radians(55.0)
ARM_SOURCE_PIVOT_X = 0.180
ARM_SOURCE_PIVOT_Z = 1.345
ARM_TARGET_PIVOT_X = 0.168
ARM_TARGET_PIVOT_Z = 0.570
ARM_LENGTH_SCALE = 0.305
ARM_THICKNESS_SCALE = 0.680
VERTICAL_SCALE = 0.420
DEPTH_SCALE = 0.750
Z_OFFSET = 0.005


def deform_point(source):
    """Map the authored VRoid male T-pose to FatherProof3's compact A-pose."""
    x, y, z = map(float, source)
    absolute_x = abs(x)
    sign = 1.0 if x >= 0.0 else -1.0

    # Natural male body column: broad shoulder/chest, gentle waist, straight hip.
    if z >= 1.40:
        width_scale = 0.76
    elif z >= 1.18:
        width_scale = 0.94
    elif z >= 0.82:
        width_scale = 0.88
    else:
        width_scale = 0.82
    base_x = x * width_scale
    base_y = y * DEPTH_SCALE
    base_z = z * VERTICAL_SCALE + Z_OFFSET

    # Move the entire authored shoulder socket with the arm.  The former high
    # Z threshold left the lower armpit in T-pose while the upper sleeve swung
    # down, producing the torn/folded shoulder seen in oblique views.
    arm_weight = smoothstep(0.150, 0.245, absolute_x) * smoothstep(0.90, 1.08, z)
    if arm_weight <= 0.0:
        return Vector((base_x, base_y, base_z))

    dx = x - sign * ARM_SOURCE_PIVOT_X
    dz = z - ARM_SOURCE_PIVOT_Z
    angle = -sign * ARM_ANGLE
    # Preserve the body surface's shoulder/upper-arm thickness while keeping
    # the compact arm length required by the untouched native hand positions.
    scaled_dx = dx * ARM_LENGTH_SCALE
    scaled_dz = dz * ARM_THICKNESS_SCALE
    rotated_x = scaled_dx * math.cos(angle) - scaled_dz * math.sin(angle)
    rotated_z = scaled_dx * math.sin(angle) + scaled_dz * math.cos(angle)
    arm_x = sign * ARM_TARGET_PIVOT_X + rotated_x
    arm_z = ARM_TARGET_PIVOT_Z + rotated_z
    arm_y = y * 0.82
    return Vector(
        (
            base_x * (1.0 - arm_weight) + arm_x * arm_weight,
            base_y * (1.0 - arm_weight) + arm_y * arm_weight,
            base_z * (1.0 - arm_weight) + arm_z * arm_weight,
        )
    )


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

by_name = {obj.name: obj for obj in imported}
for required_name in ("Body_0", "Body_1", "Body_2", "Body_3"):
    if required_name not in by_name:
        raise RuntimeError(f"VRoid male OBJ is missing {required_name}")
skin_source = by_name["Body_0"]
long_top = by_name["Body_1"]
pants = by_name["Body_2"]
shoes = by_name["Body_3"]

skin_source_coords = [vertex.co.copy() for vertex in skin_source.data.vertices]
skin_source_faces = [tuple(polygon.vertices) for polygon in skin_source.data.polygons]


def rebuild_object(obj, source_coords, source_faces, keep_face, mesh_name):
    kept = [face for face in source_faces if keep_face(face, source_coords)]
    used = sorted({index for face in kept for index in face})
    remap = {source_index: new_index for new_index, source_index in enumerate(used)}
    mesh = bpy.data.meshes.new(mesh_name)
    mesh.from_pydata(
        [tuple(deform_point(source_coords[index])) for index in used],
        [],
        [tuple(remap[index] for index in face) for face in kept],
    )
    mesh.update(calc_edges=True)
    old_mesh = obj.data
    obj.data = mesh
    bpy.data.meshes.remove(old_mesh)
    return kept, used


def skin_face_visible(face, coordinates):
    # The warped imported forearms are replaced below by one clean continuous
    # arm mesh per side.  Keep this source object only as provenance.
    return False


skin_faces, skin_used = rebuild_object(
    skin_source,
    skin_source_coords,
    skin_source_faces,
    skin_face_visible,
    "FatherSurface7OrganicMaleSkinMesh",
)
skin_source.name = "FatherSurface7OrganicMaleContinuousBody"
skin_source.data.materials.append(MAT_SKIN)
for polygon in skin_source.data.polygons:
    polygon.material_index = 0
    polygon.use_smooth = True


def bisect_mesh(obj, plane_co, plane_no, clear_inner=False, clear_outer=False):
    mesh = obj.data
    work = bmesh.new()
    work.from_mesh(mesh)
    bmesh.ops.bisect_plane(
        work,
        geom=list(work.verts) + list(work.edges) + list(work.faces),
        dist=0.00001,
        plane_co=plane_co,
        plane_no=plane_no,
        clear_inner=clear_inner,
        clear_outer=clear_outer,
    )
    work.to_mesh(mesh)
    work.free()
    mesh.update(calc_edges=True)


# Replace the long tunic template with the same authored male skin topology used
# by the forearms, then cut it on exact planes.  This produces a fitted shirt
# with no ruffles, holes, breast lobes, flared coat hem or assembled shoulders.
old_long_top_mesh = long_top.data
male_surface_shirt_mesh = bpy.data.meshes.new("FatherSurface7MaleSurfaceShirtSource")
male_surface_shirt_mesh.from_pydata(
    [tuple(point) for point in skin_source_coords],
    [],
    skin_source_faces,
)
male_surface_shirt_mesh.update(calc_edges=True)
long_top.data = male_surface_shirt_mesh
bpy.data.meshes.remove(old_long_top_mesh)
# End the torso shell inside the authored trouser waist.  The former source
# cut at 0.905 transformed to Father Z ~0.385 and left blue shirt fragments
# hanging around the hips after the correct high-waist trousers were restored.
bisect_mesh(long_top, (0.0, 0.0, SHIRT_SOURCE_HEM_Z), (0.0, 0.0, 1.0), clear_inner=True)
bisect_mesh(long_top, (0.0, 0.0, 1.550), (0.0, 0.0, 1.0), clear_outer=True)
bisect_mesh(long_top, (0.165, 0.0, 0.0), (1.0, 0.0, 0.0), clear_outer=True)
bisect_mesh(long_top, (-0.165, 0.0, 0.0), (1.0, 0.0, 0.0), clear_inner=True)
shirt_source_coords = [vertex.co.copy() for vertex in long_top.data.vertices]
shirt_source_faces = [tuple(polygon.vertices) for polygon in long_top.data.polygons]


shirt_faces, shirt_used = rebuild_object(
    long_top,
    shirt_source_coords,
    shirt_source_faces,
    lambda _face, _coordinates: True,
    "FatherSurface7AuthoredOnePieceShirtMesh",
)
shirt = long_top
shirt.name = "FatherSurface7OnePieceBodyShoulderSleeveShirt"
shirt.data.materials.append(MAT_SHIRT)
for new_index, source_index in enumerate(shirt_used):
    source = shirt_source_coords[source_index]
    if source.z < 1.16 and abs(source.x) < 0.34:
        waist_weight = 1.0 - smoothstep(0.91, 1.16, source.z)
        shirt.data.vertices[new_index].co.x *= 1.0 - 0.07 * waist_weight
        shirt.data.vertices[new_index].co.y *= 1.0 - 0.04 * waist_weight
for vertex in shirt.data.vertices:
    if vertex.co.z < SHIRT_TUCK_END_Z:
        tuck_weight = 1.0 - smoothstep(SHIRT_TUCK_BEGIN_Z, SHIRT_TUCK_END_Z, vertex.co.z)
        vertex.co.x *= 1.0 - 0.10 * tuck_weight
        vertex.co.y *= 1.0 - 0.06 * tuck_weight
    if vertex.co.z > 0.535 and abs(vertex.co.x) > 0.120:
        sign = 1.0 if vertex.co.x >= 0.0 else -1.0
        vertex.co.x = sign * (0.120 + (abs(vertex.co.x) - 0.120) * 0.03)
for polygon in shirt.data.polygons:
    polygon.material_index = 0
    polygon.use_smooth = True
smooth = shirt.modifiers.new("FatherSurface7ShirtRelax", "SMOOTH")
smooth.factor = 0.0
smooth.iterations = 1


def transform_complete_object(obj, material_slots, mesh_name):
    source_coords = [vertex.co.copy() for vertex in obj.data.vertices]
    source_faces = [tuple(polygon.vertices) for polygon in obj.data.polygons]
    source_centers = [
        sum((source_coords[index] for index in face), Vector()) / len(face)
        for face in source_faces
    ]
    rebuild_object(obj, source_coords, source_faces, lambda _f, _c: True, mesh_name)
    for material in material_slots:
        obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return source_centers


bisect_mesh(pants, (0.0, 0.0, PANTS_SOURCE_TOP_Z), (0.0, 0.0, 1.0), clear_outer=True)
pants_centers = transform_complete_object(
    pants,
    (MAT_PANTS,),
    "FatherSurface7AuthoredMaleTrouserMesh",
)
pants.name = "FatherSurface7AuthoredMaleTrousers"
for polygon, source_center in zip(pants.data.polygons, pants_centers):
    polygon.material_index = 0
for vertex in pants.data.vertices:
    if vertex.co.z > PANTS_WAIST_TAPER_BEGIN_Z:
        waist_t = smoothstep(PANTS_WAIST_TAPER_BEGIN_Z, PANTS_WAIST_TAPER_END_Z, vertex.co.z)
        target_half_width = (
            PANTS_WAIST_HALF_WIDTH_BOTTOM * (1.0 - waist_t)
            + PANTS_WAIST_HALF_WIDTH_TOP * waist_t
        )
        absolute_x = abs(vertex.co.x)
        if absolute_x > target_half_width:
            sign = 1.0 if vertex.co.x >= 0.0 else -1.0
            vertex.co.x = sign * (target_half_width + (absolute_x - target_half_width) * 0.05)
    if vertex.co.z < 0.430:
        sign = 1.0 if vertex.co.x >= 0.0 else -1.0
        vertex.co.x = sign * 0.055 + (vertex.co.x - sign * 0.060) * 0.78
        if abs(vertex.co.x) < 0.030:
            vertex.co.x *= 0.05

shoe_centers = transform_complete_object(
    shoes,
    (MAT_SHOE,),
    "FatherSurface7AuthoredMaleShoeMesh",
)
shoes.name = "FatherSurface7AuthoredBrownShoes"
for polygon, source_center in zip(shoes.data.polygons, shoe_centers):
    polygon.material_index = 0


def add_clean_arm(sign):
    """Build one smooth shoulder-to-wrist tube with a material-only sleeve break."""
    ring_count = 15
    ring_segments = 24
    shoulder = Vector((sign * 0.105, -0.020, 0.575))
    wrist = Vector((sign * 0.285, -0.012, 0.435))
    tangent = (wrist - shoulder).normalized()
    cross_xz = Vector((-tangent.z, 0.0, tangent.x)).normalized()
    cross_y = Vector((0.0, 1.0, 0.0))
    vertices = []
    ring_t = []
    for ring in range(ring_count):
        t = ring / (ring_count - 1)
        center = shoulder.lerp(wrist, t)
        center.x += sign * 0.004 * math.sin(math.pi * t)
        radius = 0.047 * (1.0 - t) + 0.035 * t + 0.0015 * math.sin(math.pi * t)
        depth_radius = radius * (1.24 - 0.44 * t)
        ring_t.append(t)
        for segment in range(ring_segments):
            angle = math.tau * segment / ring_segments
            point = (
                center
                + cross_xz * (math.cos(angle) * radius)
                + cross_y * (math.sin(angle) * depth_radius)
            )
            vertices.append(tuple(point))

    faces = []
    material_indices = []
    for ring in range(ring_count - 1):
        material_index = 0 if (ring_t[ring] + ring_t[ring + 1]) * 0.5 < 0.58 else 1
        for segment in range(ring_segments):
            nxt = (segment + 1) % ring_segments
            a = ring * ring_segments + segment
            b = ring * ring_segments + nxt
            c = (ring + 1) * ring_segments + nxt
            d = (ring + 1) * ring_segments + segment
            faces.append((a, b, c, d))
            material_indices.append(material_index)

    start_center_index = len(vertices)
    vertices.append(tuple(shoulder))
    end_center_index = len(vertices)
    vertices.append(tuple(wrist))
    for segment in range(ring_segments):
        nxt = (segment + 1) % ring_segments
        faces.append((start_center_index, nxt, segment))
        material_indices.append(0)
        a = (ring_count - 1) * ring_segments + segment
        b = (ring_count - 1) * ring_segments + nxt
        faces.append((end_center_index, a, b))
        material_indices.append(1)

    mesh = bpy.data.meshes.new(
        "FatherSurface11CleanArmMeshR" if sign > 0 else "FatherSurface11CleanArmMeshL"
    )
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    arm = bpy.data.objects.new(
        "FatherSurface11CleanArmR" if sign > 0 else "FatherSurface11CleanArmL",
        mesh,
    )
    scene.collection.objects.link(arm)
    mesh.materials.append(MAT_SHIRT)
    mesh.materials.append(MAT_SKIN)
    for polygon, material_index in zip(mesh.polygons, material_indices):
        polygon.material_index = material_index
        polygon.use_smooth = True
    arm["surfaceContinuity"] = "single shoulder-to-wrist tube; sleeve/skin boundary is material-only"
    arm["nativeHandReplacement"] = False
    return arm


clean_arms = [add_clean_arm(-1.0), add_clean_arm(1.0)]


def add_wrist_bridge(sign):
    start = Vector((sign * 0.214, -0.006, 0.498))
    end = Vector((sign * 0.247, -0.006, 0.458))
    direction = end - start
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, location=midpoint)
    bridge = bpy.context.object
    bridge.name = "FatherSurface7NativeWristBridgeR" if sign > 0 else "FatherSurface7NativeWristBridgeL"
    bridge.rotation_euler = Vector((0.0, 0.0, 1.0)).rotation_difference(direction.normalized()).to_euler()
    bridge.scale = (0.018, 0.018, direction.length * 0.5 + 0.012)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bridge.data.materials.append(MAT_SKIN)
    for polygon in bridge.data.polygons:
        polygon.use_smooth = True
    bridge["nativeHandReplacement"] = False
    bridge["surfacePolicy"] = "smooth overlap from authored male forearm into untouched owned wrist"
    return bridge


wrist_bridges = []


def mesh_bvh(obj):
    return BVHTree.FromPolygons(
        [vertex.co.copy() for vertex in obj.data.vertices],
        [tuple(polygon.vertices) for polygon in obj.data.polygons],
    )


shirt_bvh = mesh_bvh(shirt)
pants_bvh = mesh_bvh(pants)


def fitted_front_point(bvh, x, z, offset=0.0030):
    location, normal, _face_index, _distance = bvh.ray_cast(
        Vector((x, -1.0, z)), Vector((0.0, 1.0, 0.0)), 2.0
    )
    if location is None or normal is None:
        location, normal, _face_index, distance = bvh.find_nearest(Vector((x, -0.14, z)), 0.06)
        if location is None or normal is None or distance is None or distance > 0.06:
            raise RuntimeError(f"Could not fit front detail at x={x:.3f}, z={z:.3f}")
    normal.normalize()
    if normal.y > 0.0:
        normal.negate()
    return location + normal * offset, normal


def add_fitted_curve(name, points, material, bevel=0.0017, cyclic=False):
    curve = bpy.data.curves.new(name + "Curve", type="CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = bevel
    curve.bevel_resolution = 3
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, coordinate in zip(spline.points, points):
        point.co = (*coordinate, 1.0)
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    scene.collection.objects.link(obj)
    curve.materials.append(material)
    return obj


placket_points = [fitted_front_point(shirt_bvh, 0.0, z)[0] for z in (0.458, 0.495, 0.535, 0.575, 0.615)]
placket = add_fitted_curve("FatherSurface7FittedPlacketSeam", placket_points, MAT_TAILOR, 0.00145)

collar_points = [
    fitted_front_point(shirt_bvh, x, z)[0]
    for x, z in ((-0.052, 0.598), (-0.030, 0.578), (0.0, 0.562), (0.030, 0.578), (0.052, 0.598))
]
collar = add_fitted_curve("FatherSurface7FittedCollarSeam", collar_points, MAT_TAILOR, 0.0021)

pocket_outline = [
    fitted_front_point(shirt_bvh, x, z)[0]
    for x, z in ((-0.085, 0.542), (-0.043, 0.542), (-0.043, 0.507), (-0.064, 0.496), (-0.085, 0.507))
]
pocket = add_fitted_curve(
    "FatherSurface7FittedPocketStitch", pocket_outline, MAT_TAILOR, 0.00145, cyclic=True
)

buttons = []
for index, z in enumerate((0.472, 0.510, 0.548, 0.586), 1):
    location, normal = fitted_front_point(shirt_bvh, 0.0, z, 0.0038)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, location=location)
    button = bpy.context.object
    button.name = f"FatherSurface7FittedButton{index}"
    button.rotation_euler = Vector((0.0, 0.0, 1.0)).rotation_difference(normal).to_euler()
    button.scale = (0.0048, 0.0026, 0.0048)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    button.data.materials.append(MAT_SILVER)
    for polygon in button.data.polygons:
        polygon.use_smooth = True
    buttons.append(button)

buckle = bpy.data.objects.new("FatherSurface7BeltBuckleDisabled", None)
scene.collection.objects.link(buckle)
buckle.hide_render = True

for obj in (
    skin_source,
    shirt,
    pants,
    shoes,
    placket,
    collar,
    pocket,
    buckle,
    clean_cranium,
    *clean_arms,
    *wrist_bridges,
    *buttons,
):
    obj["candidateClaim"] = False
    obj["test3SakurakoExcluded"] = True
    obj["surface7Policy"] = "authored male surface or fitted detail; no box/plate torso assembly"

skin_source["surfaceContinuity"] = "hidden VRoid source provenance; warped forearm faces removed"
shirt["surfaceContinuity"] = "one copied male-surface shell connecting torso, both shoulders and both rolled sleeves"
pants["surfaceContinuity"] = "authored male trouser topology retained through the waist and seat; belt is a material region on the same mesh"
shoes["surfaceContinuity"] = "authored paired VRoid shoe topology"

owned_coordinate_after = coordinate_hash(owned_body)
owned_weight_after = weight_hash(owned_body)
hand_coordinate_after = coordinate_hash(owned_body, native_hand_vertices)
hand_weight_after = weight_hash(owned_body, native_hand_vertices)
if owned_coordinate_before != owned_coordinate_after or owned_weight_before != owned_weight_after:
    raise RuntimeError("Surface7 changed the owned Yuuka identity mesh or weights")
if hand_coordinate_before != hand_coordinate_after or hand_weight_before != hand_weight_after:
    raise RuntimeError("Surface7 changed exact native Yuuka hand coordinates or weights")
if bone_names_before != sorted(bone.name for bone in armature.data.bones):
    raise RuntimeError("Surface7 changed the owned Yuuka rig")

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.look = "AgX - Medium High Contrast"
if scene.world is not None:
    scene.world.color = (0.022, 0.026, 0.034)

center = Vector((0.0, 0.005, 0.495))
distance = 4.0
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.10
views = {
    "front": Vector((0.0, -distance, 0.0)),
    "three-quarter": Vector((distance * 0.66, -distance * 0.76, 0.0)),
    "side": Vector((distance, 0.0, 0.0)),
    "back": Vector((0.0, distance, 0.0)),
}


def render_views(prefix):
    paths = []
    for label, offset in views.items():
        camera.location = center + offset
        camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
        path = OUTPUT / f"{prefix}-{label}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(str(path))
    return paths


color_paths = render_views("father-surface7-color")

gray_backups = {}
for obj in scene.objects:
    if obj.hide_render or obj.type not in {"MESH", "CURVE"} or not hasattr(obj.data, "materials"):
        continue
    gray_backups[obj.name] = list(obj.data.materials)
    obj.data.materials.clear()
    obj.data.materials.append(MAT_GRAY)
    if obj.type == "MESH":
        for polygon in obj.data.polygons:
            polygon.material_index = 0
gray_paths = render_views("father-surface7-gray")
for name, materials in gray_backups.items():
    obj = bpy.data.objects.get(name)
    if obj is None:
        continue
    obj.data.materials.clear()
    for material in materials:
        obj.data.materials.append(material)

blend_path = OUTPUT / "father-vroid-male-surface7-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

receipt = {
    "schema": "family-company.father-vroid-male-surface7-gate.v1",
    "status": "STATIC_INTERNAL_QA_PENDING",
    "candidateClaim": False,
    "promotionBlocked": True,
    "identitySource": "user-owned test2 Yuuka FatherProof3 face with user-requested anti-squash proportion correction; short hair, glasses, expression and exact native three-digit hands",
    "outfitAuthority": str(REFERENCE),
    "outfitAuthoritySha256": sha256(REFERENCE),
    "vroidMaleSurface": {
        "obj": str(VROID_OBJ),
        "objSha256": sha256(VROID_OBJ),
        "installedSource": str(VROID_SOURCE),
        "installedSourceSha256": sha256(VROID_SOURCE),
        "officialGuidelines": "https://vroid.com/en/studio/guidelines",
        "meshPathId": 2617,
        "maleMaterialFamily": "M00",
    },
    "test3SakurakoExcluded": True,
    "headProportionCorrection": {
        "reason": "user rejected the inherited head as horizontally squashed",
        "pivot": list(HEAD_PIVOT),
        "scaleXYZ": list(HEAD_SCALE),
        "affectedOwnedVertices": "Yuuka_Original_Body vertices with world Z > 0.600; native hands excluded",
    },
    "retained": {
        "ownedCoordinatesStableAfterRequestedHeadCorrection": owned_coordinate_before == owned_coordinate_after,
        "ownedWeightsExact": owned_weight_before == owned_weight_after,
        "nativeHandComponents": native_hand_components,
        "nativeHandsExact": hand_coordinate_before == hand_coordinate_after and hand_weight_before == hand_weight_after,
        "nativeHandDescription": "all six original user-owned Yuuka 3-digit stylized hand islands",
        "rigBoneCount": len(armature.data.bones),
        "rigBoneNamesExact": bone_names_before == sorted(bone.name for bone in armature.data.bones),
    },
    "surfacePolicy": {
        "organicMaleBody": skin_source.name,
        "onePieceShirt": shirt.name,
        "authoredTrousers": pants.name,
        "trouserSeatCoverage": {
            "sourceTopZ": PANTS_SOURCE_TOP_Z,
            "fatherBeltRegionZ": [BELT_REGION_BOTTOM_Z, BELT_REGION_TOP_Z],
            "shirtSourceHemZ": SHIRT_SOURCE_HEM_Z,
            "waistTaperZ": [PANTS_WAIST_TAPER_BEGIN_Z, PANTS_WAIST_TAPER_END_Z],
            "waistHalfWidth": [PANTS_WAIST_HALF_WIDTH_BOTTOM, PANTS_WAIST_HALF_WIDTH_TOP],
            "shirtTuckZ": [SHIRT_TUCK_BEGIN_Z, SHIRT_TUCK_END_Z],
            "shirtTuckedInsideWaist": True,
        },
        "authoredShoes": shoes.name,
        "cleanRearCranium": clean_cranium.name,
        "cleanContinuousArms": [arm.name for arm in clean_arms],
        "beltIsTrouserMaterialRegion": True,
        "detachedTorsoShoulderSleevePieces": 0,
        "voxelRemeshUsed": False,
        "proceduralBoxTorsoUsed": False,
        "removedFailedObjects": removed_failed_objects,
        "fittedSeams": [placket.name, collar.name, pocket.name],
        "fittedButtons": [button.name for button in buttons],
        "wristBridges": [bridge.name for bridge in wrist_bridges],
        "fittedBuckle": None,
    },
    "renders": {"color": color_paths, "gray": gray_paths},
    "blend": str(blend_path),
    "knownLimitations": [
        "static visual gate only; no rig transfer, motion, Unity or production claim",
        "user visual approval is required before animation work",
    ],
}
receipt_path = OUTPUT / "father-vroid-male-surface7-gate-receipt.json"
receipt_path.write_text(json.dumps(receipt, indent=2, ensure_ascii=False), encoding="utf-8")
print(receipt_path)
