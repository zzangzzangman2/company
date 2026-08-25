"""MotherRetopo3: donor-fit bridge/weld retopology gate from owned Mika.

This static gray pass keeps the imported Mika face/eyes, native three-digit
hands, their vertex weights, and the complete 151-bone armature unchanged.
Only clean retopology derived from the audited Mika garment/hair/shoe fit is
added. CT1 cages, box stacking, test3/Sakurako and Unity are never used.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from collections import Counter, defaultdict, deque
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--output",
        default="Artifacts/Family3DOwnedBaseConversion/MotherRetopo3",
    )
    parser.add_argument("--style4", action="store_true")
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(argv)


ARGS = parse_args()
STYLE4 = bool(ARGS.style4)
FILE_STEM = "mother-retopo4-style" if STYLE4 else "mother-retopo3"
PASS_LABEL = "MotherRetopo4Style" if STYLE4 else "MotherRetopo3"
REPO = Path(__file__).resolve().parents[2]
OUTPUT = (REPO / ARGS.output).resolve()
OUTPUT.mkdir(parents=True, exist_ok=True)
SOURCE_FBX = (
    REPO
    / "Artifacts"
    / "ExternalReferenceStudy"
    / "UserProvided_BlueArchive_OriginalMeshes_2026-08-24"
    / "Mika"
    / "CH0069_Mesh"
    / "CH0069_Mesh.fbx"
)
AUTHORITY_NEUTRAL = REPO / "Assets/Art/Characters/Mother/mother_office_neutral_v1.png"
AUTHORITY_PIXEL = REPO / "Assets/Art/Characters/Mother/Pixel/HighMotion/mother_pixel_walk8dir6_a_v1.png"


def sha256(path: Path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def coordinate_hash(points):
    normalized = sorted(tuple(round(float(value), 7) for value in point) for point in points)
    return hashlib.sha256(json.dumps(normalized, separators=(",", ":")).encode("utf-8")).hexdigest().upper()


def weight_hash(obj, indices=None):
    if indices is None:
        indices = range(len(obj.data.vertices))
    group_names = {group.index: group.name for group in obj.vertex_groups}
    records = []
    for index in indices:
        vertex = obj.data.vertices[index]
        records.append(
            (
                tuple(round(float(value), 7) for value in (obj.matrix_world @ vertex.co)),
                sorted((group_names[item.group], round(float(item.weight), 7)) for item in vertex.groups),
            )
        )
    return hashlib.sha256(json.dumps(sorted(records), separators=(",", ":")).encode("utf-8")).hexdigest().upper()


def rig_hash(armature):
    records = []
    for bone in armature.data.bones:
        records.append(
            (
                bone.name,
                tuple(round(float(value), 8) for value in bone.head_local),
                tuple(round(float(value), 8) for value in bone.tail_local),
                bone.parent.name if bone.parent else None,
                bool(bone.use_connect),
            )
        )
    return hashlib.sha256(json.dumps(sorted(records), separators=(",", ":")).encode("utf-8")).hexdigest().upper()


def gray_material(name, value, roughness=0.76):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = (value, value, value, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    if "IOR Level" in shader.inputs:
        shader.inputs["IOR Level"].default_value = 0.14
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (value, value, value, 1.0)
    return material


def color_material(name, color, roughness=0.76):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    if "IOR Level" in shader.inputs:
        shader.inputs["IOR Level"].default_value = 0.16
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (*color, 1.0)
    return material


def components_for_material(mesh, material_index):
    polygons = [polygon for polygon in mesh.polygons if polygon.material_index == material_index]
    polygon_map = {polygon.index: polygon for polygon in polygons}
    by_vertex = defaultdict(list)
    for polygon in polygons:
        for vertex in polygon.vertices:
            by_vertex[vertex].append(polygon.index)
    remaining = set(polygon_map)
    result = []
    while remaining:
        seed = remaining.pop()
        todo = deque([seed])
        component = {seed}
        while todo:
            polygon = polygon_map[todo.popleft()]
            for vertex in polygon.vertices:
                for neighbor in by_vertex[vertex]:
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        component.add(neighbor)
                        todo.append(neighbor)
        result.append(sorted(component))
    return sorted(result, key=len, reverse=True)


def component_vertices(mesh, polygons):
    return sorted({vertex for polygon in polygons for vertex in mesh.polygons[polygon].vertices})


def world_bounds(obj, indices):
    points = [obj.matrix_world @ obj.data.vertices[index].co for index in indices]
    low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return {
        "min": [round(float(value), 6) for value in low],
        "max": [round(float(value), 6) for value in high],
        "dimensions": [round(float(value), 6) for value in (high - low)],
        "center": [round(float(value), 6) for value in ((low + high) * 0.5)],
    }


def recalc_normals(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def topology_stats(obj):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    unseen = set(mesh.verts)
    components = 0
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


def delete_unkept_polygons(obj, kept):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    mesh.faces.ensure_lookup_table()
    bmesh.ops.delete(mesh, geom=[face for face in mesh.faces if face.index not in kept], context="FACES")
    loose = [vertex for vertex in mesh.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(mesh, geom=loose, context="VERTS")
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()


def replace_material_slots(obj, mapping, fallback):
    for index, material in enumerate(list(obj.data.materials)):
        name = material.name if material else ""
        replacement = fallback
        for token, candidate in mapping:
            if token in name:
                replacement = candidate
                break
        obj.data.materials[index] = replacement
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def raw_world_object(name, vertices, faces):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    recalc_normals(obj)
    return obj


def ring_volume(rings, segments=40):
    """Closed donor-fit loft; adjacent fitted rings share welded indices."""
    vertices = []
    faces = []
    for z, radius_x, radius_y, center_y in rings:
        for index in range(segments):
            theta = 2.0 * math.pi * index / segments
            vertices.append((radius_x * math.sin(theta), center_y + radius_y * math.cos(theta), z))
    for ring in range(len(rings) - 1):
        for index in range(segments):
            next_index = (index + 1) % segments
            lower = ring * segments
            upper = (ring + 1) * segments
            faces.append((lower + index, lower + next_index, upper + next_index, upper + index))
    bottom = len(vertices)
    vertices.append((0.0, rings[0][3], rings[0][0]))
    top = len(vertices)
    vertices.append((0.0, rings[-1][3], rings[-1][0]))
    for index in range(segments):
        next_index = (index + 1) % segments
        faces.append((bottom, next_index, index))
        last = (len(rings) - 1) * segments
        faces.append((top, last + index, last + next_index))
    return vertices, faces


def open_cardigan_shell(rings, segments=44, front_gap=0.38, thickness=0.014):
    """Closed U-section cloth volume with two real rounded front edges."""
    vertices = []
    faces = []
    theta_start = -math.pi + front_gap
    theta_end = math.pi - front_gap
    row = segments + 1
    layer_size = len(rings) * row
    for layer in range(2):
        for z, radius_x, radius_y, center_y in rings:
            if layer == 1:
                radius_x -= thickness
                radius_y -= thickness
            for index in range(row):
                theta = theta_start + (theta_end - theta_start) * index / segments
                vertices.append((radius_x * math.sin(theta), center_y + radius_y * math.cos(theta), z))

    def vertex_id(layer, ring, index):
        return layer * layer_size + ring * row + index

    for ring in range(len(rings) - 1):
        for index in range(segments):
            faces.append((vertex_id(0, ring, index), vertex_id(0, ring, index + 1), vertex_id(0, ring + 1, index + 1), vertex_id(0, ring + 1, index)))
            faces.append((vertex_id(1, ring, index), vertex_id(1, ring + 1, index), vertex_id(1, ring + 1, index + 1), vertex_id(1, ring, index + 1)))
    last_ring = len(rings) - 1
    for index in range(segments):
        faces.append((vertex_id(0, 0, index), vertex_id(1, 0, index), vertex_id(1, 0, index + 1), vertex_id(0, 0, index + 1)))
        faces.append((vertex_id(0, last_ring, index), vertex_id(0, last_ring, index + 1), vertex_id(1, last_ring, index + 1), vertex_id(1, last_ring, index)))
    for ring in range(len(rings) - 1):
        faces.append((vertex_id(0, ring, 0), vertex_id(0, ring + 1, 0), vertex_id(1, ring + 1, 0), vertex_id(1, ring, 0)))
        faces.append((vertex_id(0, ring, segments), vertex_id(1, ring, segments), vertex_id(1, ring + 1, segments), vertex_id(0, ring + 1, segments)))
    return vertices, faces


def swept_sleeve(start, end, radii, segments=24):
    """Closed tapered sleeve loft with donor A-pose axis and shared ring loops."""
    start = Vector(start)
    end = Vector(end)
    axis = end - start
    axis_direction = axis.normalized()
    lateral = Vector((-axis_direction.z, 0.0, axis_direction.x)).normalized()
    depth = Vector((0.0, 1.0, 0.0))
    vertices = []
    faces = []
    for ring, (t, radius) in enumerate(radii):
        center = start.lerp(end, t)
        # A slight lower cloth bias avoids a rigid perfect tube silhouette.
        center.z -= 0.010 * math.sin(math.pi * t)
        for index in range(segments):
            theta = 2.0 * math.pi * index / segments
            radial = lateral * (math.cos(theta) * radius) + depth * (math.sin(theta) * radius * 0.78)
            vertices.append(tuple(center + radial))
    for ring in range(len(radii) - 1):
        for index in range(segments):
            next_index = (index + 1) % segments
            lower = ring * segments
            upper = (ring + 1) * segments
            faces.append((lower + index, lower + next_index, upper + next_index, upper + index))
    for endpoint, reverse in ((0, True), (len(radii) - 1, False)):
        center_index = len(vertices)
        center = start if endpoint == 0 else end
        vertices.append(tuple(center))
        offset = endpoint * segments
        for index in range(segments):
            next_index = (index + 1) % segments
            faces.append((center_index, offset + (index if reverse else next_index), offset + (next_index if reverse else index)))
    return vertices, faces


def thick_skirt_shell(segments=48, rings=14, thickness=0.014):
    """One closed outer/inner A-line shell with bridged waist and hem loops."""
    vertices = []
    faces = []
    z_top, z_bottom = (0.710, 0.090) if STYLE4 else (0.715, 0.115)
    for layer in range(2):
        for ring in range(rings + 1):
            t = ring / rings
            ease = t * t * (3.0 - 2.0 * t)
            hip = 0.020 * math.sin(math.pi * min(1.0, t / 0.55)) if t < 0.55 else 0.0
            radius_x = 0.222 + 0.096 * ease + hip
            radius_y = 0.150 + 0.070 * ease + 0.5 * hip
            if layer == 1:
                radius_x -= thickness
                radius_y -= thickness
            z = z_top + (z_bottom - z_top) * t
            for index in range(segments):
                theta = 2.0 * math.pi * index / segments
                drape = 1.0
                if STYLE4:
                    # Five extremely shallow cloth waves use the existing
                    # longitudinal loops; they do not add ruffle islands.
                    drape += 0.016 * (ease ** 1.25) * math.cos(5.0 * theta + 0.32)
                # Subtle back ease; no ruffle or floating panel shape.
                center_y = 0.006 + 0.008 * ease
                vertices.append((radius_x * drape * math.sin(theta), center_y + radius_y * drape * math.cos(theta), z))

    layer_size = (rings + 1) * segments

    def vertex_id(layer, ring, index):
        return layer * layer_size + ring * segments + index % segments

    for ring in range(rings):
        for index in range(segments):
            next_index = (index + 1) % segments
            faces.append((vertex_id(0, ring, index), vertex_id(0, ring, next_index), vertex_id(0, ring + 1, next_index), vertex_id(0, ring + 1, index)))
            faces.append((vertex_id(1, ring, index), vertex_id(1, ring + 1, index), vertex_id(1, ring + 1, next_index), vertex_id(1, ring, next_index)))
    for index in range(segments):
        next_index = (index + 1) % segments
        faces.append((vertex_id(0, 0, index), vertex_id(1, 0, index), vertex_id(1, 0, next_index), vertex_id(0, 0, next_index)))
        faces.append((vertex_id(0, rings, index), vertex_id(0, rings, next_index), vertex_id(1, rings, next_index), vertex_id(1, rings, index)))
    return vertices, faces


def lower_leg(side, segments=24, rings=8):
    center_x = 0.142 * side
    vertices = []
    faces = []
    for ring in range(rings + 1):
        t = ring / rings
        bottom_z = -0.180 if STYLE4 else -0.160
        z = 0.330 + (bottom_z - 0.330) * t
        top_x, ankle_x = ((0.064, 0.047) if STYLE4 else (0.070, 0.052))
        top_y, ankle_y = ((0.072, 0.052) if STYLE4 else (0.078, 0.058))
        radius_x = top_x + (ankle_x - top_x) * t
        radius_y = top_y + (ankle_y - top_y) * t
        for index in range(segments):
            theta = 2.0 * math.pi * index / segments
            vertices.append((center_x + radius_x * math.sin(theta), -0.005 + radius_y * math.cos(theta), z))
    for ring in range(rings):
        for index in range(segments):
            next_index = (index + 1) % segments
            lower = ring * segments
            upper = (ring + 1) * segments
            faces.append((lower + index, lower + next_index, upper + next_index, upper + index))
    for ring, reverse in ((0, True), (rings, False)):
        center_index = len(vertices)
        vertices.append((center_x, -0.005, 0.330 if ring == 0 else (-0.180 if STYLE4 else -0.160)))
        offset = ring * segments
        for index in range(segments):
            next_index = (index + 1) % segments
            faces.append((center_index, offset + (index if reverse else next_index), offset + (next_index if reverse else index)))
    return vertices, faces


def square_to_disk(u, v):
    return (
        u * math.sqrt(max(0.0, 1.0 - 0.5 * v * v)),
        v * math.sqrt(max(0.0, 1.0 - 0.5 * u * u)),
    )


def quad_boundary_cage(nx, ny, nz, mapper):
    vertices = []
    faces = []
    lookup = {}

    def vertex_id(i, j, k):
        key = (i, j, k)
        if key not in lookup:
            lookup[key] = len(vertices)
            vertices.append(tuple(mapper(-1.0 + 2.0 * i / nx, -1.0 + 2.0 * j / ny, -1.0 + 2.0 * k / nz)))
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


def loafer_mapper(side):
    center_x = 0.142 * side

    def mapper(u, v, w):
        direction = Vector((0.94 * u, 1.00 * v, 1.30 * w))
        direction.normalize()
        dx, dy, dz = direction
        half_width = (0.080 if STYLE4 else 0.084) * (1.0 - 0.08 * max(0.0, -dy))
        length = (0.215 if STYLE4 else 0.185) if dy < 0.0 else (0.112 if STYLE4 else 0.105)
        x = center_x + half_width * dx
        y = -0.025 + length * dy
        if w <= -0.999:
            z = -0.290 if STYLE4 else -0.270
        else:
            z = (-0.235 + 0.060 * dz) if STYLE4 else (-0.215 + 0.060 * dz)
            if dz > 0.0:
                z -= (0.016 if STYLE4 else 0.022) * max(0.0, -dy)
                z += (0.004 if STYLE4 else 0.006) * max(0.0, dy)
        return Vector((x, y, z))

    return mapper


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX))
armature = bpy.data.objects.get("Armature")
source = bpy.data.objects.get("CH0069_Body")
weapon = bpy.data.objects.get("CH0069_Weapon")
if armature is None or source is None:
    raise RuntimeError("Owned Mika import failed")
if len(armature.data.bones) != 151:
    raise RuntimeError(f"Expected 151 Mika bones, got {len(armature.data.bones)}")
if weapon:
    bpy.data.objects.remove(weapon, do_unlink=True)
rig_hash_before = rig_hash(armature)
armature.name = "MotherRetopo3_Rig_Mika151_Unchanged"
armature.scale = tuple(value * 180.0 for value in armature.scale)
bpy.context.view_layer.update()
armature.hide_render = True
source.name = "MotherRetopo3_MikaWeightSource_Hidden"
source.hide_render = True
source.hide_set(True)

material_names = [material.name if material else "" for material in source.data.materials]


def slot(prefix):
    return next(index for index, name in enumerate(material_names) if name.startswith(prefix))


hair_slot = slot("CH0069_Hair")
face_slot = slot("CH0069_Face")
brow_slot = slot("CH0069_Eyebrow")
eye_slot = slot("CH0069_EyeMouth")
body_slot = slot("CH0069_Body")
body_components = components_for_material(source.data, body_slot)
hair_components = components_for_material(source.data, hair_slot)
eye_components = components_for_material(source.data, eye_slot)
if len(body_components) != 389:
    raise RuntimeError(f"Expected 389 Mika body material islands, got {len(body_components)}")
if len(hair_components) < 22:
    raise RuntimeError(f"Expected at least 22 Mika hair islands, got {len(hair_components)}")
mouth_components = [component for component in eye_components if len(component) == 32]
if len(mouth_components) != 1:
    raise RuntimeError("Mika opaque mouth plate component changed")
opaque_mouth_polygons = set(mouth_components[0])

MAT_FACE = gray_material("MotherR3_FaceGray", 0.72)
MAT_EYE = gray_material("MotherR3_EyeGray", 0.14, 0.62)
MAT_BROW = gray_material("MotherR4_SoftBrowGray", 0.25, 0.68)
MAT_HAND = gray_material("MotherR3_HandGray", 0.69)
MAT_CARDIGAN = gray_material("MotherR3_CardiganGray", 0.43, 0.84)
MAT_BLOUSE = gray_material("MotherR3_BlouseGray", 0.79, 0.82)
MAT_SKIRT = gray_material("MotherR3_MidiSkirtGray", 0.31, 0.84)
MAT_SKIN = gray_material("MotherR3_LegSkinGray", 0.68)
MAT_HAIR = gray_material("MotherR3_HairGray", 0.18, 0.80)
MAT_SHOE = gray_material("MotherR3_LoaferGray", 0.15, 0.70)
MAT_MOUTH = gray_material("MotherR3_SurfaceMouthGray", 0.10, 0.62)
MAT_WIRE = gray_material("MotherR3_SeamWire", 0.035, 0.62)

# Immutable face/eyes/eyebrows. The unusable 32-polygon background plate is
# excluded; the surface-attached mouth below is a separate repair strip.
head_polygons = {
    polygon.index
    for polygon in source.data.polygons
    if polygon.material_index in {face_slot, brow_slot, eye_slot}
    and polygon.index not in opaque_mouth_polygons
}
head_vertices = component_vertices(source.data, head_polygons)
head_points_before = [source.matrix_world @ source.data.vertices[index].co for index in head_vertices]
head_coord_hash_before = coordinate_hash(head_points_before)
head_weight_hash_before = weight_hash(source, head_vertices)
head = source.copy()
head.data = source.data.copy()
head.name = "MotherRetopo3_ImmutableMikaFaceEyes"
bpy.context.collection.objects.link(head)
head.hide_render = False
head.hide_set(False)
delete_unkept_polygons(head, head_polygons)
replace_material_slots(
    head,
    [("EyeMouth", MAT_EYE), ("Eyebrow", MAT_BROW), ("Face", MAT_FACE)],
    MAT_FACE,
)
head_points_after = [head.matrix_world @ vertex.co for vertex in head.data.vertices]
head_coord_hash_after = coordinate_hash(head_points_after)
head_weight_hash_after = weight_hash(head)
if head_coord_hash_before != head_coord_hash_after or head_weight_hash_before != head_weight_hash_after:
    raise RuntimeError("Immutable Mika face/eyes changed")

# Original three-digit hands only; weighted wrist ornaments are rejected as
# complete islands. This is identical to the approved preservation selection.
def hand_weight_fractions(vertices):
    hand = arm = total = 0.0
    for vertex_index in vertices:
        for membership in source.data.vertices[vertex_index].groups:
            name = source.vertex_groups[membership.group].name.lower()
            weight = float(membership.weight)
            if " hand" in name or " finger" in name:
                hand += weight
            if any(token in name for token in ("upperarm", "forearm", "wrist")):
                arm += weight
            total += weight
    return hand / max(total, 1.0e-9), arm / max(total, 1.0e-9)


hand_components = set()
hand_component_audit = []
for component_id, component in enumerate(body_components):
    vertices = component_vertices(source.data, component)
    bounds = world_bounds(source, vertices)
    center = Vector(bounds["center"])
    dimensions = Vector(bounds["dimensions"])
    hand_fraction, arm_fraction = hand_weight_fractions(vertices)
    ornament = (
        0.395 < abs(center.x) < 0.495
        and 0.72 < center.z < 0.91
        and dimensions.x < 0.135
        and hand_fraction + arm_fraction > 0.35
    )
    keep = hand_fraction > 0.30 and not ornament
    if keep:
        hand_components.add(component_id)
    if keep or ornament:
        hand_component_audit.append(
            {
                "componentId": component_id,
                "polygons": len(component),
                "handWeightFraction": round(hand_fraction, 6),
                "armWeightFraction": round(arm_fraction, 6),
                "kept": keep,
                "wholeWristOrnamentDeleted": ornament,
            }
        )
hand_polygons = set().union(*(set(body_components[index]) for index in hand_components))
if len(hand_polygons) != 454:
    raise RuntimeError(f"Expected 454 native hand polygons, got {len(hand_polygons)}")
hand_vertices = component_vertices(source.data, hand_polygons)
hand_points_before = [source.matrix_world @ source.data.vertices[index].co for index in hand_vertices]
hand_coord_hash_before = coordinate_hash(hand_points_before)
hand_weight_hash_before = weight_hash(source, hand_vertices)
hands = source.copy()
hands.data = source.data.copy()
hands.name = "MotherRetopo3_ImmutableOriginalThreeDigitHands"
bpy.context.collection.objects.link(hands)
hands.hide_render = False
hands.hide_set(False)
delete_unkept_polygons(hands, hand_polygons)
replace_material_slots(hands, [], MAT_HAND)
hand_points_after = [hands.matrix_world @ vertex.co for vertex in hands.data.vertices]
hand_coord_hash_after = coordinate_hash(hand_points_after)
hand_weight_hash_after = weight_hash(hands)
if hand_coord_hash_before != hand_coord_hash_after or hand_weight_hash_before != hand_weight_hash_after:
    raise RuntimeError("Original three-digit hands changed")

# Nearest-rest-space Mika weights for every clean retopo surface.
source_kd = KDTree(len(source.data.vertices))
for vertex in source.data.vertices:
    source_kd.insert(source.matrix_world @ vertex.co, vertex.index)
source_kd.balance()


def transfer_weights_and_armature(obj):
    for group in source.vertex_groups:
        obj.vertex_groups.new(name=group.name)
    group_names = {group.index: group.name for group in source.vertex_groups}
    distances = []
    for vertex in obj.data.vertices:
        world_point = obj.matrix_world @ vertex.co
        nearest, source_index, distance = source_kd.find(world_point)
        distances.append(float(distance))
        for membership in source.data.vertices[source_index].groups:
            obj.vertex_groups[group_names[membership.group]].add([vertex.index], membership.weight, "REPLACE")
    modifier = obj.modifiers.new("MotherRetopo3_TransferredMikaWeights151", "ARMATURE")
    modifier.object = armature
    return {
        "meanNearestDonorDistance": round(sum(distances) / max(len(distances), 1), 6),
        "maxNearestDonorDistance": round(max(distances, default=0.0), 6),
        "weightedVertices": len(distances),
    }


def skinned_world_object(name, vertices, faces, material):
    inverse = source.matrix_world.inverted()
    local_vertices = [tuple(inverse @ Vector(point)) for point in vertices]
    mesh = bpy.data.meshes.new(name + "_Mesh")
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
    obj["weightTransferReceipt"] = json.dumps(transfer_weights_and_armature(obj))
    return obj


def voxel_union(name, specs, voxel_size, material, smooth_steps=3, smooth_factor=0.16):
    raw_parts = [raw_world_object(f"QA_RetopoSeed_{name}_{index:02d}", vertices, faces) for index, (vertices, faces) in enumerate(specs)]
    pre_stats = [topology_stats(obj) for obj in raw_parts]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in raw_parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = raw_parts[0]
    bpy.ops.object.join()
    joined = raw_parts[0]
    joined.data.remesh_voxel_size = voxel_size
    joined.data.remesh_voxel_adaptivity = 0.0
    bpy.context.view_layer.objects.active = joined
    bpy.ops.object.voxel_remesh()
    edit = bmesh.new()
    edit.from_mesh(joined.data)
    for _ in range(smooth_steps):
        bmesh.ops.smooth_vert(edit, verts=list(edit.verts), factor=smooth_factor, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    edit.to_mesh(joined.data)
    edit.free()
    recalc_normals(joined)
    vertices = [tuple(joined.matrix_world @ vertex.co) for vertex in joined.data.vertices]
    faces = [tuple(polygon.vertices) for polygon in joined.data.polygons]
    bpy.data.objects.remove(joined, do_unlink=True)
    obj = skinned_world_object(name, vertices, faces, material)
    return obj, pre_stats


# One connected cardigan surface with integrated sleeves. Style4 replaces the
# rejected painted center strip with a real U-section opening and a fitted
# blouse beneath; the two front edges are bridged through garment thickness.
if STYLE4:
    blouse_spec = ring_volume(
        [
            (0.665, 0.207, 0.128, 0.004),
            (0.735, 0.210, 0.131, 0.002),
            (0.820, 0.203, 0.128, -0.002),
            (0.910, 0.214, 0.135, -0.004),
            (0.995, 0.232, 0.141, -0.002),
            (1.052, 0.242, 0.139, 0.004),
            (1.092, 0.192, 0.118, 0.010),
            (1.116, 0.105, 0.087, 0.015),
        ],
        44,
    )
    blouse = skinned_world_object("MotherRetopo4Style_FittedBlouseUnderCardigan", *blouse_spec, MAT_BLOUSE)
    cardigan_torso_spec = open_cardigan_shell(
        [
            (0.655, 0.226, 0.143, 0.007),
            (0.715, 0.232, 0.148, 0.006),
            (0.790, 0.213, 0.139, 0.002),
            (0.885, 0.222, 0.146, -0.001),
            (0.970, 0.246, 0.153, -0.001),
            (1.025, 0.281, 0.154, 0.004),
            (1.068, 0.246, 0.137, 0.010),
            (1.102, 0.133, 0.099, 0.017),
        ],
        segments=46,
        front_gap=0.405,
        thickness=0.014,
    )
    sleeve_profile = [(0.0, 0.094), (0.18, 0.091), (0.44, 0.080), (0.70, 0.068), (0.86, 0.054), (1.0, 0.059)]
    left_sleeve_spec = swept_sleeve((-0.228, 0.000, 0.996), (-0.527, -0.006, 0.796), sleeve_profile)
    right_sleeve_spec = swept_sleeve((0.228, 0.000, 0.996), (0.527, -0.006, 0.796), sleeve_profile)
    upper, upper_pre_stats = voxel_union(
        "MotherRetopo4Style_OpenCardiganIntegratedSleevesCuffs",
        [cardigan_torso_spec, left_sleeve_spec, right_sleeve_spec],
        0.0088,
        MAT_CARDIGAN,
        smooth_steps=4,
        smooth_factor=0.15,
    )
else:
    blouse = None
    torso_spec = ring_volume(
        [
            (0.660, 0.238, 0.146, 0.006),
            (0.715, 0.255, 0.154, 0.006),
            (0.790, 0.224, 0.143, 0.002),
            (0.885, 0.235, 0.151, -0.002),
            (0.980, 0.258, 0.158, -0.002),
            (1.035, 0.292, 0.157, 0.003),
            (1.078, 0.254, 0.137, 0.010),
            (1.120, 0.114, 0.096, 0.018),
        ],
        44,
    )
    sleeve_profile = [(0.0, 0.096), (0.16, 0.096), (0.42, 0.087), (0.68, 0.078), (0.86, 0.068), (1.0, 0.062)]
    left_sleeve_spec = swept_sleeve((-0.232, 0.000, 1.002), (-0.525, -0.006, 0.800), sleeve_profile)
    right_sleeve_spec = swept_sleeve((0.232, 0.000, 1.002), (0.525, -0.006, 0.800), sleeve_profile)
    upper, upper_pre_stats = voxel_union(
        "MotherRetopo3_OneContinuousCardiganBlouseAndSleeves",
        [torso_spec, left_sleeve_spec, right_sleeve_spec],
        0.0090,
        MAT_CARDIGAN,
        smooth_steps=4,
        smooth_factor=0.15,
    )
    upper.data.materials.append(MAT_BLOUSE)
    upper.data.update()
    for polygon in upper.data.polygons:
        center = upper.matrix_world @ polygon.center
        t = max(0.0, min(1.0, (center.z - 0.69) / 0.40))
        blouse_half_width = 0.064 + 0.034 * t
        collar_wing = (
            center.z > 1.015
            and abs(center.x) < 0.132
            and center.z < 1.125 - 0.72 * max(0.0, abs(center.x) - 0.068)
        )
        if center.y < -0.105 and (abs(center.x) < blouse_half_width or collar_wing) and 0.68 < center.z < 1.115:
            polygon.material_index = 1
        else:
            polygon.material_index = 0
        polygon.use_smooth = True

# One closed outer/inner skirt shell; shared ring indices and explicit waist/
# hem bridge faces replace all four disconnected Mika fantasy panels.
skirt_vertices, skirt_faces = thick_skirt_shell()
skirt = skinned_world_object(PASS_LABEL + "_OneClosedBelowKneeMidiSkirt", skirt_vertices, skirt_faces, MAT_SKIRT)

# Smooth donor-fit lower legs overlap both skirt interior and loafer collars so
# no black horizontal gap can occur in the static silhouette.
left_leg_vertices, left_leg_faces = lower_leg(-1.0)
right_leg_vertices, right_leg_faces = lower_leg(1.0)
left_leg = skinned_world_object(PASS_LABEL + "_LowerLeg_L_DonorFitRetopo", left_leg_vertices, left_leg_faces, MAT_SKIN)
right_leg = skinned_world_object(PASS_LABEL + "_LowerLeg_R_DonorFitRetopo", right_leg_vertices, right_leg_faces, MAT_SKIN)

# Rounded low loafers: each is one closed all-quad cage with an integral flat
# sole. They are reshaped from the audited CH0069 body 22/23 foot-shell fit.
left_shoe_vertices, left_shoe_faces = quad_boundary_cage(8, 12, 6, loafer_mapper(-1.0))
right_shoe_vertices, right_shoe_faces = quad_boundary_cage(8, 12, 6, loafer_mapper(1.0))
left_shoe = skinned_world_object(PASS_LABEL + "_Loafer_L_ClosedSole", left_shoe_vertices, left_shoe_faces, MAT_SHOE)
right_shoe = skinned_world_object(PASS_LABEL + "_Loafer_R_ClosedSole", right_shoe_vertices, right_shoe_faces, MAT_SHOE)

# Donor-derived hair: keep the coherent main scalp/back component, source
# bangs and two face-framing locks. Long points are remapped into staggered
# neck/shoulder-following tips; all side-bun width is pulled into the scalp.
HAIR_COMPONENTS = {0, 1, 3, 4}
hair_polygons = set().union(*(set(hair_components[index]) for index in HAIR_COMPONENTS))
hair = source.copy()
hair.data = source.data.copy()
hair.name = PASS_LABEL + "_DonorDerivedShoulderHalfUpHair"
bpy.context.collection.objects.link(hair)
hair.hide_render = False
hair.hide_set(False)
delete_unkept_polygons(hair, hair_polygons)
replace_material_slots(hair, [], MAT_HAIR)
hair_inverse = hair.matrix_world.inverted()
for vertex in hair.data.vertices:
    point = hair.matrix_world @ vertex.co
    original = point.copy()
    # Eliminate the connected side-bun/waist-lock breadth without detaching a
    # replacement accessory. The scalp remains source fitted above 1.40 m.
    if abs(point.x) > 0.340:
        point.x = math.copysign(0.340 + 0.018 * math.tanh((abs(point.x) - 0.340) / 0.08), point.x)
    if point.y > 0.430:
        point.y = 0.430 + 0.025 * math.tanh((point.y - 0.430) / 0.10)
    if point.z < 1.400:
        q = max(0.0, min(1.0, (point.z - 0.486) / (1.400 - 0.486)))
        lateral = 0.46 + 0.34 * (q ** 0.65)
        point.x *= lateral
        point.y = 0.105 + (point.y - 0.105) * (0.38 + 0.34 * q)
        staggered_tip = 1.015 + 0.085 * min(1.0, abs(point.x) / 0.34) + 0.025 * max(0.0, -point.y / 0.20)
        point.z = staggered_tip + (1.400 - staggered_tip) * (q ** 0.72)
    elif STYLE4:
        # Reduce the spherical helmet read while preserving the donor scalp
        # fit around the forehead and ears.
        crown = max(0.0, min(1.0, (point.z - 1.400) / 0.354))
        point.x *= 1.0 - 0.045 * crown
        point.y = 0.055 + (point.y - 0.055) * (1.0 - 0.055 * crown)
        point.z -= 0.018 * (crown ** 1.5)
    # Embedded low half-up gather is a deformation of the same main donor
    # surface, not a bun/sphere/curtain object.
    if point.y > 0.16 and abs(point.x) < 0.18 and 1.23 < point.z < 1.50:
        gx = 1.0 - abs(point.x) / 0.18
        gz = 1.0 - abs(point.z - 1.355) / 0.145
        gather = max(0.0, gx * gz)
        point.x *= 1.0 - (0.27 if STYLE4 else 0.18) * gather
        point.y += (0.058 if STYLE4 else 0.038) * gather
        if STYLE4:
            point.x += 0.010 * math.sin((point.z - 1.23) * 18.0) * gather
    vertex.co = hair_inverse @ point
hair.data.update()
recalc_normals(hair)
hair_subdivision = hair.modifiers.new("MotherRetopo3_DonorHairSurfaceSmoothing", "SUBSURF")
hair_subdivision.levels = 1
hair_subdivision.render_levels = 1

# A small face-projected strip replaces only the unusable opaque plate. It is
# flush to the preserved face and does not alter any source face coordinate.
face_only_polygons = [polygon.index for polygon in source.data.polygons if polygon.material_index == face_slot]
face_only_vertices = component_vertices(source.data, face_only_polygons)
face_index = {old: new for new, old in enumerate(face_only_vertices)}
face_world = [source.matrix_world @ source.data.vertices[index].co for index in face_only_vertices]
face_faces = [tuple(face_index[index] for index in source.data.polygons[polygon].vertices) for polygon in face_only_polygons]
face_bvh = BVHTree.FromPolygons(face_world, face_faces, all_triangles=False)
mouth_vertices = []
for row in (-1.0, 1.0):
    for column in range(5):
        t = -1.0 + 0.5 * column
        target_z = 1.304 + 0.0065 * abs(t) ** 1.55 + row * 0.0024
        location, normal, _, _ = face_bvh.ray_cast(
            Vector((0.034 * t, -1.0, target_z)),
            Vector((0.0, 1.0, 0.0)),
            2.0,
        )
        if location is None:
            raise RuntimeError("Mother mouth projection failed")
        outward = normal if normal.y < 0.0 else -normal
        mouth_vertices.append(tuple(location + outward.normalized() * 0.0013))
mouth_faces = [(column, column + 1, 5 + column + 1, 5 + column) for column in range(4)]
mouth = skinned_world_object(PASS_LABEL + "_SurfaceAttachedCalmMouth", mouth_vertices, mouth_faces, MAT_MOUTH)

objects = [head, hands, upper, skirt, left_leg, right_leg, left_shoe, right_shoe, hair, mouth]
garments = [upper, skirt, left_leg, right_leg, left_shoe, right_shoe]
if blouse is not None:
    objects.append(blouse)
    garments.append(blouse)
stats = {obj.name: topology_stats(obj) for obj in garments}
required_closed = [upper.name, skirt.name, left_shoe.name, right_shoe.name]
if blouse is not None:
    required_closed.append(blouse.name)
for name in required_closed:
    item = stats[name]
    if item["connectedComponents"] != 1 or item["boundaryEdges"] != 0 or item["nonManifoldEdges"] != 0:
        raise RuntimeError(f"Fail-closed topology gate for {name}: {item}")

rig_hash_after = rig_hash(armature)
if rig_hash_before != rig_hash_after:
    raise RuntimeError("Mika 151-bone rest rig changed")

# Adult SD proportion is measured from visible character surfaces only.
visible_points = [obj.matrix_world @ vertex.co for obj in objects for vertex in obj.data.vertices]
visible_low = Vector(tuple(min(point[axis] for point in visible_points) for axis in range(3)))
visible_high = Vector(tuple(max(point[axis] for point in visible_points) for axis in range(3)))
face_low = Vector(tuple(min(point[axis] for point in head_points_after) for axis in range(3)))
face_high = Vector(tuple(max(point[axis] for point in head_points_after) for axis in range(3)))
face_height = face_high.z - face_low.z
head_count = (visible_high.z - visible_low.z) / max(face_height, 1.0e-8)

# Render-only scene.
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1100
scene.render.resolution_y = 1400
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.world = bpy.data.worlds.new(PASS_LABEL + "_GrayWorld")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.038, 0.047, 0.060, 1.0)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.34
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.08
except TypeError:
    pass


def add_area(name, location, energy, size):
    data = bpy.data.lights.new(name + "Data", "AREA")
    data.energy = energy
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0.0, 0.0, 0.82)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


lights = [
    add_area("MotherR3_Key", (-3.2, -4.2, 3.8), 570.0, 3.4),
    add_area("MotherR3_Fill", (3.5, -2.2, 2.8), 320.0, 3.0),
    add_area("MotherR3_Rim", (0.0, 3.8, 3.3), 430.0, 3.0),
]
bpy.ops.mesh.primitive_plane_add(size=10.0, location=(0.0, 0.0, -0.291 if STYLE4 else -0.271))
ground = bpy.context.object
ground.name = "QA_RenderOnlyGround"
ground.data.materials.append(gray_material("MotherR3_Ground", 0.09, 0.94))

camera_data = bpy.data.cameras.new("MotherR3_ReviewCameraData")
camera = bpy.data.objects.new("MotherR3_ReviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.data.type = "ORTHO"
scene.camera = camera


def point_camera(location, target, scale):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = scale


views = {
    "front": ((0.0, -4.2, 0.77), (0.0, 0.0, 0.74), 2.29),
    "three-quarter": ((2.9, -3.45, 0.85), (0.0, 0.0, 0.74), 2.29),
    "side": ((4.25, 0.0, 0.80), (0.0, 0.0, 0.74), 2.29),
    "back": ((0.0, 4.2, 0.80), (0.0, 0.0, 0.74), 2.29),
}
solid_paths = []
for label, (location, target, scale) in views.items():
    point_camera(location, target, scale)
    path = OUTPUT / f"{FILE_STEM}-gray-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    solid_paths.append(path)

# One restrained color view exists only to compare style semantics against the
# 2D authority. Geometry is unchanged and the saved gate blend is restored to
# gray immediately afterward.
color_path = None
if STYLE4:
    color_palette = {
        MAT_FACE: color_material("MotherR4_FaceWarm", (0.91, 0.70, 0.61), 0.78),
        MAT_EYE: color_material("MotherR4_EyeBrown", (0.18, 0.085, 0.055), 0.62),
        MAT_BROW: color_material("MotherR4_BrowChestnutSoft", (0.25, 0.095, 0.055), 0.68),
        MAT_HAND: color_material("MotherR4_HandWarm", (0.91, 0.70, 0.61), 0.78),
        MAT_SKIN: color_material("MotherR4_LegWarm", (0.91, 0.70, 0.61), 0.78),
        MAT_CARDIGAN: color_material("MotherR4_CardiganPeach", (0.72, 0.31, 0.25), 0.84),
        MAT_BLOUSE: color_material("MotherR4_BlouseCream", (0.91, 0.84, 0.70), 0.82),
        MAT_SKIRT: color_material("MotherR4_SkirtTeal", (0.055, 0.31, 0.32), 0.84),
        MAT_HAIR: color_material("MotherR4_HairChestnut", (0.20, 0.065, 0.035), 0.80),
        MAT_SHOE: color_material("MotherR4_LoaferBrown", (0.16, 0.055, 0.028), 0.72),
        MAT_MOUTH: color_material("MotherR4_MouthRose", (0.40, 0.075, 0.075), 0.64),
    }
    saved_slots = {}
    for obj in objects:
        saved_slots[obj.name] = list(obj.data.materials)
        for index, material in enumerate(list(obj.data.materials)):
            if material in color_palette:
                obj.data.materials[index] = color_palette[material]
    point_camera(*views["front"])
    color_path = OUTPUT / f"{FILE_STEM}-color-authority-front.png"
    scene.render.filepath = str(color_path)
    bpy.ops.render.render(write_still=True)
    for obj in objects:
        for index, material in enumerate(saved_slots[obj.name]):
            obj.data.materials[index] = material


def wire_copy(source_object, label):
    obj = source_object.copy()
    obj.data = source_object.data.copy()
    obj.name = "QA_Wire_" + label
    bpy.context.collection.objects.link(obj)
    obj.data.materials.clear()
    obj.data.materials.append(MAT_WIRE)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
    modifier = obj.modifiers.new("QA_ActualSurfaceEdges", "WIREFRAME")
    modifier.thickness = 0.0022
    modifier.use_replace = True
    modifier.use_even_offset = True
    return obj


wire_sources = {
    "shoulder-underarm-weld": upper,
    "waist-midi-shell": skirt,
    "rear-hair-tips": hair,
    "closed-loafer-soles": left_shoe,
}
wire_objects = {label: wire_copy(obj, label) for label, obj in wire_sources.items()}
for obj in wire_objects.values():
    obj.hide_render = True
wire_views = {
    "shoulder-underarm-weld": ((2.4, -3.6, 0.96), (0.0, 0.0, 0.94), 0.82),
    "waist-midi-shell": ((2.4, -3.7, 0.48), (0.0, 0.0, 0.48), 0.82),
    "rear-hair-tips": ((2.4, 3.7, 1.38), (0.0, 0.12, 1.38), 0.88),
    "closed-loafer-soles": ((1.7, -3.0, -0.235 if STYLE4 else -0.205), (0.0, -0.03, -0.235 if STYLE4 else -0.205), 0.46),
}
wire_paths = []
for label, (location, target, scale) in wire_views.items():
    for obj in wire_objects.values():
        obj.hide_render = True
    wire_objects[label].hide_render = False
    point_camera(location, target, scale)
    path = OUTPUT / f"{FILE_STEM}-seam-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    wire_paths.append(path)
for obj in wire_objects.values():
    bpy.data.objects.remove(obj, do_unlink=True)

blend_path = OUTPUT / f"{FILE_STEM}-gray-gate.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

def donor_component_receipt(material, ids, components):
    records = []
    for component_id in ids:
        vertices = component_vertices(source.data, components[component_id])
        records.append(
            {
                "material": material,
                "componentId": component_id,
                "polygons": len(components[component_id]),
                "vertices": len(vertices),
                "bounds": world_bounds(source, vertices),
            }
        )
    return records


skirt_segments = 48
skirt_rings = 14
receipt = {
    "schema": "family-company.mother-retopo4-style.v1" if STYLE4 else "family-company.mother-retopo3.v1",
    "status": "AWAITING_ROOT_MOTHER_STYLE_GATE" if STYLE4 else "AWAITING_ROOT_RETOPO_GATE",
    "candidate": True,
    "claimScope": (
        "Mother garment/hair/loafer style and static gray topology gate; one restrained color authority comparison"
        if STYLE4
        else "static gray connected-retopology and adult silhouette gate only"
    ),
    "source": {
        "ownedMikaFbx": str(SOURCE_FBX.relative_to(REPO)).replace("\\", "/"),
        "ownedMikaSha256": sha256(SOURCE_FBX),
        "mother2DAuthority": str(AUTHORITY_NEUTRAL.relative_to(REPO)).replace("\\", "/"),
        "pixelAuthority": str(AUTHORITY_PIXEL.relative_to(REPO)).replace("\\", "/"),
        "ct1CageGeometryUsed": False,
        "boxOrPrimitiveStackingUsed": False,
        "test3Sakurako": "EXCLUDED_NOT_OPENED",
        "unityModified": False,
        "docsModified": False,
    },
    "preservation": {
        "faceEyes": "original Mika face/eyes retained exactly; gray materials only",
        "faceEyeCoordinateHashBefore": head_coord_hash_before,
        "faceEyeCoordinateHashAfter": head_coord_hash_after,
        "faceEyeWeightHashBefore": head_weight_hash_before,
        "faceEyeWeightHashAfter": head_weight_hash_after,
        "faceEyesExact": head_coord_hash_before == head_coord_hash_after and head_weight_hash_before == head_weight_hash_after,
        "hands": "original 3-digit stylized hand retained",
        "handCoordinateHashBefore": hand_coord_hash_before,
        "handCoordinateHashAfter": hand_coord_hash_after,
        "handWeightHashBefore": hand_weight_hash_before,
        "handWeightHashAfter": hand_weight_hash_after,
        "handsExact": hand_coord_hash_before == hand_coord_hash_after and hand_weight_hash_before == hand_weight_hash_after,
        "handPolygons": len(hand_polygons),
        "handComponentAudit": hand_component_audit,
        "rigBones": len(armature.data.bones),
        "rigHashBefore": rig_hash_before,
        "rigHashAfter": rig_hash_after,
        "rigExact": rig_hash_before == rig_hash_after,
        "opaqueMouthPlatePolygonsRemoved": len(opaque_mouth_polygons),
        "surfaceAttachedMouthFaces": len(mouth_faces),
    },
    "donorFitMap": {
        "upperTorsoAndSleeveSources": donor_component_receipt("CH0069_Body", [10, 11, 18, 19], body_components),
        "midiSkirtSources": donor_component_receipt("CH0069_Body", [12, 13, 14, 15], body_components),
        "shoeSources": donor_component_receipt("CH0069_Body", [22, 23], body_components),
        "hairSources": donor_component_receipt("CH0069_Hair", sorted(HAIR_COMPONENTS), hair_components),
    },
    "bridgeWeldAudit": {
        "upper": {
            "method": (
                "closed U-section open-cardigan retopo plus tapered cuffed sleeves voxel-welded at shoulder/underarm; fitted blouse remains beneath"
                if STYLE4
                else "three donor-fit ring lofts voxel-welded at shoulder/underarm overlap; construction seeds deleted"
            ),
            "preUnionClosedParts": 3,
            "preUnionStats": upper_pre_stats,
            "shoulderUnderarmWeldZones": 2,
            "postUnion": stats[upper.name],
            "realOpenFrontEdges": 2 if STYLE4 else 0,
            "fittedBlouseUnderlayer": stats[blouse.name] if blouse is not None else None,
        },
        "skirt": {
            "method": (
                "outer and inner A-line quad rings share indices; five low-amplitude vertical drape waves use existing loops; waist/hem explicitly bridged"
                if STYLE4
                else "outer and inner A-line quad rings share indices; explicit waist and hem loop bridges"
            ),
            "outerLongitudinalBridgeFaces": skirt_rings * skirt_segments,
            "innerLongitudinalBridgeFaces": skirt_rings * skirt_segments,
            "waistLoopBridgeFaces": skirt_segments,
            "hemLoopBridgeFaces": skirt_segments,
            "weldedBoundaryLoops": 2,
            "postBridge": stats[skirt.name],
        },
        "loafers": {
            "method": "each donor-fit shoe shell rebuilt as one closed rounded quad boundary with integral flat sole",
            "left": stats[left_shoe.name],
            "right": stats[right_shoe.name],
            "openSoleBoundaries": 0,
        },
        "legs": {
            "left": stats[left_leg.name],
            "right": stats[right_leg.name],
            "skirtOverlapWorldZ": [0.090 if STYLE4 else 0.115, 0.330],
            "loaferCollarOverlapWorldZ": [-0.180 if STYLE4 else -0.160, -0.171 if STYLE4 else -0.149],
        },
    },
    "hair": {
        "method": "direct deformation of coherent Mika hair component 0 plus donor bangs/face locks 1/3/4",
        "newHairPrimitiveOrSphere": 0,
        "sideBunAccessoryObjects": 0,
        "waistLengthDonorIslandsRetained": False,
        "tipRule": "staggered neck/shoulder hem with radial pull; no common horizontal compression plane",
        "embeddedHalfUp": (
            "same component 0 rear vertices more visibly pinched, twisted and displaced; crown softened; no separate bun"
            if STYLE4
            else "same component 0 rear vertices pinched and displaced; no separate bun"
        ),
        "topology": topology_stats(hair),
    },
    "retopoWeights": {
        obj.name: json.loads(obj["weightTransferReceipt"])
        for obj in ([upper, skirt, left_leg, right_leg, left_shoe, right_shoe, mouth] + ([blouse] if blouse is not None else []))
    },
    "adultProportion": {
        "visibleBounds": {
            "min": [round(float(value), 6) for value in visible_low],
            "max": [round(float(value), 6) for value in visible_high],
            "dimensions": [round(float(value), 6) for value in (visible_high - visible_low)],
        },
        "preservedFaceHeight": round(float(face_height), 6),
        "visibleHeightInPreservedFaceHeights": round(float(head_count), 6),
        "targetRange": [3.6, 4.0],
        "withinTarget": 3.6 <= head_count <= 4.0,
        "animationClaim": False,
    },
    "automaticGate": {
        "requiredSurfacesClosedConnected": all(
            stats[name]["connectedComponents"] == 1
            and stats[name]["boundaryEdges"] == 0
            and stats[name]["nonManifoldEdges"] == 0
            for name in required_closed
        ),
        "faceEyesExact": head_coord_hash_before == head_coord_hash_after and head_weight_hash_before == head_weight_hash_after,
        "handsExact": hand_coord_hash_before == hand_coord_hash_after and hand_weight_hash_before == hand_weight_hash_after,
        "rigExact151": len(armature.data.bones) == 151 and rig_hash_before == rig_hash_after,
        "adultRatioTarget": 3.6 <= head_count <= 4.0,
    },
    "proofs": {
        "grayViews": [str(path.relative_to(REPO)).replace("\\", "/") for path in solid_paths],
        "seamWireCloseups": [str(path.relative_to(REPO)).replace("\\", "/") for path in wire_paths],
        "gif": None,
        "colorAuthorityFront": str(color_path.relative_to(REPO)).replace("\\", "/") if color_path else None,
    },
    "excludedClaims": ["final shader/material fidelity", "face age morph", "animation deformation", "production readiness"],
    "blend": str(blend_path.relative_to(REPO)).replace("\\", "/"),
}
if not all(receipt["automaticGate"].values()):
    receipt["status"] = "DIAGNOSTIC_ONLY_AUTOMATIC_GATE_FAIL"
    receipt["candidate"] = False
receipt_path = OUTPUT / f"{FILE_STEM}-receipt.json"
receipt_path.write_text(json.dumps(receipt, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(json.dumps(receipt, indent=2, ensure_ascii=False))
