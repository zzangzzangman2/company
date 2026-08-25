"""Player Proof10 low-detail topology gate.

Visible character policy:
- retain the user-owned Yuuka face, eyes, three-digit hands, original A-pose,
  weights, Armature modifier, and 118-bone rig;
- use the untouched full donor body only as a render-hidden weight source;
- build cap, hoodie, shirt, pants, and shoes as new closed connected topology;
- no visible primitive stacking, floating trim, zipper, pocket, ribbon, or
  stripe geometry. Construction cages are voxel-unioned and deleted;
- output grayscale solid+wire proofs only. No color/final/GIF output.
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
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", choices=("10", "11", "12", "13", "14"), default="10")
    parser.add_argument(
        "--output",
        default="Artifacts/Family3DPlayerHumanV5/Proof10TopologyGate",
    )
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(argv)


ARGS = parse_args()
IS_PROOF11 = ARGS.version in {"11", "12", "13", "14"}
IS_PROOF12 = ARGS.version == "12"
IS_PROOF13 = ARGS.version == "13"
IS_PROOF14 = ARGS.version == "14"
PROOF_NUMBER = ARGS.version
PROOF_TAG = f"PlayerProof{PROOF_NUMBER}"
FILE_TAG = f"player-proof{PROOF_NUMBER}"
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


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def gray_material(name: str, value: float, roughness=0.68):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = (value, value, value, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    if "IOR Level" in shader.inputs:
        shader.inputs["IOR Level"].default_value = 0.20
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def delete_vertices(obj, indices) -> None:
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    mesh.verts.ensure_lookup_table()
    targets = [mesh.verts[index] for index in sorted(indices) if index < len(mesh.verts)]
    bmesh.ops.delete(mesh, geom=targets, context="VERTS")
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def delete_polygons(obj, indices) -> None:
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    mesh.faces.ensure_lookup_table()
    targets = [mesh.faces[index] for index in sorted(indices) if index < len(mesh.faces)]
    bmesh.ops.delete(mesh, geom=targets, context="FACES")
    loose = [vertex for vertex in mesh.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(mesh, geom=loose, context="VERTS")
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def polygon_group_score(obj, polygon, predicate):
    total = 0.0
    for vertex_index in polygon.vertices:
        for membership in obj.data.vertices[vertex_index].groups:
            name = obj.vertex_groups[membership.group].name
            if predicate(name):
                total += membership.weight
    return total / max(len(polygon.vertices), 1)


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
    groups = defaultdict(set)
    for vertex in mesh.vertices:
        groups[find(vertex.index)].add(vertex.index)
    roots = sorted(groups, key=lambda root: min(groups[root]))
    vertex_component = {}
    components = []
    for component_id, root in enumerate(roots):
        item = {"id": component_id, "vertices": groups[root], "polygons": set()}
        components.append(item)
        for index in item["vertices"]:
            vertex_component[index] = component_id
    for polygon in mesh.polygons:
        components[vertex_component[polygon.vertices[0]]]["polygons"].add(polygon.index)
    return components


def topology_stats(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    components = 0
    unseen = set(mesh.verts)
    while unseen:
        root = unseen.pop()
        stack = [root]
        components += 1
        while stack:
            vertex = stack.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in unseen:
                    unseen.remove(other)
                    stack.append(other)
    result = {
        "vertices": len(mesh.verts),
        "edges": len(mesh.edges),
        "polygons": len(mesh.faces),
        "connectedComponents": components,
        "boundaryEdges": sum(1 for edge in mesh.edges if len(edge.link_faces) == 1),
        "nonManifoldEdges": sum(1 for edge in mesh.edges if len(edge.link_faces) != 2),
        "nonQuadPolygons": sum(1 for face in mesh.faces if len(face.verts) != 4),
    }
    mesh.free()
    return result


def coordinate_hash(points) -> str:
    normalized = sorted(tuple(round(float(value), 7) for value in point) for point in points)
    payload = json.dumps(normalized, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(payload).hexdigest().upper()


def recalc_normals(obj) -> None:
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def quad_boundary_cage(nx, ny, nz, mapper):
    """Closed all-quad boundary of a subdivided retopo cage."""
    vertices = []
    faces = []
    lookup = {}

    def vertex_id(i, j, k):
        key = (i, j, k)
        if key not in lookup:
            u = -1.0 + 2.0 * i / nx
            v = -1.0 + 2.0 * j / ny
            w = -1.0 + 2.0 * k / nz
            lookup[key] = len(vertices)
            vertices.append(tuple(mapper(u, v, w)))
        return lookup[key]

    for j in range(ny):
        for k in range(nz):
            faces.append((vertex_id(0, j, k), vertex_id(0, j, k + 1), vertex_id(0, j + 1, k + 1), vertex_id(0, j + 1, k)))
            faces.append((vertex_id(nx, j, k), vertex_id(nx, j + 1, k), vertex_id(nx, j + 1, k + 1), vertex_id(nx, j, k + 1)))
    for i in range(nx):
        for k in range(nz):
            faces.append((vertex_id(i, 0, k), vertex_id(i + 1, 0, k), vertex_id(i + 1, 0, k + 1), vertex_id(i, 0, k + 1)))
            faces.append((vertex_id(i, ny, k), vertex_id(i, ny, k + 1), vertex_id(i + 1, ny, k + 1), vertex_id(i + 1, ny, k)))
    for i in range(nx):
        for j in range(ny):
            faces.append((vertex_id(i, j, 0), vertex_id(i, j + 1, 0), vertex_id(i + 1, j + 1, 0), vertex_id(i + 1, j, 0)))
            faces.append((vertex_id(i, j, nz), vertex_id(i + 1, j, nz), vertex_id(i + 1, j + 1, nz), vertex_id(i, j + 1, nz)))
    return vertices, faces


def square_to_disk(u, v):
    return (
        u * math.sqrt(max(0.0, 1.0 - 0.5 * v * v)),
        v * math.sqrt(max(0.0, 1.0 - 0.5 * u * u)),
    )


def block_mapper(center, half):
    center = Vector(center)
    half = Vector(half)

    def mapper(u, v, w):
        return center + Vector((half.x * u, half.y * v, half.z * w))

    return mapper


def oriented_tube_mapper(start, end, radius_start, radius_end, cuff=False):
    start = Vector(start)
    end = Vector(end)
    axis = end - start
    axis_length = axis.length
    axis_direction = axis.normalized()
    lateral = Vector((-axis_direction.z, 0.0, axis_direction.x)).normalized()
    depth = Vector((0.0, 1.0, 0.0))

    def mapper(u, v, w):
        t = (w + 1.0) * 0.5
        disk_x, disk_y = square_to_disk(u, v)
        radius = radius_start * (1.0 - t) + radius_end * t
        if cuff and t > 0.78:
            radius *= 1.0 + 0.18 * math.sin(min(1.0, (t - 0.78) / 0.22) * math.pi)
        center = start + axis_direction * axis_length * t
        return center + lateral * disk_x * radius + depth * disk_y * radius * 0.78

    return mapper


def raw_world_object(name, vertices, faces):
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    recalc_normals(obj)
    return obj


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX))
armature = bpy.data.objects.get("Armature")
source = bpy.data.objects.get("Yuuka_Original_Body")
if armature is None or source is None:
    raise RuntimeError("Yuuka donor import failed")
armature.name = "PlayerProof10_Rig_Yuuka118"
armature.scale = tuple(component * 400.0 for component in armature.scale)
source.name = "PlayerProof10_WeightTransferSource_YuukaHidden"
for removable in ("Yuuka_Original_Calculator", "Yuuka_Original_Weapon"):
    obj = bpy.data.objects.get(removable)
    if obj:
        bpy.data.objects.remove(obj, do_unlink=True)
bpy.context.view_layer.update()

if len(armature.data.bones) != 118:
    raise RuntimeError("Expected Yuuka 118-bone rig")
components = connected_components(source.data)
if len(components) != 355:
    raise RuntimeError(f"Expected 355 Yuuka body components, got {len(components)}")

source.hide_render = True
source.hide_set(True)
armature.show_in_front = False

MAT_FACE = gray_material("Gate_FaceGray", 0.72)
MAT_HAIR = gray_material("Gate_HairGray", 0.20)
MAT_EYE = gray_material("Gate_EyeGray_BackfaceCulled", 0.34)
MAT_EYE.use_backface_culling = True
MAT_FACE.use_backface_culling = True
MAT_HAND = gray_material("Gate_HandGray", 0.68)
MAT_CAP = gray_material("Gate_CapGray", 0.46)
MAT_CAP_VISOR = gray_material("Gate_CapVisorGray_MaterialFacesOnly", 0.34)
MAT_HOODIE = gray_material("Gate_HoodieGray", 0.43)
MAT_SHIRT = gray_material("Gate_ShirtGray_StripesDeferredToShader", 0.69)
MAT_SHIRT_STRIPE = gray_material("Gate_ShirtStripeGray_MaterialFacesOnly", 0.27)
MAT_PANTS = gray_material("Gate_PantsGray", 0.38)
MAT_SHOE = gray_material("Gate_ShoeGray_ColorSplitDeferredToMaterialFaces", 0.50)
MAT_MOUTH = gray_material("Gate_SurfaceMouthGray", 0.16)
MAT_MOUTH.use_backface_culling = False


def replace_all_materials(obj, material):
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = True


# Immutable visible head/eyes/hair copy. c346 opaque mouth plate is excluded.
head = source.copy()
head.data = source.data.copy()
head.name = "PlayerProof10_ImmutableFaceEyesShortHair"
bpy.context.collection.objects.link(head)
head.hide_render = False
head.hide_set(False)
face_ids = set(range(232, 268))
brow_ids = {344, 345}
eye_ids = set(range(347, 355))
short_hair_ids = {281, 282, 329, 330, 337, 338, 339, 340, 342, 343}
head_keep_ids = face_ids | brow_ids | eye_ids | (set() if IS_PROOF11 else short_hair_ids)
head_keep_vertices = set().union(*(components[index]["vertices"] for index in head_keep_ids))
delete_vertices(head, set(range(len(head.data.vertices))) - head_keep_vertices)

# Preserve grayscale separation by original material names after compaction.
for index, material in enumerate(list(head.data.materials)):
    name = material.name if material else ""
    if "Hair" in name:
        head.data.materials[index] = MAT_HAIR
    elif "EyeMouth" in name:
        head.data.materials[index] = MAT_EYE
    elif "Eyebrow" in name:
        head.data.materials[index] = MAT_EYE
    elif "Face" in name:
        head.data.materials[index] = MAT_FACE
    else:
        head.data.materials[index] = MAT_FACE
for polygon in head.data.polygons:
    polygon.use_smooth = True

if IS_PROOF14:
    # The donor face shell includes an internal cranium cap that normally sits
    # beneath Yuuka's original hair.  Only this under-hat region is removed
    # from the visible copy so it cannot pierce the new cap as Proof13's fin.
    # All facial landmarks, eyes, brows, ears and lower head remain untouched.
    hidden_cranium_vertices = {
        vertex.index
        for vertex in head.data.vertices
        if (head.matrix_world @ vertex.co).z > 3.320
    }
    delete_vertices(head, hidden_cranium_vertices)

# Proof11 uses a separate donor-hair object so rear review can prove that no
# face/eye back surfaces are visible. Keep the original scalp/front pieces and
# the same directly cropped bone_hair_b_* upper locks that passed Proof6.
hair = None
if IS_PROOF11:
    hair_material_index = next(
        index
        for index, material in enumerate(source.data.materials)
        if material and "Hair" in material.name
    )
    retained_hair_polygons = set().union(
        *(components[index]["polygons"] for index in short_hair_ids)
    )
    for polygon in source.data.polygons:
        if polygon.material_index != hair_material_index:
            continue
        center = source.matrix_world @ polygon.center
        total = max(
            polygon_group_score(source, polygon, lambda _name: True),
            1.0e-6,
        )
        back_chain = polygon_group_score(
            source,
            polygon,
            lambda name: name.startswith("bone_hair_b_"),
        )
        twin_tail = polygon_group_score(
            source,
            polygon,
            lambda name: name.startswith("bone_hair_l_") or name.startswith("bone_hair_r_"),
        )
        if (
            back_chain / total > 0.22
            and twin_tail / total < 0.08
            and center.z >= (2.82 if (IS_PROOF12 or IS_PROOF13 or IS_PROOF14) else 2.70)
            and abs(center.x) < 0.72
        ):
            retained_hair_polygons.add(polygon.index)
    hair = source.copy()
    hair.data = source.data.copy()
    hair.name = f"{PROOF_TAG}_ShortHair_DonorScalpAndCroppedBackLocks"
    bpy.context.collection.objects.link(hair)
    hair.hide_render = False
    hair.hide_set(False)
    delete_polygons(
        hair,
        set(range(len(hair.data.polygons))) - retained_hair_polygons,
    )
    replace_all_materials(hair, MAT_HAIR)
    hair_inverse = hair.matrix_world.inverted()
    for vertex in hair.data.vertices:
        point = hair.matrix_world @ vertex.co
        if point.y > 0.18:
            point.y = 0.20 + (point.y - 0.18) * 0.14
        if IS_PROOF14:
            # Keep the retained donor fringe/nape, but sink only the upper roots
            # beneath a gently curved hat line.  A constant Z clamp made the
            # Proof14 crown read as a helmet cut off by a horizontal shelf.
            cap_x = point.x / 0.475
            cap_y = (point.y + 0.020) / 0.345
            cap_radius_sq = min(1.0, cap_x * cap_x + cap_y * cap_y)
            cap_hair_limit = 3.245 + 0.040 * (1.0 - cap_radius_sq)
            if point.z > cap_hair_limit:
                point.z = cap_hair_limit
        elif (IS_PROOF12 or IS_PROOF13) and point.z > (3.355 if IS_PROOF13 else 3.455):
            # Flatten only cap-covered roots; bangs and nape remain untouched.
            point.z = 3.355 if IS_PROOF13 else 3.455
        vertex.co = hair_inverse @ point
    hair.data.update()
    recalc_normals(hair)

# Immutable visible original three-digit hands, split from the same donor.
hand_component_ids = (
    {60, 61, 62, 96, 97, 98}
    if IS_PROOF14
    else {60, 61, 62, 68, 69, 96, 97, 98, 104, 105, 216, 223}
)
source_hand_vertices = set().union(*(components[index]["vertices"] for index in hand_component_ids))
source_hand_points = [source.matrix_world @ source.data.vertices[index].co for index in source_hand_vertices]
hands = source.copy()
hands.data = source.data.copy()
hands.name = "PlayerProof10_ImmutableOriginalThreeDigitHands"
bpy.context.collection.objects.link(hands)
hands.hide_render = False
hands.hide_set(False)
delete_vertices(hands, set(range(len(hands.data.vertices))) - source_hand_vertices)
replace_all_materials(hands, MAT_HAND)
hands_points_after = [hands.matrix_world @ vertex.co for vertex in hands.data.vertices]
hand_hash_before = coordinate_hash(source_hand_points)
hand_hash_after = coordinate_hash(hands_points_after)
if hand_hash_before != hand_hash_after:
    raise RuntimeError("Original hand geometry/pose changed")

# KD-tree rest-space weight source. New retopology receives nearest donor
# vertex-group weights, then the same 118-bone armature modifier.
source_kd = KDTree(len(source.data.vertices))
for vertex in source.data.vertices:
    source_kd.insert(source.matrix_world @ vertex.co, vertex.index)
source_kd.balance()


def transfer_weights_and_armature(obj):
    for group in source.vertex_groups:
        obj.vertex_groups.new(name=group.name)
    group_names = {group.index: group.name for group in source.vertex_groups}
    for vertex in obj.data.vertices:
        world_point = obj.matrix_world @ vertex.co
        _, source_index, _ = source_kd.find(world_point)
        for membership in source.data.vertices[source_index].groups:
            obj.vertex_groups[group_names[membership.group]].add([vertex.index], membership.weight, "REPLACE")
    modifier = obj.modifiers.new("PlayerProof10_TransferredYuukaWeights118", "ARMATURE")
    modifier.object = armature


def skinned_world_object(name, world_vertices, faces, material):
    inverse = source.matrix_world.inverted()
    local_vertices = [tuple(inverse @ Vector(point)) for point in world_vertices]
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(local_vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = source.parent
    obj.matrix_parent_inverse = source.matrix_parent_inverse.copy()
    obj.matrix_world = source.matrix_world.copy()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = True
    recalc_normals(obj)
    transfer_weights_and_armature(obj)
    return obj


def donor_component_object(name, component_ids, material):
    """Isolate audited donor islands without changing their rig or weights."""
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = name
    bpy.context.collection.objects.link(obj)
    keep = set().union(*(components[index]["vertices"] for index in component_ids))
    delete_vertices(obj, set(range(len(obj.data.vertices))) - keep)
    replace_all_materials(obj, material)
    obj.hide_render = False
    obj.hide_set(False)
    return obj


def crop_world_below(obj, z_limit):
    targets = [
        vertex.index
        for vertex in obj.data.vertices
        if (obj.matrix_world @ vertex.co).z < z_limit
    ]
    delete_vertices(obj, targets)


def reshape_world(obj, transform):
    inverse = obj.matrix_world.inverted()
    for vertex in obj.data.vertices:
        vertex.co = inverse @ Vector(transform(obj.matrix_world @ vertex.co))
    obj.data.update()
    recalc_normals(obj)


def fill_boundary_holes(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    boundary = [edge for edge in mesh.edges if len(edge.link_faces) == 1]
    if boundary:
        bmesh.ops.holes_fill(mesh, edges=boundary, sides=0)
        bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def object_world_spec(obj):
    return (
        [tuple(obj.matrix_world @ vertex.co) for vertex in obj.data.vertices],
        [tuple(polygon.vertices) for polygon in obj.data.polygons],
    )


def voxel_union(
    name,
    part_specs,
    voxel_size,
    material,
    smooth_steps=1,
    smooth_factor=0.18,
    solidify_open=0.0,
):
    raw_parts = []
    for index, (vertices, faces) in enumerate(part_specs):
        raw = raw_world_object(f"QAConstruction_{name}_{index:02d}", vertices, faces)
        if solidify_open > 0.0 and topology_stats(raw)["boundaryEdges"] > 0:
            bpy.context.view_layer.objects.active = raw
            raw.select_set(True)
            modifier = raw.modifiers.new("QAConstruction_ClothThickness", "SOLIDIFY")
            modifier.thickness = solidify_open
            modifier.offset = 0.0
            modifier.use_even_offset = True
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            raw.select_set(False)
        raw_parts.append(raw)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in raw_parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = raw_parts[0]
    bpy.ops.object.join()
    joined = raw_parts[0]
    joined.name = f"QAConstruction_{name}_Joined"
    joined.data.remesh_voxel_size = voxel_size
    joined.data.remesh_voxel_adaptivity = 0.0
    bpy.context.view_layer.objects.active = joined
    bpy.ops.object.voxel_remesh()
    recalc_normals(joined)
    edit = bmesh.new()
    edit.from_mesh(joined.data)
    for _ in range(smooth_steps):
        bmesh.ops.smooth_vert(
            edit,
            verts=list(edit.verts),
            factor=smooth_factor,
            use_axis_x=True,
            use_axis_y=True,
            use_axis_z=True,
        )
    edit.to_mesh(joined.data)
    edit.free()
    world_vertices = [tuple(joined.matrix_world @ vertex.co) for vertex in joined.data.vertices]
    faces = [tuple(polygon.vertices) for polygon in joined.data.polygons]
    bpy.data.objects.remove(joined, do_unlink=True)
    return skinned_world_object(name, world_vertices, faces, material)


def fitted_oval_mapper(z_bottom, z_top, center_y, bottom_radius, top_radius):
    """Body-conforming closed oval volume; top/bottom stay part of one cage."""
    bottom_radius = Vector(bottom_radius)
    top_radius = Vector(top_radius)

    def mapper(u, v, w):
        t = (w + 1.0) * 0.5
        ease = t * t * (3.0 - 2.0 * t)
        radii = bottom_radius.lerp(top_radius, ease)
        disk_x, disk_y = square_to_disk(u, v)
        return Vector(
            (
                radii.x * disk_x,
                center_y + radii.y * disk_y,
                z_bottom + (z_top - z_bottom) * t,
            )
        )

    return mapper


def open_elliptic_jacket_shell(
    ntheta=30,
    nz=16,
    z_bottom=1.69,
    z_top=2.49,
    center_y=0.005,
    front_gap=0.34,
    thickness=0.048,
):
    """Closed U-section cloth volume wrapping the torso with a real front opening."""
    vertices = []
    faces = []
    # The arc runs around the back and sides, stopping at two front edges.
    theta_start = -math.pi + front_gap
    theta_end = math.pi - front_gap

    def radii(t):
        shoulder = t * t * (3.0 - 2.0 * t)
        waist_softness = 0.018 * math.sin(math.pi * t)
        return (
            0.445 + 0.060 * shoulder - waist_softness,
            0.226 + 0.020 * shoulder - 0.010 * math.sin(math.pi * t),
        )

    # layer 0 outer, layer 1 inner
    for layer in range(2):
        for k in range(nz + 1):
            t = k / nz
            rx, ry = radii(t)
            if layer == 1:
                rx -= thickness
                ry -= thickness
            z = z_bottom + (z_top - z_bottom) * t
            for i in range(ntheta + 1):
                theta = theta_start + (theta_end - theta_start) * i / ntheta
                vertices.append(
                    (
                        rx * math.sin(theta),
                        center_y + ry * math.cos(theta),
                        z,
                    )
                )

    row = ntheta + 1
    layer_size = row * (nz + 1)

    def vid(layer, k, i):
        return layer * layer_size + k * row + i

    # Outer and inner wrap surfaces.
    for k in range(nz):
        for i in range(ntheta):
            faces.append((vid(0, k, i), vid(0, k, i + 1), vid(0, k + 1, i + 1), vid(0, k + 1, i)))
            faces.append((vid(1, k, i), vid(1, k + 1, i), vid(1, k + 1, i + 1), vid(1, k, i + 1)))
    # Bottom/top hems and the two front vertical edge bridges close the volume.
    for i in range(ntheta):
        faces.append((vid(0, 0, i), vid(1, 0, i), vid(1, 0, i + 1), vid(0, 0, i + 1)))
        faces.append((vid(0, nz, i), vid(0, nz, i + 1), vid(1, nz, i + 1), vid(1, nz, i)))
    for k in range(nz):
        faces.append((vid(0, k, 0), vid(0, k + 1, 0), vid(1, k + 1, 0), vid(1, k, 0)))
        faces.append((vid(0, k, ntheta), vid(1, k, ntheta), vid(1, k + 1, ntheta), vid(0, k + 1, ntheta)))
    return vertices, faces


def hood_crescent_shell(
    segments=30,
    front_gap=0.48,
    radius_x=0.39,
    radius_y=0.205,
    center_y=0.105,
    thickness=0.042,
):
    """Closed soft hood ribbon: low at front shoulders, rounded at rear."""
    vertices = []
    faces = []
    theta_start = -math.pi + front_gap
    theta_end = math.pi - front_gap
    for i in range(segments + 1):
        theta = theta_start + (theta_end - theta_start) * i / segments
        back = 0.5 * (1.0 + math.cos(theta))
        bottom_z = 2.405 + 0.025 * back
        top_z = bottom_z + 0.075 + 0.205 * back
        for radial_layer in (0, 1):
            rx = radius_x - thickness * radial_layer
            ry = radius_y - thickness * radial_layer
            x = rx * math.sin(theta)
            y = center_y + ry * math.cos(theta)
            vertices.append((x, y, bottom_z))
            vertices.append((x, y, top_z))

    def vid(i, radial_layer, top):
        return i * 4 + radial_layer * 2 + top

    for i in range(segments):
        # outer, inner, lower bridge, upper bridge
        faces.append((vid(i, 0, 0), vid(i + 1, 0, 0), vid(i + 1, 0, 1), vid(i, 0, 1)))
        faces.append((vid(i, 1, 0), vid(i, 1, 1), vid(i + 1, 1, 1), vid(i + 1, 1, 0)))
        faces.append((vid(i, 0, 0), vid(i, 1, 0), vid(i + 1, 1, 0), vid(i + 1, 0, 0)))
        faces.append((vid(i, 0, 1), vid(i + 1, 0, 1), vid(i + 1, 1, 1), vid(i, 1, 1)))
    for i in (0, segments):
        if i == 0:
            faces.append((vid(i, 0, 0), vid(i, 0, 1), vid(i, 1, 1), vid(i, 1, 0)))
        else:
            faces.append((vid(i, 0, 0), vid(i, 1, 0), vid(i, 1, 1), vid(i, 0, 1)))
    return vertices, faces


def proof13_open_hoodie_shell(
    ntheta=40,
    nz=22,
    z_bottom=1.69,
    z_top=2.48,
    center_y=0.004,
    front_gap=0.400,
    thickness=0.044,
):
    """Slim donor-fit zip hoodie torso with a real open front.

    The profile is sampled from the c146/c157 fitted torso envelope, but the
    sharp donor lapels and coat tails are deliberately retopologized away.
    Outer/inner layers and all four borders are bridged into one closed shell.
    """
    vertices = []
    faces = []
    def smoothstep(value):
        value = max(0.0, min(1.0, value))
        return value * value * (3.0 - 2.0 * value)

    def radii(t):
        # Soft waist followed by a modest chest/shoulder expansion.  The top
        # is intentionally much narrower than Proof12's armor-like blocks.
        if IS_PROOF14:
            if t < 0.50:
                blend = smoothstep(t / 0.50)
                rx = 0.405 * (1.0 - blend) + 0.385 * blend
                ry = 0.190 * (1.0 - blend) + 0.178 * blend
            else:
                blend = smoothstep((t - 0.50) / 0.50)
                rx = 0.385 * (1.0 - blend) + 0.425 * blend
                ry = 0.178 * (1.0 - blend) + 0.195 * blend
            return rx, ry
        if t < 0.46:
            blend = smoothstep(t / 0.46)
            rx = 0.430 * (1.0 - blend) + 0.405 * blend
            ry = 0.205 * (1.0 - blend) + 0.188 * blend
        else:
            blend = smoothstep((t - 0.46) / 0.54)
            rx = 0.405 * (1.0 - blend) + 0.452 * blend
            ry = 0.188 * (1.0 - blend) + 0.207 * blend
        return rx, ry

    for layer in range(2):
        for k in range(nz + 1):
            t = k / nz
            rx, ry = radii(t)
            if layer == 1:
                rx -= thickness
                ry -= thickness
            upper_gap = 0.100 if IS_PROOF14 else 0.145
            gap = front_gap + upper_gap * smoothstep((t - 0.64) / 0.36)
            theta_start = -math.pi + gap
            theta_end = math.pi - gap
            for i in range(ntheta + 1):
                theta = theta_start + (theta_end - theta_start) * i / ntheta
                edge_factor = abs(2.0 * i / ntheta - 1.0)
                frontness = smoothstep((edge_factor - 0.64) / 0.36)
                neckline_depth = 0.085 if IS_PROOF14 else 0.145
                neckline_drop = neckline_depth * frontness * (t ** 6)
                shoulder_drop = (
                    0.052 * (abs(math.sin(theta)) ** 2) * (t ** 7)
                    if IS_PROOF14
                    else 0.0
                )
                z = z_bottom + (z_top - z_bottom) * t - neckline_drop - shoulder_drop
                vertices.append((rx * math.sin(theta), center_y + ry * math.cos(theta), z))

    row = ntheta + 1
    layer_size = row * (nz + 1)

    def vid(layer, k, i):
        return layer * layer_size + k * row + i

    for k in range(nz):
        for i in range(ntheta):
            faces.append((vid(0, k, i), vid(0, k, i + 1), vid(0, k + 1, i + 1), vid(0, k + 1, i)))
            faces.append((vid(1, k, i), vid(1, k + 1, i), vid(1, k + 1, i + 1), vid(1, k, i + 1)))
    for i in range(ntheta):
        faces.append((vid(0, 0, i), vid(1, 0, i), vid(1, 0, i + 1), vid(0, 0, i + 1)))
        faces.append((vid(0, nz, i), vid(0, nz, i + 1), vid(1, nz, i + 1), vid(1, nz, i)))
    for k in range(nz):
        faces.append((vid(0, k, 0), vid(0, k + 1, 0), vid(1, k + 1, 0), vid(1, k, 0)))
        faces.append((vid(0, k, ntheta), vid(1, k, ntheta), vid(1, k + 1, ntheta), vid(0, k + 1, ntheta)))
    return vertices, faces


def proof13_hood_pouch_mapper(u, v, w):
    """Small soft hood resting only behind the neck, not a rear slab."""
    direction = Vector((u, v, w))
    direction.normalize()
    dz = direction.z
    taper = 1.0 - 0.13 * max(0.0, dz)
    return Vector(
        (
            0.285 * direction.x * taper,
            0.145 + 0.105 * direction.y + 0.015 * dz,
            2.430 + 0.115 * dz,
        )
    )


def proof14_curved_sleeve_mapper(side):
    """Soft quadratic A-pose sleeve with an organic wrist taper."""
    p0 = Vector((0.365 * side, -0.002, 2.285))
    p1 = Vector((0.710 * side, 0.008, 2.105))
    p2 = Vector((1.085 * side, -0.010, 1.720))

    def mapper(u, v, w):
        t = (w + 1.0) * 0.5
        omt = 1.0 - t
        center = p0 * (omt * omt) + p1 * (2.0 * omt * t) + p2 * (t * t)
        tangent = (p1 - p0) * (2.0 * omt) + (p2 - p1) * (2.0 * t)
        tangent.normalize()
        lateral = Vector((-tangent.z, 0.0, tangent.x)).normalized()
        depth = Vector((0.0, 1.0, 0.0))
        disk_x, disk_y = square_to_disk(u, v)
        radius = 0.162 * omt + 0.106 * t
        radius += 0.008 * math.sin(math.pi * t) ** 2
        # Integrated, subtle cuff fullness only—never a separate ring.
        radius += 0.005 * math.exp(-((t - 0.92) / 0.07) ** 2)
        return center + lateral * disk_x * radius + depth * disk_y * radius * 0.76

    return mapper


def proof14_collar_shell(segments=36, front_gap=0.62):
    """Small U collar around the neck, continuously welded to the hoodie."""
    vertices = []
    faces = []
    theta_start = -math.pi + front_gap
    theta_end = math.pi - front_gap
    for i in range(segments + 1):
        theta = theta_start + (theta_end - theta_start) * i / segments
        back = 0.5 * (1.0 + math.cos(theta))
        bottom_z = 2.375 + 0.010 * back
        top_z = 2.440 + 0.040 * back
        for radial_layer in (0, 1):
            rx = 0.292 - 0.052 * radial_layer
            ry = 0.174 - 0.050 * radial_layer
            x = rx * math.sin(theta)
            y = 0.030 + ry * math.cos(theta)
            vertices.append((x, y, bottom_z))
            vertices.append((x, y, top_z))

    def vid(i, radial_layer, top):
        return i * 4 + radial_layer * 2 + top

    for i in range(segments):
        faces.append((vid(i, 0, 0), vid(i + 1, 0, 0), vid(i + 1, 0, 1), vid(i, 0, 1)))
        faces.append((vid(i, 1, 0), vid(i, 1, 1), vid(i + 1, 1, 1), vid(i + 1, 1, 0)))
        faces.append((vid(i, 0, 0), vid(i, 1, 0), vid(i + 1, 1, 0), vid(i + 1, 0, 0)))
        faces.append((vid(i, 0, 1), vid(i + 1, 0, 1), vid(i + 1, 1, 1), vid(i, 1, 1)))
    faces.append((vid(0, 0, 0), vid(0, 0, 1), vid(0, 1, 1), vid(0, 1, 0)))
    faces.append((vid(segments, 0, 0), vid(segments, 1, 0), vid(segments, 1, 1), vid(segments, 0, 1)))
    return vertices, faces


def proof14_hood_pouch_mapper(u, v, w):
    """Flattened collapsed hood draped onto the upper back, never a bun."""
    dx, dz = square_to_disk(u, v)
    radius_sq = min(1.0, dx * dx + dz * dz)
    layer = (w + 1.0) * 0.5
    x = 0.300 * dx * (1.0 - 0.08 * max(0.0, dz))
    z = 2.335 + 0.165 * dz
    front_y = 0.105 + 0.015 * (1.0 - radius_sq)
    back_y = 0.195 + 0.030 * (1.0 - radius_sq)
    y = front_y * (1.0 - layer) + back_y * layer
    return Vector((x, y, z))


nape = None


# One closed connected all-quad cap cage. Crown, subtle band pinch and short
# front brim are deformations of the same cage, never overlapping pieces.
def cap_mapper(u, v, w):
    direction = Vector((u, v, w))
    direction.normalize()
    dz = direction.z
    if IS_PROOF11:
        # Squashed newsboy crown, an integrated lower band pinch, and a broad
        # projecting visor all deform the same closed cage. No stacked brim.
        band = math.exp(-((dz + 0.50) / 0.15) ** 2)
        crown_flare = 1.0 + 0.085 * math.exp(-((dz - 0.05) / 0.48) ** 2)
        radial = crown_flare * (1.0 - 0.085 * band)
        x = 0.555 * direction.x * radial
        y = 0.010 + 0.425 * direction.y * radial
        z = 3.720 + 0.255 * dz
        front_y = max(0.0, min(1.0, (-direction.y - 0.12) / 0.82))
        lower = max(0.0, min(1.0, (-dz + 0.10) / 0.72))
        center_weight = max(0.0, 1.0 - (abs(direction.x) / 0.92) ** 2)
        visor = front_y * lower * center_weight
        y -= 0.470 * visor
        visor_z = 3.535 + 0.018 * (1.0 - direction.x * direction.x)
        z = z * (1.0 - 0.82 * visor) + visor_z * 0.82 * visor
    else:
        band = math.exp(-((dz + 0.42) / 0.17) ** 2)
        radial = 1.0 - 0.045 * band
        x = 0.555 * direction.x * radial
        y = 0.025 + 0.455 * direction.y * radial
        z = 3.745 + 0.285 * dz
        front = max(0.0, min(1.0, (-direction.y - 0.18) / 0.72)) * max(0.0, min(1.0, (-dz + 0.18) / 0.90))
        y -= 0.175 * front
        z = z * (1.0 - 0.28 * front) + (3.625 + 0.018 * (1.0 - direction.x * direction.x)) * 0.28 * front
    return Vector((x, y, z))


if IS_PROOF14:
    def proof14_crown_mapper(u, v, w):
        """Compact shallow newsboy lens, not a spherical helmet.

        The broad, gently flattened upper panel rolls down to a low perimeter;
        its closed underside stays buried in the donor hair.  A small forward
        bias and side tilt keep the crown from reading as a symmetric bowl.
        """
        dx, dy = square_to_disk(u, v)
        radius_sq = min(1.0, dx * dx + dy * dy)
        layer = (w + 1.0) * 0.5
        dome = max(0.0, 1.0 - radius_sq) ** 0.42
        x = -0.006 + 0.475 * dx
        y = -0.020 + 0.345 * dy
        top_z = 3.265 + 0.300 * dome * (1.0 - 0.045 * dy)
        top_z += 0.010 * dx
        # Deep closed underside supplies real scalp overlap without changing
        # the visible crown silhouette.
        bottom_z = 3.120 + 0.100 * max(0.0, 1.0 - radius_sq) ** 0.70
        return Vector((x, y, bottom_z * (1.0 - layer) + top_z * layer))

    def proof14_visor_mapper(u, v, w):
        """Moderately projecting curved visor with a broad root in the crown."""
        dx, dy = square_to_disk(u, v)
        layer = (w + 1.0) * 0.5
        length = 0.215 if dy < 0.0 else 0.165
        x = 0.345 * dx * (1.0 - 0.100 * max(0.0, -dy))
        y = -0.405 + length * dy
        frontness = max(0.0, -dy)
        # The root is buried in the lens; the tip curves down just enough to
        # stay readable from front and profile without becoming a spear.
        top_z = 3.345 - 0.050 * frontness + 0.010 * dx * dx
        bottom_z = top_z - 0.038
        return Vector((x, y, bottom_z * (1.0 - layer) + top_z * layer))

    cap = voxel_union(
        f"{PROOF_TAG}_Cap_OnePuffedCrownCurvedVisor",
        [
            quad_boundary_cage(14, 14, 14, proof14_crown_mapper),
            quad_boundary_cage(18, 14, 5, proof14_visor_mapper),
        ],
        0.007,
        MAT_CAP,
        smooth_steps=5,
        smooth_factor=0.15,
    )
    cap.data.materials.append(MAT_CAP_VISOR)
    for polygon in cap.data.polygons:
        center = cap.matrix_world @ polygon.center
        if center.y < -0.405 and center.z < 3.345:
            polygon.material_index = 1
elif IS_PROOF13:
    def proof13_cap_mapper(u, v, w):
        """Compact dome volume whose lower-front surface becomes the visor.

        Unlike the rejected ellipsoid, this is a real hat volume with a broad
        underside at the hairline.  That underside hides the unchanged donor
        cranium instead of closing to a floating center point.
        """
        dx, dy = square_to_disk(u, v)
        radius_sq = min(1.0, dx * dx + dy * dy)
        radius = math.sqrt(radius_sq)
        layer = (w + 1.0) * 0.5

        def smoothstep(value):
            value = max(0.0, min(1.0, value))
            return value * value * (3.0 - 2.0 * value)

        front = smoothstep((-dy - 0.08) / 0.72)
        outer = smoothstep((radius - 0.34) / 0.58)
        center = smoothstep((0.98 - abs(dx)) / 0.22)
        visor = front * outer * center

        x = 0.510 * dx * (1.0 - 0.045 * visor)
        y = -0.010 + 0.370 * dy - 0.320 * visor

        dome = max(0.0, 1.0 - radius_sq) ** 0.58
        bottom_z = 3.395 - 0.030 * dome
        top_z = 3.420 + 0.250 * dome
        # Flatten the same outer-front top patch into a visibly projecting
        # newsboy visor while retaining a single crown/visor manifold.
        top_z = top_z * (1.0 - 0.78 * visor) + (3.404 + 0.010 * (1.0 - dx * dx)) * 0.78 * visor
        z = bottom_z * (1.0 - layer) + top_z * layer
        y -= 0.035 * dome * layer
        return Vector((x, y, z))

    cap_vertices, cap_faces = quad_boundary_cage(24, 24, 8, proof13_cap_mapper)
    cap = skinned_world_object(
        f"{PROOF_TAG}_Cap_OneCompactSkullFitCrownIntegratedVisor",
        cap_vertices,
        cap_faces,
        MAT_CAP,
    )
elif IS_PROOF12:
    def proof12_crown_mapper(u, v, w):
        direction = Vector((u, v, w))
        direction.normalize()
        dz = direction.z
        mid_flare = 1.0 + 0.055 * math.exp(-((dz - 0.02) / 0.52) ** 2)
        band = math.exp(-((dz + 0.58) / 0.16) ** 2)
        radial = mid_flare * (1.0 - 0.075 * band)
        return Vector(
            (
                0.520 * direction.x * radial,
                0.008 + 0.392 * direction.y * radial,
                3.695 + 0.238 * dz,
            )
        )

    def proof12_visor_mapper(u, v, w):
        direction = Vector((u, 0.82 * v, 1.55 * w))
        direction.normalize()
        dx, dy, dz = direction
        length = 0.285 if dy < 0.0 else 0.145
        return Vector(
            (
                0.385 * dx * (1.0 - 0.10 * max(0.0, -dy)),
                -0.405 + length * dy,
                3.488 + 0.052 * dz + 0.012 * (1.0 - dx * dx),
            )
        )

    cap_parts = [
        quad_boundary_cage(10, 10, 10, proof12_crown_mapper),
        quad_boundary_cage(10, 10, 5, proof12_visor_mapper),
    ]
    cap = voxel_union(
        f"{PROOF_TAG}_Cap_OneConnectedNewsboyCrownBandVisor",
        cap_parts,
        0.014,
        MAT_CAP,
        smooth_steps=2,
        smooth_factor=0.16,
    )
else:
    cap_vertices, cap_faces = quad_boundary_cage(8, 8, 8, cap_mapper)
    cap = skinned_world_object(f"{PROOF_TAG}_Cap_OneConnectedCrownBandBrim", cap_vertices, cap_faces, MAT_CAP)

# Fitted closed T-shirt cage. Stripe treatment is deferred to shader only.
def shirt_mapper(u, v, w):
    if IS_PROOF11:
        return fitted_oval_mapper(
            1.70,
            2.47,
            -0.002,
            (0.395, 0.188),
            (0.440, 0.205),
        )(u, v, w)
    t = (w + 1.0) * 0.5
    radius_x = 0.42 - 0.035 * abs(2.0 * t - 1.0)
    radius_y = 0.205 - 0.018 * abs(2.0 * t - 1.0)
    return Vector((radius_x * u, -0.035 + radius_y * v, 1.72 + 0.76 * t))


if IS_PROOF11:
    shirt = donor_component_object(
        f"{PROOF_TAG}_Shirt_DonorFitted_C157",
        {157},
        MAT_SHIRT,
    )

    def reshape_shirt(point):
        # Keep the authored torso fit, only soften the exaggerated front depth
        # and shorten the flared lower edge to sit under the jacket.
        point.y = -0.010 + (point.y + 0.010) * 0.82
        if point.z < 1.69:
            point.z = 1.69 + (point.z - 1.59) * 0.25
        if IS_PROOF13:
            # c157 is retained as the shirt surface, but its deep donor chest
            # projection is brought back to the actual torso envelope so it
            # cannot form the Proof12 central bulb or a black V-cavity.
            point.x *= 0.90
            point.y = -0.004 + (point.y + 0.004) * 0.58
            if point.z < 1.76:
                hem_t = max(0.0, min(1.0, (point.z - 1.69) / 0.07))
                point.x *= 0.94 + 0.06 * hem_t
        elif IS_PROOF12:
            hem_t = max(0.0, min(1.0, (point.z - 1.69) / 0.34))
            point.x *= 0.82 + 0.10 * hem_t
            point.y = -0.005 + (point.y + 0.005) * 0.90
        else:
            point.x *= 0.94
        return point

    reshape_world(shirt, reshape_shirt)
    if IS_PROOF12 or IS_PROOF13:
        fill_boundary_holes(shirt)
else:
    shirt_vertices, shirt_faces = quad_boundary_cage(8, 6, 10, shirt_mapper)
    shirt = skinned_world_object("PlayerProof10_Shirt_OneContinuousFittedSurface", shirt_vertices, shirt_faces, MAT_SHIRT)

if IS_PROOF14:
    # Build the inner tee from the c157 fit region, but carry its shoulder into
    # a small crew-neck volume so no broad flat boat-neck cap is visible.
    bpy.data.objects.remove(shirt, do_unlink=True)
    shirt = voxel_union(
        f"{PROOF_TAG}_Shirt_C157FitCrewNeckRetopo",
        [
            quad_boundary_cage(
                10,
                8,
                16,
                fitted_oval_mapper(
                    1.535,
                    2.405,
                    -0.006,
                    (0.345, 0.145),
                    (0.375, 0.158),
                ),
            ),
            quad_boundary_cage(
                8,
                8,
                6,
                fitted_oval_mapper(
                    2.385,
                    2.525,
                    -0.006,
                    (0.170, 0.125),
                    (0.138, 0.103),
                ),
            ),
        ],
        0.014,
        MAT_SHIRT,
        smooth_steps=3,
        smooth_factor=0.16,
    )
    # Keep the inner shirt a single continuous garment.  Alternating gray
    # material faces make the horizontal shirt bands legible in the silhouette
    # gate without introducing even a fraction of floating stripe geometry.
    shirt.data.materials.append(MAT_SHIRT_STRIPE)
    for polygon in shirt.data.polygons:
        world_z = (shirt.matrix_world @ polygon.center).z
        if world_z < 2.40:
            band = int((world_z - 1.535) / 0.115)
            polygon.material_index = 1 if band % 2 == 0 else 0
elif IS_PROOF13:
    # c157 establishes the donor fit and weight region, but its large V-hole
    # and faceted chest patch cannot survive the silhouette gate. Replace only
    # that garment with a clean fitted retopo; face, hair, hands and rig remain
    # untouched donor geometry.
    bpy.data.objects.remove(shirt, do_unlink=True)
    shirt_vertices, shirt_faces = quad_boundary_cage(
        10,
        8,
        14,
        fitted_oval_mapper(
            1.70,
            2.455,
            -0.006,
            (0.382, 0.172),
            (0.412, 0.186),
        ),
    )
    shirt = skinned_world_object(
        f"{PROOF_TAG}_Shirt_C157DonorFitCleanRetopo",
        shirt_vertices,
        shirt_faces,
        MAT_SHIRT,
    )

# Hoodie construction cages are deleted after one voxel union. The visible
# result is one continuous closed surface with bridged hood, torso, sleeves and
# integrated cuff radius profiles; the front center stays open for the shirt.
hoodie_parts = []
if not IS_PROOF11:
    hoodie_parts.append(quad_boundary_cage(8, 4, 10, block_mapper((0.0, 0.115, 2.10), (0.54, 0.17, 0.44))))
    hoodie_parts.append(quad_boundary_cage(5, 5, 10, block_mapper((-0.36, -0.095, 2.10), (0.19, 0.22, 0.43))))
    hoodie_parts.append(quad_boundary_cage(5, 5, 10, block_mapper((0.36, -0.095, 2.10), (0.19, 0.22, 0.43))))
    hoodie_parts.append(quad_boundary_cage(8, 5, 4, block_mapper((0.0, -0.015, 2.43), (0.51, 0.20, 0.13))))
    hoodie_parts.append(quad_boundary_cage(6, 6, 12, oriented_tube_mapper((-0.42, -0.01, 2.36), (-1.11, -0.01, 1.73), 0.255, 0.145, cuff=True)))
    hoodie_parts.append(quad_boundary_cage(6, 6, 12, oriented_tube_mapper((0.42, -0.01, 2.36), (1.11, -0.01, 1.73), 0.255, 0.145, cuff=True)))

def hood_mapper(u, v, w):
    if IS_PROOF11:
        disk_x, disk_y = square_to_disk(u, v)
        t = (w + 1.0) * 0.5
        shoulder_ease = math.sin(math.pi * t)
        return Vector(
            (
                0.445 * disk_x * (0.91 + 0.09 * shoulder_ease),
                0.145 + 0.205 * disk_y + 0.035 * (1.0 - w * w),
                2.45 + 0.43 * t + 0.030 * (1.0 - disk_x * disk_x),
            )
        )
    return Vector((0.49 * u, 0.205 + 0.235 * v + 0.045 * (1.0 - w * w), 2.61 + 0.27 * w + 0.045 * (1.0 - u * u)))


if IS_PROOF11:
    if IS_PROOF14:
        # Proof14 carries the hoodie through the trouser waist and replaces
        # rigid tubes/rings with curved tapered sleeves plus a welded U collar
        # and compact rear hood.  The front stays genuinely open for the tee.
        hoodie_specs = [
            proof13_open_hoodie_shell(
                ntheta=42,
                nz=24,
                z_bottom=1.535,
                z_top=2.420,
                center_y=0.002,
                front_gap=0.360,
                thickness=0.043,
            ),
            quad_boundary_cage(8, 8, 20, proof14_curved_sleeve_mapper(-1.0)),
            quad_boundary_cage(8, 8, 20, proof14_curved_sleeve_mapper(1.0)),
            proof14_collar_shell(),
        ]
        hoodie = voxel_union(
            f"{PROOF_TAG}_Hoodie_OneWaistLengthOpenZipOrganicSurface",
            hoodie_specs,
            0.012,
            MAT_HOODIE,
            smooth_steps=4,
            smooth_factor=0.15,
        )
        hood_bridge = None
        hoodie_gate_stats = topology_stats(hoodie)
        if hoodie_gate_stats["connectedComponents"] != 1:
            raise RuntimeError(
                f"Proof14 hoodie is not one continuous fitted surface: {hoodie_gate_stats}"
            )
    elif IS_PROOF13:
        # Proof13 discards the rejected donor lapels, bell sleeves and armor
        # bridges.  A smooth local retopo follows the c146/c157 torso fit,
        # while tapered sleeves begin *inside* that shell.  Voxel welding then
        # leaves one continuous cloth surface without a visible assembly seam.
        hoodie_specs = [
            proof13_open_hoodie_shell(),
            quad_boundary_cage(
                8,
                8,
                18,
                oriented_tube_mapper(
                    (-0.390, -0.002, 2.370),
                    (-1.090, -0.012, 1.720),
                    0.185,
                    0.135,
                    cuff=False,
                ),
            ),
            quad_boundary_cage(
                8,
                8,
                18,
                oriented_tube_mapper(
                    (0.390, -0.002, 2.370),
                    (1.090, -0.012, 1.720),
                    0.185,
                    0.135,
                    cuff=False,
                ),
            ),
            quad_boundary_cage(8, 8, 8, proof13_hood_pouch_mapper),
        ]
        hoodie = voxel_union(
            f"{PROOF_TAG}_Hoodie_OneSlimDonorFitZipFrontSurface",
            hoodie_specs,
            0.014,
            MAT_HOODIE,
            smooth_steps=3,
            smooth_factor=0.16,
        )
        hood_bridge = None
        hoodie_gate_stats = topology_stats(hoodie)
        if hoodie_gate_stats["connectedComponents"] != 1:
            raise RuntimeError(
                f"Proof13 hoodie is not one continuous fitted surface: {hoodie_gate_stats}"
            )
    elif IS_PROOF12:
        # Use the authored donor sleeves as the fitted envelope, then bridge
        # them to a curved open-front torso and a small rear hood. The visible
        # result is one remeshed cloth surface; no lapel or detached collar.
        sleeve_seed = donor_component_object(
            f"QAConstruction_{PROOF_TAG}_DonorSleeves_C141_C181",
            {141, 181},
            MAT_HOODIE,
        )

        def reshape_sleeves(point):
            point.y = -0.002 + (point.y + 0.002) * 0.88
            return point

        reshape_world(sleeve_seed, reshape_sleeves)
        hoodie_specs = [
            open_elliptic_jacket_shell(
                ntheta=34,
                nz=18,
                z_bottom=1.70,
                z_top=2.49,
                center_y=0.004,
                front_gap=0.30,
                thickness=0.052,
            ),
            object_world_spec(sleeve_seed),
            hood_crescent_shell(),
            quad_boundary_cage(
                6,
                6,
                7,
                oriented_tube_mapper((-0.34, 0.0, 2.34), (-0.74, 0.0, 2.24), 0.245, 0.220),
            ),
            quad_boundary_cage(
                6,
                6,
                7,
                oriented_tube_mapper((0.34, 0.0, 2.34), (0.74, 0.0, 2.24), 0.245, 0.220),
            ),
        ]
        bpy.data.objects.remove(sleeve_seed, do_unlink=True)
        hoodie = voxel_union(
            f"{PROOF_TAG}_Hoodie_OneSoftFittedZipFrontSurface",
            hoodie_specs,
            0.018,
            MAT_HOODIE,
            smooth_steps=3,
            smooth_factor=0.18,
            solidify_open=0.030,
        )
        hood_bridge = None
        hoodie_gate_stats = topology_stats(hoodie)
        if hoodie_gate_stats["connectedComponents"] != 1:
            raise RuntimeError(
                f"Proof12 hoodie bridge did not produce one connected fitted surface: {hoodie_gate_stats}"
            )
    else:
        hoodie = donor_component_object(
            f"{PROOF_TAG}_Hoodie_DonorFitted_C146_C141_C181",
            {141, 146, 181},
            MAT_HOODIE,
        )
        # c146 carries authored coat tails below the Player jacket hem. Remove
        # only that lower region; c141/c181 donor sleeves remain intact.
        crop_world_below(hoodie, 1.67)

        def reshape_hoodie(point):
            # Body-fitting correction only. The donor sleeve silhouette and
            # bilateral A-pose endpoints are not re-aimed.
            torso_factor = max(0.0, min(1.0, 1.0 - abs(point.x) / 0.62))
            if 1.67 <= point.z <= 2.55 and abs(point.x) < 0.70:
                point.x *= 0.84 + 0.10 * (point.z - 1.67) / 0.88
                point.y = 0.005 + (point.y - 0.005) * (0.68 + 0.12 * (1.0 - torso_factor))
            return point

        reshape_world(hoodie, reshape_hoodie)
        hood_vertices, hood_faces = open_elliptic_jacket_shell(
            ntheta=26,
            nz=10,
            front_gap=0.46,
            thickness=0.042,
        )
        hood_vertices = [
            (
                point[0] * 0.86,
                0.125 + (point[1] - 0.005) * 0.80,
                2.44 + (point[2] - 1.69) * (0.45 / 0.80),
            )
            for point in hood_vertices
        ]
        hood_bridge = skinned_world_object(
            f"{PROOF_TAG}_Hood_DonorFitRetopoBridge",
            hood_vertices,
            hood_faces,
            MAT_HOODIE,
        )
else:
    hood_bridge = None
    hoodie_parts.append(quad_boundary_cage(8, 6, 8, hood_mapper))
    hoodie = voxel_union(
        f"{PROOF_TAG}_Hoodie_OneVoxelWeldedSurface",
        hoodie_parts,
        0.046,
        MAT_HOODIE,
        smooth_steps=1,
        smooth_factor=0.18,
    )

# Pants: pelvis bridge and both straight tapered legs are voxel welded into one
# connected skinned surface. Cuff shaping is integrated into each leg cage.
if not IS_PROOF11:
    pants_parts = [quad_boundary_cage(8, 6, 5, block_mapper((0.0, -0.04, 1.54), (0.49, 0.245, 0.25)))]
    pants_parts.append(quad_boundary_cage(6, 6, 14, oriented_tube_mapper((-0.30, -0.04, 1.56), (-0.30, -0.04, 0.36), 0.255, 0.185, cuff=True)))
    pants_parts.append(quad_boundary_cage(6, 6, 14, oriented_tube_mapper((0.30, -0.04, 1.56), (0.30, -0.04, 0.36), 0.255, 0.185, cuff=True)))
if IS_PROOF11:
    pants = donor_component_object(
        f"{PROOF_TAG}_Pants_DonorFitted_C063_C099_C218",
        {63, 99, 218},
        MAT_PANTS,
    )

    def reshape_pants(point):
        # Symmetric straight-leg correction derived from the retained donor
        # shells. It preserves every source loop and all original weights.
        if point.z < 1.45:
            source_z = point.z
            sign = -1.0 if point.x < 0.0 else 1.0
            source_axis = 0.350 - 0.055 * max(0.0, min(1.0, (source_z - 0.35) / 1.10))
            target_axis = 0.225
            local_x = point.x - sign * source_axis
            radial_scale = 1.12 if source_z < 1.15 else 1.04
            point.x = sign * target_axis + local_x * radial_scale
            point.y = -0.010 + (point.y + 0.010) * 0.93
            # Stretch only the lower donor shells to a real trouser cuff that
            # overlaps the low-top shoe collar; no synthetic boot shaft.
            z_t = max(0.0, min(1.0, (source_z - 0.565) / (1.45 - 0.565)))
            point.z = 0.285 + z_t * (1.45 - 0.285)
        else:
            point.x *= 0.88
            point.y = -0.005 + (point.y + 0.005) * 0.90
        return point

    reshape_world(pants, reshape_pants)
else:
    pants = voxel_union(
        f"{PROOF_TAG}_Pants_OnePelvisToAnklesSurface",
        pants_parts,
        0.043,
        MAT_PANTS,
        smooth_steps=1,
        smooth_factor=0.18,
    )

# Each sneaker is one closed connected all-quad cage. Upper/sole/toe color
# separation is deliberately deferred to material faces after topology gate.
def shoe_mapper(side):
    center_x = (0.225 if IS_PROOF11 else 0.30) * side

    def mapper(u, v, w):
        if IS_PROOF11:
            # Rounded low-top sneaker envelope. The upper/collar overlap the
            # trouser cuff, while the single cage includes toe and flat sole.
            direction = Vector((0.92 * u, 0.78 * v, 1.15 * w))
            direction.normalize()
            dx, dy, dz = direction
            width = 0.220 * (1.0 - 0.08 * max(0.0, -dy))
            length = 0.365 if dy < 0.0 else 0.285
            x = center_x + width * dx
            y = -0.075 + length * dy
            if w <= -0.999:
                z = 0.018
            else:
                z = 0.225 + 0.215 * dz
                z -= 0.080 * max(0.0, -dy) * max(0.0, dz)
                z += 0.025 * max(0.0, dy) * max(0.0, dz)
            return Vector((x, y, z))
        front = (1.0 - v) * 0.5
        half_width = 0.205 * (1.0 - 0.10 * front)
        x = center_x + half_width * u
        y = -0.055 + 0.31 * v
        z = 0.145 + 0.145 * w + 0.035 * front * (1.0 - w * w)
        return Vector((x, y, max(0.0, z)))

    return mapper


if IS_PROOF11:
    left_shoe = donor_component_object(
        f"{PROOF_TAG}_Sneaker_L_DonorFitted_C000_C221",
        {0, 221},
        MAT_SHOE,
    )
    right_shoe = donor_component_object(
        f"{PROOF_TAG}_Sneaker_R_DonorFitted_C001_C214",
        {1, 214},
        MAT_SHOE,
    )

    def reshape_shoe(point):
        sign = -1.0 if point.x < 0.0 else 1.0
        source_axis = 0.350
        target_axis = 0.225
        point.x = sign * target_axis + (point.x - sign * source_axis) * 1.24
        point.y = -0.055 + (point.y + 0.055) * 1.16
        # Low-top cuff meets the reshaped trouser ankle without a visual gap.
        if point.z > 0.16:
            point.z = 0.16 + (point.z - 0.16) * 1.95
        return point

    reshape_world(left_shoe, reshape_shoe)
    reshape_world(right_shoe, reshape_shoe)
else:
    left_shoe_vertices, left_shoe_faces = quad_boundary_cage(6, 10, 5, shoe_mapper(-1.0))
    right_shoe_vertices, right_shoe_faces = quad_boundary_cage(6, 10, 5, shoe_mapper(1.0))
    left_shoe = skinned_world_object(f"{PROOF_TAG}_Sneaker_L_OneConnectedSurface", left_shoe_vertices, left_shoe_faces, MAT_SHOE)
    right_shoe = skinned_world_object(f"{PROOF_TAG}_Sneaker_R_OneConnectedSurface", right_shoe_vertices, right_shoe_faces, MAT_SHOE)

# Surface-attached mouth: c346 was excluded above. A narrow four-quad strip is
# projected onto the immutable c249 face shell and offset outward by 0.7 mm.
face_component = components[249]
face_vertex_indices = sorted(face_component["vertices"])
face_index_map = {old: new for new, old in enumerate(face_vertex_indices)}
face_world_vertices = [source.matrix_world @ source.data.vertices[index].co for index in face_vertex_indices]
face_polygons = [tuple(face_index_map[index] for index in source.data.polygons[p].vertices) for p in face_component["polygons"]]
face_bvh = BVHTree.FromPolygons(face_world_vertices, face_polygons, all_triangles=False)
mouth_world_vertices = []
for row in (-1.0, 1.0):
    for column in range(5):
        t = -1.0 + 0.5 * column
        target = Vector((0.064 * t, -0.40, 2.675 + 0.025 * abs(t) ** 1.7 + 0.0032 * row))
        location, normal, _, _ = face_bvh.find_nearest(target)
        if location is None:
            raise RuntimeError("Mouth projection failed")
        outward = normal if normal.y < 0.0 else -normal
        mouth_world_vertices.append(tuple(location + outward.normalized() * 0.0007))
mouth_faces = []
for column in range(4):
    mouth_faces.append((column, column + 1, 5 + column + 1, 5 + column))
mouth = skinned_world_object("PlayerProof10_SurfaceAttachedMouth", mouth_world_vertices, mouth_faces, MAT_MOUTH)

# Topology gates. Cap/shirt/shoes must be all-quad closed cages. Voxel-unioned
# hoodie and pants must be one closed manifold; their voxel output is expected
# to be quad dominant and is rejected if any non-quad face appears.
garments = [cap, shirt, hoodie, pants, left_shoe, right_shoe]
if hood_bridge is not None:
    garments.append(hood_bridge)
stats = {obj.name: topology_stats(obj) for obj in garments}
if IS_PROOF11:
    cap_item = stats[cap.name]
    if cap_item["connectedComponents"] != 1:
        raise RuntimeError(f"{cap.name} is not one connected component: {cap_item}")
    if cap_item["boundaryEdges"] != 0 or cap_item["nonManifoldEdges"] != 0:
        raise RuntimeError(f"{cap.name} is not closed manifold: {cap_item}")
    if cap_item["nonQuadPolygons"] != 0:
        raise RuntimeError(f"{cap.name} is not all-quad: {cap_item}")
else:
    for obj in garments:
        item = stats[obj.name]
        if item["connectedComponents"] != 1:
            raise RuntimeError(f"{obj.name} is not one connected component: {item}")
        if item["boundaryEdges"] != 0 or item["nonManifoldEdges"] != 0:
            raise RuntimeError(f"{obj.name} is not closed manifold: {item}")
        if item["nonQuadPolygons"] != 0:
            raise RuntimeError(f"{obj.name} is not all-quad: {item}")

# Cap/scalp overlap evidence: minimum surface distance must be below 20 mm;
# crown bottom is intentionally embedded in the retained donor scalp.
scalp_component = components[281]
scalp_indices = sorted(scalp_component["vertices"])
scalp_map = {old: new for new, old in enumerate(scalp_indices)}
scalp_world_vertices = [source.matrix_world @ source.data.vertices[index].co for index in scalp_indices]
scalp_polygons = [tuple(scalp_map[index] for index in source.data.polygons[p].vertices) for p in scalp_component["polygons"]]
scalp_bvh = BVHTree.FromPolygons(scalp_world_vertices, scalp_polygons, all_triangles=False)
cap_surface_distances = []
for vertex in cap.data.vertices:
    location, _, _, distance = scalp_bvh.find_nearest(cap.matrix_world @ vertex.co)
    if location is not None:
        cap_surface_distances.append(distance)
cap_scalp_min_distance = min(cap_surface_distances)
cap_scalp_gate = 0.035 if IS_PROOF14 else (0.030 if IS_PROOF13 else 0.020)
if cap_scalp_min_distance > cap_scalp_gate:
    raise RuntimeError(f"Cap/scalp visual gap is too large: {cap_scalp_min_distance}")

# Review-only scene.
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
scene.world = bpy.data.worlds.new("PlayerProof10_GrayGateWorld")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.055, 0.065, 0.078, 1.0)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.50
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.10
except TypeError:
    pass


def add_area(name, location, energy, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = (1.0, 1.0, 1.0)
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0.0, 0.0, 2.0)) - obj.location).to_track_quat("-Z", "Y").to_euler()


add_area("Gate_Key", (-4.2, -5.5, 6.4), 1000.0, 4.8)
add_area("Gate_Fill", (4.5, -3.6, 4.6), 700.0, 4.2)
add_area("Gate_Rim", (0.0, 4.5, 5.6), 850.0, 4.0)

bpy.ops.mesh.primitive_plane_add(size=24.0, location=(0.0, 0.0, -0.018))
ground = bpy.context.object
ground.name = "PlayerProof10_ReviewGround_NotCharacter"
ground.data.materials.append(gray_material("Gate_Ground", 0.08, 0.80))

camera_data = bpy.data.cameras.new("PlayerProof10_GateCamera")
camera = bpy.data.objects.new("PlayerProof10_GateCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.data.type = "ORTHO"
scene.camera = camera


def point_camera(location, target, scale):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = scale


# Temporary real-topology wire overlays. They are removed before saving.
wire_material = gray_material("Gate_TopologyWire", 0.015, 0.82)
visible_meshes = [head, hands, mouth, cap, shirt, hoodie, pants, left_shoe, right_shoe]
if hair is not None:
    visible_meshes.append(hair)
if nape is not None:
    visible_meshes.append(nape)
if hood_bridge is not None:
    visible_meshes.append(hood_bridge)
wire_sources = [cap, shirt, hoodie, pants, left_shoe, right_shoe] if IS_PROOF11 else visible_meshes
if IS_PROOF11 and hood_bridge is not None:
    wire_sources.append(hood_bridge)
wire_objects = []
for original in wire_sources:
    wire = original.copy()
    wire.data = original.data.copy()
    wire.name = f"QA_Wire_{original.name}"
    bpy.context.collection.objects.link(wire)
    wire.data.materials.clear()
    wire.data.materials.append(wire_material)
    for polygon in wire.data.polygons:
        polygon.material_index = 0
    modifier = wire.modifiers.new("QA_ActualTopologyWire", "WIREFRAME")
    modifier.thickness = 0.0028
    modifier.use_replace = True
    modifier.use_even_offset = True
    wire.hide_render = IS_PROOF11
    wire_objects.append(wire)

proof_views = {
    "front": ((0.0, -8.4, 2.12), (0.0, 0.0, 2.05), 4.72),
    "three-quarter": ((5.9, -5.9, 2.15), (0.0, 0.0, 2.05), 4.72),
    "side": ((8.4, 0.0, 2.12), (0.0, 0.0, 2.05), 4.72),
    "back": ((0.0, 8.4, 2.12), (0.0, 0.0, 2.05), 4.72),
    "cap-seam-closeup": ((2.8, -4.2, 3.82), (0.0, 0.0, 3.72), 1.35),
    "shoulder-seam-closeup": ((3.4, -4.8, 2.62), (0.35, 0.0, 2.35), 1.55),
    "waist-seam-closeup": ((2.8, -4.8, 1.72), (0.0, -0.02, 1.50), 1.50),
    "shoe-seam-closeup": ((2.6, -4.5, 0.48), (0.0, -0.05, 0.20), 1.28),
}
proof_paths = []
for name, (location, target, scale) in proof_views.items():
    closeup = name.endswith("closeup")
    if IS_PROOF11:
        for wire in wire_objects:
            wire.hide_render = not closeup
        rear_review = name == "back"
        # Keep the donor head shell as the natural rear occluder. Eye and face
        # materials are backface-culled, while the front-only mouth strip is
        # hidden, so no rear eye/mouth card can leak through the skull.
        head.hide_render = False
        mouth.hide_render = rear_review
    point_camera(location, target, scale)
    mode = "gray-wire" if (not IS_PROOF11 or closeup) else "gray-solid"
    path = OUTPUT / f"{FILE_TAG}-{name}-{mode}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    proof_paths.append(path)

head.hide_render = False
mouth.hide_render = False

for wire in wire_objects:
    bpy.data.objects.remove(wire, do_unlink=True)
bpy.data.materials.remove(wire_material)

blend_path = OUTPUT / f"{FILE_TAG}-{'silhouette' if IS_PROOF11 else 'topology'}-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

receipt = {
    "schema": f"family-company.player-proof{PROOF_NUMBER}-{'silhouette' if IS_PROOF11 else 'topology'}-gate.v1",
    "status": "SILHOUETTE_GATE_ONLY_AWAITING_ROOT_REVIEW" if IS_PROOF11 else "DIAGNOSTIC_ONLY_STYLE_FAIL",
    "source": {
        "fbx": str(SOURCE_FBX.relative_to(REPO)).replace("\\", "/"),
        "sha256": sha256(SOURCE_FBX),
        "test3Sakurako": "EXCLUDED_NOT_OPENED",
    },
    "immutableDonor": {
        "rigBones": len(armature.data.bones),
        "handVertexCount": len(hands.data.vertices),
        "handCoordinateHashBefore": hand_hash_before,
        "handCoordinateHashAfter": hand_hash_after,
        "handGeometryPoseUnchanged": hand_hash_before == hand_hash_after,
        "handRetentionStatement": "original 3-digit stylized hand retained",
        "opaqueMouthPlateC346Retained": False,
        "eyeBackfaceCulling": True,
        "originalAPoseRetained": True,
    },
    "visibleTopology": stats,
    "constructionPolicy": {
        "visiblePrimitiveStacking": 0,
        "floatingTrimGeometry": 0,
        "zipperPocketRibbonStripeGeometry": 0,
        "constructionCagesRemovedAfterVoxelUnion": True,
        "weightTransfer": "nearest Yuuka donor rest-space vertex groups; same 118-bone armature modifier",
        "capScalpMinimumSurfaceDistance": cap_scalp_min_distance,
        "stripeAndZipperDeferredToShaderOnly": True,
        "shoeColorSplitDeferredToMaterialFacesOnly": True,
        "colorFinalGifGenerated": False,
    },
    "proofs": [str(path.relative_to(REPO)).replace("\\", "/") for path in proof_paths],
    "blend": str(blend_path.relative_to(REPO)).replace("\\", "/"),
}
if IS_PROOF11:
    receipt["constructionPolicy"].update(
        {
            "silhouettePriority": "natural fitted donor silhouette takes precedence over synthetic one-component metrics",
            "donorFittedSurfaces": {
                "hoodie": ["c146", "c141", "c181"],
                "shirt": ["c157"],
                "pants": ["c063", "c099", "c218"],
                "leftShoe": ["c000", "c221"],
                "rightShoe": ["c001", "c214"],
            },
            "cap": "new one-connected smooth all-quad newsboy crown/band/visor cage",
            "proceduralCuboidGarments": 0,
            "constructionCagesRemovedAfterVoxelUnion": "NOT_APPLICABLE_DONOR_SURFACES_USED",
            "weightTransfer": "donor garment loops retain original Yuuka weights; only the cap and small fitted hood bridge use nearest-rest-space transfer to the same 118-bone rig",
            "rearEyeOcclusion": "donor head shell remains as the rear occluder; eye/face materials are backface-culled and the front-only mouth strip is hidden for rear QA; no synthetic sphere",
        }
    )
    if IS_PROOF12:
        receipt["constructionPolicy"].update(
            {
                "silhouettePriority": "soft fitted 2D-authority silhouette; topology derives from donor sleeves/body envelope plus local bridge-retopo",
                "donorFittedSurfaces": {
                    "hoodieSleeves": ["c141", "c181"],
                    "shirt": ["c157", "boundary holes connected/filled"],
                    "pants": ["c063", "c099", "c218"],
                    "leftShoe": ["c000", "c221"],
                    "rightShoe": ["c001", "c214"],
                },
                "cap": "one visible voxel-welded all-quad surface from a smooth newsboy crown/band cage and projecting visor cage; construction cages removed",
                "constructionCagesRemovedAfterVoxelUnion": True,
                "weightTransfer": "pants/shoes/shirt retain donor weights; the single remeshed hoodie and cap receive nearest-rest-space weights from the same Yuuka 118-bone rig",
                "hoodie": "one connected soft open-zip fitted surface with donor sleeves, curved torso bridge, integrated shoulder bridges and small rear hood",
                "hairCapIntersectionPolicy": "cap-covered donor hair roots clamped below crown/visor envelope; target penetration 0",
            }
        )
    if IS_PROOF13:
        receipt["constructionPolicy"].update(
            {
                "silhouettePriority": "compact 2D-authority cap and slim soft zip hoodie; fail closed before color or animation",
                "donorFittedSurfaces": {
                    "hoodieFitEnvelope": ["c146", "c157", "sharp lapels/tails discarded during local retopo"],
                    "shirt": ["c157 donor fit/weight region", "clean closed fitted retopo replaces the V-hole and faceted chest patch"],
                    "pants": ["c063", "c099", "c218"],
                    "leftShoe": ["c000", "c221"],
                    "rightShoe": ["c001", "c214"],
                },
                "cap": "one direct all-quad manifold surface; compact skull-fit crown and forward visor share the same topology with no intermediate band",
                "constructionCagesRemovedAfterVoxelUnion": True,
                "weightTransfer": "pants/shoes retain donor weights; the c157-fit shirt, continuous hoodie retopo and direct cap receive nearest-rest-space weights from the same Yuuka 118-bone rig",
                "hoodie": "one continuous slim open-front fitted surface; tapered sleeves overlap inside the torso shell before welding; small hood rests only behind the neck",
                "hairCapIntersectionPolicy": "cap-covered donor hair roots cropped to z<=3.355 beneath the broad crown underside; visible penetration target 0",
                "capCoveredHairMaximumZ": 3.355,
                "capUndersideMinimumZ": 3.365,
                "capHairVerticalClearanceMinimum": 0.010,
            }
        )
    if IS_PROOF14:
        receipt["immutableDonor"].update(
            {
                "excludedDonorCuffAccessoryComponents": ["c068", "c069", "c104", "c105", "c216", "c223"],
                "handAudit": "c060+c061+c062 and mirrored c096+c097+c098 are the unchanged native three-digit skin/hand surfaces; oversized donor cuff accessory islands are not hand geometry",
                "underCapCraniumPolicy": "only render-hidden cranium vertices above z=3.320 removed from the visible copy; facial landmarks, eyes, brows, ears and lower head unchanged",
            }
        )
        receipt["constructionPolicy"].update(
            {
                "silhouettePriority": "rounded scalp-hugging panel cap and waist-length soft zip hoodie; fail closed before color or animation",
                "donorFittedSurfaces": {
                    "hoodieFitEnvelope": ["c146", "c157", "waist-length local retopo"],
                    "shirt": ["c157 donor fit/weight region", "crew-neck clean retopo"],
                    "pants": ["c063", "c099", "c218"],
                    "leftShoe": ["c000", "c221"],
                    "rightShoe": ["c001", "c214"],
                },
                "cap": "one voxel-welded all-quad manifold from a compact shallow asymmetric newsboy lens and short curved visor; no intermediate band or visible overlap seam",
                "constructionCagesRemovedAfterVoxelUnion": True,
                "weightTransfer": "pants/shoes retain donor weights; crew-neck shirt, continuous hoodie and cap receive nearest-rest-space weights from the same Yuuka 118-bone rig",
                "hoodie": "one continuous waist-overlapping open-front surface with curved organic sleeve taper, subtle integrated cuff radius and a rear-raised U hood/collar fused into the back neckline",
                "hairCapIntersectionPolicy": "cap-covered donor hair roots follow a curved z=3.245..3.285 limit; the closed crown underside extends to z=3.120 inside the hair envelope so the lower crown overlaps hair without a horizontal air gap",
                "capCoveredHairMaximumZ": 3.285,
                "capEmbeddedCrownRangeZ": [3.120, 3.273],
                "capVisorGeometry": "same welded manifold; grayscale distinction uses material faces only",
                "stripeAndZipperDeferredToShaderOnly": False,
                "shirtStripeGeometry": 0,
                "shirtStripeTreatment": "alternating grayscale material faces on the continuous shirt surface; no stripe geometry",
            }
        )
receipt_path = OUTPUT / f"{FILE_TAG}-{'silhouette' if IS_PROOF11 else 'topology'}-gate-receipt.json"
receipt_path.write_text(json.dumps(receipt, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(json.dumps(receipt, indent=2, ensure_ascii=False))
