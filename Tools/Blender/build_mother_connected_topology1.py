"""MotherConnectedTopology1 grayscale silhouette/topology gate.

Authority and scope:
- user-owned Mika CH0069 FBX is the only 3D donor;
- the Mother neutral illustration and pixel walk sheet are silhouette authority;
- original Mika face/eyes and original three-digit SD hands remain undeformed;
- the complete 151-bone rig and donor weights are retained as the skinning source;
- final visible cardigan, blouse, skirt, each loafer, and hair are each one
  connected closed manifold surface;
- no color/final-style, face-age, animation, or GIF claim is made here.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from collections import defaultdict, deque
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output",
        default="Artifacts/Family3DOwnedBaseConversion/MotherConnectedTopology1",
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
    / "Mika"
    / "CH0069_Mesh"
)
SOURCE_FBX = SOURCE_DIR / "CH0069_Mesh.fbx"
AUTHORITY_NEUTRAL = REPO / "Assets" / "Art" / "Characters" / "Mother" / "mother_office_neutral_v1.png"
AUTHORITY_PIXEL = (
    REPO
    / "Assets"
    / "Art"
    / "Characters"
    / "Mother"
    / "Pixel"
    / "HighMotion"
    / "mother_pixel_walk8dir6_a_v1.png"
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def coordinate_hash(points) -> str:
    normalized = sorted(tuple(round(float(value), 7) for value in point) for point in points)
    payload = json.dumps(normalized, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(payload).hexdigest().upper()


def gray_material(name: str, value: float, roughness: float = 0.72):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = (value, value, value, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    if "IOR Level" in shader.inputs:
        shader.inputs["IOR Level"].default_value = 0.16
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (value, value, value, 1.0)
    return material


def replace_all_materials(obj, material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = True


def recalc_normals(obj) -> None:
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def delete_vertices(obj, indices) -> None:
    edit = bmesh.new()
    edit.from_mesh(obj.data)
    edit.verts.ensure_lookup_table()
    targets = [edit.verts[index] for index in sorted(indices) if index < len(edit.verts)]
    bmesh.ops.delete(edit, geom=targets, context="VERTS")
    edit.to_mesh(obj.data)
    edit.free()
    obj.data.update()


def material_components(mesh, material_index):
    polygons = [polygon for polygon in mesh.polygons if polygon.material_index == material_index]
    polygon_map = {polygon.index: polygon for polygon in polygons}
    by_vertex = defaultdict(list)
    for polygon in polygons:
        for vertex_index in polygon.vertices:
            by_vertex[vertex_index].append(polygon.index)
    remaining = set(polygon_map)
    components = []
    while remaining:
        seed = remaining.pop()
        queue = deque([seed])
        component = {seed}
        while queue:
            polygon = polygon_map[queue.popleft()]
            for vertex_index in polygon.vertices:
                for neighbor in by_vertex[vertex_index]:
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        component.add(neighbor)
                        queue.append(neighbor)
        components.append(sorted(component))
    return sorted(components, key=len, reverse=True)


def component_vertices(mesh, component):
    return sorted({index for polygon_index in component for index in mesh.polygons[polygon_index].vertices})


def topology_stats(obj):
    edit = bmesh.new()
    edit.from_mesh(obj.data)
    unseen = set(edit.verts)
    components = 0
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
        "vertices": len(edit.verts),
        "edges": len(edit.edges),
        "polygons": len(edit.faces),
        "connectedComponents": components,
        "boundaryEdges": sum(1 for edge in edit.edges if len(edge.link_faces) == 1),
        "nonManifoldEdges": sum(1 for edge in edit.edges if len(edge.link_faces) != 2),
        "nonQuadPolygons": sum(1 for face in edit.faces if len(face.verts) != 4),
    }
    edit.free()
    return result


def mesh_world_bounds(obj):
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    lo = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    hi = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return {
        "min": [round(float(value), 6) for value in lo],
        "max": [round(float(value), 6) for value in hi],
        "dimensions": [round(float(value), 6) for value in (hi - lo)],
    }


def quad_boundary_cage(nx, ny, nz, mapper):
    """Closed all-quad boundary of a subdivided deformation cage."""
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


def ellipsoid_mapper(center, radii):
    center = Vector(center)
    radii = Vector(radii)

    def mapper(u, v, w):
        direction = Vector((u, v, w))
        direction.normalize()
        return center + Vector((radii.x * direction.x, radii.y * direction.y, radii.z * direction.z))

    return mapper


def oriented_tube_mapper(start, end, radius_start, radius_end, depth_scale=0.82, cuff=False):
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
        if cuff and t > 0.76:
            cuff_t = min(1.0, (t - 0.76) / 0.24)
            radius *= 1.0 + 0.15 * math.sin(cuff_t * math.pi)
        center = start + axis_direction * axis_length * t
        return center + lateral * disk_x * radius + depth * disk_y * radius * depth_scale

    return mapper


def blouse_mapper(u, v, w):
    t = (w + 1.0) * 0.5
    disk_x, disk_y = square_to_disk(u, v)
    if t < 0.72:
        q = t / 0.72
        ease = q * q * (3.0 - 2.0 * q)
        radius_x = 0.218 * (1.0 - ease) + 0.282 * ease
        radius_y = 0.137 * (1.0 - ease) + 0.166 * ease
    else:
        q = (t - 0.72) / 0.28
        ease = q * q * (3.0 - 2.0 * q)
        radius_x = 0.282 * (1.0 - ease) + 0.114 * ease
        radius_y = 0.166 * (1.0 - ease) + 0.088 * ease
    bust = 0.004 * math.sin(math.pi * min(1.0, t / 0.78)) * max(0.0, -disk_y)
    return Vector((radius_x * disk_x, 0.004 + radius_y * disk_y - bust, 0.615 + 0.595 * t))


def open_cardigan_shell(ntheta=48, nz=22, thickness=0.013):
    """Closed C-section torso cloth with a genuine open center front."""
    vertices = []
    faces = []
    def radii(t):
        shoulder = t ** 1.55
        waist_softness = 0.018 * math.sin(math.pi * t)
        return (
            0.255 + 0.080 * shoulder - waist_softness,
            0.192 + 0.014 * shoulder - 0.005 * math.sin(math.pi * t),
        )

    row = ntheta + 1
    layer_size = row * (nz + 1)

    def vid(layer, k, i):
        return layer * layer_size + k * row + i

    for layer in range(2):
        for k in range(nz + 1):
            t = k / nz
            radius_x, radius_y = radii(t)
            if layer == 1:
                radius_x -= thickness
                radius_y -= thickness
            front_gap = 0.31 + 0.24 * (t ** 2.1)
            theta_start = -math.pi + front_gap
            theta_end = math.pi - front_gap
            for i in range(ntheta + 1):
                theta = theta_start + (theta_end - theta_start) * i / ntheta
                shoulder_curve = 0.050 * (t ** 5.0) * abs(math.sin(theta))
                front_hem_lift = 0.014 * ((1.0 - t) ** 5.0) * max(0.0, -math.cos(theta))
                z = 0.605 + (1.135 - 0.605) * t + shoulder_curve + front_hem_lift
                vertices.append(
                    (
                        radius_x * math.sin(theta),
                        -0.008 + radius_y * math.cos(theta),
                        z,
                    )
                )
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


def midi_skirt_mesh(segments=56, rings=18, thickness=0.014):
    """One continuous closed waist-to-hem A-line cloth shell."""
    vertices = []
    faces = []
    ring_size = segments
    layer_size = (rings + 1) * ring_size

    def vid(layer, ring, segment):
        return layer * layer_size + ring * ring_size + (segment % segments)

    for layer in range(2):
        for ring in range(rings + 1):
            t = ring / rings
            ease = 0.34 * t + 0.66 * (t ** 1.65)
            radius_x = 0.228 + 0.108 * ease + 0.010 * math.sin(math.pi * t)
            radius_y = 0.148 + 0.073 * ease + 0.006 * math.sin(math.pi * t)
            if layer == 1:
                radius_x -= thickness
                radius_y -= thickness
            z_base = 0.710 + (0.205 - 0.710) * t
            for segment in range(segments):
                theta = 2.0 * math.pi * segment / segments
                restrained_fold = 1.0 + 0.018 * (t ** 1.7) * math.cos(4.0 * theta)
                z = z_base + 0.006 * (t ** 4.0) * (1.0 - math.cos(4.0 * theta))
                vertices.append(
                    (
                        radius_x * restrained_fold * math.sin(theta),
                        0.008 + radius_y * restrained_fold * math.cos(theta),
                        z,
                    )
                )
    for ring in range(rings):
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            faces.append((vid(0, ring, segment), vid(0, ring, next_segment), vid(0, ring + 1, next_segment), vid(0, ring + 1, segment)))
            faces.append((vid(1, ring, segment), vid(1, ring + 1, segment), vid(1, ring + 1, next_segment), vid(1, ring, next_segment)))
    for segment in range(segments):
        next_segment = (segment + 1) % segments
        faces.append((vid(0, 0, segment), vid(1, 0, segment), vid(1, 0, next_segment), vid(0, 0, next_segment)))
        faces.append((vid(0, rings, segment), vid(0, rings, next_segment), vid(1, rings, next_segment), vid(1, rings, segment)))
    return vertices, faces


def loafer_mapper(side):
    center_x = 0.158 * side

    def mapper(u, v, w):
        disk_x, disk_y = square_to_disk(u, v)
        front = max(0.0, -disk_y)
        width = 0.086 * (1.0 - 0.07 * front)
        length = 0.148 if disk_y < 0.0 else 0.096
        x = center_x + width * disk_x
        y = -0.040 + length * disk_y
        edge = min(1.0, disk_x * disk_x + disk_y * disk_y)
        half_height = 0.041 + 0.009 * (1.0 - edge)
        z = -0.056 + half_height * w + 0.007 * front * (1.0 - w * w)
        return Vector((x, y, max(-0.102, z)))

    return mapper


def coherent_half_up_hair_mapper(u, v, w):
    """Single smooth closed mass with a rear gather and shoulder-length hem."""
    direction = Vector((u, v, w))
    direction.normalize()
    dx, dy, dz = direction
    lower = max(0.0, -dz)
    x = 0.300 * dx * (1.0 - 0.10 * lower * lower)
    y = 0.058 + 0.218 * dy
    z = 1.505 + 0.300 * dz
    # Extend only the lower hemisphere to the shoulders; a very restrained
    # five-lobe hem prevents a round ball without becoming separate clumps.
    theta = math.atan2(dy, dx)
    z -= 0.205 * (lower ** 1.45) * (0.84 + 0.16 * (1.0 - abs(dx)))
    z -= 0.018 * (lower ** 2.0) * (0.5 + 0.5 * math.cos(5.0 * theta))
    # Recess lower front hair so the immutable face and eyes stay readable.
    if dy < 0.0:
        y += 0.042 * (lower ** 0.8) * max(0.0, 1.0 - abs(dx))
    # The half-up gather is a deformation of the same back surface—not a bun.
    gather = (max(0.0, dy) ** 3.0) * math.exp(-((dz - 0.02) / 0.42) ** 2) * math.exp(-((dx / 0.58) ** 2))
    y += 0.095 * gather
    z += 0.025 * gather
    return Vector((x, y, z))


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
source = bpy.data.objects.get("CH0069_Body")
if armature is None or source is None:
    raise RuntimeError("Owned Mika donor import failed")
if len(armature.data.bones) != 151:
    raise RuntimeError(f"Expected Mika 151-bone rig, got {len(armature.data.bones)}")

armature.name = "MotherConnectedTopology1_Rig_Mika151"
armature.scale = tuple(component * 180.0 for component in armature.scale)
source.name = "MotherConnectedTopology1_WeightSource_MikaHidden"
weapon = bpy.data.objects.get("CH0069_Weapon")
if weapon:
    bpy.data.objects.remove(weapon, do_unlink=True)
bpy.context.view_layer.update()

source.hide_render = True
source.hide_set(True)
armature.show_in_front = False

source_material_names = [material.name if material else "" for material in source.data.materials]


def slot(prefix):
    return next(index for index, name in enumerate(source_material_names) if name.startswith(prefix))


hair_slot = slot("CH0069_Hair")
face_slot = slot("CH0069_Face")
brow_slot = slot("CH0069_Eyebrow")
eye_slot = slot("CH0069_EyeMouth")
body_slot = slot("CH0069_Body")

MAT_FACE = gray_material("MotherCT1_FaceGray", 0.73, 0.75)
MAT_EYE = gray_material("MotherCT1_EyeGray", 0.18, 0.62)
MAT_BROW = gray_material("MotherCT1_BrowGray", 0.13, 0.68)
MAT_HAND = gray_material("MotherCT1_HandGray", 0.70, 0.74)
MAT_BODY = gray_material("MotherCT1_BodyGray", 0.69, 0.76)
MAT_CARDIGAN = gray_material("MotherCT1_CardiganGray", 0.52, 0.82)
MAT_BLOUSE = gray_material("MotherCT1_BlouseGray", 0.82, 0.80)
MAT_SKIRT = gray_material("MotherCT1_SkirtGray", 0.39, 0.84)
MAT_LOAFER = gray_material("MotherCT1_LoaferGray", 0.27, 0.64)
MAT_HAIR = gray_material("MotherCT1_HairGray", 0.24, 0.78)

# Immutable original face/eyes/eyebrows; only the known 32-polygon opaque
# background mouth component is omitted. There is no generated mouth patch.
eye_components = material_components(source.data, eye_slot)
mouth_components = [component for component in eye_components if len(component) == 32]
if len(mouth_components) != 1:
    raise RuntimeError(f"Expected one Mika 32-polygon EyeMouth background component, got {len(mouth_components)}")
mouth_polygons = set(mouth_components[0])
head_polygons = {
    polygon.index
    for polygon in source.data.polygons
    if polygon.material_index in {face_slot, brow_slot, eye_slot} and polygon.index not in mouth_polygons
}
head_vertices = sorted({vertex for polygon_index in head_polygons for vertex in source.data.polygons[polygon_index].vertices})
head_points_before = [source.matrix_world @ source.data.vertices[index].co for index in head_vertices]
head = source.copy()
head.data = source.data.copy()
head.name = "MotherConnectedTopology1_ImmutableOriginalFaceEyes"
bpy.context.collection.objects.link(head)
head.hide_render = False
head.hide_set(False)
delete_vertices(head, set(range(len(head.data.vertices))) - set(head_vertices))
for index, material in enumerate(list(head.data.materials)):
    name = material.name if material else ""
    if "EyeMouth" in name:
        head.data.materials[index] = MAT_EYE
    elif "Eyebrow" in name:
        head.data.materials[index] = MAT_BROW
    else:
        head.data.materials[index] = MAT_FACE
for polygon in head.data.polygons:
    polygon.use_smooth = True
head_points_after = [head.matrix_world @ vertex.co for vertex in head.data.vertices]
head_hash_before = coordinate_hash(head_points_before)
head_hash_after = coordinate_hash(head_points_after)
if head_hash_before != head_hash_after:
    raise RuntimeError("Original Mika face/eye geometry changed")


def group_scores(vertex_indices):
    scores = defaultdict(float)
    for vertex_index in vertex_indices:
        for membership in source.data.vertices[vertex_index].groups:
            name = source.vertex_groups[membership.group].name.lower()
            weight = float(membership.weight)
            if " hand" in name or " finger" in name:
                scores["hand"] += weight
            if any(token in name for token in ("upperarm", "forearm", "wrist")):
                scores["arm"] += weight
            scores["total"] += weight
    return scores


hand_components = []
hand_component_receipt = []
for component_index, component in enumerate(material_components(source.data, body_slot)):
    vertices = component_vertices(source.data, component)
    points = [source.matrix_world @ source.data.vertices[index].co for index in vertices]
    lo = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    hi = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    center = (lo + hi) * 0.5
    dimensions = hi - lo
    scores = group_scores(vertices)
    total = max(scores["total"], 1.0e-9)
    hand_fraction = scores["hand"] / total
    arm_fraction = scores["arm"] / total
    wrist_ornament = (
        0.395 < abs(center.x) < 0.495
        and 0.72 < center.z < 0.91
        and dimensions.x < 0.135
        and hand_fraction + arm_fraction > 0.35
    )
    keep = hand_fraction > 0.30 and not wrist_ornament
    if keep:
        hand_components.append(component)
    if keep or wrist_ornament:
        hand_component_receipt.append(
            {
                "sourceBodyMaterialComponent": component_index,
                "polygons": len(component),
                "handWeightFraction": round(float(hand_fraction), 6),
                "armWeightFraction": round(float(arm_fraction), 6),
                "keptAsOriginalHand": keep,
                "excludedWholeWristOrnament": wrist_ornament,
                "centerWorld": [round(float(value), 6) for value in center],
            }
        )

hand_polygons = set().union(*(set(component) for component in hand_components))
# Whole-component selection deliberately excludes donor wrist ornaments.  The
# two mirrored main hand surfaces total 454 polygons and keep all three source
# digits per side without borrowing bracelet/cuff faces.
if len(hand_polygons) != 454:
    raise RuntimeError(f"Expected 454 ornament-free original Mika hand polygons, selected {len(hand_polygons)}")
hand_vertices = sorted({vertex for polygon_index in hand_polygons for vertex in source.data.polygons[polygon_index].vertices})
hand_points_before = [source.matrix_world @ source.data.vertices[index].co for index in hand_vertices]
hands = source.copy()
hands.data = source.data.copy()
hands.name = "MotherConnectedTopology1_ImmutableOriginalThreeDigitHands"
bpy.context.collection.objects.link(hands)
hands.hide_render = False
hands.hide_set(False)
delete_vertices(hands, set(range(len(hands.data.vertices))) - set(hand_vertices))
replace_all_materials(hands, MAT_HAND)
hand_points_after = [hands.matrix_world @ vertex.co for vertex in hands.data.vertices]
hand_hash_before = coordinate_hash(hand_points_before)
hand_hash_after = coordinate_hash(hand_points_after)
if hand_hash_before != hand_hash_after:
    raise RuntimeError("Original Mika hand geometry/rest pose changed")

# Hidden full donor is the only weight source. New surfaces copy its complete
# 141-group vocabulary and nearest rest-space weights, then use the same rig.
source_kd = KDTree(len(source.data.vertices))
for vertex in source.data.vertices:
    source_kd.insert(source.matrix_world @ vertex.co, vertex.index)
source_kd.balance()


def transfer_weights_and_armature(obj):
    for group in source.vertex_groups:
        obj.vertex_groups.new(name=group.name)
    group_names = {group.index: group.name for group in source.vertex_groups}
    assignment_count = 0
    for vertex in obj.data.vertices:
        world_point = obj.matrix_world @ vertex.co
        _, source_index, _ = source_kd.find(world_point)
        for membership in source.data.vertices[source_index].groups:
            obj.vertex_groups[group_names[membership.group]].add([vertex.index], membership.weight, "REPLACE")
            assignment_count += 1
    modifier = obj.modifiers.new("MotherCT1_TransferredMikaWeights151", "ARMATURE")
    modifier.object = armature
    obj["weightAssignmentCount"] = assignment_count


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


def voxel_union(name, part_specs, voxel_size, material, smooth_steps=3, smooth_factor=0.20):
    raw_parts = []
    for index, (vertices, faces) in enumerate(part_specs):
        raw_parts.append(raw_world_object(f"QAConstruction_{name}_{index:02d}", vertices, faces))
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
    recalc_normals(joined)
    world_vertices = [tuple(joined.matrix_world @ vertex.co) for vertex in joined.data.vertices]
    faces = [tuple(polygon.vertices) for polygon in joined.data.polygons]
    bpy.data.objects.remove(joined, do_unlink=True)
    return skinned_world_object(name, world_vertices, faces, material)


# Fitted cream blouse; collar and buttons are intentionally texture-only.
blouse_vertices, blouse_faces = quad_boundary_cage(12, 10, 16, blouse_mapper)
blouse = skinned_world_object(
    "MotherCT1_Blouse_OneNearBodyClosedSurface",
    blouse_vertices,
    blouse_faces,
    MAT_BLOUSE,
)

# Open cardigan torso, both sleeves, and integrated cuff radius profiles are
# construction cages only; voxel union leaves one smooth connected garment.
cardigan_parts = [open_cardigan_shell()]
cardigan_parts.append(
    quad_boundary_cage(
        8,
        8,
        18,
        oriented_tube_mapper((-0.255, 0.002, 1.085), (-0.475, -0.014, 0.805), 0.118, 0.071, cuff=True),
    )
)
cardigan_parts.append(
    quad_boundary_cage(
        8,
        8,
        18,
        oriented_tube_mapper((0.255, 0.002, 1.085), (0.475, -0.014, 0.805), 0.118, 0.071, cuff=True),
    )
)
cardigan = voxel_union(
    "MotherCT1_Cardigan_TorsoSleevesCuffs_OneVoxelWeldedSurface",
    cardigan_parts,
    0.0085,
    MAT_CARDIGAN,
    smooth_steps=4,
    smooth_factor=0.20,
)

# One restrained, continuous below-knee A-line shell: no ruffle parts.
skirt_vertices, skirt_faces = midi_skirt_mesh()
skirt = skinned_world_object(
    "MotherCT1_MidiSkirt_OneWaistToHemClosedSurface",
    skirt_vertices,
    skirt_faces,
    MAT_SKIRT,
)

# Adult-proportion under-body. Each leg includes an overlapped foot volume so
# the corresponding closed loafer genuinely contains the foot at the ankle.
legs = []
for side, suffix in ((-1.0, "R"), (1.0, "L")):
    leg_parts = [
        quad_boundary_cage(
            8,
            8,
            18,
            oriented_tube_mapper((0.145 * side, 0.004, 0.650), (0.158 * side, -0.004, -0.045), 0.078, 0.055, depth_scale=0.88),
        ),
        quad_boundary_cage(
            8,
            10,
            7,
            ellipsoid_mapper((0.158 * side, -0.050, -0.055), (0.060, 0.102, 0.046)),
        ),
    ]
    legs.append(
        voxel_union(
            f"MotherCT1_LegFoot_{suffix}_OneContinuousBodySurface",
            leg_parts,
            0.0105,
            MAT_BODY,
            smooth_steps=3,
            smooth_factor=0.20,
        )
    )

left_loafer_vertices, left_loafer_faces = quad_boundary_cage(10, 14, 8, loafer_mapper(1.0))
right_loafer_vertices, right_loafer_faces = quad_boundary_cage(10, 14, 8, loafer_mapper(-1.0))
left_loafer = skinned_world_object(
    "MotherCT1_Loafer_L_OneConnectedFootContainingSurface",
    left_loafer_vertices,
    left_loafer_faces,
    MAT_LOAFER,
)
right_loafer = skinned_world_object(
    "MotherCT1_Loafer_R_OneConnectedFootContainingSurface",
    right_loafer_vertices,
    right_loafer_faces,
    MAT_LOAFER,
)

# Short tapered neck overlaps both the preserved face shell and blouse. This is
# a body under-surface, not a floating collar/accessory.
neck_vertices, neck_faces = quad_boundary_cage(
    8,
    8,
    8,
    lambda u, v, w: Vector(
        (
            (0.068 - 0.010 * ((w + 1.0) * 0.5)) * square_to_disk(u, v)[0],
            0.018 + (0.058 - 0.006 * ((w + 1.0) * 0.5)) * square_to_disk(u, v)[1],
            1.095 + 0.155 * ((w + 1.0) * 0.5),
        )
    ),
)
neck = skinned_world_object("MotherCT1_Neck_TaperedOverlapSurface", neck_vertices, neck_faces, MAT_BODY)

# One coherent shoulder-length half-up outer hair mass. The rear gather and
# tapered hem are deformations of this single closed cage, so no column clumps,
# side bun, bow, waist-length donor hair, or separately assembled lock survives.
hair_vertices, hair_faces = quad_boundary_cage(20, 18, 20, coherent_half_up_hair_mapper)
hair = skinned_world_object(
    "MotherCT1_Hair_ShoulderHalfUp_OneCoherentMass",
    hair_vertices,
    hair_faces,
    MAT_HAIR,
)

# Automatic closed-manifold gates. The original face/hands and under-body are
# excluded from garment claims; they remain visible for fit and identity scale.
garments = [cardigan, blouse, skirt, left_loafer, right_loafer, hair]
stats = {obj.name: topology_stats(obj) for obj in garments}
for obj in garments:
    item = stats[obj.name]
    if item["connectedComponents"] != 1:
        raise RuntimeError(f"{obj.name} is not one connected component: {item}")
    if item["boundaryEdges"] != 0 or item["nonManifoldEdges"] != 0:
        raise RuntimeError(f"{obj.name} is not closed manifold: {item}")

body_stats = {obj.name: topology_stats(obj) for obj in [neck, *legs]}

all_visible_character = [head, hands, neck, *legs, blouse, cardigan, skirt, left_loafer, right_loafer, hair]
visible_points = [obj.matrix_world @ vertex.co for obj in all_visible_character for vertex in obj.data.vertices]
visible_lo = Vector(tuple(min(point[index] for point in visible_points) for index in range(3)))
visible_hi = Vector(tuple(max(point[index] for point in visible_points) for index in range(3)))
face_world_points = [head.matrix_world @ vertex.co for vertex in head.data.vertices]
face_lo = Vector(tuple(min(point[index] for point in face_world_points) for index in range(3)))
face_hi = Vector(tuple(max(point[index] for point in face_world_points) for index in range(3)))

# Review-only grayscale scene.
scene = bpy.context.scene
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1100
scene.render.resolution_y = 1400
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new("MotherCT1_GrayGateWorld")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.040, 0.050, 0.064, 1.0)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.30
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.35
except TypeError:
    pass


def add_area(name, location, energy, size):
    data = bpy.data.lights.new(name + "Data", "AREA")
    data.energy = energy
    data.color = (1.0, 1.0, 1.0)
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0.0, 0.0, 0.92)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


lights = [
    add_area("MotherCT1_Key", (-3.2, -4.3, 3.9), 410.0, 3.3),
    add_area("MotherCT1_Fill", (3.6, -2.4, 2.8), 235.0, 3.0),
    add_area("MotherCT1_Rim", (0.0, 3.7, 3.5), 340.0, 3.0),
]

bpy.ops.mesh.primitive_plane_add(size=10.0, location=(0.0, 0.0, -0.106))
ground = bpy.context.object
ground.name = "MotherCT1_ReviewGround_NotCharacter"
ground.data.materials.append(gray_material("MotherCT1_GroundGray", 0.075, 0.86))

camera_data = bpy.data.cameras.new("MotherCT1_GateCameraData")
camera = bpy.data.objects.new("MotherCT1_GateCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.data.type = "ORTHO"
scene.camera = camera


def point_camera(location, target, scale):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = scale


solid_views = {
    "front": ((0.0, -4.2, 0.86), (0.0, 0.0, 0.84), 2.13),
    "three-quarter": ((3.0, -3.0, 0.88), (0.0, 0.0, 0.84), 2.13),
    "side": ((4.2, 0.0, 0.86), (0.0, 0.0, 0.84), 2.13),
    "back": ((0.0, 4.2, 0.86), (0.0, 0.0, 0.84), 2.13),
}
solid_paths = []
for label, (location, target, scale) in solid_views.items():
    point_camera(location, target, scale)
    path = OUTPUT / f"mother-connected-topology1-solid-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    solid_paths.append(path)

# Sparse actual-topology overlays, enabled only for the named seam closeup.
wire_material = gray_material("MotherCT1_ActualTopologyWire", 0.015, 0.88)
wire_material.node_tree.nodes.get("Principled BSDF").inputs["Emission Color"].default_value = (0.015, 0.015, 0.015, 1.0)
wire_targets = [cardigan, blouse, skirt, left_loafer, right_loafer, hair]
wire_objects = {}
for original in wire_targets:
    wire = original.copy()
    wire.data = original.data.copy()
    wire.name = f"QA_Wire_{original.name}"
    bpy.context.collection.objects.link(wire)
    wire.data.materials.clear()
    wire.data.materials.append(wire_material)
    for polygon in wire.data.polygons:
        polygon.material_index = 0
    modifier = wire.modifiers.new("QA_ActualTopologyWire", "WIREFRAME")
    modifier.thickness = 0.0018
    modifier.use_replace = True
    modifier.use_even_offset = True
    wire.hide_render = True
    wire_objects[original.name] = wire

wire_views = {
    "cardigan-front-opening": (
        [cardigan, blouse],
        (2.15, -3.5, 1.00),
        (0.0, -0.015, 0.93),
        1.12,
    ),
    "cardigan-shoulder-cuff": (
        [cardigan],
        (2.6, -3.7, 1.02),
        (0.285, -0.005, 0.99),
        1.05,
    ),
    "skirt-waist-hem": (
        [skirt],
        (2.3, -3.8, 0.49),
        (0.0, 0.0, 0.47),
        0.82,
    ),
    "hair-rear-half-up": (
        [hair],
        (2.4, 3.6, 1.46),
        (0.0, 0.06, 1.43),
        1.02,
    ),
    "loafer-ankle-overlap": (
        [left_loafer, right_loafer],
        (1.9, -3.2, 0.16),
        (0.0, -0.02, 0.12),
        0.48,
    ),
}
wire_paths = []
for label, (targets, location, target, scale) in wire_views.items():
    for wire in wire_objects.values():
        wire.hide_render = True
    for original in targets:
        wire_objects[original.name].hide_render = False
    point_camera(location, target, scale)
    path = OUTPUT / f"mother-connected-topology1-wire-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    wire_paths.append(path)

for wire in wire_objects.values():
    bpy.data.objects.remove(wire, do_unlink=True)
bpy.data.materials.remove(wire_material)

blend_path = OUTPUT / "mother-connected-topology1-gray-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

receipt = {
    "schema": "family-company.mother-connected-topology1.v1",
    "status": "DIAGNOSTIC_ONLY_STYLE_FAIL",
    "claimScope": [
        "Mother topology continuity",
        "Mother grayscale silhouette against supplied 2D authority",
        "connected garment/hair construction only",
    ],
    "excludedClaims": [
        "final color or shader fidelity",
        "face-age differentiation",
        "accessory fidelity",
        "animation deformation quality",
        "production readiness",
    ],
    "visualStyleGate": {
        "result": "FAIL",
        "candidateClaim": False,
        "reasons": [
            "cardigan reads as a rigid rectangular torso with tube sleeves",
            "blouse and skirt read as flat procedural cone/slab shells",
            "rear hair reads as a curtain plus spherical gathered mass",
            "loafers read as ball blobs rather than foot-enclosing loafers",
            "approximately 2.5-head-tall child silhouette does not match the 44-year adult authority",
        ],
        "nextPassConstraint": "do not reuse these primitive cage shapes; use donor-fitted/adult matched retopology with a 3.5-4 head silhouette",
    },
    "source": {
        "ownedMikaFbx": str(SOURCE_FBX.relative_to(REPO)).replace("\\", "/"),
        "ownedMikaSha256": sha256(SOURCE_FBX),
        "test3Sakurako": "EXCLUDED_NOT_OPENED",
        "unityModified": False,
        "docsModified": False,
    },
    "authority": {
        "neutral": str(AUTHORITY_NEUTRAL.relative_to(REPO)).replace("\\", "/"),
        "neutralSha256": sha256(AUTHORITY_NEUTRAL),
        "pixelWalk": str(AUTHORITY_PIXEL.relative_to(REPO)).replace("\\", "/"),
        "pixelWalkSha256": sha256(AUTHORITY_PIXEL),
        "readAs": [
            "open cardigan over fitted blouse",
            "below-knee A-line midi skirt without ruffles",
            "shoulder-length center-back half-up hair",
            "low-profile loafers",
            "adult parent silhouette with broader shoulder line and longer lower-body read",
        ],
    },
    "preservation": {
        "faceEyes": "original Mika face/eyes retained undeformed; grayscale review material only",
        "faceEyeCoordinateHashBefore": head_hash_before,
        "faceEyeCoordinateHashAfter": head_hash_after,
        "faceEyeCoordinatesUnchanged": head_hash_before == head_hash_after,
        "faceEyeVertexCount": len(head.data.vertices),
        "opaqueMouthBackgroundPlatePolygonsRemoved": len(mouth_polygons),
        "newMouthGeometry": 0,
        "hands": "original 3-digit stylized hand retained",
        "handCoordinateHashBefore": hand_hash_before,
        "handCoordinateHashAfter": hand_hash_after,
        "handCoordinatesRestPoseUnchanged": hand_hash_before == hand_hash_after,
        "handPolygons": len(hand_polygons),
        "handVertices": len(hands.data.vertices),
        "handComponentAudit": hand_component_receipt,
        "rigBones": len(armature.data.bones),
        "donorVertexGroups": len(source.vertex_groups),
        "rigUse": "hidden full owned Mika body is nearest-rest-space weight source for new topology; same armature modifier",
    },
    "topologyGate": stats,
    "underBodyTopology": body_stats,
    "constructionPolicy": {
        "finalVisibleCardiganObjects": 1,
        "finalVisibleBlouseObjects": 1,
        "finalVisibleSkirtObjects": 1,
        "finalVisibleLoaferObjects": 2,
        "finalVisibleHairObjects": 1,
        "constructionCagesRemovedAfterVoxelUnion": True,
        "floatingTrimOrAccessoryGeometry": 0,
        "collarButtonsAndShoeDetail": "texture-only/deferred; not geometry",
        "fantasyWingsHaloBowsRufflesSideBunWaistHairVisible": False,
        "allClaimedSurfacesOneComponentClosedManifold": all(
            item["connectedComponents"] == 1 and item["boundaryEdges"] == 0 and item["nonManifoldEdges"] == 0
            for item in stats.values()
        ),
        "colorFinalGifGenerated": False,
    },
    "silhouetteMetricsWorld": {
        "visibleBounds": {
            "min": [round(float(value), 6) for value in visible_lo],
            "max": [round(float(value), 6) for value in visible_hi],
            "dimensions": [round(float(value), 6) for value in (visible_hi - visible_lo)],
        },
        "preservedFaceBounds": {
            "min": [round(float(value), 6) for value in face_lo],
            "max": [round(float(value), 6) for value in face_hi],
            "dimensions": [round(float(value), 6) for value in (face_hi - face_lo)],
        },
        "headWidthToVisibleHeight": round(float((face_hi.x - face_lo.x) / (visible_hi.z - visible_lo.z)), 6),
        "cardigan": mesh_world_bounds(cardigan),
        "blouse": mesh_world_bounds(blouse),
        "midiSkirt": mesh_world_bounds(skirt),
        "hair": mesh_world_bounds(hair),
        "leftLoafer": mesh_world_bounds(left_loafer),
        "rightLoafer": mesh_world_bounds(right_loafer),
        "targetReading": "modestly reduced teen head/body impression through broader shoulder line, fitted longer torso, below-knee skirt and clean calf-to-loafer span; preserved head itself is not morphed",
    },
    "proofs": {
        "solidGray": [str(path.relative_to(REPO)).replace("\\", "/") for path in solid_paths],
        "sparseActualWireSeams": [str(path.relative_to(REPO)).replace("\\", "/") for path in wire_paths],
        "gif": None,
    },
    "blend": str(blend_path.relative_to(REPO)).replace("\\", "/"),
}
receipt_path = OUTPUT / "mother-connected-topology1-receipt.json"
receipt_path.write_text(json.dumps(receipt, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(json.dumps(receipt, indent=2, ensure_ascii=False))
